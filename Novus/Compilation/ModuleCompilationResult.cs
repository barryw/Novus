using Novus.IR;

namespace Novus.Compilation;

/// <summary>
/// Holds the result of compiling a Novus module to IR (before C code generation).
/// This allows us to collect all modules first, then generate a shared types header,
/// then generate per-function C files.
/// </summary>
public class ModuleCompilationResult
{
    public string ModulePath { get; init; }
    public string ModuleName { get; init; }
    public IrModule IrModule { get; init; }
    public List<IrStringLiteral> StringLiterals { get; init; }
    public List<string> ImportedModules { get; init; }
    public bool IsMainModule { get; init; }

    public ModuleCompilationResult(
        string modulePath,
        string moduleName,
        IrModule irModule,
        List<IrStringLiteral> stringLiterals,
        List<string> importedModules,
        bool isMainModule)
    {
        ModulePath = modulePath;
        ModuleName = moduleName;
        IrModule = irModule;
        StringLiterals = stringLiterals;
        ImportedModules = importedModules;
        IsMainModule = isMainModule;
    }
}
