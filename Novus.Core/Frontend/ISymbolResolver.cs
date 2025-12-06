using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Interface for resolving symbols (names to their definitions).
/// This represents the first phase of IR building: determining what each name refers to.
/// </summary>
public interface ISymbolResolver
{
    /// <summary>
    /// Resolve a type name to its IrType definition.
    /// </summary>
    IrType? ResolveType(string name);

    /// <summary>
    /// Resolve a function name to its symbol.
    /// </summary>
    FunctionSymbol? ResolveFunction(string name);

    /// <summary>
    /// Resolve a variable name to its symbol in the current scope.
    /// </summary>
    VariableSymbol? ResolveVariable(string name);

    /// <summary>
    /// Resolve a struct type by name.
    /// </summary>
    IrStructType? ResolveStruct(string name);

    /// <summary>
    /// Resolve an enum type by name.
    /// </summary>
    IrEnumType? ResolveEnum(string name);

    /// <summary>
    /// Resolve a trait by name.
    /// </summary>
    IrTrait? ResolveTrait(string name);

    /// <summary>
    /// Resolve a constant by name.
    /// </summary>
    ConstantSymbol? ResolveConstant(string name);

    /// <summary>
    /// Check if a name is visible in the current scope.
    /// </summary>
    bool IsNameVisible(string name);
}

/// <summary>
/// Implementation that delegates to SymbolTable for symbol resolution.
/// This can be used by IrBuilder to centralize all name lookups.
/// </summary>
public class SymbolTableResolver : ISymbolResolver
{
    private readonly SymbolTable _symbols;
    private readonly Func<string, VariableSymbol?>? _localVariableLookup;

    public SymbolTableResolver(SymbolTable symbols, Func<string, VariableSymbol?>? localVariableLookup = null)
    {
        _symbols = symbols;
        _localVariableLookup = localVariableLookup;
    }

    public IrType? ResolveType(string name)
    {
        // Try struct first
        var structType = _symbols.LookupStruct(name);
        if (structType != null) return structType;

        // Then enum
        var enumType = _symbols.LookupEnum(name);
        if (enumType != null) return enumType;

        // Then builtin types (i32, bool, etc.)
        return ResolveBuiltinType(name);
    }

    public FunctionSymbol? ResolveFunction(string name)
    {
        return _symbols.LookupFunction(name);
    }

    public VariableSymbol? ResolveVariable(string name)
    {
        // First check local variables
        var local = _localVariableLookup?.Invoke(name);
        if (local != null) return local;

        // Then check global variables via SymbolTable's lookup
        return _symbols.LookupVariable(name);
    }

    public IrStructType? ResolveStruct(string name)
    {
        return _symbols.LookupStruct(name);
    }

    public IrEnumType? ResolveEnum(string name)
    {
        return _symbols.LookupEnum(name);
    }

    public IrTrait? ResolveTrait(string name)
    {
        return _symbols.LookupTrait(name);
    }

    public ConstantSymbol? ResolveConstant(string name)
    {
        return _symbols.LookupConstant(name);
    }

    public bool IsNameVisible(string name)
    {
        return ResolveType(name) != null ||
               ResolveFunction(name) != null ||
               ResolveVariable(name) != null ||
               ResolveConstant(name) != null;
    }

    private static IrType? ResolveBuiltinType(string name)
    {
        return name switch
        {
            "i8" => IrIntType.I8,
            "i16" => IrIntType.I16,
            "i32" => IrIntType.I32,
            "i64" => IrIntType.I64,
            "u8" => IrIntType.U8,
            "u16" => IrIntType.U16,
            "u32" => IrIntType.U32,
            "u64" => IrIntType.U64,
            "bool" => IrBoolType.Instance,
            "f32" => IrFloatType.F32,
            "f64" => IrFloatType.F64,
            "fixed16" => IrFixedType.Fixed16,
            "fixed32" => IrFixedType.Fixed32,
            "()" or "unit" => IrTupleType.Unit,
            _ => null
        };
    }
}
