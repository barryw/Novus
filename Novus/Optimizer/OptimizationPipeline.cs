using Novus.IR;

namespace Novus.Optimizer;

/// <summary>
/// Manages the execution of optimization passes on IR modules
/// </summary>
public class OptimizationPipeline
{
    private readonly List<IOptimizationPass> _passes = new();
    private readonly int _maxIterations;
    private readonly bool _verbose;

    public OptimizationPipeline(int maxIterations = 10, bool verbose = false)
    {
        _maxIterations = maxIterations;
        _verbose = verbose;
    }

    /// <summary>
    /// Add an optimization pass to the pipeline
    /// </summary>
    public void AddPass(IOptimizationPass pass)
    {
        _passes.Add(pass);
    }

    /// <summary>
    /// Get a specific pass by type (for retrieving results)
    /// </summary>
    public T? GetPass<T>() where T : IOptimizationPass
    {
        return _passes.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Run all optimization passes on the module until fixpoint or max iterations
    /// </summary>
    public void Run(IrModule module)
    {
        if (_verbose)
        {
            Console.WriteLine($"Running optimization pipeline with {_passes.Count} passes...");
        }

        // Run optimization passes until convergence
        for (int iteration = 0; iteration < _maxIterations; iteration++)
        {
            bool changed = false;

            foreach (var pass in _passes)
            {
                if (_verbose)
                {
                    Console.WriteLine($"  [Iteration {iteration + 1}] Running {pass.Name}...");
                }

                bool passChanged = pass.Run(module);
                changed |= passChanged;

                if (_verbose && passChanged)
                {
                    Console.WriteLine($"    -> Modified");
                }

                // In DEBUG builds, validate IR after each pass to catch bugs early
                #if DEBUG
                ValidateModuleAfterPass(module, pass.Name);
                #endif
            }

            // If no pass made changes, we've reached a fixpoint
            if (!changed)
            {
                if (_verbose)
                {
                    Console.WriteLine($"Optimization converged after {iteration + 1} iteration(s)");
                }
                break;
            }
        }
    }

    /// <summary>
    /// Validate IR after an optimization pass (DEBUG builds only)
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private void ValidateModuleAfterPass(IrModule module, string passName)
    {
        var validator = new IrValidator();
        var result = validator.Validate(module);
        if (!result.IsValid)
        {
            var errors = string.Join("\n  ", result.Errors);
            throw new InvalidOperationException(
                $"IR validation failed after optimization pass '{passName}':\n  {errors}");
        }
    }

    /// <summary>
    /// Create a standard optimization pipeline for a given optimization level
    /// </summary>
    public static OptimizationPipeline CreatePipeline(int level, bool verbose = false)
    {
        var pipeline = new OptimizationPipeline(maxIterations: 10, verbose: verbose);

        switch (level)
        {
            case 0:
                // No optimizations
                break;

            case 1:
                // Basic optimizations - fast compile time
                pipeline.AddPass(new Passes.ConstantFoldingPass());
                pipeline.AddPass(new Passes.AlgebraicSimplificationPass());
                pipeline.AddPass(new Passes.DeadCodeEliminationPass());
                break;

            case 2:
                // Standard optimizations - balanced
                pipeline.AddPass(new Passes.ConstantFoldingPass());
                pipeline.AddPass(new Passes.AlgebraicSimplificationPass());
                pipeline.AddPass(new Passes.ConstantPropagationPass());
                pipeline.AddPass(new Passes.ResultOptimizationPass());  // Optimize Result/Option before DCE
                pipeline.AddPass(new Passes.CFGDeadCodeEliminationPass());
                pipeline.AddPass(new Passes.CopyPropagationPass());
                break;

            case 3:
                // Aggressive optimizations - longer compile time
                // Function inlining runs first to expose more optimization opportunities
                pipeline.AddPass(new Passes.FunctionInliningPass());
                pipeline.AddPass(new Passes.ConstantFoldingPass());
                pipeline.AddPass(new Passes.AlgebraicSimplificationPass());
                pipeline.AddPass(new Passes.ConstantPropagationPass());
                pipeline.AddPass(new Passes.ResultOptimizationPass());  // Optimize Result/Option before DCE
                pipeline.AddPass(new Passes.CFGDeadCodeEliminationPass());
                pipeline.AddPass(new Passes.CopyPropagationPass());
                pipeline.AddPass(new Passes.CommonSubexpressionEliminationPass());
                pipeline.AddPass(new Passes.StrengthReductionPass());
                pipeline.AddPass(new Passes.LoopInvariantCodeMotionPass());
                // Dead function elimination runs AFTER inlining (inlining may make functions dead)
                pipeline.AddPass(new Passes.DeadFunctionEliminationPass());
                break;

            default:
                throw new ArgumentException($"Invalid optimization level: {level}. Must be 0-3.");
        }

        return pipeline;
    }

    /// <summary>
    /// Create a pipeline for generating instrumented code (--pgo-generate)
    /// </summary>
    public static OptimizationPipeline CreateInstrumentationPipeline(bool verbose = false)
    {
        var pipeline = new OptimizationPipeline(maxIterations: 1, verbose: verbose);

        // Only add instrumentation - no other optimizations
        // We want the profile data to reflect the original code structure
        pipeline.AddPass(new Passes.InstrumentationPass());

        return pipeline;
    }

    /// <summary>
    /// Create a PGO-guided optimization pipeline (--pgo-use)
    /// </summary>
    public static OptimizationPipeline CreatePgoPipeline(int level, ProfileData profile, bool verbose = false)
    {
        var pipeline = new OptimizationPipeline(maxIterations: 10, verbose: verbose);

        // Run PGO passes first to add metadata
        pipeline.AddPass(new Passes.ProfileGuidedBranchOptimizationPass { Profile = profile });
        pipeline.AddPass(new Passes.ProfileGuidedInliningPass { Profile = profile });
        pipeline.AddPass(new Passes.ProfileGuidedLoopOptimizationPass { Profile = profile });

        // Then run standard optimization passes based on level
        switch (level)
        {
            case 0:
                // Still apply PGO metadata even at O0
                break;

            case 1:
                pipeline.AddPass(new Passes.ConstantFoldingPass());
                pipeline.AddPass(new Passes.AlgebraicSimplificationPass());
                pipeline.AddPass(new Passes.DeadCodeEliminationPass());
                break;

            case 2:
                pipeline.AddPass(new Passes.ConstantFoldingPass());
                pipeline.AddPass(new Passes.AlgebraicSimplificationPass());
                pipeline.AddPass(new Passes.ConstantPropagationPass());
                pipeline.AddPass(new Passes.ResultOptimizationPass());
                pipeline.AddPass(new Passes.CFGDeadCodeEliminationPass());
                pipeline.AddPass(new Passes.CopyPropagationPass());
                break;

            case 3:
                // PGO-guided inlining already added above
                pipeline.AddPass(new Passes.FunctionInliningPass());
                pipeline.AddPass(new Passes.ConstantFoldingPass());
                pipeline.AddPass(new Passes.AlgebraicSimplificationPass());
                pipeline.AddPass(new Passes.ConstantPropagationPass());
                pipeline.AddPass(new Passes.ResultOptimizationPass());
                pipeline.AddPass(new Passes.CFGDeadCodeEliminationPass());
                pipeline.AddPass(new Passes.CopyPropagationPass());
                pipeline.AddPass(new Passes.CommonSubexpressionEliminationPass());
                pipeline.AddPass(new Passes.StrengthReductionPass());
                pipeline.AddPass(new Passes.LoopInvariantCodeMotionPass());
                // PGO code layout should be near the end
                pipeline.AddPass(new Passes.ProfileGuidedCodeLayoutPass { Profile = profile });
                pipeline.AddPass(new Passes.DeadFunctionEliminationPass());
                break;
        }

        return pipeline;
    }
}

/// <summary>
/// Base class for function-level passes that iterates over all functions
/// </summary>
public abstract class FunctionPassBase : IFunctionPass
{
    public abstract string Name { get; }

    public bool Run(IrModule module)
    {
        bool changed = false;
        foreach (var function in module.Functions)
        {
            changed |= RunOnFunction(function);
        }
        return changed;
    }

    public abstract bool RunOnFunction(IrFunction function);
}

/// <summary>
/// Base class for basic-block level passes that iterates over all blocks
/// </summary>
public abstract class BasicBlockPassBase : IBasicBlockPass
{
    public abstract string Name { get; }

    public bool Run(IrModule module)
    {
        bool changed = false;
        foreach (var function in module.Functions)
        {
            foreach (var block in function.BasicBlocks)
            {
                changed |= RunOnBasicBlock(block);
            }
        }
        return changed;
    }

    public abstract bool RunOnBasicBlock(IrBasicBlock block);
}
