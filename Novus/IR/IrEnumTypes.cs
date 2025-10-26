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
    private int? _cachedSize;

    public IrEnumType(string enumName, List<IrEnumVariant> variants, List<string>? genericParams = null)
    {
        EnumName = enumName;
        Variants = variants;
        GenericParameters = genericParams ?? new List<string>();
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
    public int DataIndex { get; set; }  // Which piece of associated data (0, 1, 2...)
    public IrType DataType { get; set; }

    public IrExtractVariantData(string resultName, IrValue enumValue, int dataIndex, IrType dataType)
    {
        ResultName = resultName;
        EnumValue = enumValue;
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
