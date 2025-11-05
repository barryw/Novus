using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Algebraic simplification pass
/// Applies algebraic identities to simplify expressions
/// Examples:
///   x + 0 => x
///   x * 1 => x
///   x * 0 => 0
///   x - 0 => x
///   x - x => 0
///   x / 1 => x
///   x % 1 => 0
///   x << 0 => x
///   x >> 0 => x
///   x | 0 => x
///   x | -1 => -1
///   x & 0 => 0
///   x & -1 => x
///   x ^ 0 => x
///   x ^ x => 0
///
/// REFACTORED: Now uses IrRewriter for IR traversal and modification instead of manual switch statements.
/// The visitor pattern allows automatic propagation through all instruction types.
/// </summary>
public class AlgebraicSimplificationPass : BasicBlockPassBase
{
    public override string Name => "Algebraic Simplification";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        var rewriter = new AlgebraicSimplificationRewriter();
        rewriter.RewriteBasicBlock(block);
        return rewriter.Changed;
    }

    /// <summary>
    /// IrRewriter implementation that applies algebraic simplifications
    /// </summary>
    private class AlgebraicSimplificationRewriter : IrRewriter
    {
        public bool Changed { get; private set; }

        // Track replacements from old variable names to simplified values
        private readonly Dictionary<string, IrValue> _replacements = new();

        public override IrInstruction? RewriteBinaryOp(IrBinaryOp binaryOp)
        {
            // First rewrite operands (applies any pending replacements)
            binaryOp.Left = RewriteValue(binaryOp.Left);
            binaryOp.Right = RewriteValue(binaryOp.Right);

            // Try to simplify the binary operation
            var simplified = TrySimplify(binaryOp);
            if (simplified != null)
            {
                // Track this replacement so future uses of the result get replaced
                _replacements[binaryOp.ResultName] = simplified;
                Changed = true;

                // Return null to delete this instruction
                return null;
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

        /// <summary>
        /// Try to simplify a binary operation using algebraic identities
        /// Returns the simplified value if successful, null otherwise
        /// </summary>
        private IrValue? TrySimplify(IrBinaryOp binOp)
        {
            // Get constant values if operands are constants
            var leftConst = binOp.Left as IrConstant;
            var rightConst = binOp.Right as IrConstant;

            // Check for identity operations
            return binOp.Operation switch
            {
                IrBinaryOp.OpKind.Add => TrySimplifyAdd(binOp, leftConst, rightConst),
                IrBinaryOp.OpKind.Sub => TrySimplifySubtract(binOp, leftConst, rightConst),
                IrBinaryOp.OpKind.Mul => TrySimplifyMultiply(binOp, leftConst, rightConst),
                IrBinaryOp.OpKind.Div => TrySimplifyDivide(binOp, leftConst, rightConst),
                IrBinaryOp.OpKind.Mod => TrySimplifyModulo(binOp, leftConst, rightConst),
                IrBinaryOp.OpKind.Shl or IrBinaryOp.OpKind.Shr => TrySimplifyShift(binOp, leftConst, rightConst),
                IrBinaryOp.OpKind.Or => TrySimplifyBitOr(binOp, leftConst, rightConst),
                IrBinaryOp.OpKind.And => TrySimplifyBitAnd(binOp, leftConst, rightConst),
                IrBinaryOp.OpKind.Xor => TrySimplifyBitXor(binOp, leftConst, rightConst),
                _ => null
            };
        }

        private IrValue? TrySimplifyAdd(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x + 0 => x
            if (rightConst != null && IsZero(rightConst))
            {
                return binOp.Left;
            }

            // 0 + x => x
            if (leftConst != null && IsZero(leftConst))
            {
                return binOp.Right;
            }

            return null;
        }

        private IrValue? TrySimplifySubtract(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x - 0 => x
            if (rightConst != null && IsZero(rightConst))
            {
                return binOp.Left;
            }

            // x - x => 0 (only for variables, not expressions)
            if (binOp.Left is IrVariable leftVar &&
                binOp.Right is IrVariable rightVar &&
                leftVar.Name == rightVar.Name)
            {
                return CreateZeroConstant(binOp.Type);
            }

            return null;
        }

        private IrValue? TrySimplifyMultiply(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x * 0 => 0
            if (rightConst != null && IsZero(rightConst))
            {
                return CreateZeroConstant(binOp.Type);
            }

            // 0 * x => 0
            if (leftConst != null && IsZero(leftConst))
            {
                return CreateZeroConstant(binOp.Type);
            }

            // x * 1 => x
            if (rightConst != null && IsOne(rightConst))
            {
                return binOp.Left;
            }

            // 1 * x => x
            if (leftConst != null && IsOne(leftConst))
            {
                return binOp.Right;
            }

            return null;
        }

        private IrValue? TrySimplifyDivide(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x / 1 => x
            if (rightConst != null && IsOne(rightConst))
            {
                return binOp.Left;
            }

            // 0 / x => 0 (assuming x != 0, which is UB anyway)
            if (leftConst != null && IsZero(leftConst))
            {
                return CreateZeroConstant(binOp.Type);
            }

            return null;
        }

        private IrValue? TrySimplifyModulo(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x % 1 => 0
            if (rightConst != null && IsOne(rightConst))
            {
                return CreateZeroConstant(binOp.Type);
            }

            // 0 % x => 0 (assuming x != 0)
            if (leftConst != null && IsZero(leftConst))
            {
                return CreateZeroConstant(binOp.Type);
            }

            return null;
        }

        private IrValue? TrySimplifyShift(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x << 0 => x
            // x >> 0 => x
            if (rightConst != null && IsZero(rightConst))
            {
                return binOp.Left;
            }

            // 0 << x => 0
            // 0 >> x => 0
            if (leftConst != null && IsZero(leftConst))
            {
                return CreateZeroConstant(binOp.Type);
            }

            return null;
        }

        private IrValue? TrySimplifyBitOr(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x | 0 => x
            if (rightConst != null && IsZero(rightConst))
            {
                return binOp.Left;
            }

            // 0 | x => x
            if (leftConst != null && IsZero(leftConst))
            {
                return binOp.Right;
            }

            // x | -1 => -1 (all bits set)
            if (rightConst != null && IsAllOnes(rightConst))
            {
                return rightConst;
            }

            // -1 | x => -1
            if (leftConst != null && IsAllOnes(leftConst))
            {
                return leftConst;
            }

            return null;
        }

        private IrValue? TrySimplifyBitAnd(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x & 0 => 0
            if (rightConst != null && IsZero(rightConst))
            {
                return CreateZeroConstant(binOp.Type);
            }

            // 0 & x => 0
            if (leftConst != null && IsZero(leftConst))
            {
                return CreateZeroConstant(binOp.Type);
            }

            // x & -1 => x (all bits set)
            if (rightConst != null && IsAllOnes(rightConst))
            {
                return binOp.Left;
            }

            // -1 & x => x
            if (leftConst != null && IsAllOnes(leftConst))
            {
                return binOp.Right;
            }

            return null;
        }

        private IrValue? TrySimplifyBitXor(IrBinaryOp binOp, IrConstant? leftConst, IrConstant? rightConst)
        {
            // x ^ 0 => x
            if (rightConst != null && IsZero(rightConst))
            {
                return binOp.Left;
            }

            // 0 ^ x => x
            if (leftConst != null && IsZero(leftConst))
            {
                return binOp.Right;
            }

            // x ^ x => 0 (only for variables)
            if (binOp.Left is IrVariable leftVar &&
                binOp.Right is IrVariable rightVar &&
                leftVar.Name == rightVar.Name)
            {
                return CreateZeroConstant(binOp.Type);
            }

            return null;
        }

        /// <summary>
        /// Check if a constant is zero
        /// </summary>
        private bool IsZero(IrConstant constant)
        {
            return constant.Value == 0;
        }

        /// <summary>
        /// Check if a constant is one
        /// </summary>
        private bool IsOne(IrConstant constant)
        {
            return constant.Value == 1;
        }

        /// <summary>
        /// Check if a constant has all bits set (-1 for signed, max value for unsigned)
        /// </summary>
        private bool IsAllOnes(IrConstant constant)
        {
            // Check for -1 (all bits set for signed integers)
            // or check based on type size for unsigned
            if (constant.Value == -1)
                return true;

            // For byte type, all bits set is 255
            if (constant.Type is IrIntType intType && intType.SizeInBytes == 1 && !intType.IsSigned)
                return constant.Value == 255;

            return false;
        }

        /// <summary>
        /// Create a zero constant of the given type
        /// </summary>
        private IrConstant CreateZeroConstant(IrType type)
        {
            // IrConstant.Value is always long, so just pass 0
            return new IrConstant(0, type);
        }
    }
}
