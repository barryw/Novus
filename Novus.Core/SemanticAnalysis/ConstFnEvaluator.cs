using Novus.IR;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Result of evaluating a const function at compile time.
/// Contains either a successful value or an error message.
/// </summary>
public class ConstFnResult
{
    public bool Success { get; }
    public object? Value { get; }
    public IrType? ValueType { get; }
    public string? Error { get; }

    private ConstFnResult(bool success, object? value, IrType? valueType, string? error)
    {
        Success = success;
        Value = value;
        ValueType = valueType;
        Error = error;
    }

    public static ConstFnResult Ok(object? value, IrType valueType) => new(true, value, valueType, null);
    public static ConstFnResult Err(string error) => new(false, null, null, error);
}

/// <summary>
/// Evaluates const functions at compile time by interpreting their IR.
///
/// Const functions have the following restrictions:
/// - No side effects (no I/O, no mutable statics)
/// - No heap allocations
/// - No unsafe blocks
/// - Can only call other const functions
/// - No loops (for now - could support bounded loops later)
///
/// The evaluator interprets the IR instructions directly, tracking variable
/// values and control flow to compute the return value.
/// </summary>
public class ConstFnEvaluator
{
    private readonly IrModule _module;
    private readonly Dictionary<string, object?> _variables = new();
    private readonly Dictionary<string, IrFunction> _constFunctions = new();
    private readonly int _maxSteps;
    private int _stepCount;

    /// <summary>
    /// Maximum recursion depth for const function calls
    /// </summary>
    private const int MaxRecursionDepth = 100;
    private int _recursionDepth;

    public ConstFnEvaluator(IrModule module, int maxSteps = 10000)
    {
        _module = module;
        _maxSteps = maxSteps;

        // Cache all const functions for quick lookup
        foreach (var func in module.Functions)
        {
            if (func.IsConstFn)
            {
                _constFunctions[func.Name] = func;
            }
        }
    }

    /// <summary>
    /// Evaluate a const function call with the given arguments.
    /// </summary>
    public ConstFnResult Evaluate(string functionName, List<object?> arguments)
    {
        if (!_constFunctions.TryGetValue(functionName, out var function))
        {
            return ConstFnResult.Err($"Function '{functionName}' is not a const fn");
        }

        if (arguments.Count != function.Parameters.Count)
        {
            return ConstFnResult.Err($"Expected {function.Parameters.Count} arguments, got {arguments.Count}");
        }

        _recursionDepth++;
        if (_recursionDepth > MaxRecursionDepth)
        {
            _recursionDepth--;
            return ConstFnResult.Err($"Maximum recursion depth ({MaxRecursionDepth}) exceeded in const fn evaluation");
        }

        // Save current variables for nested calls
        var savedVariables = new Dictionary<string, object?>(_variables);

        try
        {
            // Bind arguments to parameters
            _variables.Clear();
            for (int i = 0; i < arguments.Count; i++)
            {
                _variables[function.Parameters[i].Name] = arguments[i];
            }

            // Execute the function
            return ExecuteFunction(function);
        }
        finally
        {
            _recursionDepth--;
            // Restore previous variables
            _variables.Clear();
            foreach (var kvp in savedVariables)
            {
                _variables[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Evaluate a const function with typed IrValue arguments (from constant expressions).
    /// </summary>
    public ConstFnResult Evaluate(IrFunction function, List<IrValue> arguments)
    {
        if (!function.IsConstFn)
        {
            return ConstFnResult.Err($"Function '{function.Name}' is not a const fn");
        }

        if (arguments.Count != function.Parameters.Count)
        {
            return ConstFnResult.Err($"Expected {function.Parameters.Count} arguments, got {arguments.Count}");
        }

        _recursionDepth++;
        if (_recursionDepth > MaxRecursionDepth)
        {
            _recursionDepth--;
            return ConstFnResult.Err($"Maximum recursion depth ({MaxRecursionDepth}) exceeded in const fn evaluation");
        }

        // Save current variables for nested calls
        var savedVariables = new Dictionary<string, object?>(_variables);

        try
        {
            // Bind arguments to parameters
            _variables.Clear();
            for (int i = 0; i < arguments.Count; i++)
            {
                var argValue = EvaluateValue(arguments[i]);
                // Allow null for pointer arguments (e.g., null literal)
                if (argValue == null && arguments[i] is not IrConstant { Value: 0 })
                {
                    return ConstFnResult.Err($"Could not evaluate argument {i} at compile time");
                }
                _variables[function.Parameters[i].Name] = argValue;
            }

            // Execute the function
            return ExecuteFunction(function);
        }
        finally
        {
            _recursionDepth--;
            // Restore previous variables
            _variables.Clear();
            foreach (var kvp in savedVariables)
            {
                _variables[kvp.Key] = kvp.Value;
            }
        }
    }

    private ConstFnResult ExecuteFunction(IrFunction function)
    {
        if (function.BasicBlocks.Count == 0)
        {
            return ConstFnResult.Err($"Const function '{function.Name}' has no body");
        }

        // Start from entry block at instruction index 0
        var currentBlock = function.BasicBlocks[0];
        int currentInstrIndex = 0;
        _stepCount = 0;

        while (currentBlock != null)
        {
            while (currentInstrIndex < currentBlock.Instructions.Count)
            {
                var instruction = currentBlock.Instructions[currentInstrIndex];
                currentInstrIndex++;
                _stepCount++;

                if (_stepCount > _maxSteps)
                {
                    return ConstFnResult.Err($"Const evaluation exceeded maximum steps ({_maxSteps}). Possible infinite loop?");
                }

                // Check if this instruction is a terminator or label
                if (instruction is IrLabel)
                {
                    // Just skip labels - they're targets for branches
                    continue;
                }

                if (instruction is IrReturn ret)
                {
                    if (ret.Value == null)
                    {
                        return ConstFnResult.Ok(null, IrVoidType.Instance);
                    }
                    var returnValue = EvaluateValue(ret.Value);
                    return ConstFnResult.Ok(returnValue, ret.Value.Type);
                }

                if (instruction is IrBranch branch)
                {
                    // Try to find a separate block first
                    var targetBlock = FindBlock(function, branch.Target);
                    if (targetBlock != null)
                    {
                        currentBlock = targetBlock;
                        currentInstrIndex = 0;
                    }
                    else
                    {
                        // Look for label within current block
                        var labelIdx = FindLabelIndex(currentBlock, branch.Target);
                        if (labelIdx >= 0)
                        {
                            currentInstrIndex = labelIdx + 1; // Start after the label
                        }
                        else
                        {
                            return ConstFnResult.Err($"Branch target '{branch.Target}' not found");
                        }
                    }
                    continue;
                }

                if (instruction is IrConditionalBranch condBranch)
                {
                    var condition = EvaluateValue(condBranch.Condition);
                    bool condBool;
                    if (condition is bool b)
                    {
                        condBool = b;
                    }
                    else if (condition is long l)
                    {
                        condBool = l != 0;
                    }
                    else if (condition is int i)
                    {
                        condBool = i != 0;
                    }
                    else
                    {
                        var condVar = condBranch.Condition as IrVariable;
                        var varName = condVar?.Name ?? "unknown";
                        var availableVars = string.Join(", ", _variables.Keys);
                        return ConstFnResult.Err($"Conditional branch condition is not a boolean: got {condition?.GetType().Name ?? "null"}, var name: '{varName}', available: [{availableVars}]");
                    }

                    var targetLabel = condBool ? condBranch.TrueTarget : condBranch.FalseTarget;

                    // Try to find a separate block first
                    var targetBlock = FindBlock(function, targetLabel);
                    if (targetBlock != null)
                    {
                        currentBlock = targetBlock;
                        currentInstrIndex = 0;
                    }
                    else
                    {
                        // Look for label within current block
                        var labelIdx = FindLabelIndex(currentBlock, targetLabel);
                        if (labelIdx >= 0)
                        {
                            currentInstrIndex = labelIdx + 1; // Start after the label
                        }
                        else
                        {
                            return ConstFnResult.Err($"Branch target '{targetLabel}' not found");
                        }
                    }
                    continue;
                }

                var result = ExecuteInstruction(instruction, function);
                if (result != null)
                {
                    // Error occurred
                    return result;
                }
            }

            // Reached end of current block - try to fall through to next block
            var idx = function.BasicBlocks.IndexOf(currentBlock);
            if (idx + 1 < function.BasicBlocks.Count)
            {
                currentBlock = function.BasicBlocks[idx + 1];
                currentInstrIndex = 0;
            }
            else
            {
                currentBlock = null;
            }
        }

        // Reached end without return
        return ConstFnResult.Ok(null, IrVoidType.Instance);
    }

    private IrBasicBlock? FindBlock(IrFunction function, string label)
    {
        return function.BasicBlocks.FirstOrDefault(b => b.Label == label);
    }

    /// <summary>
    /// Find the index of an IrLabel instruction within a block.
    /// Returns -1 if not found.
    /// </summary>
    private int FindLabelIndex(IrBasicBlock block, string label)
    {
        for (int i = 0; i < block.Instructions.Count; i++)
        {
            if (block.Instructions[i] is IrLabel lbl && lbl.Name == label)
            {
                return i;
            }
        }
        return -1;
    }

    private ConstFnResult? ExecuteInstruction(IrInstruction instruction, IrFunction function)
    {
        switch (instruction)
        {
            case IrLocalDecl localDecl:
                _variables[localDecl.Name] = localDecl.InitialValue != null
                    ? EvaluateValue(localDecl.InitialValue)
                    : GetDefaultValue(localDecl.Type);
                return null;

            case IrStore store:
                _variables[store.VariableName] = EvaluateValue(store.Value);
                return null;

            case IrBinaryOp binOp:
                var left = EvaluateValue(binOp.Left);
                var right = EvaluateValue(binOp.Right);
                if (left == null || right == null)
                {
                    return ConstFnResult.Err($"Binary operation operands cannot be evaluated: left={left?.GetType().Name ?? "null"}, right={right?.GetType().Name ?? "null"}, leftVal={binOp.Left?.GetType().Name}, rightVal={binOp.Right?.GetType().Name}");
                }
                var result = EvaluateBinaryOp(binOp.Operation, left, right, binOp.Type);
                if (result == null)
                {
                    return ConstFnResult.Err($"Binary operation '{binOp.Operation}' returned null for {left.GetType().Name} and {right.GetType().Name}");
                }
                _variables[binOp.ResultName] = result;
                return null;

            case IrCall call:
                // Check if calling another const function
                if (_constFunctions.TryGetValue(call.FunctionName, out var callee))
                {
                    var args = call.Arguments.Select(a => EvaluateValue(a)).ToList();
                    var callResult = Evaluate(call.FunctionName, args);
                    if (!callResult.Success)
                    {
                        return callResult;
                    }
                    if (call.ResultName != null)
                    {
                        _variables[call.ResultName] = callResult.Value;
                    }
                    return null;
                }
                return ConstFnResult.Err($"Cannot call non-const function '{call.FunctionName}' from const context");

            case IrReturn ret:
                // Handled by terminator processing
                return null;

            case IrBranch:
            case IrConditionalBranch:
                // Handled by terminator processing
                return null;

            case IrPhi phi:
                // PHI nodes in const evaluation - we need to track which block we came from
                // For now, skip - proper PHI handling would require tracking predecessor
                return null;

            case IrDefer:
                return ConstFnResult.Err("defer is not allowed in const fn");

            case IrPanic:
                return ConstFnResult.Err("panic! is not allowed in const fn");

            case IrAssert assert:
                // Evaluate assert at compile time
                var cond = EvaluateValue(assert.Condition);
                if (cond is bool b && !b)
                {
                    return ConstFnResult.Err($"Compile-time assertion failed: {assert.Message ?? "assertion failed"}");
                }
                return null;

            case IrDereferenceStore:
                return ConstFnResult.Err("Pointer dereference store is not allowed in const fn");

            default:
                return ConstFnResult.Err($"Unsupported instruction type in const fn: {instruction.GetType().Name}");
        }
    }

    private object? EvaluateValue(IrValue value)
    {
        switch (value)
        {
            case IrConstant c: return c.Value;
            case IrBoolConstant b: return b.Value;
            case IrFloatConstant f: return f.Value;
            case IrFixedConstant fx: return fx.Value;
            case IrVariable v:
                if (_variables.TryGetValue(v.Name, out var val))
                    return val;
                // Variable not found - this can happen if comparison result wasn't stored
                // Return null and let caller handle the error
                return null;
            case IrSizeOf sz: return sz.TargetType.SizeInBytes;
            // null pointer is represented as IrConstant(0) with pointer type
            default: return null;
        }
    }

    private object? EvaluateBinaryOp(IrBinaryOp.OpKind op, object? left, object? right, IrType resultType)
    {
        // Handle integer operations
        if (left is long lLong && right is long rLong)
        {
            return op switch
            {
                IrBinaryOp.OpKind.Add => lLong + rLong,
                IrBinaryOp.OpKind.Sub => lLong - rLong,
                IrBinaryOp.OpKind.Mul => lLong * rLong,
                IrBinaryOp.OpKind.Div => rLong != 0 ? lLong / rLong : 0L,
                IrBinaryOp.OpKind.Mod => rLong != 0 ? lLong % rLong : 0L,
                IrBinaryOp.OpKind.And => lLong & rLong,
                IrBinaryOp.OpKind.Or => lLong | rLong,
                IrBinaryOp.OpKind.Xor => lLong ^ rLong,
                IrBinaryOp.OpKind.Shl => lLong << (int)rLong,
                IrBinaryOp.OpKind.Shr => lLong >> (int)rLong,
                IrBinaryOp.OpKind.Eq => lLong == rLong,
                IrBinaryOp.OpKind.Ne => lLong != rLong,
                IrBinaryOp.OpKind.Lt => lLong < rLong,
                IrBinaryOp.OpKind.Le => lLong <= rLong,
                IrBinaryOp.OpKind.Gt => lLong > rLong,
                IrBinaryOp.OpKind.Ge => lLong >= rLong,
                _ => null
            };
        }

        // Handle float operations
        if (left is double lDouble && right is double rDouble)
        {
            return op switch
            {
                IrBinaryOp.OpKind.Add => lDouble + rDouble,
                IrBinaryOp.OpKind.Sub => lDouble - rDouble,
                IrBinaryOp.OpKind.Mul => lDouble * rDouble,
                IrBinaryOp.OpKind.Div => rDouble != 0 ? lDouble / rDouble : 0.0,
                IrBinaryOp.OpKind.Eq => lDouble == rDouble,
                IrBinaryOp.OpKind.Ne => lDouble != rDouble,
                IrBinaryOp.OpKind.Lt => lDouble < rDouble,
                IrBinaryOp.OpKind.Le => lDouble <= rDouble,
                IrBinaryOp.OpKind.Gt => lDouble > rDouble,
                IrBinaryOp.OpKind.Ge => lDouble >= rDouble,
                _ => null
            };
        }

        // Handle boolean operations
        if (left is bool lBool && right is bool rBool)
        {
            return op switch
            {
                IrBinaryOp.OpKind.And => lBool && rBool,
                IrBinaryOp.OpKind.Or => lBool || rBool,
                IrBinaryOp.OpKind.Eq => lBool == rBool,
                IrBinaryOp.OpKind.Ne => lBool != rBool,
                _ => null
            };
        }

        // Mixed numeric types - convert to long
        if (IsNumeric(left) && IsNumeric(right))
        {
            var l = ToLong(left);
            var r = ToLong(right);
            return EvaluateBinaryOp(op, l, r, resultType);
        }

        return null;
    }

    private static bool IsNumeric(object? value) =>
        value is long or int or short or sbyte or ulong or uint or ushort or byte or double or float;

    private static long ToLong(object? value) => value switch
    {
        long l => l,
        int i => i,
        short s => s,
        sbyte sb => sb,
        ulong ul => (long)ul,
        uint ui => ui,
        ushort us => us,
        byte b => b,
        double d => (long)d,
        float f => (long)f,
        _ => 0L
    };

    private static object? GetDefaultValue(IrType type) => type switch
    {
        IrIntType => 0L,
        IrBoolType => false,
        IrFloatType => 0.0,
        IrFixedType => 0.0,
        _ => null
    };

    /// <summary>
    /// Validate that a function can be a const fn (has no disallowed operations).
    /// </summary>
    public static List<string> ValidateConstFn(IrFunction function)
    {
        var errors = new List<string>();

        if (function.BasicBlocks.Count == 0)
        {
            errors.Add("const fn must have a body");
            return errors;
        }

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                switch (instruction)
                {
                    case IrDefer:
                        errors.Add("defer is not allowed in const fn");
                        break;
                    case IrPanic:
                        errors.Add("panic! is not allowed in const fn");
                        break;
                    case IrDereferenceStore:
                        errors.Add("Pointer writes are not allowed in const fn");
                        break;
                    // Note: IrCall validation would require checking if the callee is also const
                    // This is done at evaluation time, not validation time
                }
            }
        }

        return errors;
    }
}
