namespace Novus.Frontend;

internal static class ByteLiteralParser
{
    public static byte[] Parse(string token, int prefixLength)
    {
        var content = token[(prefixLength + 1)..^1];
        var bytes = new List<byte>(content.Length);

        for (var index = 0; index < content.Length; index++)
        {
            var ch = content[index];
            if (ch != '\\')
            {
                if (ch > byte.MaxValue)
                    throw new FormatException("byte strings may only contain byte-sized characters");
                bytes.Add((byte)ch);
                continue;
            }

            if (++index >= content.Length)
                throw new FormatException("incomplete byte-string escape");

            ch = content[index];
            if (ch == 'x')
            {
                if (index + 2 >= content.Length)
                    throw new FormatException("byte hex escapes require two digits");
                bytes.Add(Convert.ToByte(content.Substring(index + 1, 2), 16));
                index += 2;
                continue;
            }

            bytes.Add(ch switch
            {
                '0' => 0,
                'b' => (byte)'\b',
                't' => (byte)'\t',
                'n' => (byte)'\n',
                'f' => (byte)'\f',
                'r' => (byte)'\r',
                '"' => (byte)'"',
                '\'' => (byte)'\'',
                '\\' => (byte)'\\',
                _ => throw new FormatException($"unknown byte escape '\\{ch}'")
            });
        }

        return bytes.ToArray();
    }
}
