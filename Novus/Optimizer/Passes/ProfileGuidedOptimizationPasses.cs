using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Profile-guided branch prediction optimization.
/// Uses profile data to reorder branches so the likely path falls through.
/// On 68k, taken branches are more expensive than not-taken, so we want
/// the common case to be the fall-through path.
/// </summary>
public class ProfileGuidedBranchOptimizationPass : IOptimizationPass
{
    public string Name => "PGO Branch Optimization";

    /// <summary>
    /// The profile data to use for optimization decisions
    /// </summary>
    public ProfileData? Profile { get; set; }

    /// <summary>
    /// Minimum bias threshold to consider a branch strongly biased (default 70%)
    /// </summary>
    public double BiasThreshold { get; set; } = 0.70;

    public bool Run(IrModule module)
    {
        if (Profile == null)
            return false;

        bool changed = false;

        foreach (var function in module.Functions)
        {
            if (!Profile.Functions.TryGetValue(function.Name, out var funcProfile))
                continue;

            changed |= OptimizeFunctionBranches(function, funcProfile);
        }

        return changed;
    }

    private bool OptimizeFunctionBranches(IrFunction function, FunctionProfile funcProfile)
    {
        bool changed = false;

        foreach (var block in function.BasicBlocks)
        {
            var branchId = $"{function.Name}:{block.Label}";
            if (!funcProfile.Branches.TryGetValue(branchId, out var branchProfile))
                continue;

            // Skip branches that aren't strongly biased
            if (!branchProfile.IsStronglyBiased)
                continue;

            // Find the conditional branch in this block
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                if (block.Instructions[i] is IrConditionalBranch condBranch)
                {
                    // If true is unlikely (< 50%), swap the branches
                    // This puts the likely case as the fall-through
                    // Note: We don't actually swap here - the code generator should
                    // emit the branch with the "likely" hint based on probability.
                    // For 68k, we add metadata that the code gen can use.
                    if (branchProfile.TrueProbability < 0.5)
                    {
                        // Add hint that false branch is likely
                        condBranch.Metadata ??= new Dictionary<string, object>();
                        condBranch.Metadata["pgo_likely_false"] = true;
                        condBranch.Metadata["pgo_true_probability"] = branchProfile.TrueProbability;
                        changed = true;
                    }
                    else if (branchProfile.TrueProbability > 0.8)
                    {
                        // Add hint that true branch is likely
                        condBranch.Metadata ??= new Dictionary<string, object>();
                        condBranch.Metadata["pgo_likely_true"] = true;
                        condBranch.Metadata["pgo_true_probability"] = branchProfile.TrueProbability;
                        changed = true;
                    }
                    break;
                }
            }
        }

        return changed;
    }
}

/// <summary>
/// Profile-guided inlining optimization.
/// Uses profile data to decide which functions to inline based on call frequency.
/// Hot functions are more aggressively inlined, while cold functions are not.
/// </summary>
public class ProfileGuidedInliningPass : IOptimizationPass
{
    public string Name => "PGO Inlining";

    /// <summary>
    /// The profile data to use for optimization decisions
    /// </summary>
    public ProfileData? Profile { get; set; }

    /// <summary>
    /// Minimum call count to consider a function "hot" (default 100)
    /// </summary>
    public long HotThreshold { get; set; } = 100;

    /// <summary>
    /// Maximum instruction count for hot function inlining (default 100)
    /// Hot functions get a higher limit
    /// </summary>
    public int HotInlineLimit { get; set; } = 100;

    /// <summary>
    /// Maximum instruction count for cold function inlining (default 20)
    /// </summary>
    public int ColdInlineLimit { get; set; } = 20;

    public bool Run(IrModule module)
    {
        if (Profile == null)
            return false;

        bool changed = false;

        // Find the maximum call count for normalization
        long maxCallCount = Profile.Functions.Values
            .Select(f => f.CallCount)
            .DefaultIfEmpty(0)
            .Max();

        // Build a lookup of functions by name
        var functions = module.Functions.ToDictionary(f => f.Name);

        foreach (var function in module.Functions.ToList())
        {
            changed |= OptimizeFunctionCalls(function, functions, maxCallCount);
        }

        return changed;
    }

    private bool OptimizeFunctionCalls(
        IrFunction function,
        Dictionary<string, IrFunction> functions,
        long maxCallCount)
    {
        bool changed = false;

        foreach (var block in function.BasicBlocks)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                if (block.Instructions[i] is IrCall call)
                {
                    if (!functions.TryGetValue(call.FunctionName, out var callee))
                        continue;

                    // Don't inline extern functions
                    if (callee.IsExtern)
                        continue;

                    // Don't inline recursive calls
                    if (callee.Name == function.Name)
                        continue;

                    // Check if this call site is hot
                    bool isHot = false;
                    if (Profile!.Functions.TryGetValue(callee.Name, out var calleeProfile))
                    {
                        isHot = calleeProfile.CallCount >= HotThreshold;
                    }

                    // Apply different inlining thresholds based on hotness
                    int maxInstructions = isHot ? HotInlineLimit : ColdInlineLimit;
                    int instructionCount = CountInstructions(callee);

                    if (instructionCount <= maxInstructions)
                    {
                        // Mark the call for inlining (actual inlining done by InlineExpansionPass)
                        // We add metadata to guide the inliner
                        call.Metadata ??= new Dictionary<string, object>();
                        call.Metadata["pgo_inline"] = true;
                        call.Metadata["pgo_hot"] = isHot;
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    private int CountInstructions(IrFunction function)
    {
        return function.BasicBlocks.Sum(b => b.Instructions.Count);
    }
}

/// <summary>
/// Profile-guided loop optimization.
/// Uses profile data to decide on loop unrolling factors based on
/// observed iteration counts.
/// </summary>
public class ProfileGuidedLoopOptimizationPass : IOptimizationPass
{
    public string Name => "PGO Loop Optimization";

    /// <summary>
    /// The profile data to use for optimization decisions
    /// </summary>
    public ProfileData? Profile { get; set; }

    /// <summary>
    /// Maximum unroll factor (default 8)
    /// </summary>
    public int MaxUnrollFactor { get; set; } = 8;

    public bool Run(IrModule module)
    {
        if (Profile == null)
            return false;

        bool changed = false;

        foreach (var function in module.Functions)
        {
            if (!Profile.Functions.TryGetValue(function.Name, out var funcProfile))
                continue;

            changed |= OptimizeFunctionLoops(function, funcProfile);
        }

        return changed;
    }

    private bool OptimizeFunctionLoops(IrFunction function, FunctionProfile funcProfile)
    {
        bool changed = false;

        // Detect loop headers (same logic as InstrumentationPass)
        var blockOrder = new Dictionary<string, int>();
        for (int i = 0; i < function.BasicBlocks.Count; i++)
        {
            blockOrder[function.BasicBlocks[i].Label] = i;
        }

        foreach (var block in function.BasicBlocks)
        {
            var loopId = $"{function.Name}:{block.Label}";
            if (!funcProfile.Loops.TryGetValue(loopId, out var loopProfile))
                continue;

            // Get suggested unroll factor from profile
            int unrollFactor = loopProfile.SuggestedUnrollFactor;
            if (unrollFactor <= 0 || unrollFactor > MaxUnrollFactor)
                continue;

            // Add metadata to guide the loop unroller
            // Find the first instruction in the block and add metadata
            if (block.Instructions.Count > 0)
            {
                var firstInstr = block.Instructions[0];
                firstInstr.Metadata ??= new Dictionary<string, object>();
                firstInstr.Metadata["pgo_unroll_factor"] = unrollFactor;
                firstInstr.Metadata["pgo_avg_iterations"] = loopProfile.AverageIterations;
                changed = true;
            }
        }

        return changed;
    }
}

/// <summary>
/// Profile-guided code layout optimization.
/// Reorders basic blocks to improve instruction cache locality
/// by placing hot blocks together and cold blocks at the end.
/// </summary>
public class ProfileGuidedCodeLayoutPass : IOptimizationPass
{
    public string Name => "PGO Code Layout";

    /// <summary>
    /// The profile data to use for optimization decisions
    /// </summary>
    public ProfileData? Profile { get; set; }

    public bool Run(IrModule module)
    {
        if (Profile == null)
            return false;

        bool changed = false;

        foreach (var function in module.Functions)
        {
            if (!Profile.Functions.TryGetValue(function.Name, out var funcProfile))
                continue;

            // Only reorder functions with enough profile data
            if (funcProfile.Branches.Count == 0)
                continue;

            changed |= ReorderBlocks(function, funcProfile);
        }

        return changed;
    }

    private bool ReorderBlocks(IrFunction function, FunctionProfile funcProfile)
    {
        if (function.BasicBlocks.Count <= 2)
            return false;

        // Calculate block "hotness" based on how often branches lead to them
        var blockHotness = new Dictionary<string, double>();

        // Entry block is always hot
        if (function.BasicBlocks.Count > 0)
        {
            blockHotness[function.BasicBlocks[0].Label] = 1.0;
        }

        // Propagate hotness through the CFG
        foreach (var (branchId, branchProfile) in funcProfile.Branches)
        {
            // branchId format: "function:block"
            var parts = branchId.Split(':');
            if (parts.Length < 2) continue;

            var blockLabel = parts[^1]; // Last part is the block label

            // Find the branch in this block
            var block = function.BasicBlocks.FirstOrDefault(b => b.Label == blockLabel);
            if (block == null) continue;

            var condBranch = block.Instructions
                .OfType<IrConditionalBranch>()
                .FirstOrDefault();

            if (condBranch == null) continue;

            // Add hotness to targets based on probability
            var trueProbability = branchProfile.TrueProbability;
            var currentHotness = blockHotness.GetValueOrDefault(blockLabel, 0.5);

            if (!blockHotness.ContainsKey(condBranch.TrueTarget))
                blockHotness[condBranch.TrueTarget] = 0;
            if (!blockHotness.ContainsKey(condBranch.FalseTarget))
                blockHotness[condBranch.FalseTarget] = 0;

            blockHotness[condBranch.TrueTarget] += currentHotness * trueProbability;
            blockHotness[condBranch.FalseTarget] += currentHotness * (1 - trueProbability);
        }

        // Sort blocks by hotness (keeping entry block first)
        var entryBlock = function.BasicBlocks[0];
        var otherBlocks = function.BasicBlocks.Skip(1).ToList();

        // Sort: hot blocks first, cold blocks last
        otherBlocks.Sort((a, b) =>
        {
            var hotnessA = blockHotness.GetValueOrDefault(a.Label, 0.5);
            var hotnessB = blockHotness.GetValueOrDefault(b.Label, 0.5);
            return hotnessB.CompareTo(hotnessA); // Descending order
        });

        // Rebuild the block list
        var newOrder = new List<IrBasicBlock> { entryBlock };
        newOrder.AddRange(otherBlocks);

        // Check if order changed
        bool orderChanged = false;
        for (int i = 0; i < function.BasicBlocks.Count; i++)
        {
            if (function.BasicBlocks[i] != newOrder[i])
            {
                orderChanged = true;
                break;
            }
        }

        if (orderChanged)
        {
            function.BasicBlocks.Clear();
            function.BasicBlocks.AddRange(newOrder);
        }

        return orderChanged;
    }
}
