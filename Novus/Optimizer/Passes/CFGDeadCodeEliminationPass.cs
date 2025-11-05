using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// CFG-based dead code elimination pass
/// Uses liveness analysis across basic blocks to identify and remove dead instructions
/// More powerful than basic DCE because it can eliminate dead code across block boundaries
///
/// REFACTORED: Now uses IrVisitor for collecting uses and defs instead of manual switch statements.
/// The visitor pattern automatically handles all instruction and value types.
/// </summary>
public class CFGDeadCodeEliminationPass : FunctionPassBase
{
    public override string Name => "CFG-based Dead Code Elimination";

    public override bool RunOnFunction(IrFunction function)
    {
        // Build control flow graph
        var cfg = new ControlFlowGraph(function);

        // Compute liveness information
        var liveness = ComputeLiveness(cfg);

        // Remove dead instructions
        bool changed = false;
        foreach (var node in cfg.Nodes)
        {
            if (node.Block == null) continue; // Skip entry/exit nodes

            var block = node.Block;
            var blockLiveness = liveness[node];

            // Track live variables as we process instructions backwards
            // Start with variables live at the end of the block
            var liveVars = new HashSet<string>(blockLiveness.LiveOut);

            // Process instructions backwards to maintain correct liveness
            for (int i = block.Instructions.Count - 1; i >= 0; i--)
            {
                var instruction = block.Instructions[i];

                // Check if this instruction defines a variable
                string? definedVar = GetDefinedVariable(instruction);
                if (definedVar != null)
                {
                    // Check if the variable is live after this instruction (instruction-level)
                    bool isLive = liveVars.Contains(definedVar);

                    // Special case: Comparison operations used directly for branching
                    // These set condition codes even if the result variable appears "dead"
                    bool isComparisonForBranch = IsComparisonUsedForBranching(instruction, block.Instructions, i);

                    // If the instruction has no side effects and the variable is dead, remove it
                    // But preserve comparisons used for branching (they set condition codes)
                    if (!isLive && !HasSideEffects(instruction) && !isComparisonForBranch)
                    {
                        block.Instructions.RemoveAt(i);
                        changed = true;
                    }
                    else
                    {
                        // Instruction kept - update liveness
                        // Remove the defined variable from live set (it's now dead before this instruction)
                        liveVars.Remove(definedVar);
                    }
                }

                // Add variables used by this instruction to the live set
                var collector = new UseCollectorVisitor();
                collector.VisitInstruction(instruction, null);
                liveVars.UnionWith(collector.UsedVariables);
            }
        }

        return changed;
    }

    /// <summary>
    /// Check if a comparison BinaryOp is immediately used by a ConditionalBranch
    /// </summary>
    private bool IsComparisonUsedForBranching(IrInstruction instruction, IList<IrInstruction> instructions, int index)
    {
        // Check if this is a comparison operation
        if (instruction is not IrBinaryOp binOp) return false;
        if (!IsComparisonOp(binOp.Operation)) return false;

        // Check if the next instruction (possibly after a Store) is a ConditionalBranch using this result
        int nextIndex = index + 1;

        // Skip optional Store instruction
        if (nextIndex < instructions.Count &&
            instructions[nextIndex] is IrStore store &&
            store.Value is IrVariable storeVar &&
            storeVar.Name == binOp.ResultName)
        {
            nextIndex++;
        }

        // Check if next instruction is a ConditionalBranch using this comparison
        if (nextIndex < instructions.Count &&
            instructions[nextIndex] is IrConditionalBranch condBranch &&
            condBranch.Condition is IrVariable condVar &&
            condVar.Name == binOp.ResultName)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a binary operation is a comparison
    /// </summary>
    private bool IsComparisonOp(IrBinaryOp.OpKind op)
    {
        return op switch
        {
            IrBinaryOp.OpKind.Eq => true,
            IrBinaryOp.OpKind.Ne => true,
            IrBinaryOp.OpKind.Lt => true,
            IrBinaryOp.OpKind.Le => true,
            IrBinaryOp.OpKind.Gt => true,
            IrBinaryOp.OpKind.Ge => true,
            _ => false
        };
    }

    /// <summary>
    /// Compute liveness information for all blocks using backward dataflow analysis
    /// </summary>
    private Dictionary<CFGNode, LivenessInfo> ComputeLiveness(ControlFlowGraph cfg)
    {
        var liveness = new Dictionary<CFGNode, LivenessInfo>();

        // Initialize liveness sets for all nodes
        foreach (var node in cfg.Nodes)
        {
            liveness[node] = new LivenessInfo();
        }

        // Iterative dataflow analysis until fixpoint
        bool changed = true;
        while (changed)
        {
            changed = false;

            // Process nodes in reverse postorder (backwards) for better convergence
            foreach (var node in cfg.Nodes)
            {
                if (node.Block == null) continue; // Skip entry/exit nodes

                var info = liveness[node];
                var oldLiveOut = new HashSet<string>(info.LiveOut);

                // LiveOut[n] = Union of LiveIn[s] for all successors s
                info.LiveOut.Clear();
                foreach (var edge in node.Successors)
                {
                    if (liveness.ContainsKey(edge.Destination))
                    {
                        info.LiveOut.UnionWith(liveness[edge.Destination].LiveIn);
                    }
                }

                // LiveIn[n] = Use[n] ∪ (LiveOut[n] - Def[n])
                var use = GetUses(node.Block);
                var def = GetDefs(node.Block);

                info.LiveIn.Clear();
                info.LiveIn.UnionWith(use);
                info.LiveIn.UnionWith(info.LiveOut.Except(def));

                // Check if anything changed
                if (!oldLiveOut.SetEquals(info.LiveOut))
                {
                    changed = true;
                }
            }
        }

        return liveness;
    }

    /// <summary>
    /// Get all variables used in a basic block using visitor pattern
    /// </summary>
    private HashSet<string> GetUses(IrBasicBlock block)
    {
        var collector = new UseCollectorVisitor();
        collector.VisitBasicBlock(block, null);
        return collector.UsedVariables;
    }

    /// <summary>
    /// Get all variables defined in a basic block
    /// </summary>
    private HashSet<string> GetDefs(IrBasicBlock block)
    {
        var defs = new HashSet<string>();
        foreach (var instruction in block.Instructions)
        {
            var def = GetDefinedVariable(instruction);
            if (def != null)
            {
                defs.Add(def);
            }
        }
        return defs;
    }

    /// <summary>
    /// Get the variable defined by an instruction, if any
    /// </summary>
    private string? GetDefinedVariable(IrInstruction instruction)
    {
        return instruction switch
        {
            IrBinaryOp binOp => binOp.ResultName,
            IrCall call when !string.IsNullOrEmpty(call.ResultName) => call.ResultName,
            IrIndexAccess indexAccess => indexAccess.ResultName,
            _ => null
        };
    }

    /// <summary>
    /// Check if an instruction has side effects (and thus cannot be eliminated)
    /// </summary>
    private bool HasSideEffects(IrInstruction instruction)
    {
        return instruction switch
        {
            IrReturn => true,
            IrStore => true,
            IrDereferenceStore => true,
            IrIndexStore => true,
            IrCall => true, // Calls may have side effects
            IrConditionalBranch => true,
            IrBranch => true,
            IrLocalDecl => true,
            _ => false
        };
    }

    /// <summary>
    /// Visitor that collects all used variable names
    /// REFACTORED: Uses IrVisitor instead of manual switch statements
    /// </summary>
    private class UseCollectorVisitor : IrVisitor<object?, object?>
    {
        public HashSet<string> UsedVariables { get; } = new();

        public override object? VisitVariable(IrVariable variable, object? context)
        {
            UsedVariables.Add(variable.Name);
            return null;
        }

        // Note: The base visitor automatically traverses all value types recursively,
        // so we only need to override VisitVariable to collect variable names.
        // IrDereferenceValue, IrBorrowValue, etc. are automatically handled.
    }
}

/// <summary>
/// Liveness information for a basic block
/// </summary>
internal class LivenessInfo
{
    /// <summary>
    /// Variables live at the entry of the block
    /// </summary>
    public HashSet<string> LiveIn { get; } = new();

    /// <summary>
    /// Variables live at the exit of the block
    /// </summary>
    public HashSet<string> LiveOut { get; } = new();
}
