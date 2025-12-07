using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Novus.Tools;

/// <summary>
/// Generates Novus FFI bindings and assembly stubs from SFD files
/// </summary>
public class SfdGenerator
{
    private readonly string _ndkPath;
    private readonly string _outputPath;

    public SfdGenerator(string ndkPath, string outputPath)
    {
        _ndkPath = ndkPath;
        _outputPath = outputPath;
    }

    public void GenerateAllBindings()
    {
        var sfdPath = Path.Combine(_ndkPath, "Include", "sfd");
        if (!Directory.Exists(sfdPath))
        {
            Console.WriteLine($"SFD directory not found: {sfdPath}");
            return;
        }

        // Clean up old generated files
        var ffiPath = Path.Combine(_outputPath, "std", "ffi");
        var stubsPath = Path.Combine(_outputPath, "stubs");

        if (Directory.Exists(ffiPath))
        {
            foreach (var file in Directory.GetFiles(ffiPath, "*.novus"))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(stubsPath))
        {
            foreach (var file in Directory.GetFiles(stubsPath, "*_stubs.s"))
            {
                File.Delete(file);
            }
        }

        var sfdFiles = Directory.GetFiles(sfdPath, "*_lib.sfd");
        Console.WriteLine($"Found {sfdFiles.Length} SFD files");

        // First pass: collect all includes from all SFD files
        var allIncludes = new HashSet<string>();
        var allLibraries = new List<SfdParser.SfdLibrary>();

        foreach (var sfdFile in sfdFiles)
        {
            try
            {
                var library = SfdParser.ParseFile(sfdFile);
                allLibraries.Add(library);

                foreach (var include in library.Includes)
                {
                    allIncludes.Add(include);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing {sfdFile}: {ex.Message}");
            }
        }

        // Generate central structs file from ALL headers
        Console.WriteLine("\nGenerating central amiga_structs.novus...");
        GenerateCentralStructsFile(allIncludes.ToList());

        // Second pass: generate bindings
        Console.WriteLine("\nGenerating FFI bindings...");
        foreach (var library in allLibraries)
        {
            try
            {
                Console.WriteLine($"Processing {library.LibraryName}...");

                // Generate Novus FFI bindings
                GenerateNovusBindings(library);

                // Generate assembly stubs
                GenerateAssemblyStubs(library);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing library: {ex.Message}");
            }
        }
    }

    private void GenerateNovusBindings(SfdParser.SfdLibrary library)
    {
        var sb = new StringBuilder();

        // Get library name without .library suffix
        var libName = library.LibraryName.Replace(".library", "").Replace(".", "_");

        // Skip if no library name (incomplete SFD file)
        if (string.IsNullOrWhiteSpace(libName))
        {
            Console.WriteLine($"  Skipping - no library name defined");
            return;
        }

        sb.AppendLine("// Generated from SFD file by Novus SFD Parser");
        sb.AppendLine($"// Library: {library.LibraryName}");
        sb.AppendLine($"// Base: {library.BaseSymbol}");
        sb.AppendLine("//");
        sb.AppendLine("// NOTE: Constants are in std::ffi::amiga_consts");
        sb.AppendLine("// NOTE: Structs are in std::ffi::amiga_structs");
        sb.AppendLine();

        // Function declarations section
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Library Functions");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // Only generate real functions (not aliases or reserved slots)
        var realFunctions = library.Functions
            .Where(f => !f.IsReserved && !f.IsAlias)
            .ToList();

        foreach (var func in realFunctions)
        {
            // Skip varargs for now - they need special handling
            if (func.IsVarargs)
            {
                sb.AppendLine($"// Varargs function {func.Name} skipped - needs manual implementation");
                continue;
            }

            // Generate extern function declaration
            sb.Append($"extern pub fn {func.Name}(");

            // Generate parameters
            var novusParams = new List<string>();
            for (int i = 0; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                var paramType = SfdParser.MapAmigaTypeToNovus(param.Type);
                var paramName = string.IsNullOrWhiteSpace(param.Name) ? $"arg{i}" : SanitizeIdentifier(param.Name);

                novusParams.Add($"{paramName}: {paramType}");
            }

            sb.Append(string.Join(", ", novusParams));
            sb.Append(")");

            // Generate return type
            var returnType = SfdParser.MapAmigaTypeToNovus(func.ReturnType);
            if (returnType != "void")
            {
                sb.Append($" -> {returnType}");
            }

            sb.AppendLine();
        }

        // Write to file
        var outputFile = Path.Combine(_outputPath, "std", "ffi", $"{libName}.novus");
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        File.WriteAllText(outputFile, sb.ToString());
        Console.WriteLine($"  Generated {outputFile}");
    }

    private void GenerateAssemblyStubs(SfdParser.SfdLibrary library)
    {
        var sb = new StringBuilder();

        // Get library name without .library suffix
        var libName = library.LibraryName.Replace(".library", "").Replace(".", "_");

        // Skip if no library name (incomplete SFD file)
        if (string.IsNullOrWhiteSpace(libName))
        {
            return;
        }

        sb.AppendLine("; Generated from SFD file by Novus SFD Parser");
        sb.AppendLine($"; Library: {library.LibraryName}");
        sb.AppendLine($"; Base: {library.BaseSymbol}");
        sb.AppendLine("; Each function is in its own section for dead code elimination");
        sb.AppendLine();

        // External declaration for library base (global)
        sb.AppendLine($"\txref\t{library.BaseSymbol}");
        sb.AppendLine();

        // Only generate real functions (not aliases or reserved slots)
        var realFunctions = library.Functions
            .Where(f => !f.IsReserved && !f.IsAlias && !f.IsVarargs)
            .ToList();

        foreach (var func in realFunctions)
        {
            GenerateStubFunction(sb, func, library);
        }

        // Write to file
        var outputFile = Path.Combine(_outputPath, "stubs", $"{libName}_stubs.s");
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        File.WriteAllText(outputFile, sb.ToString());
        Console.WriteLine($"  Generated {outputFile}");
    }

    private void GenerateStubFunction(StringBuilder sb, SfdParser.SfdFunction func, SfdParser.SfdLibrary library)
    {
        // Put each stub in its own section for dead code elimination
        sb.AppendLine($"\tsection\t_{func.Name}_stub,code");
        sb.AppendLine();
        sb.AppendLine($"; {func.ReturnType} {func.Name}({string.Join(", ", func.Parameters.Select(p => $"{p.Type} {p.Name}"))})");
        sb.AppendLine($"\txdef\t_{func.Name}");
        sb.AppendLine($"_{func.Name}:");

        // For 68000, parameters come on stack at 4(sp), 8(sp), etc.
        // We need to move them to registers according to the SFD

        var stackOffset = 4; // Skip return address
        var usedRegisters = new HashSet<string>();

        for (int i = 0; i < func.Registers.Count && i < func.Parameters.Count; i++)
        {
            var reg = func.Registers[i].ToLower();
            usedRegisters.Add(reg);

            // Move parameter from stack to register
            if (reg.StartsWith("d"))
            {
                // Data register
                sb.AppendLine($"\tmove.l\t{stackOffset}(sp),{reg}");
            }
            else if (reg.StartsWith("a"))
            {
                // Address register
                sb.AppendLine($"\tmovea.l\t{stackOffset}(sp),{reg}");
            }

            stackOffset += 4; // Each parameter is 4 bytes (LONG/pointer)
        }

        // Load library base into a6
        sb.AppendLine($"\tmovea.l\t{library.BaseSymbol},a6");

        // Call the library function
        sb.AppendLine($"\tjsr\t-{func.Offset}(a6)");

        // Return value is in d0 (or d0/d1 for 64-bit)
        sb.AppendLine($"\trts");
        sb.AppendLine();
    }

    private void GenerateCentralStructsFile(List<string> allIncludes)
    {
        var allStructs = new Dictionary<string, CHeaderParser.CStruct>();
        var allConstants = new Dictionary<string, CHeaderParser.CConstant>();

        // Always include core Amiga headers that define fundamental types
        var coreHeaders = new List<string>
        {
            "exec/types.h",           // Node, List, MinNode, MinList
            "utility/tagitem.h",      // TagItem
            "exec/devices.h",         // Device, Unit
            "graphics/gfx.h",         // Rectangle, BitMap, RastPort
            "graphics/layers.h",      // Layer
            "intuition/intuition.h",  // Screen, Window, Requester, NewWindow, Gadget, Image
            "intuition/intuitionbase.h",  // IntuitionBase
            "intuition/screens.h",    // Screen functions
            "exec/ports.h",           // MsgPort, Message
            "exec/libraries.h",       // Library
            "exec/tasks.h",           // Task
            "dos/dos.h"               // DOS structures
        };

        // Combine core headers with SFD includes
        var allIncludesToParse = new HashSet<string>(coreHeaders);
        foreach (var include in allIncludes)
        {
            allIncludesToParse.Add(include.Trim('<', '>', '"'));
        }

        // Parse all headers and collect structs AND constants (with transitive includes)
        foreach (var includePath in allIncludesToParse)
        {
            var headerPath = Path.Combine(_ndkPath, "Include", "include_h", includePath);

            if (!File.Exists(headerPath))
                continue;

            // Parse with transitive includes enabled
            var parsed = CHeaderParser.ParseFile(headerPath, _ndkPath);

            foreach (var s in parsed.Structs)
            {
                if (!allStructs.ContainsKey(s.Name))
                    allStructs[s.Name] = s;
            }

            foreach (var c in parsed.Constants)
            {
                if (!allConstants.ContainsKey(c.Name))
                    allConstants[c.Name] = c;
            }
        }

        // Generate amiga_consts.novus (constants only)
        var constsSb = new StringBuilder();
        constsSb.AppendLine("// Generated from NDK headers by Novus SFD Parser");
        constsSb.AppendLine("// AmigaOS constant definitions (source of truth)");
        constsSb.AppendLine("//");
        constsSb.AppendLine("// This file contains ALL constants from the NDK headers.");
        constsSb.AppendLine("// Struct definitions are in std::ffi::amiga_structs");
        constsSb.AppendLine();
        constsSb.AppendLine("// ============================================================================");
        constsSb.AppendLine("// Constants");
        constsSb.AppendLine("// ============================================================================");
        constsSb.AppendLine();

        int skippedCount = 0;
        foreach (var constant in allConstants.Values.OrderBy(c => c.Name))
        {
            var novusValue = ConvertConstantValue(constant.Value);
            // Skip constants that couldn't be converted (complex macros, field accessors, etc.)
            if (novusValue == null)
            {
                skippedCount++;
                continue;
            }
            constsSb.AppendLine($"pub const {constant.Name}: u32 = {novusValue}");
        }

        var constsOutputFile = Path.Combine(_outputPath, "std", "ffi", "amiga_consts.novus");
        Directory.CreateDirectory(Path.GetDirectoryName(constsOutputFile)!);
        File.WriteAllText(constsOutputFile, constsSb.ToString());
        var includedCount = allConstants.Count - skippedCount;
        Console.WriteLine($"  Generated {constsOutputFile} with {includedCount} constants ({skippedCount} skipped)");

        // Generate amiga_structs.novus (structs only)
        var structsSb = new StringBuilder();
        structsSb.AppendLine("// Generated from NDK headers by Novus SFD Parser");
        structsSb.AppendLine("// AmigaOS struct definitions");
        structsSb.AppendLine("//");
        structsSb.AppendLine("// This file contains struct definitions from the NDK headers.");
        structsSb.AppendLine("// Constants are in std::ffi::amiga_consts");
        structsSb.AppendLine();
        structsSb.AppendLine("// ============================================================================");
        structsSb.AppendLine("// Struct Definitions");
        structsSb.AppendLine("// ============================================================================");
        structsSb.AppendLine();

        var sortedStructs = TopologicalSortStructs(allStructs);
        foreach (var structDef in sortedStructs)
        {
            GenerateNovusStruct(structsSb, structDef);
        }

        var structsOutputFile = Path.Combine(_outputPath, "std", "ffi", "amiga_structs.novus");
        Directory.CreateDirectory(Path.GetDirectoryName(structsOutputFile)!);
        File.WriteAllText(structsOutputFile, structsSb.ToString());
        Console.WriteLine($"  Generated {structsOutputFile} with {sortedStructs.Count} structs");
    }

    private void GenerateConstants(StringBuilder sb, List<string> includes)
    {
        var allConstants = new List<CHeaderParser.CConstant>();

        // Parse each included header
        foreach (var include in includes)
        {
            // Convert <exec/memory.h> to actual path
            var includePath = include.Trim('<', '>', '"');
            var headerPath = Path.Combine(_ndkPath, "Include", "include_h", includePath);

            if (!File.Exists(headerPath))
                continue;

            // Parse with transitive includes
            var parsed = CHeaderParser.ParseFile(headerPath, _ndkPath);

            // Collect constants only (structs are in central amiga_structs.novus)
            allConstants.AddRange(parsed.Constants);
        }

        // Generate constants section
        if (allConstants.Count > 0)
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Constants");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();

            foreach (var constant in allConstants)
            {
                // Convert C constant value to Novus
                var novusValue = ConvertConstantValue(constant.Value);
                sb.AppendLine($"pub const {constant.Name}: u32 = {novusValue}");
            }

            sb.AppendLine();
        }
    }

    private List<CHeaderParser.CStruct> TopologicalSortStructs(Dictionary<string, CHeaderParser.CStruct> structs)
    {
        var sorted = new List<CHeaderParser.CStruct>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(string name)
        {
            if (visited.Contains(name))
                return;

            if (visiting.Contains(name))
            {
                // Circular dependency - just skip for now
                return;
            }

            if (!structs.ContainsKey(name))
                return;

            visiting.Add(name);

            var structDef = structs[name];

            // Visit dependencies first
            foreach (var field in structDef.Fields)
            {
                // Extract struct name from type like "*MemChunk" or "Node"
                var fieldType = field.Type.TrimStart('*').Trim();

                // If it's a struct type (not a primitive), visit it first
                if (structs.ContainsKey(fieldType))
                {
                    Visit(fieldType);
                }
            }

            visiting.Remove(name);
            visited.Add(name);
            sorted.Add(structDef);
        }

        foreach (var name in structs.Keys)
        {
            Visit(name);
        }

        return sorted;
    }

    private void GenerateNovusStruct(StringBuilder sb, CHeaderParser.CStruct structDef)
    {
        sb.AppendLine($"pub struct {structDef.Name} {{");

        if (structDef.HasUnion)
        {
            sb.AppendLine("    // WARNING: Contains union - manual conversion required");
            sb.AppendLine("    // Novus does not support C unions; use largest variant + unsafe casts");
        }

        foreach (var field in structDef.Fields)
        {
            var fieldType = SfdParser.MapAmigaTypeToNovus(field.Type);

            if (field.IsArray)
            {
                // Novus arrays: fieldName: [Type; size]
                sb.AppendLine($"    {field.Name}: [{fieldType}; {field.ArraySize}],");
            }
            else
            {
                sb.AppendLine($"    {field.Name}: {fieldType},");
            }
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private string? ConvertConstantValue(string cValue)
    {
        cValue = cValue.Trim();

        // Skip struct field accessors (contain dots) - these are C macros for nested field access
        if (cValue.Contains("."))
            return null;

        // Handle (0L) or (0) or 0L
        if (cValue == "(0L)" || cValue == "(0)" || cValue == "0L")
            return "0";

        // Handle negative numbers: -2, -1, (-1), etc.
        var negMatch = Regex.Match(cValue, @"^\(?\s*(-\d+)L?\s*\)?$");
        if (negMatch.Success)
            return negMatch.Groups[1].Value;

        // Handle character constants: 'R', 'W', etc.
        var charMatch = Regex.Match(cValue, @"^'(.)'$");
        if (charMatch.Success)
        {
            char c = charMatch.Groups[1].Value[0];
            return ((int)c).ToString();
        }

        // Handle (1L<<n) or (1<<n) bit shift patterns - with or without parens
        var shiftMatch = Regex.Match(cValue, @"\(?(\d+)L?\s*<<\s*(\d+)\)?");
        if (shiftMatch.Success)
        {
            var value = shiftMatch.Groups[1].Value;
            var shift = shiftMatch.Groups[2].Value;
            return $"(1 << {shift})";
        }

        // Handle hex values: 0x1234 -> $1234
        if (cValue.StartsWith("0x") || cValue.StartsWith("0X"))
        {
            var hexPart = cValue.Substring(2).TrimEnd('L', 'l');
            return "$" + hexPart;
        }

        // Handle plain positive numbers with optional L suffix
        var numMatch = Regex.Match(cValue, @"^\(?\s*(\d+)L?\s*\)?$");
        if (numMatch.Success)
            return numMatch.Groups[1].Value;

        // Skip anything else (expressions, aliases, etc.) - these need manual handling or evaluation
        return null;
    }

    private string SanitizeIdentifier(string name)
    {
        // Remove characters that aren't valid in Novus identifiers
        var sanitized = name.Replace("[", "").Replace("]", "").Replace("*", "");

        // If the name is a Novus keyword, prefix with underscore
        var keywords = new HashSet<string> { "fn", "let", "const", "if", "else", "while", "for", "match", "return", "struct", "enum", "impl", "type" };
        if (keywords.Contains(sanitized))
            return "_" + sanitized;

        return sanitized;
    }
}
