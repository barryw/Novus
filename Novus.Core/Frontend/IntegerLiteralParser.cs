using System.Numerics;
using Novus.IR;

namespace Novus.Frontend;

internal readonly record struct ParsedIntegerLiteral(
    BigInteger Value,
    IrIntType Type)
{
    public BigInteger Minimum => Type.IsSigned
        ? -(BigInteger.One << (Type.BitWidth - 1))
        : BigInteger.Zero;

    public BigInteger Maximum => Type.IsSigned
        ? (BigInteger.One << (Type.BitWidth - 1)) - 1
        : (BigInteger.One << Type.BitWidth) - 1;

    public bool FitsType
    {
        get => Value >= Minimum && Value <= Maximum;
    }

    public long ToBitPattern()
    {
        if (Value >= long.MinValue && Value <= long.MaxValue)
            return (long)Value;

        return unchecked((long)(ulong)Value);
    }
}

internal static class IntegerLiteralParser
{
    public static ParsedIntegerLiteral Parse(
        string text,
        IrIntType? expectedType = null,
        IrIntType? defaultType = null)
    {
        text = text.Replace("_", "", StringComparison.Ordinal);
        var negative = text.StartsWith('-');
        if (negative)
            text = text[1..];

        var (digits, radix) = ExtractRadix(text);
        if (digits.Length == 0)
            throw new FormatException("integer literal has no digits");

        var value = BigInteger.Zero;
        foreach (var character in digits)
        {
            var digit = character switch
            {
                >= '0' and <= '9' => character - '0',
                >= 'a' and <= 'f' => character - 'a' + 10,
                >= 'A' and <= 'F' => character - 'A' + 10,
                _ => -1
            };
            if (digit < 0 || digit >= radix)
                throw new FormatException($"invalid base-{radix} digit '{character}'");
            value = value * radix + digit;
        }

        if (negative)
            value = -value;

        return new ParsedIntegerLiteral(
            value,
            expectedType ?? defaultType ?? IrIntType.I32);
    }

    private static (string Digits, int Radix) ExtractRadix(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return (text[2..], 16);
        if (text.StartsWith('$'))
            return (text[1..], 16);
        if (text.StartsWith('%'))
            return (text[1..], 2);
        return (text, 10);
    }

}
