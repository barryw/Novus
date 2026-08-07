using Novus.Diagnostics;
using Novus.IR;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Encapsulates the results of semantic analysis for use by subsequent compiler phases.
/// This class serves as the bridge between SemanticAnalyzer and IrBuilder, ensuring
/// that type checking happens before IR building and that type information is shared.
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// Function symbols discovered during analysis (first overload per name for compatibility).
    /// For full overload information, use FunctionOverloads.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionSymbol> Functions { get; }

    /// <summary>
    /// All function overloads by name. Each name maps to a list of overloads with different signatures.
    /// </summary>
    public IReadOnlyDictionary<string, List<FunctionSymbol>> FunctionOverloads { get; }

    /// <summary>
    /// Names of functions that have multiple overloads (different signatures).
    /// </summary>
    public IReadOnlySet<string> OverloadedFunctionNames { get; }

    /// <summary>
    /// Local variable symbols in the current scope.
    /// </summary>
    public IReadOnlyDictionary<string, VariableSymbol> Variables { get; }

    /// <summary>
    /// Global (extern) variable symbols.
    /// </summary>
    public IReadOnlyDictionary<string, VariableSymbol> GlobalVariables { get; }

    /// <summary>
    /// Struct type definitions.
    /// </summary>
    public IReadOnlyDictionary<string, IrStructType> Structs { get; }

    /// <summary>
    /// Enum type definitions.
    /// </summary>
    public IReadOnlyDictionary<string, IrEnumType> Enums { get; }

    /// <summary>
    /// Transparent type aliases.
    /// </summary>
    public IReadOnlyDictionary<string, IrType> TypeAliases { get; }

    /// <summary>
    /// Trait definitions.
    /// </summary>
    public IReadOnlyDictionary<string, IrTrait> Traits { get; }

    /// <summary>
    /// Constant definitions.
    /// </summary>
    public IReadOnlyDictionary<string, ConstantSymbol> Constants { get; }

    /// <summary>
    /// Source locations of struct definitions (for error reporting).
    /// </summary>
    public IReadOnlyDictionary<string, SourceLocation> StructLocations { get; }

    /// <summary>
    /// Source locations of enum definitions (for error reporting).
    /// </summary>
    public IReadOnlyDictionary<string, SourceLocation> EnumLocations { get; }

    /// <summary>
    /// Source locations of trait definitions (for error reporting).
    /// </summary>
    public IReadOnlyDictionary<string, SourceLocation> TraitLocations { get; }

    /// <summary>
    /// Documentation comments extracted during analysis.
    /// </summary>
    public IReadOnlyDictionary<string, string> DocComments { get; }

    /// <summary>
    /// Trait implementations collected during analysis.
    /// </summary>
    public TraitResolver TraitResolver { get; }

    /// <summary>
    /// Type interner for canonical type representation.
    /// </summary>
    public TypeInterner TypeInterner { get; }

    /// <summary>
    /// Whether the analysis succeeded without errors.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Any diagnostics (errors, warnings) from analysis.
    /// </summary>
    public DiagnosticBag Diagnostics { get; }

    /// <summary>
    /// The file path of the analyzed source.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// The source code that was analyzed.
    /// </summary>
    public string SourceCode { get; }

    public AnalysisResult(
        bool success,
        DiagnosticBag diagnostics,
        string filePath,
        string sourceCode,
        IReadOnlyDictionary<string, FunctionSymbol> functions,
        IReadOnlyDictionary<string, List<FunctionSymbol>> functionOverloads,
        IReadOnlySet<string> overloadedFunctionNames,
        IReadOnlyDictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, VariableSymbol> globalVariables,
        IReadOnlyDictionary<string, IrStructType> structs,
        IReadOnlyDictionary<string, IrEnumType> enums,
        IReadOnlyDictionary<string, IrType> typeAliases,
        IReadOnlyDictionary<string, IrTrait> traits,
        IReadOnlyDictionary<string, ConstantSymbol> constants,
        IReadOnlyDictionary<string, SourceLocation> structLocations,
        IReadOnlyDictionary<string, SourceLocation> enumLocations,
        IReadOnlyDictionary<string, SourceLocation> traitLocations,
        IReadOnlyDictionary<string, string> docComments,
        TraitResolver traitResolver,
        TypeInterner typeInterner)
    {
        Success = success;
        Diagnostics = diagnostics;
        FilePath = filePath;
        SourceCode = sourceCode;
        Functions = functions;
        FunctionOverloads = functionOverloads;
        OverloadedFunctionNames = overloadedFunctionNames;
        Variables = variables;
        GlobalVariables = globalVariables;
        Structs = structs;
        Enums = enums;
        TypeAliases = typeAliases;
        Traits = traits;
        Constants = constants;
        StructLocations = structLocations;
        EnumLocations = enumLocations;
        TraitLocations = traitLocations;
        DocComments = docComments;
        TraitResolver = traitResolver;
        TypeInterner = typeInterner;
    }
}
