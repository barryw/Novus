using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Constant folding optimization pass
/// Evaluates binary operations with constant operands at compile time
/// Example: 2 + 3 => 5
///
/// REFACTORED: Now uses IrRewriter for IR traversal instead of manual switch statements.
/// The visitor pattern allows automatic traversal of all instruction types.
/// </summary>
public class ConstantFoldingPass : BasicBlockPassBase
{
    public override string Name => "Constant Folding";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        var rewriter = new ConstantFoldingRewriter();
        rewriter.RewriteBasicBlock(block);
        return rewriter.Changed;
    }

    /// <summary>
    /// IrRewriter implementation that folds constant binary operations
    /// </summary>
    private class ConstantFoldingRewriter : IrRewriter
    {
        public bool Changed { get; private set; }

        // Track replacements from old variable names to new constant values
        private readonly Dictionary<string, IrValue> _replacements = new();

        public override IrInstruction? RewriteBinaryOp(IrBinaryOp binaryOp)
        {
            // First rewrite operands (applies any pending replacements)
            binaryOp.Left = RewriteValue(binaryOp.Left);
            binaryOp.Right = RewriteValue(binaryOp.Right);

            // Check if both operands are constants after rewriting
            if (binaryOp.Left is IrConstant leftConst && binaryOp.Right is IrConstant rightConst)
            {
                var result = EvaluateConstantOp(binaryOp.Operation, leftConst.Value, rightConst.Value);
                if (result.HasValue)
                {
                    // Replace the binary op with a constant
                    var newConstant = new IrConstant(result.Value, binaryOp.Type);

                    // Track this replacement so future uses of the result get replaced
                    _replacements[binaryOp.ResultName] = newConstant;

                    Changed = true;

                    // Return null to delete this instruction
                    return null;
                }
            }

            return binaryOp;
        }

        public override IrValue RewriteVariable(IrVariable variable)
        {
            // Apply any tracked replacements
            if (_replacements.TryGetValue(variable.Name, out var replacement))
            {
                return replacement;
            }
            return variable;
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
    }
}
