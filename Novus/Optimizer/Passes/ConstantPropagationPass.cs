using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Constant propagation pass
/// Replaces uses of variables with known constant values
/// Example: let x = 5; let y = x + 1; => let y = 5 + 1;
///
/// Also evaluates const fn calls with constant arguments at compile time.
/// Example: const fn double(x: i32) -> i32 { x * 2 }
///          let y = double(5); => let y = 10;
/// </summary>
public class ConstantPropagationPass : IBasicBlockPass
{
    public string Name => "Constant Propagation";

    // Module reference for const fn lookup
    private IrModule? _module;

    // Const fn evaluator (created lazily when module is available)
    private ConstFnEvaluator? _constFnEvaluator;

    // Cache of const fn call results for memoization
    private readonly Dictionary<string, IrConstant> _constFnCache = new();

    public bool Run(IrModule module)
    {
        _module = module;
        _constFnEvaluator = new ConstFnEvaluator(module);

        bool changed = false;
        foreach (var function in module.Functions)
        {
            foreach (var block in function.BasicBlocks)
            {
                changed |= RunOnBasicBlock(block);
            }
        }
        return changed;
    }

    /// <summary>
    /// Run constant propagation on a single basic block.
    /// Note: Const fn evaluation requires Run(IrModule) to be called first.
    /// When called directly without a module, const fn calls won't be evaluated.
    /// </summary>
    public bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;
        var constantValues = new Dictionary<string, IrConstant>();

        for (int i = 0; i < block.Instructions.Count; i++)
        {
            var instruction = block.Instructions[i];

            // Track assignments of constants to variables
            if (instruction is IrLocalDecl localDecl)
            {
                if (localDecl.InitialValue is IrConstant constant)
                {
                    constantValues[localDecl.Name] = constant;
                }
                else if (localDecl.InitialValue is IrVariable sourceVar && constantValues.ContainsKey(sourceVar.Name))
                {
                    // x = y where y is constant => x is also constant
                    localDecl.InitialValue = constantValues[sourceVar.Name];
                    constantValues[localDecl.Name] = constantValues[sourceVar.Name];
                    changed = true;
                }
                else
                {
                    // Non-constant assignment, remove from tracking
                    constantValues.Remove(localDecl.Name);
                }
            }
            else if (instruction is IrStore store)
            {
                // Store invalidates the variable's constant status
                constantValues.Remove(store.VariableName);

                // But if storing a constant, track it
                if (store.Value is IrConstant constant)
                {
                    constantValues[store.VariableName] = constant;
                }
                else if (store.Value is IrVariable sourceVar && constantValues.ContainsKey(sourceVar.Name))
                {
                    store.Value = constantValues[sourceVar.Name];
                    constantValues[store.VariableName] = constantValues[sourceVar.Name];
                    changed = true;
                }
            }
            else if (instruction is IrBinaryOp binOp)
            {
                // Replace left operand if it's a known constant
                if (binOp.Left is IrVariable leftVar && constantValues.ContainsKey(leftVar.Name))
                {
                    binOp.Left = constantValues[leftVar.Name];
                    changed = true;
                }

                // Replace right operand if it's a known constant
                if (binOp.Right is IrVariable rightVar && constantValues.ContainsKey(rightVar.Name))
                {
                    binOp.Right = constantValues[rightVar.Name];
                    changed = true;
                }

                // Track if this binary op assigns a result
                if (!string.IsNullOrEmpty(binOp.ResultName))
                {
                    // Invalidate the result variable (may be reassigned)
                    constantValues.Remove(binOp.ResultName);
                }
            }
            else if (instruction is IrReturn ret)
            {
                // Propagate constants into return statements
                if (ret.Value is IrVariable retVar && constantValues.ContainsKey(retVar.Name))
                {
                    ret.Value = constantValues[retVar.Name];
                    changed = true;
                }
            }
            else if (instruction is IrCall call)
            {
                // Propagate constants into call arguments
                for (int j = 0; j < call.Arguments.Count; j++)
                {
                    if (call.Arguments[j] is IrVariable argVar && constantValues.ContainsKey(argVar.Name))
                    {
                        call.Arguments[j] = constantValues[argVar.Name];
                        changed = true;
                    }
                }

                // Try to evaluate const fn calls at compile time
                if (call.ResultName != null && TryEvaluateConstFnCall(call, constantValues, out var constResult))
                {
                    // Replace call with constant store
                    block.Instructions[i] = new IrStore(call.ResultName, constResult!);
                    constantValues[call.ResultName] = constResult!;
                    changed = true;
                }
                else if (IsConstFn(call.FunctionName))
                {
                    // Const fn with non-constant args: result not known, but no side effects
                    // so we don't need to clear constantValues
                    if (call.ResultName != null)
                    {
                        constantValues.Remove(call.ResultName);
                    }
                }
                else
                {
                    // Non-const function call may have side effects - clear all tracked constants
                    constantValues.Clear();
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Check if a function is a const fn (no side effects).
    /// </summary>
    private bool IsConstFn(string functionName)
    {
        if (_module == null) return false;
        var function = _module.Functions.FirstOrDefault(f => f.Name == functionName);
        return function?.IsConstFn ?? false;
    }

    /// <summary>
    /// Try to evaluate a const fn call at compile time.
    /// </summary>
    private bool TryEvaluateConstFnCall(IrCall call, Dictionary<string, IrConstant> constantValues, out IrConstant? result)
    {
        result = null;

        if (_module == null || _constFnEvaluator == null)
            return false;

        // Check if this is a const fn
        var function = _module.Functions.FirstOrDefault(f => f.Name == call.FunctionName);
        if (function == null || !function.IsConstFn)
            return false;

        // Check if all arguments are constants
        var constArgs = new List<object?>();
        foreach (var arg in call.Arguments)
        {
            if (arg is IrConstant constant)
            {
                constArgs.Add(constant.Value);
            }
            else if (arg is IrVariable variable && constantValues.TryGetValue(variable.Name, out var varConst))
            {
                constArgs.Add(varConst.Value);
            }
            else if (arg is IrBoolConstant boolConst)
            {
                constArgs.Add(boolConst.Value ? 1L : 0L);
            }
            else
            {
                // Non-constant argument
                return false;
            }
        }

        // Check cache for memoization
        var cacheKey = $"{call.FunctionName}({string.Join(",", constArgs)})";
        if (_constFnCache.TryGetValue(cacheKey, out var cached))
        {
            result = cached;
            return true;
        }

        // Evaluate the const fn
        var evalResult = _constFnEvaluator.Evaluate(call.FunctionName, constArgs);
        if (!evalResult.Success)
            return false;

        // Convert result to IrConstant
        if (evalResult.Value is long longVal)
        {
            result = new IrConstant(longVal, evalResult.ValueType ?? call.ReturnType ?? new IrIntType(32, true));
        }
        else if (evalResult.Value is int intVal)
        {
            result = new IrConstant(intVal, evalResult.ValueType ?? call.ReturnType ?? new IrIntType(32, true));
        }
        else if (evalResult.Value is bool boolVal)
        {
            result = new IrConstant(boolVal ? 1 : 0, evalResult.ValueType ?? IrBoolType.Instance);
        }
        else
        {
            return false;
        }

        // Cache the result
        _constFnCache[cacheKey] = result;
        return true;
    }
}
