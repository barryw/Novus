using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Algebraic simplification pass
/// Applies algebraic identities to simplify expressions
/// Examples:
///   x + 0 => x
///   x * 1 => x
///   x * 0 => 0
///   x - 0 => x
///   x - x => 0
///   x / 1 => x
///   x % 1 => 0
///   x << 0 => x
///   x >> 0 => x
///   x | 0 => x
///   x | -1 => -1
///   x & 0 => 0
///   x & -1 => x
///   x ^ 0 => x
///   x ^ x => 0
/// </summary>
public class AlgebraicSimplificationPass : BasicBlockPassBase
{
    public override string Name => "Algebraic Simplification";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;

        for (int i = 0; i < block.Instructions.Count; i++)
        {
            var instruction = block.Instructions[i];

            if (instruction is IrBinaryOp binOp)
            {
                var simplified = TrySimplify(binOp);
                if (simplified != null)
                {
                    // Replace the binary operation with the simplified value
                    // We need to update all uses of this operation
                    ReplaceInstruction(block, i, simplified, binOp);
                    changed = true;
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Try to simplify a binary operation using algebraic identities
    /// Returns the simplified value if successful, null otherwise
    /// </summary>
    private IrValue? TrySimplify(IrBinaryOp binOp)
    {
        // Get constant values if operands are constants
        var leftConst = binOp.Left as IrConstant;
        var rightConst = binOp.Right as IrConstant;

        // Check for identity operations
        switch (binOp.Operation)
        {
            case IrBinaryOp.OpKind.Add:
                return TrySimplifyAdd(binOp, leftConst, rightConst);

            case IrBinaryOp.OpKind.Sub:
                return TrySimplifySubtract(binOp, leftConst, rightConst);

            case IrBinaryOp.OpKind.Mul:
                return TrySimplifyMultiply(binOp, leftConst, rightConst);

            case IrBinaryOp.OpKind.Div:
                return TrySimplifyDivide(binOp, leftConst, rightConst);

            case IrBinaryOp.OpKind.Mod:
                return TrySimplifyModulo(binOp, leftConst, rightConst);

            case IrBinaryOp.OpKind.Shl:
            case IrBinaryOp.OpKind.Shr:
                return TrySimplifyShift(binOp, leftConst, rightConst);

            case IrBinaryOp.OpKind.Or:
                return TrySimplifyBitOr(binOp, leftConst, rightConst);

            case IrBinaryOp.OpKind.And:
                return TrySimplifyBitAnd(binOp, leftConst, rightConst);

            case IrBinaryOp.OpKind.Xor:
                return TrySimplifyBitXor(binOp, leftConst, rightConst);

            default:
                return null;
        }
    }

    private IrValue? TrySimplifyAdd(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x + 0 => x
        if (rightConst != null && IsZero(rightConst))
        {
            return binOp.Left;
        }

        // 0 + x => x
        if (leftConst != null && IsZero(leftConst))
        {
            return binOp.Right;
        }

        return null;
    }

    private IrValue? TrySimplifySubtract(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x - 0 => x
        if (rightConst != null && IsZero(rightConst))
        {
            return binOp.Left;
        }

        // x - x => 0 (only for variables, not expressions)
        if (binOp.Left is IrVariable leftVar &&
            binOp.Right is IrVariable rightVar &&
            leftVar.Name == rightVar.Name)
        {
            return CreateZeroConstant(binOp.Type);
        }

        return null;
    }

    private IrValue? TrySimplifyMultiply(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x * 0 => 0
        if (rightConst != null && IsZero(rightConst))
        {
            return CreateZeroConstant(binOp.Type);
        }

        // 0 * x => 0
        if (leftConst != null && IsZero(leftConst))
        {
            return CreateZeroConstant(binOp.Type);
        }

        // x * 1 => x
        if (rightConst != null && IsOne(rightConst))
        {
            return binOp.Left;
        }

        // 1 * x => x
        if (leftConst != null && IsOne(leftConst))
        {
            return binOp.Right;
        }

        return null;
    }

    private IrValue? TrySimplifyDivide(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x / 1 => x
        if (rightConst != null && IsOne(rightConst))
        {
            return binOp.Left;
        }

        // 0 / x => 0 (assuming x != 0, which is UB anyway)
        if (leftConst != null && IsZero(leftConst))
        {
            return CreateZeroConstant(binOp.Type);
        }

        return null;
    }

    private IrValue? TrySimplifyModulo(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x % 1 => 0
        if (rightConst != null && IsOne(rightConst))
        {
            return CreateZeroConstant(binOp.Type);
        }

        // 0 % x => 0 (assuming x != 0)
        if (leftConst != null && IsZero(leftConst))
        {
            return CreateZeroConstant(binOp.Type);
        }

        return null;
    }

    private IrValue? TrySimplifyShift(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x << 0 => x
        // x >> 0 => x
        if (rightConst != null && IsZero(rightConst))
        {
            return binOp.Left;
        }

        // 0 << x => 0
        // 0 >> x => 0
        if (leftConst != null && IsZero(leftConst))
        {
            return CreateZeroConstant(binOp.Type);
        }

        return null;
    }

    private IrValue? TrySimplifyBitOr(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x | 0 => x
        if (rightConst != null && IsZero(rightConst))
        {
            return binOp.Left;
        }

        // 0 | x => x
        if (leftConst != null && IsZero(leftConst))
        {
            return binOp.Right;
        }

        // x | -1 => -1 (all bits set)
        if (rightConst != null && IsAllOnes(rightConst))
        {
            return rightConst;
        }

        // -1 | x => -1
        if (leftConst != null && IsAllOnes(leftConst))
        {
            return leftConst;
        }

        return null;
    }

    private IrValue? TrySimplifyBitAnd(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x & 0 => 0
        if (rightConst != null && IsZero(rightConst))
        {
            return CreateZeroConstant(binOp.Type);
        }

        // 0 & x => 0
        if (leftConst != null && IsZero(leftConst))
        {
            return CreateZeroConstant(binOp.Type);
        }

        // x & -1 => x (all bits set)
        if (rightConst != null && IsAllOnes(rightConst))
        {
            return binOp.Left;
        }

        // -1 & x => x
        if (leftConst != null && IsAllOnes(leftConst))
        {
            return binOp.Right;
        }

        return null;
    }

    private IrValue? TrySimplifyBitXor(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
    {
        // x ^ 0 => x
        if (rightConst != null && IsZero(rightConst))
        {
            return binOp.Left;
        }

        // 0 ^ x => x
        if (leftConst != null && IsZero(leftConst))
        {
            return binOp.Right;
        }

        // x ^ x => 0 (only for variables)
        if (binOp.Left is IrVariable leftVar &&
            binOp.Right is IrVariable rightVar &&
            leftVar.Name == rightVar.Name)
        {
            return CreateZeroConstant(binOp.Type);
        }

        return null;
    }

    /// <summary>
    /// Check if a constant is zero
    /// </summary>
    private bool IsZero(IrConstant constant)
    {
        return constant.Value == 0;
    }

    /// <summary>
    /// Check if a constant is one
    /// </summary>
    private bool IsOne(IrConstant constant)
    {
        return constant.Value == 1;
    }

    /// <summary>
    /// Check if a constant has all bits set (-1 for signed, max value for unsigned)
    /// </summary>
    private bool IsAllOnes(IrConstant constant)
    {
        // Check for -1 (all bits set for signed integers)
        // or check based on type size for unsigned
        if (constant.Value == -1)
            return true;

        // For byte type, all bits set is 255
        if (constant.Type is IrIntType intType && intType.SizeInBytes == 1 && !intType.IsSigned)
            return constant.Value == 255;

        return false;
    }

    /// <summary>
    /// Create a zero constant of the given type
    /// </summary>
    private IrConstant CreateZeroConstant(IrType type)
    {
        // IrConstant.Value is always long, so just pass 0
        return new IrConstant(0, type);
    }

    /// <summary>
    /// Replace an instruction with a simplified value
    /// Updates all subsequent uses of the instruction's result
    /// </summary>
    private void ReplaceInstruction(IrBasicBlock block, int index, IrValue replacement, IrBinaryOp original)
    {
        // Find all uses of the variable that holds the result of this binary operation
        // and replace them with the simplified value
        string resultName = original.ResultName;

        // Look ahead for uses of the result variable
        for (int j = index + 1; j < block.Instructions.Count; j++)
        {
            var inst = block.Instructions[j];

            if (inst is IrStore store && store.Value is IrVariable storeVar && storeVar.Name == resultName)
            {
                store.Value = replacement;
            }
            else if (inst is IrReturn ret && ret.Value is IrVariable retVar && retVar.Name == resultName)
            {
                ret.Value = replacement;
            }
            else if (inst is IrBinaryOp binOp)
            {
                if (binOp.Left is IrVariable leftVar && leftVar.Name == resultName)
                {
                    binOp.Left = replacement;
                }
                if (binOp.Right is IrVariable rightVar && rightVar.Name == resultName)
                {
                    binOp.Right = replacement;
                }
            }
            else if (inst is IrCall call)
            {
                for (int k = 0; k < call.Arguments.Count; k++)
                {
                    if (call.Arguments[k] is IrVariable argVar && argVar.Name == resultName)
                    {
                        call.Arguments[k] = replacement;
                    }
                }
            }
            else if (inst is IrConditionalBranch branch && branch.Condition is IrVariable branchVar && branchVar.Name == resultName)
            {
                branch.Condition = replacement;
            }
        }

        // Remove the binary op instruction since it's been simplified
        block.Instructions.RemoveAt(index);
    }
}
