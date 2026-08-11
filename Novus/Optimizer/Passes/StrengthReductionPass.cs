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
    private int _tempCounter = 0;

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;

        // Process in forward order, but track index carefully since we may insert
        for (int i = 0; i < block.Instructions.Count; i++)
        {
            var instruction = block.Instructions[i];

            if (instruction is IrBinaryOp binOp)
            {
                // Try to reduce multiplication
                if (binOp.Operation == IrBinaryOp.OpKind.Mul)
                {
                    if (TryReduceMultiply(binOp, block, i))
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

    private bool TryReduceMultiply(IrBinaryOp binOp, IrBasicBlock block, int index)
    {
        // Check if we're multiplying by a constant
        IrConstant? constant = null;
        IrValue? variable = null;

        if (binOp.Right is IrConstant rightConst)
        {
            constant = rightConst;
            variable = binOp.Left;
        }
        else if (binOp.Left is IrConstant leftConst)
        {
            constant = leftConst;
            variable = binOp.Right;
        }

        if (constant == null || variable == null)
            return false;

        long multiplier = constant.Value;

        // Handle special cases first
        if (multiplier == 0 || multiplier == 1)
            return false; // Algebraic simplification will handle these

        // Try power of 2 first (fastest)
        if (IsPowerOfTwo(multiplier))
        {
            var shiftAmount = Log2(multiplier);
            binOp.Operation = IrBinaryOp.OpKind.Shl;
            binOp.Left = variable;
            binOp.Right = new IrConstant(shiftAmount, IrIntType.I32);
            return true;
        }

        // For non-power-of-2, try shift-add sequences
        // This remains beneficial on the 68020 baseline.
        var sequence = FindBestShiftAddSequence(multiplier);
        if (sequence != null && sequence.Cost <= 4) // Only if cheaper than multiply
        {
            // Handle common cases by inserting a shift instruction
            // and converting the multiply to an add/sub
            switch (multiplier)
            {
                case 3: // (x << 1) + x
                case 5: // (x << 2) + x
                case 9: // (x << 3) + x
                case 17: // (x << 4) + x
                case 33: // (x << 5) + x
                    {
                        // Insert: %temp = x << log2(n-1)
                        // Convert multiply to: result = %temp + x
                        var shift = Log2(multiplier - 1);
                        var tempName = $"%sr_temp{_tempCounter++}";
                        var shiftOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Shl,
                            variable, new IrConstant(shift, IrIntType.I32), binOp.Type);

                        // Insert shift before the multiply
                        block.Instructions.Insert(index, shiftOp);

                        // Convert multiply to add
                        binOp.Operation = IrBinaryOp.OpKind.Add;
                        binOp.Left = new IrVariable(tempName, binOp.Type);
                        binOp.Right = variable;
                        return true;
                    }

                case 7: // (x << 3) - x
                case 15: // (x << 4) - x
                case 31: // (x << 5) - x
                case 63: // (x << 6) - x
                    {
                        // Insert: %temp = x << log2(n+1)
                        // Convert multiply to: result = %temp - x
                        var shift = Log2(multiplier + 1);
                        var tempName = $"%sr_temp{_tempCounter++}";
                        var shiftOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Shl,
                            variable, new IrConstant(shift, IrIntType.I32), binOp.Type);

                        // Insert shift before the multiply
                        block.Instructions.Insert(index, shiftOp);

                        // Convert multiply to subtract
                        binOp.Operation = IrBinaryOp.OpKind.Sub;
                        binOp.Left = new IrVariable(tempName, binOp.Type);
                        binOp.Right = variable;
                        return true;
                    }
            }
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

    /// <summary>
    /// Find the best shift-add sequence for a given multiplier
    /// Returns null if no good sequence is found
    /// </summary>
    private ShiftAddSequence? FindBestShiftAddSequence(long multiplier)
    {
        if (multiplier <= 0 || multiplier > 1000)
            return null; // Only optimize small positive constants

        // Check if it's one away from a power of 2 (2^n ± 1)
        // These are the easiest to optimize

        // Check 2^n + 1 (like 3, 5, 9, 17, 33)
        for (int shift = 1; shift <= 10; shift++)
        {
            long powerOf2 = 1L << shift;
            if (multiplier == powerOf2 + 1)
            {
                // x * (2^n + 1) = (x << n) + x
                return new ShiftAddSequence { Cost = 2 }; // 1 shift + 1 add
            }
            if (multiplier == powerOf2 - 1)
            {
                // x * (2^n - 1) = (x << n) - x
                return new ShiftAddSequence { Cost = 2 }; // 1 shift + 1 sub
            }
        }

        // Check sum of two powers of 2 (like 6=4+2, 10=8+2, 12=8+4)
        for (int shift1 = 1; shift1 <= 10; shift1++)
        {
            for (int shift2 = 1; shift2 < shift1; shift2++)
            {
                long sum = (1L << shift1) + (1L << shift2);
                if (multiplier == sum)
                {
                    // x * (2^a + 2^b) = (x << a) + (x << b)
                    return new ShiftAddSequence { Cost = 3 }; // 2 shifts + 1 add
                }
            }
        }

        // For other cases, we'd need more complex sequences
        // Return null to indicate no simple sequence found
        return null;
    }
}

/// <summary>
/// Represents the cost of a shift-add sequence
/// </summary>
internal class ShiftAddSequence
{
    /// <summary>
    /// Number of operations (shifts + adds/subs)
    /// </summary>
    public int Cost { get; set; }
}
