using Novus.IR;

namespace Novus.Transforms;

/// <summary>
/// Manages execution of IR transformation passes
/// Transformations run BEFORE optimization and can modify IR structure
/// </summary>
public class TransformPipeline
{
    private readonly List<IIrTransformPass> _passes = new();
    private readonly bool _verbose;

    public TransformPipeline(bool verbose = false)
    {
        _verbose = verbose;
    }

    /// <summary>
    /// Add a transformation pass to the pipeline
    /// </summary>
    public void AddPass(IIrTransformPass pass)
    {
        _passes.Add(pass);
    }

    /// <summary>
    /// Run all transformation passes on the module
    /// Returns true if any pass modified the IR
    /// </summary>
    public bool Run(IrModule module)
    {
        if (_verbose)
        {
            Console.WriteLine($"Running transformation pipeline with {_passes.Count} passes...");
        }

        bool anyChanged = false;

        foreach (var pass in _passes)
        {
            if (_verbose)
            {
                Console.WriteLine($"  Running {pass.Name}...");
            }

            bool changed = pass.Transform(module);
            anyChanged |= changed;

            if (_verbose && changed)
            {
                Console.WriteLine($"    -> Modified");
            }
        }

        if (_verbose)
        {
            Console.WriteLine($"Transformation pipeline complete (modified: {anyChanged})");
        }

        return anyChanged;
    }

    /// <summary>
    /// Create a pipeline for required HIR lowering passes.
    /// These MUST run regardless of optimization level to convert DSLs to standard IR.
    /// </summary>
    public static TransformPipeline CreateLoweringPipeline(bool verbose = false)
    {
        var pipeline = new TransformPipeline(verbose);

        // HIR Lowering passes - convert high-level DSLs to standard IR
        pipeline.AddPass(new Passes.CopperLoweringPass());
        pipeline.AddPass(new Passes.BlitterLoweringPass());

        // Future required lowering passes:
        // pipeline.AddPass(new Passes.AsyncLoweringPass());

        return pipeline;
    }

    /// <summary>
    /// Create a pipeline for optional optimization-related transformations.
    /// These only run when optimization is enabled.
    /// </summary>
    public static TransformPipeline CreateOptimizationPipeline(bool enableInlining = false, bool verbose = false)
    {
        var pipeline = new TransformPipeline(verbose);

        // Inlining pass - only when optimizing
        if (enableInlining)
        {
            pipeline.AddPass(new Passes.InlineExpansionPass());
        }

        // Future optimization passes:
        // pipeline.AddPass(new Passes.GenericMonomorphizationPass());

        return pipeline;
    }

    /// <summary>
    /// Create a standard transformation pipeline (legacy - for tests)
    /// </summary>
    public static TransformPipeline CreatePipeline(bool enableInlining = false, bool verbose = false)
    {
        var pipeline = new TransformPipeline(verbose);

        // Add transformation passes in order

        // HIR Lowering passes - run first to convert high-level DSLs to IR
        pipeline.AddPass(new Passes.CopperLoweringPass());
        pipeline.AddPass(new Passes.BlitterLoweringPass());

        // Inlining pass - runs after lowering, before optimization
        if (enableInlining)
        {
            pipeline.AddPass(new Passes.InlineExpansionPass());
        }

        // Future passes:
        // pipeline.AddPass(new Passes.GenericMonomorphizationPass());
        // pipeline.AddPass(new Passes.AsyncLoweringPass());

        return pipeline;
    }
}
