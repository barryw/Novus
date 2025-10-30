namespace Novus.IR;

/// <summary>
/// Represents an unresolved generic type that will be inferred from usage
/// Example: let vec = Vec::new() creates Vec<UnresolvedGeneric>
/// When vec.push(42i32) is called, we resolve it to Vec<i32>
/// </summary>
public class IrUnresolvedGenericType : IrType
{
    private static int _counter = 0;

    public int Id { get; }
    public IrType? ResolvedType { get; set; } // Null until resolved from usage

    public IrUnresolvedGenericType()
    {
        Id = _counter++;
    }

    public override string Name => ResolvedType?.Name ?? $"<unresolved{Id}>";

    public override int SizeInBytes => ResolvedType?.SizeInBytes ?? 4; // Default to pointer size
}

/// <summary>
/// Represents a generic type with unresolved type parameters
/// Example: Vec<UnresolvedGeneric> before we know what T is
/// </summary>
public class IrPartiallyResolvedGenericType : IrType
{
    public string GenericTypeName { get; set; } // "Vec", "Option", etc.
    public List<IrType> TypeArguments { get; set; } // May contain IrUnresolvedGenericType instances
    public IrStructType? FullyResolvedType { get; set; } // Null until all type args are resolved

    public IrPartiallyResolvedGenericType(string genericTypeName, List<IrType> typeArguments)
    {
        GenericTypeName = genericTypeName;
        TypeArguments = typeArguments;
    }

    public override string Name
    {
        get
        {
            if (FullyResolvedType != null)
                return FullyResolvedType.Name;

            var args = string.Join(", ", TypeArguments.Select(t => t.Name));
            return $"{GenericTypeName}<{args}>";
        }
    }

    public override int SizeInBytes => FullyResolvedType?.SizeInBytes ?? 12; // Default struct size

    /// <summary>
    /// Check if all type arguments are resolved
    /// </summary>
    public bool IsFullyResolved()
    {
        foreach (var arg in TypeArguments)
        {
            if (arg is IrUnresolvedGenericType unresolvedArg && unresolvedArg.ResolvedType == null)
                return false;
            if (arg is IrPartiallyResolvedGenericType partialArg && !partialArg.IsFullyResolved())
                return false;
        }
        return true;
    }

    /// <summary>
    /// Get the fully resolved type arguments (following resolution chains)
    /// </summary>
    public List<IrType> GetResolvedTypeArguments()
    {
        var resolvedArgs = new List<IrType>();
        foreach (var arg in TypeArguments)
        {
            if (arg is IrUnresolvedGenericType unresolvedArg && unresolvedArg.ResolvedType != null)
                resolvedArgs.Add(unresolvedArg.ResolvedType);
            else if (arg is IrPartiallyResolvedGenericType partialArg && partialArg.IsFullyResolved())
                resolvedArgs.Add(partialArg.FullyResolvedType!);
            else
                resolvedArgs.Add(arg);
        }
        return resolvedArgs;
    }
}
