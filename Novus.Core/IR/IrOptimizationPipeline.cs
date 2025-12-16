namespace Novus.IR;

/// <summary>
/// Orchestrates all IR optimization passes in the correct order.
///
/// The optimization pipeline runs passes iteratively until a fixpoint is reached.
/// This ensures that optimizations enabled by other optimizations are applied.
///
/// Pass ordering is critical for effectiveness:
/// 1. SSA Construction (if needed)
/// 2. Algebraic Simplification (enables constant folding)
/// 3. Strength Reduction (reduces expensive operations)
/// 4. Constant Propagation (propagates known values)
/// 5. Copy Propagation (eliminates redundant copies)
/// 6. Dead Code Elimination (removes unused code)
/// 7. SSA Destruction (converts back to normal form)
///
/// The pipeline can be configured to run specific optimization levels:
/// - O0: No optimizations
/// - O1: Basic optimizations (algebraic, constant prop, DCE)
/// - O2: Standard optimizations (adds strength reduction, copy prop)
/// - O3: Aggressive optimizations (adds loop opts, inlining, etc.)
/// - Os: Size optimizations (prefer code size over speed)
/// </summary>
public class IrOptimizationPipeline
{
    private readonly IrFunction _function;
    private readonly OptimizationLevel _level;
    private readonly OptimizationOptions _options;

    public IrOptimizationPipeline(IrFunction function, OptimizationLevel level = OptimizationLevel.O2, OptimizationOptions? options = null)
    {
        _function = function;
        _level = level;
        _options = options ?? new OptimizationOptions();
    }

    /// <summary>
    /// Run the complete optimization pipeline.
    /// Returns statistics about what was optimized.
    /// </summary>
    public OptimizationStatistics Run()
    {
        var stats = new OptimizationStatistics();

        if (_level == OptimizationLevel.O0)
            return stats; // No optimizations

        // Track if we're in SSA form
        bool inSSA = false;

        // SSA Conversion Policy:
        // - O1: Optional (controlled by _options.UseSSA)
        // - O2+: Default ON (SSA enables better optimizations like CSE, copy propagation)
        // - Can be explicitly disabled via _options.UseSSA = false even at O2+
        bool shouldUseSSA = _level >= OptimizationLevel.O2 || _options.UseSSA;

        if (shouldUseSSA && !inSSA)
        {
            if (_options.Verbose)
                Console.WriteLine("Converting to SSA form...");

            var ssaConstructor = new SsaConstructor(_function);
            ssaConstructor.ConstructSsa();
            inSSA = true;
            stats.SSAConstructed = true;
        }

        // Run optimization passes iteratively until fixpoint
        int iteration = 0;
        bool changed;

        do
        {
            changed = false;
            iteration++;

            if (_options.Verbose)
                Console.WriteLine($"\n=== Optimization Iteration {iteration} ===");

            if (_options.MaxIterations > 0 && iteration > _options.MaxIterations)
            {
                if (_options.Verbose)
                    Console.WriteLine("Max iterations reached, stopping.");
                break;
            }

            // Phase 1: Algebraic simplifications
            if (_level >= OptimizationLevel.O1)
            {
                var algebraic = new AlgebraicSimplification(_function);
                int count = algebraic.Simplify();
                stats.AlgebraicSimplifications += count;
                if (count > 0)
                {
                    changed = true;
                    if (_options.Verbose)
                        Console.WriteLine($"  Algebraic Simplification: {count} simplifications");
                }
            }

            // Phase 2: Strength reduction (replace expensive ops with cheap ones)
            if (_level >= OptimizationLevel.O2)
            {
                var strengthReduction = new StrengthReduction(_function);
                int count = strengthReduction.Reduce();
                stats.StrengthReductions += count;
                if (count > 0)
                {
                    changed = true;
                    if (_options.Verbose)
                        Console.WriteLine($"  Strength Reduction: {count} reductions");
                }
            }

            // Phase 3: Constant propagation and folding
            if (_level >= OptimizationLevel.O1)
            {
                var constantProp = new ConstantPropagation(_function);
                int count = constantProp.Propagate();
                stats.ConstantPropagations += count;
                if (count > 0)
                {
                    changed = true;
                    if (_options.Verbose)
                        Console.WriteLine($"  Constant Propagation: {count} propagations");
                }
            }

            // Phase 4: Copy propagation
            if (_level >= OptimizationLevel.O2)
            {
                var copyProp = new CopyPropagation(_function);
                int count = copyProp.Propagate();
                stats.CopyPropagations += count;
                if (count > 0)
                {
                    changed = true;
                    if (_options.Verbose)
                        Console.WriteLine($"  Copy Propagation: {count} propagations");
                }
            }

            // Phase 5: Common Subexpression Elimination
            if (_level >= OptimizationLevel.O2)
            {
                var cse = new CommonSubexpressionElimination(_function);
                int count = cse.Eliminate();
                stats.CommonSubexpressionEliminations += count;
                if (count > 0)
                {
                    changed = true;
                    if (_options.Verbose)
                        Console.WriteLine($"  Common Subexpression Elimination: {count} eliminations");
                }
            }

            // Phase 6: Dead store elimination
            if (_level >= OptimizationLevel.O1)
            {
                var dse = new DeadStoreElimination(_function);
                int count = dse.Eliminate();
                stats.DeadStoreEliminations += count;
                if (count > 0)
                {
                    changed = true;
                    if (_options.Verbose)
                        Console.WriteLine($"  Dead Store Elimination: {count} eliminations");
                }
            }

            // Phase 7: Dead code elimination
            if (_level >= OptimizationLevel.O1)
            {
                var dce = new DeadCodeElimination(_function);
                int count = dce.Eliminate();
                stats.DeadCodeEliminations += count;
                if (count > 0)
                {
                    changed = true;
                    if (_options.Verbose)
                        Console.WriteLine($"  Dead Code Elimination: {count} eliminations");
                }
            }

            // Phase 8: 68k Peephole Optimization
            if (_level >= OptimizationLevel.O2)
            {
                var peephole = new M68kPeepholeOptimization(_function, _options.CpuTarget);
                int count = peephole.Optimize();
                stats.PeepholeOptimizations += count;
                if (count > 0)
                {
                    changed = true;
                    if (_options.Verbose)
                        Console.WriteLine($"  68k Peephole Optimization: {count} optimizations");
                }
            }

            // Note: Function Inlining is handled at the module level via FunctionInliningPass in OptimizationPipeline
            // Additional O3 passes (implemented where beneficial for 68k targets):
            // - Loop-Invariant Code Motion: planned for loop-heavy code
            // - Induction Variable Optimization: planned for array iteration
            // - Global Value Numbering: subsumed by CSE for most cases
            // - Partial Redundancy Elimination: limited benefit on 68k register-starved architecture

            stats.Iterations = iteration;

        } while (changed);

        if (_options.Verbose)
            Console.WriteLine($"\nReached fixpoint after {iteration} iterations");

        // Convert out of SSA if we converted in
        if (inSSA && !_options.LeaveInSSA)
        {
            if (_options.Verbose)
                Console.WriteLine("\nConverting out of SSA form...");

            var ssaDestruction = new SsaDestruction(_function);
            ssaDestruction.DestructSsa();
            ssaDestruction.RemoveSsaVersioning();
            stats.SSADestroyed = true;
        }

        return stats;
    }

    /// <summary>
    /// Run a quick optimization pass (single iteration, most important opts only).
    /// Useful for compile-time sensitive scenarios.
    /// </summary>
    public OptimizationStatistics RunQuick()
    {
        var stats = new OptimizationStatistics();

        // Just one pass of the most impactful optimizations
        var algebraic = new AlgebraicSimplification(_function);
        stats.AlgebraicSimplifications = algebraic.Simplify();

        var constantProp = new ConstantPropagation(_function);
        stats.ConstantPropagations = constantProp.Propagate();

        var dce = new DeadCodeElimination(_function);
        stats.DeadCodeEliminations = dce.Eliminate();

        stats.Iterations = 1;

        return stats;
    }
}

/// <summary>
/// Optimization level determines which passes run and how aggressive they are.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>No optimizations</summary>
    O0,

    /// <summary>Basic optimizations: algebraic simplification, constant propagation, DCE</summary>
    O1,

    /// <summary>Standard optimizations: adds strength reduction, copy propagation</summary>
    O2,

    /// <summary>Aggressive optimizations: adds loop opts, inlining, GVN, etc.</summary>
    O3,

    /// <summary>Size optimizations: prefer smaller code over faster code</summary>
    Os
}

/// <summary>
/// Options to configure the optimization pipeline.
/// </summary>
public class OptimizationOptions
{
    /// <summary>Convert to SSA form before optimizing</summary>
    public bool UseSSA { get; set; } = false;

    /// <summary>Leave function in SSA form after optimization (don't destruct)</summary>
    public bool LeaveInSSA { get; set; } = false;

    /// <summary>Maximum number of optimization iterations (0 = unlimited)</summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>Print verbose optimization statistics</summary>
    public bool Verbose { get; set; } = false;

    /// <summary>Target 68k CPU (affects strength reduction, peephole opts)</summary>
    public M68kCpuTarget CpuTarget { get; set; } = M68kCpuTarget.M68020;

    /// <summary>Optimize for size instead of speed</summary>
    public bool OptimizeSize { get; set; } = false;
}

/// <summary>
/// Target 68k CPU for optimization decisions.
/// </summary>
public enum M68kCpuTarget
{
    /// <summary>68000/68010 - no 32-bit multiply/divide</summary>
    M68000,

    /// <summary>68020/68030 - has 32-bit multiply/divide</summary>
    M68020,

    /// <summary>68040 - pipelined, different instruction costs</summary>
    M68040,

    /// <summary>68060 - superscalar, avoid certain instructions</summary>
    M68060,

    /// <summary>68080/Apollo - AMMX SIMD instructions available</summary>
    M68080
}

/// <summary>
/// Statistics about what optimizations were performed.
/// </summary>
public class OptimizationStatistics
{
    public int Iterations { get; set; }
    public int AlgebraicSimplifications { get; set; }
    public int StrengthReductions { get; set; }
    public int ConstantPropagations { get; set; }
    public int CopyPropagations { get; set; }
    public int DeadStoreEliminations { get; set; }
    public int DeadCodeEliminations { get; set; }
    public bool SSAConstructed { get; set; }
    public bool SSADestroyed { get; set; }

    // Placeholders for future optimizations
    public int CommonSubexpressionEliminations { get; set; }
    public int PeepholeOptimizations { get; set; }
    public int LoopInvariantMotions { get; set; }
    public int FunctionsInlined { get; set; }
    public int TailCallsOptimized { get; set; }
    public int LoopsUnrolled { get; set; }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Optimization Statistics:");
        sb.AppendLine($"  Iterations: {Iterations}");

        if (SSAConstructed)
            sb.AppendLine($"  SSA Construction: Yes");

        if (AlgebraicSimplifications > 0)
            sb.AppendLine($"  Algebraic Simplifications: {AlgebraicSimplifications}");

        if (StrengthReductions > 0)
            sb.AppendLine($"  Strength Reductions: {StrengthReductions}");

        if (ConstantPropagations > 0)
            sb.AppendLine($"  Constant Propagations: {ConstantPropagations}");

        if (CopyPropagations > 0)
            sb.AppendLine($"  Copy Propagations: {CopyPropagations}");

        if (DeadStoreEliminations > 0)
            sb.AppendLine($"  Dead Store Eliminations: {DeadStoreEliminations}");

        if (DeadCodeEliminations > 0)
            sb.AppendLine($"  Dead Code Eliminations: {DeadCodeEliminations}");

        if (CommonSubexpressionEliminations > 0)
            sb.AppendLine($"  Common Subexpression Eliminations: {CommonSubexpressionEliminations}");

        if (PeepholeOptimizations > 0)
            sb.AppendLine($"  68k Peephole Optimizations: {PeepholeOptimizations}");

        if (LoopInvariantMotions > 0)
            sb.AppendLine($"  Loop Invariant Motions: {LoopInvariantMotions}");

        if (FunctionsInlined > 0)
            sb.AppendLine($"  Functions Inlined: {FunctionsInlined}");

        if (TailCallsOptimized > 0)
            sb.AppendLine($"  Tail Calls Optimized: {TailCallsOptimized}");

        if (LoopsUnrolled > 0)
            sb.AppendLine($"  Loops Unrolled: {LoopsUnrolled}");

        if (SSADestroyed)
            sb.AppendLine($"  SSA Destruction: Yes");

        return sb.ToString();
    }
}
