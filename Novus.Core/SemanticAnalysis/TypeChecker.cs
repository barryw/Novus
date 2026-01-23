using Novus.Diagnostics;
using Novus.IR;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Type compatibility checking and coercion logic.
///
/// This class consolidates type checking operations that were previously scattered
/// across IrBuilder's partial class files. It provides:
/// - Pure type compatibility checks (no side effects)
/// - Coercion kind determination
/// - Type validation with error reporting
///
/// The TypeChecker is designed to be used during IR building for type validation,
/// and can also be used independently for type queries.
/// </summary>
public class TypeChecker
{
    private readonly DiagnosticBag _diagnostics;

    /// <summary>
    /// Coercion types supported by the Novus type system
    /// </summary>
    public enum CoercionKind
    {
        /// <summary>No coercion needed - types are identical</summary>
        None,

        /// <summary>Integer widening (e.g., i8 → i32)</summary>
        IntegerWidening,

        /// <summary>Integer narrowing (e.g., i32 → i8) - may lose precision</summary>
        IntegerNarrowing,

        /// <summary>Sign change (e.g., u32 → i32 or vice versa)</summary>
        SignChange,

        /// <summary>Pointer to bool coercion (null check)</summary>
        PointerToBool,

        /// <summary>Reference to pointer decay (e.g., &T → *T)</summary>
        ReferenceToPointer,

        /// <summary>Array to pointer decay (e.g., [T; N] → *T)</summary>
        ArrayToPointer,

        /// <summary>Sized array reference to slice (e.g., &[T; N] → Slice[T])</summary>
        ArrayRefToSlice,

        /// <summary>String to pointer (Str → *u8)</summary>
        StringToPointer,

        /// <summary>Value to reference (automatic borrowing)</summary>
        ValueToReference,

        /// <summary>Enum variant coercion (specific variant to general enum type)</summary>
        EnumVariant,

        /// <summary>Implicit deref coercion (&&T → &T)</summary>
        ImplicitDeref,

        /// <summary>Unit coercion (any expression in void context)</summary>
        ToUnit,
    }

    public TypeChecker(DiagnosticBag diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Check if two types are structurally identical.
    /// This is a strict equality check - no coercions are considered.
    /// </summary>
    public static bool TypesAreEqual(IrType a, IrType b)
    {
        if (ReferenceEquals(a, b))
            return true;

        // Handle null cases
        if (a == null || b == null)
            return false;

        // Same concrete type checks
        return (a, b) switch
        {
            (IrIntType ia, IrIntType ib) => ia.BitWidth == ib.BitWidth && ia.IsSigned == ib.IsSigned,
            (IrFloatType fa, IrFloatType fb) => fa.BitWidth == fb.BitWidth,
            (IrFixedType xa, IrFixedType xb) => xa.BitWidth == xb.BitWidth,
            (IrBoolType, IrBoolType) => true,
            (IrVoidType, IrVoidType) => true,
            (IrPointerType pa, IrPointerType pb) => TypesAreEqual(pa.PointeeType, pb.PointeeType),
            (IrReferenceType ra, IrReferenceType rb) => TypesAreEqual(ra.PointeeType, rb.PointeeType),
            (IrMutReferenceType ma, IrMutReferenceType mb) => TypesAreEqual(ma.PointeeType, mb.PointeeType),
            (IrArrayType aa, IrArrayType ab) => aa.Length == ab.Length && TypesAreEqual(aa.ElementType, ab.ElementType),
            (IrStructType sa, IrStructType sb) => StructTypesAreEqual(sa, sb),
            (IrEnumType ea, IrEnumType eb) => EnumTypesAreEqual(ea, eb),
            (IrTupleType ta, IrTupleType tb) => TupleTypesAreEqual(ta, tb),
            (IrFunctionPointerType fa, IrFunctionPointerType fb) => FunctionPointerTypesAreEqual(fa, fb),
            (IrGenericType ga, IrGenericType gb) => ga.ParameterName == gb.ParameterName,
            _ => false
        };
    }

    private static bool StructTypesAreEqual(IrStructType a, IrStructType b)
    {
        // For monomorphized structs, compare cache keys
        if (a.CacheKey != null && b.CacheKey != null)
            return a.CacheKey == b.CacheKey;

        // For non-generic structs, compare names
        if (a.GenericParameters is [] && b.GenericParameters is [])
            return a.StructName == b.StructName;

        // Generic struct with type arguments - compare name and type args
        return a.StructName == b.StructName &&
               a.GenericParameters.SequenceEqual(b.GenericParameters);
    }

    private static bool EnumTypesAreEqual(IrEnumType a, IrEnumType b)
    {
        // For monomorphized enums, compare cache keys
        if (a.CacheKey != null && b.CacheKey != null)
            return a.CacheKey == b.CacheKey;

        // For non-generic enums, compare names
        if (a.GenericParameters is [] && b.GenericParameters is [])
            return a.EnumName == b.EnumName;

        // Generic enum - compare name and parameters
        return a.EnumName == b.EnumName &&
               a.GenericParameters.SequenceEqual(b.GenericParameters);
    }

    private static bool TupleTypesAreEqual(IrTupleType a, IrTupleType b)
    {
        if (a.ElementTypes.Count != b.ElementTypes.Count)
            return false;

        for (int i = 0; i < a.ElementTypes.Count; i++)
        {
            if (!TypesAreEqual(a.ElementTypes[i], b.ElementTypes[i]))
                return false;
        }
        return true;
    }

    private static bool FunctionPointerTypesAreEqual(IrFunctionPointerType a, IrFunctionPointerType b)
    {
        if (!TypesAreEqual(a.ReturnType, b.ReturnType))
            return false;

        if (a.ParameterTypes.Count != b.ParameterTypes.Count)
            return false;

        for (int i = 0; i < a.ParameterTypes.Count; i++)
        {
            if (!TypesAreEqual(a.ParameterTypes[i], b.ParameterTypes[i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Check if a value of sourceType can be implicitly coerced to targetType.
    /// Returns the coercion kind if possible, or null if no implicit coercion exists.
    /// </summary>
    public static CoercionKind? GetImplicitCoercionKind(IrType sourceType, IrType targetType)
    {
        // Identical types need no coercion
        if (TypesAreEqual(sourceType, targetType))
            return CoercionKind.None;

        // Integer coercions
        if (sourceType is IrIntType sourceInt && targetType is IrIntType targetInt)
        {
            // Same width, different sign
            if (sourceInt.BitWidth == targetInt.BitWidth)
                return CoercionKind.SignChange;

            // Widening (always safe for same signedness)
            if (sourceInt.BitWidth < targetInt.BitWidth)
            {
                if (sourceInt.IsSigned == targetInt.IsSigned)
                    return CoercionKind.IntegerWidening;
                // Unsigned to larger signed is safe
                if (!sourceInt.IsSigned && targetInt.IsSigned)
                    return CoercionKind.IntegerWidening;
            }

            // Narrowing (may lose precision) - not implicit
            return null;
        }

        // Pointer to bool coercion (null check)
        if (targetType is IrBoolType &&
            (sourceType is IrPointerType || sourceType is IrReferenceType || sourceType is IrMutReferenceType))
        {
            return CoercionKind.PointerToBool;
        }

        // Reference to pointer decay
        if (targetType is IrPointerType targetPtr)
        {
            if (sourceType is IrReferenceType sourceRef &&
                TypesAreEqual(sourceRef.PointeeType, targetPtr.PointeeType))
            {
                return CoercionKind.ReferenceToPointer;
            }
            if (sourceType is IrMutReferenceType sourceMutRef &&
                TypesAreEqual(sourceMutRef.PointeeType, targetPtr.PointeeType))
            {
                return CoercionKind.ReferenceToPointer;
            }

            // Array to pointer decay
            if (sourceType is IrArrayType sourceArray &&
                TypesAreEqual(sourceArray.ElementType, targetPtr.PointeeType))
            {
                return CoercionKind.ArrayToPointer;
            }
        }

        // String types to *u8 (Str, String)
        if (targetType is IrPointerType targetPtrU8 &&
            targetPtrU8.PointeeType is IrIntType u8Type &&
            u8Type.BitWidth == 8 && !u8Type.IsSigned)
        {
            if (sourceType is IrStructType sourceStruct &&
                (sourceStruct.StructName == "Str" || sourceStruct.StructName == "String"))
            {
                return CoercionKind.StringToPointer;
            }
        }

        // Value to reference (automatic borrowing for method calls)
        if (targetType is IrReferenceType targetRef &&
            TypesAreEqual(sourceType, targetRef.PointeeType))
        {
            return CoercionKind.ValueToReference;
        }
        if (targetType is IrMutReferenceType targetMutRef &&
            TypesAreEqual(sourceType, targetMutRef.PointeeType))
        {
            return CoercionKind.ValueToReference;
        }

        // Unit coercion (void context)
        if (targetType is IrVoidType || targetType is IrTupleType tupleType && tupleType.ElementTypes is [])
        {
            return CoercionKind.ToUnit;
        }

        // No implicit coercion available
        return null;
    }

    /// <summary>
    /// Check if sourceType can be assigned to a variable/parameter of targetType.
    /// This includes implicit coercions that are safe in Novus.
    /// </summary>
    public bool CanAssign(IrType targetType, IrType sourceType)
    {
        var coercion = GetImplicitCoercionKind(sourceType, targetType);
        return coercion != null;
    }

    /// <summary>
    /// Check if two integer types are compatible for binary operations.
    /// Returns true if they can be used together (after potential promotion).
    /// </summary>
    public static bool IntegerTypesAreCompatible(IrIntType a, IrIntType b)
    {
        // Same type - always compatible
        if (a.BitWidth == b.BitWidth && a.IsSigned == b.IsSigned)
            return true;

        // Same signedness, different widths - compatible (smaller promotes)
        if (a.IsSigned == b.IsSigned)
            return true;

        // Mixed signedness with same width - compatible but may need explicit cast
        if (a.BitWidth == b.BitWidth)
            return true;

        // Different signedness, different widths - need explicit cast
        return false;
    }

    /// <summary>
    /// Get the common type for two integer types in a binary operation.
    /// Returns the type that both operands should be promoted to.
    /// </summary>
    public static IrIntType GetCommonIntegerType(IrIntType a, IrIntType b)
    {
        // Same type - return either
        if (a.BitWidth == b.BitWidth && a.IsSigned == b.IsSigned)
            return a;

        // Different widths - promote to larger
        if (a.BitWidth != b.BitWidth)
        {
            var larger = a.BitWidth > b.BitWidth ? a : b;
            var smaller = a.BitWidth > b.BitWidth ? b : a;

            // If signedness matches, use larger type
            if (larger.IsSigned == smaller.IsSigned)
                return larger;

            // Mixed signedness: if larger is signed and can hold unsigned, use signed
            // Otherwise, use unsigned (may lose negative values)
            return larger;
        }

        // Same width, different signedness - prefer signed (matches C semantics)
        return a.IsSigned ? a : b.IsSigned ? b : a;
    }

    /// <summary>
    /// Validate that an assignment is type-safe and report an error if not.
    /// </summary>
    public bool ValidateAssignment(IrType targetType, IrType sourceType, SourceLocation location)
    {
        if (CanAssign(targetType, sourceType))
            return true;

        _diagnostics.ReportError(
            ErrorCodes.TypeMismatch,
            $"Cannot assign value of type '{sourceType.Name}' to variable of type '{targetType.Name}'",
            location);
        return false;
    }

    /// <summary>
    /// Check if a type is a numeric type (integer, float, or fixed-point).
    /// </summary>
    public static bool IsNumericType(IrType type)
    {
        return type is IrIntType or IrFloatType or IrFixedType;
    }

    /// <summary>
    /// Check if a type is an integer type.
    /// </summary>
    public static bool IsIntegerType(IrType type)
    {
        return type is IrIntType;
    }

    /// <summary>
    /// Check if a type is a pointer-like type (pointer, reference, or mutable reference).
    /// </summary>
    public static bool IsPointerLikeType(IrType type)
    {
        return type is IrPointerType or IrReferenceType or IrMutReferenceType;
    }

    /// <summary>
    /// Get the element/pointee type from a pointer-like or array type.
    /// Returns null if the type doesn't have an element type.
    /// </summary>
    public static IrType? GetElementType(IrType type)
    {
        return type switch
        {
            IrPointerType ptr => ptr.PointeeType,
            IrReferenceType r => r.PointeeType,
            IrMutReferenceType mr => mr.PointeeType,
            IrArrayType arr => arr.ElementType,
            _ => null
        };
    }

    /// <summary>
    /// Check if a type is a string type (Str or String).
    /// </summary>
    public static bool IsStringType(IrType type)
    {
        return type is IrStructType structType &&
               (structType.StructName == "Str" || structType.StructName == "String");
    }

    /// <summary>
    /// Check if a type is the unit type (empty tuple).
    /// </summary>
    public static bool IsUnitType(IrType type)
    {
        if (type is IrVoidType)
            return true;
        if (type is IrTupleType tupleType && tupleType.ElementTypes is [])
            return true;
        return false;
    }
}
