using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Novus.Tools;

/// <summary>
/// Parses C header files from Amiga NDK to extract structs and constants
/// </summary>
public class CHeaderParser
{
    public class HeaderFile
    {
        public string Path { get; set; } = "";
        public List<CStruct> Structs { get; set; } = new();
        public List<CConstant> Constants { get; set; } = new();
        public List<CEnum> Enums { get; set; } = new();
        public List<string> Includes { get; set; } = new();
    }

    public class CStruct
    {
        public string Name { get; set; } = "";
        public List<CField> Fields { get; set; } = new();
        public bool HasUnion { get; set; }
    }

    public class CField
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsArray { get; set; }
        public string ArraySize { get; set; } = "";
    }

    public class CConstant
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Comment { get; set; } = "";
    }

    public class CEnum
    {
        public string Name { get; set; } = "";
        public List<CEnumValue> Values { get; set; } = new();
    }

    public class CEnumValue
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public static HeaderFile ParseFile(string filePath, string? ndkPath = null, HashSet<string>? visitedFiles = null)
    {
        var header = new HeaderFile { Path = filePath };

        if (!File.Exists(filePath))
            return header;

        visitedFiles ??= new HashSet<string>();

        // Avoid parsing the same file twice
        var canonicalPath = System.IO.Path.GetFullPath(filePath);
        if (visitedFiles.Contains(canonicalPath))
            return header;

        visitedFiles.Add(canonicalPath);

        var lines = File.ReadAllLines(filePath);
        var preprocessed = PreprocessLines(lines);

        for (int i = 0; i < preprocessed.Count; i++)
        {
            var line = preprocessed[i].Trim();

            // Parse #define constants
            if (line.StartsWith("#define"))
            {
                // Skip function-like macros: #define FOO(x) (identifier immediately followed by paren)
                // Allow value defines with parens: #define FOO (1<<2) (space before paren)
                var match = Regex.Match(line, @"#define\s+([A-Za-z_][A-Za-z0-9_]*)\(");
                bool isFunctionMacro = match.Success; // No space between name and (

                if (!isFunctionMacro)
                {
                    var constant = ParseDefine(line);
                    if (constant != null)
                        header.Constants.Add(constant);
                }
            }

            // Parse struct definitions
            if (line.StartsWith("struct") && line.Contains("{"))
            {
                var structDef = ParseStruct(preprocessed, ref i);
                if (structDef != null)
                    header.Structs.Add(structDef);
            }

            // Parse enum definitions
            if (line.StartsWith("enum") && line.Contains("{"))
            {
                var enumDef = ParseEnum(preprocessed, ref i);
                if (enumDef != null)
                    header.Enums.Add(enumDef);
            }

            // Parse includes and recursively process them
            if (line.StartsWith("#include"))
            {
                var includeMatch = Regex.Match(line, @"#include\s+[<""](.+?)[>""]");
                if (includeMatch.Success)
                {
                    var includePath = includeMatch.Groups[1].Value;
                    header.Includes.Add(includePath);

                    // Recursively parse included file if NDK path is provided
                    if (ndkPath != null)
                    {
                        var fullIncludePath = System.IO.Path.Combine(ndkPath, "Include", "include_h", includePath);
                        if (File.Exists(fullIncludePath))
                        {
                            var includedHeader = ParseFile(fullIncludePath, ndkPath, visitedFiles);

                            // Merge structs and constants from included file
                            foreach (var s in includedHeader.Structs)
                            {
                                if (!header.Structs.Any(existing => existing.Name == s.Name))
                                    header.Structs.Add(s);
                            }

                            foreach (var c in includedHeader.Constants)
                            {
                                if (!header.Constants.Any(existing => existing.Name == c.Name))
                                    header.Constants.Add(c);
                            }
                        }
                    }
                }
            }
        }

        return header;
    }

    private static List<string> PreprocessLines(string[] lines)
    {
        var result = new List<string>();
        bool inBlockComment = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Handle block comments
            if (inBlockComment)
            {
                var endIndex = trimmed.IndexOf("*/");
                if (endIndex >= 0)
                {
                    inBlockComment = false;
                    trimmed = trimmed.Substring(endIndex + 2).Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;
                }
                else
                {
                    continue; // Still in block comment
                }
            }

            // Skip single-line comments
            if (trimmed.StartsWith("//"))
                continue;

            // Remove inline comments /* ... */ but keep the line
            var commentIndex = trimmed.IndexOf("/*");
            if (commentIndex >= 0)
            {
                var endIndex = trimmed.IndexOf("*/", commentIndex);
                if (endIndex >= 0)
                {
                    // Single-line inline comment
                    trimmed = (trimmed.Substring(0, commentIndex) + " " + trimmed.Substring(endIndex + 2)).Trim();
                }
                else
                {
                    // Start of multi-line comment
                    inBlockComment = true;
                    trimmed = trimmed.Substring(0, commentIndex).Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
                result.Add(trimmed);
        }

        return result;
    }

    private static CConstant? ParseDefine(string line)
    {
        // Pattern: #define NAME value [comment]
        var match = Regex.Match(line, @"#define\s+([A-Za-z_][A-Za-z0-9_]*)\s+(.+)");
        if (!match.Success)
            return null;

        var name = match.Groups[1].Value;
        var value = match.Groups[2].Value.Trim();

        // Skip empty values
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Skip include guards and other non-constant defines
        if (name.EndsWith("_H") || name.StartsWith("EXEC_") && name.EndsWith("_H"))
            return null;

        return new CConstant { Name = name, Value = value };
    }

    private static CStruct? ParseStruct(List<string> lines, ref int index)
    {
        var line = lines[index];

        // Extract struct name: "struct Name {" or "struct {"
        var structMatch = Regex.Match(line, @"struct\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{");
        if (!structMatch.Success)
            return null;

        var structName = structMatch.Groups[1].Value;
        var structDef = new CStruct { Name = structName };

        // Check if this is a single-line struct: struct Name { fields };
        if (line.Contains("};"))
        {
            // Extract the content between { and }
            var startBrace = line.IndexOf('{');
            var endBrace = line.IndexOf('}');
            if (startBrace >= 0 && endBrace > startBrace)
            {
                var content = line.Substring(startBrace + 1, endBrace - startBrace - 1).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    // Parse the field (there should only be one on single-line structs)
                    var field = ParseField(content + ";");
                    if (field != null)
                        structDef.Fields.Add(field);
                }
            }
            return structDef;
        }

        index++; // Move past opening brace

        // Parse fields until we hit closing brace
        while (index < lines.Count)
        {
            var fieldLine = lines[index].Trim();

            if (fieldLine.StartsWith("}"))
                break;

            if (fieldLine.StartsWith("union"))
            {
                structDef.HasUnion = true;
                // Skip union for now - would need more complex parsing
                index++;
                continue;
            }

            // Parse field: TYPE name; or TYPE name[SIZE];
            var field = ParseField(fieldLine);
            if (field != null)
                structDef.Fields.Add(field);

            index++;
        }

        return structDef;
    }

    private static CField? ParseField(string line)
    {
        if (!line.EndsWith(";"))
            return null;

        line = line.TrimEnd(';').Trim();

        // Handle function pointers: RETURNTYPE (*fieldName)()
        var funcPtrMatch = Regex.Match(line, @"^(.+?)\s*\(\*([A-Za-z_][A-Za-z0-9_]*)\)\s*\(.*?\)$");
        if (funcPtrMatch.Success)
        {
            return new CField
            {
                Type = "*u8",  // Function pointers become *u8
                Name = funcPtrMatch.Groups[2].Value.Trim(),
                IsArray = false
            };
        }

        // Handle arrays: TYPE name[SIZE]
        var arrayMatch = Regex.Match(line, @"^(.+?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\[(.+?)\]$");
        if (arrayMatch.Success)
        {
            return new CField
            {
                Type = arrayMatch.Groups[1].Value.Trim(),
                Name = arrayMatch.Groups[2].Value.Trim(),
                IsArray = true,
                ArraySize = arrayMatch.Groups[3].Value.Trim()
            };
        }

        // Handle pointers with * next to name: struct Foo *fieldName or struct Foo **fieldName
        var ptrNameMatch = Regex.Match(line, @"^(.+?)\s+(\*+)([A-Za-z_][A-Za-z0-9_]*)$");
        if (ptrNameMatch.Success)
        {
            var baseType = ptrNameMatch.Groups[1].Value.Trim();
            var pointers = ptrNameMatch.Groups[2].Value; // ** or * etc.
            var name = ptrNameMatch.Groups[3].Value.Trim();
            return new CField
            {
                Type = baseType + " " + pointers,
                Name = name,
                IsArray = false
            };
        }

        // Handle pointers and regular fields: TYPE name
        var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
            return null;

        var fieldName = tokens[^1];
        var fieldType = string.Join(" ", tokens.Take(tokens.Length - 1));

        return new CField
        {
            Type = fieldType,
            Name = fieldName,
            IsArray = false
        };
    }

    private static CEnum? ParseEnum(List<string> lines, ref int index)
    {
        var line = lines[index];

        // Extract enum name (optional): "enum Name {" or "enum {"
        var enumMatch = Regex.Match(line, @"enum\s+([A-Za-z_][A-Za-z0-9_]*)?\s*\{");
        var enumName = enumMatch.Success && enumMatch.Groups.Count > 1 ? enumMatch.Groups[1].Value : "";

        var enumDef = new CEnum { Name = enumName };

        index++; // Move past opening brace

        // Parse enum values
        while (index < lines.Count)
        {
            var valueLine = lines[index].Trim();

            if (valueLine.StartsWith("}"))
                break;

            // Parse: NAME = value, or NAME,
            var valueMatch = Regex.Match(valueLine, @"([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(.+?))?[,}]");
            if (valueMatch.Success)
            {
                enumDef.Values.Add(new CEnumValue
                {
                    Name = valueMatch.Groups[1].Value,
                    Value = valueMatch.Groups.Count > 2 ? valueMatch.Groups[2].Value.Trim() : ""
                });
            }

            index++;
        }

        return enumDef;
    }
}
