using Novus.Diagnostics;
using Novus.Frontend.Generics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Configuration for IrBuilder that allows dependency injection of key components.
/// This enables testability and customization of the IR building process.
///
/// Heavy coupling in IrBuilder is addressed by allowing injection of:
/// - ISymbolResolver: Name resolution (lookup functions, types, variables)
/// - ITypeChecker: Type validation (assignment compatibility, operation types)
/// - IIrEmitter: IR code generation (emit instructions to basic blocks)
/// - SymbolTable: Symbol storage (can inject pre-populated tables)
/// - DiagnosticBag: Error/warning collection
/// - TypeInterner: Type caching/interning
/// </summary>
public class IrBuilderConfiguration
{
    /// <summary>
    /// Factory function to create an ISymbolResolver.
    /// Default creates a SymbolTableResolver.
    /// </summary>
    public Func<SymbolTable, Func<string, VariableSymbol?>?, ISymbolResolver>? SymbolResolverFactory { get; set; }

    /// <summary>
    /// Factory function to create an ITypeChecker.
    /// Default creates a DefaultTypeChecker.
    /// </summary>
    public Func<DiagnosticBag, ITypeChecker>? TypeCheckerFactory { get; set; }

    /// <summary>
    /// Factory function to create an IIrEmitter.
    /// Default creates a DefaultIrEmitter.
    /// </summary>
    public Func<IR.IrFunction, IIrEmitter>? EmitterFactory { get; set; }

    /// <summary>
    /// Pre-populated symbol table to use.
    /// Useful for multi-module compilation where symbols from other modules are pre-loaded.
    /// </summary>
    public SymbolTable? SymbolTable { get; set; }

    /// <summary>
    /// Diagnostic bag for error/warning collection.
    /// If not provided, a new DiagnosticBag is created.
    /// </summary>
    public DiagnosticBag? Diagnostics { get; set; }

    /// <summary>
    /// Type interner for efficient type equality checks.
    /// If not provided, a new TypeInterner is created.
    /// </summary>
    public TypeInterner? TypeInterner { get; set; }

    /// <summary>
    /// Skip auto-importing the core module.
    /// Useful for tests that want to control imports explicitly.
    /// </summary>
    public bool SkipAutoImports { get; set; }

    /// <summary>
    /// Pre-computed analysis results from SemanticAnalyzer.
    /// When set, IrBuilder will use these instead of re-computing type information.
    /// </summary>
    public AnalysisResult? AnalysisResult { get; set; }

    /// <summary>
    /// Path to the standard library.
    /// </summary>
    public string? StdLibPath { get; set; }

    /// <summary>
    /// Path to the input file being compiled.
    /// </summary>
    public string? InputFilePath { get; set; }

    /// <summary>
    /// Source lines for error reporting.
    /// </summary>
    public string[]? SourceLines { get; set; }

    /// <summary>
    /// Creates a default configuration with all default implementations.
    /// </summary>
    public static IrBuilderConfiguration Default => new();

    /// <summary>
    /// Creates a configuration for testing with auto-imports disabled.
    /// </summary>
    public static IrBuilderConfiguration ForTesting => new() { SkipAutoImports = true };

    /// <summary>
    /// Creates a configuration using pre-computed semantic analysis results.
    /// This enforces proper compiler phase ordering: type check before IR build.
    /// </summary>
    public static IrBuilderConfiguration FromAnalysisResult(AnalysisResult analysisResult) => new()
    {
        AnalysisResult = analysisResult,
        SkipAutoImports = false
    };

    /// <summary>
    /// Builder pattern for fluent configuration.
    /// </summary>
    public IrBuilderConfiguration WithSymbolResolver(Func<SymbolTable, Func<string, VariableSymbol?>?, ISymbolResolver> factory)
    {
        SymbolResolverFactory = factory;
        return this;
    }

    public IrBuilderConfiguration WithTypeChecker(Func<DiagnosticBag, ITypeChecker> factory)
    {
        TypeCheckerFactory = factory;
        return this;
    }

    public IrBuilderConfiguration WithEmitter(Func<IR.IrFunction, IIrEmitter> factory)
    {
        EmitterFactory = factory;
        return this;
    }

    public IrBuilderConfiguration WithDiagnostics(DiagnosticBag diagnostics)
    {
        Diagnostics = diagnostics;
        return this;
    }

    public IrBuilderConfiguration WithSymbolTable(SymbolTable symbolTable)
    {
        SymbolTable = symbolTable;
        return this;
    }

    public IrBuilderConfiguration WithTypeInterner(TypeInterner typeInterner)
    {
        TypeInterner = typeInterner;
        return this;
    }

    public IrBuilderConfiguration WithStdLibPath(string path)
    {
        StdLibPath = path;
        return this;
    }

    public IrBuilderConfiguration WithInputFilePath(string path)
    {
        InputFilePath = path;
        return this;
    }

    public IrBuilderConfiguration WithSourceLines(string[] lines)
    {
        SourceLines = lines;
        return this;
    }
}
