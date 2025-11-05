using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Dead code elimination pass
/// Removes instructions whose results are never used
/// Example: let x = 5; return 10; // x is never used, can be removed
///
/// REFACTORED: Now uses IrVisitor for collecting uses instead of manual switch statements.
/// The visitor pattern automatically handles all instruction and value types.
/// </summary>
public class DeadCodeEliminationPass : FunctionPassBase
{
    public override string Name => "Dead Code Elimination";

    public override bool RunOnFunction(IrFunction function)
    {
        bool changed = false;

        foreach (var block in function.BasicBlocks)
        {
            // Build a set of all used variables in this block using the visitor
            var collector = new UseCollectorVisitor();
            collector.VisitBasicBlock(block, null);
            var usedVars = collector.UsedVariables;

            // Second pass: remove unused instructions
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                if (block.Instructions[i] is IrBinaryOp binOp)
                {
                    // If this result is never used, remove it
                    if (!usedVars.Contains(binOp.ResultName))
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

    /// <summary>
    /// Visitor that collects all used variable names
    /// </summary>
    private class UseCollectorVisitor : IrVisitor<object?, object?>
    {
        public HashSet<string> UsedVariables { get; } = new();

        public override object? VisitVariable(IrVariable variable, object? context)
        {
            UsedVariables.Add(variable.Name);
            return null;
        }

        // Note: The base visitor automatically traverses all instructions and values,
        // so we only need to override the specific value types we care about.
        // IrDereferenceValue, IrBorrowValue, etc. are automatically handled because
        // the base class calls VisitValue recursively on their inner values.
    }
}
