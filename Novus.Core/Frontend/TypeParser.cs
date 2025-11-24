using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Frontend;

/// <summary>
/// Context interface for type parsing - allows different implementations
/// (IrBuilder vs SemanticAnalyzer) to provide their own lookup/registration logic
/// </summary>
public interface ITypeParsingContext
{
    // Lookups
    IrType? LookupGenericParameter(string name);
    IrStructType? LookupStruct(string name);
    IrEnumType? LookupEnum(string name);
    IrStructType? LookupMonomorphizedStruct(string cacheKey);
    IrEnumType? LookupMonomorphizedEnum(string cacheKey);

    // Registration
    void RegisterMonomorphizedStruct(string key, IrStructType type);
    void RegisterMonomorphizedEnum(string key, IrEnumType type);

    // Type interning (for reference/pointer types)
    IrType GetReferenceType(IrType pointeeType);
    IrType GetMutReferenceType(IrType pointeeType);
    IrType GetPointerType(IrType pointeeType);
    IrType GetArrayType(IrType elementType, long length);
    IrType GetFunctionPointerType(List<IrType> paramTypes, IrType returnType);
    IrType GetTupleType(List<IrType> elementTypes);

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
public class TypeParser
{
    private readonly ITypeParsingContext _context;

    public TypeParser(ITypeParsingContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Main entry point: parse any type context
    /// </summary>
    public IrType ParseType(NovusParser.TypeContext context)
    {
        return context switch
        {
            NovusParser.ReferenceTypeContext refCtx => ParseReferenceType(refCtx),
            NovusParser.PointerTypeContext ptrCtx => ParsePointerType(ptrCtx),
            NovusParser.ArrayTypeWithSizeContext arrayWithSizeCtx => ParseArrayTypeWithSize(arrayWithSizeCtx),
            NovusParser.ArrayTypeInferredContext arrayInferredCtx => ParseArrayTypeInferred(arrayInferredCtx),
            NovusParser.UnitTypeContext _ => IrTupleType.Unit,
            NovusParser.TupleTypeContext tupleCtx => ParseTupleType(tupleCtx),
            NovusParser.FunctionPointerTypeContext fpCtx => ParseFunctionPointerType(fpCtx),
            NovusParser.SelfTypeContext selfCtx => ResolveSelfType(),
            NovusParser.PrimitiveTypeContext primCtx => ParsePrimitiveType(primCtx),
            NovusParser.NamedTypeContext namedCtx => ParseNamedType(namedCtx),
            _ => throw new Exception($"Unknown type context: {context.GetType().Name}")
        };
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
    /// Parse reference type: &T or &mut T
    /// </summary>
    private IrType ParseReferenceType(NovusParser.ReferenceTypeContext context)
    {
        var pointeeType = ParseType(context.type());

        // Check if this is a mutable reference (&mut T) or immutable reference (&T)
        bool isMutable = context.GetChild(1)?.GetText() == "mut";

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

        // Check if it's a struct type
        var structType = _context.LookupStruct(typeName);
        if (structType != null)
        {
            // Handle generic instantiation (e.g., Vec<i32>)
            if (context.typeList() != null)
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
            if (context.typeList() != null)
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
        // First, create a preliminary cache key to check if we're already processing this type
        // This prevents infinite recursion when the struct's type arguments reference the struct itself
        var typeArgNames = context.typeList()!.type().Select(t => t.GetText());
        var preliminaryCacheKey = $"{structType.StructName}<{string.Join(",", typeArgNames)}>";

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
        foreach (var typeCtx in context.typeList()!.type())
        {
            typeArgs.Add(ParseType(typeCtx));
        }

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

        // Force calculation of field offsets only if fully monomorphized
        // If still contains generic types, offset calculation will happen later
        if (fullyMonomorphized)
        {
            _ = placeholderStruct.SizeInBytes;
        }

        // If we used a different final cache key, register under that too
        if (finalCacheKey != preliminaryCacheKey)
        {
            _context.RegisterMonomorphizedStruct(finalCacheKey, placeholderStruct);
        }

        return placeholderStruct;
    }

    /// <summary>
    /// Monomorphize a generic enum (e.g., Option<T> -> Option<i32>)
    /// Creates a concrete enum type with type parameters substituted
    /// </summary>
    private IrType MonomorphizeEnum(IrEnumType enumType, NovusParser.NamedTypeContext context)
    {
        // First, create a preliminary cache key to check if we're already processing this type
        // This prevents infinite recursion when the enum's type arguments reference the enum itself
        var typeArgNames = context.typeList()!.type().Select(t => t.GetText());
        var preliminaryCacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgNames)}>";

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
        foreach (var typeCtx in context.typeList()!.type())
        {
            typeArgs.Add(ParseType(typeCtx));
        }

        // Create final cache key using actual parsed types
        var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
        var finalCacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";

        // If the final cache key is different (shouldn't happen often), check cache again
        if (finalCacheKey != preliminaryCacheKey)
        {
            cached = _context.LookupMonomorphizedEnum(finalCacheKey);
            if (cached != null)
            {
                return cached;
            }
        }

        // Create monomorphized enum with concrete types
        var typeSubstitutions = new Dictionary<string, IrType>();
        for (int i = 0; i < enumType.GenericParameters.Count; i++)
        {
            typeSubstitutions[enumType.GenericParameters[i]] = typeArgs[i];
        }

        // Create monomorphized variants
        var monomorphizedVariants = new List<IrEnumVariant>();
        foreach (var origVariant in enumType.Variants)
        {
            var monomorphizedData = new List<IrType>();
            foreach (var dataType in origVariant.AssociatedData)
            {
                var substitutedType = SubstituteGenericTypes(dataType, typeSubstitutions);
                monomorphizedData.Add(substitutedType);
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

        // If we used a different final cache key, register under that too
        if (finalCacheKey != preliminaryCacheKey)
        {
            _context.RegisterMonomorphizedEnum(finalCacheKey, placeholderEnum);
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
        return type switch
        {
            IrGenericType => true,
            IrPointerType ptrType => ContainsGenericTypes(ptrType.PointeeType),
            IrReferenceType refType => ContainsGenericTypes(refType.PointeeType),
            IrMutReferenceType mutRefType => ContainsGenericTypes(mutRefType.PointeeType),
            IrArrayType arrayType => ContainsGenericTypes(arrayType.ElementType),
            IrStructType structType => structType.Fields.Any(f => ContainsGenericTypes(f.Type)),
            IrEnumType enumType => enumType.Variants.Any(v => v.AssociatedData.Any(ContainsGenericTypes)),
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

        // For primitive types, reference equality should have caught it
        // but as a fallback, we consider them equal by default
        return false;
    }

    /// <summary>
    /// Recursively substitute generic type parameters with concrete types
    /// </summary>
    public IrType SubstituteGenericTypes(IrType type, Dictionary<string, IrType> substitutions)
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
            var substitutedPointee = SubstituteGenericTypes(ptrType.PointeeType, substitutions);
            if (substitutedPointee != ptrType.PointeeType)
            {
                return _context.GetPointerType(substitutedPointee);
            }
        }

        // Immutable reference type substitution
        if (type is IrReferenceType refType)
        {
            var substitutedPointee = SubstituteGenericTypes(refType.PointeeType, substitutions);
            if (substitutedPointee != refType.PointeeType)
            {
                return _context.GetReferenceType(substitutedPointee);
            }
        }

        // Mutable reference type substitution
        if (type is IrMutReferenceType mutRefType)
        {
            var substitutedPointee = SubstituteGenericTypes(mutRefType.PointeeType, substitutions);
            if (substitutedPointee != mutRefType.PointeeType)
            {
                return _context.GetMutReferenceType(substitutedPointee);
            }
        }

        // Array type substitution
        if (type is IrArrayType arrayType)
        {
            var substitutedElement = SubstituteGenericTypes(arrayType.ElementType, substitutions);
            if (substitutedElement != arrayType.ElementType)
            {
                return _context.GetArrayType(substitutedElement, arrayType.Length);
            }
        }

        // Struct type substitution (recursive field substitution)
        if (type is IrStructType structType)
        {
            // If the struct still has generic parameters and we're in a generic context,
            // we should not create a new struct type - just return the original
            // This prevents creating duplicate generic struct instances
            if (structType.GenericParameters.Count > 0)
            {
                // Check if any of the substitutions actually change generic to concrete
                bool hasConcreteSubstitution = false;
                foreach (var genericParam in structType.GenericParameters)
                {
                    if (substitutions.ContainsKey(genericParam))
                    {
                        var substType = substitutions[genericParam];
                        // Check if it's being replaced with a concrete (non-generic) type
                        if (!(substType is IrGenericType))
                        {
                            hasConcreteSubstitution = true;
                            break;
                        }
                        else
                        {
                        }
                    }
                }

                // If no generic parameters are being replaced with concrete types,
                // return the original struct unchanged
                if (!hasConcreteSubstitution)
                {
                    return structType;
                }
            }

            // Check if any field types contain generics that need substitution
            bool needsSubstitution = false;
            var substitutedFields = new List<IrStructField>();

            foreach (var field in structType.Fields)
            {
                var substitutedFieldType = SubstituteGenericTypes(field.Type, substitutions);
                substitutedFields.Add(new IrStructField(field.Name, substitutedFieldType));

                if (!TypesAreEqual(substitutedFieldType, field.Type))
                {
                    needsSubstitution = true;
                }
            }

            if (needsSubstitution)
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

                // Generate cache key if fully monomorphized
                string? cacheKey = null;
                if (remainingGenericParams.Count == 0 && structType.GenericParameters.Count > 0)
                {
                    // Fully monomorphized - generate cache key
                    var typeArgKeys = structType.GenericParameters.Select(p =>
                        substitutions.ContainsKey(p) ? GetTypeCacheKey(substitutions[p]) : p);
                    cacheKey = $"{structType.StructName}<{string.Join(",", typeArgKeys)}>";
                }

                var substitutedStruct = new IrStructType(
                    structType.StructName,
                    substitutedFields,
                    remainingGenericParams,  // Use the remaining generic parameters, not the original list
                    cacheKey,
                    structType.Attributes,
                    structType.WhereClause
                );
                return substitutedStruct;
            }
        }

        // Enum type substitution (recursive variant substitution)
        if (type is IrEnumType enumType)
        {
            // If the enum still has generic parameters and we're in a generic context,
            // we should not create a new enum type - just return the original
            // This prevents creating duplicate generic enum instances
            if (enumType.GenericParameters.Count > 0)
            {
                // Check if any of the substitutions actually change generic to concrete
                bool hasConcreteSubstitution = false;
                foreach (var genericParam in enumType.GenericParameters)
                {
                    if (substitutions.ContainsKey(genericParam))
                    {
                        var substType = substitutions[genericParam];
                        // Check if it's being replaced with a concrete (non-generic) type
                        if (!(substType is IrGenericType))
                        {
                            hasConcreteSubstitution = true;
                            break;
                        }
                    }
                }

                // If no generic parameters are being replaced with concrete types,
                // return the original enum unchanged
                if (!hasConcreteSubstitution)
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
                    var substitutedDataType = SubstituteGenericTypes(dataType, substitutions);
                    substitutedData.Add(substitutedDataType);

                    if (!TypesAreEqual(substitutedDataType, dataType))
                    {
                        needsSubstitution = true;
                    }
                }

                substitutedVariants.Add(new IrEnumVariant(variant.Name, variant.Tag, substitutedData));
            }

            if (needsSubstitution)
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

                // Generate cache key if fully monomorphized
                string? cacheKey = null;
                if (remainingGenericParams.Count == 0)
                {
                    // Fully monomorphized - generate cache key
                    // BUG FIX: Even if enumType.GenericParameters.Count == 0, we may have substituted
                    // generic types in the variant data. Extract the actual type args from variant data.
                    if (enumType.GenericParameters.Count > 0)
                    {
                        // Original enum had generic parameters - use those
                        var typeArgKeys = enumType.GenericParameters.Select(p =>
                            substitutions.ContainsKey(p) ? GetTypeCacheKey(substitutions[p]) : p);
                        cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";
                    }
                    else
                    {
                        // Original enum had no generic parameters listed, but we substituted types in variant data
                        // Extract the type arguments from the SUBSTITUTED variant data
                        // This handles cases like Option<*T> where T gets substituted in the variant data to *u8
                        var typeArgKeys = new HashSet<string>();
                        foreach (var variant in substitutedVariants)
                        {
                            foreach (var dataType in variant.AssociatedData)
                            {
                                // Add the cache key for each concrete type in variant data
                                if (!(dataType is IrGenericType))
                                {
                                    typeArgKeys.Add(GetTypeCacheKey(dataType));
                                }
                            }
                        }
                        if (typeArgKeys.Count > 0)
                        {
                            cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys.OrderBy(x => x))}>";
                        }
                    }
                }

                var substitutedEnum = new IrEnumType(
                    enumType.EnumName,
                    substitutedVariants,
                    remainingGenericParams,  // Use the remaining generic parameters, not the original list
                    cacheKey
                );
                return substitutedEnum;
            }
        }

        return type;
    }

    /// <summary>
    /// Get a cache key for a type (used for monomorphization caching)
    /// Handles nested generics properly
    /// </summary>
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
            return $"&mut {GetTypeCacheKey(mutRefType.PointeeType)}";
        }
        else
        {
            return type.Name;
        }
    }
}
