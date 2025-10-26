using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Dead code elimination pass
/// Removes instructions whose results are never used
/// Example: let x = 5; return 10; // x is never used, can be removed
/// </summary>
public class DeadCodeEliminationPass : FunctionPassBase
{
    public override string Name => "Dead Code Elimination";

    public override bool RunOnFunction(IrFunction function)
    {
        bool changed = false;

        foreach (var block in function.BasicBlocks)
        {
            // Build a set of all used variables in this block
            var usedVars = new HashSet<string>();

            // First pass: collect all uses
            foreach (var instruction in block.Instructions)
            {
                CollectUses(instruction, usedVars);
            }

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

    private void CollectUses(IrInstruction instruction, HashSet<string> usedVars)
    {
        switch (instruction)
        {
            case IrBinaryOp binOp:
                if (binOp.Left is IrVariable leftVar)
                    usedVars.Add(leftVar.Name);
                if (binOp.Right is IrVariable rightVar)
                    usedVars.Add(rightVar.Name);
                break;

            case IrReturn ret:
                if (ret.Value is IrVariable retVar)
                    usedVars.Add(retVar.Name);
                break;
        }
    }
}
