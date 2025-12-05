using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Instrumentation pass for profile-guided optimization (PGO).
/// Inserts counters at function entries, branch points, and loop headers.
/// The instrumented code writes profile data that can be collected and used
/// for subsequent compilation with --pgo-use.
/// </summary>
public class InstrumentationPass : IOptimizationPass
{
    public string Name => "PGO Instrumentation";

    /// <summary>
    /// Collected instrumentation points for generating profile runtime
    /// </summary>
    public InstrumentationData InstrumentationData { get; } = new();

    /// <summary>
    /// Whether to instrument function entry/exit (for call count profiling)
    /// </summary>
    public bool InstrumentFunctions { get; set; } = true;

    /// <summary>
    /// Whether to instrument branches (for branch probability profiling)
    /// </summary>
    public bool InstrumentBranches { get; set; } = true;

    /// <summary>
    /// Whether to instrument loops (for iteration count profiling)
    /// </summary>
    public bool InstrumentLoops { get; set; } = true;

    public bool Run(IrModule module)
    {
        bool changed = false;

        // Clear previous instrumentation data
        InstrumentationData.Clear();

        foreach (var function in module.Functions)
        {
            // Skip extern functions (no body to instrument)
            if (function.IsExtern)
                continue;

            // Skip the profile runtime functions themselves
            if (IsProfileRuntimeFunction(function.Name))
                continue;

            if (InstrumentFunctions)
            {
                changed |= InstrumentFunctionEntry(function);
            }

            if (InstrumentBranches)
            {
                changed |= InstrumentBranchPoints(function);
            }

            if (InstrumentLoops)
            {
                changed |= InstrumentLoopHeaders(function);
            }
        }

        return changed;
    }

    private bool IsProfileRuntimeFunction(string name)
    {
        // Profile runtime functions that shouldn't be instrumented
        return name.StartsWith("__pgo_") ||
               name.StartsWith("_pgo_") ||
               name == "__novus_profile_init" ||
               name == "__novus_profile_write" ||
               name == "__novus_profile_func_entry" ||
               name == "__novus_profile_branch" ||
               name == "__novus_profile_loop_entry" ||
               name == "__novus_profile_loop_iter";
    }

    /// <summary>
    /// Insert function entry instrumentation
    /// </summary>
    private bool InstrumentFunctionEntry(IrFunction function)
    {
        if (function.BasicBlocks.Count == 0)
            return false;

        var entryBlock = function.BasicBlocks[0];
        if (entryBlock.Instructions.Count == 0)
            return false;

        // Create a unique counter ID for this function
        var counterId = InstrumentationData.AddFunctionCounter(function.Name);

        // Insert call to __novus_profile_func_entry(counter_id)
        var instrCall = new IrCall("__novus_profile_func_entry", IrVoidType.Instance)
        {
            Arguments = { new IrConstant(counterId, IrIntType.I32) }
        };

        // Insert at the beginning of the entry block, after any local decls
        var insertIndex = 0;
        while (insertIndex < entryBlock.Instructions.Count &&
               entryBlock.Instructions[insertIndex] is IrLocalDecl)
        {
            insertIndex++;
        }

        entryBlock.Instructions.Insert(insertIndex, instrCall);
        return true;
    }

    /// <summary>
    /// Insert branch instrumentation at conditional branches
    /// </summary>
    private bool InstrumentBranchPoints(IrFunction function)
    {
        bool changed = false;

        foreach (var block in function.BasicBlocks)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                if (block.Instructions[i] is IrConditionalBranch condBranch)
                {
                    // Create a unique counter ID for this branch
                    var branchId = $"{function.Name}:{block.Label}";
                    var counterId = InstrumentationData.AddBranchCounter(branchId);

                    // Insert call to __novus_profile_branch(counter_id, condition)
                    // This tracks whether the branch is taken (condition true) or not (condition false)
                    var instrCall = new IrCall("__novus_profile_branch", IrVoidType.Instance)
                    {
                        Arguments =
                        {
                            new IrConstant(counterId, IrIntType.I32),
                            condBranch.Condition
                        }
                    };

                    block.Instructions.Insert(i, instrCall);
                    i++; // Skip past the inserted instruction
                    changed = true;
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Insert loop instrumentation at loop headers
    /// </summary>
    private bool InstrumentLoopHeaders(IrFunction function)
    {
        bool changed = false;

        // Detect loops by finding back edges (branches to earlier blocks)
        var blockOrder = new Dictionary<string, int>();
        for (int i = 0; i < function.BasicBlocks.Count; i++)
        {
            blockOrder[function.BasicBlocks[i].Label] = i;
        }

        // Track which blocks are loop headers (targets of back edges)
        var loopHeaders = new HashSet<string>();
        var loopBackEdges = new Dictionary<string, List<string>>(); // header -> list of back edge sources

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                var targets = GetBranchTargets(instruction);
                foreach (var target in targets)
                {
                    if (blockOrder.TryGetValue(target, out var targetOrder) &&
                        blockOrder.TryGetValue(block.Label, out var sourceOrder) &&
                        targetOrder <= sourceOrder)
                    {
                        // This is a back edge - target is a loop header
                        loopHeaders.Add(target);
                        if (!loopBackEdges.ContainsKey(target))
                        {
                            loopBackEdges[target] = new List<string>();
                        }
                        loopBackEdges[target].Add(block.Label);
                    }
                }
            }
        }

        // Instrument each loop header
        foreach (var block in function.BasicBlocks)
        {
            if (!loopHeaders.Contains(block.Label))
                continue;

            var loopId = $"{function.Name}:{block.Label}";
            var counterId = InstrumentationData.AddLoopCounter(loopId);

            // Insert call to __novus_profile_loop_iter(counter_id) at loop header
            // This counts each loop iteration
            var instrCall = new IrCall("__novus_profile_loop_iter", IrVoidType.Instance)
            {
                Arguments = { new IrConstant(counterId, IrIntType.I32) }
            };

            // Insert at the beginning of the block, after any phi nodes
            var insertIndex = 0;
            while (insertIndex < block.Instructions.Count &&
                   block.Instructions[insertIndex] is IrPhi)
            {
                insertIndex++;
            }

            block.Instructions.Insert(insertIndex, instrCall);
            changed = true;
        }

        return changed;
    }

    private List<string> GetBranchTargets(IrInstruction instruction)
    {
        return instruction switch
        {
            IrBranch branch => new List<string> { branch.Target },
            IrConditionalBranch condBranch => new List<string> { condBranch.TrueTarget, condBranch.FalseTarget },
            _ => new List<string>()
        };
    }
}

/// <summary>
/// Collected instrumentation data for generating the profile runtime data section
/// </summary>
public class InstrumentationData
{
    private int _nextCounterId = 0;

    /// <summary>
    /// Function counter IDs: function name -> counter ID
    /// </summary>
    public Dictionary<string, int> FunctionCounters { get; } = new();

    /// <summary>
    /// Branch counter IDs: branch ID (function:block) -> counter ID
    /// </summary>
    public Dictionary<string, int> BranchCounters { get; } = new();

    /// <summary>
    /// Loop counter IDs: loop ID (function:block) -> counter ID
    /// </summary>
    public Dictionary<string, int> LoopCounters { get; } = new();

    /// <summary>
    /// Total number of counters (size needed for counter array)
    /// </summary>
    public int TotalCounters => _nextCounterId;

    public void Clear()
    {
        _nextCounterId = 0;
        FunctionCounters.Clear();
        BranchCounters.Clear();
        LoopCounters.Clear();
    }

    public int AddFunctionCounter(string functionName)
    {
        var id = _nextCounterId++;
        FunctionCounters[functionName] = id;
        return id;
    }

    public int AddBranchCounter(string branchId)
    {
        var id = _nextCounterId++;
        BranchCounters[branchId] = id;
        return id;
    }

    public int AddLoopCounter(string loopId)
    {
        var id = _nextCounterId++;
        LoopCounters[loopId] = id;
        return id;
    }

    /// <summary>
    /// Generate metadata that can be embedded in the executable
    /// This allows the profile runtime to know what each counter means
    /// </summary>
    public InstrumentationMetadata GenerateMetadata()
    {
        return new InstrumentationMetadata
        {
            TotalCounters = TotalCounters,
            FunctionCounters = new Dictionary<string, int>(FunctionCounters),
            BranchCounters = new Dictionary<string, int>(BranchCounters),
            LoopCounters = new Dictionary<string, int>(LoopCounters)
        };
    }
}

/// <summary>
/// Metadata about instrumentation points, serialized with the executable
/// </summary>
public class InstrumentationMetadata
{
    public int TotalCounters { get; set; }
    public Dictionary<string, int> FunctionCounters { get; set; } = new();
    public Dictionary<string, int> BranchCounters { get; set; } = new();
    public Dictionary<string, int> LoopCounters { get; set; } = new();
}
