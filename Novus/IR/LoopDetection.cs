namespace Novus.IR;

/// <summary>
/// Represents a natural loop in the CFG.
/// A natural loop has:
/// - A single entry point (header)
/// - At least one back edge to the header
/// - All nodes in the loop can reach the back edge without leaving the loop
/// </summary>
public class Loop
{
    /// <summary>
    /// The loop header - dominates all nodes in the loop
    /// </summary>
    public CFGNode Header { get; }

    /// <summary>
    /// All nodes that are part of this loop (including header)
    /// </summary>
    public HashSet<CFGNode> Body { get; }

    /// <summary>
    /// Back edges that define this loop (tail → header)
    /// </summary>
    public List<CFGEdge> BackEdges { get; }

    /// <summary>
    /// Nested loops contained within this loop
    /// </summary>
    public List<Loop> NestedLoops { get; } = new();

    /// <summary>
    /// Parent loop if this is nested, null if outermost
    /// </summary>
    public Loop? ParentLoop { get; set; }

    public Loop(CFGNode header)
    {
        Header = header;
        Body = new HashSet<CFGNode> { header };
        BackEdges = new List<CFGEdge>();
    }

    /// <summary>
    /// Check if a node is part of this loop
    /// </summary>
    public bool Contains(CFGNode node) => Body.Contains(node);

    /// <summary>
    /// Get the loop depth (0 for outermost, 1 for first nesting level, etc.)
    /// </summary>
    public int GetDepth()
    {
        int depth = 0;
        var parent = ParentLoop;
        while (parent != null)
        {
            depth++;
            parent = parent.ParentLoop;
        }
        return depth;
    }

    public override string ToString() => $"Loop[header={Header.Label}, body={Body.Count} nodes, depth={GetDepth()}]";
}

/// <summary>
/// Loop detection using natural loop algorithm.
/// Finds all loops in a CFG based on back edges in the dominator tree.
/// </summary>
public class LoopDetector
{
    private readonly ControlFlowGraph _cfg;
    private readonly DominatorTree _domTree;

    public LoopDetector(ControlFlowGraph cfg)
    {
        _cfg = cfg;
        _domTree = cfg.BuildDominatorTree();
    }

    /// <summary>
    /// Detect all natural loops in the CFG.
    /// Returns loops in innermost-first order (useful for optimization).
    /// </summary>
    public List<Loop> DetectLoops()
    {
        var loops = new List<Loop>();

        // Find all back edges using dominator-based definition:
        // An edge n → h is a back edge if h dominates n
        var backEdges = FindNaturalBackEdges();

        // Group back edges by header
        var edgesByHeader = new Dictionary<CFGNode, List<CFGEdge>>();
        foreach (var edge in backEdges)
        {
            var header = edge.Destination;
            if (!edgesByHeader.ContainsKey(header))
            {
                edgesByHeader[header] = new List<CFGEdge>();
            }
            edgesByHeader[header].Add(edge);
        }

        // For each loop header, compute the natural loop
        foreach (var (header, edges) in edgesByHeader)
        {
            var loop = ComputeNaturalLoop(header, edges);
            loops.Add(loop);
        }

        // Establish nesting relationships
        EstablishNesting(loops);

        // Sort loops by depth (innermost first) - better for optimizations
        loops.Sort((a, b) => b.GetDepth().CompareTo(a.GetDepth()));

        return loops;
    }

    /// <summary>
    /// Find back edges using the dominator-based definition.
    /// An edge n → h is a back edge if h dominates n.
    /// This is more precise than DFS-based back edge detection.
    /// </summary>
    private List<CFGEdge> FindNaturalBackEdges()
    {
        var backEdges = new List<CFGEdge>();

        foreach (var node in _cfg.Nodes)
        {
            foreach (var edge in node.Successors)
            {
                // Back edge: destination dominates source
                if (_domTree.Dominates(edge.Destination, node))
                {
                    backEdges.Add(edge);
                }
            }
        }

        return backEdges;
    }

    /// <summary>
    /// Compute the natural loop for a given header and its back edges.
    /// The natural loop consists of:
    /// - The header
    /// - All nodes that can reach any back edge tail without going through the header
    /// </summary>
    private Loop ComputeNaturalLoop(CFGNode header, List<CFGEdge> backEdges)
    {
        var loop = new Loop(header);
        loop.BackEdges.AddRange(backEdges);

        // Start with the tails of all back edges
        var worklist = new Queue<CFGNode>();
        foreach (var edge in backEdges)
        {
            var tail = edge.Source;
            if (tail != header && !loop.Body.Contains(tail))
            {
                loop.Body.Add(tail);
                worklist.Enqueue(tail);
            }
        }

        // Work backwards from tails, adding predecessors until we reach header
        while (worklist.Count > 0)
        {
            var node = worklist.Dequeue();

            foreach (var pred in node.Predecessors)
            {
                // Don't go past the header (header dominates the loop)
                if (pred != header && !loop.Body.Contains(pred))
                {
                    loop.Body.Add(pred);
                    worklist.Enqueue(pred);
                }
            }
        }

        return loop;
    }

    /// <summary>
    /// Establish parent-child relationships between nested loops.
    /// A loop A is nested in loop B if:
    /// - A's header is in B's body
    /// - A is not equal to B
    /// </summary>
    private void EstablishNesting(List<Loop> loops)
    {
        foreach (var innerLoop in loops)
        {
            Loop? directParent = null;

            // Find the innermost loop that contains this loop
            foreach (var outerLoop in loops)
            {
                if (innerLoop == outerLoop) continue;

                // Check if outerLoop contains innerLoop's header
                if (outerLoop.Contains(innerLoop.Header))
                {
                    // Keep the innermost containing loop as parent
                    if (directParent == null || outerLoop.Body.Count < directParent.Body.Count)
                    {
                        directParent = outerLoop;
                    }
                }
            }

            if (directParent != null)
            {
                innerLoop.ParentLoop = directParent;
                directParent.NestedLoops.Add(innerLoop);
            }
        }
    }

    /// <summary>
    /// Find the loop containing a given node, or null if not in any loop
    /// </summary>
    public Loop? FindLoopContaining(CFGNode node, List<Loop> loops)
    {
        Loop? containingLoop = null;

        foreach (var loop in loops)
        {
            if (loop.Contains(node))
            {
                // Return the innermost loop containing this node
                if (containingLoop == null || loop.Body.Count < containingLoop.Body.Count)
                {
                    containingLoop = loop;
                }
            }
        }

        return containingLoop;
    }

    /// <summary>
    /// Check if an instruction is loop-invariant.
    /// An instruction is loop-invariant if:
    /// - All its operands are defined outside the loop, OR
    /// - All its operands are themselves loop-invariant
    /// </summary>
    public bool IsLoopInvariant(IrInstruction instruction, Loop loop)
    {
        // Get all variables used by this instruction
        var usedVars = GetUsedVariables(instruction);

        foreach (var varName in usedVars)
        {
            // Check if this variable is defined in the loop
            bool definedInLoop = false;

            foreach (var node in loop.Body)
            {
                if (node.Block == null) continue;

                foreach (var inst in node.Block.Instructions)
                {
                    var defVar = GetDefinedVariable(inst);
                    if (defVar == varName)
                    {
                        // Variable is defined in the loop
                        // Check if this definition is itself loop-invariant
                        if (!IsLoopInvariant(inst, loop))
                        {
                            definedInLoop = true;
                            break;
                        }
                    }
                }

                if (definedInLoop) break;
            }

            if (definedInLoop) return false;
        }

        // All operands are either from outside the loop or are loop-invariant
        return true;
    }

    /// <summary>
    /// Get all variables used by an instruction
    /// </summary>
    private HashSet<string> GetUsedVariables(IrInstruction instruction)
    {
        var vars = new HashSet<string>();

        switch (instruction)
        {
            case IrBinaryOp binOp:
                CollectVars(binOp.Left, vars);
                CollectVars(binOp.Right, vars);
                break;
            case IrReturn ret when ret.Value != null:
                CollectVars(ret.Value, vars);
                break;
            case IrStore store:
                CollectVars(store.Value, vars);
                break;
            case IrConditionalBranch condBr:
                CollectVars(condBr.Condition, vars);
                break;
            // Add other instruction types as needed
        }

        return vars;
    }

    private void CollectVars(IrValue value, HashSet<string> vars)
    {
        if (value is IrVariable var)
        {
            vars.Add(var.Name);
        }
    }

    /// <summary>
    /// Get the variable defined by an instruction, or null if none
    /// </summary>
    private string? GetDefinedVariable(IrInstruction instruction)
    {
        return instruction switch
        {
            IrBinaryOp binOp => binOp.ResultName,
            IrCall call when !string.IsNullOrEmpty(call.ResultName) => call.ResultName,
            _ => null
        };
    }
}
