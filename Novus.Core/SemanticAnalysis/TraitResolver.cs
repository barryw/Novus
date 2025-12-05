using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Resolves trait implementations and validates generic constraints.
/// Extracted from SemanticAnalyzer to improve code organization.
///
/// Responsibilities:
/// - Track trait implementations (impl Trait for Type)
/// - Check if a type implements a specific trait
/// - Validate generic constraints (where clauses)
/// - Support From trait conversions
/// </summary>
public class TraitResolver
{
    private readonly Dictionary<string, TraitImplInfo> _traitImpls = new();
    private readonly SymbolTable _symbols;

    /// <summary>
    /// Delegate to get cache key for a type (for complex type comparisons).
    /// </summary>
    public Func<IrType, string>? GetTypeCacheKeyFn { get; set; }

    public TraitResolver(SymbolTable symbols)
    {
        _symbols = symbols;
    }

    /// <summary>
    /// Registers a trait implementation.
    /// </summary>
    public void RegisterTraitImpl(
        string typeName,
        string traitName,
        List<IrType> traitTypeArgs,
        List<string> implGenericParams,
        SourceLocation location)
    {
        // Build the lookup key
        var traitArgsStr = traitTypeArgs.Count > 0
            ? $"<{string.Join(",", traitTypeArgs.Select(t => GetTypeCacheKey(t)))}>"
            : "";
        var implKey = $"{typeName}::{traitName}{traitArgsStr}";

        _traitImpls[implKey] = new TraitImplInfo(
            typeName,
            traitName,
            traitTypeArgs,
            implGenericParams,
            location
        );
    }

    /// <summary>
    /// Check if a type implements a specific trait.
    /// </summary>
    public bool TypeImplementsTrait(IrType type, string traitName, List<IrType> traitTypeArgs)
    {
        // Validate that the trait exists
        if (!_symbols.HasTrait(traitName))
        {
            return false;
        }

        // Extract the base type name from the IR type
        string typeName = GetBaseTypeName(type);

        // Build the lookup key for this specific trait impl
        var traitArgsStr = traitTypeArgs.Count > 0
            ? $"<{string.Join(",", traitTypeArgs.Select(t => GetTypeCacheKey(t)))}>"
            : "";
        var implKey = $"{typeName}::{traitName}{traitArgsStr}";

        // Check if we have an exact match for this trait impl
        if (_traitImpls.ContainsKey(implKey))
        {
            return true;
        }

        // For generic impls, we need to check if there's a generic impl that could satisfy this
        foreach (var kvp in _traitImpls)
        {
            var implInfo = kvp.Value;

            // Check if this is the right trait
            if (implInfo.TraitName != traitName)
                continue;

            // Check if the type names match
            if (implInfo.TypeName != typeName)
                continue;

            // If the impl has generic parameters, we need to check if the trait type args
            // can be unified with the constraint's trait type args
            if (implInfo.ImplGenericParams.Count > 0)
            {
                // Generic impl exists - assume it can be monomorphized
                return true;
            }

            // Check if trait type arguments match exactly
            if (TraitTypeArgsMatch(implInfo.TraitTypeArgs, traitTypeArgs))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a type satisfies all trait bounds.
    /// </summary>
    public bool TypeSatisfiesBounds(IrType type, List<IrTraitBound> bounds, DiagnosticBag? diagnostics, SourceLocation? location)
    {
        foreach (var bound in bounds)
        {
            if (!TypeImplementsTrait(type, bound.TraitName, bound.TraitTypeArgs))
            {
                diagnostics?.ReportError(
                    "E0277",
                    $"the trait bound `{bound.TraitName}` is not satisfied for type `{type.Name}`",
                    location ?? new SourceLocation("", 0, 0, 0, "")
                );
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Validate generic constraints when monomorphizing a type.
    /// </summary>
    public bool ValidateGenericConstraints(
        IrWhereClause? whereClause,
        List<string> genericParams,
        List<IrType> typeArgs,
        DiagnosticBag? diagnostics,
        SourceLocation location)
    {
        if (whereClause == null || whereClause.Constraints.Count == 0)
            return true;

        // Build substitution map from generic parameters to concrete types
        var substitutions = new Dictionary<string, IrType>();
        for (int i = 0; i < genericParams.Count; i++)
        {
            substitutions[genericParams[i]] = typeArgs[i];
        }

        // Check each constraint
        foreach (var constraint in whereClause.Constraints)
        {
            // Get the concrete type for this constrained parameter
            if (!substitutions.TryGetValue(constraint.TypeParameter, out var concreteType))
                continue;

            // Check if the concrete type satisfies all bounds
            if (!TypeSatisfiesBounds(concreteType, constraint.Bounds, diagnostics, location))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if a From trait implementation exists for the target type.
    /// </summary>
    public bool CanConvertViaFromTrait(IrType sourceType, IrType targetType)
    {
        var sourceTypeName = GetBaseTypeName(sourceType);
        var targetTypeName = GetBaseTypeName(targetType);

        // Check for From<SourceType> impl on targetType
        foreach (var (key, implInfo) in _traitImpls)
        {
            if (implInfo.TraitName != "From")
                continue;

            if (implInfo.TypeName != targetTypeName)
                continue;

            // Check if this is From<sourceType>
            if (implInfo.TraitTypeArgs.Count == 1)
            {
                var fromTypeName = GetBaseTypeName(implInfo.TraitTypeArgs[0]);
                if (fromTypeName == sourceTypeName)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if an Iterator impl exists for a type and get its Item type.
    /// </summary>
    public bool TryGetIteratorItemType(IrType type, out IrType? itemType)
    {
        itemType = null;
        var typeName = GetBaseTypeName(type);

        foreach (var kvp in _traitImpls)
        {
            var implInfo = kvp.Value;
            if (implInfo.TraitName != "Iterator")
                continue;

            if (implInfo.TypeName != typeName)
                continue;

            // Found Iterator impl - return the Item type (first trait type arg)
            if (implInfo.TraitTypeArgs.Count > 0)
            {
                itemType = implInfo.TraitTypeArgs[0];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if any impl exists for a type with a specific trait (ignoring type args).
    /// </summary>
    public bool HasTraitImpl(string typeName, string traitName)
    {
        foreach (var kvp in _traitImpls)
        {
            var implInfo = kvp.Value;
            if (implInfo.TraitName == traitName && implInfo.TypeName == typeName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get all trait impls registered (for debugging/introspection).
    /// </summary>
    public IReadOnlyDictionary<string, TraitImplInfo> GetAllImpls() => _traitImpls;

    /// <summary>
    /// Clear all registered trait impls (for testing or reset).
    /// </summary>
    public void Clear() => _traitImpls.Clear();

    #region Private Helpers

    private string GetTypeCacheKey(IrType type)
    {
        return GetTypeCacheKeyFn?.Invoke(type) ?? type.Name;
    }

    private string GetBaseTypeName(IrType type)
    {
        return type switch
        {
            IrStructType structType => structType.StructName,
            IrEnumType enumType => enumType.EnumName,
            IrPointerType ptrType => GetBaseTypeName(ptrType.PointeeType),
            IrArrayType arrayType => GetBaseTypeName(arrayType.ElementType),
            IrIntType intType => intType.IsSigned ? $"i{intType.BitWidth}" : $"u{intType.BitWidth}",
            IrBoolType => "bool",
            _ => type.Name
        };
    }

    private bool TraitTypeArgsMatch(List<IrType> args1, List<IrType> args2)
    {
        if (args1.Count != args2.Count)
            return false;

        for (int i = 0; i < args1.Count; i++)
        {
            if (GetTypeCacheKey(args1[i]) != GetTypeCacheKey(args2[i]))
                return false;
        }

        return true;
    }

    #endregion
}

/// <summary>
/// Information about a trait implementation.
/// </summary>
public record TraitImplInfo(
    string TypeName,              // The type implementing the trait
    string TraitName,             // Trait being implemented
    List<IrType> TraitTypeArgs,   // Type args for the trait
    List<string> ImplGenericParams, // Generic params on the impl block
    SourceLocation Location       // Where the impl was declared
);
