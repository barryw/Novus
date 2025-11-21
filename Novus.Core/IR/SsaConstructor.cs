namespace Novus.IR;

/// <summary>
/// Converts IR from non-SSA form to SSA form
/// Implements the classic SSA construction algorithm:
/// 1. Build CFG and compute dominance frontiers
/// 2. Insert phi functions at dominance frontiers
/// 3. Rename variables with version numbers
/// </summary>
public class SsaConstructor
{
    private readonly IrFunction _function;
    private ControlFlowGraph? _cfg;
    private DominatorTree? _domTree;

    /// <summary>
    /// Maps variable names to the blocks where they are defined
    /// Used to determine where phi functions are needed
    /// </summary>
    private Dictionary<string, HashSet<IrBasicBlock>> _defSites = new();

    /// <summary>
    /// Current version counter for each variable
    /// </summary>
    private Dictionary<string, int> _versionCounters = new();

    /// <summary>
    /// Stack of current versions for each variable (for renaming pass)
    /// </summary>
    private Dictionary<string, Stack<int>> _versionStacks = new();

    public SsaConstructor(IrFunction function)
    {
        _function = function;
    }

    /// <summary>
    /// Convert the function to SSA form
    /// </summary>
    public void ConstructSsa()
    {
        // Step 1: Build CFG and dominator tree
        _cfg = new ControlFlowGraph(_function);
        _domTree = _cfg.BuildDominatorTree();

        // Step 2: Find all variable definitions
        FindDefinitionSites();

        // Step 3: Insert phi functions at dominance frontiers
        InsertPhiFunctions();

        // Step 4: Rename variables with version numbers
        RenameVariables();
    }

    /// <summary>
    /// Find all blocks where each variable is defined
    /// </summary>
    private void FindDefinitionSites()
    {
        _defSites.Clear();

        foreach (var block in _function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                var definedVars = GetDefinedVariables(instruction);
                foreach (var varName in definedVars)
                {
                    if (!_defSites.ContainsKey(varName))
                    {
                        _defSites[varName] = new HashSet<IrBasicBlock>();
                    }
                    _defSites[varName].Add(block);
                }
            }
        }
    }

    /// <summary>
    /// Get the list of variables defined by an instruction
    /// </summary>
    private List<string> GetDefinedVariables(IrInstruction instruction)
    {
        var defined = new List<string>();

        switch (instruction)
        {
            case IrLocalDecl decl:
                defined.Add(decl.Name);
                break;
            case IrStore store:
                defined.Add(store.VariableName);
                break;
            case IrBinaryOp binOp:
                defined.Add(binOp.ResultName);
                break;
            case IrCall call when call.ResultName != null:
                defined.Add(call.ResultName);
                break;
            case IrIndexAccess indexAccess:
                defined.Add(indexAccess.ResultName);
                break;
            case IrMemberAccess memberAccess:
                defined.Add(memberAccess.ResultName);
                break;
            case IrExtractTag extractTag:
                defined.Add(extractTag.ResultName);
                break;
            case IrExtractVariantData extractData:
                defined.Add(extractData.ResultName);
                break;
        }

        return defined;
    }

    /// <summary>
    /// Insert phi functions at dominance frontiers of definition sites
    /// </summary>
    private void InsertPhiFunctions()
    {
        if (_cfg == null || _domTree == null)
            throw new InvalidOperationException("CFG and dominator tree must be built first");

        // Create mapping from IrBasicBlock to CFGNode
        var blockToNode = new Dictionary<IrBasicBlock, CFGNode>();
        foreach (var node in _cfg.Nodes)
        {
            if (node.Block != null)
            {
                blockToNode[node.Block] = node;
            }
        }

        foreach (var (varName, defBlocks) in _defSites)
        {
            // Worklist of blocks where we need to insert phi functions
            var worklist = new Queue<IrBasicBlock>(defBlocks);
            var processedBlocks = new HashSet<IrBasicBlock>();
            var phiInserted = new HashSet<IrBasicBlock>();

            while (worklist.Count > 0)
            {
                var block = worklist.Dequeue();
                if (!processedBlocks.Add(block))
                    continue;

                if (!blockToNode.TryGetValue(block, out var node))
                    continue;

                // Get dominance frontier of this block
                var frontier = _domTree.GetDominanceFrontier(node, _cfg);

                foreach (var frontierNode in frontier)
                {
                    var frontierBlock = frontierNode.Block;

                    // Only insert phi if we haven't already
                    if (!phiInserted.Contains(frontierBlock))
                    {
                        // Create a temporary variable for the phi result
                        // We'll set the proper version during renaming
                        var phiResult = new IrVariable(varName, -1, GetVariableType(varName));
                        var phi = new IrPhi(phiResult);

                        frontierBlock.AddPhi(phi);
                        phiInserted.Add(frontierBlock);

                        // If this block didn't already define the variable, add it to worklist
                        if (!defBlocks.Contains(frontierBlock))
                        {
                            worklist.Enqueue(frontierBlock);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get the type of a variable by looking at its first definition or use
    /// This is a simplified approach - a real implementation would need type tracking
    /// </summary>
    private IrType GetVariableType(string varName)
    {
        // Search for first definition or use of this variable
        foreach (var block in _function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                // Check local declarations
                if (instruction is IrLocalDecl decl && decl.Name == varName)
                {
                    return decl.Type;
                }

                // Check stores
                if (instruction is IrStore store && store.VariableName == varName)
                {
                    return store.Value.Type;
                }

                // Check binary operations
                if (instruction is IrBinaryOp binOp && binOp.ResultName == varName)
                {
                    return binOp.Type;
                }
            }
        }

        // Default to i32 if we can't determine type
        // This shouldn't happen in well-formed IR
        return IrIntType.I32;
    }

    /// <summary>
    /// Rename variables with SSA versions
    /// Uses a depth-first traversal of the dominator tree
    /// </summary>
    private void RenameVariables()
    {
        if (_cfg == null || _domTree == null)
            throw new InvalidOperationException("CFG and dominator tree must be built first");

        // Initialize version counters and stacks
        _versionCounters.Clear();
        _versionStacks.Clear();

        // Initialize for all variables in def sites
        foreach (var varName in _defSites.Keys)
        {
            _versionCounters[varName] = 0;
            _versionStacks[varName] = new Stack<int>();
        }

        // Also initialize for variables in phi functions
        foreach (var block in _function.BasicBlocks)
        {
            foreach (var phi in block.PhiFunctions)
            {
                var varName = phi.Destination.Name;
                if (!_versionCounters.ContainsKey(varName))
                {
                    _versionCounters[varName] = 0;
                    _versionStacks[varName] = new Stack<int>();
                }
            }
        }

        // Start renaming from the entry block
        var entryBlock = _function.BasicBlocks.FirstOrDefault();
        if (entryBlock != null)
        {
            RenameBlock(entryBlock);
        }
    }

    /// <summary>
    /// Rename variables in a block and its dominated children
    /// </summary>
    private void RenameBlock(IrBasicBlock block)
    {
        if (_cfg == null || _domTree == null)
            return;

        // Find the CFGNode for this block
        var node = _cfg.Nodes.FirstOrDefault(n => n.Block == block);
        if (node == null)
            return;

        // Track which variables we pushed versions for (for cleanup)
        var pushedVars = new List<string>();

        // First, rename phi function destinations
        foreach (var phi in block.PhiFunctions)
        {
            if (phi?.Destination == null)
                continue;

            var varName = phi.Destination.Name;
            var newVersion = GetNewVersion(varName);
            phi.Destination = new IrVariable(varName, newVersion, phi.Destination.Type);
            pushedVars.Add(varName);
        }

        // Rename uses and definitions in instructions
        foreach (var instruction in block.Instructions)
        {
            if (instruction == null)
                continue;

            // Rename uses first (before definitions)
            RenameUses(instruction);

            // Then rename definitions
            var definedVars = GetDefinedVariables(instruction);
            foreach (var varName in definedVars)
            {
                var newVersion = GetNewVersion(varName);
                RenameDefinition(instruction, varName, newVersion);
                pushedVars.Add(varName);
            }
        }

        // Fill in phi function arguments for successor blocks
        foreach (var successor in node.Successors)
        {
            var successorBlock = successor.Destination.Block;
            if (successorBlock == null)
                continue;

            foreach (var phi in successorBlock.PhiFunctions)
            {
                if (phi?.Destination == null)
                    continue;

                var varName = phi.Destination.Name;
                if (_versionStacks.ContainsKey(varName) && _versionStacks[varName].Count > 0)
                {
                    var currentVersion = _versionStacks[varName].Peek();
                    var value = new IrVariable(varName, currentVersion, phi.Destination.Type);
                    phi.AddIncoming(value, block);
                }
            }
        }

        // Recursively rename dominated children in the dominator tree
        if (_domTree.Children.TryGetValue(node, out var children))
        {
            foreach (var child in children)
            {
                // Only rename if the child has an actual block (not virtual node)
                if (child.Block != null)
                {
                    RenameBlock(child.Block);
                }
            }
        }

        // Pop versions we pushed (restore state for siblings in dominator tree)
        foreach (var varName in pushedVars)
        {
            if (_versionStacks.ContainsKey(varName) && _versionStacks[varName].Count > 0)
            {
                _versionStacks[varName].Pop();
            }
        }
    }

    /// <summary>
    /// Get a new version number for a variable and push it on the stack
    /// </summary>
    private int GetNewVersion(string varName)
    {
        if (!_versionCounters.ContainsKey(varName))
        {
            _versionCounters[varName] = 0;
            _versionStacks[varName] = new Stack<int>();
        }

        var version = _versionCounters[varName]++;
        _versionStacks[varName].Push(version);
        return version;
    }

    /// <summary>
    /// Rename all uses of variables in an instruction to their current SSA versions
    /// </summary>
    private void RenameUses(IrInstruction instruction)
    {
        switch (instruction)
        {
            case IrBinaryOp binOp:
                binOp.Left = RenameValue(binOp.Left);
                binOp.Right = RenameValue(binOp.Right);
                break;
            case IrStore store:
                store.Value = RenameValue(store.Value);
                break;
            case IrReturn ret:
                if (ret.Value != null)
                    ret.Value = RenameValue(ret.Value);
                break;
            case IrConditionalBranch condBranch:
                condBranch.Condition = RenameValue(condBranch.Condition);
                break;
            case IrCall call:
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    call.Arguments[i] = RenameValue(call.Arguments[i]);
                }
                break;
            case IrLocalDecl decl:
                decl.InitialValue = RenameValue(decl.InitialValue);
                break;
            case IrIndexAccess indexAccess:
                indexAccess.Array = RenameValue(indexAccess.Array);
                indexAccess.Index = RenameValue(indexAccess.Index);
                break;
            case IrMemberAccess memberAccess:
                memberAccess.Struct = RenameValue(memberAccess.Struct);
                break;
        }
    }

    /// <summary>
    /// Rename a value to its current SSA version if it's a variable
    /// </summary>
    private IrValue RenameValue(IrValue value)
    {
        if (value is IrVariable variable)
        {
            var varName = variable.Name;
            // Initialize if not seen before (for variables not in defSites, like parameters or uses before def)
            if (!_versionStacks.ContainsKey(varName))
            {
                _versionCounters[varName] = 0;
                _versionStacks[varName] = new Stack<int>();
            }

            if (_versionStacks[varName].Count > 0)
            {
                var currentVersion = _versionStacks[varName].Peek();
                return new IrVariable(varName, currentVersion, variable.Type);
            }
        }
        return value;
    }

    /// <summary>
    /// Rename the definition in an instruction to use the new SSA version
    /// </summary>
    private void RenameDefinition(IrInstruction instruction, string varName, int newVersion)
    {
        switch (instruction)
        {
            case IrLocalDecl decl when decl.Name == varName:
                // Local declarations become versioned
                decl.Name = $"{varName}_{newVersion}";
                break;
            case IrStore store when store.VariableName == varName:
                // Stores update the variable name
                store.VariableName = $"{varName}_{newVersion}";
                break;
            case IrBinaryOp binOp when binOp.ResultName == varName:
                binOp.ResultName = $"{varName}_{newVersion}";
                break;
            case IrCall call when call.ResultName == varName:
                call.ResultName = $"{varName}_{newVersion}";
                break;
            case IrIndexAccess indexAccess when indexAccess.ResultName == varName:
                indexAccess.ResultName = $"{varName}_{newVersion}";
                break;
            case IrMemberAccess memberAccess when memberAccess.ResultName == varName:
                memberAccess.ResultName = $"{varName}_{newVersion}";
                break;
        }
    }
}
