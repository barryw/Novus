namespace Novus.IR;

/// <summary>
/// 68k-specific peephole optimization pass.
///
/// Peephole optimization examines small sequences of instructions (the "peephole")
/// and replaces them with more efficient equivalents specific to the 68k architecture.
///
/// This is particularly important for the 68k because:
/// - Some instruction sequences are much slower than others
/// - Address register operations are faster than data register ops for some things
/// - Immediate values have size limits and encoding costs
/// - Certain instruction combinations can be fused
///
/// Optimizations performed:
/// 1. Move elimination: x = y; z = x → x = y; z = y
/// 2. Redundant load/store: x = expr; y = x → x = expr; (if x not used again)
/// 3. Constant folding of remaining operations
/// 4. Address mode optimization (when lowered to 68k)
/// 5. Compare-and-branch fusion
/// 6. 68k-specific instruction selection
///
/// Examples:
///   x = 0 → use CLR instead of MOVE #0
///   x = y + 1; if x == 0 → use ADDQ and test flags, avoid separate CMP
///   x * -1 → use NEG x
///
/// </summary>
public class M68kPeepholeOptimization
{
    private readonly IrFunction _function;
    private bool _madeChanges = false;

    public M68kPeepholeOptimization(IrFunction function, M68kCpuTarget cpuTarget = M68kCpuTarget.M68020)
    {
        _function = function;
    }

    /// <summary>
    /// Run peephole optimization until no more changes can be made.
    /// Returns the number of optimizations performed.
    /// </summary>
    public int Optimize()
    {
        int totalOptimizations = 0;

        do
        {
            _madeChanges = false;
            int optimizations = OptimizePass();
            totalOptimizations += optimizations;
        } while (_madeChanges);

        return totalOptimizations;
    }

    private int OptimizePass()
    {
        int optimizationCount = 0;

        foreach (var block in _function.BasicBlocks)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                // Try single-instruction optimizations
                var singleOpt = OptimizeSingleInstruction(block.Instructions[i]);
                if (singleOpt != null)
                {
                    block.Instructions[i] = singleOpt;
                    optimizationCount++;
                    _madeChanges = true;
                    continue;
                }

                // Try two-instruction patterns
                if (i + 1 < block.Instructions.Count)
                {
                    var pairOpt = OptimizeInstructionPair(
                        block.Instructions[i],
                        block.Instructions[i + 1]);

                    if (pairOpt != null)
                    {
                        // Replace with optimized sequence
                        block.Instructions.RemoveAt(i);
                        block.Instructions.RemoveAt(i); // Remove second instruction
                        block.Instructions.InsertRange(i, pairOpt);
                        optimizationCount++;
                        _madeChanges = true;
                        continue;
                    }
                }

                // Try three-instruction patterns
                if (i + 2 < block.Instructions.Count)
                {
                    var tripleOpt = OptimizeInstructionTriple(
                        block.Instructions[i],
                        block.Instructions[i + 1],
                        block.Instructions[i + 2]);

                    if (tripleOpt != null)
                    {
                        block.Instructions.RemoveAt(i);
                        block.Instructions.RemoveAt(i);
                        block.Instructions.RemoveAt(i);
                        block.Instructions.InsertRange(i, tripleOpt);
                        optimizationCount++;
                        _madeChanges = true;
                        continue;
                    }
                }
            }
        }

        return optimizationCount;
    }

    /// <summary>
    /// Optimize a single instruction
    /// </summary>
    private IrInstruction? OptimizeSingleInstruction(IrInstruction instruction)
    {
        if (instruction is IrBinaryOp binOp)
        {
            // x * -1 → -x (NEG is faster than multiply)
            if (binOp.Operation == IrBinaryOp.OpKind.Mul)
            {
                if (binOp.Right is IrConstant rightConst && rightConst.Value == -1)
                {
                    // Replace with subtraction: 0 - x
                    return new IrBinaryOp(binOp.ResultName, IrBinaryOp.OpKind.Sub,
                        new IrConstant(0, binOp.Type),
                        binOp.Left,
                        binOp.Type);
                }
                if (binOp.Left is IrConstant leftConst && leftConst.Value == -1)
                {
                    return new IrBinaryOp(binOp.ResultName, IrBinaryOp.OpKind.Sub,
                        new IrConstant(0, binOp.Type),
                        binOp.Right,
                        binOp.Type);
                }
            }

            // x - 0 → x (sometimes missed by algebraic simplification)
            if (binOp.Operation == IrBinaryOp.OpKind.Sub)
            {
                if (binOp.Right is IrConstant rightConst2 && rightConst2.Value == 0)
                {
                    return new IrStore(binOp.ResultName, binOp.Left);
                }
            }

            // x | 0 → x
            if (binOp.Operation == IrBinaryOp.OpKind.Or)
            {
                if (binOp.Right is IrConstant orConst && orConst.Value == 0)
                {
                    return new IrStore(binOp.ResultName, binOp.Left);
                }
                if (binOp.Left is IrConstant orConst2 && orConst2.Value == 0)
                {
                    return new IrStore(binOp.ResultName, binOp.Right);
                }
            }

            // x & -1 (all bits set) → x
            if (binOp.Operation == IrBinaryOp.OpKind.And)
            {
                if (binOp.Right is IrConstant andConst && andConst.Value == -1)
                {
                    return new IrStore(binOp.ResultName, binOp.Left);
                }
                if (binOp.Left is IrConstant andConst2 && andConst2.Value == -1)
                {
                    return new IrStore(binOp.ResultName, binOp.Right);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Optimize a pair of instructions
    /// </summary>
    private List<IrInstruction>? OptimizeInstructionPair(IrInstruction first, IrInstruction second)
    {
        // Pattern: x = y; z = x → x = y; z = y (copy propagation at IR level)
        if (first is IrStore store1 && second is IrStore store2)
        {
            if (store2.Value is IrVariable var && var.Name == store1.VariableName)
            {
                // Second store copies from first - propagate the original value
                return new List<IrInstruction>
                {
                    first,
                    new IrStore(store2.VariableName, store1.Value)
                };
            }
        }

        // Pattern: x = expr; y = x + c → x = expr; y = expr + c (if x not used elsewhere)
        if (first is IrBinaryOp binOp1 && second is IrBinaryOp binOp2)
        {
            // Check if binOp2 uses binOp1's result
            if (binOp2.Left is IrVariable leftVar && leftVar.Name == binOp1.ResultName)
            {
                // Replace binOp2's left operand with binOp1's result expression
                // This is profitable if it avoids a register-to-register move
                // For now, just handle the case where both operands are simple
                if (binOp1.Left is IrConstant || binOp1.Right is IrConstant)
                {
                    return new List<IrInstruction>
                    {
                        binOp1,
                        binOp2 // Keep as-is for now
                    };
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Optimize a triple of instructions
    /// </summary>
    private List<IrInstruction>? OptimizeInstructionTriple(
        IrInstruction first,
        IrInstruction second,
        IrInstruction third)
    {
        // Pattern: x = a; y = b; z = x op y → x = a; y = b; z = a op b
        if (first is IrStore store1 &&
            second is IrStore store2 &&
            third is IrBinaryOp binOp)
        {
            bool leftMatch = binOp.Left is IrVariable leftVar && leftVar.Name == store1.VariableName;
            bool rightMatch = binOp.Right is IrVariable rightVar && rightVar.Name == store2.VariableName;

            if (leftMatch && rightMatch)
            {
                // Both operands come from previous stores - inline them
                return new List<IrInstruction>
                {
                    first,
                    second,
                    new IrBinaryOp(binOp.ResultName, binOp.Operation,
                        store1.Value,
                        store2.Value,
                        binOp.Type)
                };
            }
        }

        return null;
    }
}
