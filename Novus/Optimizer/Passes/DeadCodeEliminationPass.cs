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
                CollectUsesFromValue(binOp.Left, usedVars);
                CollectUsesFromValue(binOp.Right, usedVars);
                break;

            case IrReturn ret:
                if (ret.Value != null)
                    CollectUsesFromValue(ret.Value, usedVars);
                break;

            case IrStore store:
                CollectUsesFromValue(store.Value, usedVars);
                break;

            case IrDereferenceStore derefStore:
                CollectUsesFromValue(derefStore.Value, usedVars);
                CollectUsesFromValue(derefStore.Pointer, usedVars);
                break;

            case IrCall call:
                foreach (var arg in call.Arguments)
                    CollectUsesFromValue(arg, usedVars);
                break;

            case IrConditionalBranch condBranch:
                CollectUsesFromValue(condBranch.Condition, usedVars);
                break;

            case IrIndexStore indexStore:
                CollectUsesFromValue(indexStore.Index, usedVars);
                CollectUsesFromValue(indexStore.Value, usedVars);
                break;

            case IrLocalDecl localDecl:
                CollectUsesFromValue(localDecl.InitialValue, usedVars);
                break;

            case IrIndexAccess indexAccess:
                if (indexAccess.Array is IrVariable arrayVar)
                    usedVars.Add(arrayVar.Name);
                CollectUsesFromValue(indexAccess.Index, usedVars);
                break;
        }
    }

    private void CollectUsesFromValue(IrValue value, HashSet<string> usedVars)
    {
        if (value is IrVariable var)
        {
            usedVars.Add(var.Name);
        }
        else if (value is IrDereferenceValue deref)
        {
            CollectUsesFromValue(deref.PointerValue, usedVars);
        }
        else if (value is IrBorrowValue borrow)
        {
            CollectUsesFromValue(borrow.BorrowedValue, usedVars);
        }
        // Note: BinaryOp can appear as a value, but we handle it as an instruction
        // IrConstant values don't reference variables
    }
}
