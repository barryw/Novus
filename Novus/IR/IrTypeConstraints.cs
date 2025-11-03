namespace Novus.IR;

/// <summary>
/// Represents a trait bound on a generic type parameter
/// Example: T: Sortable means type T must implement the Sortable trait
/// </summary>
public class IrTraitBound
{
    public string TraitName { get; }
    public List<IrType> TraitTypeArgs { get; }  // Type arguments for the trait (e.g., Iterator<T>)

    public IrTraitBound(string traitName, List<IrType>? traitTypeArgs = null)
    {
        TraitName = traitName;
        TraitTypeArgs = traitTypeArgs ?? new List<IrType>();
    }

    public override string ToString()
    {
        if (TraitTypeArgs.Count > 0)
            return $"{TraitName}<{string.Join(", ", TraitTypeArgs.Select(t => t.Name))}>";
        return TraitName;
    }
}

/// <summary>
/// Represents a complete constraint on a generic type parameter
/// Example: T: Sortable + Display means T must implement both Sortable and Display
/// </summary>
public class IrTypeConstraint
{
    public string TypeParameter { get; }  // The generic type parameter name (e.g., "T")
    public List<IrTraitBound> Bounds { get; }  // List of traits this type must implement

    public IrTypeConstraint(string typeParameter, List<IrTraitBound> bounds)
    {
        TypeParameter = typeParameter;
        Bounds = bounds;
    }

    public override string ToString()
    {
        return $"{TypeParameter}: {string.Join(" + ", Bounds)}";
    }
}

/// <summary>
/// Container for where clause constraints
/// Stores all constraints for a generic declaration (function, struct, impl, etc.)
/// </summary>
public class IrWhereClause
{
    public List<IrTypeConstraint> Constraints { get; }

    public IrWhereClause(List<IrTypeConstraint> constraints)
    {
        Constraints = constraints;
    }

    public IrWhereClause()
    {
        Constraints = new List<IrTypeConstraint>();
    }

    /// <summary>
    /// Get all trait bounds for a specific type parameter
    /// </summary>
    public List<IrTraitBound> GetBoundsFor(string typeParameter)
    {
        var constraint = Constraints.FirstOrDefault(c => c.TypeParameter == typeParameter);
        return constraint?.Bounds ?? new List<IrTraitBound>();
    }

    /// <summary>
    /// Check if a type parameter has any constraints
    /// </summary>
    public bool HasConstraints(string typeParameter)
    {
        return Constraints.Any(c => c.TypeParameter == typeParameter);
    }

    public override string ToString()
    {
        if (Constraints.Count == 0)
            return "";
        return $"where {string.Join(", ", Constraints)}";
    }
}
