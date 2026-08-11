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

        var ffiPath = Path.Combine(_outputPath, "std", "ffi");
        var stubsPath = Path.Combine(_outputPath, "stubs");

        var sfdFiles = Directory.GetFiles(sfdPath, "*_lib.sfd")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Console.WriteLine($"Found {sfdFiles.Length} SFD files");

        // First pass: collect all includes from all SFD files
        var allIncludes = new HashSet<string>();
        var allLibraries = new List<SfdParser.SfdLibrary>();

        foreach (var sfdFile in sfdFiles)
        {
            var library = SfdParser.ParseFile(sfdFile);
            if (string.IsNullOrWhiteSpace(library.LibraryName))
                throw new InvalidDataException($"{Path.GetFileName(sfdFile)} has no library identity");
            allLibraries.Add(library);

            foreach (var include in library.Includes)
            {
                allIncludes.Add(include);
            }
        }

        // Generate central structs file from ALL headers
        Console.WriteLine("\nGenerating central amiga_structs.novus...");
        var allFunctions = allLibraries.SelectMany(library => library.Functions).ToList();
        GenerateCentralStructsFile(
            allIncludes.ToList(),
            allFunctions.Select(function => function.Name).ToHashSet(StringComparer.Ordinal),
            allFunctions.SelectMany(function => function.Parameters.Select(parameter => parameter.Type)
                .Append(function.ReturnType)));
        GenerateHeaderMap(allLibraries);
        GenerateVersionMap(allLibraries);

        // Second pass: generate bindings
        Console.WriteLine("\nGenerating FFI bindings...");
        foreach (var library in allLibraries)
        {
            Console.WriteLine($"Processing {library.LibraryName}...");

            // Generate Novus FFI bindings
            GenerateNovusBindings(library);

            // Generate assembly stubs
            GenerateAssemblyStubs(library);
        }
    }

    private void GenerateHeaderMap(IEnumerable<SfdParser.SfdLibrary> libraries)
    {
        var lines = libraries
            .Where(library => !string.IsNullOrWhiteSpace(library.LibraryName))
            .Select(library =>
            {
                var moduleName = library.LibraryName.Replace(".library", "").Replace(".", "_");
                var headers = library.Includes
                    .Select(include => include.Trim('<', '>', '"'))
                    .Where(include => File.Exists(Path.Combine(_ndkPath, "Include", "include_h", include)))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(include => include, StringComparer.Ordinal);
                return $"{moduleName}|{string.Join(',', headers)}";
            })
            .OrderBy(line => line, StringComparer.Ordinal);
        var outputFile = Path.Combine(_outputPath, "std", "ffi", "ndk_headers.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        File.WriteAllLines(outputFile, lines);
    }

    private void GenerateVersionMap(IEnumerable<SfdParser.SfdLibrary> libraries)
    {
        var lines = libraries
            .SelectMany(library => library.Functions
                .Where(function => !function.IsReserved && function.Version > 0)
                .Select(function =>
                    $"{library.LibraryName.Replace(".library", "").Replace(".", "_")}|{function.Name}|{function.Version}"))
            .OrderBy(line => line, StringComparer.Ordinal);
        var outputFile = Path.Combine(_outputPath, "std", "ffi", "ndk_versions.txt");
        File.WriteAllLines(outputFile, lines);
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
        sb.AppendLine($"// Base: {(string.IsNullOrWhiteSpace(library.BaseSymbol) ? "caller-supplied" : library.BaseSymbol)}");
        sb.AppendLine("//");
        sb.AppendLine("// NOTE: Constants are in std::ffi::amiga_consts");
        sb.AppendLine("// NOTE: Structs are in std::ffi::amiga_structs");
        sb.AppendLine();
        sb.AppendLine("from std::ffi::amiga_structs import *");
        sb.AppendLine();

        // Function declarations section
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Library Functions");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // Reserved slots have no callable API. Aliases and varargs are official NDK APIs.
        var realFunctions = library.Functions
            .Where(f => !f.IsReserved)
            .ToList();

        foreach (var func in realFunctions)
        {
            // Generate extern function declaration
            sb.AppendLine($"@library(\"{library.LibraryName}\")");
            sb.Append($"extern pub fn {func.Name}(");

            // Generate parameters
            var novusParams = new List<string>();
            for (int i = 0; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                if (param.Type == "...")
                {
                    novusParams.Add("...args");
                    continue;
                }
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
        sb.AppendLine($"; Base: {(string.IsNullOrWhiteSpace(library.BaseSymbol) ? "caller-supplied" : library.BaseSymbol)}");
        sb.AppendLine("; Each function is in its own section for dead code elimination");
        sb.AppendLine();

        // External declaration for library base (global)
        if (!string.IsNullOrWhiteSpace(library.BaseSymbol))
        {
            sb.AppendLine($"\txref\t{library.BaseSymbol}");
            sb.AppendLine();
        }

        // Reserved slots have no callable API. Aliases and varargs share an existing vector.
        var realFunctions = library.Functions
            .Where(f => !f.IsReserved)
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

        // Novus C ABI parameters arrive on the stack at 4(sp), 8(sp), etc.
        // We need to move them to registers according to the SFD

        var usedRegisters = func.Registers
            .Select(reg => reg.ToLowerInvariant())
            .ToArray();
        var preservedRegisters = new[] { "d2", "d3", "d4", "d5", "d6", "d7", "a2", "a3", "a4", "a5", "a6" }
            .Where(reg => reg == "a6" || usedRegisters.Any(spec => RegisterSpecContains(spec, reg)))
            .ToArray();
        var preservedRegisterList = string.Join("/", preservedRegisters);

        sb.AppendLine($"\tmovem.l\t{preservedRegisterList},-(sp)");
        var stackOffset = 4 + preservedRegisters.Length * 4; // Return address + saved registers

        var registerParameterCount = func.IsVarargs ? func.Registers.Count - 1 : func.Registers.Count;
        for (int i = 0; i < registerParameterCount && i < func.Parameters.Count; i++)
        {
            var reg = func.Registers[i].ToLower();
            var parameterSize = GetStackSize(func.Parameters[i].Type);

            // Move parameter from stack to register
            if (reg.Contains('-'))
            {
                sb.AppendLine($"\tmovem.l\t{stackOffset}(sp),{reg}");
            }
            else if (reg.StartsWith("d"))
            {
                // Data register
                sb.AppendLine($"\tmove.l\t{stackOffset}(sp),{reg}");
            }
            else if (reg.StartsWith("a"))
            {
                // Address register
                sb.AppendLine($"\tmovea.l\t{stackOffset}(sp),{reg}");
            }

            stackOffset += parameterSize;
        }

        if (func.IsVarargs)
        {
            var concreteParameterCount = func.Parameters.Count(parameter => parameter.Type != "...");
            if (func.Registers.Count == 0 || func.Parameters.LastOrDefault()?.Type != "..." ||
                concreteParameterCount < registerParameterCount || concreteParameterCount > registerParameterCount + 1)
                throw new InvalidDataException($"Unsupported varargs ABI for {library.LibraryName}/{func.Name}");

            var pointerRegister = func.Registers[^1].ToLowerInvariant();
            if (Regex.IsMatch(pointerRegister, @"^a[0-7]$"))
                sb.AppendLine($"\tlea\t{stackOffset}(sp),{pointerRegister}");
            else if (Regex.IsMatch(pointerRegister, @"^d[0-7]$"))
            {
                sb.AppendLine($"\tlea\t{stackOffset}(sp),a6");
                sb.AppendLine($"\tmove.l\ta6,{pointerRegister}");
            }
            else
                throw new InvalidDataException($"Unsupported varargs pointer register '{pointerRegister}' for {library.LibraryName}/{func.Name}");
        }

        // Normal libraries use a global base. cia.resource supplies CIAA/CIAB
        // as the explicit A6 parameter already loaded above.
        if (!string.IsNullOrWhiteSpace(library.BaseSymbol))
            sb.AppendLine($"\tmovea.l\t{library.BaseSymbol},a6");

        // Call the library function
        sb.AppendLine($"\tjsr\t-{func.Offset}(a6)");
        sb.AppendLine($"\tmovem.l\t(sp)+,{preservedRegisterList}");

        // Return value is in d0 (or d0/d1 for 64-bit)
        sb.AppendLine($"\trts");
        sb.AppendLine();
    }

    private static int GetStackSize(string amigaType)
    {
        var type = amigaType.Replace("const ", "", StringComparison.OrdinalIgnoreCase).Trim();
        return type is "DOUBLE" or "QUAD" or "UQUAD" ? 8 : 4;
    }

    private static bool RegisterSpecContains(string spec, string register) =>
        spec == register ||
        spec.Length == 5 && spec[2] == '-' && spec[0] == register[0] && spec[3] == register[0] &&
        spec[1] <= register[1] && register[1] <= spec[4];

    private void GenerateCentralStructsFile(
        List<string> _,
        IReadOnlySet<string> functionNames,
        IEnumerable<string> functionTypes)
    {
        var allStructs = new Dictionary<string, CHeaderParser.CStruct>();
        var allConstants = new Dictionary<string, CHeaderParser.CConstant>();
        var includeRoot = Path.Combine(_ndkPath, "Include", "include_h");
        var visitedHeaders = new HashSet<string>();

        // The NDK is the source of truth; scanning it is both simpler and more complete
        // than maintaining a hand-picked header list.
        foreach (var headerPath in Directory.EnumerateFiles(includeRoot, "*.h", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var parsed = CHeaderParser.ParseFile(headerPath, _ndkPath, visitedHeaders);

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

        // Forward-declared NDK tags still need a Novus name for typed pointers.
        var referencedTags = allStructs.Values
            .SelectMany(structDef => structDef.Fields)
            .Select(field => field.Type)
            .Concat(functionTypes)
            .Select(type => Regex.Match(type, @"\b(?:struct|union)\s+([A-Za-z_][A-Za-z0-9_]*)"))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var tag in referencedTags)
        {
            if (!allStructs.ContainsKey(tag))
                allStructs[tag] = new CHeaderParser.CStruct { Name = tag, TagName = tag };
        }

        // Generate amiga_consts.novus (constants only)
        var constsSb = new StringBuilder();
        constsSb.AppendLine("// Generated from NDK headers by Novus SFD Parser");
        constsSb.AppendLine("// AmigaOS constant definitions (source of truth)");
        constsSb.AppendLine("//");
        constsSb.AppendLine("// This file contains numeric and string constants from the NDK headers.");
        constsSb.AppendLine("// Struct definitions are in std::ffi::amiga_structs");
        constsSb.AppendLine();
        constsSb.AppendLine("// ============================================================================");
        constsSb.AppendLine("// Constants");
        constsSb.AppendLine("// ============================================================================");
        constsSb.AppendLine();

        var constantNames = allConstants.Keys.ToHashSet(StringComparer.Ordinal);
        var stringConstants = allConstants.Values
            .Where(constant => IsCStringLiteral(constant.Value))
            .ToDictionary(constant => constant.Name, constant => constant.Value.Trim(), StringComparer.Ordinal);
        bool addedStringAlias;
        do
        {
            addedStringAlias = false;
            foreach (var constant in allConstants.Values)
            {
                var alias = constant.Value.Trim();
                if (!stringConstants.ContainsKey(constant.Name) && stringConstants.ContainsKey(alias))
                {
                    stringConstants[constant.Name] = alias;
                    addedStringAlias = true;
                }
            }
        } while (addedStringAlias);

        var convertedConstants = allConstants.Values
            .Select(constant => (constant.Name, Value: ConvertConstantValue(constant.Value, constantNames)))
            .Where(constant => constant.Value != null)
            .ToDictionary(constant => constant.Name, constant => constant.Value!, StringComparer.Ordinal);
        List<string> invalidAliases;
        do
        {
            invalidAliases = convertedConstants
                .Where(constant => GetConstantIdentifiers(constant.Value)
                    .Any(identifier => !convertedConstants.ContainsKey(identifier)))
                .Select(constant => constant.Key)
                .ToList();
            foreach (var name in invalidAliases)
                convertedConstants.Remove(name);
        } while (invalidAliases.Count > 0);

        foreach (var constant in TopologicalSortConstants(convertedConstants))
            constsSb.AppendLine($"pub const {constant.Key}: u32 = {constant.Value}");
        if (!allConstants.ContainsKey("WA_SIZE_UNLIMITED"))
            constsSb.AppendLine("pub const WA_SIZE_UNLIMITED: u32 = $FFFFFFFF");
        foreach (var constant in TopologicalSortConstants(stringConstants))
            constsSb.AppendLine($"pub const {constant.Key}: *u8 = {constant.Value}");

        var constsOutputFile = Path.Combine(_outputPath, "std", "ffi", "amiga_consts.novus");
        Directory.CreateDirectory(Path.GetDirectoryName(constsOutputFile)!);
        File.WriteAllText(constsOutputFile, constsSb.ToString());
        var includedCount = convertedConstants.Count + stringConstants.Count;
        var skippedCount = allConstants.Count - includedCount;
        Console.WriteLine($"  Generated {constsOutputFile} with {includedCount} constants ({skippedCount} skipped)");

        var unsupportedOutputFile = Path.Combine(_outputPath, "std", "ffi", "ndk_unsupported_macros.txt");
        var includedNames = convertedConstants.Keys.Concat(stringConstants.Keys).ToHashSet(StringComparer.Ordinal);
        File.WriteAllLines(unsupportedOutputFile, allConstants.Values
            .Where(constant => !includedNames.Contains(constant.Name))
            .OrderBy(constant => constant.Name, StringComparer.Ordinal)
            .Select(constant => $"{constant.Name} = {constant.Value}"));

        // Generate amiga_structs.novus (structs only)
        var structsSb = new StringBuilder();
        structsSb.AppendLine("// Generated from NDK headers by Novus SFD Parser");
        structsSb.AppendLine("// AmigaOS struct definitions");
        structsSb.AppendLine("//");
        structsSb.AppendLine("// This file contains struct definitions from the NDK headers.");
        structsSb.AppendLine("// Constants are in std::ffi::amiga_consts");
        structsSb.AppendLine();
        structsSb.AppendLine("from std::ffi::amiga_consts import *");
        structsSb.AppendLine();
        structsSb.AppendLine("// ============================================================================");
        structsSb.AppendLine("// Struct Definitions");
        structsSb.AppendLine("// ============================================================================");
        structsSb.AppendLine();

        var sortedStructs = TopologicalSortStructs(allStructs);
        foreach (var structDef in sortedStructs)
        {
            GenerateNovusStruct(structsSb, structDef, allStructs.Keys, allConstants);
        }

        var structsOutputFile = Path.Combine(_outputPath, "std", "ffi", "amiga_structs.novus");
        Directory.CreateDirectory(Path.GetDirectoryName(structsOutputFile)!);
        File.WriteAllText(structsOutputFile, structsSb.ToString());
        Console.WriteLine($"  Generated {structsOutputFile} with {sortedStructs.Count} structs");

        var ndkTypesOutputFile = Path.Combine(_outputPath, "std", "ffi", "ndk_types.h");
        var ndkTypes = new StringBuilder();
        ndkTypes.AppendLine("/* Generated NDK tag/typedef bridge for Novus C output. */");
        foreach (var structDef in sortedStructs.OrderBy(value => value.Name, StringComparer.Ordinal))
        {
            if (functionNames.Contains(structDef.Name) || structDef.IsTypedef || structDef.IsSynthetic)
                continue;
            var tagName = string.IsNullOrWhiteSpace(structDef.TagName) ? structDef.Name : structDef.TagName;
            ndkTypes.AppendLine($"typedef {(structDef.IsUnion ? "union" : "struct")} {tagName} {structDef.Name};");
        }
        File.WriteAllText(ndkTypesOutputFile, ndkTypes.ToString());
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

        var constantNames = allConstants.Select(constant => constant.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var constant in allConstants)
        {
            // Convert C constant value to Novus
            var novusValue = ConvertConstantValue(constant.Value, constantNames);
            if (novusValue != null)
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
                var taggedType = Regex.Match(field.Type, @"\b(?:struct|union)\s+([A-Za-z_][A-Za-z0-9_]*)");
                var fieldType = taggedType.Success
                    ? taggedType.Groups[1].Value
                    : field.Type.TrimStart('*').Trim();

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

    private void GenerateNovusStruct(
        StringBuilder sb,
        CHeaderParser.CStruct structDef,
        IEnumerable<string> knownStructs,
        IReadOnlyDictionary<string, CHeaderParser.CConstant> constants)
    {
        if (!structDef.IsSynthetic)
            sb.AppendLine("#[extern_type]");
        sb.AppendLine($"pub {(structDef.IsUnion ? "union" : "struct")} {structDef.Name} {{");

        foreach (var field in structDef.Fields)
        {
            var fieldType = GetAmigaCallbackType(structDef.Name, field.Name) ??
                (knownStructs.Contains(field.Type)
                    ? field.Type
                    : SfdParser.MapAmigaTypeToNovus(field.Type));

            if (field.IsArray)
            {
                foreach (var dimension in field.ArraySize.Split("][", StringSplitOptions.TrimEntries)
                             .Select(value => ResolveArraySize(value, constants, []))
                             .Reverse())
                    fieldType = $"[{fieldType}; {dimension}]";
                sb.AppendLine($"    {SanitizeIdentifier(field.Name)}: {fieldType},");
            }
            else
            {
                sb.AppendLine($"    {SanitizeIdentifier(field.Name)}: {fieldType},");
            }
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string? GetAmigaCallbackType(string owner, string field) => (owner, field) switch
    {
        ("Hook", "h_Entry" or "h_SubEntry") =>
            "amiga fn(*Hook in a0, *u8 in a2, *u8 in a1) -> u32 in d0",
        ("Interrupt", "is_Code") or ("IntVector", "iv_Code") =>
            "amiga fn(*u8 in a1) -> u32 in d0",
        _ => null
    };

    private static string ResolveArraySize(
        string expression,
        IReadOnlyDictionary<string, CHeaderParser.CConstant> constants,
        HashSet<string> visiting)
    {
        expression = Regex.Replace(expression, @"\b[A-Za-z_][A-Za-z0-9_]*\b", match =>
        {
            var name = match.Value;
            if (!constants.TryGetValue(name, out var constant) || !visiting.Add(name))
                return name;

            var value = ResolveArraySize(constant.Value, constants, visiting);
            visiting.Remove(name);
            return $"({value})";
        });
        expression = Regex.Replace(
            expression,
            @"0[xX]([0-9A-Fa-f]+)[uUlL]*",
            match => "$" + match.Groups[1].Value);
        return Regex.Replace(expression, @"(?<=\d)[uUlL]+\b", "");
    }

    private string? ConvertConstantValue(string cValue, IReadOnlySet<string> constantNames)
    {
        cValue = cValue.Trim();

        var id = Regex.Match(cValue,
            @"^MAKE_ID\s*\(\s*'(.)'\s*,\s*'(.)'\s*,\s*'(.)'\s*,\s*'(.)'\s*\)$");
        if (id.Success)
        {
            var value = Enumerable.Range(1, 4)
                .Aggregate(0u, (result, index) => (result << 8) | id.Groups[index].Value[0]);
            return $"${value:X8}";
        }

        cValue = Regex.Replace(
            cValue,
            @"\(\s*(BYTE|UBYTE|WORD|UWORD|LONG|ULONG|QUAD|UQUAD|Tag|signed\s+char|unsigned\s+char|short|unsigned\s+short|int|unsigned\s+int|long|unsigned\s+long)\s*\)",
            "");
        cValue = Regex.Replace(cValue,
            @"\(\s*(?:const\s+)?(?:struct\s+[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*)\s*\*+\s*\)",
            "");
        cValue = Regex.Replace(cValue, @"\bNULL\b", "0");

        // C declaration helpers and casts are macros, but not numeric constants.
        if (Regex.IsMatch(cValue, @"\b(const|enum|extern|float|int|long|NULL|register|short|signed|sizeof|static|struct|union|unsigned|void|volatile)\b"))
            return null;

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
        var shiftMatch = Regex.Match(cValue, @"^\(?(\d+)L?\s*<<\s*(\d+)\)?$");
        if (shiftMatch.Success)
        {
            var value = shiftMatch.Groups[1].Value;
            var shift = shiftMatch.Groups[2].Value;
            return $"({value} << {shift})";
        }

        // Handle hex values: 0x1234 -> $1234
        var hexMatch = Regex.Match(cValue, @"^0[xX]([0-9A-Fa-f]+)[uUlL]*$");
        if (hexMatch.Success)
            return "$" + hexMatch.Groups[1].Value;

        // Handle plain positive numbers with optional L suffix
        var numMatch = Regex.Match(cValue, @"^\(?\s*(\d+)L?\s*\)?$");
        if (numMatch.Success)
            return numMatch.Groups[1].Value;

        // Keep arithmetic-only macro expressions usable as Novus constants.
        var expression = Regex.Replace(
            cValue,
            @"0[xX]([0-9A-Fa-f]+)[uUlL]*",
            match => "$" + match.Groups[1].Value);
        expression = Regex.Replace(expression, @"(?<=\d)[uUlL]+\b", "");
        if (Regex.IsMatch(expression, @"^[A-Za-z0-9_$\s()+\-*/%<>&|~^]+$") &&
            HasBalancedParentheses(expression) &&
            GetConstantIdentifiers(expression).All(constantNames.Contains))
            return expression;

        // Skip anything else (expressions, aliases, etc.) - these need manual handling or evaluation
        return null;
    }

    private static bool IsCStringLiteral(string value) =>
        Regex.IsMatch(value.Trim(), "^\\\"(?:\\\\.|[^\\\"\\\\])*\\\"$");

    private static IEnumerable<string> GetConstantIdentifiers(string expression) =>
        Regex.Matches(expression, @"(?<!\$)\b[A-Za-z_][A-Za-z0-9_]*\b")
            .Select(match => match.Value);

    private static IEnumerable<KeyValuePair<string, string>> TopologicalSortConstants(
        IReadOnlyDictionary<string, string> constants)
    {
        var sorted = new List<KeyValuePair<string, string>>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string name)
        {
            if (visited.Contains(name) || !visiting.Add(name)) return;
            foreach (var dependency in GetConstantIdentifiers(constants[name]))
                if (constants.ContainsKey(dependency) && dependency != name)
                    Visit(dependency);
            visiting.Remove(name);
            visited.Add(name);
            sorted.Add(new KeyValuePair<string, string>(name, constants[name]));
        }

        foreach (var name in constants.Keys.OrderBy(name => name, StringComparer.Ordinal)) Visit(name);
        return sorted;
    }

    private static bool HasBalancedParentheses(string expression)
    {
        var depth = 0;
        foreach (var character in expression)
        {
            if (character == '(') depth++;
            if (character == ')' && --depth < 0) return false;
        }
        return depth == 0;
    }

    private string SanitizeIdentifier(string name)
    {
        // Remove characters that aren't valid in Novus identifiers
        var sanitized = name.Replace("[", "").Replace("]", "").Replace("*", "");

        // If the name is a Novus keyword, prefix with underscore
        var keywords = new HashSet<string> { "fn", "let", "const", "if", "else", "while", "for", "match", "return", "struct", "union", "enum", "impl", "type", "blitter" };
        if (keywords.Contains(sanitized))
            return "_" + sanitized;

        return sanitized;
    }
}
