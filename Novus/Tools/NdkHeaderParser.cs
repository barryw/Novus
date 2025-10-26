using System.Text;
using System.Text.RegularExpressions;

namespace Novus.Tools;

/// <summary>
/// Parses Amiga NDK C header files to extract structs and constants
/// </summary>
public class NdkHeaderParser
{
    /// <summary>
    /// Represents a parsed struct from a C header
    /// </summary>
    public record StructDefinition(string Name, List<StructField> Fields);

    /// <summary>
    /// Represents a field in a struct
    /// </summary>
    public record StructField(string Name, string CType, bool IsPointer, bool IsArray, int ArraySize = 0);

    /// <summary>
    /// Represents a constant definition
    /// </summary>
    public record ConstantDefinition(string Name, string Value, string? Comment = null);

    /// <summary>
    /// Map C types to Novus types
    /// </summary>
    private static readonly Dictionary<string, string> TypeMap = new()
    {
        // Exact types
        { "UBYTE", "u8" },
        { "BYTE", "i8" },
        { "UWORD", "u16" },
        { "WORD", "i16" },
        { "ULONG", "u32" },
        { "LONG", "i32" },
        { "BOOL", "i32" },  // BOOL is WORD on Amiga (16-bit), but we'll use i32 for compatibility

        // Standard C types
        { "unsigned char", "u8" },
        { "char", "i8" },
        { "unsigned short", "u16" },
        { "short", "i16" },
        { "unsigned int", "u32" },
        { "int", "i32" },
        { "unsigned long", "u32" },
        { "long", "i32" },

        // Pointer types - all become *u8 (opaque)
        { "APTR", "*u8" },
        { "STRPTR", "*u8" },
        { "CONST_STRPTR", "*u8" },
        { "void", "*u8" },
    };

    /// <summary>
    /// Parse structs from a header file
    /// </summary>
    public static List<StructDefinition> ParseStructs(string headerPath)
    {
        var structs = new List<StructDefinition>();
        var content = File.ReadAllText(headerPath);

        // Remove C comments
        content = Regex.Replace(content, @"/\*.*?\*/", "", RegexOptions.Singleline);
        content = Regex.Replace(content, @"//.*$", "", RegexOptions.Multiline);

        // Find struct definitions: struct Name { ... };
        var structPattern = @"struct\s+(\w+)\s*\{([^}]+)\}\s*;";
        var matches = Regex.Matches(content, structPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var structName = match.Groups[1].Value;
            var body = match.Groups[2].Value;

            var fields = ParseStructFields(body);
            if (fields.Count > 0)
            {
                structs.Add(new StructDefinition(structName, fields));
            }
        }

        return structs;
    }

    /// <summary>
    /// Parse fields from struct body
    /// </summary>
    private static List<StructField> ParseStructFields(string body)
    {
        var fields = new List<StructField>();
        var lines = body.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Skip preprocessor directives and unions
            if (trimmed.StartsWith("#") || trimmed.StartsWith("union")) continue;

            // Parse field: type name or type *name or type name[size]
            var fieldMatch = Regex.Match(trimmed, @"(\w+(?:\s+\w+)*)\s+(\*?)(\w+)(?:\[(\d+)\])?");
            if (fieldMatch.Success)
            {
                var type = fieldMatch.Groups[1].Value.Trim();
                var pointerIndicator = fieldMatch.Groups[2].Value;
                var name = fieldMatch.Groups[3].Value;
                var arraySize = fieldMatch.Groups[4].Success ? int.Parse(fieldMatch.Groups[4].Value) : 0;

                bool isPointer = pointerIndicator == "*" || type.StartsWith("struct ");
                bool isArray = arraySize > 0;

                fields.Add(new StructField(name, type, isPointer, isArray, arraySize));
            }
        }

        return fields;
    }

    /// <summary>
    /// Parse #define constants from a header file
    /// </summary>
    public static List<ConstantDefinition> ParseConstants(string headerPath, string? prefix = null)
    {
        var constants = new List<ConstantDefinition>();
        var lines = File.ReadAllLines(headerPath);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Match #define NAME value
            var match = Regex.Match(trimmed, @"#define\s+(\w+)\s+(.+)");
            if (match.Success)
            {
                var name = match.Groups[1].Value;
                var value = match.Groups[2].Value.Trim();

                // Skip if prefix filter is set and doesn't match
                if (prefix != null && !name.StartsWith(prefix))
                    continue;

                // Skip function-like macros
                if (value.Contains("(") && !value.StartsWith("("))
                    continue;

                // Clean up value - remove comments
                var commentIdx = value.IndexOf("/*");
                string? comment = null;
                if (commentIdx >= 0)
                {
                    comment = value.Substring(commentIdx + 2).Replace("*/", "").Trim();
                    value = value.Substring(0, commentIdx).Trim();
                }

                // Skip non-numeric constants (C keywords, identifiers, etc.)
                // Only accept numeric literals: 0x1234, 1234, (1<<2), etc.
                if (!IsValidConstantValue(value))
                    continue;

                constants.Add(new ConstantDefinition(name, value, comment));
            }
        }

        return constants;
    }

    /// <summary>
    /// Check if a constant value is valid (numeric literal or expression)
    /// </summary>
    private static bool IsValidConstantValue(string value)
    {
        // Remove parentheses and whitespace for checking
        value = value.Trim('(', ')', ' ');

        // Skip C keywords and identifiers
        var invalidKeywords = new[] { "extern", "static", "void", "const", "volatile", "auto", "register" };
        if (invalidKeywords.Contains(value))
            return false;

        // Skip if it's just an identifier (not a number or expression)
        if (Regex.IsMatch(value, @"^[a-zA-Z_]\w*$"))
            return false;

        // Accept numeric literals: 123, 0x123, 0X123, 0L, 123L
        if (Regex.IsMatch(value, @"^(0[xX][0-9a-fA-F]+L?|[0-9]+L?)$"))
            return true;

        // Accept shift expressions: (1<<5), (1L<<5)
        if (Regex.IsMatch(value, @"^\(?\d+L?<<\d+\)?$"))
            return true;

        // Accept parenthesized numbers: (123), (0x123), (0L), (123L)
        if (Regex.IsMatch(value, @"^\((0[xX][0-9a-fA-F]+L?|[0-9]+L?)\)$"))
            return true;

        // Reject everything else
        return false;
    }

    /// <summary>
    /// Convert C type to Novus type
    /// </summary>
    public static string ConvertType(string cType, bool isPointer)
    {
        // Strip struct keyword
        cType = cType.Replace("struct ", "").Trim();

        // If it's a pointer, return *u8 (opaque pointer)
        if (isPointer)
            return "*u8";

        // Try exact match
        if (TypeMap.TryGetValue(cType, out var novusType))
            return novusType;

        // Default to i32 for unknown types
        return "i32";
    }

    /// <summary>
    /// Generate Novus struct declaration
    /// </summary>
    public static string GenerateNovusStruct(StructDefinition structDef)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"pub struct {structDef.Name} {{");

        foreach (var field in structDef.Fields)
        {
            var novusType = ConvertType(field.CType, field.IsPointer);

            if (field.IsArray)
            {
                // Arrays become fixed-size arrays in Novus
                sb.AppendLine($"    {field.Name}: [{field.ArraySize}]{novusType},");
            }
            else
            {
                sb.AppendLine($"    {field.Name}: {novusType},");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate Novus constants
    /// </summary>
    public static string GenerateNovusConstants(List<ConstantDefinition> constants)
    {
        var sb = new StringBuilder();

        foreach (var constant in constants)
        {
            // Try to determine the type from the value
            string type = "u32"; // Default to u32 for flags/enums

            // Clean up value
            var value = constant.Value;

            // Handle bit shifts: (1L<<0) -> 1
            if (value.Contains("<<"))
            {
                var shiftMatch = Regex.Match(value, @"\((\d+)L?<<(\d+)\)");
                if (shiftMatch.Success)
                {
                    var baseVal = int.Parse(shiftMatch.Groups[1].Value);
                    var shift = int.Parse(shiftMatch.Groups[2].Value);
                    value = (baseVal << shift).ToString();
                }
            }

            // Remove L suffix
            value = value.Replace("L", "");

            // Convert C hex format (0x) to Novus hex format ($)
            // Handle both 0x and 0X, and preserve parentheses if present
            if (value.Contains("0x") || value.Contains("0X"))
            {
                value = Regex.Replace(value, @"\b0[xX]([0-9a-fA-F]+)\b", "$$$1");
            }

            // Add comment if available
            var comment = constant.Comment != null ? $" // {constant.Comment}" : "";

            sb.AppendLine($"pub const {constant.Name}: {type} = {value}{comment}");
        }

        return sb.ToString();
    }
}
