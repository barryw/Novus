using Novus.Codegen;
using Novus.Diagnostics;

namespace Novus.Compilation;

/// <summary>
/// Shared context for compilation phases.
/// Contains options, state, and results that flow between phases.
/// </summary>
public class CompilationContext
{
    // Input configuration
    public required CompilerOptions Options { get; init; }
    public required string CompilerDir { get; init; }
    public required string StdLibPath { get; init; }
    public required string OutputDir { get; init; }
    public required string BaseName { get; init; }
    public required SafetyLevel SafetyLevel { get; init; }

    // State from parsing phase
    public ModuleIR? MainModuleIR { get; set; }
    public Dictionary<string, ModuleIR> AllModulesIR { get; } = new();

    // State from code generation phase
    public List<string> CFiles { get; } = new();
    public List<string> ObjectFiles { get; } = new();
    public List<CCodeGenerator> AllCodeGenerators { get; } = new();
    public string? TypesHeaderHash { get; set; }
    public string? TypesHeaderPath { get; set; }

    // Library wrapper assembly object (linked after C code)
    public string? WrapperObjectFile { get; set; }

    // Caches
    public ModuleCache ModuleCache { get; } = new();
    public CompilationCache? CompilationCache { get; set; }
    public DiagnosticBag Diagnostics { get; } = new();
    public CircularImportDetector CircularImportDetector { get; set; } = null!;

    // Assembly target CPU (resolved from "auto" to concrete target)
    public string AssemblyCpu => Options.Cpu == "auto" ? "68020" : Options.Cpu;

    // Build mode string for cache directories
    public string BuildModeStr => Options.BuildMode == BuildMode.Release ? "release" : "debug";

    // Helper properties
    public bool IsLibrary => Options.ProjectType.Equals("library", StringComparison.OrdinalIgnoreCase);
    public bool IsDevice => Options.ProjectType.Equals("device", StringComparison.OrdinalIgnoreCase);
}
