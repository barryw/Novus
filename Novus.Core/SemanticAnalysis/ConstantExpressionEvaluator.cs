using Antlr4.Runtime.Misc;
using Novus.Parser;
using System.Globalization;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Evaluates constant expressions at compile time.
/// Supports: literals, identifiers, bitwise ops (|, &, ^, <<, >>, ~), unary minus
/// </summary>
public class ConstantExpressionEvaluator : NovusBaseVisitor<int?>
{
    private readonly Dictionary<string, object> _constants;
    private readonly Action<string> _onError;

    public ConstantExpressionEvaluator(Dictionary<string, object> constants, Action<string>? onError = null)
    {
        _constants = constants;
        _onError = onError ?? (_ => { });
    }

    public override int? VisitIntegerLiteral([NotNull] NovusParser.IntegerLiteralContext context)
    {
        var text = context.INTEGER_LITERAL().GetText();
        // Remove type suffixes
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(u8|u16|u32|u64|i8|i16|i32|i64)$", "");

        // Handle negative prefix if present
        var fullText = context.GetText();
        if (fullText.StartsWith("-") && !text.StartsWith("-"))
        {
            text = "-" + text;
        }

        if (int.TryParse(text, out var value))
        {
            return value;
        }

        return null;
    }

    public override int? VisitHexLiteral([NotNull] NovusParser.HexLiteralContext context)
    {
        var text = context.HEX_LITERAL().GetText();
        text = text.TrimStart('$');
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(u8|u16|u32|u64|i8|i16|i32|i64)$", "");

        if (int.TryParse(text, NumberStyles.HexNumber, null, out var value))
        {
            // Handle negative prefix if present
            var fullText = context.GetText();
            if (fullText.StartsWith("-"))
            {
                return -value;
            }
            return value;
        }

        return null;
    }

    public override int? VisitBinaryLiteral([NotNull] NovusParser.BinaryLiteralContext context)
    {
        var text = context.BINARY_LITERAL().GetText();
        text = text.TrimStart('%');
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(u8|u16|u32|u64|i8|i16|i32|i64)$", "");
        text = text.Replace("_", "");

        try
        {
            var value = Convert.ToInt32(text, 2);

            // Handle negative prefix if present
            var fullText = context.GetText();
            if (fullText.StartsWith("-"))
            {
                return -value;
            }
            return value;
        }
        catch
        {
            return null;
        }
    }

    public override int? VisitIdentifierExpr([NotNull] NovusParser.IdentifierExprContext context)
    {
        var name = context.identifier().GetText();

        if (_constants.TryGetValue(name, out var value))
        {
            return (int)value;
        }

        _onError($"undefined constant '{name}'");
        return null;
    }

    public override int? VisitParenExpr([NotNull] NovusParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override int? VisitUnaryExpr([NotNull] NovusParser.UnaryExprContext context)
    {
        var operand = Visit(context.expression());
        if (!operand.HasValue)
            return null;

        var op = context.GetChild(0).GetText();
        return op switch
        {
            "-" => -operand.Value,
            "~" => ~operand.Value,
            "!" => operand.Value == 0 ? 1 : 0,  // ! is logical not
            _ => null
        };
    }

    public override int? VisitBitwiseOrExpr([NotNull] NovusParser.BitwiseOrExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));

        if (left.HasValue && right.HasValue)
            return left.Value | right.Value;

        return null;
    }

    public override int? VisitBitwiseAndExpr([NotNull] NovusParser.BitwiseAndExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));

        if (left.HasValue && right.HasValue)
            return left.Value & right.Value;

        return null;
    }

    public override int? VisitBitwiseXorExpr([NotNull] NovusParser.BitwiseXorExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));

        if (left.HasValue && right.HasValue)
            return left.Value ^ right.Value;

        return null;
    }

    public override int? VisitShiftExpr([NotNull] NovusParser.ShiftExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));

        if (!left.HasValue || !right.HasValue)
            return null;

        // Check which shift operator is used
        var isLeftShift = context.LSHIFT() != null;
        return isLeftShift ? left.Value << right.Value : left.Value >> right.Value;
    }

    public override int? VisitAdditiveExpr([NotNull] NovusParser.AdditiveExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));

        if (!left.HasValue || !right.HasValue)
            return null;

        var op = context.GetChild(1).GetText();
        return op switch
        {
            "+" => left.Value + right.Value,
            "-" => left.Value - right.Value,
            _ => null
        };
    }

    public override int? VisitMultiplicativeExpr([NotNull] NovusParser.MultiplicativeExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));

        if (!left.HasValue || !right.HasValue)
            return null;

        var op = context.GetChild(1).GetText();
        return op switch
        {
            "*" => left.Value * right.Value,
            "/" => right.Value != 0 ? left.Value / right.Value : null,
            "%" => right.Value != 0 ? left.Value % right.Value : null,
            _ => null
        };
    }

    public override int? VisitPrimaryExpr([NotNull] NovusParser.PrimaryExprContext context)
    {
        // Delegate to the primary expression
        return Visit(context.primaryExpression());
    }

    protected override int? DefaultResult => null;
}
