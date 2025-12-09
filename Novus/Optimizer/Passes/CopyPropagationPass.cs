using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Copy propagation pass
/// Replaces uses of copied variables with the original
/// Example: let x = y; let z = x; => let z = y;
///
/// REFACTORED: Now uses IrRewriter for IR traversal and modification instead of manual switch statements.
/// The visitor pattern allows automatic propagation through all instruction types.
/// </summary>
public class CopyPropagationPass : BasicBlockPassBase
{
    public override string Name => "Copy Propagation";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        var rewriter = new CopyPropagationRewriter();
        rewriter.RewriteBasicBlock(block);
        return rewriter.Changed;
    }

    /// <summary>
    /// IrRewriter implementation that propagates copies
    /// </summary>
    private class CopyPropagationRewriter : IrRewriter
    {
        public bool Changed { get; private set; }

        // Map from variable name to the value it copies
        private readonly Dictionary<string, IrValue> _copies = new();

        // Track variables declared with 'let' that shouldn't be propagated across scopes
        private readonly HashSet<string> _localDecls = new();

        public override IrInstruction RewriteLocalDecl(IrLocalDecl localDecl)
        {
            // Mark this as a local declaration (let/var binding)
            _localDecls.Add(localDecl.Name);

            // Rewrite the initial value
            if (localDecl.InitialValue != null)
            {
                localDecl.InitialValue = RewriteValue(localDecl.InitialValue);
            }

            // Check if this is a copy: let x = y
            if (localDecl.InitialValue is IrVariable sourceVar)
            {
                // Only track if the source is NOT a local decl (to avoid scope issues)
                if (!_localDecls.Contains(sourceVar.Name))
                {
                    _copies[localDecl.Name] = sourceVar;
                }
            }
            else
            {
                // Not a copy - remove from tracking if it exists
                _copies.Remove(localDecl.Name);
            }

            return localDecl;
        }

        public override IrInstruction RewriteStore(IrStore store)
        {
            // First, invalidate any copies that reference this variable
            // because we're about to modify it
            InvalidateCopiesReferencingVariable(store.VariableName);

            // Rewrite the value
            store.Value = RewriteValue(store.Value);

            // Check if this is a copy: x = y
            if (store.Value is IrVariable sourceVar)
            {
                // Only track if the source is NOT a local decl (to avoid scope issues)
                if (!_localDecls.Contains(sourceVar.Name))
                {
                    _copies[store.VariableName] = sourceVar;
                }
            }
            else
            {
                // Not a copy - remove from tracking
                _copies.Remove(store.VariableName);
            }

            return store;
        }

        public override IrInstruction RewriteBinaryOp(IrBinaryOp binaryOp)
        {
            // Rewrite operands (this will apply copy propagation)
            binaryOp.Left = RewriteValue(binaryOp.Left);
            binaryOp.Right = RewriteValue(binaryOp.Right);

            // The result invalidates any copies that reference this variable
            InvalidateCopiesReferencingVariable(binaryOp.ResultName);
            _copies.Remove(binaryOp.ResultName);

            return binaryOp;
        }

        public override IrInstruction RewriteCall(IrCall call)
        {
            // Propagate copies into call arguments
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                call.Arguments[i] = RewriteValue(call.Arguments[i]);
            }

            // Function calls may have side effects - invalidate all copies
            // to be conservative (could be improved with side-effect analysis)
            _copies.Clear();

            return call;
        }

        public override IrValue RewriteVariable(IrVariable variable)
        {
            // Apply copy propagation
            if (_copies.TryGetValue(variable.Name, out var replacement))
            {
                // Only propagate if replacement is not a local decl (scope safety)
                if (replacement is IrVariable replVar && !_localDecls.Contains(replVar.Name))
                {
                    Changed = true;
                    return replacement;
                }
            }
            return variable;
        }

        /// <summary>
        /// Invalidate all copies that reference the given variable.
        /// When a variable is modified, any copy relationships that reference it are no longer valid.
        /// For example: let a = x; x = 5; // Now 'a' no longer refers to the current value of 'x'
        /// </summary>
        private void InvalidateCopiesReferencingVariable(string variableName)
        {
            var toRemove = new List<string>();
            foreach (var kvp in _copies)
            {
                if (kvp.Value is IrVariable copyVar && copyVar.Name == variableName)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
            {
                _copies.Remove(key);
            }
        }
    }
}
