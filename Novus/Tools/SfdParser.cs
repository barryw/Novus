using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Novus.Tools;

/// <summary>
/// Parses Amiga SFD (Standard Function Description) files from NDK 3.9
/// </summary>
public class SfdParser
{
    public class SfdLibrary
    {
        public string LibraryName { get; set; } = "";
        public string BaseSymbol { get; set; } = "";
        public string BaseType { get; set; } = "";
        public int Bias { get; set; }
        public bool IsPublic { get; set; }
        public List<string> Includes { get; set; } = new();
        public List<SfdFunction> Functions { get; set; } = new();
    }

    public class SfdFunction
    {
        public string Name { get; set; } = "";
        public string ReturnType { get; set; } = "";
        public List<SfdParameter> Parameters { get; set; } = new();
        public List<string> Registers { get; set; } = new();
        public int Offset { get; set; }
        public bool IsReserved { get; set; }
        public bool IsAlias { get; set; }
        public bool IsVarargs { get; set; }
        public int Version { get; set; }
    }

    public class SfdParameter
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
    }

    // Note: Can't use simple regex for nested parentheses - handled manually in ParseFile

    public static SfdLibrary ParseFile(string filePath)
    {
        var library = new SfdLibrary();
        var lines = File.ReadAllLines(filePath);
        var currentOffset = 0;
        var currentVersion = 0;
        var isAlias = false;
        var isVarargs = false;
        var reserveCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("*"))
                continue;

            // Handle metadata directives
            if (line.StartsWith("=="))
            {
                var parts = line.Substring(2).Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                var directive = parts[0].ToLower();

                switch (directive)
                {
                    case "base":
                        library.BaseSymbol = parts.Length > 1 ? parts[1] : "";
                        break;
                    case "basetype":
                        library.BaseType = parts.Length > 1 ? parts[1] : "";
                        break;
                    case "libname":
                        library.LibraryName = parts.Length > 1 ? parts[1] : "";
                        break;
                    case "bias":
                        if (parts.Length > 1 && int.TryParse(parts[1], out var bias))
                        {
                            library.Bias = bias;
                            currentOffset = bias;
                        }
                        break;
                    case "public":
                        library.IsPublic = true;
                        break;
                    case "include":
                        if (parts.Length > 1)
                            library.Includes.Add(parts[1]);
                        break;
                    case "version":
                        if (parts.Length > 1 && int.TryParse(parts[1], out var version))
                            currentVersion = version;
                        break;
                    case "alias":
                        isAlias = true;
                        break;
                    case "varargs":
                        isVarargs = true;
                        break;
                    case "reserve":
                        if (parts.Length > 1 && int.TryParse(parts[1], out reserveCount))
                        {
                            currentOffset += reserveCount * 6; // Each slot is 6 bytes
                        }
                        break;
                }
                continue;
            }

            // Handle function declarations (possibly multi-line)
            var functionLine = line;

            // Check if this might be a multi-line function declaration
            while (!functionLine.Contains("(") ||
                   (functionLine.Count(c => c == '(') != functionLine.Count(c => c == ')')))
            {
                i++;
                if (i >= lines.Length)
                    break;
                functionLine += " " + lines[i].Trim();
            }

            // Try to parse as a function manually (can't use regex due to nested parens)
            var parsed = ParseFunctionSignature(functionLine);
            if (parsed != null)
            {
                var function = new SfdFunction
                {
                    ReturnType = parsed.Value.returnType,
                    Name = parsed.Value.name,
                    Offset = currentOffset,
                    Version = currentVersion,
                    IsAlias = isAlias,
                    IsVarargs = isVarargs
                };

                // Parse parameters
                if (!string.IsNullOrWhiteSpace(parsed.Value.parameters))
                {
                    function.Parameters = ParseParameters(parsed.Value.parameters);
                }

                // Parse registers
                if (!string.IsNullOrEmpty(parsed.Value.registers))
                {
                    function.Registers = parsed.Value.registers.Split(',')
                        .Select(r => r.Trim())
                        .Where(r => !string.IsNullOrEmpty(r))
                        .ToList();
                }

                library.Functions.Add(function);
                currentOffset += 6; // Each function is 6 bytes (JMP instruction)

                // Reset per-function flags
                isAlias = false;
                isVarargs = false;
            }
        }

        return library;
    }

    private static (string returnType, string name, string parameters, string registers)? ParseFunctionSignature(string line)
    {
        // Format: RETURNTYPE FunctionName(params) (registers)
        // We need to find the function name, then match parentheses carefully

        // Find the first '(' - this starts the parameters
        var firstParen = line.IndexOf('(');
        if (firstParen < 0)
            return null;

        // Everything before the first '(' is return type + function name
        var beforeParams = line.Substring(0, firstParen).Trim();
        var parts = beforeParams.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        // Last part is function name, everything else is return type
        var functionName = parts[^1];
        var returnType = string.Join(" ", parts.Take(parts.Length - 1));

        // Now extract parameters - match parentheses from firstParen
        int depth = 0;
        int paramStart = firstParen + 1;
        int paramEnd = -1;

        for (int i = firstParen; i < line.Length; i++)
        {
            if (line[i] == '(') depth++;
            if (line[i] == ')') {
                depth--;
                if (depth == 0) {
                    paramEnd = i;
                    break;
                }
            }
        }

        if (paramEnd < 0)
            return null;

        var parameters = line.Substring(paramStart, paramEnd - paramStart).Trim();

        // Now find the register list - it's the next (...) after the parameters
        var registerStart = line.IndexOf('(', paramEnd + 1);
        if (registerStart < 0)
            return null;

        var registerEnd = line.IndexOf(')', registerStart + 1);
        if (registerEnd < 0)
            return null;

        var registers = line.Substring(registerStart + 1, registerEnd - registerStart - 1).Trim();

        return (returnType, functionName, parameters, registers);
    }

    private static List<SfdParameter> ParseParameters(string paramText)
    {
        var parameters = new List<SfdParameter>();

        // Handle special case of "void" or empty parameters
        if (string.IsNullOrWhiteSpace(paramText) || paramText.Trim() == "void")
            return parameters;

        // Split by comma, but be careful about nested types (struct Foo *, etc.)
        var parts = new List<string>();
        var current = "";
        var depth = 0;

        foreach (var c in paramText)
        {
            if (c == ',' && depth == 0)
            {
                parts.Add(current.Trim());
                current = "";
            }
            else
            {
                if (c == '(') depth++;
                if (c == ')') depth--;
                current += c;
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
            parts.Add(current.Trim());

        foreach (var part in parts)
        {
            var param = ParseParameter(part);
            if (param != null)
                parameters.Add(param);
        }

        return parameters;
    }

    private static SfdParameter? ParseParameter(string paramText)
    {
        if (string.IsNullOrWhiteSpace(paramText))
            return null;

        // Handle varargs (...)
        if (paramText.Trim() == "...")
        {
            return new SfdParameter { Type = "...", Name = "" };
        }

        // Handle function pointer types: "ULONG (*userFunction)()"
        // For FFI purposes, we treat these as *u8 and extract the name from inside (*name)
        var funcPtrMatch = System.Text.RegularExpressions.Regex.Match(
            paramText,
            @"(.+?)\s*\(\*(\w+)\)\s*\(.*?\)");

        if (funcPtrMatch.Success)
        {
            var returnType = funcPtrMatch.Groups[1].Value.Trim();
            var name = funcPtrMatch.Groups[2].Value.Trim();
            // Store the whole function pointer type for mapping
            return new SfdParameter { Type = paramText.Trim(), Name = name };
        }

        // Find the parameter name (last identifier that's not a type keyword)
        // Type can be: "LONG", "const APTR", "struct Foo *", etc.

        // Simple heuristic: the name is the last word
        var tokens = paramText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens is [])
            return null;

        // The name is typically the last token
        var name2 = tokens[^1];

        // But if it's just a type with no name, use the whole thing as type
        if (tokens.Length == 1 && (name2.Contains("*") || name2.All(char.IsUpper)))
        {
            return new SfdParameter { Type = paramText.Trim(), Name = "" };
        }

        // Build the type from everything except the last token
        var type = string.Join(" ", tokens.Take(tokens.Length - 1));

        // If there's no type, then the name is actually the type
        if (string.IsNullOrWhiteSpace(type))
        {
            return new SfdParameter { Type = paramText.Trim(), Name = "" };
        }

        return new SfdParameter { Type = type.Trim(), Name = name2.Trim() };
    }

    public static Dictionary<string, string> GetAmigaTypeMap()
    {
        return new Dictionary<string, string>
        {
            // Basic types
            ["BYTE"] = "i8",
            ["UBYTE"] = "u8",
            ["WORD"] = "i16",
            ["UWORD"] = "u16",
            ["LONG"] = "i32",
            ["ULONG"] = "u32",
            ["BOOL"] = "i16",

            // Pointer types
            ["APTR"] = "*u8",
            ["STRPTR"] = "*u8",
            ["CONST_STRPTR"] = "*u8",
            ["BPTR"] = "i32",  // BCPL pointer - special case!
            ["PLANEPTR"] = "*u8",

            // Void
            ["VOID"] = "void",
            ["void"] = "void",

            // Const variants
            ["const APTR"] = "*u8",
            ["const STRPTR"] = "*u8",
            ["CONST APTR"] = "*u8",
        };
    }

    public static string MapAmigaTypeToNovus(string amigaType)
    {
        var typeMap = GetAmigaTypeMap();

        // Try direct lookup
        if (typeMap.TryGetValue(amigaType, out var mapped))
            return mapped;

        // Handle "const" prefix
        var cleanType = amigaType
            .Replace("const ", "")
            .Replace("CONST ", "")
            .Trim();

        if (typeMap.TryGetValue(cleanType, out mapped))
            return mapped;

        // Handle function pointer types (e.g., "ULONG (*func)()")
        if (amigaType.Contains("(*") && amigaType.Contains(")"))
            return "*u8";  // Function pointers become *u8 for FFI

        // Handle struct pointer types (struct Foo *)
        if (amigaType.Contains("struct ") && amigaType.Contains("*"))
        {
            // Extract struct name and preserve it
            var structMatch = System.Text.RegularExpressions.Regex.Match(
                amigaType,
                @"struct\s+(\w+)\s*\*");

            if (structMatch.Success)
            {
                var structName = structMatch.Groups[1].Value;
                return $"*{structName}";  // Preserve struct name for type safety
            }

            return "*u8";  // Fallback if we can't parse it
        }

        // Handle other pointer types that aren't structs
        if (amigaType.Contains("*"))
            return "*u8";  // Generic pointers become *u8

        // Handle struct types without pointer (unusual, but possible)
        if (amigaType.StartsWith("struct "))
        {
            var structMatch = System.Text.RegularExpressions.Regex.Match(
                amigaType,
                @"struct\s+(\w+)");

            if (structMatch.Success)
            {
                var structName = structMatch.Groups[1].Value;
                return structName;  // Return bare struct name (passed by value)
            }
        }

        // Unknown type - keep as-is and add comment
        return $"*u8 /* {amigaType} */";
    }
}
