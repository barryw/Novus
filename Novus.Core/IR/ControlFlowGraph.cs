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
    /// Compute variable scope information - determines which variables can be block-scoped
    /// vs which must be function-scoped.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - functionScopedVars: Variables that must be declared at function scope (used across blocks)
    /// - blockScopedVars: Dictionary mapping block label to variables that can be scoped to that block
    /// </returns>
    public (HashSet<string> functionScopedVars, Dictionary<string, HashSet<string>> blockScopedVars) ComputeVariableScopes()
    {
        // Track where each variable is defined (IrLocalDecl or first IrStore)
        var definedIn = new Dictionary<string, string>(); // varName -> block label
        // Track where each variable is used
        var usedIn = new Dictionary<string, HashSet<string>>(); // varName -> set of block labels

        foreach (var block in Function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                // Collect variable definitions
                if (instruction is IrLocalDecl localDecl)
                {
                    var varName = localDecl.Name;
                    if (!definedIn.ContainsKey(varName))
                    {
                        definedIn[varName] = block.Label;
                    }

                    // Also count uses in initial value
                    CollectUsedVariables(localDecl.InitialValue, block.Label, usedIn);
                }
                else if (instruction is IrStore store)
                {
                    var varName = store.VariableName;
                    if (!definedIn.ContainsKey(varName))
                    {
                        definedIn[varName] = block.Label;
                    }
                    // Store target is also a use of the destination
                    if (!usedIn.ContainsKey(varName))
                        usedIn[varName] = new HashSet<string>();
                    usedIn[varName].Add(block.Label);

                    // Collect uses in stored value
                    CollectUsedVariables(store.Value, block.Label, usedIn);
                }
                else
                {
                    // Collect all variable uses in this instruction
                    CollectUsedVariablesInInstruction(instruction, block.Label, usedIn);
                }
            }
        }

        var functionScopedVars = new HashSet<string>();
        var blockScopedVars = new Dictionary<string, HashSet<string>>();

        // Initialize block scopes
        foreach (var block in Function.BasicBlocks)
        {
            blockScopedVars[block.Label] = new HashSet<string>();
        }

        // Determine scope for each variable
        foreach (var (varName, defBlock) in definedIn)
        {
            // Get all blocks where this variable is used
            var useBlocks = usedIn.GetValueOrDefault(varName, new HashSet<string>());

            // A variable needs function scope if:
            // 1. It's used in multiple blocks, OR
            // 2. It's used in a block other than where it's defined
            if (useBlocks.Count > 1 || (useBlocks.Count == 1 && !useBlocks.Contains(defBlock)))
            {
                functionScopedVars.Add(varName);
            }
            else
            {
                // Can be block-scoped to its definition block
                blockScopedVars[defBlock].Add(varName);
            }
        }

        return (functionScopedVars, blockScopedVars);
    }

    /// <summary>
    /// Collect variable names used in an IrValue
    /// </summary>
    private void CollectUsedVariables(IrValue value, string blockLabel, Dictionary<string, HashSet<string>> usedIn)
    {
        switch (value)
        {
            case IrVariable variable:
                if (!usedIn.ContainsKey(variable.Name))
                    usedIn[variable.Name] = new HashSet<string>();
                usedIn[variable.Name].Add(blockLabel);
                break;

            case IrBorrowValue borrow:
                CollectUsedVariables(borrow.BorrowedValue, blockLabel, usedIn);
                break;

            case IrFieldReference fieldRef:
                CollectUsedVariables(fieldRef.Struct, blockLabel, usedIn);
                break;

            case IrIndexedFieldAccess indexedField:
                CollectUsedVariables(indexedField.Array, blockLabel, usedIn);
                CollectUsedVariables(indexedField.Index, blockLabel, usedIn);
                break;

            case IrDereferenceValue deref:
                CollectUsedVariables(deref.PointerValue, blockLabel, usedIn);
                break;

            case IrCastValue cast:
                CollectUsedVariables(cast.Value, blockLabel, usedIn);
                break;

            case IrTupleElementAccess tupleAccess:
                CollectUsedVariables(tupleAccess.Tuple, blockLabel, usedIn);
                break;
        }
    }

    /// <summary>
    /// Collect variable names used in an instruction
    /// </summary>
    private void CollectUsedVariablesInInstruction(IrInstruction instruction, string blockLabel, Dictionary<string, HashSet<string>> usedIn)
    {
        switch (instruction)
        {
            case IrCall call:
                foreach (var arg in call.Arguments)
                {
                    CollectUsedVariables(arg, blockLabel, usedIn);
                }
                break;

            case IrIndirectCall indirectCall:
                CollectUsedVariables(indirectCall.FunctionPointer, blockLabel, usedIn);
                foreach (var arg in indirectCall.Arguments)
                {
                    CollectUsedVariables(arg, blockLabel, usedIn);
                }
                break;

            case IrBinaryOp binOp:
                CollectUsedVariables(binOp.Left, blockLabel, usedIn);
                CollectUsedVariables(binOp.Right, blockLabel, usedIn);
                break;

            case IrReturn ret:
                if (ret.Value != null)
                    CollectUsedVariables(ret.Value, blockLabel, usedIn);
                break;

            case IrConditionalBranch condBranch:
                CollectUsedVariables(condBranch.Condition, blockLabel, usedIn);
                break;

            case IrMemberStore memberStore:
                CollectUsedVariables(memberStore.Struct, blockLabel, usedIn);
                CollectUsedVariables(memberStore.Value, blockLabel, usedIn);
                break;

            case IrIndexStore indexStore:
                CollectUsedVariables(indexStore.Array, blockLabel, usedIn);
                CollectUsedVariables(indexStore.Index, blockLabel, usedIn);
                CollectUsedVariables(indexStore.Value, blockLabel, usedIn);
                break;

            case IrIndexedFieldStore indexedFieldStore:
                CollectUsedVariables(indexedFieldStore.Array, blockLabel, usedIn);
                CollectUsedVariables(indexedFieldStore.Index, blockLabel, usedIn);
                CollectUsedVariables(indexedFieldStore.Value, blockLabel, usedIn);
                break;

            case IrDereferenceStore derefStore:
                CollectUsedVariables(derefStore.Pointer, blockLabel, usedIn);
                CollectUsedVariables(derefStore.Value, blockLabel, usedIn);
                break;

            case IrIndexAccess indexAccess:
                CollectUsedVariables(indexAccess.Array, blockLabel, usedIn);
                CollectUsedVariables(indexAccess.Index, blockLabel, usedIn);
                break;

            case IrMemberAccess memberAccess:
                CollectUsedVariables(memberAccess.Struct, blockLabel, usedIn);
                break;

            case IrAssert assert:
                CollectUsedVariables(assert.Condition, blockLabel, usedIn);
                break;

            case IrPanic panic:
                // panic has a message string, no variables typically
                break;
        }
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
