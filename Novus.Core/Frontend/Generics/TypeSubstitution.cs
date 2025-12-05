using Novus.IR;

namespace Novus.Frontend.Generics;

/// <summary>
/// Helper methods for extracting generic type mappings by comparing base and monomorphized types
/// This is used for type inference during generic instantiation
/// </summary>
public static class TypeSubstitutionHelper
{
    /// <summary>
    /// Extract generic type mappings by recursively comparing base type with monomorphized type
    /// For example: comparing Option&lt;T&gt; with Option&lt;i32&gt; extracts T -> i32
    /// </summary>
    public static void ExtractGenericTypeMapping(
        IrType baseType,
        IrType monomorphizedType,
        Dictionary<string, IrType> substitutions)
    {
        switch (baseType)
        {
            case IrGenericType gt:
                // Direct generic type - map it to the concrete type
                if (!substitutions.ContainsKey(gt.ParameterName))
                {
                    substitutions[gt.ParameterName] = monomorphizedType;
                }
                break;

            case IrPointerType basePtrType when monomorphizedType is IrPointerType monoPtrType:
                // Recurse into pointer pointee types
                ExtractGenericTypeMapping(basePtrType.PointeeType, monoPtrType.PointeeType, substitutions);
                break;

            case IrMutReferenceType baseRefType when monomorphizedType is IrMutReferenceType monoRefType:
                // Recurse into mutable reference types
                ExtractGenericTypeMapping(baseRefType.PointeeType, monoRefType.PointeeType, substitutions);
                break;

            case IrReferenceType baseRefType when monomorphizedType is IrReferenceType monoRefType:
                // Recurse into immutable reference types
                ExtractGenericTypeMapping(baseRefType.PointeeType, monoRefType.PointeeType, substitutions);
                break;

            case IrArrayType baseArrayType when monomorphizedType is IrArrayType monoArrayType:
                // Recurse into array element types
                if (baseArrayType.Length == monoArrayType.Length)
                {
                    ExtractGenericTypeMapping(baseArrayType.ElementType, monoArrayType.ElementType, substitutions);
                }
                break;

            case IrStructType baseStructType when monomorphizedType is IrStructType monoStructType:
                // Recurse into struct field types to extract generic mappings
                // For example: Box<T> matched with Box<i32> should extract T -> i32
                if (baseStructType.StructName == monoStructType.StructName &&
                    baseStructType.Fields.Count == monoStructType.Fields.Count)
                {
                    for (int i = 0; i < baseStructType.Fields.Count; i++)
                    {
                        ExtractGenericTypeMapping(
                            baseStructType.Fields[i].Type,
                            monoStructType.Fields[i].Type,
                            substitutions);
                    }
                }
                break;

            case IrEnumType baseEnumType when monomorphizedType is IrEnumType monoEnumType:
                // Recurse into enum variant types to extract generic mappings
                // For example: Option<T> matched with Option<i32> should extract T -> i32
                if (baseEnumType.EnumName == monoEnumType.EnumName &&
                    baseEnumType.Variants.Count == monoEnumType.Variants.Count)
                {
                    for (int i = 0; i < baseEnumType.Variants.Count; i++)
                    {
                        var baseVariant = baseEnumType.Variants[i];
                        var monoVariant = monoEnumType.Variants[i];

                        if (baseVariant.Name == monoVariant.Name &&
                            baseVariant.AssociatedData.Count == monoVariant.AssociatedData.Count)
                        {
                            for (int j = 0; j < baseVariant.AssociatedData.Count; j++)
                            {
                                ExtractGenericTypeMapping(
                                    baseVariant.AssociatedData[j],
                                    monoVariant.AssociatedData[j],
                                    substitutions);
                            }
                        }
                    }
                }
                break;

            // For non-generic types (primitives, concrete structs), no mapping needed
            default:
                break;
        }
    }

    /// <summary>
    /// Infer generic type arguments from call site arguments by comparing with template parameters
    /// </summary>
    public static Dictionary<string, IrType>? InferGenericTypes(
        List<string> genericParams,
        List<IrParameter> templateParams,
        List<IrValue> arguments)
    {
        if (arguments.Count != templateParams.Count)
        {
            return null; // Argument count mismatch
        }

        var typeSubstitutions = new Dictionary<string, IrType>();

        // Match each argument to its parameter and extract type mappings
        for (int i = 0; i < arguments.Count; i++)
        {
            var argType = arguments[i].Type;
            var paramType = templateParams[i].Type;

            // Recursively extract generic type mappings
            ExtractGenericTypeMapping(paramType, argType, typeSubstitutions);
        }

        // Verify all generic parameters were resolved
        foreach (var genericParam in genericParams)
        {
            if (!typeSubstitutions.ContainsKey(genericParam))
            {
                return null; // Could not infer all type parameters
            }
        }

        return typeSubstitutions;
    }

    /// <summary>
    /// Build a type substitution map from a struct's generic parameters and its type arguments
    /// </summary>
    public static Dictionary<string, IrType>? BuildStructTypeSubstitutions(
        IrStructType baseStruct,
        IrStructType monomorphizedStruct)
    {
        var typeSubstitutions = new Dictionary<string, IrType>();

        // First, try to build substitutions directly from TypeArguments if available
        if (monomorphizedStruct.TypeArguments != null &&
            monomorphizedStruct.TypeArguments.Count == baseStruct.GenericParameters.Count)
        {
            for (int i = 0; i < baseStruct.GenericParameters.Count; i++)
            {
                typeSubstitutions[baseStruct.GenericParameters[i]] = monomorphizedStruct.TypeArguments[i];
            }
            return typeSubstitutions;
        }

        // Second, try to parse type arguments from the cache key
        if (monomorphizedStruct.CacheKey != null && monomorphizedStruct.CacheKey.Contains("<"))
        {
            var parsedSubstitutions = ParseTypeArgumentsFromCacheKey(
                monomorphizedStruct.CacheKey,
                baseStruct.GenericParameters);
            if (parsedSubstitutions != null && parsedSubstitutions.Count > 0)
            {
                return parsedSubstitutions;
            }
        }

        // Fallback: scan fields to extract generic type mappings
        for (int i = 0; i < baseStruct.Fields.Count && i < monomorphizedStruct.Fields.Count; i++)
        {
            var baseFieldType = baseStruct.Fields[i].Type;
            var monomorphizedFieldType = monomorphizedStruct.Fields[i].Type;

            // Recursively extract generic type mappings from field types
            ExtractGenericTypeMapping(baseFieldType, monomorphizedFieldType, typeSubstitutions);
        }

        return typeSubstitutions.Count > 0 ? typeSubstitutions : null;
    }

    /// <summary>
    /// Build a type substitution map from an enum's generic parameters and its variants
    /// </summary>
    public static Dictionary<string, IrType>? BuildEnumTypeSubstitutions(
        IrEnumType baseEnum,
        IrEnumType monomorphizedEnum)
    {
        var typeSubstitutions = new Dictionary<string, IrType>();

        // Extract type mappings by comparing base enum variants with monomorphized enum variants
        if (monomorphizedEnum.CacheKey != null) // enum is monomorphized (e.g., Option<i32>)
        {
            for (int varIdx = 0; varIdx < baseEnum.Variants.Count && varIdx < monomorphizedEnum.Variants.Count; varIdx++)
            {
                var baseVariant = baseEnum.Variants[varIdx];
                var monoVariant = monomorphizedEnum.Variants[varIdx];

                if (baseVariant.Name == monoVariant.Name &&
                    baseVariant.AssociatedData.Count == monoVariant.AssociatedData.Count)
                {
                    for (int dataIdx = 0; dataIdx < baseVariant.AssociatedData.Count; dataIdx++)
                    {
                        ExtractGenericTypeMapping(
                            baseVariant.AssociatedData[dataIdx],
                            monoVariant.AssociatedData[dataIdx],
                            typeSubstitutions);
                    }
                }
            }
        }

        return typeSubstitutions.Count > 0 ? typeSubstitutions : null;
    }

    /// <summary>
    /// Parse type arguments from a cache key string (e.g., "HashMap<u32,u32>")
    /// Returns substitutions for primitive types only (complex types need full parsing)
    /// </summary>
    private static Dictionary<string, IrType>? ParseTypeArgumentsFromCacheKey(
        string cacheKey,
        List<string> genericParameters)
    {
        var openBracket = cacheKey.IndexOf('<');
        var closeBracket = cacheKey.LastIndexOf('>');
        if (openBracket < 0 || closeBracket <= openBracket)
        {
            return null;
        }

        var typeArgsStr = cacheKey.Substring(openBracket + 1, closeBracket - openBracket - 1);
        var typeArgNames = typeArgsStr.Split(',');

        if (typeArgNames.Length != genericParameters.Count)
        {
            return null;
        }

        var substitutions = new Dictionary<string, IrType>();
        for (int i = 0; i < genericParameters.Count; i++)
        {
            var typeArgName = typeArgNames[i].Trim();
            // Only handle primitive types - complex types need full parsing
            IrType? concreteType = typeArgName switch
            {
                "u8" => IrIntType.U8,
                "u16" => IrIntType.U16,
                "u32" => IrIntType.U32,
                "u64" => IrIntType.U64,
                "i8" => IrIntType.I8,
                "i16" => IrIntType.I16,
                "i32" => IrIntType.I32,
                "i64" => IrIntType.I64,
                "bool" => IrBoolType.Instance,
                "void" => IrVoidType.Instance,
                _ => null
            };

            if (concreteType != null)
            {
                substitutions[genericParameters[i]] = concreteType;
            }
        }

        return substitutions.Count > 0 ? substitutions : null;
    }
}

/// <summary>
/// Builder for creating instantiation cache keys
/// </summary>
public static class InstantiationKeyBuilder
{
    /// <summary>
    /// Build a method instantiation key (e.g., "Vec<i32>::push")
    /// </summary>
    public static string BuildMethodKey(
        string structCacheKey,
        string methodName,
        bool isTraitImpl = false,
        string? traitName = null)
    {
        if (isTraitImpl && traitName != null)
        {
            return $"{structCacheKey}::{traitName}::{methodName}";
        }
        return $"{structCacheKey}::{methodName}";
    }

    /// <summary>
    /// Build a function instantiation key (e.g., "identity<i32>")
    /// </summary>
    public static string BuildFunctionKey(
        string functionName,
        Dictionary<string, IrType> typeSubstitutions)
    {
        var typeNames = typeSubstitutions
            .OrderBy(kv => kv.Key)
            .Select(kv => kv.Value.Name);
        return $"{functionName}<{string.Join(",", typeNames)}>";
    }

    /// <summary>
    /// Build a method template key (e.g., "Vec::push")
    /// </summary>
    public static string BuildMethodTemplateKey(string baseTypeName, string methodName)
    {
        return $"{baseTypeName}::{methodName}";
    }

    /// <summary>
    /// Build a mangled name for an instantiated generic function (e.g., "identity_i32")
    /// </summary>
    public static string BuildGenericFunctionMangledName(
        string functionName,
        Dictionary<string, IrType> typeSubstitutions)
    {
        var mangledName = functionName;
        foreach (var kvp in typeSubstitutions.OrderBy(kv => kv.Key))
        {
            mangledName += "_" + kvp.Value.Name
                .Replace("*", "ptr")
                .Replace("&", "ref")
                .Replace("[", "arr")
                .Replace("]", "");
        }
        return mangledName;
    }

    /// <summary>
    /// Build a mangled name for an instantiated enum method (e.g., "Option::unwrap_i32")
    /// </summary>
    public static string BuildEnumMethodMangledName(
        string enumName,
        string methodName,
        List<string> typeArgKeys)
    {
        var sanitizedKeys = typeArgKeys.Select(k =>
            k.Replace("<", "_")
             .Replace(">", "_")
             .Replace(",", "_")
             .Replace("*", "ptr_"));
        return $"{enumName}::{methodName}_{string.Join("_", sanitizedKeys)}";
    }

    /// <summary>
    /// Build a mangled name for a method (trait impl or inherent impl).
    /// This is the single source of truth for method name mangling.
    /// - Trait impls: Type_Trait_TypeArg1_TypeArg2_method (e.g., Counter_Iterator_i32_next)
    /// - Inherent impls: Type::method (e.g., Vec::push)
    /// </summary>
    public static string BuildMethodMangledName(
        string typeName,
        string methodName,
        bool isTraitImpl = false,
        string? traitName = null,
        IReadOnlyList<IrType>? traitTypeArgs = null)
    {
        if (isTraitImpl && traitName != null)
        {
            var typeArgsSuffix = traitTypeArgs != null && traitTypeArgs.Count > 0
                ? "_" + string.Join("_", traitTypeArgs.Select(t => t.Name.Replace("::", "_")))
                : "";
            return $"{typeName}_{traitName}{typeArgsSuffix}_{methodName}";
        }
        return $"{typeName}::{methodName}";
    }

    /// <summary>
    /// Build a mangled name for an inherent impl method.
    /// Convenience overload for the common case.
    /// </summary>
    public static string BuildInherentMethodName(string typeName, string methodName)
    {
        return $"{typeName}::{methodName}";
    }
}
