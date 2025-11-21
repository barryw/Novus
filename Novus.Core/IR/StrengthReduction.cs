namespace Novus.IR;

/// <summary>
/// Strength Reduction optimization pass.
/// Replaces expensive operations with cheaper equivalent operations.
///
/// This is particularly critical for the 68k architecture where:
/// - Multiplication takes 38-70 cycles (depending on operand size)
/// - Division takes 76-140 cycles
/// - Shifts take 6-8 cycles
/// - Adds take 4-8 cycles
///
/// Transformations:
/// - x * 2^n → x << n (multiply by power of 2 → shift left)
/// - x / 2^n → x >> n (divide by power of 2 → shift right, for unsigned/positive)
/// - x * 3 → (x << 1) + x (x * 2 + x)
/// - x * 5 → (x << 2) + x (x * 4 + x)
/// - x * 6 → (x << 1) + (x << 2) (x * 2 + x * 4)
/// - x * 7 → (x << 3) - x ((x * 8) - x)
/// - x * 9 → (x << 3) + x (x * 8 + x)
/// - x * 10 → (x << 3) + (x << 1) (x * 8 + x * 2)
///
/// These transformations can save hundreds of cycles per operation on 68k.
/// </summary>
public class StrengthReduction
{
    private readonly IrFunction _function;
    private bool _madeChanges = false;

    public StrengthReduction(IrFunction function)
    {
        _function = function;
    }

    /// <summary>
    /// Run strength reduction until no more changes can be made.
    /// Returns the number of reductions performed.
    /// </summary>
    public int Reduce()
    {
        int totalReductions = 0;

        do
        {
            _madeChanges = false;
            int reductions = ReducePass();
            totalReductions += reductions;
        } while (_madeChanges);

        return totalReductions;
    }

    private int ReducePass()
    {
        int reductionCount = 0;

        foreach (var block in _function.BasicBlocks)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var instruction = block.Instructions[i];
                if (instruction is IrBinaryOp binOp)
                {
                    var reduced = ReduceBinaryOp(binOp);
                    if (reduced != null)
                    {
                        // Replace with potentially multiple instructions
                        block.Instructions.RemoveAt(i);
                        block.Instructions.InsertRange(i, reduced);
                        reductionCount++;
                        _madeChanges = true;
                    }
                }
            }
        }

        return reductionCount;
    }

    /// <summary>
    /// Attempt to reduce a binary operation to cheaper operations.
    /// Returns a list of replacement instructions, or null if no reduction possible.
    /// </summary>
    private List<IrInstruction>? ReduceBinaryOp(IrBinaryOp binOp)
    {
        var left = binOp.Left;
        var right = binOp.Right;

        // Check if right operand is a constant (most common case for strength reduction)
        if (right is not IrConstant rightConst)
            return null;

        long value = rightConst.Value;

        switch (binOp.Operation)
        {
            case IrBinaryOp.OpKind.Mul:
                return ReduceMultiplication(binOp, left, value);

            case IrBinaryOp.OpKind.Div:
                return ReduceDivision(binOp, left, value);

            case IrBinaryOp.OpKind.Mod:
                return ReduceModulo(binOp, left, value);
        }

        return null;
    }

    /// <summary>
    /// Reduce multiplication to cheaper operations.
    /// </summary>
    private List<IrInstruction>? ReduceMultiplication(IrBinaryOp binOp, IrValue left, long multiplier)
    {
        // x * 0 = 0 (should be handled by algebraic simplification, but just in case)
        if (multiplier == 0)
            return new List<IrInstruction> { new IrStore(binOp.ResultName, new IrConstant(0, binOp.Type)) };

        // x * 1 = x (should be handled by algebraic simplification)
        if (multiplier == 1)
            return new List<IrInstruction> { new IrStore(binOp.ResultName, left) };

        // x * -1 = -x (negate)
        if (multiplier == -1)
        {
            return new List<IrInstruction>
            {
                new IrBinaryOp(binOp.ResultName, IrBinaryOp.OpKind.Sub,
                    new IrConstant(0, binOp.Type), left, binOp.Type)
            };
        }

        // x * 2^n = x << n (power of 2)
        if (IsPowerOfTwo(multiplier))
        {
            int shift = Log2(multiplier);
            return new List<IrInstruction>
            {
                new IrBinaryOp(binOp.ResultName, IrBinaryOp.OpKind.Shl,
                    left, new IrConstant(shift, IrIntType.I32), binOp.Type)
            };
        }

        // Handle small multipliers with add/shift combinations
        var instructions = ReduceSmallMultiplier(binOp, left, multiplier);
        if (instructions != null)
            return instructions;

        return null; // No reduction possible
    }

    /// <summary>
    /// Reduce small multipliers to add/shift combinations.
    /// Only worth it for small constants where the cost is less than multiplication.
    /// </summary>
    private List<IrInstruction>? ReduceSmallMultiplier(IrBinaryOp binOp, IrValue left, long multiplier)
    {
        string result = binOp.ResultName;
        var type = binOp.Type;

        // Generate temporary variable names
        string temp1 = $"{result}_sr1";
        string temp2 = $"{result}_sr2";

        switch (multiplier)
        {
            case 3: // x * 3 = (x << 1) + x
                return new List<IrInstruction>
                {
                    new IrBinaryOp(temp1, IrBinaryOp.OpKind.Shl, left, new IrConstant(1, IrIntType.I32), type),
                    new IrBinaryOp(result, IrBinaryOp.OpKind.Add, new IrVariable(temp1, type), left, type)
                };

            case 5: // x * 5 = (x << 2) + x
                return new List<IrInstruction>
                {
                    new IrBinaryOp(temp1, IrBinaryOp.OpKind.Shl, left, new IrConstant(2, IrIntType.I32), type),
                    new IrBinaryOp(result, IrBinaryOp.OpKind.Add, new IrVariable(temp1, type), left, type)
                };

            case 6: // x * 6 = (x << 1) + (x << 2)
                return new List<IrInstruction>
                {
                    new IrBinaryOp(temp1, IrBinaryOp.OpKind.Shl, left, new IrConstant(1, IrIntType.I32), type),
                    new IrBinaryOp(temp2, IrBinaryOp.OpKind.Shl, left, new IrConstant(2, IrIntType.I32), type),
                    new IrBinaryOp(result, IrBinaryOp.OpKind.Add, new IrVariable(temp1, type), new IrVariable(temp2, type), type)
                };

            case 7: // x * 7 = (x << 3) - x
                return new List<IrInstruction>
                {
                    new IrBinaryOp(temp1, IrBinaryOp.OpKind.Shl, left, new IrConstant(3, IrIntType.I32), type),
                    new IrBinaryOp(result, IrBinaryOp.OpKind.Sub, new IrVariable(temp1, type), left, type)
                };

            case 9: // x * 9 = (x << 3) + x
                return new List<IrInstruction>
                {
                    new IrBinaryOp(temp1, IrBinaryOp.OpKind.Shl, left, new IrConstant(3, IrIntType.I32), type),
                    new IrBinaryOp(result, IrBinaryOp.OpKind.Add, new IrVariable(temp1, type), left, type)
                };

            case 10: // x * 10 = (x << 3) + (x << 1)
                return new List<IrInstruction>
                {
                    new IrBinaryOp(temp1, IrBinaryOp.OpKind.Shl, left, new IrConstant(3, IrIntType.I32), type),
                    new IrBinaryOp(temp2, IrBinaryOp.OpKind.Shl, left, new IrConstant(1, IrIntType.I32), type),
                    new IrBinaryOp(result, IrBinaryOp.OpKind.Add, new IrVariable(temp1, type), new IrVariable(temp2, type), type)
                };

            case 12: // x * 12 = (x << 2) + (x << 3)
                return new List<IrInstruction>
                {
                    new IrBinaryOp(temp1, IrBinaryOp.OpKind.Shl, left, new IrConstant(2, IrIntType.I32), type),
                    new IrBinaryOp(temp2, IrBinaryOp.OpKind.Shl, left, new IrConstant(3, IrIntType.I32), type),
                    new IrBinaryOp(result, IrBinaryOp.OpKind.Add, new IrVariable(temp1, type), new IrVariable(temp2, type), type)
                };

            case 15: // x * 15 = (x << 4) - x
                return new List<IrInstruction>
                {
                    new IrBinaryOp(temp1, IrBinaryOp.OpKind.Shl, left, new IrConstant(4, IrIntType.I32), type),
                    new IrBinaryOp(result, IrBinaryOp.OpKind.Sub, new IrVariable(temp1, type), left, type)
                };

            default:
                return null;
        }
    }

    /// <summary>
    /// Reduce division by power of 2 to shift right.
    /// Note: Only valid for unsigned or positive values due to rounding differences.
    /// For now, we apply it unconditionally (can be refined later with type info).
    /// </summary>
    private List<IrInstruction>? ReduceDivision(IrBinaryOp binOp, IrValue left, long divisor)
    {
        // x / 1 = x (should be handled by algebraic simplification)
        if (divisor == 1)
            return new List<IrInstruction> { new IrStore(binOp.ResultName, left) };

        // x / 2^n = x >> n (only for power of 2)
        if (IsPowerOfTwo(divisor))
        {
            int shift = Log2(divisor);
            return new List<IrInstruction>
            {
                new IrBinaryOp(binOp.ResultName, IrBinaryOp.OpKind.Shr,
                    left, new IrConstant(shift, IrIntType.I32), binOp.Type)
            };
        }

        return null;
    }

    /// <summary>
    /// Reduce modulo by power of 2 to bitwise AND.
    /// x % 2^n = x & (2^n - 1)
    /// </summary>
    private List<IrInstruction>? ReduceModulo(IrBinaryOp binOp, IrValue left, long divisor)
    {
        // x % 2^n = x & (2^n - 1)
        if (IsPowerOfTwo(divisor))
        {
            long mask = divisor - 1;
            return new List<IrInstruction>
            {
                new IrBinaryOp(binOp.ResultName, IrBinaryOp.OpKind.And,
                    left, new IrConstant(mask, binOp.Type), binOp.Type)
            };
        }

        return null;
    }

    /// <summary>
    /// Check if a number is a power of 2.
    /// </summary>
    private bool IsPowerOfTwo(long n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }

    /// <summary>
    /// Calculate log2 of a power of 2.
    /// </summary>
    private int Log2(long n)
    {
        int log = 0;
        while (n > 1)
        {
            n >>= 1;
            log++;
        }
        return log;
    }
}
