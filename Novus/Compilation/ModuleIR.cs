using Novus.IR;

namespace Novus.Compilation;

/// <summary>
/// Module IR compilation result (before C code generation).
/// Holds all IR and metadata from parsing/analyzing a Novus module.
/// </summary>
public record ModuleIR(
    string ModulePath,
    string ModuleName,
    IrModule IrModule,
    List<IrStringLiteral> StringLiterals,
    List<string> ImportedModules,
    bool HasMain);
