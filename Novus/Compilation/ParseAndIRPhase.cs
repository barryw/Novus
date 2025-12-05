using Novus.Diagnostics;

namespace Novus.Compilation;

/// <summary>
/// Phase 1: Parse source files and generate IR for all modules.
/// </summary>
public class ParseAndIRPhase : ICompilationPhase
{
    public string Name => "Parse and IR Generation";

    private readonly Func<string, string, CompilerOptions, ModuleCache, CircularImportDetector, CompilationCache?, Task<ModuleIR?>> _compileModuleToIR;

    public ParseAndIRPhase(
        Func<string, string, CompilerOptions, ModuleCache, CircularImportDetector, CompilationCache?, Task<ModuleIR?>> compileModuleToIR)
    {
        _compileModuleToIR = compileModuleToIR;
    }

    public async Task<bool> ExecuteAsync(CompilationContext context)
    {
        Console.WriteLine("Parsing...");
        Console.WriteLine("Analyzing semantics...");
        Console.WriteLine("Building IR...");

        // Compile the main file to IR
        var mainIR = await _compileModuleToIR(
            context.Options.InputFile,
            context.StdLibPath,
            context.Options,
            context.ModuleCache,
            context.CircularImportDetector,
            context.CompilationCache);

        if (mainIR == null)
        {
            if (context.Diagnostics.HasErrors)
            {
                Console.WriteLine(context.Diagnostics.FormatDiagnostics());
            }
            return false;
        }

        context.MainModuleIR = mainIR;

        // Record dependencies from the main module
        foreach (var import in mainIR.ImportedModules)
        {
            if (!context.CircularImportDetector.RecordDependency(context.Options.InputFile, import))
            {
                Console.WriteLine(context.Diagnostics.FormatDiagnostics());
                return false;
            }
        }

        // Recursively collect all dependencies to IR
        var toProcess = new Queue<string>(mainIR.ImportedModules);
        var processed = new HashSet<string>();

        while (toProcess.Count > 0)
        {
            var modulePath = toProcess.Dequeue();
            if (processed.Contains(modulePath))
                continue;

            processed.Add(modulePath);

            // Show which module is being compiled
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetFileName(Path.GetDirectoryName(modulePath));
            var displayName = moduleDir == "std" ? $"std::{moduleName}" : moduleName;
            Console.WriteLine($"  → {displayName}");

            var moduleIR = await _compileModuleToIR(
                modulePath,
                context.StdLibPath,
                context.Options,
                context.ModuleCache,
                context.CircularImportDetector,
                context.CompilationCache);

            if (moduleIR == null)
            {
                if (context.Diagnostics.HasErrors)
                {
                    Console.WriteLine(context.Diagnostics.FormatDiagnostics());
                }
                else
                {
                    Console.WriteLine($"Failed to compile dependency: {modulePath}");
                }
                return false;
            }

            context.AllModulesIR[modulePath] = moduleIR;

            // Add transitive dependencies
            foreach (var import in moduleIR.ImportedModules)
            {
                if (!context.CircularImportDetector.RecordDependency(modulePath, import))
                {
                    Console.WriteLine(context.Diagnostics.FormatDiagnostics());
                    return false;
                }

                if (!processed.Contains(import))
                {
                    toProcess.Enqueue(import);
                }
            }
        }

        if (context.AllModulesIR.Count > 0)
        {
            Console.WriteLine($"  ✓ Compiled {context.AllModulesIR.Count + 1} module{(context.AllModulesIR.Count > 0 ? "s" : "")}");
        }

        if (context.Options.Verbose && context.ModuleCache.Count > 0)
        {
            Console.WriteLine($"  Module cache: {context.ModuleCache.Count} modules cached");
        }

        return true;
    }
}
