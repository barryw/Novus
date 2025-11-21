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
/// Combined with DCE, the x_0 and y_0 definitions may also be eliminated if unused elsewhere.
/// </summary>
public class ConstantPropagation
{
    private readonly IrFunction _function;

    /// <summary>
    /// Map from variable SSA name to its constant value (if known)
    /// </summary>
    private Dictionary<string, IrConstant> _constantValues = new();

    /// <summary>
    /// Track which instructions were modified (for iteration)
    /// </summary>
    private bool _madeChanges = false;

    public ConstantPropagation(IrFunction function)
    {
        _function = function;
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
                    case IrLocalDecl decl when decl.InitialValue is IrConstant constant:
                        _constantValues[decl.Name] = constant;
                        break;

                    case IrStore store when store.Value is IrConstant constant:
                        _constantValues[store.VariableName] = constant;
                        break;

                    case IrBinaryOp binOp when CanFoldBinaryOp(binOp, out var folded):
                        _constantValues[binOp.ResultName] = folded!;
                        break;

                    case IrCall call when call.ResultName != null:
                        // Calls are generally not constant (unless proven pure and with constant args)
                        // For now, we don't propagate through calls
                        break;
                }
            }
        }
    }

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
                    var newValue = PropagateInValue(decl.InitialValue);
                    if (newValue != decl.InitialValue)
                    {
                        decl.InitialValue = newValue;
                        changed = true;
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
                    }
                    break;
                }

            case IrCall call:
                {
                    for (int i = 0; i < call.Arguments.Count; i++)
                    {
                        var newArg = PropagateInValue(call.Arguments[i]);
                        if (newArg != call.Arguments[i])
                        {
                            call.Arguments[i] = newArg;
                            changed = true;
                        }
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
                    if (newArray != indexAccess.Array || newIndex != indexAccess.Index)
                    {
                        indexAccess.Array = newArray;
                        indexAccess.Index = newIndex;
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
                    if (newArray != indexStore.Array || newIndex != indexStore.Index || newValue != indexStore.Value)
                    {
                        indexStore.Array = newArray;
                        indexStore.Index = newIndex;
                        indexStore.Value = newValue;
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
}
