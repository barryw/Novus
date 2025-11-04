namespace Novus.IR;

/// <summary>
/// Enum type - sum type with multiple variants (like Rust/Swift enums)
/// Can have associated data with each variant
/// </summary>
public class IrEnumType : IrType
{
    public string EnumName { get; }
    public List<IrEnumVariant> Variants { get; }
    public List<string> GenericParameters { get; }  // Type parameter names (e.g., ["T", "E"])
    public string? CacheKey { get; set; }  // Cache key for monomorphized types (e.g., "Option<i32>")
    public Novus.SemanticAnalysis.AttributeCollection? Attributes { get; set; }  // Enum attributes
    public IrWhereClause? WhereClause { get; set; }  // Generic type constraints (e.g., where T: Sortable)
    private int? _cachedSize;

    public IrEnumType(string enumName, List<IrEnumVariant> variants, List<string>? genericParams = null, string? cacheKey = null, Novus.SemanticAnalysis.AttributeCollection? attributes = null, IrWhereClause? whereClause = null)
    {
        EnumName = enumName;
        Variants = variants;
        GenericParameters = genericParams ?? new List<string>();
        CacheKey = cacheKey;
        Attributes = attributes;
        WhereClause = whereClause;
    }

    public override int SizeInBytes
    {
        get
        {
            if (_cachedSize.HasValue)
                return _cachedSize.Value;

            // Enum size = tag (4 bytes) + max(variant data sizes)
            // Tag is 32-bit int discriminant
            int tagSize = 4;
            int maxDataSize = 0;

            foreach (var variant in Variants)
            {
                int variantSize = 0;
                foreach (var dataType in variant.AssociatedData)
                {
                    variantSize += dataType.SizeInBytes;
                }
                if (variantSize > maxDataSize)
                    maxDataSize = variantSize;
            }

            // Word-align the total size (68k prefers word alignment)
            int totalSize = tagSize + maxDataSize;
            if (totalSize % 2 != 0)
                totalSize++;

            _cachedSize = totalSize;
            return totalSize;
        }
    }

    public override string Name
    {
        get
        {
            if (GenericParameters.Count > 0)
                return $"{EnumName}<{string.Join(", ", GenericParameters)}>";
            return EnumName;
        }
    }

    public IrEnumVariant? GetVariant(string variantName)
    {
        return Variants.FirstOrDefault(v => v.Name == variantName);
    }

    public int GetVariantTag(string variantName)
    {
        for (int i = 0; i < Variants.Count; i++)
        {
            if (Variants[i].Name == variantName)
                return i;
        }
        return -1;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not IrEnumType other)
            return false;

        // Two enums are equal if they have the same name and same number of generic parameters
        // This handles both generic (Option<T>) and monomorphized (Option<i32>) types
        return EnumName == other.EnumName &&
               GenericParameters.Count == other.GenericParameters.Count &&
               GenericParameters.SequenceEqual(other.GenericParameters) &&
               Variants.Count == other.Variants.Count &&
               Variants.Zip(other.Variants, (v1, v2) =>
                   v1.Name == v2.Name &&
                   v1.Tag == v2.Tag &&
                   v1.AssociatedData.Count == v2.AssociatedData.Count &&
                   v1.AssociatedData.Zip(v2.AssociatedData, (d1, d2) => d1.Equals(d2)).All(x => x)
               ).All(x => x);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EnumName);
        hash.Add(GenericParameters.Count);
        foreach (var param in GenericParameters)
            hash.Add(param);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Represents a variant of an enum
/// </summary>
public class IrEnumVariant
{
    public string Name { get; set; }
    public List<IrType> AssociatedData { get; set; }  // Types of associated data
    public int Tag { get; set; }  // Discriminant value (0, 1, 2, ...)

    public IrEnumVariant(string name, int tag, List<IrType>? associatedData = null)
    {
        Name = name;
        Tag = tag;
        AssociatedData = associatedData ?? new List<IrType>();
    }

    public bool HasAssociatedData => AssociatedData.Count > 0;
}

/// <summary>
/// Enum constructor - represents a partially applied enum variant (e.g., Option::Some)
/// This is returned by path expressions and can be "called" to create an IrEnumValue
/// </summary>
public class IrEnumConstructor : IrValue
{
    public string VariantName { get; set; }
    public int VariantTag { get; set; }

    public IrEnumConstructor(IrEnumType enumType, string variantName, int tag)
        : base(enumType)
    {
        VariantName = variantName;
        VariantTag = tag;
    }
}

/// <summary>
/// Enum value construction (e.g., Option::Some(42))
/// </summary>
public class IrEnumValue : IrValue
{
    public string VariantName { get; set; }
    public List<IrValue> AssociatedValues { get; set; }  // Values for the variant's associated data
    public int VariantTag { get; set; }

    public IrEnumValue(IrEnumType enumType, string variantName, int tag, List<IrValue>? associatedValues = null)
        : base(enumType)
    {
        VariantName = variantName;
        VariantTag = tag;
        AssociatedValues = associatedValues ?? new List<IrValue>();
    }
}

/// <summary>
/// Turbo-fish parameterized type (e.g., Vec::<u32> before accessing members)
/// This is an intermediate value that stores type name and explicit type arguments
/// </summary>
public class IrTurboFishType : IrValue
{
    public string TypeName { get; set; }
    public List<IrType> TypeArguments { get; set; }

    public IrTurboFishType(string typeName, List<IrType> typeArguments)
        : base(IrVoidType.Instance)  // Placeholder type
    {
        TypeName = typeName;
        TypeArguments = typeArguments;
    }
}

/// <summary>
/// Generic associated function reference (e.g., Vec::new before type parameters are known)
/// This is a placeholder that will be resolved to a concrete function when called
/// </summary>
public class IrGenericAssociatedFunction : IrValue
{
    public string TypeName { get; set; }
    public string MethodName { get; set; }
    public List<string> GenericParameters { get; set; }
    public List<IrType>? ExplicitTypeArgs { get; set; }  // For turbo-fish syntax: Vec::<u32>::new

    public IrGenericAssociatedFunction(string typeName, string methodName, List<string> genericParameters, List<IrType>? explicitTypeArgs = null)
        : base(IrVoidType.Instance)  // Type will be determined when instantiated
    {
        TypeName = typeName;
        MethodName = methodName;
        GenericParameters = genericParameters;
        ExplicitTypeArgs = explicitTypeArgs;
    }
}

/// <summary>
/// Function reference (e.g., Vec_i32_new after monomorphization, or non-generic associated function)
/// Can be called directly
/// </summary>
public class IrFunctionRef : IrValue
{
    public IrFunction Function { get; set; }

    public IrFunctionRef(IrFunction function)
        : base(function.ReturnType)
    {
        Function = function;
    }
}

/// <summary>
/// Match expression instruction - pattern matching on enum values
/// </summary>
public class IrMatch : IrInstruction
{
    public IrValue MatchValue { get; set; }  // Value being matched
    public List<IrMatchArm> Arms { get; set; }
    public string? ResultName { get; set; }  // If match is an expression, where to store result
    public IrType? ResultType { get; set; }  // Type of result if match is an expression

    public IrMatch(IrValue matchValue, List<IrMatchArm> arms, string? resultName = null, IrType? resultType = null)
    {
        MatchValue = matchValue;
        Arms = arms;
        ResultName = resultName;
        ResultType = resultType;
    }
}

/// <summary>
/// Match arm - pattern + target label
/// </summary>
public class IrMatchArm
{
    public IrPattern Pattern { get; set; }
    public string TargetLabel { get; set; }  // Label to jump to if pattern matches
    public List<string> BoundVariables { get; set; }  // Variables bound by pattern (for variant data)

    public IrMatchArm(IrPattern pattern, string targetLabel, List<string>? boundVariables = null)
    {
        Pattern = pattern;
        TargetLabel = targetLabel;
        BoundVariables = boundVariables ?? new List<string>();
    }
}

/// <summary>
/// Pattern for matching
/// </summary>
public abstract class IrPattern
{
}

/// <summary>
/// Wildcard pattern (_) - matches anything
/// </summary>
public class IrWildcardPattern : IrPattern
{
}

/// <summary>
/// Enum variant pattern (e.g., Some(x), None, Ok(val))
/// </summary>
public class IrVariantPattern : IrPattern
{
    public string VariantName { get; set; }
    public int VariantTag { get; set; }
    public List<string> BoundVariables { get; set; }  // Names of variables bound by this pattern

    public IrVariantPattern(string variantName, int tag, List<string>? boundVariables = null)
    {
        VariantName = variantName;
        VariantTag = tag;
        BoundVariables = boundVariables ?? new List<string>();
    }
}

/// <summary>
/// Literal pattern (e.g., 42, true, "hello")
/// </summary>
public class IrLiteralPattern : IrPattern
{
    public IrValue LiteralValue { get; set; }

    public IrLiteralPattern(IrValue literalValue)
    {
        LiteralValue = literalValue;
    }
}

/// <summary>
/// Extract tag from enum value (for pattern matching)
/// </summary>
public class IrExtractTag : IrInstruction
{
    public string ResultName { get; set; }
    public IrValue EnumValue { get; set; }

    public IrExtractTag(string resultName, IrValue enumValue)
    {
        ResultName = resultName;
        EnumValue = enumValue;
    }
}

/// <summary>
/// Extract associated data from enum variant
/// </summary>
public class IrExtractVariantData : IrInstruction
{
    public string ResultName { get; set; }
    public IrValue EnumValue { get; set; }
    public string VariantName { get; set; }  // Name of the variant we're extracting from
    public int DataIndex { get; set; }  // Which piece of associated data (0, 1, 2...)
    public IrType DataType { get; set; }

    public IrExtractVariantData(string resultName, IrValue enumValue, string variantName, int dataIndex, IrType dataType)
    {
        ResultName = resultName;
        EnumValue = enumValue;
        VariantName = variantName;
        DataIndex = dataIndex;
        DataType = dataType;
    }
}

/// <summary>
/// Generic type parameter (used during semantic analysis before monomorphization)
/// </summary>
public class IrGenericType : IrType
{
    public string ParameterName { get; }

    public IrGenericType(string parameterName)
    {
        ParameterName = parameterName;
    }

    public override int SizeInBytes => throw new InvalidOperationException(
        "Generic types must be monomorphized before code generation");

    public override string Name => ParameterName;

    // Override Equals and GetHashCode to ensure type parameter identity is based on name
    // This allows TypeInterner to correctly cache pointer types like *T
    public override bool Equals(object? obj)
    {
        if (obj is IrGenericType other)
        {
            return ParameterName == other.ParameterName;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return ParameterName.GetHashCode();
    }
}

/// <summary>
/// Monomorphized (concretized) generic type instance
/// E.g., Option<i32> is a monomorphization of Option<T> with T=i32
/// </summary>
public class IrMonomorphizedType
{
    public string BaseName { get; set; }  // e.g., "Option"
    public List<IrType> TypeArguments { get; set; }  // e.g., [i32]
    public IrType ConcreteType { get; set; }  // The actual instantiated type

    public IrMonomorphizedType(string baseName, List<IrType> typeArguments, IrType concreteType)
    {
        BaseName = baseName;
        TypeArguments = typeArguments;
        ConcreteType = concreteType;
    }

    public string MangledName
    {
        get
        {
            var typeArgs = string.Join("_", TypeArguments.Select(t => t.Name.Replace("<", "_").Replace(">", "_")));
            return $"{BaseName}_{typeArgs}";
        }
    }
}
