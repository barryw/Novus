using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Constant folding optimization pass
/// Evaluates binary operations with constant operands at compile time
/// Example: 2 + 3 => 5
/// </summary>
public class ConstantFoldingPass : BasicBlockPassBase
{
    public override string Name => "Constant Folding";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;

        for (int i = 0; i < block.Instructions.Count; i++)
        {
            if (block.Instructions[i] is IrBinaryOp binOp)
            {
                // Check if both operands are constants
                if (binOp.Left is IrConstant leftConst && binOp.Right is IrConstant rightConst)
                {
                    var result = EvaluateConstantOp(binOp.Operation, leftConst.Value, rightConst.Value);
                    if (result.HasValue)
                    {
                        // Replace the binary op with a constant
                        var newConstant = new IrConstant(result.Value, binOp.Type);

                        // Update all uses of this binary op result to use the constant instead
                        ReplaceUses(block, binOp.ResultName, newConstant);

                        // Remove the binary op instruction
                        block.Instructions.RemoveAt(i);
                        i--; // Adjust index after removal

                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    private long? EvaluateConstantOp(IrBinaryOp.OpKind operation, long left, long right)
    {
        try
        {
            return operation switch
            {
                IrBinaryOp.OpKind.Add => checked(left + right),
                IrBinaryOp.OpKind.Sub => checked(left - right),
                IrBinaryOp.OpKind.Mul => checked(left * right),
                IrBinaryOp.OpKind.Div => right != 0 ? left / right : null,
                IrBinaryOp.OpKind.Mod => right != 0 ? left % right : null,
                IrBinaryOp.OpKind.And => left & right,
                IrBinaryOp.OpKind.Or => left | right,
                IrBinaryOp.OpKind.Xor => left ^ right,
                IrBinaryOp.OpKind.Shl => left << (int)right,
                IrBinaryOp.OpKind.Shr => left >> (int)right,
                IrBinaryOp.OpKind.Eq => left == right ? 1 : 0,
                IrBinaryOp.OpKind.Ne => left != right ? 1 : 0,
                IrBinaryOp.OpKind.Lt => left < right ? 1 : 0,
                IrBinaryOp.OpKind.Le => left <= right ? 1 : 0,
                IrBinaryOp.OpKind.Gt => left > right ? 1 : 0,
                IrBinaryOp.OpKind.Ge => left >= right ? 1 : 0,
                _ => null
            };
        }
        catch (OverflowException)
        {
            // Don't fold operations that would overflow
            return null;
        }
    }

    private void ReplaceUses(IrBasicBlock block, string oldName, IrValue newValue)
    {
        foreach (var instruction in block.Instructions)
        {
            if (instruction is IrBinaryOp binOp)
            {
                if (binOp.Left is IrVariable leftVar && leftVar.Name == oldName)
                {
                    binOp.Left = newValue;
                }
                if (binOp.Right is IrVariable rightVar && rightVar.Name == oldName)
                {
                    binOp.Right = newValue;
                }
            }
            else if (instruction is IrCall call)
            {
                // Replace in call arguments
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    if (call.Arguments[i] is IrVariable argVar && argVar.Name == oldName)
                    {
                        call.Arguments[i] = newValue;
                    }
                }
            }
            else if (instruction is IrReturn ret)
            {
                if (ret.Value is IrVariable retVar && retVar.Name == oldName)
                {
                    ret.Value = newValue;
                }
            }
        }
    }
}
