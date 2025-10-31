namespace Novus.IR;

/// <summary>
/// Control Flow Graph (CFG) - represents control flow through a function as a directed graph.
/// Each node is a basic block, edges represent possible control flow.
/// </summary>
public class ControlFlowGraph
{
    public CFGNode EntryNode { get; }
    public CFGNode? ExitNode { get; private set; }
    public List<CFGNode> Nodes { get; } = new();
    public IrFunction Function { get; }

    private readonly Dictionary<string, CFGNode> _labelToNode = new();

    public ControlFlowGraph(IrFunction function)
    {
        Function = function;

        // Create entry node (virtual node before first basic block)
        EntryNode = new CFGNode(null, "ENTRY");
        Nodes.Add(EntryNode);

        Build();
    }

    /// <summary>
    /// Build the CFG from the function's basic blocks
    /// </summary>
    private void Build()
    {
        // Phase 1: Create nodes for all basic blocks
        foreach (var block in Function.BasicBlocks)
        {
            var node = new CFGNode(block, block.Label);
            Nodes.Add(node);
            _labelToNode[block.Label] = node;
        }

        // Create exit node upfront if any block has a return
        foreach (var block in Function.BasicBlocks)
        {
            if (block.Instructions.Count > 0 && block.Instructions[^1] is IrReturn)
            {
                if (ExitNode == null)
                {
                    ExitNode = new CFGNode(null, "EXIT");
                    Nodes.Add(ExitNode);
                }
                break;
            }
        }

        // Phase 2: Create edges based on control flow
        // Use ToList() to avoid modification during iteration
        foreach (var node in Nodes.ToList())
        {
            if (node.Block == null) continue; // Skip entry/exit nodes

            var block = node.Block;
            if (block.Instructions.Count == 0) continue;

            var lastInstruction = block.Instructions[^1];

            switch (lastInstruction)
            {
                case IrReturn:
                    // Return goes to exit
                    if (ExitNode != null)
                    {
                        AddEdge(node, ExitNode);
                    }
                    break;

                case IrBranch branch:
                    // Unconditional branch to target
                    if (_labelToNode.TryGetValue(branch.Target, out var target))
                    {
                        AddEdge(node, target);
                    }
                    break;

                case IrConditionalBranch condBranch:
                    // Conditional branch to true and false targets
                    if (_labelToNode.TryGetValue(condBranch.TrueTarget, out var trueTarget))
                    {
                        AddEdge(node, trueTarget, isConditional: true);
                    }
                    if (_labelToNode.TryGetValue(condBranch.FalseTarget, out var falseTarget))
                    {
                        AddEdge(node, falseTarget, isConditional: true);
                    }
                    break;

                default:
                    // No explicit terminator - this shouldn't happen in valid IR
                    // But we'll handle it gracefully
                    break;
            }
        }

        // Connect entry node to first basic block
        if (Function.BasicBlocks.Count > 0)
        {
            var firstBlock = Function.BasicBlocks[0];
            if (_labelToNode.TryGetValue(firstBlock.Label, out var firstNode))
            {
                AddEdge(EntryNode, firstNode);
            }
        }
    }

    /// <summary>
    /// Add an edge from source to destination
    /// </summary>
    private void AddEdge(CFGNode source, CFGNode dest, bool isConditional = false)
    {
        var edge = new CFGEdge(source, dest, isConditional);
        source.Successors.Add(edge);
        dest.Predecessors.Add(source);
    }

    /// <summary>
    /// Get a node by its label
    /// </summary>
    public CFGNode? GetNode(string label)
    {
        return _labelToNode.GetValueOrDefault(label);
    }

    /// <summary>
    /// Perform depth-first search traversal starting from entry
    /// </summary>
    public List<CFGNode> DepthFirstTraversal()
    {
        var visited = new HashSet<CFGNode>();
        var result = new List<CFGNode>();

        void DFS(CFGNode node)
        {
            if (visited.Contains(node)) return;
            visited.Add(node);
            result.Add(node);

            foreach (var edge in node.Successors)
            {
                DFS(edge.Destination);
            }
        }

        DFS(EntryNode);
        return result;
    }

    /// <summary>
    /// Get nodes in reverse post-order (useful for forward dataflow analysis)
    /// </summary>
    public List<CFGNode> ReversePostOrder()
    {
        var visited = new HashSet<CFGNode>();
        var postOrder = new List<CFGNode>();

        void PostOrderDFS(CFGNode node)
        {
            if (visited.Contains(node)) return;
            visited.Add(node);

            foreach (var edge in node.Successors)
            {
                PostOrderDFS(edge.Destination);
            }

            postOrder.Add(node);
        }

        PostOrderDFS(EntryNode);
        postOrder.Reverse();
        return postOrder;
    }

    /// <summary>
    /// Compute dominators for all nodes.
    /// A node A dominates node B if all paths from entry to B must go through A.
    /// </summary>
    public Dictionary<CFGNode, HashSet<CFGNode>> ComputeDominators()
    {
        var dominators = new Dictionary<CFGNode, HashSet<CFGNode>>();

        // Initialize: Entry dominates only itself, all others dominated by all nodes
        dominators[EntryNode] = new HashSet<CFGNode> { EntryNode };

        foreach (var node in Nodes)
        {
            if (node != EntryNode)
            {
                dominators[node] = new HashSet<CFGNode>(Nodes);
            }
        }

        // Iterative algorithm until fixed point
        bool changed = true;
        while (changed)
        {
            changed = false;

            foreach (var node in Nodes)
            {
                if (node == EntryNode) continue;

                // New dominators = {node} ∪ (∩ dominators of predecessors)
                HashSet<CFGNode>? newDoms = null;

                foreach (var pred in node.Predecessors)
                {
                    if (newDoms == null)
                    {
                        newDoms = new HashSet<CFGNode>(dominators[pred]);
                    }
                    else
                    {
                        newDoms.IntersectWith(dominators[pred]);
                    }
                }

                if (newDoms == null)
                    newDoms = new HashSet<CFGNode>();

                newDoms.Add(node);

                if (!newDoms.SetEquals(dominators[node]))
                {
                    dominators[node] = newDoms;
                    changed = true;
                }
            }
        }

        return dominators;
    }

    /// <summary>
    /// Compute the immediate dominator for each node.
    /// The immediate dominator of a node is the unique node that strictly dominates it
    /// and is dominated by all other strict dominators.
    /// </summary>
    public Dictionary<CFGNode, CFGNode?> ComputeImmediateDominators()
    {
        var dominators = ComputeDominators();
        var idom = new Dictionary<CFGNode, CFGNode?>();

        foreach (var node in Nodes)
        {
            if (node == EntryNode)
            {
                idom[node] = null; // Entry has no dominator
                continue;
            }

            // Strict dominators = all dominators except the node itself
            var strictDoms = new HashSet<CFGNode>(dominators[node]);
            strictDoms.Remove(node);

            if (strictDoms.Count == 0)
            {
                idom[node] = null;
                continue;
            }

            // Find the immediate dominator: the one that is dominated by all others
            CFGNode? immediateDominator = null;
            foreach (var candidate in strictDoms)
            {
                bool isDominatedByAll = true;
                foreach (var other in strictDoms)
                {
                    if (other != candidate && !dominators[candidate].Contains(other))
                    {
                        isDominatedByAll = false;
                        break;
                    }
                }

                if (isDominatedByAll)
                {
                    immediateDominator = candidate;
                    break;
                }
            }

            idom[node] = immediateDominator;
        }

        return idom;
    }

    /// <summary>
    /// Build the dominator tree from immediate dominators
    /// </summary>
    public DominatorTree BuildDominatorTree()
    {
        return new DominatorTree(this);
    }

    /// <summary>
    /// Find back edges (edges that point to ancestors in DFS tree).
    /// Back edges indicate loops.
    /// </summary>
    public List<CFGEdge> FindBackEdges()
    {
        var backEdges = new List<CFGEdge>();
        var visited = new HashSet<CFGNode>();
        var recursionStack = new HashSet<CFGNode>();

        void DFS(CFGNode node)
        {
            visited.Add(node);
            recursionStack.Add(node);

            foreach (var edge in node.Successors)
            {
                var dest = edge.Destination;

                if (!visited.Contains(dest))
                {
                    DFS(dest);
                }
                else if (recursionStack.Contains(dest))
                {
                    // Back edge found!
                    backEdges.Add(edge);
                }
            }

            recursionStack.Remove(node);
        }

        DFS(EntryNode);
        return backEdges;
    }

    /// <summary>
    /// Get all reachable nodes from entry
    /// </summary>
    public HashSet<CFGNode> GetReachableNodes()
    {
        var reachable = new HashSet<CFGNode>();

        void DFS(CFGNode node)
        {
            if (reachable.Contains(node)) return;
            reachable.Add(node);

            foreach (var edge in node.Successors)
            {
                DFS(edge.Destination);
            }
        }

        DFS(EntryNode);
        return reachable;
    }

    /// <summary>
    /// Check if all paths from entry reach the exit node (i.e., all paths end with a return).
    /// Returns true if the function is guaranteed to return on all code paths.
    /// </summary>
    public bool AllPathsReturn()
    {
        // If there's no exit node, no paths return
        if (ExitNode == null)
            return false;

        // Find all nodes reachable from entry
        var reachableFromEntry = new HashSet<CFGNode>();
        void MarkReachable(CFGNode node)
        {
            if (reachableFromEntry.Contains(node)) return;
            reachableFromEntry.Add(node);
            foreach (var edge in node.Successors)
            {
                MarkReachable(edge.Destination);
            }
        }
        MarkReachable(EntryNode);

        // Find all nodes that can reach exit (work backwards from exit)
        var canReachExit = new HashSet<CFGNode>();
        void MarkCanReachExit(CFGNode node)
        {
            if (canReachExit.Contains(node)) return;
            canReachExit.Add(node);
            foreach (var pred in node.Predecessors)
            {
                MarkCanReachExit(pred);
            }
        }
        MarkCanReachExit(ExitNode);

        // All paths return if every reachable node (except entry/exit) can reach exit
        foreach (var node in reachableFromEntry)
        {
            // Skip entry and exit nodes themselves
            if (node == EntryNode || node == ExitNode)
                continue;

            // If this node is reachable but can't reach exit, there's a path that doesn't return
            if (!canReachExit.Contains(node))
                return false;
        }

        return true;
    }
}

/// <summary>
/// A node in the control flow graph representing a basic block
/// </summary>
public class CFGNode
{
    public IrBasicBlock? Block { get; }
    public string Label { get; }
    public List<CFGEdge> Successors { get; } = new();
    public List<CFGNode> Predecessors { get; } = new();

    public CFGNode(IrBasicBlock? block, string label)
    {
        Block = block;
        Label = label;
    }

    public override string ToString() => Label;
}

/// <summary>
/// An edge in the control flow graph
/// </summary>
public class CFGEdge
{
    public CFGNode Source { get; }
    public CFGNode Destination { get; }
    public bool IsConditional { get; }

    public CFGEdge(CFGNode source, CFGNode destination, bool isConditional = false)
    {
        Source = source;
        Destination = destination;
        IsConditional = isConditional;
    }

    public override string ToString() => $"{Source.Label} -> {Destination.Label}";
}

/// <summary>
/// Dominator tree - represents dominance relationships
/// </summary>
public class DominatorTree
{
    public CFGNode Root { get; }
    public Dictionary<CFGNode, CFGNode?> ImmediateDominators { get; }
    public Dictionary<CFGNode, List<CFGNode>> Children { get; }

    public DominatorTree(ControlFlowGraph cfg)
    {
        Root = cfg.EntryNode;
        ImmediateDominators = cfg.ComputeImmediateDominators();
        Children = new Dictionary<CFGNode, List<CFGNode>>();

        // Build children mapping from idom
        foreach (var node in cfg.Nodes)
        {
            Children[node] = new List<CFGNode>();
        }

        foreach (var (node, idom) in ImmediateDominators)
        {
            if (idom != null)
            {
                Children[idom].Add(node);
            }
        }
    }

    /// <summary>
    /// Check if node A dominates node B
    /// </summary>
    public bool Dominates(CFGNode a, CFGNode b)
    {
        // A dominates B if A is in the dominator path from root to B
        var current = b;
        while (current != null)
        {
            if (current == a) return true;
            current = ImmediateDominators.GetValueOrDefault(current);
        }
        return false;
    }

    /// <summary>
    /// Find the dominance frontier of a node.
    /// The dominance frontier of a node X is the set of nodes Y such that:
    /// - X dominates a predecessor of Y
    /// - X does not strictly dominate Y
    /// </summary>
    public HashSet<CFGNode> GetDominanceFrontier(CFGNode node, ControlFlowGraph cfg)
    {
        var frontier = new HashSet<CFGNode>();

        foreach (var cfgNode in cfg.Nodes)
        {
            foreach (var pred in cfgNode.Predecessors)
            {
                // If node dominates pred but not cfgNode, then cfgNode is in frontier
                if (Dominates(node, pred) && !Dominates(node, cfgNode))
                {
                    frontier.Add(cfgNode);
                }
            }
        }

        return frontier;
    }
}
