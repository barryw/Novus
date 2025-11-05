using Novus.IR;
using Novus.IR.Analysis;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Dead code elimination pass
/// Removes instructions whose results are never used
/// Example: let x = 5; return 10; // x is never used, can be removed
///
/// REFACTORED: Now uses DefUseAnalysis infrastructure instead of manually collecting uses.
/// The centralized analysis provides def-use information across the entire function.
/// </summary>
public class DeadCodeEliminationPass : FunctionPassBase
{
    public override string Name => "Dead Code Elimination";

    public override bool RunOnFunction(IrFunction function)
    {
        // Perform def-use analysis once for the entire function
        var defUse = DefUseAnalysis.Analyze(function);

        bool changed = false;

        foreach (var block in function.BasicBlocks)
        {
            // Remove unused instructions
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                if (block.Instructions[i] is IrBinaryOp binOp)
                {
                    // Use DefUseAnalysis.IsUsed() instead of checking if variable in set
                    if (!defUse.IsUsed(binOp.ResultName))
                    {
                        block.Instructions.RemoveAt(i);
                        i--; // Adjust index after removal
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }
}
