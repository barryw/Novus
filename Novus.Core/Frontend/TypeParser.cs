using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Novus.Frontend.Generics;

namespace Novus.Frontend;

/// <summary>
/// Context interface for type parsing - allows different implementations
/// (IrBuilder vs SemanticAnalyzer) to provide their own lookup/registration logic
/// </summary>
public interface ITypeParsingContext
{
    // Lookups
    IrType? LookupGenericParameter(string name);
    IrConstGenericParam? LookupConstGenericParameter(string name);
    IrStructType? LookupStruct(string name);
    IrEnumType? LookupEnum(string name);
    IrStructType? LookupMonomorphizedStruct(string cacheKey);
    IrEnumType? LookupMonomorphizedEnum(string cacheKey);

    // Registration
    void RegisterMonomorphizedStruct(string key, IrStructType type);
    void RegisterMonomorphizedEnum(string key, IrEnumType type);

    // Finalization (called after fields are fully populated)
    void FinalizeMonomorphizedStruct(IrStructType type);
    void FinalizeMonomorphizedEnum(IrEnumType type);

    // Type interning (for reference/pointer types)
    IrType GetReferenceType(IrType pointeeType);
    IrType GetMutReferenceType(IrType pointeeType);
    IrType GetPointerType(IrType pointeeType);
    IrType GetArrayType(IrType elementType, long length);
    IrType GetFunctionPointerType(List<IrType> paramTypes, IrType returnType);
    IrType GetTupleType(List<IrType> elementTypes);
    IrType GetClosureType(List<IrType> paramTypes, IrType returnType);

    // Current state (for generic instantiation)
    IrType? CurrentSelfType { get; }
    Dictionary<string, IrType>? CurrentTypeSubstitutions { get; }

    // Constant values (for array size evaluation)
    Dictionary<string, (IrType Type, object Value)> GetConstantValues();

    // Extern function parsing state
    bool IsParsingExternFunction { get; }

    // Error reporting hook (optional - null means throw exceptions)
    Action<string>? ErrorReporter { get; }
}

/// <summary>
/// Shared type parsing logic for both IrBuilder and SemanticAnalyzer.
/// Handles type contexts, generic instantiation, and monomorphization.
/// </summary>
/// <summary>
/// Result type for type parsing operations.
/// Allows callers to handle errors without exceptions.
/// </summary>
public readonly struct TypeParseResult
{
    public IrType? Type { get; }
    public string? Error { get; }
    public bool IsSuccess => Error == null;

    private TypeParseResult(IrType? type, string? error)
    {
        Type = type;
        Error = error;
    }

    public static TypeParseResult Ok(IrType type) => new(type, null);
    public static TypeParseResult Err(string error) => new(null, error);

    /// <summary>
    /// Get the type or throw if error.
    /// </summary>
    public IrType Unwrap()
    {
        if (Error != null)
            throw new TypeParseException(Error);
        return Type!;
    }

    /// <summary>
    /// Get the type or return a default value if error.
    /// </summary>
    public IrType UnwrapOr(IrType defaultValue)
    {
        return IsSuccess ? Type! : defaultValue;
    }
}

/// <summary>
/// Exception thrown when type parsing fails.
/// </summary>
public class TypeParseException : Exception
{
    public TypeParseException(string message) : base(message) { }
}

public class TypeParser : ITypeSubstitutionEngine
{
    private readonly ITypeParsingContext _context;

    // Track in-progress struct monomorphizations to detect when incomplete placeholders are being accessed
    // Key: cache key (e.g., "Vec<i32>"), Value: the placeholder struct being populated
    private readonly Dictionary<string, IrStructType> _inProgressStructs = new();

    public TypeParser(ITypeParsingContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Report an error using the context's error reporter or throw an exception.
    /// </summary>
    private void ReportError(string message)
    {
        if (_context.ErrorReporter != null)
        {
            _context.ErrorReporter(message);
        }
        else
        {
            throw new TypeParseException(message);
        }
    }

    /// <summary>
    /// Main entry point: parse any type context.
    /// Throws TypeParseException on error, or reports via ErrorReporter if configured.
    /// </summary>
    public IrType ParseType(NovusParser.TypeContext context)
    {
        return TryParseType(context).Unwrap();
    }

    /// <summary>
    /// Try to parse a type, returning a Result instead of throwing.
    /// Use this when you want to handle errors without exceptions.
    /// </summary>
    public TypeParseResult TryParseType(NovusParser.TypeContext context)
    {
        try
        {
            var type = context switch
            {
                NovusParser.ReferenceTypeContext refCtx => ParseReferenceType(refCtx),
                NovusParser.PointerTypeContext ptrCtx => ParsePointerType(ptrCtx),
                NovusParser.ArrayTypeWithSizeContext arrayWithSizeCtx => ParseArrayTypeWithSize(arrayWithSizeCtx),
                NovusParser.ArrayTypeInferredContext arrayInferredCtx => ParseArrayTypeInferred(arrayInferredCtx),
                NovusParser.UnitTypeContext _ => IrTupleType.Unit,
                NovusParser.TupleTypeContext tupleCtx => ParseTupleType(tupleCtx),
                NovusParser.FunctionPointerTypeContext fpCtx => ParseFunctionPointerType(fpCtx),
                NovusParser.ClosureTypeContext closureCtx => ParseClosureType(closureCtx),
                NovusParser.SelfTypeContext selfCtx => ResolveSelfType(),
                NovusParser.PrimitiveTypeContext primCtx => ParsePrimitiveType(primCtx),
                NovusParser.NamedTypeContext namedCtx => ParseNamedType(namedCtx),
                NovusParser.ConstIntTypeContext constIntCtx => ParseConstIntType(constIntCtx),
                NovusParser.ConstHexTypeContext constHexCtx => ParseConstHexType(constHexCtx),
                _ => throw new TypeParseException($"Unknown type context: {context.GetType().Name}")
            };
            return TypeParseResult.Ok(type);
        }
        catch (TypeParseException ex)
        {
            return TypeParseResult.Err(ex.Message);
        }
        catch (Exception ex)
        {
            return TypeParseResult.Err($"Internal error parsing type: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolve Self type (used in impl blocks and trait definitions)
    /// </summary>
    private IrType ResolveSelfType()
    {
        // If we have a current Self type (in impl block), resolve to it
        if (_context.CurrentSelfType != null)
        {
            return _context.CurrentSelfType;
        }

        // Otherwise, keep it as IrSelfType for trait definitions
        // This allows Self to be used in trait method signatures
        // It will be resolved when the trait is implemented
        return IrSelfType.Instance;
    }

    /// <summary>
    /// Parse reference type: &T or &var T
    /// </summary>
    private IrType ParseReferenceType(NovusParser.ReferenceTypeContext context)
    {
        var pointeeType = ParseType(context.type());

        // Check if this is a mutable reference (&var T) or immutable reference (&T)
        bool isMutable = context.KW_VAR() != null;

        return isMutable
            ? _context.GetMutReferenceType(pointeeType)
            : _context.GetReferenceType(pointeeType);
    }

    /// <summary>
    /// Parse pointer type: *T
    /// </summary>
    private IrType ParsePointerType(NovusParser.PointerTypeContext context)
    {
        var pointeeType = ParseType(context.type());
        return _context.GetPointerType(pointeeType);
    }

    /// <summary>
    /// Parse named type (struct, enum, or generic parameter)
    /// Handles generic instantiation (e.g., Vec<i32>, Option<*u8>)
    /// </summary>
    private IrType ParseNamedType(NovusParser.NamedTypeContext context)
    {
        var typeName = context.typeName().GetText();

        // Check if it's a generic type parameter (T, E, etc.)
        var genericParam = _context.LookupGenericParameter(typeName);
        if (genericParam != null)
        {
            // If we're inside a generic method instantiation and have a concrete type, use it
            if (_context.CurrentTypeSubstitutions != null &&
                _context.CurrentTypeSubstitutions.ContainsKey(typeName))
            {
                return _context.CurrentTypeSubstitutions[typeName];
            }
            return genericParam;
        }

        // ADDED: Check if we have a type substitution even if genericParam lookup failed
        // This can happen if we're instantiating a method body and generic params are in _currentTypeSubstitutions
        // but not yet registered in the symbol table
        if (_context.CurrentTypeSubstitutions != null &&
            _context.CurrentTypeSubstitutions.ContainsKey(typeName))
        {
            return _context.CurrentTypeSubstitutions[typeName];
        }

        // Check if it's a struct type
        var structType = _context.LookupStruct(typeName);
        if (structType != null)
        {
            // Handle generic instantiation (e.g., Vec<i32>)
            if (context.genericTypeArgs()?.typeList() != null)
            {
                return MonomorphizeStruct(structType, context);
            }

            return structType;
        }

        // Check if it's an enum type
        var enumType = _context.LookupEnum(typeName);
        if (enumType != null)
        {
            // Handle generic instantiation (e.g., Option<i32>)
            if (context.genericTypeArgs()?.typeList() != null)
            {
                return MonomorphizeEnum(enumType, context);
            }

            return enumType;
        }

        // Unknown type - if we're parsing an extern function, skip validation and return placeholder
        if (_context.IsParsingExternFunction)
        {
            return IrIntType.I32; // Placeholder type for extern function parameters/return
        }

        // Unknown type
        var errorMsg = $"unknown type '{typeName}'";
        if (_context.ErrorReporter != null)
        {
            _context.ErrorReporter(errorMsg);
            return IrIntType.I32; // Fallback for error recovery
        }
        throw new Exception(errorMsg);
    }

    /// <summary>
    /// Monomorphize a generic struct (e.g., Vec<T> -> Vec<i32>)
    /// Creates a concrete struct type with type parameters substituted
    /// </summary>
    private IrType MonomorphizeStruct(IrStructType structType, NovusParser.NamedTypeContext context)
    {
        // First, create a preliminary cache key using raw text to check for cycles
        // This prevents infinite recursion when the struct's type arguments reference the struct itself
        var rawTypeArgNames = context.genericTypeArgs()!.typeList()!.type().Select(t => t.GetText());
        var rawPreliminaryCacheKey = $"{structType.StructName}<{string.Join(",", rawTypeArgNames)}>";

        // If we have type substitutions, compute the "true" cache key by substituting generic params
        // This ensures Vec<T> with T=i32 maps to Vec<i32>, not Vec<T>
        string preliminaryCacheKey = rawPreliminaryCacheKey;
        if (_context.CurrentTypeSubstitutions != null && _context.CurrentTypeSubstitutions.Count > 0)
        {
            var substitutedArgNames = context.genericTypeArgs()!.typeList()!.type().Select(t => {
                var rawText = t.GetText();
                // Simple case: the type arg itself is a generic parameter (e.g., "T")
                if (_context.CurrentTypeSubstitutions.TryGetValue(rawText, out var substitutedType))
                {
                    return GetTypeCacheKey(substitutedType);
                }
                // Check if raw text contains any generic parameters needing substitution
                bool needsSubstitution = _context.CurrentTypeSubstitutions.Keys.Any(k => rawText.Contains(k));
                if (needsSubstitution)
                {
                    // Complex case: the type arg contains generic parameters (e.g., "HashMap<K,V>")
                    // We can't parse it yet (would cause infinite recursion), so do string substitution
                    var substitutedText = rawText;
                    foreach (var kvp in _context.CurrentTypeSubstitutions)
                    {
                        // Replace generic param with its concrete type's cache key
                        // Be careful to replace whole words only (K but not KV)
                        var pattern = $@"\b{kvp.Key}\b";
                        substitutedText = System.Text.RegularExpressions.Regex.Replace(
                            substitutedText, pattern, GetTypeCacheKey(kvp.Value));
                    }
                    return substitutedText;
                }
                return rawText;
            });
            preliminaryCacheKey = $"{structType.StructName}<{string.Join(",", substitutedArgNames)}>";
        }

        // Check cache first - this catches already-completed monomorphizations
        var cached = _context.LookupMonomorphizedStruct(preliminaryCacheKey);
        if (cached != null)
        {
            return cached;
        }

        // Create placeholder struct and register it BEFORE parsing type arguments
        // This breaks cycles when parsing recursive types
        var placeholderFields = new List<IrStructField>();
        var placeholderStruct = new IrStructType(
            structType.StructName,
            placeholderFields,
            null,  // No generic parameters on monomorphized type
            preliminaryCacheKey
        );
        _context.RegisterMonomorphizedStruct(preliminaryCacheKey, placeholderStruct);

        // Now parse type arguments (this can recurse safely because we've cached the placeholder)
        var typeArgs = new List<IrType>();
        foreach (var typeCtx in context.genericTypeArgs()!.typeList()!.type())
        {
            typeArgs.Add(ParseType(typeCtx));
        }

        // IMPORTANT: Set TypeArguments immediately after parsing so any code that looks up
        // this struct from the cache gets a struct with TypeArguments populated
        // This MUST happen BEFORE any other code can use the cached struct for method instantiation
        placeholderStruct.TypeArguments = typeArgs;

        // Create final cache key using actual parsed types
        var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
        var finalCacheKey = $"{structType.StructName}<{string.Join(",", typeArgKeys)}>";

        // If the final cache key is different (shouldn't happen often), check cache again
        if (finalCacheKey != preliminaryCacheKey)
        {
            cached = _context.LookupMonomorphizedStruct(finalCacheKey);
            if (cached != null)
            {
                return cached;
            }
        }

        // Validate type argument count matches generic parameter count
        if (typeArgs.Count != structType.GenericParameters.Count)
        {
            throw new ArgumentException(
                $"Type argument count mismatch for struct '{structType.StructName}': " +
                $"expected {structType.GenericParameters.Count} type arguments, got {typeArgs.Count}");
        }

        // Create monomorphized struct with concrete types
        var typeSubstitutions = new Dictionary<string, IrType>();
        for (int i = 0; i < structType.GenericParameters.Count; i++)
        {
            typeSubstitutions[structType.GenericParameters[i]] = typeArgs[i];
        }

        // Create monomorphized fields using recursive substitution
        var monomorphizedFields = new List<IrStructField>();
        bool fullyMonomorphized = true;

        foreach (var origField in structType.Fields)
        {
            var fieldType = SubstituteGenericTypes(origField.Type, typeSubstitutions);
            monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));

            // Check if field type is still generic
            if (ContainsGenericTypes(fieldType))
            {
                fullyMonomorphized = false;
            }
        }

        // Update the placeholder with the actual fields
        placeholderStruct.Fields.Clear();
        foreach (var field in monomorphizedFields)
        {
            placeholderStruct.Fields.Add(field);
        }

        // Note: TypeArguments was already set immediately after parsing type args (line 322)

        // Force calculation of field offsets only if fully monomorphized
        // If still contains generic types, offset calculation will happen later
        if (fullyMonomorphized)
        {
            _ = placeholderStruct.SizeInBytes;
        }

        // If we used a different final cache key, update the struct and register under that too
        if (finalCacheKey != preliminaryCacheKey)
        {
            // IMPORTANT: Update the struct's CacheKey to use the final (correct) cache key
            // The preliminary cache key was based on raw text, but the final one is based on actual types
            placeholderStruct.CacheKey = finalCacheKey;
            _context.RegisterMonomorphizedStruct(finalCacheKey, placeholderStruct);
        }

        // IMPORTANT: Only finalize if fully monomorphized (no generic types remain in fields)
        // This adds the struct to the module so it gets emitted in code generation
        // Partially monomorphized structs (like HashMapEntry<K,V> during HashMap<K,V> processing)
        // will be finalized later when they're fully instantiated with concrete types
        if (fullyMonomorphized)
        {
            _context.FinalizeMonomorphizedStruct(placeholderStruct);
        }

        return placeholderStruct;
    }

    /// <summary>
    /// Monomorphize a generic enum (e.g., Option<T> -> Option<i32>)
    /// Creates a concrete enum type with type parameters substituted
    /// </summary>
    private IrType MonomorphizeEnum(IrEnumType enumType, NovusParser.NamedTypeContext context)
    {
        // First, create a preliminary cache key using raw text to check for cycles
        // This prevents infinite recursion when the enum's type arguments reference the enum itself
        var rawTypeArgNames = context.genericTypeArgs()!.typeList()!.type().Select(t => t.GetText());
        var rawPreliminaryCacheKey = $"{enumType.EnumName}<{string.Join(",", rawTypeArgNames)}>";

        // If we have type substitutions, compute the "true" cache key by substituting generic params
        // This ensures Option<HashMap<K,V>> with K=u32,V=u32 maps to Option<HashMap<u32,u32>>
        string preliminaryCacheKey = rawPreliminaryCacheKey;
        if (_context.CurrentTypeSubstitutions != null && _context.CurrentTypeSubstitutions.Count > 0)
        {
            var substitutedArgNames = context.genericTypeArgs()!.typeList()!.type().Select(t => {
                var rawText = t.GetText();
                // Simple case: the type arg itself is a generic parameter (e.g., "K")
                if (_context.CurrentTypeSubstitutions.TryGetValue(rawText, out var substitutedType))
                {
                    return GetTypeCacheKey(substitutedType);
                }
                // Check if raw text contains any generic parameters needing substitution
                bool needsSubstitution = _context.CurrentTypeSubstitutions.Keys.Any(k => rawText.Contains(k));
                if (needsSubstitution)
                {
                    // Complex case: the type arg contains generic parameters (e.g., "HashMap<K,V>")
                    // We can't parse it yet (would cause infinite recursion), so do string substitution
                    var substitutedText = rawText;
                    foreach (var kvp in _context.CurrentTypeSubstitutions)
                    {
                        // Replace generic param with its concrete type's cache key
                        // Be careful to replace whole words only (K but not KV)
                        var pattern = $@"\b{kvp.Key}\b";
                        substitutedText = System.Text.RegularExpressions.Regex.Replace(
                            substitutedText, pattern, GetTypeCacheKey(kvp.Value));
                    }
                    return substitutedText;
                }
                return rawText;
            });
            preliminaryCacheKey = $"{enumType.EnumName}<{string.Join(",", substitutedArgNames)}>";
        }

        // Check cache first - this catches already-completed monomorphizations
        var cached = _context.LookupMonomorphizedEnum(preliminaryCacheKey);
        if (cached != null)
        {
            return cached;
        }

        // Create placeholder enum and register it BEFORE parsing type arguments
        // This breaks cycles when parsing recursive types like Result<String, DosError>
        // where DosError might contain Result in its variants
        var placeholderVariants = new List<IrEnumVariant>();
        var placeholderEnum = new IrEnumType(
            enumType.EnumName,
            placeholderVariants,
            null,  // No generic parameters on monomorphized type
            preliminaryCacheKey
        );
        _context.RegisterMonomorphizedEnum(preliminaryCacheKey, placeholderEnum);

        // Now parse type arguments (this can recurse safely because we've cached the placeholder)
        var typeArgs = new List<IrType>();
        foreach (var typeCtx in context.genericTypeArgs()!.typeList()!.type())
        {
            typeArgs.Add(ParseType(typeCtx));
        }

        // Create final cache key using actual parsed types (should match preliminary if substitutions were correct)
        var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
        var finalCacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";

        // If the final cache key is different, check cache again and also register under final key
        if (finalCacheKey != preliminaryCacheKey)
        {
            cached = _context.LookupMonomorphizedEnum(finalCacheKey);
            if (cached != null)
            {
                return cached;
            }
        }

        // Validate type argument count matches generic parameter count
        if (typeArgs.Count != enumType.GenericParameters.Count)
        {
            throw new ArgumentException(
                $"Type argument count mismatch for enum '{enumType.EnumName}': " +
                $"expected {enumType.GenericParameters.Count} type arguments, got {typeArgs.Count}");
        }

        // Create monomorphized enum with concrete types
        var typeSubstitutions = new Dictionary<string, IrType>();
        for (int i = 0; i < enumType.GenericParameters.Count; i++)
        {
            typeSubstitutions[enumType.GenericParameters[i]] = typeArgs[i];
        }

        // Create monomorphized variants
        var monomorphizedVariants = new List<IrEnumVariant>();
        bool fullyMonomorphized = true;
        foreach (var origVariant in enumType.Variants)
        {
            var monomorphizedData = new List<IrType>();
            foreach (var dataType in origVariant.AssociatedData)
            {
                var substitutedType = SubstituteGenericTypes(dataType, typeSubstitutions);
                monomorphizedData.Add(substitutedType);

                // Check if variant data type is still generic
                if (ContainsGenericTypes(substitutedType))
                {
                    fullyMonomorphized = false;
                }
            }
            monomorphizedVariants.Add(new IrEnumVariant(
                origVariant.Name,
                origVariant.Tag,
                monomorphizedData
            ));
        }

        // Update the placeholder with the actual variants
        placeholderEnum.Variants.Clear();
        foreach (var variant in monomorphizedVariants)
        {
            placeholderEnum.Variants.Add(variant);
        }

        // If we used a different final cache key, update the enum and register under that too
        if (finalCacheKey != preliminaryCacheKey)
        {
            // IMPORTANT: Update the enum's CacheKey to use the final (correct) cache key
            // The preliminary cache key was based on raw text, but the final one is based on actual types
            placeholderEnum.CacheKey = finalCacheKey;
            _context.RegisterMonomorphizedEnum(finalCacheKey, placeholderEnum);
        }

        // IMPORTANT: Only finalize if fully monomorphized (no generic types remain in variant data)
        // This adds the enum to the module so it gets emitted in code generation
        if (fullyMonomorphized)
        {
            _context.FinalizeMonomorphizedEnum(placeholderEnum);
        }

        return placeholderEnum;
    }

    /// <summary>
    /// Parse array type with explicit size: [u8; 100]
    /// </summary>
    private IrType ParseArrayTypeWithSize(NovusParser.ArrayTypeWithSizeContext context)
    {
        // Evaluate the size expression as a compile-time constant
        var sizeExpr = context.expression();

        // Convert typed constants to untyped for ConstantExpressionEvaluator
        var constants = _context.GetConstantValues()
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);

        var evaluator = new ConstantExpressionEvaluator(
            constants,
            errorMsg => {
                // Error handling - will be caught by semantic analyzer or reported later
                _context.ErrorReporter?.Invoke(errorMsg);
            }
        );

        var sizeValue = evaluator.Visit(sizeExpr);
        if (!sizeValue.HasValue)
        {
            sizeValue = 0; // fallback - error will be reported by semantic analyzer
        }

        var elementType = ParseType(context.type());
        return _context.GetArrayType(elementType, sizeValue.Value);
    }

    /// <summary>
    /// Parse array type with inferred/unsized: [i32]
    /// When used in type position (not initialization), this represents an unsized slice
    /// When used in initialization, size will be inferred from the initializer expression
    /// </summary>
    private IrType ParseArrayTypeInferred(NovusParser.ArrayTypeInferredContext context)
    {
        // For unsized/inferred size arrays, we create a placeholder with size -1
        // The actual size will be determined when we parse the array literal initializer
        // OR this represents an unsized slice type (runtime fat pointer)
        var elementType = ParseType(context.type());
        // Use size -1 as a sentinel value to indicate "size to be inferred" or "unsized slice"
        return _context.GetArrayType(elementType, -1);
    }

    /// <summary>
    /// Parse tuple type: (u8, u8, u8) or () for unit type
    /// </summary>
    private IrType ParseTupleType(NovusParser.TupleTypeContext context)
    {
        var typeContexts = context.type();

        // Unit type () has no elements
        if (typeContexts == null || typeContexts.Length == 0)
        {
            return IrTupleType.Unit;
        }

        var elementTypes = new List<IrType>();
        foreach (var typeCtx in typeContexts)
        {
            elementTypes.Add(ParseType(typeCtx));
        }

        return _context.GetTupleType(elementTypes);
    }

    /// <summary>
    /// Parse function pointer type: fn(i32, i32) -> i32
    /// </summary>
    private IrType ParseFunctionPointerType(NovusParser.FunctionPointerTypeContext context)
    {
        var paramTypes = new List<IrType>();

        if (context.typeList() != null)
        {
            foreach (var typeCtx in context.typeList().type())
            {
                paramTypes.Add(ParseType(typeCtx));
            }
        }

        var returnType = context.type() != null ? ParseType(context.type()) : IrVoidType.Instance;

        return _context.GetFunctionPointerType(paramTypes, returnType);
    }

    /// <summary>
    /// Parse closure type: closure(i32, i32) -> i32
    /// </summary>
    private IrType ParseClosureType(NovusParser.ClosureTypeContext context)
    {
        var paramTypes = new List<IrType>();

        if (context.typeList() != null)
        {
            foreach (var typeCtx in context.typeList().type())
            {
                paramTypes.Add(ParseType(typeCtx));
            }
        }

        var returnType = context.type() != null ? ParseType(context.type()) : IrVoidType.Instance;

        return _context.GetClosureType(paramTypes, returnType);
    }

    /// <summary>
    /// Parse const integer literal as a type argument (for const generics)
    /// e.g., SmallVec&lt;i32, 16&gt; - the 16 becomes IrConstGenericValue
    /// </summary>
    private IrType ParseConstIntType(NovusParser.ConstIntTypeContext context)
    {
        var text = context.INTEGER_LITERAL().GetText();
        var (value, constType) = AstParsingHelpers.ParseIntegerLiteral(text);
        return new IrConstGenericValue(constType, value);
    }

    /// <summary>
    /// Parse const hex literal as a type argument (for const generics)
    /// e.g., Buffer&lt;0x100&gt; - the 0x100 becomes IrConstGenericValue
    /// </summary>
    private IrType ParseConstHexType(NovusParser.ConstHexTypeContext context)
    {
        var text = context.HEX_LITERAL().GetText();
        var (value, constType) = AstParsingHelpers.ParseHexLiteral(text);
        return new IrConstGenericValue(constType, value);
    }

    /// <summary>
    /// Parse primitive type (u8, i32, bool, etc.)
    /// </summary>
    private IrType ParsePrimitiveType(NovusParser.PrimitiveTypeContext context)
    {
        var typeText = context.GetText();
        return typeText switch
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
            "f32" => IrFloatType.F32,
            "f64" => IrFloatType.F64,
            "fixed16" => IrFixedType.Fixed16,
            "fixed32" => IrFixedType.Fixed32,
            _ => throw new Exception($"Unknown primitive type: {typeText}")
        };
    }

    /// <summary>
    /// Check if a type contains any generic type parameters
    /// </summary>
    public bool ContainsGenericTypes(IrType type)
    {
        return ContainsGenericTypesInternal(type, new HashSet<IrType>(ReferenceEqualityComparer.Instance));
    }

    private bool ContainsGenericTypesInternal(IrType type, HashSet<IrType> visited)
    {
        // Prevent infinite recursion for recursive types (e.g., Node with *Node fields)
        if (!visited.Add(type))
            return false;

        return type switch
        {
            IrGenericType => true,
            IrPointerType ptrType => ContainsGenericTypesInternal(ptrType.PointeeType, visited),
            IrReferenceType refType => ContainsGenericTypesInternal(refType.PointeeType, visited),
            IrMutReferenceType mutRefType => ContainsGenericTypesInternal(mutRefType.PointeeType, visited),
            IrArrayType arrayType => ContainsGenericTypesInternal(arrayType.ElementType, visited),
            IrStructType structType => structType.Fields.Any(f => ContainsGenericTypesInternal(f.Type, visited)),
            IrEnumType enumType => enumType.Variants.Any(v => v.AssociatedData.Any(d => ContainsGenericTypesInternal(d, visited))),
            _ => false
        };
    }

    /// <summary>
    /// Check if two types are semantically equal
    /// This is needed because reference equality doesn't work for types that are constructed separately
    /// </summary>
    public bool TypesAreEqual(IrType a, IrType b)
    {
        // Fast path: reference equality
        if (ReferenceEquals(a, b)) return true;

        // Different type classes
        if (a.GetType() != b.GetType()) return false;

        // Generic types: compare parameter names
        if (a is IrGenericType gtA && b is IrGenericType gtB)
        {
            return gtA.ParameterName == gtB.ParameterName;
        }

        // Pointer types: compare pointee types recursively
        if (a is IrPointerType ptrA && b is IrPointerType ptrB)
        {
            return TypesAreEqual(ptrA.PointeeType, ptrB.PointeeType);
        }

        // Reference types: compare pointee types recursively
        if (a is IrReferenceType refA && b is IrReferenceType refB)
        {
            return TypesAreEqual(refA.PointeeType, refB.PointeeType);
        }

        // Mutable reference types: compare pointee types recursively
        if (a is IrMutReferenceType mutRefA && b is IrMutReferenceType mutRefB)
        {
            return TypesAreEqual(mutRefA.PointeeType, mutRefB.PointeeType);
        }

        // Array types: compare element type and length
        if (a is IrArrayType arrA && b is IrArrayType arrB)
        {
            return arrA.Length == arrB.Length && TypesAreEqual(arrA.ElementType, arrB.ElementType);
        }

        // Struct types: compare by name and cache key
        // We use cache key when available because it uniquely identifies monomorphized versions
        if (a is IrStructType structA && b is IrStructType structB)
        {
            if (structA.CacheKey != null && structB.CacheKey != null)
            {
                return structA.CacheKey == structB.CacheKey;
            }
            return structA.StructName == structB.StructName &&
                   structA.GenericParameters.Count == structB.GenericParameters.Count;
        }

        // Enum types: compare by name and cache key
        if (a is IrEnumType enumA && b is IrEnumType enumB)
        {
            if (enumA.CacheKey != null && enumB.CacheKey != null)
            {
                return enumA.CacheKey == enumB.CacheKey;
            }
            return enumA.EnumName == enumB.EnumName &&
                   enumA.GenericParameters.Count == enumB.GenericParameters.Count;
        }

        // Tuple types: compare element types recursively
        if (a is IrTupleType tupleA && b is IrTupleType tupleB)
        {
            if (tupleA.ElementTypes.Count != tupleB.ElementTypes.Count)
            {
                return false;
            }
            for (int i = 0; i < tupleA.ElementTypes.Count; i++)
            {
                if (!TypesAreEqual(tupleA.ElementTypes[i], tupleB.ElementTypes[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // For primitive types, reference equality should have caught it
        // but as a fallback, we consider them equal by default
        return false;
    }

    /// <summary>
    /// Recursively substitute generic type parameters with concrete types
    /// </summary>
    public IrType SubstituteGenericTypes(IrType type, Dictionary<string, IrType> substitutions)
    {
        // Use a visited set to prevent infinite recursion for self-referential structs
        return SubstituteGenericTypesInternal(type, substitutions, new HashSet<string>());
    }

    /// <summary>
    /// Internal implementation with cycle detection for self-referential types
    /// </summary>
    private IrType SubstituteGenericTypesInternal(IrType type, Dictionary<string, IrType> substitutions, HashSet<string> visitedStructs)
    {
        // Handle Self type - resolve to current implementing type
        if (type is IrSelfType)
        {
            if (_context.CurrentSelfType == null)
            {
                throw new Exception("'Self' type encountered outside of impl block context");
            }
            return _context.CurrentSelfType;
        }

        // Direct generic parameter substitution
        if (type is IrGenericType gt && substitutions.ContainsKey(gt.ParameterName))
        {
            return substitutions[gt.ParameterName];
        }

        // Pointer type substitution
        if (type is IrPointerType ptrType)
        {
            var substitutedPointee = SubstituteGenericTypesInternal(ptrType.PointeeType, substitutions, visitedStructs);
            if (substitutedPointee != ptrType.PointeeType)
            {
                return _context.GetPointerType(substitutedPointee);
            }
        }

        // Immutable reference type substitution
        if (type is IrReferenceType refType)
        {
            var substitutedPointee = SubstituteGenericTypesInternal(refType.PointeeType, substitutions, visitedStructs);
            if (substitutedPointee != refType.PointeeType)
            {
                return _context.GetReferenceType(substitutedPointee);
            }
        }

        // Mutable reference type substitution
        if (type is IrMutReferenceType mutRefType)
        {
            var substitutedPointee = SubstituteGenericTypesInternal(mutRefType.PointeeType, substitutions, visitedStructs);
            if (substitutedPointee != mutRefType.PointeeType)
            {
                return _context.GetMutReferenceType(substitutedPointee);
            }
        }

        // Array type substitution
        if (type is IrArrayType arrayType)
        {
            var substitutedElement = SubstituteGenericTypesInternal(arrayType.ElementType, substitutions, visitedStructs);
            if (substitutedElement != arrayType.ElementType)
            {
                return _context.GetArrayType(substitutedElement, arrayType.Length);
            }
        }

        // Tuple type substitution - tuples are special structs with element types
        if (type is IrTupleType tupleType)
        {
            bool anyChanged = false;
            var substitutedElements = new List<IrType>();

            foreach (var elementType in tupleType.ElementTypes)
            {
                var substitutedElement = SubstituteGenericTypesInternal(elementType, substitutions, visitedStructs);
                substitutedElements.Add(substitutedElement);
                if (!TypesAreEqual(substitutedElement, elementType))
                {
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                return _context.GetTupleType(substitutedElements);
            }
            return tupleType;
        }

        // Struct type substitution (recursive field substitution)
        if (type is IrStructType structType)
        {
            // Cycle detection: if we've already visited this struct, return it unchanged
            // This prevents infinite recursion for self-referential structs like LinkedList, Window, etc.
            var structKey = structType.CacheKey ?? structType.StructName;
            if (visitedStructs.Contains(structKey))
            {
                return structType;
            }
            visitedStructs.Add(structKey);

            // Check if this struct needs substitution. This can happen in two cases:
            // 1. The struct has generic parameters (e.g., HashMap<K, V> where K and V are unbound)
            // 2. The struct has type arguments that contain generic types (e.g., HashMap<K, V> where K and V come from an outer generic context)
            bool needsParameterSubstitution = false;


            // Case 1: Check if the struct has generic parameters that need substitution
            if (structType.GenericParameters.Count > 0)
            {
                // Check if any of the substitutions actually change generic to concrete
                foreach (var genericParam in structType.GenericParameters)
                {
                    if (substitutions.ContainsKey(genericParam))
                    {
                        var substType = substitutions[genericParam];
                        // Check if it's being replaced with a concrete (non-generic) type
                        if (!(substType is IrGenericType))
                        {
                            needsParameterSubstitution = true;
                            break;
                        }
                    }
                }
            }
            // Case 2: Check if the struct has type arguments that contain generic types
            else if (structType.TypeArguments != null && structType.TypeArguments.Count > 0)
            {
                // Check if any type argument is a generic type that needs substitution
                foreach (var typeArg in structType.TypeArguments)
                {
                    if (typeArg is IrGenericType gtArg && substitutions.ContainsKey(gtArg.ParameterName))
                    {
                        needsParameterSubstitution = true;
                        break;
                    }
                    // Also check nested generic types (like Option<K> where K needs substitution)
                    if (typeArg is IrStructType || typeArg is IrEnumType || typeArg is IrPointerType || typeArg is IrReferenceType)
                    {
                        // Recursively check if this type needs substitution
                        var substitutedTypeArg = SubstituteGenericTypesInternal(typeArg, substitutions, new HashSet<string>(visitedStructs));
                        if (!TypesAreEqual(substitutedTypeArg, typeArg))
                        {
                            needsParameterSubstitution = true;
                            break;
                        }
                    }
                }
            }

            // SPECIAL CASE: Struct has a CacheKey with generic type names but no TypeArguments
            // This can happen when a struct is referenced in a field like `entries: *HashMapEntry<K,V>`
            // The HashMapEntry gets a CacheKey of "HashMapEntry<K,V>" but TypeArguments is null/empty
            // We need to check if the CacheKey contains any of the substitution keys
            if (!needsParameterSubstitution && structType.GenericParameters.Count == 0 &&
                (structType.TypeArguments == null || structType.TypeArguments.Count == 0) &&
                structType.CacheKey != null && structType.CacheKey.Contains('<'))
            {
                // Extract type parameter names from the cache key
                // CacheKey format: "StructName<Type1,Type2,...>"
                var startIdx = structType.CacheKey.IndexOf('<');
                var endIdx = structType.CacheKey.LastIndexOf('>');
                if (startIdx >= 0 && endIdx > startIdx)
                {
                    var typeParamsStr = structType.CacheKey.Substring(startIdx + 1, endIdx - startIdx - 1);
                    var typeParamNames = typeParamsStr.Split(',').Select(s => s.Trim()).ToList();

                    // Check if any of these type parameter names are in our substitutions
                    bool anySubstituted = false;
                    var newTypeArgKeys = new List<string>();
                    var newTypeArgs = new List<IrType>();

                    foreach (var paramName in typeParamNames)
                    {
                        if (substitutions.ContainsKey(paramName))
                        {
                            var substType = substitutions[paramName];
                            newTypeArgs.Add(substType);
                            var substKey = GetTypeCacheKey(substType);
                            newTypeArgKeys.Add(substKey);
                            // Only mark as substituted if we're replacing with a different concrete type
                            // (not just replacing K with another generic K)
                            if (substKey != paramName)
                            {
                                anySubstituted = true;
                            }
                        }
                        else
                        {
                            // This type parameter is not being substituted
                            newTypeArgKeys.Add(paramName);
                            // Create a generic type for it
                            newTypeArgs.Add(new IrGenericType(paramName));
                        }
                    }

                    if (anySubstituted)
                    {
                        // Build the new cache key with substituted types
                        var newCacheKey = $"{structType.StructName}<{string.Join(",", newTypeArgKeys)}>";

                        // Check cache first
                        var cachedSubstituted = _context.LookupMonomorphizedStruct(newCacheKey);
                        if (cachedSubstituted != null)
                        {
                            return cachedSubstituted;
                        }

                        // Substitute field types with the new substitutions
                        var newSubstitutedFields = new List<IrStructField>();
                        foreach (var field in structType.Fields)
                        {
                            var substitutedFieldType = SubstituteGenericTypesInternal(field.Type, substitutions, visitedStructs);
                            newSubstitutedFields.Add(new IrStructField(field.Name, substitutedFieldType));
                        }

                        // Determine remaining generic parameters
                        var remainingGenericParams = new List<string>();
                        foreach (var paramName in typeParamNames)
                        {
                            if (!substitutions.ContainsKey(paramName) || substitutions[paramName] is IrGenericType)
                            {
                                remainingGenericParams.Add(paramName);
                            }
                        }

                        // Create the new struct
                        var substitutedStruct = new IrStructType(
                            structType.StructName,
                            newSubstitutedFields,
                            remainingGenericParams,
                            newCacheKey,
                            structType.Attributes,
                            structType.WhereClause,
                            newTypeArgs
                        );

                        // Register and finalize
                        _context.RegisterMonomorphizedStruct(newCacheKey, substitutedStruct);

                        bool fullyMonomorphized = !ContainsGenericTypes(substitutedStruct);
                        if (fullyMonomorphized)
                        {
                            _context.FinalizeMonomorphizedStruct(substitutedStruct);
                        }

                        return substitutedStruct;
                    }
                }
            }

            // If no generic parameters or type arguments need substitution, return the original struct unchanged
            if (!needsParameterSubstitution && structType.GenericParameters.Count == 0 &&
                (structType.TypeArguments == null || structType.TypeArguments.Count == 0))
            {
                return structType;
            }

            // Check if any field types contain generics that need substitution
            bool needsSubstitution = false;
            var substitutedFields = new List<IrStructField>();

            foreach (var field in structType.Fields)
            {
                var substitutedFieldType = SubstituteGenericTypesInternal(field.Type, substitutions, visitedStructs);
                substitutedFields.Add(new IrStructField(field.Name, substitutedFieldType));

                if (!TypesAreEqual(substitutedFieldType, field.Type))
                {
                    needsSubstitution = true;
                }
            }

            // If either fields need substitution OR type arguments need substitution, create a new struct
            if (needsSubstitution || needsParameterSubstitution)
            {
                // Create a new struct type with substituted field types
                // Check if ALL generic parameters have been substituted with concrete types
                var remainingGenericParams = new List<string>();
                foreach (var genericParam in structType.GenericParameters)
                {
                    if (!substitutions.ContainsKey(genericParam) || substitutions[genericParam] is IrGenericType)
                    {
                        // This parameter wasn't substituted or was substituted with another generic
                        remainingGenericParams.Add(genericParam);
                    }
                }

                // Generate cache key and type arguments
                string? cacheKey = null;
                List<IrType>? typeArguments = null;

                // Case 1: We started with a generic struct (has GenericParameters) and fully substituted it
                if (remainingGenericParams.Count == 0 && structType.GenericParameters.Count > 0)
                {
                    // Fully monomorphized from generic - build type arguments from substitutions
                    typeArguments = new List<IrType>();
                    var typeArgKeys = new List<string>();
                    foreach (var p in structType.GenericParameters)
                    {
                        if (substitutions.ContainsKey(p))
                        {
                            typeArguments.Add(substitutions[p]);
                            typeArgKeys.Add(GetTypeCacheKey(substitutions[p]));
                        }
                        else
                        {
                            typeArgKeys.Add(p);
                        }
                    }
                    cacheKey = $"{structType.StructName}<{string.Join(",", typeArgKeys)}>";
                }
                // Case 2: We started with an already-monomorphized struct (has TypeArguments) and substituted within it
                else if (structType.TypeArguments != null && structType.TypeArguments.Count > 0)
                {
                    // Substitute within the existing type arguments
                    typeArguments = new List<IrType>();
                    var typeArgKeys = new List<string>();
                    foreach (var typeArg in structType.TypeArguments)
                    {
                        // Use a fresh visited set to allow re-substitution of nested types
                        var substitutedTypeArg = SubstituteGenericTypesInternal(typeArg, substitutions, new HashSet<string>());
                        typeArguments.Add(substitutedTypeArg);
                        typeArgKeys.Add(GetTypeCacheKey(substitutedTypeArg));
                    }
                    cacheKey = $"{structType.StructName}<{string.Join(",", typeArgKeys)}>";
                }

                // Check cache first to avoid creating duplicate structs
                if (cacheKey != null)
                {
                    var cachedSubstituted = _context.LookupMonomorphizedStruct(cacheKey);
                    if (cachedSubstituted != null)
                    {
                        return cachedSubstituted;
                    }
                }

                var substitutedStruct = new IrStructType(
                    structType.StructName,
                    substitutedFields,
                    remainingGenericParams,  // Use the remaining generic parameters, not the original list
                    cacheKey,
                    structType.Attributes,
                    structType.WhereClause,
                    typeArguments  // Pass type arguments for monomorphized types
                );

                // Register and finalize the substituted struct if it's fully monomorphized
                // This ensures it gets added to the module for code generation
                if (cacheKey != null)
                {
                    _context.RegisterMonomorphizedStruct(cacheKey, substitutedStruct);

                    // Only finalize if fully monomorphized (check both fields and type arguments)
                    bool fullyMonomorphizedSubst = !ContainsGenericTypes(substitutedStruct);
                    if (fullyMonomorphizedSubst)
                    {
                        _context.FinalizeMonomorphizedStruct(substitutedStruct);
                    }
                }

                return substitutedStruct;
            }
        }

        // Enum type substitution (recursive variant substitution)
        if (type is IrEnumType enumType)
        {
            // Check if this enum needs substitution. This can happen in two cases:
            // 1. The enum has generic parameters (e.g., Option<T> where T is unbound)
            // 2. The enum has type arguments that contain generic types (e.g., Option<K> where K comes from an outer generic context)
            bool needsParameterSubstitution = false;

            // Case 1: Check if the enum has generic parameters that need substitution
            if (enumType.GenericParameters.Count > 0)
            {
                // Check if any of the substitutions actually change generic to concrete
                foreach (var genericParam in enumType.GenericParameters)
                {
                    if (substitutions.ContainsKey(genericParam))
                    {
                        var substType = substitutions[genericParam];
                        // Check if it's being replaced with a concrete (non-generic) type
                        if (!(substType is IrGenericType))
                        {
                            needsParameterSubstitution = true;
                            break;
                        }
                    }
                }
            }
            // Case 2: Check if the enum has type arguments that contain generic types
            else if (enumType.TypeArguments != null && enumType.TypeArguments.Count > 0)
            {
                // Check if any type argument is a generic type that needs substitution
                foreach (var typeArg in enumType.TypeArguments)
                {
                    if (typeArg is IrGenericType gtArg && substitutions.ContainsKey(gtArg.ParameterName))
                    {
                        needsParameterSubstitution = true;
                        break;
                    }
                    // Also check nested generic types (like HashMap<K, V> where K and V need substitution)
                    if (typeArg is IrStructType || typeArg is IrEnumType || typeArg is IrPointerType || typeArg is IrReferenceType)
                    {
                        // Recursively check if this type needs substitution
                        var substitutedTypeArg = SubstituteGenericTypesInternal(typeArg, substitutions, new HashSet<string>(visitedStructs));
                        if (!TypesAreEqual(substitutedTypeArg, typeArg))
                        {
                            needsParameterSubstitution = true;
                            break;
                        }
                    }
                }
            }

            // If no generic parameters or type arguments need substitution, check variants anyway
            // because even fully concrete enums might have variants with generic data
            // (Actually, we should always check variants if we have any possibility of substitution)
            if (!needsParameterSubstitution && enumType.GenericParameters.Count == 0 &&
                (enumType.TypeArguments == null || enumType.TypeArguments.Count == 0))
            {
                // Still check if variants contain generics (they shouldn't, but be safe)
                // If there are no generics anywhere, we can safely return unchanged
                bool hasGenericInVariants = enumType.Variants.Any(v =>
                    v.AssociatedData.Any(d => d is IrGenericType));
                if (!hasGenericInVariants)
                {
                    return enumType;
                }
            }

            // Check if any variant types contain generics that need substitution
            bool needsSubstitution = false;
            var substitutedVariants = new List<IrEnumVariant>();

            foreach (var variant in enumType.Variants)
            {
                var substitutedData = new List<IrType>();
                foreach (var dataType in variant.AssociatedData)
                {
                    var substitutedDataType = SubstituteGenericTypesInternal(dataType, substitutions, visitedStructs);
                    substitutedData.Add(substitutedDataType);

                    if (!TypesAreEqual(substitutedDataType, dataType))
                    {
                        needsSubstitution = true;
                    }
                }

                substitutedVariants.Add(new IrEnumVariant(variant.Name, variant.Tag, substitutedData));
            }

            // If either variants need substitution OR type arguments need substitution, create a new enum
            if (needsSubstitution || needsParameterSubstitution)
            {
                // Create a new enum type with substituted variant types
                // Check if ALL generic parameters have been substituted with concrete types
                var remainingGenericParams = new List<string>();
                foreach (var genericParam in enumType.GenericParameters)
                {
                    if (!substitutions.ContainsKey(genericParam) || substitutions[genericParam] is IrGenericType)
                    {
                        // This parameter wasn't substituted or was substituted with another generic
                        remainingGenericParams.Add(genericParam);
                    }
                }

                // Generate cache key and type arguments
                string? cacheKey = null;
                List<IrType>? typeArguments = null;

                // Case 1: We started with a generic enum (has GenericParameters) and fully substituted it
                if (remainingGenericParams.Count == 0 && enumType.GenericParameters.Count > 0)
                {
                    // Fully monomorphized from generic - build type arguments from substitutions
                    typeArguments = new List<IrType>();
                    var typeArgKeys = new List<string>();
                    foreach (var p in enumType.GenericParameters)
                    {
                        if (substitutions.ContainsKey(p))
                        {
                            typeArguments.Add(substitutions[p]);
                            typeArgKeys.Add(GetTypeCacheKey(substitutions[p]));
                        }
                        else
                        {
                            typeArgKeys.Add(p);
                        }
                    }
                    cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";
                }
                // Case 2: We started with an already-monomorphized enum (has TypeArguments) and substituted within it
                else if (enumType.TypeArguments != null && enumType.TypeArguments.Count > 0)
                {
                    // Substitute within the existing type arguments
                    typeArguments = new List<IrType>();
                    var typeArgKeys = new List<string>();
                    foreach (var typeArg in enumType.TypeArguments)
                    {
                        // Use a fresh visited set to allow re-substitution of nested types
                        var substitutedTypeArg = SubstituteGenericTypesInternal(typeArg, substitutions, new HashSet<string>());
                        typeArguments.Add(substitutedTypeArg);
                        typeArgKeys.Add(GetTypeCacheKey(substitutedTypeArg));
                    }
                    cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";
                }

                // Check cache first to avoid creating duplicate enums
                if (cacheKey != null)
                {
                    var cachedSubstituted = _context.LookupMonomorphizedEnum(cacheKey);
                    if (cachedSubstituted != null)
                    {
                        return cachedSubstituted;
                    }
                }

                var substitutedEnum = new IrEnumType(
                    enumType.EnumName,
                    substitutedVariants,
                    remainingGenericParams,  // Use the remaining generic parameters, not the original list
                    cacheKey,
                    enumType.Attributes,
                    enumType.WhereClause,
                    typeArguments  // Pass type arguments for monomorphized types
                );

                // Register and finalize the substituted enum if it's fully monomorphized
                // This ensures it gets added to the module for code generation
                if (cacheKey != null)
                {
                    _context.RegisterMonomorphizedEnum(cacheKey, substitutedEnum);

                    // Only finalize if fully monomorphized (check both variants and type arguments)
                    bool fullyMonomorphizedSubst = !ContainsGenericTypes(substitutedEnum);
                    if (fullyMonomorphizedSubst)
                    {
                        _context.FinalizeMonomorphizedEnum(substitutedEnum);
                    }
                }

                return substitutedEnum;
            }
        }

        return type;
    }

    /// <summary>
    /// Get a cache key for a type (used for monomorphization caching)
    /// Handles nested generics properly
    /// </summary>
    public string GetTypeCacheKey(IrType type)
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
        else if (type is IrStructType structType)
        {
            // Similar logic for struct types
            if (structType.GenericParameters.Count > 0)
            {
                return $"{structType.StructName}<{string.Join(",", structType.GenericParameters)}>";
            }
            else if (structType.CacheKey != null)
            {
                return structType.CacheKey;
            }
            return structType.StructName;
        }
        else if (type is IrGenericType gt)
        {
            return gt.ParameterName;
        }
        else if (type is IrConstGenericParam cgp)
        {
            return cgp.ParameterName;
        }
        else if (type is IrConstGenericValue cgv)
        {
            // For const generic values, include the value in the cache key
            // e.g., SmallVec<i32, 16> vs SmallVec<i32, 32> should have different cache keys
            return cgv.Value?.ToString() ?? "0";
        }
        else if (type is IrPointerType ptrType)
        {
            return $"*{GetTypeCacheKey(ptrType.PointeeType)}";
        }
        else if (type is IrReferenceType refType)
        {
            return $"&{GetTypeCacheKey(refType.PointeeType)}";
        }
        else if (type is IrMutReferenceType mutRefType)
        {
            return $"&var {GetTypeCacheKey(mutRefType.PointeeType)}";
        }
        else
        {
            return type.Name;
        }
    }
}
