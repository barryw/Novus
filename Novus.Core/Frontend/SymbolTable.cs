using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Unified symbol table manager for tracking types, functions, variables, and constants
/// across compilation phases. Supports hierarchical scopes with parent/child relationships.
/// </summary>
/// <remarks>
/// This symbol table serves dual purposes:
/// 1. IrBuilder: Tracks IR types and values during code generation
/// 2. SemanticAnalyzer: Tracks symbols with location/attribute metadata during type checking
///
/// The table is designed to be flexible enough for both use cases while avoiding duplication.
/// Scoping rules: child scopes shadow parent scope symbols of the same name.
/// </remarks>
public class SymbolTable
{
    private readonly SymbolTable? _parent;

    // Type definitions
    private readonly Dictionary<string, IrStructType> _structs = new();
    private readonly Dictionary<string, IrEnumType> _enums = new();
    private readonly Dictionary<string, IrTrait> _traits = new();

    // Functions and variables
    private readonly Dictionary<string, FunctionSymbol> _functions = new();
    private readonly Dictionary<string, VariableSymbol> _localVariables = new();
    private readonly Dictionary<string, VariableSymbol> _globalVariables = new();

    // Constants
    private readonly Dictionary<string, ConstantSymbol> _constants = new();

    // Generic type parameters (for template definitions)
    private readonly Dictionary<string, IrGenericType> _genericParams = new();

    // Monomorphization caches (shared across all scopes via root table)
    private readonly Dictionary<string, IrEnumType> _monomorphizedEnums = new();
    private readonly Dictionary<string, IrStructType> _monomorphizedStructs = new();
    private readonly Dictionary<string, FunctionSymbol> _monomorphizedFunctions = new();

    // Generic templates (for later instantiation)
    // Key format: "TypeName::methodName" or "functionName"
    private readonly Dictionary<string, GenericTemplate> _genericTemplates = new();

    // Track which generic instances have been created
    private readonly HashSet<string> _instantiatedGenerics = new();

    // Location tracking (for LSP support)
    private readonly Dictionary<string, SourceLocation> _structLocations = new();
    private readonly Dictionary<string, SourceLocation> _enumLocations = new();
    private readonly Dictionary<string, SourceLocation> _traitLocations = new();

    // Documentation comments (for LSP hover)
    private readonly Dictionary<string, string> _docComments = new();

    // Trait implementations: key = "TypeName::TraitName<TypeArg1,TypeArg2,...>"
    private readonly Dictionary<string, TraitImplInfo> _traitImpls = new();

    // Imported names: maps imported name -> module name (for semantic analysis)
    private readonly Dictionary<string, string> _importedNames = new();

    // Re-exported symbols: maps symbol name -> source module path
    // When a module does `pub use std::core::Option`, this tracks that Option is re-exported
    private readonly Dictionary<string, string> _reexportedSymbols = new();

    /// <summary>
    /// Creates a new root-level symbol table
    /// </summary>
    public SymbolTable()
    {
        _parent = null;
    }

    /// <summary>
    /// Creates a child symbol table with a parent scope
    /// </summary>
    private SymbolTable(SymbolTable parent)
    {
        _parent = parent;
    }

    /// <summary>
    /// Creates a new child scope that inherits from this scope
    /// </summary>
    public SymbolTable CreateChildScope()
    {
        return new SymbolTable(this);
    }

    /// <summary>
    /// Gets the root symbol table (walks up the parent chain)
    /// </summary>
    private SymbolTable GetRoot()
    {
        var current = this;
        while (current._parent != null)
            current = current._parent;
        return current;
    }

    // ============================================================================
    // STRUCT REGISTRATION AND LOOKUP
    // ============================================================================

    /// <summary>
    /// Registers a struct type in the current scope
    /// </summary>
    public void RegisterStruct(string name, IrStructType type, SourceLocation? location = null)
    {
        _structs[name] = type;
        if (location != null)
            _structLocations[name] = location;
    }

    /// <summary>
    /// Looks up a struct type, checking parent scopes if not found locally
    /// </summary>
    public IrStructType? LookupStruct(string name)
    {
        if (_structs.TryGetValue(name, out var type))
            return type;
        return _parent?.LookupStruct(name);
    }

    /// <summary>
    /// Checks if a struct is defined in this scope or any parent scope
    /// </summary>
    public bool HasStruct(string name)
    {
        return LookupStruct(name) != null;
    }

    /// <summary>
    /// Gets the location where a struct was defined (for LSP)
    /// </summary>
    public SourceLocation? GetStructLocation(string name)
    {
        if (_structLocations.TryGetValue(name, out var location))
            return location;
        return _parent?.GetStructLocation(name);
    }

    /// <summary>
    /// Gets all structs defined in this scope (not including parent scopes)
    /// </summary>
    public IReadOnlyDictionary<string, IrStructType> GetLocalStructs() => _structs;

    // ============================================================================
    // ENUM REGISTRATION AND LOOKUP
    // ============================================================================

    /// <summary>
    /// Registers an enum type in the current scope
    /// </summary>
    public void RegisterEnum(string name, IrEnumType type, SourceLocation? location = null)
    {
        _enums[name] = type;
        if (location != null)
            _enumLocations[name] = location;
    }

    /// <summary>
    /// Looks up an enum type, checking parent scopes if not found locally
    /// </summary>
    public IrEnumType? LookupEnum(string name)
    {
        if (_enums.TryGetValue(name, out var type))
            return type;
        return _parent?.LookupEnum(name);
    }

    /// <summary>
    /// Checks if an enum is defined in this scope or any parent scope
    /// </summary>
    public bool HasEnum(string name)
    {
        return LookupEnum(name) != null;
    }

    /// <summary>
    /// Gets the location where an enum was defined (for LSP)
    /// </summary>
    public SourceLocation? GetEnumLocation(string name)
    {
        if (_enumLocations.TryGetValue(name, out var location))
            return location;
        return _parent?.GetEnumLocation(name);
    }

    /// <summary>
    /// Gets all enums defined in this scope (not including parent scopes)
    /// </summary>
    public IReadOnlyDictionary<string, IrEnumType> GetLocalEnums() => _enums;

    // ============================================================================
    // TRAIT REGISTRATION AND LOOKUP
    // ============================================================================

    /// <summary>
    /// Registers a trait in the current scope
    /// </summary>
    public void RegisterTrait(string name, IrTrait trait, SourceLocation? location = null)
    {
        _traits[name] = trait;
        if (location != null)
            _traitLocations[name] = location;
    }

    /// <summary>
    /// Looks up a trait, checking parent scopes if not found locally
    /// </summary>
    public IrTrait? LookupTrait(string name)
    {
        if (_traits.TryGetValue(name, out var trait))
            return trait;
        return _parent?.LookupTrait(name);
    }

    /// <summary>
    /// Checks if a trait is defined in this scope or any parent scope
    /// </summary>
    public bool HasTrait(string name)
    {
        return LookupTrait(name) != null;
    }

    /// <summary>
    /// Gets the location where a trait was defined (for LSP)
    /// </summary>
    public SourceLocation? GetTraitLocation(string name)
    {
        if (_traitLocations.TryGetValue(name, out var location))
            return location;
        return _parent?.GetTraitLocation(name);
    }

    /// <summary>
    /// Gets all traits defined in this scope (not including parent scopes)
    /// </summary>
    public IReadOnlyDictionary<string, IrTrait> GetLocalTraits() => _traits;

    // ============================================================================
    // CONSTANT REGISTRATION AND LOOKUP
    // ============================================================================

    /// <summary>
    /// Registers a constant in the current scope
    /// </summary>
    public void RegisterConstant(string name, ConstantSymbol constant)
    {
        _constants[name] = constant;
    }

    /// <summary>
    /// Registers a constant with type and value (convenience overload for IrBuilder)
    /// </summary>
    public void RegisterConstant(string name, IrType type, object value)
    {
        // Create a minimal ConstantSymbol without location info (for IrBuilder use case)
        var location = new SourceLocation("", 0, 0, 0, "");
        _constants[name] = new ConstantSymbol(name, type, value, location, null);
    }

    /// <summary>
    /// Looks up a constant, checking parent scopes if not found locally
    /// </summary>
    public ConstantSymbol? LookupConstant(string name)
    {
        if (_constants.TryGetValue(name, out var constant))
            return constant;
        return _parent?.LookupConstant(name);
    }

    /// <summary>
    /// Checks if a constant is defined in this scope or any parent scope
    /// </summary>
    public bool HasConstant(string name)
    {
        return LookupConstant(name) != null;
    }

    /// <summary>
    /// Gets all constants defined in this scope (not including parent scopes)
    /// </summary>
    public IReadOnlyDictionary<string, ConstantSymbol> GetLocalConstants() => _constants;

    // ============================================================================
    // FUNCTION REGISTRATION AND LOOKUP
    // ============================================================================

    /// <summary>
    /// Registers a function in the current scope
    /// </summary>
    public void RegisterFunction(string name, FunctionSymbol function)
    {
        _functions[name] = function;
    }

    /// <summary>
    /// Looks up a function, checking parent scopes if not found locally
    /// </summary>
    public FunctionSymbol? LookupFunction(string name)
    {
        if (_functions.TryGetValue(name, out var function))
            return function;
        return _parent?.LookupFunction(name);
    }

    /// <summary>
    /// Checks if a function is defined in this scope or any parent scope
    /// </summary>
    public bool HasFunction(string name)
    {
        return LookupFunction(name) != null;
    }

    /// <summary>
    /// Gets all functions defined in this scope (not including parent scopes)
    /// </summary>
    public IReadOnlyDictionary<string, FunctionSymbol> GetLocalFunctions() => _functions;

    // ============================================================================
    // VARIABLE REGISTRATION AND LOOKUP
    // ============================================================================

    /// <summary>
    /// Registers a local variable in the current scope
    /// </summary>
    public void RegisterLocalVariable(string name, VariableSymbol variable)
    {
        _localVariables[name] = variable;
    }

    /// <summary>
    /// Registers a global/extern variable (only at root scope)
    /// </summary>
    public void RegisterGlobalVariable(string name, VariableSymbol variable)
    {
        GetRoot()._globalVariables[name] = variable;
    }

    /// <summary>
    /// Looks up a local variable, checking parent scopes if not found locally
    /// </summary>
    public VariableSymbol? LookupLocalVariable(string name)
    {
        if (_localVariables.TryGetValue(name, out var variable))
            return variable;
        return _parent?.LookupLocalVariable(name);
    }

    /// <summary>
    /// Looks up a global variable (always checks root scope)
    /// </summary>
    public VariableSymbol? LookupGlobalVariable(string name)
    {
        return GetRoot()._globalVariables.TryGetValue(name, out var variable) ? variable : null;
    }

    /// <summary>
    /// Looks up a variable (checks locals first, then globals)
    /// </summary>
    public VariableSymbol? LookupVariable(string name)
    {
        return LookupLocalVariable(name) ?? LookupGlobalVariable(name);
    }

    /// <summary>
    /// Gets all local variables defined in this scope (not including parent scopes)
    /// </summary>
    public IReadOnlyDictionary<string, VariableSymbol> GetLocalVariables() => _localVariables;

    /// <summary>
    /// Gets all global variables (from root scope)
    /// </summary>
    public IReadOnlyDictionary<string, VariableSymbol> GetGlobalVariables() => GetRoot()._globalVariables;

    // ============================================================================
    // GENERIC TYPE PARAMETERS
    // ============================================================================

    /// <summary>
    /// Registers a generic type parameter in the current scope
    /// </summary>
    public void RegisterGenericParameter(string name, IrGenericType genericType)
    {
        _genericParams[name] = genericType;
    }

    /// <summary>
    /// Looks up a generic type parameter, checking parent scopes if not found locally
    /// </summary>
    public IrGenericType? LookupGenericParameter(string name)
    {
        if (_genericParams.TryGetValue(name, out var type))
            return type;
        return _parent?.LookupGenericParameter(name);
    }

    /// <summary>
    /// Checks if a generic parameter is defined in this scope or any parent scope
    /// </summary>
    public bool HasGenericParameter(string name)
    {
        return LookupGenericParameter(name) != null;
    }

    /// <summary>
    /// Clears all generic parameters in the current scope
    /// </summary>
    public void ClearGenericParameters()
    {
        _genericParams.Clear();
    }

    // ============================================================================
    // MONOMORPHIZATION CACHES (always use root scope)
    // ============================================================================

    /// <summary>
    /// Registers a monomorphized enum (cached at root level)
    /// </summary>
    public void RegisterMonomorphizedEnum(string key, IrEnumType type)
    {
        GetRoot()._monomorphizedEnums[key] = type;
    }

    /// <summary>
    /// Looks up a monomorphized enum from the cache
    /// </summary>
    public IrEnumType? LookupMonomorphizedEnum(string key)
    {
        return GetRoot()._monomorphizedEnums.TryGetValue(key, out var type) ? type : null;
    }

    /// <summary>
    /// Registers a monomorphized struct (cached at root level)
    /// </summary>
    public void RegisterMonomorphizedStruct(string key, IrStructType type)
    {
        GetRoot()._monomorphizedStructs[key] = type;
    }

    /// <summary>
    /// Looks up a monomorphized struct from the cache
    /// </summary>
    public IrStructType? LookupMonomorphizedStruct(string key)
    {
        return GetRoot()._monomorphizedStructs.TryGetValue(key, out var type) ? type : null;
    }

    /// <summary>
    /// Registers a monomorphized function (cached at root level)
    /// </summary>
    public void RegisterMonomorphizedFunction(string key, FunctionSymbol function)
    {
        GetRoot()._monomorphizedFunctions[key] = function;
    }

    /// <summary>
    /// Looks up a monomorphized function from the cache
    /// </summary>
    public FunctionSymbol? LookupMonomorphizedFunction(string key)
    {
        return GetRoot()._monomorphizedFunctions.TryGetValue(key, out var function) ? function : null;
    }

    // ============================================================================
    // GENERIC TEMPLATES
    // ============================================================================

    /// <summary>
    /// Registers a generic template (method or function)
    /// </summary>
    public void RegisterGenericTemplate(string key, GenericTemplate template)
    {
        GetRoot()._genericTemplates[key] = template;
    }

    /// <summary>
    /// Looks up a generic template
    /// </summary>
    public GenericTemplate? LookupGenericTemplate(string key)
    {
        return GetRoot()._genericTemplates.TryGetValue(key, out var template) ? template : null;
    }

    /// <summary>
    /// Marks a generic instance as instantiated
    /// </summary>
    public void MarkGenericInstantiated(string key)
    {
        GetRoot()._instantiatedGenerics.Add(key);
    }

    /// <summary>
    /// Checks if a generic instance has been instantiated
    /// </summary>
    public bool IsGenericInstantiated(string key)
    {
        return GetRoot()._instantiatedGenerics.Contains(key);
    }

    // ============================================================================
    // TRAIT IMPLEMENTATIONS
    // ============================================================================

    /// <summary>
    /// Registers a trait implementation
    /// </summary>
    public void RegisterTraitImpl(string key, TraitImplInfo impl)
    {
        GetRoot()._traitImpls[key] = impl;
    }

    /// <summary>
    /// Looks up a trait implementation
    /// </summary>
    public TraitImplInfo? LookupTraitImpl(string key)
    {
        return GetRoot()._traitImpls.TryGetValue(key, out var impl) ? impl : null;
    }

    /// <summary>
    /// Gets all trait implementations
    /// </summary>
    public IReadOnlyDictionary<string, TraitImplInfo> GetTraitImpls() => GetRoot()._traitImpls;

    // ============================================================================
    // DOCUMENTATION AND METADATA
    // ============================================================================

    /// <summary>
    /// Associates documentation with a symbol
    /// </summary>
    public void SetDocComment(string symbolName, string docComment)
    {
        _docComments[symbolName] = docComment;
    }

    /// <summary>
    /// Gets documentation for a symbol
    /// </summary>
    public string? GetDocComment(string symbolName)
    {
        if (_docComments.TryGetValue(symbolName, out var doc))
            return doc;
        return _parent?.GetDocComment(symbolName);
    }

    /// <summary>
    /// Gets all documentation comments in this scope
    /// </summary>
    public IReadOnlyDictionary<string, string> GetLocalDocComments() => _docComments;

    // ============================================================================
    // IMPORT TRACKING (for semantic analysis)
    // ============================================================================

    /// <summary>
    /// Records that a name was imported from a module
    /// </summary>
    public void RegisterImportedName(string importedName, string moduleName)
    {
        _importedNames[importedName] = moduleName;
    }

    /// <summary>
    /// Gets the module a name was imported from
    /// </summary>
    public string? GetImportSource(string importedName)
    {
        if (_importedNames.TryGetValue(importedName, out var moduleName))
            return moduleName;
        return _parent?.GetImportSource(importedName);
    }

    // ============================================================================
    // RE-EXPORT TRACKING (for pub use)
    // ============================================================================

    /// <summary>
    /// Records that a symbol is re-exported from this module
    /// </summary>
    public void RegisterReexport(string symbolName, string sourceModule)
    {
        _reexportedSymbols[symbolName] = sourceModule;
    }

    /// <summary>
    /// Checks if a symbol is re-exported from this module
    /// </summary>
    public bool IsReexported(string symbolName)
    {
        return _reexportedSymbols.ContainsKey(symbolName);
    }

    /// <summary>
    /// Gets the source module for a re-exported symbol
    /// </summary>
    public string? GetReexportSource(string symbolName)
    {
        return _reexportedSymbols.TryGetValue(symbolName, out var source) ? source : null;
    }

    /// <summary>
    /// Gets all re-exported symbols from this module
    /// </summary>
    public IReadOnlyDictionary<string, string> GetReexportedSymbols()
    {
        return _reexportedSymbols;
    }

    // ============================================================================
    // SCOPE MANAGEMENT
    // ============================================================================

    /// <summary>
    /// Clears all local symbols in the current scope (does not affect parent scopes)
    /// </summary>
    public void ClearLocalScope()
    {
        _structs.Clear();
        _enums.Clear();
        _traits.Clear();
        _functions.Clear();
        _localVariables.Clear();
        _constants.Clear();
        _genericParams.Clear();
        _structLocations.Clear();
        _enumLocations.Clear();
        _traitLocations.Clear();
        _docComments.Clear();
        _importedNames.Clear();
        // Note: monomorphization caches and trait impls are preserved (they're global)
    }

    /// <summary>
    /// Clears all symbols including global caches (for root scope only)
    /// </summary>
    public void ClearAll()
    {
        ClearLocalScope();
        _globalVariables.Clear();
        _monomorphizedEnums.Clear();
        _monomorphizedStructs.Clear();
        _monomorphizedFunctions.Clear();
        _genericTemplates.Clear();
        _instantiatedGenerics.Clear();
        _traitImpls.Clear();
    }
}

/// <summary>
/// Represents a generic template (method or function) for later instantiation
/// </summary>
public record GenericTemplate(
    List<string> GenericParameters,
    object Context,  // NovusParser.FunctionDeclarationContext (stored as object to avoid parser dependency)
    Dictionary<string, ConstantSymbol> Constants  // Constants visible when template was created
);

/// <summary>
/// Information about a trait implementation (for constraint checking)
/// </summary>
public record TraitImplInfo(
    string TypeName,              // The type implementing the trait (e.g., "Vec", "Counter")
    string TraitName,             // Trait being implemented (e.g., "Iterator")
    List<IrType> TraitTypeArgs,   // Type args for the trait (e.g., [i32] for Iterator<i32>)
    List<string> ImplGenericParams, // Generic params on the impl block itself
    SourceLocation Location       // Where the impl was declared
);
