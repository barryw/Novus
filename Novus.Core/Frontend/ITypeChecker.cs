using Novus.Diagnostics;
using Novus.IR;

namespace Novus.Frontend;

/// <summary>
/// Interface for type checking operations during IR building.
/// This represents the second phase: validating type correctness of operations.
/// </summary>
public interface ITypeChecker
{
    /// <summary>
    /// Check if two types are equal.
    /// </summary>
    bool TypesAreEqual(IrType? left, IrType? right);

    /// <summary>
    /// Check if a value of sourceType can be assigned to a location of targetType.
    /// </summary>
    bool CanAssign(IrType targetType, IrType sourceType);

    /// <summary>
    /// Get the result type of a binary operation.
    /// </summary>
    IrType? GetBinaryOperationResultType(IrType leftType, IrType rightType, string operation);

    /// <summary>
    /// Get the result type of a unary operation.
    /// </summary>
    IrType? GetUnaryOperationResultType(IrType operandType, string operation);

    /// <summary>
    /// Check if a type can be used as a boolean condition.
    /// </summary>
    bool CanUseAsCondition(IrType type);

    /// <summary>
    /// Check if a type supports indexing.
    /// </summary>
    bool CanIndex(IrType type);

    /// <summary>
    /// Get the element type of an indexable type.
    /// </summary>
    IrType? GetElementType(IrType type);

    /// <summary>
    /// Check if a type supports member access.
    /// </summary>
    bool HasMember(IrType type, string memberName);

    /// <summary>
    /// Get the type of a member.
    /// </summary>
    IrType? GetMemberType(IrType type, string memberName);

    /// <summary>
    /// Get the common type for two types (e.g., for if-else branches).
    /// </summary>
    IrType? GetCommonType(IrType type1, IrType type2);
}

/// <summary>
/// Default implementation of ITypeChecker that uses TypeChecker utility.
/// </summary>
public class DefaultTypeChecker : ITypeChecker
{
    private readonly DiagnosticBag _diagnostics;

    public DefaultTypeChecker(DiagnosticBag diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public bool TypesAreEqual(IrType? left, IrType? right)
    {
        if (left == null || right == null) return left == right;
        return Novus.SemanticAnalysis.TypeChecker.TypesAreEqual(left, right);
    }

    public bool CanAssign(IrType targetType, IrType sourceType)
    {
        // Simple equality check for now - the full implementation would
        // consider coercions, reference conversions, etc.
        return TypesAreEqual(targetType, sourceType) ||
               CanCoerce(sourceType, targetType);
    }

    private bool CanCoerce(IrType from, IrType to)
    {
        // Integer widening
        if (from is IrIntType fromInt && to is IrIntType toInt)
        {
            if (toInt.BitWidth >= fromInt.BitWidth)
            {
                return true;
            }
        }

        // Pointer-to-bool
        if (from is IrPointerType && to is IrBoolType)
        {
            return true;
        }

        return false;
    }

    public IrType? GetBinaryOperationResultType(IrType leftType, IrType rightType, string operation)
    {
        // Arithmetic operations return the operand type
        if (operation is "+" or "-" or "*" or "/" or "%")
        {
            if (leftType is IrIntType leftInt && rightType is IrIntType rightInt)
            {
                return Novus.SemanticAnalysis.TypeChecker.GetCommonIntegerType(leftInt, rightInt);
            }
            if (leftType is IrFloatType || rightType is IrFloatType)
            {
                return IrFloatType.F64; // Promote to double
            }
        }

        // Comparison operations return bool
        if (operation is "==" or "!=" or "<" or "<=" or ">" or ">=")
        {
            return IrBoolType.Instance;
        }

        // Logical operations return bool
        if (operation is "&&" or "||")
        {
            return IrBoolType.Instance;
        }

        // Bitwise operations return the operand type
        if (operation is "&" or "|" or "^" or "<<" or ">>")
        {
            if (leftType is IrIntType)
            {
                return leftType;
            }
        }

        return null;
    }

    public IrType? GetUnaryOperationResultType(IrType operandType, string operation)
    {
        return operation switch
        {
            "-" when operandType is IrIntType => operandType,
            "-" when operandType is IrFloatType => operandType,
            "!" when operandType is IrBoolType => IrBoolType.Instance,
            "~" when operandType is IrIntType => operandType,
            "*" when operandType is IrPointerType pt => pt.PointeeType,
            "*" when operandType is IrReferenceType rt => rt.PointeeType,
            "*" when operandType is IrMutReferenceType mrt => mrt.PointeeType,
            "&" => new IrReferenceType(operandType),
            "&mut" => new IrMutReferenceType(operandType),
            _ => null
        };
    }

    public bool CanUseAsCondition(IrType type)
    {
        return type is IrBoolType ||
               type is IrIntType ||
               type is IrPointerType;
    }

    public bool CanIndex(IrType type)
    {
        return type is IrArrayType ||
               type is IrPointerType;
    }

    public IrType? GetElementType(IrType type)
    {
        return type switch
        {
            IrArrayType arr => arr.ElementType,
            IrPointerType ptr => ptr.PointeeType,
            _ => null
        };
    }

    public bool HasMember(IrType type, string memberName)
    {
        if (type is IrStructType structType)
        {
            return structType.Fields.Any(f => f.Name == memberName);
        }
        return false;
    }

    public IrType? GetMemberType(IrType type, string memberName)
    {
        if (type is IrStructType structType)
        {
            var field = structType.Fields.FirstOrDefault(f => f.Name == memberName);
            return field?.Type;
        }
        return null;
    }

    public IrType? GetCommonType(IrType type1, IrType type2)
    {
        if (TypesAreEqual(type1, type2))
        {
            return type1;
        }

        // Integer promotion
        if (type1 is IrIntType intType1 && type2 is IrIntType intType2)
        {
            return Novus.SemanticAnalysis.TypeChecker.GetCommonIntegerType(intType1, intType2);
        }

        return null;
    }
}
