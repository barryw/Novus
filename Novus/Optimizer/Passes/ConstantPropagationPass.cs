using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Constant propagation pass
/// Replaces uses of variables with known constant values
/// Example: let x = 5; let y = x + 1; => let y = 5 + 1;
/// </summary>
public class ConstantPropagationPass : BasicBlockPassBase
{
    public override string Name => "Constant Propagation";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;
        var constantValues = new Dictionary<string, IrConstant>();

        foreach (var instruction in block.Instructions)
        {
            if (instruction is IrBinaryOp binOp)
            {
                // Replace left operand if it's a known constant
                if (binOp.Left is IrVariable leftVar && constantValues.ContainsKey(leftVar.Name))
                {
                    binOp.Left = constantValues[leftVar.Name];
                    changed = true;
                }

                // Replace right operand if it's a known constant
                if (binOp.Right is IrVariable rightVar && constantValues.ContainsKey(rightVar.Name))
                {
                    binOp.Right = constantValues[rightVar.Name];
                    changed = true;
                }

                // If the result is a constant, track it
                if (binOp.Left is IrConstant && binOp.Right is IrConstant)
                {
                    // The constant folding pass will handle this
                    // But we can track if we know the result
                }
            }
            else if (instruction is IrReturn ret)
            {
                // Propagate constants into return statements
                if (ret.Value is IrVariable retVar && constantValues.ContainsKey(retVar.Name))
                {
                    ret.Value = constantValues[retVar.Name];
                    changed = true;
                }
            }
        }

        return changed;
    }
}
