using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Common subexpression elimination pass
/// Eliminates redundant computations of the same expression
/// Example: let x = a + b; let y = a + b; => let x = a + b; let y = x;
/// </summary>
public class CommonSubexpressionEliminationPass : BasicBlockPassBase
{
    public override string Name => "Common Subexpression Elimination";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;
        // Map from expression signature to the variable that holds the result
        var expressions = new Dictionary<string, string>();

        for (int i = 0; i < block.Instructions.Count; i++)
        {
            if (block.Instructions[i] is IrBinaryOp binOp)
            {
                var signature = GetExpressionSignature(binOp);
                if (signature != null && expressions.ContainsKey(signature))
                {
                    // This expression has already been computed
                    // Replace all uses of this result with the previous result
                    var previousResult = expressions[signature];
                    ReplaceUses(block, binOp.ResultName, new IrVariable(previousResult, binOp.Type));

                    // Remove this instruction
                    block.Instructions.RemoveAt(i);
                    i--;
                    changed = true;
                }
                else if (signature != null)
                {
                    // Track this expression
                    expressions[signature] = binOp.ResultName;
                }
            }
        }

        return changed;
    }

    private string? GetExpressionSignature(IrBinaryOp binOp)
    {
        // Only eliminate if both operands are either constants or variables (not other temp results)
        var leftSig = GetValueSignature(binOp.Left);
        var rightSig = GetValueSignature(binOp.Right);

        if (leftSig == null || rightSig == null)
            return null;

        // For commutative operations, normalize the order
        if (IsCommutative(binOp.Operation))
        {
            if (string.CompareOrdinal(leftSig, rightSig) > 0)
            {
                (leftSig, rightSig) = (rightSig, leftSig);
            }
        }

        return $"{binOp.Operation}({leftSig}, {rightSig})";
    }

    private string? GetValueSignature(IrValue value)
    {
        return value switch
        {
            IrConstant c => $"const:{c.Value}",
            IrVariable v => $"var:{v.Name}",
            _ => null
        };
    }

    private bool IsCommutative(IrBinaryOp.OpKind op)
    {
        return op switch
        {
            IrBinaryOp.OpKind.Add => true,
            IrBinaryOp.OpKind.Mul => true,
            IrBinaryOp.OpKind.And => true,
            IrBinaryOp.OpKind.Or => true,
            IrBinaryOp.OpKind.Xor => true,
            IrBinaryOp.OpKind.Eq => true,
            IrBinaryOp.OpKind.Ne => true,
            _ => false
        };
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
