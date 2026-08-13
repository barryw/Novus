using Novus.SemanticAnalysis;

namespace Novus.IR;

/// <summary>
/// Constant Propagation optimization pass.
/// Replaces variable uses with known constant values.
///
/// In SSA form, this is particularly effective because:
/// - Each variable is defined exactly once
/// - We can easily determine if a definition is a constant
/// - Constant folding opportunities are immediately visible
///
/// Algorithm:
/// 1. Find all variables defined as constants
/// 2. Replace all uses of those variables with the constant value
/// 3. Perform constant folding on binary operations when both operands are constant
/// 4. Evaluate const fn calls with constant arguments at compile time
///
/// Example:
///   x_0 = 5
///   y_0 = x_0 + 3    // x_0 is constant
///   z_0 = y_0 * 2    // y_0 becomes constant after folding
///
/// After constant propagation:
///   x_0 = 5
///   y_0 = 8          // 5 + 3 folded
///   z_0 = 16         // 8 * 2 folded
///
/// Const fn optimization:
///   const fn double(x: i32) -> i32 { x * 2 }
///   y = double(5)    // Call with constant arg
///
/// After const fn evaluation:
///   y = 10           // Evaluated at compile time
///
/// Combined with DCE, the x_0 and y_0 definitions may also be eliminated if unused elsewhere.
/// </summary>
public class ConstantPropagation
{
    private readonly IrFunction _function;
    private readonly IrModule? _module;
    private ConstFnEvaluator? _constFnEvaluator;

    /// <summary>
    /// Map from variable SSA name to its constant value (if known)
    /// </summary>
    private Dictionary<string, IrConstant> _constantValues = new();
    private readonly Dictionary<string, int> _definitionCounts = new();

    /// <summary>
    /// Cache of const fn call results: (functionName, args) -> result
    /// This enables memoization of repeated const fn calls
    /// </summary>
    private Dictionary<string, IrConstant> _constFnCache = new();

    /// <summary>
    /// Track which instructions were modified (for iteration)
    /// </summary>
    private bool _madeChanges = false;

    /// <summary>
    /// Statistics for const fn optimizations
    /// </summary>
    public int ConstFnEvaluations { get; private set; }
    public int ConstFnCacheHits { get; private set; }

    public ConstantPropagation(IrFunction function, IrModule? module = null)
    {
        _function = function;
        _module = module;

        // Create const fn evaluator if we have a module
        if (_module != null)
        {
            _constFnEvaluator = new ConstFnEvaluator(_module);
        }
    }

    /// <summary>
    /// Run constant propagation until no more changes can be made
    /// Returns the number of constants propagated
    /// </summary>
    public int Propagate()
    {
        int totalPropagations = 0;

        // Iterate until no more changes
        do
        {
            _madeChanges = false;
            _constantValues.Clear();
            CountDefinitions();

            // Pass 1: Identify constant definitions
            IdentifyConstants();

            // Pass 2: Propagate constants to uses and fold operations
            int propagations = PropagateConstants();
            totalPropagations += propagations;

        } while (_madeChanges);

        return totalPropagations;
    }

    /// <summary>
    /// Identify all variables that are defined as constants
    /// </summary>
    private void IdentifyConstants()
    {
        foreach (var block in _function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                switch (instruction)
                {
                    case IrLocalDecl decl when decl.InitialValue is IrConstant constant && IsSingleDefinition(decl.Name):
                        _constantValues[decl.Name] = constant;
                        break;

                    case IrStore store when store.Value is IrConstant constant && IsSingleDefinition(store.VariableName):
                        _constantValues[store.VariableName] = constant;
                        break;

                    case IrBinaryOp binOp when IsSingleDefinition(binOp.ResultName) && CanFoldBinaryOp(binOp, out var folded):
                        _constantValues[binOp.ResultName] = folded!;
                        break;

                    case IrCall call when call.ResultName != null && IsSingleDefinition(call.ResultName):
                        // Check if this is a const fn call with all constant arguments
                        if (TryEvaluateConstFnCall(call, out var constResult))
                        {
                            _constantValues[call.ResultName] = constResult!;
                        }
                        break;
                }
            }
        }
    }

    private void CountDefinitions()
    {
        _definitionCounts.Clear();
        foreach (var instruction in _function.BasicBlocks.SelectMany(block => block.Instructions))
        {
            var name = instruction switch
            {
                IrLocalDecl decl => decl.Name,
                IrStore store => store.VariableName,
                IrBinaryOp binary => binary.ResultName,
                IrCall call => call.ResultName,
                IrIndexAccess access => access.ResultName,
                IrMemberAccess access => access.ResultName,
                IrExtractTag extract => extract.ResultName,
                IrExtractVariantData extract => extract.ResultName,
                IrCreateClosure closure => closure.ResultName,
                IrLoadCapture capture => capture.ResultName,
                _ => null
            };
            if (name != null)
                _definitionCounts[name] = _definitionCounts.GetValueOrDefault(name) + 1;
        }
    }

    private bool IsSingleDefinition(string name) => _definitionCounts.GetValueOrDefault(name) == 1;

    /// <summary>
    /// Propagate constants throughout the function
    /// </summary>
    private int PropagateConstants()
    {
        int propagationCount = 0;

        foreach (var block in _function.BasicBlocks)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var instruction = block.Instructions[i];
                var modified = PropagateInInstruction(instruction);
                if (modified != null)
                {
                    block.Instructions[i] = modified;
                    propagationCount++;
                    _madeChanges = true;
                }
            }

            // Also propagate in phi functions
            for (int i = 0; i < block.PhiFunctions.Count; i++)
            {
                var phi = block.PhiFunctions[i];
                bool changed = false;

                for (int j = 0; j < phi.IncomingValues.Count; j++)
                {
                    var newValue = PropagateInValue(phi.IncomingValues[j]);
                    if (newValue != phi.IncomingValues[j])
                    {
                        phi.IncomingValues[j] = newValue;
                        changed = true;
                    }
                }

                if (changed)
                {
                    propagationCount++;
                    _madeChanges = true;
                }
            }
        }

        return propagationCount;
    }

    /// <summary>
    /// Propagate constants in a single instruction
    /// Returns the modified instruction, or null if no changes
    /// </summary>
    private IrInstruction? PropagateInInstruction(IrInstruction instruction)
    {
        bool changed = false;

        switch (instruction)
        {
            case IrLocalDecl decl:
                {
                    if (decl.InitialValue != null)
                    {
                        var newValue = PropagateInValue(decl.InitialValue);
                        if (newValue != decl.InitialValue)
                        {
                            decl.InitialValue = newValue;
                            changed = true;
                        }
                    }
                    break;
                }

            case IrStore store:
                {
                    var newValue = PropagateInValue(store.Value);
                    if (newValue != store.Value)
                    {
                        store.Value = newValue;
                        changed = true;
                    }
                    break;
                }

            case IrBinaryOp binOp:
                {
                    var newLeft = PropagateInValue(binOp.Left);
                    var newRight = PropagateInValue(binOp.Right);

                    if (newLeft != binOp.Left || newRight != binOp.Right)
                    {
                        binOp.Left = newLeft;
                        binOp.Right = newRight;
                        changed = true;

                        // Try to fold the operation now that we have constants
                        if (newLeft is IrConstant leftConst && newRight is IrConstant rightConst)
                        {
                            var folded = FoldBinaryOp(binOp.Operation, leftConst, rightConst, binOp.Type);
                            if (folded != null)
                            {
                                // Replace binary op with constant store
                                return new IrStore(binOp.ResultName, folded);
                            }
                        }
                    }
                    break;
                }

            case IrReturn ret when ret.Value != null:
                {
                    var newValue = PropagateInValue(ret.Value);
                    if (newValue != ret.Value)
                    {
                        ret.Value = newValue;
                        changed = true;
                    }
                    break;
                }

            case IrConditionalBranch condBranch:
                {
                    var newCond = PropagateInValue(condBranch.Condition);
                    if (newCond != condBranch.Condition)
                    {
                        condBranch.Condition = newCond;
                        changed = true;

                        // ENHANCEMENT: If condition is now constant, simplify to unconditional branch
                        if (newCond is IrConstant condConst)
                        {
                            // Constant condition - replace with unconditional branch
                            if (condConst.Value != 0)
                            {
                                // Always true - branch to true target
                                return new IrBranch(condBranch.TrueTarget);
                            }
                            else
                            {
                                // Always false - branch to false target
                                return new IrBranch(condBranch.FalseTarget);
                            }
                        }

                        // Also check for boolean constants
                        if (newCond is IrBoolConstant boolConst)
                        {
                            if (boolConst.Value)
                            {
                                return new IrBranch(condBranch.TrueTarget);
                            }
                            else
                            {
                                return new IrBranch(condBranch.FalseTarget);
                            }
                        }
                    }
                    break;
                }

            case IrCall call:
                {
                    // Propagate constants in arguments
                    for (int i = 0; i < call.Arguments.Count; i++)
                    {
                        var newArg = PropagateInValue(call.Arguments[i]);
                        if (newArg != call.Arguments[i])
                        {
                            call.Arguments[i] = newArg;
                            changed = true;
                        }
                    }

                    // Try to evaluate const fn call at compile time
                    if (call.ResultName != null && TryEvaluateConstFnCall(call, out var constResult))
                    {
                        // Replace the call with a constant store
                        return new IrStore(call.ResultName, constResult!);
                    }
                    break;
                }

            case IrIndirectCall indirectCall:
                {
                    var newFp = PropagateInValue(indirectCall.FunctionPointer);
                    if (newFp != indirectCall.FunctionPointer)
                    {
                        indirectCall.FunctionPointer = newFp;
                        changed = true;
                    }

                    for (int i = 0; i < indirectCall.Arguments.Count; i++)
                    {
                        var newArg = PropagateInValue(indirectCall.Arguments[i]);
                        if (newArg != indirectCall.Arguments[i])
                        {
                            indirectCall.Arguments[i] = newArg;
                            changed = true;
                        }
                    }
                    break;
                }

            case IrIndexAccess indexAccess:
                {
                    var newArray = PropagateInValue(indexAccess.Array);
                    var newIndex = PropagateInValue(indexAccess.Index);
                    var newLength = indexAccess.Length == null ? null : PropagateInValue(indexAccess.Length);
                    if (newArray != indexAccess.Array || newIndex != indexAccess.Index || newLength != indexAccess.Length)
                    {
                        indexAccess.Array = newArray;
                        indexAccess.Index = newIndex;
                        indexAccess.Length = newLength;
                        changed = true;
                    }
                    break;
                }

            case IrSliceBoundsCheck sliceCheck:
                {
                    var start = PropagateInValue(sliceCheck.Start);
                    var end = PropagateInValue(sliceCheck.End);
                    var length = PropagateInValue(sliceCheck.Length);
                    if (start != sliceCheck.Start || end != sliceCheck.End || length != sliceCheck.Length)
                    {
                        sliceCheck.Start = start;
                        sliceCheck.End = end;
                        sliceCheck.Length = length;
                        changed = true;
                    }
                    break;
                }

            case IrMemberAccess memberAccess:
                {
                    var newStruct = PropagateInValue(memberAccess.Struct);
                    if (newStruct != memberAccess.Struct)
                    {
                        memberAccess.Struct = newStruct;
                        changed = true;
                    }
                    break;
                }

            case IrMemberStore memberStore:
                {
                    var newStruct = PropagateInValue(memberStore.Struct);
                    var newValue = PropagateInValue(memberStore.Value);
                    if (newStruct != memberStore.Struct || newValue != memberStore.Value)
                    {
                        memberStore.Struct = newStruct;
                        memberStore.Value = newValue;
                        changed = true;
                    }
                    break;
                }

            case IrIndexStore indexStore:
                {
                    var newArray = PropagateInValue(indexStore.Array);
                    var newIndex = PropagateInValue(indexStore.Index);
                    var newValue = PropagateInValue(indexStore.Value);
                    var newLength = indexStore.Length == null ? null : PropagateInValue(indexStore.Length);
                    if (newArray != indexStore.Array || newIndex != indexStore.Index || newValue != indexStore.Value || newLength != indexStore.Length)
                    {
                        indexStore.Array = newArray;
                        indexStore.Index = newIndex;
                        indexStore.Value = newValue;
                        indexStore.Length = newLength;
                        changed = true;
                    }
                    break;
                }

            case IrDereferenceStore derefStore:
                {
                    var newPointer = PropagateInValue(derefStore.Pointer);
                    var newValue = PropagateInValue(derefStore.Value);
                    if (newPointer != derefStore.Pointer || newValue != derefStore.Value)
                    {
                        derefStore.Pointer = newPointer;
                        derefStore.Value = newValue;
                        changed = true;
                    }
                    break;
                }

            case IrAssert assert:
                {
                    var newCond = PropagateInValue(assert.Condition);
                    if (newCond != assert.Condition)
                    {
                        assert.Condition = newCond;
                        changed = true;
                    }
                    break;
                }

            case IrExtractTag extractTag:
                {
                    var newEnum = PropagateInValue(extractTag.EnumValue);
                    if (newEnum != extractTag.EnumValue)
                    {
                        extractTag.EnumValue = newEnum;
                        changed = true;
                    }
                    break;
                }

            case IrExtractVariantData extractData:
                {
                    var newEnum = PropagateInValue(extractData.EnumValue);
                    if (newEnum != extractData.EnumValue)
                    {
                        extractData.EnumValue = newEnum;
                        changed = true;
                    }
                    break;
                }
        }

        return changed ? instruction : null;
    }

    /// <summary>
    /// Propagate constants in a value
    /// </summary>
    private IrValue PropagateInValue(IrValue value)
    {
        if (value is IrVariable variable)
        {
            var varName = variable.SsaName;
            if (_constantValues.TryGetValue(varName, out var constant))
            {
                return constant;
            }
        }
        return value;
    }

    /// <summary>
    /// Check if a binary operation can be folded to a constant
    /// </summary>
    private bool CanFoldBinaryOp(IrBinaryOp binOp, out IrConstant? result)
    {
        result = null;

        // First propagate constants in operands
        var left = PropagateInValue(binOp.Left);
        var right = PropagateInValue(binOp.Right);

        if (left is IrConstant leftConst && right is IrConstant rightConst)
        {
            result = FoldBinaryOp(binOp.Operation, leftConst, rightConst, binOp.Type);
            return result != null;
        }

        return false;
    }

    /// <summary>
    /// Fold a binary operation with constant operands
    /// </summary>
    private IrConstant? FoldBinaryOp(IrBinaryOp.OpKind op, IrConstant left, IrConstant right, IrType resultType)
    {
        long leftVal = left.Value;
        long rightVal = right.Value;

        long? result = op switch
        {
            IrBinaryOp.OpKind.Add => leftVal + rightVal,
            IrBinaryOp.OpKind.Sub => leftVal - rightVal,
            IrBinaryOp.OpKind.Mul => leftVal * rightVal,
            IrBinaryOp.OpKind.Div when rightVal != 0 => leftVal / rightVal,
            IrBinaryOp.OpKind.Mod when rightVal != 0 => leftVal % rightVal,
            IrBinaryOp.OpKind.And => leftVal & rightVal,
            IrBinaryOp.OpKind.Or => leftVal | rightVal,
            IrBinaryOp.OpKind.Xor => leftVal ^ rightVal,
            IrBinaryOp.OpKind.Shl => leftVal << (int)rightVal,
            IrBinaryOp.OpKind.Shr => leftVal >> (int)rightVal,
            _ => null
        };

        if (result.HasValue)
        {
            return new IrConstant(result.Value, resultType);
        }

        // Handle comparison operations (return 1 for true, 0 for false)
        long? boolResult = op switch
        {
            IrBinaryOp.OpKind.Eq => leftVal == rightVal ? 1L : 0L,
            IrBinaryOp.OpKind.Ne => leftVal != rightVal ? 1L : 0L,
            IrBinaryOp.OpKind.Lt => leftVal < rightVal ? 1L : 0L,
            IrBinaryOp.OpKind.Le => leftVal <= rightVal ? 1L : 0L,
            IrBinaryOp.OpKind.Gt => leftVal > rightVal ? 1L : 0L,
            IrBinaryOp.OpKind.Ge => leftVal >= rightVal ? 1L : 0L,
            _ => null
        };

        if (boolResult.HasValue)
        {
            return new IrConstant(boolResult.Value, resultType);
        }

        return null;
    }

    /// <summary>
    /// Try to evaluate a const fn call at compile time.
    /// Returns true if the call was evaluated and provides the result.
    /// </summary>
    private bool TryEvaluateConstFnCall(IrCall call, out IrConstant? result)
    {
        result = null;

        // Need module and evaluator to evaluate const fn calls
        if (_module == null || _constFnEvaluator == null)
            return false;

        // Look up the function
        var function = _module.Functions.FirstOrDefault(f => f.Name == call.FunctionName);
        if (function == null || !function.IsConstFn)
            return false;

        // Check if all arguments are constants (after propagation)
        var constArgs = new List<object?>();
        foreach (var arg in call.Arguments)
        {
            var propagated = PropagateInValue(arg);
            if (propagated is IrConstant constant)
            {
                constArgs.Add(constant.Value);
            }
            else if (propagated is IrBoolConstant boolConst)
            {
                constArgs.Add(boolConst.Value ? 1L : 0L);
            }
            else
            {
                // Non-constant argument - can't evaluate at compile time
                return false;
            }
        }

        // Create cache key for memoization
        var cacheKey = $"{call.FunctionName}({string.Join(",", constArgs)})";

        // Check cache first
        if (_constFnCache.TryGetValue(cacheKey, out var cached))
        {
            result = cached;
            ConstFnCacheHits++;
            return true;
        }

        // Evaluate the const fn
        var evalResult = _constFnEvaluator.Evaluate(call.FunctionName, constArgs);
        if (!evalResult.Success)
        {
            // Evaluation failed (e.g., infinite loop, stack overflow)
            return false;
        }

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
        else if (evalResult.Value == null)
        {
            // Void return or null - can't propagate
            return false;
        }
        else
        {
            // Unsupported result type
            return false;
        }

        // Cache the result
        _constFnCache[cacheKey] = result;
        ConstFnEvaluations++;

        return true;
    }
}
