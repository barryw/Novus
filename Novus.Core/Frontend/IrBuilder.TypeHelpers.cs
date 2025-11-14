using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing type parsing and manipulation helper methods.
/// This file contains utilities for parsing types, literals, and type-related operations.
/// </summary>
public partial class IrBuilder
{
    private IrType ParseType(NovusParser.TypeContext context)
    {
        return _typeParser.ParseType(context);
    }

    /// <summary>
    /// Map a primitive type name (from grammar keywords) to its IrType representation
    /// </summary>
    private IrType MapPrimitiveTypeName(string primitiveTypeName)
    {
        return primitiveTypeName switch
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
            _ => throw new CompilerBugException(
                $"Unknown primitive type name: {primitiveTypeName}",
                "MapPrimitiveTypeName",
                _inputFilePath,
                null
            )
        };
    }

    /// <summary>
    /// Recursively substitute generic type parameters with concrete types
    /// </summary>
    /// <summary>
    /// Check if a type contains a specific generic parameter
    /// </summary>
    private bool TypeContainsGeneric(IrType type, string genericParamName)
    {
        if (type is IrGenericType gt)
        {
            return gt.ParameterName == genericParamName;
        }
        if (type is IrPointerType ptrType)
        {
            return TypeContainsGeneric(ptrType.PointeeType, genericParamName);
        }
        if (type is IrReferenceType refType)
        {
            return TypeContainsGeneric(refType.PointeeType, genericParamName);
        }
        if (type is IrMutReferenceType mutRefType)
        {
            return TypeContainsGeneric(mutRefType.PointeeType, genericParamName);
        }
        if (type is IrArrayType arrayType)
        {
            return TypeContainsGeneric(arrayType.ElementType, genericParamName);
        }
        if (type is IrStructType structType)
        {
            return structType.Fields.Any(f => TypeContainsGeneric(f.Type, genericParamName));
        }
        if (type is IrEnumType enumType)
        {
            return enumType.Variants.Any(v => v.AssociatedData.Any(d => TypeContainsGeneric(d, genericParamName)));
        }
        return false;
    }

    private string GetTypeCacheKey(IrType type)
    {
        // Recursively build a cache key for a type, handling nested generics
        if (type is IrEnumType enumType)
        {
            // Check if enum still contains generic types in its variants
            // An enum is only fully monomorphized if it has no generic parameters
            // AND no generic types in its variant data
            bool hasGenericData = enumType.Variants.Any(v =>
                v.AssociatedData.Any(d => d is IrGenericType));

            if (enumType.GenericParameters.Count > 0 || hasGenericData)
            {
                // Still generic - build cache key from generic parameter names found in variant data
                if (hasGenericData)
                {
                    // Extract generic type names from variant data
                    var genericNames = new HashSet<string>();
                    foreach (var variant in enumType.Variants)
                    {
                        foreach (var data in variant.AssociatedData)
                        {
                            if (data is IrGenericType gt)
                            {
                                genericNames.Add(gt.ParameterName);
                            }
                        }
                    }
                    return $"{enumType.EnumName}<{string.Join(",", genericNames.OrderBy(x => x))}>";
                }
                else
                {
                    // Use declared generic parameters
                    return $"{enumType.EnumName}<{string.Join(",", enumType.GenericParameters)}>";
                }
            }
            else
            {
                // Fully monomorphized enum - use stored cache key if available
                if (enumType.CacheKey != null)
                {
                    return enumType.CacheKey;
                }
                // Non-generic enum (like DosError) - just use the name
                return enumType.EnumName;
            }
        }
        else if (type is IrGenericType gt)
        {
            return gt.ParameterName;
        }
        else
        {
            return type.Name;
        }
    }

    private (long value, IrType type) ParseIntegerLiteral(string text)
    {
        // Strip underscores for readability (e.g., 1_000_000)
        text = text.Replace("_", "");

        // Check for type suffix
        if (text.EndsWith("u8"))
            return (long.Parse(text[..^2]), IrIntType.U8);
        if (text.EndsWith("u16"))
            return (long.Parse(text[..^3]), IrIntType.U16);
        if (text.EndsWith("u32"))
            return (long.Parse(text[..^3]), IrIntType.U32);
        if (text.EndsWith("u64"))
            return (long.Parse(text[..^3]), IrIntType.U64);
        if (text.EndsWith("i8"))
            return (long.Parse(text[..^2]), IrIntType.I8);
        if (text.EndsWith("i16"))
            return (long.Parse(text[..^3]), IrIntType.I16);
        if (text.EndsWith("i32"))
            return (long.Parse(text[..^3]), IrIntType.I32);
        if (text.EndsWith("i64"))
            return (long.Parse(text[..^3]), IrIntType.I64);

        // Default to i32
        return (long.Parse(text), IrIntType.I32);
    }

    private (double value, IrType type) ParseFloatLiteral(string text)
    {
        // Check for type suffix
        if (text.EndsWith("fixed32"))
        {
            var numText = text[..^7];
            return (double.Parse(numText), IrFixedType.Fixed32);
        }
        if (text.EndsWith("fixed16"))
        {
            var numText = text[..^7];
            return (double.Parse(numText), IrFixedType.Fixed16);
        }
        if (text.EndsWith("f64"))
        {
            var numText = text[..^3];
            return (double.Parse(numText), IrFloatType.F64);
        }
        if (text.EndsWith("f32"))
        {
            var numText = text[..^3];
            return (double.Parse(numText), IrFloatType.F32);
        }

        // Default to f32
        return (double.Parse(text), IrFloatType.F32);
    }

    private (long value, IrType type) ParseBinaryLiteral(string text)
    {
        // Remove '%' prefix and underscores
        text = text[1..].Replace("_", "");

        // Extract type suffix if present
        IrType type = IrIntType.I32;
        string binaryText = text;

        if (text.EndsWith("u8"))
        {
            type = IrIntType.U8;
            binaryText = text[..^2];
        }
        else if (text.EndsWith("u16"))
        {
            type = IrIntType.U16;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("u32"))
        {
            type = IrIntType.U32;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("u64"))
        {
            type = IrIntType.U64;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("i8"))
        {
            type = IrIntType.I8;
            binaryText = text[..^2];
        }
        else if (text.EndsWith("i16"))
        {
            type = IrIntType.I16;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("i32"))
        {
            type = IrIntType.I32;
            binaryText = text[..^3];
        }
        else if (text.EndsWith("i64"))
        {
            type = IrIntType.I64;
            binaryText = text[..^3];
        }

        // Parse binary string to long
        var value = Convert.ToInt64(binaryText, 2);
        return (value, type);
    }

    private (long value, IrType type) ParseHexLiteral(string text)
    {
        // Remove '$' prefix and underscores
        text = text[1..].Replace("_", "");

        // Extract type suffix if present
        IrType type = IrIntType.I32;
        string hexText = text;

        if (text.EndsWith("u8"))
        {
            type = IrIntType.U8;
            hexText = text[..^2];
        }
        else if (text.EndsWith("u16"))
        {
            type = IrIntType.U16;
            hexText = text[..^3];
        }
        else if (text.EndsWith("u32"))
        {
            type = IrIntType.U32;
            hexText = text[..^3];
        }
        else if (text.EndsWith("u64"))
        {
            type = IrIntType.U64;
            hexText = text[..^3];
        }
        else if (text.EndsWith("i8"))
        {
            type = IrIntType.I8;
            hexText = text[..^2];
        }
        else if (text.EndsWith("i16"))
        {
            type = IrIntType.I16;
            hexText = text[..^3];
        }
        else if (text.EndsWith("i32"))
        {
            type = IrIntType.I32;
            hexText = text[..^3];
        }
        else if (text.EndsWith("i64"))
        {
            type = IrIntType.I64;
            hexText = text[..^3];
        }

        // Parse hex string to long
        var value = Convert.ToInt64(hexText, 16);
        return (value, type);
    }

    /// <summary>
    /// Parse a type from its mangled name (e.g., "i32" -> IrIntType.I32, "Vec_i32" -> Vec<i32>)
    /// </summary>
    private IrType ParseTypeFromMangledName(string mangledName)
    {
        // Handle primitive types
        if (mangledName == "i8") return IrIntType.I8;
        if (mangledName == "i16") return IrIntType.I16;
        if (mangledName == "i32") return IrIntType.I32;
        if (mangledName == "i64") return IrIntType.I64;
        if (mangledName == "u8") return IrIntType.U8;
        if (mangledName == "u16") return IrIntType.U16;
        if (mangledName == "u32") return IrIntType.U32;
        if (mangledName == "u64") return IrIntType.U64;
        if (mangledName == "bool") return IrBoolType.Instance;
        if (mangledName == "void") return IrVoidType.Instance;

        // Handle struct types (e.g., "Vec_i32" -> Vec<i32>)
        // For now, this is a simple implementation
        // TODO: Handle nested generics and more complex types
        var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot parse complex mangled type name '{mangledName}' yet",
            errorLocation
        );
        return null;
    }

    /// <summary>
    /// Parses variadic parameter from parameter list context and adds it to the function.
    /// This helper consolidates the repeated pattern of variadic parameter parsing that appears
    /// throughout IrBuilder (8 occurrences).
    ///
    /// Variadic parameters are given an opaque pointer type (*void) since their actual types
    /// are checked at the call site rather than in the function signature.
    /// </summary>
    /// <param name="paramList">The parameter list context from the parse tree</param>
    /// <param name="function">The IrFunction to add the variadic parameter to</param>
    private void ParseVariadicParameter(NovusParser.ParameterListContext? paramList, IrFunction function)
    {
        if (paramList?.variadicParameter() == null)
            return;

        var variadicCtx = paramList.variadicParameter();
        var variadicName = variadicCtx.IDENTIFIER().GetText();
        // Variadic parameters have opaque type for now (we'll handle type checking later)
        var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
        function.Parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
        function.IsVariadic = true;
    }

    /// <summary>
    /// Parses variadic parameter from parameter list context and adds it to a parameter list.
    /// Overload for contexts where parameters are being collected in a list rather than added
    /// directly to an IrFunction (e.g., trait method signatures, template parameters).
    /// </summary>
    /// <param name="paramList">The parameter list context from the parse tree</param>
    /// <param name="parameters">The list to add the variadic parameter to</param>
    private void ParseVariadicParameter(NovusParser.ParameterListContext? paramList, List<IrParameter> parameters)
    {
        if (paramList?.variadicParameter() == null)
            return;

        var variadicCtx = paramList.variadicParameter();
        var variadicName = variadicCtx.IDENTIFIER().GetText();
        // Variadic parameters have opaque type for now (we'll handle type checking later)
        var variadicType = _typeInterner.GetPointerType(IrVoidType.Instance);
        parameters.Add(new IrParameter(variadicName, variadicType, isVariadic: true));
    }

    /// <summary>
    /// Parses return type from a function declaration context.
    /// This helper consolidates the repeated ternary pattern that appears throughout both
    /// IrBuilder (9+ occurrences) and SemanticAnalyzer (3+ occurrences).
    ///
    /// If the context has a type annotation, parses it. Otherwise returns void.
    /// </summary>
    /// <param name="typeContext">The type context from the parse tree (may be null)</param>
    /// <returns>The parsed return type, or IrVoidType.Instance if no type specified</returns>
    private IrType ParseReturnType(NovusParser.TypeContext? typeContext)
    {
        return typeContext != null ? ParseType(typeContext) : IrVoidType.Instance;
    }

    /// <summary>
    /// Parses generic parameter names from a generic parameter context.
    /// This helper consolidates the repeated loop pattern that appears 10+ times in IrBuilder
    /// for extracting generic parameter names from the parse tree.
    /// </summary>
    /// <param name="genericParamsContext">The generic parameters context from the parse tree (may be null)</param>
    /// <returns>List of generic parameter names, or empty list if no generic parameters</returns>
    private List<string> ParseGenericParameters(NovusParser.GenericParamsContext? genericParamsContext)
    {
        if (genericParamsContext == null)
            return new List<string>();

        var genericParams = new List<string>();
        foreach (var paramId in genericParamsContext.IDENTIFIER())
        {
            genericParams.Add(paramId.GetText());
        }
        return genericParams;
    }

    /// <summary>
    /// Get the mangled name for a type (e.g., IrIntType.I32 -> "i32", Vec<i32> -> "Vec_i32")
    /// </summary>
    private string GetMangledTypeName(IrType type)
    {
        if (type is IrIntType intType)
        {
            if (intType == IrIntType.I8) return "i8";
            if (intType == IrIntType.I16) return "i16";
            if (intType == IrIntType.I32) return "i32";
            if (intType == IrIntType.I64) return "i64";
            if (intType == IrIntType.U8) return "u8";
            if (intType == IrIntType.U16) return "u16";
            if (intType == IrIntType.U32) return "u32";
            if (intType == IrIntType.U64) return "u64";
        }
        else if (type is IrBoolType)
        {
            return "bool";
        }
        else if (type is IrStructType structType)
        {
            // Use CacheKey if available (for monomorphized types like Vec<i32>)
            if (structType.CacheKey != null)
            {
                return structType.CacheKey;
            }
            // Fall back to struct name for non-generic types
            return structType.StructName;
        }
        else if (type is IrPointerType ptrType)
        {
            return "ptr_" + GetMangledTypeName(ptrType.PointeeType);
        }

        var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot get mangled name for type '{type.Name}'",
            errorLocation
        );
        return null;
    }

    /// <summary>
    /// Ensure that a drop() method is instantiated for this type if it exists as a template.
    /// For generic types like Vec<T>, this will instantiate Vec<T>::drop() if it exists.

    private bool IsPrimitiveTypeName(string typeName)
    {
        return typeName switch
        {
            "i8" or "i16" or "i32" or "i64" or
            "u8" or "u16" or "u32" or "u64" or
            "bool" or "void" or "f32" or "f64" or "Self" => true,
            _ => false
        };
    }
}
