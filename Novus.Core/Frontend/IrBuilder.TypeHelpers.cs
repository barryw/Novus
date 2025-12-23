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
        var type = _typeParser.ParseType(context);

        // Apply type substitutions if we're instantiating a generic method
        if (_currentTypeSubstitutions != null && _currentTypeSubstitutions.Count > 0)
        {
            type = _typeParser.SubstituteGenericTypes(type, _currentTypeSubstitutions);
        }

        return type;
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

    /// <summary>
    /// Extract type suffix from numeric literal text. Consolidates the duplicated type suffix checking
    /// logic from ParseIntegerLiteral, ParseBinaryLiteral, and ParseHexLiteral.
    /// </summary>
    /// <param name="text">The literal text (with underscores already stripped)</param>
    /// <param name="defaultType">The default type if no suffix is found</param>
    /// <returns>Tuple of (numeric part without suffix, type)</returns>
    private (string NumericPart, IrType Type) ExtractIntegerTypeSuffix(string text, IrType defaultType)
    {
        // Check suffixes in order of decreasing length to avoid partial matches
        // For example, check "u64" before "u8" to avoid matching "u8" in "123u64"
        if (text.Length >= 3)
        {
            var suffix3 = text[^3..];
            switch (suffix3)
            {
                case "u64": return (text[..^3], IrIntType.U64);
                case "i64": return (text[..^3], IrIntType.I64);
                case "u32": return (text[..^3], IrIntType.U32);
                case "i32": return (text[..^3], IrIntType.I32);
                case "u16": return (text[..^3], IrIntType.U16);
                case "i16": return (text[..^3], IrIntType.I16);
            }
        }

        if (text.Length >= 2)
        {
            var suffix2 = text[^2..];
            switch (suffix2)
            {
                case "u8": return (text[..^2], IrIntType.U8);
                case "i8": return (text[..^2], IrIntType.I8);
            }
        }

        // No suffix found, use default type
        return (text, defaultType);
    }

    private (long value, IrType type) ParseIntegerLiteral(string text)
    {
        // Strip underscores for readability (e.g., 1_000_000)
        text = text.Replace("_", "");

        // Extract type suffix and numeric part
        var (numericPart, type) = ExtractIntegerTypeSuffix(text, IrIntType.I32);

        // Parse the numeric part
        return (long.Parse(numericPart), type);
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

        // Extract type suffix and binary part
        var (binaryText, type) = ExtractIntegerTypeSuffix(text, IrIntType.I32);

        // Parse binary string to long
        var value = Convert.ToInt64(binaryText, 2);
        return (value, type);
    }

    private (long value, IrType type) ParseHexLiteral(string text)
    {
        // Remove hex prefix ('$' or '0x'/'0X') and underscores
        if (text.StartsWith("0x") || text.StartsWith("0X"))
        {
            text = text[2..].Replace("_", "");
        }
        else
        {
            // Must be '$' prefix
            text = text[1..].Replace("_", "");
        }

        // Extract type suffix and hex part
        var (hexText, type) = ExtractIntegerTypeSuffix(text, IrIntType.I32);

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
        // Complex mangled type names with nested generics are not yet supported.
        // This is a graceful fallback for error recovery.
        var errorLocation = _currentStatementLocation ?? new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot parse complex mangled type name '{mangledName}'",
            errorLocation
        );
        return IrIntType.I32;
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
    /// Parse regular parameters (not self, not variadic) from a parameter list and add to a parameter collection.
    /// This helper consolidates the repeated pattern that appears 8+ times across IrBuilder files.
    /// </summary>
    /// <param name="paramList">The parameter list context from the parse tree</param>
    /// <param name="parameters">The list to add parsed parameters to</param>
    private void ParseRegularParameters(NovusParser.ParameterListContext? paramList, List<IrParameter> parameters)
    {
        if (paramList == null) return;

        foreach (var paramCtx in paramList.parameter())
        {
            var paramName = paramCtx.IDENTIFIER().GetText();
            var paramType = ParseType(paramCtx.type());

            // Special case: &[T] (unsized slice reference) should be treated as Slice<T>
            // This is syntactic sugar: &[T] in parameter position becomes Slice<T>
            if (paramType is IrReferenceType refType &&
                refType.PointeeType is IrArrayType arrayType &&
                arrayType.Length == -1)
            {
                // Try to find Slice<T> struct
                var sliceStruct = _module.Structs.FirstOrDefault(s => s.StructName == "Slice");
                if (sliceStruct != null && sliceStruct.GenericParameters != null && sliceStruct.GenericParameters.Count > 0)
                {
                    // Check if we already have this monomorphization
                    var elementType = arrayType.ElementType;
                    var cacheKey = $"Slice<{elementType.Name}>";
                    var monomorphizedSlice = _module.Structs.FirstOrDefault(s => s.CacheKey == cacheKey);

                    if (monomorphizedSlice == null)
                    {
                        // Need to monomorphize Slice<T> for this element type
                        var typeArgs = new List<IrType> { elementType };

                        monomorphizedSlice = IrStructType.Monomorphize(sliceStruct, typeArgs);
                        if (monomorphizedSlice != null)
                        {
                            // Register the monomorphized struct
                            monomorphizedSlice.CacheKey = cacheKey;
                            _module.AddStruct(monomorphizedSlice);
                        }
                    }

                    if (monomorphizedSlice != null)
                    {
                        // Use the monomorphized Slice<T> instead of &[T]
                        paramType = monomorphizedSlice;
                    }
                }
            }

            parameters.Add(new IrParameter(paramName, paramType));
        }
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
    /// <param name="registerInSymbolTable">Whether to register the parameters in the symbol table (default: false)</param>
    /// <returns>List of generic parameter names, or empty list if no generic parameters</returns>
    private List<string> ParseGenericParameters(NovusParser.GenericParamsContext? genericParamsContext, bool registerInSymbolTable = false)
    {
        if (genericParamsContext == null)
            return new List<string>();

        var genericParams = new List<string>();
        foreach (var param in genericParamsContext.genericParam())
        {
            if (param is NovusParser.TypeGenericParamContext typeParam)
            {
                var paramName = typeParam.IDENTIFIER().GetText();
                genericParams.Add(paramName);

                if (registerInSymbolTable)
                {
                    _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
                }
            }
            else if (param is NovusParser.ConstGenericParamContext constParam)
            {
                var paramName = constParam.IDENTIFIER().GetText();
                var constType = ParseType(constParam.type());
                genericParams.Add(paramName);

                if (registerInSymbolTable)
                {
                    _symbols.RegisterConstGenericParameter(paramName, new IrConstGenericParam(paramName, constType));
                }
            }
        }
        return genericParams;
    }

    /// <summary>
    /// Parse type arguments from a generic type args context (e.g., From&lt;DosError&gt;).
    /// This helper consolidates the repeated pattern that appears 6+ times across IrBuilder.cs
    /// and IrBuilder.Imports.cs for parsing trait/generic type arguments.
    /// </summary>
    /// <param name="typeArgsContext">The generic type arguments context from the parse tree (may be null)</param>
    /// <returns>List of parsed types, or empty list if no type arguments</returns>
    private List<IrType> ParseTypeArguments(NovusParser.GenericTypeArgsContext? typeArgsContext)
    {
        var typeArgs = new List<IrType>();
        if (typeArgsContext != null)
        {
            var typeList = typeArgsContext.typeList();
            foreach (var typeCtx in typeList.type())
            {
                typeArgs.Add(ParseType(typeCtx));
            }
        }
        return typeArgs;
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

        // Unsupported type for mangling - report error with current statement location
        var errorLocation = _currentStatementLocation ?? new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot get mangled name for type '{type.Name}'",
            errorLocation
        );
        return "unknown";
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

    #region Safe Symbol Lookup Helpers

    /// <summary>
    /// Safely lookup an enum type, throwing a descriptive exception if not found.
    /// Use this instead of LookupEnum()! pattern to avoid cryptic NullReferenceExceptions.
    /// </summary>
    /// <remarks>
    /// This method should be called only when the enum is known to exist (e.g., after HasEnum check).
    /// The exception is a CompilerBugException because reaching this with a non-existent enum
    /// indicates a bug in the compiler's internal logic, not a user error.
    /// </remarks>
    private IrEnumType RequireEnum(string typeName, Antlr4.Runtime.ParserRuleContext? context = null)
    {
        var enumType = _symbols.LookupEnum(typeName);
        if (enumType == null)
        {
            throw new CompilerBugException(
                $"Expected enum '{typeName}' to exist but lookup returned null. " +
                "This indicates HasEnum/LookupEnum inconsistency or a race condition.",
                "RequireEnum",
                _inputFilePath,
                context?.Start?.Line
            );
        }
        return enumType;
    }

    /// <summary>
    /// Safely lookup a struct type, throwing a descriptive exception if not found.
    /// Use this instead of LookupStruct()! pattern to avoid cryptic NullReferenceExceptions.
    /// </summary>
    /// <remarks>
    /// This method should be called only when the struct is known to exist (e.g., after HasStruct check).
    /// The exception is a CompilerBugException because reaching this with a non-existent struct
    /// indicates a bug in the compiler's internal logic, not a user error.
    /// </remarks>
    private IrStructType RequireStruct(string typeName, Antlr4.Runtime.ParserRuleContext? context = null)
    {
        var structType = _symbols.LookupStruct(typeName);
        if (structType == null)
        {
            throw new CompilerBugException(
                $"Expected struct '{typeName}' to exist but lookup returned null. " +
                "This indicates HasStruct/LookupStruct inconsistency or a race condition.",
                "RequireStruct",
                _inputFilePath,
                context?.Start?.Line
            );
        }
        return structType;
    }

    /// <summary>
    /// Safely lookup a monomorphized enum type, throwing a descriptive exception if not found.
    /// Use this instead of LookupMonomorphizedEnum()! pattern.
    /// </summary>
    private IrEnumType RequireMonomorphizedEnum(string cacheKey, Antlr4.Runtime.ParserRuleContext? context = null)
    {
        var enumType = _symbols.LookupMonomorphizedEnum(cacheKey);
        if (enumType == null)
        {
            throw new CompilerBugException(
                $"Expected monomorphized enum with cache key '{cacheKey}' to exist but lookup returned null.",
                "RequireMonomorphizedEnum",
                _inputFilePath,
                context?.Start?.Line
            );
        }
        return enumType;
    }

    /// <summary>
    /// Safely lookup a monomorphized struct type, throwing a descriptive exception if not found.
    /// Use this instead of LookupMonomorphizedStruct()! pattern.
    /// </summary>
    private IrStructType RequireMonomorphizedStruct(string cacheKey, Antlr4.Runtime.ParserRuleContext? context = null)
    {
        var structType = _symbols.LookupMonomorphizedStruct(cacheKey);
        if (structType == null)
        {
            throw new CompilerBugException(
                $"Expected monomorphized struct with cache key '{cacheKey}' to exist but lookup returned null.",
                "RequireMonomorphizedStruct",
                _inputFilePath,
                context?.Start?.Line
            );
        }
        return structType;
    }

    #endregion

    /// <summary>
    /// Parse an impl target type (either primitive or named) and return the type name and implementing type.
    /// This helper consolidates the repeated impl target type parsing logic that appears 5+ times across
    /// IrBuilder.cs and IrBuilder.Imports.cs.
    /// </summary>
    /// <param name="targetTypeCtx">The impl target type context from the parse tree (may be null for inherent impls)</param>
    /// <param name="targetTypeName">The target type name context for inherent impls (may be null for trait impls)</param>
    /// <param name="errorContext">Context for error reporting</param>
    /// <returns>Tuple of (type name, implementing type), or (null, null) if error occurred</returns>
    private (string? TypeName, IrType? ImplementingType) ParseImplTargetType(
        NovusParser.ImplTargetTypeContext? targetTypeCtx,
        NovusParser.TypeNameContext? targetTypeName,
        Antlr4.Runtime.ParserRuleContext errorContext)
    {
        string typeName;
        IrType? implementingType = null;

        if (targetTypeCtx != null)
        {
            // Trait impl format: impl Trait for TargetType
            if (targetTypeCtx is NovusParser.PrimitiveImplTargetContext primitiveCtx)
            {
                // impl Trait for i32, bool, etc.
                var primitiveTypeNameCtx = primitiveCtx.primitiveTypeName();
                typeName = primitiveTypeNameCtx.GetText().ToLowerInvariant();
                implementingType = MapPrimitiveTypeName(typeName);
            }
            else if (targetTypeCtx is NovusParser.NamedImplTargetContext namedCtx)
            {
                // impl Trait for MyType
                typeName = namedCtx.typeName().IDENTIFIER(0).GetText();

                // Look up the implementing type (could be struct or enum)
                implementingType = _symbols.LookupStruct(typeName) ?? (IrType?)_symbols.LookupEnum(typeName);

                if (implementingType == null)
                {
                    var errorLocation = GetLocation(errorContext);
                    _diagnostics.ReportError(
                        ErrorCodes.TypeNotFound,
                        $"Type '{typeName}' not found for impl block",
                        errorLocation
                    );
                    return (null, null);
                }
            }
            else
            {
                throw new CompilerBugException(
                    $"Unknown impl target type context: {targetTypeCtx?.GetType().Name}",
                    "ParseImplTargetType",
                    _inputFilePath,
                    null
                );
            }
        }
        else if (targetTypeName != null)
        {
            // Inherent impl format: impl TargetType
            typeName = targetTypeName.IDENTIFIER(0).GetText();

            // Look up the implementing type (could be struct or enum)
            implementingType = _symbols.LookupStruct(typeName) ?? (IrType?)_symbols.LookupEnum(typeName);

            if (implementingType == null)
            {
                var errorLocation = GetLocation(errorContext);
                _diagnostics.ReportError(
                    ErrorCodes.TypeNotFound,
                    $"Type '{typeName}' not found for impl block",
                    errorLocation
                );
                return (null, null);
            }
        }
        else
        {
            throw new CompilerBugException(
                "Both targetTypeCtx and targetTypeName are null in ParseImplTargetType",
                "ParseImplTargetType",
                _inputFilePath,
                null
            );
        }

        return (typeName, implementingType);
    }
}
