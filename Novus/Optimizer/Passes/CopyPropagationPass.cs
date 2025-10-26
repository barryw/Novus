using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Copy propagation pass
/// Replaces uses of copied variables with the original
/// Example: let x = y; let z = x; => let z = y;
/// </summary>
public class CopyPropagationPass : BasicBlockPassBase
{
    public override string Name => "Copy Propagation";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;
        // Map from variable name to the value it copies
        var copies = new Dictionary<string, IrValue>();

        foreach (var instruction in block.Instructions)
        {
            if (instruction is IrBinaryOp binOp)
            {
                // Replace operands if they're copies
                if (binOp.Left is IrVariable leftVar && copies.ContainsKey(leftVar.Name))
                {
                    binOp.Left = copies[leftVar.Name];
                    changed = true;
                }

                if (binOp.Right is IrVariable rightVar && copies.ContainsKey(rightVar.Name))
                {
                    binOp.Right = copies[rightVar.Name];
                    changed = true;
                }
            }
            else if (instruction is IrReturn ret)
            {
                if (ret.Value is IrVariable retVar && copies.ContainsKey(retVar.Name))
                {
                    ret.Value = copies[retVar.Name];
                    changed = true;
                }
            }
        }

        return changed;
    }
}
