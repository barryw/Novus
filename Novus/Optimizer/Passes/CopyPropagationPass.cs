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
        // Track variables declared with 'let' that shouldn't be propagated across scopes
        var localDecls = new HashSet<string>();

        foreach (var instruction in block.Instructions)
        {
            // Track copy assignments: x = y
            if (instruction is IrLocalDecl decl)
            {
                // Mark this as a local declaration (let/var binding)
                localDecls.Add(decl.Name);

                if (decl.InitialValue is IrVariable sourceVar)
                {
                    // This is a copy: let x = y
                    // Only track if the source is NOT a local decl (to avoid scope issues)
                    if (!localDecls.Contains(sourceVar.Name))
                    {
                        copies[decl.Name] = sourceVar;
                    }
                }
                else
                {
                    // Not a copy - remove from tracking if it exists
                    copies.Remove(decl.Name);
                }
            }
            else if (instruction is IrStore store)
            {
                // First, invalidate any copies that reference this variable
                // because we're about to modify it
                InvalidateCopiesReferencingVariable(store.VariableName, copies);

                if (store.Value is IrVariable sourceVar)
                {
                    // This is a copy: x = y
                    // Only track if the source is NOT a local decl (to avoid scope issues)
                    if (!localDecls.Contains(sourceVar.Name))
                    {
                        copies[store.VariableName] = sourceVar;
                    }
                }
                else
                {
                    // Not a copy - remove from tracking
                    copies.Remove(store.VariableName);
                }
            }
            else if (instruction is IrBinaryOp binOp)
            {
                // Replace operands if they're copies
                if (binOp.Left is IrVariable leftVar && copies.ContainsKey(leftVar.Name))
                {
                    var replacement = copies[leftVar.Name];
                    // Only propagate if replacement is not a local decl (scope safety)
                    if (replacement is IrVariable replVar && !localDecls.Contains(replVar.Name))
                    {
                        binOp.Left = replacement;
                        changed = true;
                    }
                }

                if (binOp.Right is IrVariable rightVar && copies.ContainsKey(rightVar.Name))
                {
                    var replacement = copies[rightVar.Name];
                    // Only propagate if replacement is not a local decl (scope safety)
                    if (replacement is IrVariable replVar && !localDecls.Contains(replVar.Name))
                    {
                        binOp.Right = replacement;
                        changed = true;
                    }
                }

                // The result invalidates any copies that reference this variable
                InvalidateCopiesReferencingVariable(binOp.ResultName, copies);
                copies.Remove(binOp.ResultName);
            }
            else if (instruction is IrReturn ret)
            {
                if (ret.Value is IrVariable retVar && copies.ContainsKey(retVar.Name))
                {
                    var replacement = copies[retVar.Name];
                    // Only propagate if replacement is not a local decl (scope safety)
                    if (replacement is IrVariable replVar && !localDecls.Contains(replVar.Name))
                    {
                        ret.Value = replacement;
                        changed = true;
                    }
                }
            }
            else if (instruction is IrConditionalBranch condBranch)
            {
                if (condBranch.Condition is IrVariable condVar && copies.ContainsKey(condVar.Name))
                {
                    var replacement = copies[condVar.Name];
                    // Only propagate if replacement is not a local decl (scope safety)
                    if (replacement is IrVariable replVar && !localDecls.Contains(replVar.Name))
                    {
                        condBranch.Condition = replacement;
                        changed = true;
                    }
                }
            }
            else if (instruction is IrCall call)
            {
                // Propagate copies into call arguments
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    if (call.Arguments[i] is IrVariable argVar && copies.ContainsKey(argVar.Name))
                    {
                        var replacement = copies[argVar.Name];
                        // Only propagate if replacement is not a local decl (scope safety)
                        if (replacement is IrVariable replVar && !localDecls.Contains(replVar.Name))
                        {
                            call.Arguments[i] = replacement;
                            changed = true;
                        }
                    }
                }

                // Function calls may have side effects - invalidate all copies
                // to be conservative (could be improved with side-effect analysis)
                copies.Clear();
            }
        }

        return changed;
    }

    /// <summary>
    /// Invalidate all copies that reference the given variable.
    /// When a variable is modified, any copy relationships that reference it are no longer valid.
    /// For example: let a = x; x = 5; // Now 'a' no longer refers to the current value of 'x'
    /// </summary>
    private void InvalidateCopiesReferencingVariable(string variableName, Dictionary<string, IrValue> copies)
    {
        var toRemove = new List<string>();
        foreach (var kvp in copies)
        {
            if (kvp.Value is IrVariable copyVar && copyVar.Name == variableName)
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            copies.Remove(key);
        }
    }
}
