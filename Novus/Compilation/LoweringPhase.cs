using Novus.Transforms;

namespace Novus.Compilation;

/// <summary>
/// Phase 1.5: Run HIR lowering passes on all modules.
/// Converts high-level DSLs (copper/blitter) to standard IR.
/// Must run before code generation, regardless of optimization level.
/// </summary>
public class LoweringPhase : ICompilationPhase
{
    public string Name => "HIR Lowering";

    public Task<bool> ExecuteAsync(CompilationContext context)
    {
        var loweringPipeline = TransformPipeline.CreateLoweringPipeline(context.Options.Verbose);

        // Run lowering on main module
        if (context.MainModuleIR != null)
        {
            loweringPipeline.Run(context.MainModuleIR.IrModule);
        }

        // Run lowering on all imported modules
        foreach (var moduleIR in context.AllModulesIR.Values)
        {
            loweringPipeline.Run(moduleIR.IrModule);
        }

        // Optionally emit IR
        if (context.Options.EmitIr)
        {
            Console.WriteLine("\n=== IR Dump (Before Optimization) ===");
            Console.WriteLine("(IR dump not yet implemented)");
            Console.WriteLine();
        }

        return Task.FromResult(true);
    }
}
