using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Common subexpression elimination pass
/// Eliminates redundant computations of the same expression
/// Example: let x = a + b; let y = a + b; => let x = a + b; let y = x;
///
/// REFACTORED: Now uses IrRewriter for IR traversal and modification instead of manual switch statements.
/// The visitor pattern allows automatic propagation through all instruction types.
/// </summary>
public class CommonSubexpressionEliminationPass : BasicBlockPassBase
{
    public override string Name => "Common Subexpression Elimination";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        var rewriter = new CommonSubexpressionEliminationRewriter();
        rewriter.RewriteBasicBlock(block);
        return rewriter.Changed;
    }

    /// <summary>
    /// IrRewriter implementation that eliminates common subexpressions
    /// </summary>
    private class CommonSubexpressionEliminationRewriter : IrRewriter
    {
        public bool Changed { get; private set; }

        // Map from expression signature to the variable that holds the result
        private readonly Dictionary<string, string> _expressions = new();
        private readonly Dictionary<string, string> _memberLoads = new();

        // Track replacements from old variable names to new variable references
        private readonly Dictionary<string, IrValue> _replacements = new();

        public override IrInstruction? RewriteInstruction(IrInstruction instruction)
        {
            var rewritten = base.RewriteInstruction(instruction);
            if (instruction is IrCall or IrIndirectCall or IrStore or IrMemberStore
                or IrIndexStore or IrIndexedFieldStore or IrDereferenceStore
                or IrStoreCapture or IrDropInPlace)
            {
                _expressions.Clear();
                _memberLoads.Clear();
            }
            if (instruction is IrBranch or IrConditionalBranch or IrReturn or IrLabel)
            {
                _expressions.Clear();
                _memberLoads.Clear();
            }
            return rewritten;
        }

        public override IrInstruction? RewriteBinaryOp(IrBinaryOp binaryOp)
        {
            // First rewrite operands (applies any pending replacements)
            binaryOp.Left = RewriteValue(binaryOp.Left);
            binaryOp.Right = RewriteValue(binaryOp.Right);

            // Compute the signature of this expression
            var signature = GetExpressionSignature(binaryOp);
            if (signature != null && _expressions.TryGetValue(signature, out var previousResult))
            {
                // This expression has already been computed
                // Replace all uses of this result with the previous result
                _replacements[binaryOp.ResultName] = new IrVariable(previousResult, binaryOp.Type);
                Changed = true;

                // Remove this instruction
                return null;
            }
            else if (signature != null)
            {
                // Track this expression
                _expressions[signature] = binaryOp.ResultName;
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

        public override IrInstruction? RewriteMemberAccess(IrMemberAccess memberAccess)
        {
            memberAccess.Struct = RewriteValue(memberAccess.Struct);
            var source = GetStableValueKey(memberAccess.Struct);
            if (!IsScalar(memberAccess.FieldType) || source == null)
                return memberAccess;

            var signature = $"{source}.{memberAccess.FieldName}";
            if (_memberLoads.TryGetValue(signature, out var previousResult))
            {
                _replacements[memberAccess.ResultName] = new IrVariable(previousResult, memberAccess.FieldType);
                Changed = true;
                return null;
            }

            _memberLoads[signature] = memberAccess.ResultName;
            return memberAccess;
        }

        public override IrInstruction? RewriteDefer(IrDefer defer)
        {
            // Deferred code is a separate execution region. Its available values must not
            // leak into the registering block (or vice versa), but outer replacements still
            // need to be visible while rewriting references captured by the defer.
            var outerExpressions = new Dictionary<string, string>(_expressions);
            var outerMemberLoads = new Dictionary<string, string>(_memberLoads);
            var outerReplacements = new Dictionary<string, IrValue>(_replacements);
            _expressions.Clear();
            _memberLoads.Clear();

            defer.DeferredBlock = RewriteBasicBlock(defer.DeferredBlock);

            _expressions.Clear();
            _memberLoads.Clear();
            _replacements.Clear();
            foreach (var pair in outerExpressions)
                _expressions.Add(pair.Key, pair.Value);
            foreach (var pair in outerMemberLoads)
                _memberLoads.Add(pair.Key, pair.Value);
            foreach (var pair in outerReplacements)
                _replacements.Add(pair.Key, pair.Value);
            return defer;
        }

        private static bool IsScalar(IrType type) => type is IrBoolType or IrIntType
            or IrFloatType or IrFixedType or IrPointerType or IrReferenceType or IrMutReferenceType;

        private static string? GetStableValueKey(IrValue value) => value switch
        {
            IrVariable variable => variable.Name,
            IrDereferenceValue { IsVolatile: false } dereference =>
                GetStableValueKey(dereference.PointerValue) is { } inner ? $"*{inner}" : null,
            IrBorrowValue borrow =>
                GetStableValueKey(borrow.BorrowedValue) is { } inner ? $"&{inner}" : null,
            IrCastValue cast =>
                GetStableValueKey(cast.Value) is { } inner ? $"({cast.Type.Name}){inner}" : null,
            _ => null
        };

        /// <summary>
        /// Generate a signature for a binary operation that can be used for CSE
        /// Returns null if the operation is not suitable for CSE
        /// </summary>
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

        /// <summary>
        /// Generate a signature for a value
        /// </summary>
        private string? GetValueSignature(IrValue value)
        {
            return value switch
            {
                IrConstant c => $"const:{c.Value}",
                IrVariable v => $"var:{v.Name}",
                _ => null
            };
        }

        /// <summary>
        /// Check if a binary operation is commutative
        /// </summary>
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
    }
}
