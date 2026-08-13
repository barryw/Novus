using Antlr4.Runtime.Misc;
using Novus.Parser;
using System.Globalization;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Result of a const function call evaluation.
/// </summary>
public class ConstFnCallResult
{
    public bool Success { get; }
    public int? Value { get; }
    public string? Error { get; }

    private ConstFnCallResult(bool success, int? value, string? error)
    {
        Success = success;
        Value = value;
        Error = error;
    }

    public static ConstFnCallResult Ok(int value) => new(true, value, null);
    public static ConstFnCallResult Err(string error) => new(false, null, error);
    public static ConstFnCallResult NotAConstFn() => new(false, null, null);
}

/// <summary>
/// Delegate for evaluating const function calls.
/// Takes: function name, evaluated arguments
/// Returns: ConstFnCallResult with the computed value or error
/// </summary>
public delegate ConstFnCallResult? ConstFnCallEvaluator(string functionName, List<int> arguments);

/// <summary>
/// Evaluates constant expressions at compile time.
/// Supports: literals, identifiers, bitwise ops (|, &, ^, <<, >>, ~), unary minus, arithmetic, const fn calls.
/// </summary>
public class ConstantExpressionEvaluator : NovusParserBaseVisitor<int?>
{
    public static bool TryParseFloatingLiteral(NovusParser.ExpressionContext context, out double value)
    {
        return double.TryParse(context.GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private readonly Dictionary<string, object> _constants;
    private readonly Action<string> _onError;
    private readonly ConstFnCallEvaluator? _constFnEvaluator;

    /// <summary>
    /// Gets whether this expression contains a function call that couldn't be evaluated.
    /// This can be used to defer evaluation to a later pass when const fn IR is available.
    /// </summary>
    public bool HasDeferredFunctionCall { get; private set; }

    public ConstantExpressionEvaluator(Dictionary<string, object> constants, Action<string>? onError = null, ConstFnCallEvaluator? constFnEvaluator = null)
    {
        _constants = constants;
        _onError = onError ?? (_ => { });
        _constFnEvaluator = constFnEvaluator;
    }

    public override int? VisitIntegerLiteral([NotNull] NovusParser.IntegerLiteralContext context)
    {
        var text = context.INTEGER_LITERAL().GetText();

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
        // Handle both '$' and '0x'/'0X' prefixes
        if (text.StartsWith("0x") || text.StartsWith("0X"))
        {
            text = text[2..];
        }
        else
        {
            text = text.TrimStart('$');
        }
        text = text.Replace("_", "");

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

    /// <summary>
    /// Handle function calls in constant expressions.
    /// Only const fn calls with constant arguments are valid.
    /// </summary>
    public override int? VisitCallExpr([NotNull] NovusParser.CallExprContext context)
    {
        // Get the function being called - it should be an identifier expression
        var calleeExpr = context.expression();

        // Extract function name from the callee expression
        string? functionName = null;

        if (calleeExpr is NovusParser.PrimaryExprContext primaryExpr)
        {
            var primary = primaryExpr.primaryExpression();
            if (primary is NovusParser.IdentifierExprContext identExpr)
            {
                functionName = identExpr.identifier().GetText();
            }
        }

        if (functionName == null)
        {
            // Not a simple function call - could be method call, closure, etc.
            _onError("Only direct const fn calls are supported in constant expressions");
            return null;
        }

        // Evaluate all arguments
        var arguments = new List<int>();
        var argList = context.argumentList();

        if (argList != null)
        {
            foreach (var argExpr in argList.expression())
            {
                var argValue = Visit(argExpr);
                if (!argValue.HasValue)
                {
                    _onError($"Argument to const fn '{functionName}' must be a constant expression");
                    return null;
                }
                arguments.Add(argValue.Value);
            }
        }

        // If we have a const fn evaluator, try to evaluate the call
        if (_constFnEvaluator != null)
        {
            var result = _constFnEvaluator(functionName, arguments);

            if (result != null)
            {
                if (result.Success && result.Value.HasValue)
                {
                    return result.Value;
                }
                else if (result.Error != null)
                {
                    _onError(result.Error);
                    return null;
                }
            }
            // result is null or NotAConstFn - function is not a const fn
        }

        // No evaluator or function is not a const fn - mark as deferred
        HasDeferredFunctionCall = true;
        return null;
    }

    protected override int? DefaultResult => null;
}
