using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend.Generics;

/// <summary>
/// Template information for generic functions (standalone and methods)
/// </summary>
public record GenericTemplate(
    List<string> GenericParams,  // Type-level generics (e.g., T in impl<T> Option<T>)
    NovusParser.FunctionDeclarationContext Context,
    Dictionary<string, (IrType Type, object Value)> Constants,
    IrWhereClause? WhereClause = null,  // Generic type constraints (e.g., where T: Sortable)
    List<string>? MethodGenericParams = null  // Method-level generics (e.g., E in fn ok_or<E>)
);

/// <summary>
/// Core interface for generic type and method instantiation
/// </summary>
public interface IGenericInstantiator
{
    /// <summary>
    /// Instantiate a type by substituting generic parameters with concrete types
    /// </summary>
    IrType InstantiateType(IrType genericType, Dictionary<string, IrType> substitutions);

    /// <summary>
    /// Instantiate a generic method for a struct type
    /// </summary>
    IrFunction? InstantiateStructMethod(
        IrStructType monomorphizedStruct,
        string methodName,
        bool isTraitImpl = false,
        string? traitName = null,
        List<IrType>? traitTypeArgs = null
    );

    /// <summary>
    /// Instantiate a generic method for an enum type
    /// </summary>
    IrFunction? InstantiateEnumMethod(
        IrEnumType monomorphizedEnum,
        string methodName,
        List<IrValue> arguments
    );

    /// <summary>
    /// Instantiate a generic standalone function
    /// </summary>
    IrFunction? InstantiateFunction(
        string functionName,
        Dictionary<string, IrType> typeSubstitutions
    );

    /// <summary>
    /// Register a generic method template for later instantiation
    /// </summary>
    void RegisterMethodTemplate(string templateKey, GenericTemplate template);

    /// <summary>
    /// Register a generic function template for later instantiation
    /// </summary>
    void RegisterFunctionTemplate(string functionName, GenericTemplate template);

    /// <summary>
    /// Check if a method has been instantiated
    /// </summary>
    bool HasMethodInstantiation(string cacheKey);

    /// <summary>
    /// Check if a function has been instantiated
    /// </summary>
    bool HasFunctionInstantiation(string cacheKey);

    /// <summary>
    /// Check if a method template exists
    /// </summary>
    bool HasMethodTemplate(string templateKey);

    /// <summary>
    /// Check if a function template exists
    /// </summary>
    bool HasFunctionTemplate(string functionName);

    /// <summary>
    /// Try to get a method template
    /// </summary>
    bool TryGetMethodTemplate(string templateKey, out GenericTemplate? template);

    /// <summary>
    /// Try to get a function template
    /// </summary>
    bool TryGetFunctionTemplate(string functionName, out GenericTemplate? template);

    /// <summary>
    /// Infer generic type arguments from call site arguments
    /// </summary>
    Dictionary<string, IrType>? InferGenericTypes(
        List<string> genericParams,
        List<IrParameter> templateParams,
        List<IrValue> arguments
    );

    /// <summary>
    /// Infer generic type arguments for enum associated functions
    /// Handles both argument-based and return-type-based inference
    /// </summary>
    Dictionary<string, IrType>? InferEnumGenericTypes(
        IrEnumType baseEnum,
        string methodName,
        List<IrValue> arguments,
        IrType? expectedReturnType
    );

    /// <summary>
    /// Extract generic type mappings by comparing base type with monomorphized type
    /// </summary>
    void ExtractGenericTypeMapping(
        IrType baseType,
        IrType monomorphizedType,
        Dictionary<string, IrType> substitutions
    );
}

/// <summary>
/// Context interface that GenericInstantiator needs from its host (IrBuilder)
/// This allows GenericInstantiator to call back into IrBuilder for parsing and function creation
/// </summary>
public interface IInstantiationContext
{
    // Type parsing
    IrType ParseType(NovusParser.TypeContext context);
    IrType? ParseReturnType(NovusParser.TypeContext? context);

    // Function building
    void ParseSelfParameter(
        NovusParser.SelfParameterContext? context,
        IrFunction function,
        IrType implementingType
    );
    void ParseFunctionParameters(
        NovusParser.FunctionDeclarationContext context,
        IrFunction function
    );
    void ParseVariadicParameter(
        NovusParser.ParameterListContext? paramList,
        IrFunction function
    );
    void ParseVariadicParameter(
        NovusParser.ParameterListContext? paramList,
        List<IrParameter> parameters
    );

    // Function body building
    object? VisitFunctionBody(NovusParser.BlockContext? blockContext);

    // Module access
    IrModule Module { get; }
    IrFunction? CurrentFunction { get; set; }
    IrBasicBlock? CurrentBlock { get; set; }
    Dictionary<string, IrLocalVariable> LocalVariables { get; }

    // Symbol lookups
    IrStructType? LookupStruct(string name);
    IrEnumType? LookupEnum(string name);

    // Generic parameter management
    void RegisterGenericParameter(string name, IrGenericType genericType);
    IrGenericType? LookupGenericParameter(string name);
    void ClearGenericParameters();

    // Type substitution for recursive calls
    ITypeSubstitutionEngine SubstitutionEngine { get; }

    // Name mangling
    string GenerateMethodMangledName(
        string typeName,
        string methodName,
        bool isTraitImpl,
        string? traitName,
        List<IrType> traitTypeArgs
    );

    string GetTypeCacheKey(IrType type);

    // State management for instantiation
    Dictionary<string, IrType>? CurrentTypeSubstitutions { get; set; }
    IrType? CurrentSelfType { get; set; }

    // Diagnostic reporting
    string? InputFilePath { get; }
    void ReportError(string errorCode, string message, SourceLocation location);

    // Constants management for generic templates
    void RestoreConstantsFromTuples(Dictionary<string, (IrType Type, object Value)> constants);
}

/// <summary>
/// Interface for type substitution operations (used by GenericInstantiator)
/// This is implemented by TypeParser
/// </summary>
public interface ITypeSubstitutionEngine
{
    /// <summary>
    /// Recursively substitute generic type parameters with concrete types
    /// </summary>
    IrType SubstituteGenericTypes(IrType type, Dictionary<string, IrType> substitutions);

    /// <summary>
    /// Check if a type contains any generic type parameters
    /// </summary>
    bool ContainsGenericTypes(IrType type);

    /// <summary>
    /// Check if two types are semantically equal
    /// </summary>
    bool TypesAreEqual(IrType a, IrType b);

    /// <summary>
    /// Get a cache key for a type (used for monomorphization caching)
    /// </summary>
    string GetTypeCacheKey(IrType type);
}
