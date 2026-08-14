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
        public string TagName { get; set; } = "";
        public List<CField> Fields { get; set; } = new();
        public bool HasUnion { get; set; }
        public bool IsUnion { get; set; }
        public bool IsTypedef { get; set; }
        public bool IsSynthetic { get; set; }
        public List<CStruct> NestedTypes { get; set; } = new();
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
            if ((line.StartsWith("struct") || line.StartsWith("typedef struct") ||
                 line.StartsWith("union") || line.StartsWith("typedef union")) &&
                (line.Contains("{") || (i + 1 < preprocessed.Count && preprocessed[i + 1].TrimStart().StartsWith("{"))))
            {
                var structDef = ParseStruct(preprocessed, ref i);
                if (structDef != null)
                {
                    AddStructAndNestedTypes(header.Structs, structDef);
                    if (!string.IsNullOrWhiteSpace(structDef.TagName) &&
                        structDef.TagName != structDef.Name)
                    {
                        header.Structs.Add(new CStruct
                        {
                            Name = structDef.TagName,
                            TagName = structDef.TagName
                        });
                    }
                }
            }

            // Parse enum definitions
            if (line.StartsWith("enum") &&
                (line.Contains("{") ||
                 (i + 1 < preprocessed.Count && preprocessed[i + 1].TrimStart().StartsWith("{"))))
            {
                var enumDef = ParseEnum(preprocessed, ref i);
                if (enumDef != null)
                {
                    header.Enums.Add(enumDef);
                    header.Constants.AddRange(enumDef.Values.Select(value => new CConstant
                    {
                        Name = value.Name,
                        Value = value.Value
                    }));
                }
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

    private static void AddStructAndNestedTypes(List<CStruct> structs, CStruct value)
    {
        foreach (var nested in value.NestedTypes)
            AddStructAndNestedTypes(structs, nested);
        structs.Add(value);
    }

    private static List<string> PreprocessLines(string[] lines)
    {
        var result = new List<string>();
        bool inBlockComment = false;
        var logicalLines = new List<string>();
        var continued = "";

        foreach (var physicalLine in lines)
        {
            var part = physicalLine.TrimEnd();
            if (part.EndsWith('\\'))
            {
                continued += part[..^1] + " ";
                continue;
            }
            logicalLines.Add(continued + part);
            continued = "";
        }
        if (continued.Length > 0)
            logicalLines.Add(continued);

        foreach (var line in logicalLines)
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
            {
                var lineCommentIndex = trimmed.IndexOf("//", StringComparison.Ordinal);
                if (lineCommentIndex >= 0)
                    trimmed = trimmed[..lineCommentIndex].Trim();
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

        // Extract struct name from either `struct Name {` or the common NDK
        // style where the opening brace is on the following line.
        var isTypedef = line.StartsWith("typedef struct", StringComparison.Ordinal) ||
                        line.StartsWith("typedef union", StringComparison.Ordinal);
        var isUnion = line.StartsWith("union", StringComparison.Ordinal) ||
                      line.StartsWith("typedef union", StringComparison.Ordinal);
        var structMatch = Regex.Match(line, @"(?:struct|union)(?:\s+([A-Za-z_][A-Za-z0-9_]*))?");
        if (!structMatch.Success || !isTypedef && !structMatch.Groups[1].Success)
            return null;

        var structName = structMatch.Groups[1].Value;
        var structDef = new CStruct
        {
            Name = structName,
            TagName = structName,
            IsTypedef = isTypedef,
            IsUnion = isUnion
        };

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
                    foreach (var declaration in SplitTopLevel(content, ';'))
                        if (!string.IsNullOrWhiteSpace(declaration))
                            structDef.Fields.AddRange(ParseFields(declaration.Trim() + ";"));
                }
            }
            if (isTypedef)
                structDef.Name = GetTypedefName(line) ?? structDef.Name;
            return structDef;
        }

        if (!line.Contains("{"))
            index++; // Move to the separate opening-brace line
        index++; // Move past opening brace

        // Parse fields until we hit closing brace
        while (index < lines.Count)
        {
            var fieldLine = lines[index].Trim();

            if (fieldLine.StartsWith("}"))
                break;

            if ((fieldLine.StartsWith("union") || fieldLine.StartsWith("struct")) &&
                (fieldLine.Contains('{') ||
                 (index + 1 < lines.Count && lines[index + 1].TrimStart().StartsWith("{"))))
            {
                var nested = ParseNestedAggregate(lines, ref index, structDef.Name,
                    structDef.NestedTypes.Count, out var field);
                structDef.HasUnion |= nested.IsUnion;
                structDef.NestedTypes.Add(nested);
                structDef.Fields.Add(field);
                index++;
                continue;
            }

            // Parse field: TYPE name; or TYPE name[SIZE];
            structDef.Fields.AddRange(ParseFields(fieldLine));

            index++;
        }

        if (isTypedef)
            structDef.Name = GetTypedefName(lines[index]) ?? structDef.Name;
        return string.IsNullOrWhiteSpace(structDef.Name) ? null : structDef;
    }

    private static CStruct ParseNestedAggregate(
        List<string> lines,
        ref int index,
        string parentName,
        int ordinal,
        out CField field)
    {
        var declaration = lines[index].Trim();
        var isUnion = declaration.StartsWith("union", StringComparison.Ordinal);
        var aggregate = new CStruct
        {
            Name = $"{parentName}_{(isUnion ? "union" : "struct")}{ordinal}",
            IsUnion = isUnion,
            IsSynthetic = true
        };
        var tag = Regex.Match(declaration, @"^(?:struct|union)\s+([A-Za-z_][A-Za-z0-9_]*)");
        if (tag.Success)
        {
            aggregate.Name = tag.Groups[1].Value;
            aggregate.TagName = aggregate.Name;
            aggregate.IsSynthetic = false;
        }

        if (!declaration.Contains('{'))
            index++;
        index++;

        while (index < lines.Count)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("}"))
            {
                var match = Regex.Match(line, @"}\s*([A-Za-z_][A-Za-z0-9_]*)(?:\s*\[([^\]]+)\])?\s*;");
                var fieldName = match.Success ? match.Groups[1].Value : $"_anonymous{ordinal}";
                if (!tag.Success) aggregate.Name = $"{parentName}_{fieldName}";
                field = new CField
                {
                    Name = fieldName, Type = aggregate.Name,
                    IsArray = match.Groups[2].Success, ArraySize = match.Groups[2].Value
                };
                return aggregate;
            }

            if ((line.StartsWith("union") || line.StartsWith("struct")) &&
                (line.Contains('{') ||
                 (index + 1 < lines.Count && lines[index + 1].TrimStart().StartsWith("{"))))
            {
                var nested = ParseNestedAggregate(lines, ref index, aggregate.Name,
                    aggregate.NestedTypes.Count, out var nestedField);
                aggregate.HasUnion |= nested.IsUnion;
                aggregate.NestedTypes.Add(nested);
                aggregate.Fields.Add(nestedField);
                index++;
                continue;
            }

            aggregate.Fields.AddRange(ParseFields(line));
            index++;
        }

        field = new CField { Name = $"_anonymous{ordinal}", Type = aggregate.Name };
        return aggregate;
    }

    private static string? GetTypedefName(string closingLine)
    {
        var match = Regex.Match(closingLine, @"}\s*([A-Za-z_][A-Za-z0-9_]*)\s*;");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void SkipBracedBlock(List<string> lines, ref int index)
    {
        while (index < lines.Count && !lines[index].Contains('{'))
            index++;

        var depth = 0;
        do
        {
            depth += lines[index].Count(c => c == '{');
            depth -= lines[index].Count(c => c == '}');
            index++;
        } while (index < lines.Count && depth > 0);

        index--;
    }

    private static IEnumerable<CField> ParseFields(string line)
    {
        if (!line.EndsWith(";"))
            yield break;

        var declaration = line.TrimEnd(';').Trim();
        var declarators = SplitTopLevel(declaration, ',');
        if (declarators.Count == 1)
        {
            var field = ParseField(line);
            if (field != null)
                yield return field;
            yield break;
        }

        var firstMatch = Regex.Match(
            declarators[0],
            @"^(.+?)\s+((?:\*+\s*)?[A-Za-z_][A-Za-z0-9_]*(?:\s*\[[^\]]+\])?)$");
        if (!firstMatch.Success)
            yield break;

        var baseType = firstMatch.Groups[1].Value.Trim();
        declarators[0] = firstMatch.Groups[2].Value;
        foreach (var declarator in declarators)
        {
            var field = ParseField($"{baseType} {declarator.Trim()};");
            if (field != null)
                yield return field;
        }
    }

    private static List<string> SplitTopLevel(string value, char separator)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;

        for (var i = 0; i < value.Length; i++)
        {
            depth += value[i] is '(' or '[' ? 1 : value[i] is ')' or ']' ? -1 : 0;
            if (value[i] == separator && depth == 0)
            {
                parts.Add(value[start..i]);
                start = i + 1;
            }
        }

        parts.Add(value[start..]);
        return parts;
    }

    private static CField? ParseField(string line)
    {
        if (!line.EndsWith(";"))
            return null;

        line = line.TrimEnd(';').Trim();

        // Handle direct callbacks and NDK's RET (*name) __CLIB_PROTOTYPE((args)) form.
        var funcPtrMatch = Regex.Match(
            line,
            @"^(.+?)\s*\(\*([A-Za-z_][A-Za-z0-9_]*)(?:\s*\[([^\]]+)\])?\)\s*(?:__CLIB_PROTOTYPE\s*)?\((.*)\)$");
        if (funcPtrMatch.Success)
        {
            var parameters = funcPtrMatch.Groups[4].Value.Trim();
            if (parameters.StartsWith('(') && parameters.EndsWith(')'))
                parameters = parameters[1..^1];
            return new CField
            {
                Type = $"{funcPtrMatch.Groups[1].Value.Trim()} (*{funcPtrMatch.Groups[2].Value})({parameters})",
                Name = funcPtrMatch.Groups[2].Value.Trim(),
                IsArray = funcPtrMatch.Groups[3].Success,
                ArraySize = funcPtrMatch.Groups[3].Value.Trim()
            };
        }

        // Handle arrays: TYPE name[SIZE]
        var arrayMatch = Regex.Match(line, @"^(.+?)\s+(\*+)?([A-Za-z_][A-Za-z0-9_]*)\s*\[(.+)\]$");
        if (arrayMatch.Success)
        {
            return new CField
            {
                Type = (arrayMatch.Groups[1].Value + " " + arrayMatch.Groups[2].Value).Trim(),
                Name = arrayMatch.Groups[3].Value.Trim(),
                IsArray = true,
                ArraySize = arrayMatch.Groups[4].Value.Trim()
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

        if (!line.Contains("{"))
            index++; // Move to the separate opening-brace line
        index++; // Move past opening brace

        string? previousName = null;

        // Parse enum values
        while (index < lines.Count)
        {
            var valueLine = lines[index].Trim();

            if (valueLine.StartsWith("}"))
                break;

            // Parse: NAME = value, or NAME,
            var valueMatch = Regex.Match(valueLine,
                @"^([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(.*?))?\s*(?:[,}]|$)");
            if (valueMatch.Success)
            {
                var name = valueMatch.Groups[1].Value;
                var explicitValue = valueMatch.Groups.Count > 2 ? valueMatch.Groups[2].Value.Trim() : "";
                enumDef.Values.Add(new CEnumValue
                {
                    Name = name,
                    Value = string.IsNullOrWhiteSpace(explicitValue)
                        ? previousName == null ? "0" : $"({previousName} + 1)"
                        : explicitValue
                });
                previousName = name;
            }

            index++;
        }

        return enumDef;
    }
}
