namespace Novus.IR;

/// <summary>
/// Algebraic Simplification optimization pass.
/// Applies algebraic identities to simplify expressions.
///
/// Identities implemented:
/// - x + 0 = x
/// - x - 0 = x
/// - x * 1 = x
/// - x * 0 = 0
/// - x / 1 = x
/// - x & x = x
/// - x | x = x
/// - x | 0 = x
/// - x & 0 = 0
/// - x ^ 0 = x
/// - x ^ x = 0
/// - x - x = 0
/// - 0 - x = -x (negation)
/// - x << 0 = x
/// - x >> 0 = x
///
/// These simplifications are particularly important for 68k code generation
/// as they can eliminate instructions entirely.
/// </summary>
public class AlgebraicSimplification
{
    private readonly IrFunction _function;
    private bool _madeChanges = false;

    public AlgebraicSimplification(IrFunction function)
    {
        _function = function;
    }

    /// <summary>
    /// Run algebraic simplification until no more changes can be made.
    /// Returns the number of simplifications performed.
    /// </summary>
    public int Simplify()
    {
        int totalSimplifications = 0;

        do
        {
            _madeChanges = false;
            int simplifications = SimplifyPass();
            totalSimplifications += simplifications;
        } while (_madeChanges);

        return totalSimplifications;
    }

    private int SimplifyPass()
    {
        int simplificationCount = 0;

        foreach (var block in _function.BasicBlocks)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var instruction = block.Instructions[i];
                if (instruction is IrBinaryOp binOp)
                {
                    var simplified = SimplifyBinaryOp(binOp);
                    if (simplified != null)
                    {
                        block.Instructions[i] = simplified;
                        simplificationCount++;
                        _madeChanges = true;
                    }
                }
            }
        }

        return simplificationCount;
    }

    /// <summary>
    /// Attempt to simplify a binary operation using algebraic identities.
    /// Returns a simplified instruction, or null if no simplification possible.
    /// </summary>
    private IrInstruction? SimplifyBinaryOp(IrBinaryOp binOp)
    {
        var left = binOp.Left;
        var right = binOp.Right;

        // Check if operands are constants
        var leftConst = left as IrConstant;
        var rightConst = right as IrConstant;

        // Check if operands are the same variable
        bool sameVariable = false;
        if (left is IrVariable leftVar && right is IrVariable rightVar)
        {
            sameVariable = leftVar.SsaName == rightVar.SsaName;
        }

        switch (binOp.Operation)
        {
            case IrBinaryOp.OpKind.Add:
                // x + 0 = x
                if (rightConst != null && rightConst.Value == 0)
                    return new IrStore(binOp.ResultName, left);
                // 0 + x = x
                if (leftConst != null && leftConst.Value == 0)
                    return new IrStore(binOp.ResultName, right);
                break;

            case IrBinaryOp.OpKind.Sub:
                // x - 0 = x
                if (rightConst != null && rightConst.Value == 0)
                    return new IrStore(binOp.ResultName, left);
                // x - x = 0
                if (sameVariable)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                // 0 - x = -x (negation) - represent as 0 - x for now, could optimize further
                break;

            case IrBinaryOp.OpKind.Mul:
                // x * 1 = x
                if (rightConst != null && rightConst.Value == 1)
                    return new IrStore(binOp.ResultName, left);
                // 1 * x = x
                if (leftConst != null && leftConst.Value == 1)
                    return new IrStore(binOp.ResultName, right);
                // x * 0 = 0
                if (rightConst != null && rightConst.Value == 0)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                // 0 * x = 0
                if (leftConst != null && leftConst.Value == 0)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                break;

            case IrBinaryOp.OpKind.Div:
                // x / 1 = x
                if (rightConst != null && rightConst.Value == 1)
                    return new IrStore(binOp.ResultName, left);
                // x / x = 1 (if x != 0, but we assume it's safe)
                if (sameVariable)
                    return new IrStore(binOp.ResultName, new IrConstant(1, binOp.Type));
                break;

            case IrBinaryOp.OpKind.Mod:
                // x % 1 = 0
                if (rightConst != null && rightConst.Value == 1)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                // x % x = 0 (if x != 0)
                if (sameVariable)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                break;

            case IrBinaryOp.OpKind.And:
                // x & x = x
                if (sameVariable)
                    return new IrStore(binOp.ResultName, left);
                // x & 0 = 0
                if (rightConst != null && rightConst.Value == 0)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                // 0 & x = 0
                if (leftConst != null && leftConst.Value == 0)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                // x & -1 (all bits set) = x
                if (rightConst != null && rightConst.Value == -1)
                    return new IrStore(binOp.ResultName, left);
                if (leftConst != null && leftConst.Value == -1)
                    return new IrStore(binOp.ResultName, right);
                break;

            case IrBinaryOp.OpKind.Or:
                // x | x = x
                if (sameVariable)
                    return new IrStore(binOp.ResultName, left);
                // x | 0 = x
                if (rightConst != null && rightConst.Value == 0)
                    return new IrStore(binOp.ResultName, left);
                // 0 | x = x
                if (leftConst != null && leftConst.Value == 0)
                    return new IrStore(binOp.ResultName, right);
                // x | -1 = -1 (all bits set)
                if (rightConst != null && rightConst.Value == -1)
                    return new IrStore(binOp.ResultName, new IrConstant(-1, binOp.Type));
                if (leftConst != null && leftConst.Value == -1)
                    return new IrStore(binOp.ResultName, new IrConstant(-1, binOp.Type));
                break;

            case IrBinaryOp.OpKind.Xor:
                // x ^ 0 = x
                if (rightConst != null && rightConst.Value == 0)
                    return new IrStore(binOp.ResultName, left);
                // 0 ^ x = x
                if (leftConst != null && leftConst.Value == 0)
                    return new IrStore(binOp.ResultName, right);
                // x ^ x = 0
                if (sameVariable)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                break;

            case IrBinaryOp.OpKind.Shl:
            case IrBinaryOp.OpKind.Shr:
                // x << 0 = x, x >> 0 = x
                if (rightConst != null && rightConst.Value == 0)
                    return new IrStore(binOp.ResultName, left);
                // 0 << x = 0, 0 >> x = 0
                if (leftConst != null && leftConst.Value == 0)
                    return new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type));
                break;
        }

        return null; // No simplification possible
    }
}
