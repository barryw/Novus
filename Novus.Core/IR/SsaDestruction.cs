namespace Novus.IR;

/// <summary>
/// Converts IR from SSA form back to conventional form by eliminating phi functions.
///
/// SSA Destruction Process:
/// 1. For each phi function x = φ(a from B1, b from B2, ...):
///    - Insert copies at the end of each predecessor block
///    - B1: x = a (before branch)
///    - B2: x = b (before branch)
///
/// 2. Handle the parallel copy problem:
///    When multiple phis exist in the same block, they execute "simultaneously"
///    We need to ensure copies don't interfere with each other
///
/// Example Problem:
///    x_2 = φ(x_0, x_1)
///    y_2 = φ(y_0, x_0)  // Uses x_0 which might be overwritten by first phi
///
/// Solution: Use sequential copy algorithm with temporary variables when needed
/// </summary>
public class SsaDestruction(IrFunction function)
{
    private int _tempCounter = 0;

    /// <summary>
    /// Eliminate all phi functions from the function, converting back to non-SSA form
    /// </summary>
    public void DestructSsa()
    {
        // Build CFG once for the entire function (uses caching)
        var cfg = function.GetCFG();

        foreach (var block in function.BasicBlocks)
        {
            if (block.PhiFunctions is [])
                continue;

            EliminatePhisInBlock(block, cfg);
        }

        // Invalidate CFG since we modified instructions in predecessor blocks
        function.InvalidateCFG();
    }

    /// <summary>
    /// Eliminate all phi functions in a block by inserting copies in predecessor blocks
    /// </summary>
    private void EliminatePhisInBlock(IrBasicBlock block, ControlFlowGraph cfg)
    {
        // Use cached CFG to find predecessors
        var blockNode = cfg.Nodes.FirstOrDefault(n => n.Block == block);
        if (blockNode == null)
            return;

        // Get all predecessors
        var predecessors = blockNode.Predecessors.Select(p => p.Block).Where(b => b != null).ToList();

        // For each predecessor, determine what copies to insert
        foreach (var pred in predecessors!)
        {
            var copiesToInsert = new List<(string destVar, IrValue sourceValue)>();

            // Collect all phi functions that need copies from this predecessor
            foreach (var phi in block.PhiFunctions)
            {
                var value = phi.GetValueForBlock(pred!);
                if (value != null && phi.Destination != null)
                {
                    // Need to insert: phi.Destination = value
                    copiesToInsert.Add((phi.Destination.SsaName, value));
                }
            }

            // Insert copies at the end of predecessor block (before the branch)
            InsertCopiesBeforeBranch(pred!, copiesToInsert);
        }

        // Remove all phi functions from the block
        block.PhiFunctions.Clear();
    }

    /// <summary>
    /// Insert copies at the end of a block, before the final branch instruction.
    /// Handles the parallel copy problem by using sequential algorithm.
    /// </summary>
    private void InsertCopiesBeforeBranch(IrBasicBlock block, List<(string dest, IrValue src)> copies)
    {
        if (copies is [])
            return;

        // Find the position to insert (before the final branch/return)
        int insertPosition = block.Instructions.Count;

        // Look for the last control flow instruction
        for (int i = block.Instructions.Count - 1; i >= 0; i--)
        {
            if (block.Instructions[i] is IrBranch or IrConditionalBranch or IrReturn)
            {
                insertPosition = i;
                break;
            }
        }

        // Handle parallel copy problem using sequential copy algorithm
        var resolvedCopies = ResolveParallelCopies(copies);

        // Insert the resolved copies
        foreach (var (dest, src) in resolvedCopies)
        {
            block.Instructions.Insert(insertPosition, new IrStore(dest, src));
            insertPosition++;
        }
    }

    /// <summary>
    /// Resolve parallel copies to avoid interference.
    /// Uses a sequential algorithm that introduces temporary variables when needed.
    ///
    /// Algorithm:
    /// 1. While there are unprocessed copies:
    ///    a. Find a "ready" copy (dest not used as source in remaining copies)
    ///    b. If found, emit it
    ///    c. If not found (cycle), break cycle with temporary variable
    /// </summary>
    private List<(string dest, IrValue src)> ResolveParallelCopies(List<(string dest, IrValue src)> copies)
    {
        if (copies.Count <= 1)
            return copies; // No interference possible

        var result = new List<(string dest, IrValue src)>();
        var remaining = new List<(string dest, IrValue src)>(copies);

        while (remaining.Count > 0)
        {
            // Find a copy where destination is not used as source in remaining copies
            var readyCopy = FindReadyCopy(remaining);

            if (readyCopy.HasValue)
            {
                // Emit this copy and remove it from remaining
                result.Add(readyCopy.Value);
                remaining.Remove(readyCopy.Value);
            }
            else
            {
                // No ready copy found - there's a cycle
                // Break the cycle by using a temporary variable
                var cycleCopy = remaining[0];
                var tempName = $"_tmp_phi_{_tempCounter++}";
                var tempType = cycleCopy.src.Type;

                // temp = src
                result.Add((tempName, cycleCopy.src));

                // Replace src in remaining copies
                for (int i = 0; i < remaining.Count; i++)
                {
                    var (dest, src) = remaining[i];
                    if (src is IrVariable srcVar && srcVar.SsaName == cycleCopy.dest)
                    {
                        // Replace with temp variable
                        remaining[i] = (dest, new IrVariable(tempName, tempType));
                    }
                }

                // Update the original copy to use temp
                remaining[0] = (cycleCopy.dest, new IrVariable(tempName, tempType));
            }
        }

        return result;
    }

    /// <summary>
    /// Find a copy that's "ready" - its destination is not used as source in remaining copies
    /// </summary>
    private (string dest, IrValue src)? FindReadyCopy(List<(string dest, IrValue src)> copies)
    {
        foreach (var copy in copies)
        {
            bool destIsUsedAsSource = false;

            // Check if this copy's destination is used as a source in any other copy
            foreach (var otherCopy in copies)
            {
                if (copy.Equals(otherCopy))
                    continue;

                if (otherCopy.src is IrVariable srcVar && srcVar.SsaName == copy.dest)
                {
                    destIsUsedAsSource = true;
                    break;
                }
            }

            if (!destIsUsedAsSource)
                return copy;
        }

        return null; // No ready copy found (cycle exists)
    }

    /// <summary>
    /// Remove SSA versioning from variable names throughout the function.
    /// Converts x_0, x_1, x_2 back to just x.
    /// </summary>
    public void RemoveSsaVersioning()
    {
        foreach (var block in function.BasicBlocks)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                block.Instructions[i] = RemoveSsaVersioningFromInstruction(block.Instructions[i]);
            }
        }
    }

    /// <summary>
    /// Remove SSA versioning from a single instruction
    /// </summary>
    private IrInstruction RemoveSsaVersioningFromInstruction(IrInstruction instruction)
    {
        switch (instruction)
        {
            case IrLocalDecl decl:
                decl.Name = RemoveVersion(decl.Name);
                if (decl.InitialValue != null)
                {
                    decl.InitialValue = RemoveSsaVersioningFromValue(decl.InitialValue);
                }
                break;

            case IrStore store:
                store.VariableName = RemoveVersion(store.VariableName);
                store.Value = RemoveSsaVersioningFromValue(store.Value);
                break;

            case IrBinaryOp binOp:
                binOp.ResultName = RemoveVersion(binOp.ResultName);
                binOp.Left = RemoveSsaVersioningFromValue(binOp.Left);
                binOp.Right = RemoveSsaVersioningFromValue(binOp.Right);
                break;

            case IrCall call:
                if (call.ResultName != null)
                    call.ResultName = RemoveVersion(call.ResultName);
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    call.Arguments[i] = RemoveSsaVersioningFromValue(call.Arguments[i]);
                }
                break;

            case IrReturn ret:
                if (ret.Value != null)
                    ret.Value = RemoveSsaVersioningFromValue(ret.Value);
                break;

            case IrConditionalBranch condBranch:
                condBranch.Condition = RemoveSsaVersioningFromValue(condBranch.Condition);
                break;

            case IrIndexAccess indexAccess:
                indexAccess.ResultName = RemoveVersion(indexAccess.ResultName);
                indexAccess.Array = RemoveSsaVersioningFromValue(indexAccess.Array);
                indexAccess.Index = RemoveSsaVersioningFromValue(indexAccess.Index);
                break;

            case IrMemberAccess memberAccess:
                memberAccess.ResultName = RemoveVersion(memberAccess.ResultName);
                memberAccess.Struct = RemoveSsaVersioningFromValue(memberAccess.Struct);
                break;

            case IrMemberStore memberStore:
                memberStore.Struct = RemoveSsaVersioningFromValue(memberStore.Struct);
                memberStore.Value = RemoveSsaVersioningFromValue(memberStore.Value);
                break;

            case IrIndexStore indexStore:
                indexStore.Array = RemoveSsaVersioningFromValue(indexStore.Array);
                indexStore.Index = RemoveSsaVersioningFromValue(indexStore.Index);
                indexStore.Value = RemoveSsaVersioningFromValue(indexStore.Value);
                break;

            case IrDereferenceStore derefStore:
                derefStore.Pointer = RemoveSsaVersioningFromValue(derefStore.Pointer);
                derefStore.Value = RemoveSsaVersioningFromValue(derefStore.Value);
                break;

            case IrAssert assert:
                assert.Condition = RemoveSsaVersioningFromValue(assert.Condition);
                break;

            case IrExtractTag extractTag:
                extractTag.ResultName = RemoveVersion(extractTag.ResultName);
                extractTag.EnumValue = RemoveSsaVersioningFromValue(extractTag.EnumValue);
                break;

            case IrExtractVariantData extractData:
                extractData.ResultName = RemoveVersion(extractData.ResultName);
                extractData.EnumValue = RemoveSsaVersioningFromValue(extractData.EnumValue);
                break;

            case IrIndirectCall indirectCall:
                indirectCall.FunctionPointer = RemoveSsaVersioningFromValue(indirectCall.FunctionPointer);
                for (int i = 0; i < indirectCall.Arguments.Count; i++)
                {
                    indirectCall.Arguments[i] = RemoveSsaVersioningFromValue(indirectCall.Arguments[i]);
                }
                break;
        }

        return instruction;
    }

    /// <summary>
    /// Remove SSA versioning from a value
    /// </summary>
    private IrValue RemoveSsaVersioningFromValue(IrValue value)
    {
        if (value is IrVariable variable)
        {
            // Remove version from name (handles both SSA form variables and string-based versions)
            var cleanName = RemoveVersion(variable.Name);
            if (cleanName != variable.Name || variable.IsInSsaForm)
            {
                // Create new variable without SSA version
                return new IrVariable(cleanName, variable.Type);
            }
        }
        return value;
    }

    /// <summary>
    /// Remove version suffix from variable name (e.g., "x_0" -> "x")
    /// </summary>
    private string RemoveVersion(string varName)
    {
        // SSA variables are named like "x_0", "x_1", etc.
        // Remove the "_N" suffix
        var lastUnderscore = varName.LastIndexOf('_');
        if (lastUnderscore > 0 && lastUnderscore < varName.Length - 1)
        {
            var suffix = varName.Substring(lastUnderscore + 1);
            if (int.TryParse(suffix, out _))
            {
                return varName.Substring(0, lastUnderscore);
            }
        }
        return varName;
    }
}
