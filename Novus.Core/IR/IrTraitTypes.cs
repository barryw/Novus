namespace Novus.IR;

/// <summary>
/// Trait type - defines a set of methods that types must implement
/// Similar to Rust traits - zero-cost abstraction through monomorphization
/// </summary>
public class IrTrait
{
    public string TraitName { get; }
    public List<IrTraitMethod> Methods { get; }
    public List<string> GenericParameters { get; }  // Type parameter names (e.g., ["T"])
    public Visibility Visibility { get; set; }
    public Novus.SemanticAnalysis.AttributeCollection? Attributes { get; set; }  // Trait attributes

    public IrTrait(string traitName, List<IrTraitMethod> methods, List<string>? genericParams = null,
        Visibility visibility = Visibility.Private,
        Novus.SemanticAnalysis.AttributeCollection? attributes = null)
    {
        TraitName = traitName;
        Methods = methods;
        GenericParameters = genericParams ?? new List<string>();
        Visibility = visibility;
        Attributes = attributes;
    }

    public IrTraitMethod? GetMethod(string methodName)
    {
        return Methods.FirstOrDefault(m => m.Name == methodName);
    }

    public string FullName
    {
        get
        {
            if (GenericParameters.Count > 0)
                return $"{TraitName}<{string.Join(", ", GenericParameters)}>";
            return TraitName;
        }
    }
}

/// <summary>
/// Represents a method signature within a trait definition
/// May optionally have a default implementation
/// </summary>
public class IrTraitMethod
{
    public string Name { get; set; }
    public List<IrParameter> Parameters { get; }
    public IrType ReturnType { get; set; }
    public List<string> GenericParameters { get; }  // Method-level generic parameters

    /// <summary>
    /// The default implementation function, if one was provided in the trait definition.
    /// Null means this method must be implemented by any type implementing the trait.
    /// </summary>
    public IrFunction? DefaultImplementation { get; set; }

    /// <summary>
    /// The AST context for the default implementation body (for deferred parsing).
    /// Stored during initial parsing, processed during semantic analysis when needed.
    /// </summary>
    public object? DefaultBodyContext { get; set; }

    /// <summary>
    /// Indicates whether this trait method has a default implementation.
    /// If true, implementing types can skip implementing this method.
    /// </summary>
    public bool HasDefaultImplementation => DefaultBodyContext != null || DefaultImplementation != null;

    public IrTraitMethod(string name, List<IrParameter> parameters, IrType returnType, List<string>? genericParams = null)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
        GenericParameters = genericParams ?? new List<string>();
        DefaultImplementation = null;
        DefaultBodyContext = null;
    }

    public string Signature
    {
        get
        {
            var paramStr = string.Join(", ", Parameters.Select(p => $"{p.Name}: {p.Type.Name}"));
            var genericStr = GenericParameters.Count > 0
                ? $"<{string.Join(", ", GenericParameters)}>"
                : "";
            var retStr = ReturnType is IrVoidType ? "" : $" -> {ReturnType.Name}";
            return $"fn {Name}{genericStr}({paramStr}){retStr}";
        }
    }
}

/// <summary>
/// Represents a trait implementation for a specific type
/// Maps a trait to a type and provides implementations for all trait methods
/// </summary>
public class IrTraitImpl
{
    public string TraitName { get; set; }  // Name of the trait being implemented
    public List<IrType> TraitTypeArgs { get; set; }  // Type arguments for the trait (if generic trait)
    public string TypeName { get; set; }  // Name of the type implementing the trait
    public IrType ImplementingType { get; set; }  // The actual type
    public List<string> GenericParameters { get; }  // Generic parameters on the impl block (e.g., impl<T> Iterator<T> for Vec<T>)
    public List<IrFunction> Methods { get; }  // Implementations of trait methods
    public Visibility Visibility { get; set; }
    public Novus.SemanticAnalysis.AttributeCollection? Attributes { get; set; }
    public IrWhereClause? WhereClause { get; set; }  // Generic type constraints (e.g., where T: Sortable)

    public IrTraitImpl(
        string traitName,
        List<IrType> traitTypeArgs,
        string typeName,
        IrType implementingType,
        List<string>? genericParams = null,
        Visibility visibility = Visibility.Private,
        Novus.SemanticAnalysis.AttributeCollection? attributes = null,
        IrWhereClause? whereClause = null)
    {
        TraitName = traitName;
        TraitTypeArgs = traitTypeArgs;
        TypeName = typeName;
        ImplementingType = implementingType;
        GenericParameters = genericParams ?? new List<string>();
        Methods = new List<IrFunction>();
        Visibility = visibility;
        Attributes = attributes;
        WhereClause = whereClause;
    }

    /// <summary>
    /// Get the mangled name for this trait implementation
    /// Used to generate unique function names for trait methods
    /// Format: TypeName_TraitName_MethodName (e.g., Counter_Iterator_next)
    /// </summary>
    public string GetMangledMethodName(string methodName)
    {
        // If TraitName already contains generic parameters (e.g., "From<DosError>"),
        // don't append TraitTypeArgs again, since they're already in TraitName.
        // This prevents double-mangling like "NovusError_From<DosError>_DosError_convert"
        var traitArgs = "";
        if (TraitTypeArgs.Count > 0 && !TraitName.Contains("<"))
        {
            // Only append type args if they're not already in the trait name
            traitArgs = "_" + string.Join("_", TraitTypeArgs.Select(t => t.Name.Replace("<", "_").Replace(">", "_").Replace("::", "_")));
        }
        return $"{TypeName}_{TraitName}{traitArgs}_{methodName}";
    }

    public string FullName
    {
        get
        {
            var traitTypeArgsStr = TraitTypeArgs.Count > 0
                ? $"<{string.Join(", ", TraitTypeArgs.Select(t => t.Name))}>"
                : "";
            return $"impl {TraitName}{traitTypeArgsStr} for {TypeName}";
        }
    }
}
