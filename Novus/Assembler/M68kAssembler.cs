using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Novus.Assembler;

/// <summary>Clean Novus-owned Motorola-syntax 68020+ assembler.</summary>
public sealed class M68kAssembler
{
    private const uint HunkUnit = 999, HunkName = 1000, HunkCode = 1001,
        HunkData = 1002, HunkBss = 1003, HunkExt = 1007, HunkSymbol = 1008, HunkEnd = 1010;

    private sealed class Section(string name, uint kind)
    {
        public string Name { get; } = name;
        public uint Kind { get; } = kind;
        public List<byte> Data { get; } = [];
        public Dictionary<string, int> Labels { get; } = new(StringComparer.Ordinal);
        public List<(int Offset, string Name, byte Condition, int Width, int Line)> Branches { get; } = [];
        public List<(int Offset, string Name)> References { get; } = [];
    }

    public byte[] Assemble(string source, string unitName = "input.s")
    {
        var sections = new List<Section>();
        var exports = new HashSet<string>(StringComparer.Ordinal);
        var imports = new HashSet<string>(StringComparer.Ordinal);
        var labels = new Dictionary<string, (Section Section, int Offset)>(StringComparer.Ordinal);
        Section? current = null;
        var lines = source.Replace("\r", "").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = StripComment(lines[index]).Trim();
            if (line.Length == 0) continue;

            if (line.EndsWith(':'))
            {
                current ??= AddSection(sections, "CODE", HunkCode);
                var name = line[..^1].Trim();
                if (!labels.TryAdd(name, (current, current.Data.Count)))
                    throw Error(lineNumber, $"duplicate label '{name}'");
                current.Labels[name] = current.Data.Count;
                continue;
            }

            var split = line.IndexOfAny([' ', '\t']);
            var operation = (split < 0 ? line : line[..split]).ToLowerInvariant();
            var operands = split < 0 ? "" : line[(split + 1)..].Trim();
            switch (operation)
            {
                case "cpu":
                    if (!operands.Contains("68020", StringComparison.OrdinalIgnoreCase) &&
                        !operands.Contains("68030", StringComparison.OrdinalIgnoreCase) &&
                        !operands.Contains("68040", StringComparison.OrdinalIgnoreCase) &&
                        !operands.Contains("68060", StringComparison.OrdinalIgnoreCase))
                        throw Error(lineNumber, "Novus targets 68020 or newer");
                    break;
                case "section":
                    current = ParseSection(sections, operands, lineNumber);
                    break;
                case "xdef":
                    foreach (var name in SplitOperands(operands)) exports.Add(name);
                    break;
                case "xref":
                    foreach (var name in SplitOperands(operands)) imports.Add(name);
                    break;
                case "even":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    if ((current.Data.Count & 1) != 0) current.Data.Add(0);
                    break;
                case "dc.b": case "dc.w": case "dc.l":
                    current ??= AddSection(sections, "DATA", HunkData);
                    EmitData(current.Data, operation[^1], operands, lineNumber);
                    break;
                case "nop":
                    RequireNoOperands(operands, operation, lineNumber);
                    current ??= AddSection(sections, "CODE", HunkCode);
                    Word(current.Data, 0x4e71);
                    break;
                case "rts":
                    RequireNoOperands(operands, operation, lineNumber);
                    current ??= AddSection(sections, "CODE", HunkCode);
                    Word(current.Data, 0x4e75);
                    break;
                case "moveq":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    EmitMoveq(current.Data, operands, lineNumber);
                    break;
                case "movea.l":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    EmitMovea(current, operands, imports, lineNumber);
                    break;
                case "move.l":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    EmitMoveLong(current.Data, operands, lineNumber);
                    break;
                case "add.l":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    EmitAddLong(current.Data, operands, lineNumber);
                    break;
                case "addq.l":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    EmitAddQuickLong(current.Data, operands, lineNumber);
                    break;
                case "movem.l":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    EmitMovem(current.Data, operands, lineNumber);
                    break;
                case "jsr":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    EmitJsr(current, operands, imports, lineNumber);
                    break;
                case "bra.s": case "beq.s": case "bne.s": case "bpl.s": case "bmi.s":
                    current ??= AddSection(sections, "CODE", HunkCode);
                    EmitBranch(current, operation, operands, lineNumber);
                    break;
                default:
                    throw Error(lineNumber, $"unsupported instruction or directive '{operation}'");
            }
        }

        foreach (var section in sections)
        foreach (var branch in section.Branches)
        {
            if (!labels.TryGetValue(branch.Name, out var target) || target.Section != section)
                throw Error(branch.Line, $"short branch target '{branch.Name}' is not in this section");
            var displacement = target.Offset - (branch.Offset + 2);
            if (displacement is < -128 or > 127 or 0)
                throw Error(branch.Line, $"short branch to '{branch.Name}' is out of range");
            section.Data[branch.Offset + 1] = unchecked((byte)(sbyte)displacement);
        }
        foreach (var name in exports)
            if (!labels.ContainsKey(name)) throw new FormatException($"exported label '{name}' is undefined");
        return WriteHunk(unitName, sections, exports, labels);
    }

    private static Section ParseSection(List<Section> sections, string operands, int line)
    {
        var values = SplitOperands(operands);
        if (values.Count != 2) throw Error(line, "section requires a name and code, data, or bss");
        var name = values[0].Trim('"');
        var kind = values[1].ToLowerInvariant() switch
        {
            "code" => HunkCode, "data" => HunkData, "bss" => HunkBss,
            _ => throw Error(line, $"unknown section kind '{values[1]}'")
        };
        return AddSection(sections, name, kind);
    }

    private static Section AddSection(List<Section> sections, string name, uint kind)
    {
        var section = new Section(name, kind);
        sections.Add(section);
        return section;
    }

    private static void EmitMoveq(List<byte> data, string operands, int line)
    {
        var values = SplitOperands(operands);
        if (values.Count != 2 || !values[0].StartsWith('#') || !TryDataRegister(values[1], out var register))
            throw Error(line, "moveq syntax is moveq #signed-byte,d0-d7");
        var value = ParseNumber(values[0][1..], line);
        if (value is < -128 or > 127) throw Error(line, "moveq immediate must fit a signed byte");
        Word(data, (ushort)(0x7000 | register << 9 | (byte)(sbyte)value));
    }

    private static void EmitBranch(Section section, string operation, string target, int line)
    {
        var condition = operation switch
        {
            "bra.s" => 0x0, "beq.s" => 0x7, "bne.s" => 0x6,
            "bpl.s" => 0xa, "bmi.s" => 0xb, _ => throw Error(line, "unsupported branch")
        };
        var offset = section.Data.Count;
        Word(section.Data, (ushort)(0x6000 | condition << 8));
        section.Branches.Add((offset, target, (byte)condition, 1, line));
    }

    private static void EmitJsr(Section section, string target, HashSet<string> imports, int line)
    {
        if (TryDisplacement(target, out var displacement, out var register))
        {
            Word(section.Data, (ushort)(0x4ea8 | register));
            Word(section.Data, unchecked((ushort)displacement));
            return;
        }
        if (!imports.Contains(target)) throw Error(line, $"external symbol '{target}' requires xref");
        Word(section.Data, 0x4eb9);
        section.References.Add((section.Data.Count, target));
        Long(section.Data, 0);
    }

    private static void EmitMovea(Section section, string operands, HashSet<string> imports, int line)
    {
        var values = SplitOperands(operands);
        if (values.Count != 2 || !TryAddressRegister(values[1], out var register) || !imports.Contains(values[0]))
            throw Error(line, "movea.l currently supports xref_symbol,a0-a7");
        Word(section.Data, (ushort)(0x2079 | register << 9));
        section.References.Add((section.Data.Count, values[0]));
        Long(section.Data, 0);
    }

    private static void EmitMoveLong(List<byte> data, string operands, int line)
    {
        var values = SplitOperands(operands);
        if (values.Count == 2 && TryDataRegister(values[0], out var pushedRegister) &&
            (values[1].Equals("-(sp)", StringComparison.OrdinalIgnoreCase) ||
             values[1].Equals("-(a7)", StringComparison.OrdinalIgnoreCase)))
        {
            Word(data, (ushort)(0x2f00 | pushedRegister));
            return;
        }
        if (values.Count == 2 && TryDisplacement(values[0], out var displacement, out var source) &&
            TryDataRegister(values[1], out var destination))
        {
            Word(data, (ushort)(0x2028 | destination << 9 | source));
            Word(data, unchecked((ushort)displacement));
            return;
        }
        throw Error(line, "move.l currently supports displacement(a0-a7),d0-d7");
    }

    private static void EmitAddLong(List<byte> data, string operands, int line)
    {
        var values = SplitOperands(operands);
        if (values.Count == 2 && TryDisplacement(values[0], out var displacement, out var sourceRegister) &&
            TryDataRegister(values[1], out var memoryDestination))
        {
            Word(data, (ushort)(0xd0a8 | memoryDestination << 9 | sourceRegister));
            Word(data, unchecked((ushort)displacement));
            return;
        }
        if (values.Count == 2 && TryDataRegister(values[0], out var source) &&
            TryDataRegister(values[1], out var destination))
        {
            Word(data, (ushort)(0xd080 | destination << 9 | source));
            return;
        }
        throw Error(line, "add.l currently supports d0-d7 or displacement(a0-a7) sources");
    }

    private static void EmitAddQuickLong(List<byte> data, string operands, int line)
    {
        var values = SplitOperands(operands);
        if (values.Count != 2 || !values[0].StartsWith('#'))
            throw Error(line, "addq.l syntax is addq.l #1-8,d0-d7 or a0-a7");
        var value = ParseNumber(values[0][1..], line);
        if (value is < 1 or > 8) throw Error(line, "addq.l immediate must be 1 through 8");
        if (TryDataRegister(values[1], out var dataRegister))
            Word(data, (ushort)(0x5080 | (value & 7) << 9 | dataRegister));
        else if (TryAddressRegister(values[1], out var addressRegister))
            Word(data, (ushort)(0x5088 | (value & 7) << 9 | addressRegister));
        else
            throw Error(line, "addq.l destination must be d0-d7 or a0-a7");
    }

    private static void EmitMovem(List<byte> data, string operands, int line)
    {
        var values = SplitOperands(operands);
        if (values.Count != 2) throw Error(line, "movem.l requires two operands");
        if (values[1].Equals("-(sp)", StringComparison.OrdinalIgnoreCase) ||
            values[1].Equals("-(a7)", StringComparison.OrdinalIgnoreCase))
        {
            Word(data, 0x48e7);
            Word(data, ReverseBits(ParseRegisterMask(values[0], line)));
            return;
        }
        if (values[0].Equals("(sp)+", StringComparison.OrdinalIgnoreCase) ||
            values[0].Equals("(a7)+", StringComparison.OrdinalIgnoreCase))
        {
            var mask = ParseRegisterMask(values[1], line);
            if ((mask & (mask - 1)) == 0)
            {
                var register = System.Numerics.BitOperations.TrailingZeroCount(mask);
                Word(data, (ushort)((register < 8 ? 0x201f : 0x205f) | (register & 7) << 9));
                return;
            }
            Word(data, 0x4cdf);
            Word(data, mask);
            return;
        }
        throw Error(line, "movem.l currently supports register lists pushed to or popped from sp");
    }

    private static ushort ParseRegisterMask(string value, int line)
    {
        ushort mask = 0;
        foreach (var part in value.Split('/'))
        {
            var range = part.Split('-');
            if (!TryRegister(range[0], out var first) ||
                (range.Length == 2 && !TryRegister(range[1], out _)) || range.Length > 2)
                throw Error(line, $"invalid register list '{value}'");
            var last = first;
            if (range.Length == 2) TryRegister(range[1], out last);
            if (last < first) throw Error(line, $"invalid register range '{part}'");
            for (var register = first; register <= last; register++) mask |= (ushort)(1 << register);
        }
        return mask;
    }

    private static ushort ReverseBits(ushort value)
    {
        ushort result = 0;
        for (var bit = 0; bit < 16; bit++)
            if ((value & 1 << bit) != 0) result |= (ushort)(1 << (15 - bit));
        return result;
    }

    private static void EmitData(List<byte> data, char size, string operands, int line)
    {
        foreach (var value in SplitOperands(operands))
        {
            if (size == 'b' && value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            {
                var text = value[1..^1];
                foreach (var character in text) data.Add((byte)character);
                continue;
            }
            var number = ParseNumber(value, line);
            switch (size)
            {
                case 'b': data.Add(unchecked((byte)number)); break;
                case 'w': Word(data, unchecked((ushort)number)); break;
                case 'l': Long(data, unchecked((uint)number)); break;
            }
        }
    }

    private static byte[] WriteHunk(string unitName, List<Section> sections, HashSet<string> exports,
        Dictionary<string, (Section Section, int Offset)> labels)
    {
        using var stream = new MemoryStream();
        Long(stream, HunkUnit); Name(stream, Path.GetFileName(unitName));
        foreach (var section in sections)
        {
            Long(stream, HunkName); Name(stream, section.Name);
            Long(stream, section.Kind);
            Long(stream, (uint)((section.Data.Count + 3) / 4));
            if (section.Kind != HunkBss)
            {
                stream.Write(section.Data.ToArray());
                if (section.Kind == HunkCode && (stream.Position & 3) == 2)
                    Word(stream, 0x4e71);
                else
                    while ((stream.Position & 3) != 0) stream.WriteByte(0);
            }
            var sectionExports = exports.Where(name => labels[name].Section == section).Order(StringComparer.Ordinal).ToList();
            if (section.References.Count > 0 || sectionExports.Count > 0)
            {
                Long(stream, HunkExt);
                foreach (var reference in section.References.GroupBy(reference => reference.Name).OrderBy(group => group.Key, StringComparer.Ordinal))
                {
                    ExtensionName(stream, 0x81, reference.Key);
                    Long(stream, (uint)reference.Count());
                    foreach (var item in reference) Long(stream, (uint)item.Offset);
                }
                foreach (var name in sectionExports)
                {
                    ExtensionName(stream, 1, name);
                    Long(stream, (uint)labels[name].Offset);
                }
                Long(stream, 0);
                Long(stream, HunkSymbol);
                foreach (var (name, label) in labels.Where(item => item.Value.Section == section))
                {
                    Name(stream, name);
                    Long(stream, (uint)label.Offset);
                }
                Long(stream, 0);
            }
            Long(stream, HunkEnd);
        }
        return stream.ToArray();
    }

    private static void ExtensionName(Stream stream, byte type, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var longs = (bytes.Length + 3) / 4;
        Long(stream, (uint)(type << 24 | longs));
        stream.Write(bytes);
        for (var index = bytes.Length; index < longs * 4; index++) stream.WriteByte(0);
    }

    private static void Name(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var longs = (bytes.Length + 3) / 4;
        Long(stream, (uint)longs);
        stream.Write(bytes);
        for (var index = bytes.Length; index < longs * 4; index++) stream.WriteByte(0);
    }

    private static string StripComment(string line)
    {
        var quote = false;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '\'') quote = !quote;
            if (line[index] == ';' && !quote) return line[..index];
        }
        return line;
    }

    private static List<string> SplitOperands(string value)
    {
        var result = new List<string>();
        var quote = false;
        var start = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\'') quote = !quote;
            if (value[index] == ',' && !quote)
            {
                result.Add(value[start..index].Trim());
                start = index + 1;
            }
        }
        if (start < value.Length) result.Add(value[start..].Trim());
        return result;
    }

    private static int ParseNumber(string value, int line)
    {
        value = value.Trim();
        var sign = 1;
        if (value.StartsWith('-')) { sign = -1; value = value[1..]; }
        var style = NumberStyles.Integer;
        if (value.StartsWith('$')) { style = NumberStyles.HexNumber; value = value[1..]; }
        else if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        { style = NumberStyles.HexNumber; value = value[2..]; }
        if (!int.TryParse(value, style, CultureInfo.InvariantCulture, out var number))
            throw Error(line, $"invalid integer '{value}'");
        return number * sign;
    }

    private static bool TryDataRegister(string value, out int register)
    {
        register = -1;
        return value.Length == 2 && (value[0] is 'd' or 'D') &&
               int.TryParse(value[1..], out register) && register is >= 0 and <= 7;
    }

    private static bool TryAddressRegister(string value, out int register)
    {
        if (value.Equals("sp", StringComparison.OrdinalIgnoreCase)) { register = 7; return true; }
        register = -1;
        return value.Length == 2 && (value[0] is 'a' or 'A') &&
               int.TryParse(value[1..], out register) && register is >= 0 and <= 7;
    }

    private static bool TryRegister(string value, out int register)
    {
        if (TryDataRegister(value, out register)) return true;
        if (TryAddressRegister(value, out register)) { register += 8; return true; }
        return false;
    }

    private static bool TryDisplacement(string value, out int displacement, out int register)
    {
        displacement = 0; register = -1;
        var parenthesis = value.IndexOf('(');
        if (parenthesis <= 0 || !value.EndsWith(')') ||
            !TryAddressRegister(value[(parenthesis + 1)..^1], out register)) return false;
        try { displacement = ParseNumber(value[..parenthesis], 0); return displacement is >= short.MinValue and <= short.MaxValue; }
        catch (FormatException) { return false; }
    }

    private static void RequireNoOperands(string operands, string operation, int line)
    { if (operands.Length != 0) throw Error(line, $"{operation} takes no operands"); }
    private static FormatException Error(int line, string message) => new($"line {line}: {message}");
    private static void Word(List<byte> data, ushort value)
    { data.Add((byte)(value >> 8)); data.Add((byte)value); }
    private static void Long(List<byte> data, uint value)
    { data.Add((byte)(value >> 24)); data.Add((byte)(value >> 16)); data.Add((byte)(value >> 8)); data.Add((byte)value); }
    private static void Long(Stream stream, uint value)
    { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(bytes, value); stream.Write(bytes); }
    private static void Word(Stream stream, ushort value)
    { Span<byte> bytes = stackalloc byte[2]; BinaryPrimitives.WriteUInt16BigEndian(bytes, value); stream.Write(bytes); }
}
