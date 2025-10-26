using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Strength reduction pass
/// Replaces expensive operations with cheaper equivalents
/// Examples:
///   x * 2 => x << 1
///   x * 4 => x << 2
///   x / 2 => x >> 1 (for unsigned)
/// </summary>
public class StrengthReductionPass : BasicBlockPassBase
{
    public override string Name => "Strength Reduction";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;

        foreach (var instruction in block.Instructions)
        {
            if (instruction is IrBinaryOp binOp)
            {
                // Try to reduce multiplication
                if (binOp.Operation == IrBinaryOp.OpKind.Mul)
                {
                    if (TryReduceMultiply(binOp))
                        changed = true;
                }
                // Try to reduce division
                else if (binOp.Operation == IrBinaryOp.OpKind.Div)
                {
                    if (TryReduceDivide(binOp))
                        changed = true;
                }
            }
        }

        return changed;
    }

    private bool TryReduceMultiply(IrBinaryOp binOp)
    {
        // Check if we're multiplying by a power of 2
        IrConstant? powerConst = null;
        IrValue? otherValue = null;

        if (binOp.Right is IrConstant rightConst)
        {
            powerConst = rightConst;
            otherValue = binOp.Left;
        }
        else if (binOp.Left is IrConstant leftConst)
        {
            powerConst = leftConst;
            otherValue = binOp.Right;
        }

        if (powerConst != null && otherValue != null && IsPowerOfTwo(powerConst.Value))
        {
            var shiftAmount = Log2(powerConst.Value);
            // Replace multiply with left shift
            binOp.Operation = IrBinaryOp.OpKind.Shl;
            binOp.Left = otherValue;
            binOp.Right = new IrConstant(shiftAmount, IrIntType.I32);
            return true;
        }

        return false;
    }

    private bool TryReduceDivide(IrBinaryOp binOp)
    {
        // Only reduce division by power of 2 for unsigned types
        // (signed division requires arithmetic shift and is more complex)
        if (binOp.Type is IrIntType intType && !intType.IsSigned)
        {
            if (binOp.Right is IrConstant divisorConst && IsPowerOfTwo(divisorConst.Value))
            {
                var shiftAmount = Log2(divisorConst.Value);
                // Replace divide with right shift
                binOp.Operation = IrBinaryOp.OpKind.Shr;
                binOp.Right = new IrConstant(shiftAmount, IrIntType.I32);
                return true;
            }
        }

        return false;
    }

    private bool IsPowerOfTwo(long value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    private int Log2(long value)
    {
        int result = 0;
        while (value > 1)
        {
            value >>= 1;
            result++;
        }
        return result;
    }
}
