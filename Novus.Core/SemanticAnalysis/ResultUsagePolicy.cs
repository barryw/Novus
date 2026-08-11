using Novus.IR;
using Novus.Parser;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Shared rules for deciding whether a Result-valued expression is consumed.
/// </summary>
public static class ResultUsagePolicy
{
    public static bool IsResult(IrType? type) =>
        type is IrEnumType { EnumName: "Result" } enumType &&
        enumType.Variants.Any(variant => variant.Name == "Ok") &&
        enumType.Variants.Any(variant => variant.Name == "Err");

    public static bool TryGetTypes(IrType? type, out IrType? okType, out IrType? errorType)
    {
        okType = null;
        errorType = null;

        if (type is not IrEnumType enumType || !IsResult(enumType))
        {
            return false;
        }

        var ok = enumType.Variants.First(variant => variant.Name == "Ok");
        var error = enumType.Variants.First(variant => variant.Name == "Err");
        if (ok.AssociatedData.Count != 1 || error.AssociatedData.Count != 1)
        {
            return false;
        }

        okType = ok.AssociatedData[0];
        errorType = error.AssociatedData[0];
        return true;
    }

    public static bool IsUnit(IrType? type) =>
        type is IrTupleType { ElementTypes.Count: 0 };

    public static bool IsConsumed(
        NovusParser.ExpressionStatementContext context,
        IrType? currentFunctionReturnType)
    {
        if (context.Parent is not NovusParser.StatementContext statement ||
            statement.Parent is not NovusParser.BlockContext block)
        {
            return false;
        }

        var statements = block.statement();
        if (statements.Length == 0 || !ReferenceEquals(statements[^1], statement))
        {
            return false;
        }

        return block.Parent switch
        {
            NovusParser.FunctionDeclarationContext => currentFunctionReturnType is not null and not IrVoidType,
            NovusParser.ClosureExpressionContext => true,
            NovusParser.MatchArmContext => true,
            NovusParser.IfExprContext => true,
            NovusParser.IfElseChainContext => true,
            NovusParser.UnsafeExprContext => true,
            _ => false
        };
    }
}
