using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Novus.Tools;

public sealed class NdkCoverageManifest
{
    public int SchemaVersion { get; set; } = 2;
    public NdkBaseline Baseline { get; set; } = new();
    public List<NdkInterface> Interfaces { get; set; } = [];
    public List<NdkSymbol> Symbols { get; set; } = [];
    public List<NdkExtension> Extensions { get; set; } = [];
    public List<NdkExtensionSymbol> ExtensionSymbols { get; set; } = [];
    public Dictionary<string, int> Summary { get; set; } = new(StringComparer.Ordinal);
}

public sealed class NdkBaseline
{
    public string Name { get; set; } = "";
    public string Release { get; set; } = "";
    public string Platform { get; set; } = "classic-68k-amigaos";
    public string SourceLayout { get; set; } = "";
    public string InputSha256 { get; set; } = "";
    public string InventorySha256 { get; set; } = "";
    public List<string> ExcludedCompilerInterfaceDirectories { get; set; } = [];
    public string ThirdPartyHeaderPacks { get; set; } = "none";
}

public sealed class NdkInterface
{
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Source { get; set; } = "";
    public string NovusModule { get; set; } = "";
    public string Status { get; set; } = "DIRECTLY_SUPPORTED";
    public int MinimumVersion { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class NdkSymbol
{
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Interface { get; set; } = "";
    public List<string> Sources { get; set; } = [];
    public int MinimumVersion { get; set; }
    public string NovusModule { get; set; } = "";
    public string Status { get; set; } = "";
    public string Definition { get; set; } = "";
    public string Notes { get; set; } = "";

    [JsonIgnore]
    public string Id => $"{Category}|{Interface}|{Name}";
}

public sealed class NdkExtension
{
    public string Module { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class NdkExtensionSymbol
{
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Module { get; set; } = "";
    public string Scope { get; set; } = "NOVUS_SUPPORT";
    public string Notes { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(NdkCoverageManifest))]
internal partial class NdkCoverageJsonContext : JsonSerializerContext;

/// <summary>Builds and verifies the licensed-NDK inventory snapshot checked into the repo.</summary>
public static class NdkCoverage
{
    private static readonly HashSet<string> ExcludedHeaderDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "clib", "defines", "inline", "pragma", "pragmas", "proto"
    };

    private static readonly HashSet<string> COnlyMacros = new(StringComparer.Ordinal)
    {
        "CONST", "EXTERN", "FOREVER", "GLOBAL", "IMPORT", "REGISTER", "STATIC", "VOID", "VOLATILE",
        "__CLIB_PROTOTYPE"
    };

    private static readonly Regex PublicFunction = new(
        @"extern\s+pub\s+fn\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);
    private static readonly Regex PublicType = new(
        @"pub\s+(?:struct|union|enum|type)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex PublicConstant = new(
        @"pub\s+const\s+([A-Za-z_][A-Za-z0-9_]*)\s*:", RegexOptions.Compiled);

    public static NdkCoverageManifest Generate(string ndkPath, string rawPath)
    {
        ValidateNdk39(ndkPath);
        var includeRoot = Path.Combine(ndkPath, "Include", "include_h");
        var raw = ReadRawSurface(rawPath);
        var manifest = new NdkCoverageManifest
        {
            Baseline = new NdkBaseline
            {
                Name = "Native Developer Kit for AmigaOS 3.9",
                Release = "NDK 3.9 (2001 distribution)",
                SourceLayout = "README; Include/sfd; Include/fd; Include/include_h",
                InputSha256 = HashAuthoritativeInputs(ndkPath),
                ExcludedCompilerInterfaceDirectories = ExcludedHeaderDirectories.Order().ToList()
            },
            Extensions =
            [
                new() { Module = "amiga::raw::amissl", Scope = "THIRD_PARTY_EXTENSION", Notes = "AmiSSL; not in the pinned NDK inputs." },
                new() { Module = "amiga::raw::bsdsocket", Scope = "THIRD_PARTY_EXTENSION", Notes = "Roadshow-compatible bsdsocket API; not in Include/include_h of the pinned NDK." },
                new() { Module = "amiga::raw::mui_tags", Scope = "THIRD_PARTY_EXTENSION", Notes = "MUI; not part of AmigaOS NDK 3.9." },
                new() { Module = "amiga::raw::reaction_tags", Scope = "NDK_REACTION", Notes = "Hand-maintained ReAction convenience aliases over pinned NDK constants." }
            ],
            ExtensionSymbols =
            [
                new()
                {
                    Category = "type", Name = "InternalLoadSegFree", Module = "amiga::raw::dos",
                    Notes = "Named Novus form of the anonymous callback signature used by dos.library/InternalUnLoadSeg."
                },
                new()
                {
                    Category = "constant", Name = "WA_SIZE_UNLIMITED", Module = "amiga::raw::consts",
                    Notes = "Named Novus sentinel for an unconstrained window dimension; equivalent to the NDK value ~0UL."
                },
                new()
                {
                    Category = "function", Name = "BeginIO", Module = "amiga::raw::exec",
                    Notes = "Compatibility declaration of the canonical amiga.lib routine for existing exec imports."
                }
            ]
        };

        AddSfdFunctions(ndkPath, raw, manifest);
        AddFdOnlyFunctions(ndkPath, raw, manifest);
        AddAmigaLibFunctions(includeRoot, raw, manifest);
        AddHeaderSurface(includeRoot, raw, manifest);
        AddDeviceAndResourceInterfaces(includeRoot, manifest);

        manifest.Symbols = manifest.Symbols
            .GroupBy(symbol => symbol.Id, StringComparer.Ordinal)
            .Select(MergeSymbols)
            .OrderBy(symbol => symbol.Category, StringComparer.Ordinal)
            .ThenBy(symbol => symbol.Interface, StringComparer.Ordinal)
            .ThenBy(symbol => symbol.Name, StringComparer.Ordinal)
            .ToList();
        manifest.Interfaces = manifest.Interfaces
            .GroupBy(value => $"{value.Category}|{value.Name}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(value => value.Category, StringComparer.Ordinal)
            .ThenBy(value => value.Name, StringComparer.Ordinal)
            .ToList();

        manifest.Baseline.InventorySha256 = HashInventory(manifest.Symbols);
        manifest.Summary = manifest.Symbols
            .GroupBy(symbol => $"{symbol.Category.ToLowerInvariant()}_{symbol.Status.ToLowerInvariant()}")
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        manifest.Summary["interfaces_total"] = manifest.Interfaces.Count;
        manifest.Summary["symbols_total"] = manifest.Symbols.Count;
        return manifest;
    }

    public static void Write(NdkCoverageManifest manifest, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, NdkCoverageJsonContext.Default.NdkCoverageManifest) + "\n");
    }

    public static NdkCoverageManifest Read(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), NdkCoverageJsonContext.Default.NdkCoverageManifest)
        ?? throw new InvalidDataException($"Empty NDK coverage manifest: {path}");

    public static List<string> Verify(NdkCoverageManifest manifest, string rawPath, string? ndkPath = null)
    {
        var errors = new List<string>();
        if (manifest.Baseline.Platform != "classic-68k-amigaos")
            errors.Add($"unsupported baseline platform: {manifest.Baseline.Platform}");

        var duplicateManifest = manifest.Symbols.GroupBy(symbol => symbol.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key);
        errors.AddRange(duplicateManifest.Select(id => $"duplicate manifest symbol: {id}"));

        var raw = ReadRawSurface(rawPath);
        foreach (var symbol in manifest.Symbols)
        {
            if (symbol.Status == "UNSUPPORTED_NEEDS_WORK")
                errors.Add($"unsupported NDK symbol: {symbol.Id} ({symbol.Notes})");
            if (symbol.Status is "NOVUS_EQUIVALENT" or "NOT_APPLICABLE_C_ONLY" && string.IsNullOrWhiteSpace(symbol.Notes))
                errors.Add($"classified symbol has no reason: {symbol.Id}");
            if (symbol.Status != "DIRECTLY_SUPPORTED")
                continue;

            var present = symbol.Category switch
            {
                "function" => raw.Functions.TryGetValue(symbol.NovusModule, out var functions) && functions.Contains(symbol.Name),
                "type" => raw.Types.Contains(symbol.Name),
                "constant" => raw.Constants.Contains(symbol.Name),
                _ => true
            };
            if (!present)
                errors.Add($"missing direct binding: {symbol.Id} -> {symbol.NovusModule}");
        }

        foreach (var (module, duplicates) in raw.DuplicateFunctions)
            errors.AddRange(duplicates.Select(name => $"duplicate raw binding: {module}::{name}"));
        errors.AddRange(raw.DuplicateTypes.Select(name => $"duplicate raw type binding: {name}"));
        errors.AddRange(raw.DuplicateConstants.Select(name => $"duplicate raw constant binding: {name}"));

        var baselineFunctions = manifest.Symbols.Where(symbol => symbol.Category == "function" && symbol.Status == "DIRECTLY_SUPPORTED")
            .GroupBy(symbol => symbol.NovusModule, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(symbol => symbol.Name).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var (module, functions) in raw.Functions)
        {
            if (!baselineFunctions.TryGetValue(module, out var expected))
            {
                if (functions.Count > 0 && !manifest.Extensions.Any(extension => extension.Module == module))
                    errors.Add($"raw function module is outside pinned baseline and is not a classified extension: {module}");
                continue;
            }
            foreach (var extra in functions.Where(name => !expected.Contains(name) && !name.StartsWith("__novus_", StringComparison.Ordinal)))
                if (!manifest.ExtensionSymbols.Any(extension => extension.Category == "function" &&
                    extension.Module == module && extension.Name == extra && !string.IsNullOrWhiteSpace(extension.Notes)))
                    errors.Add($"raw binding is outside pinned baseline: {module}::{extra}");
        }

        VerifyExtraRawSymbols("type", raw.TypesByModule, manifest, errors);
        VerifyExtraRawSymbols("constant", raw.ConstantsByModule, manifest, errors);
        var macroDiagnostics = Path.Combine(rawPath, "ndk_unsupported_macros.txt");
        if (File.Exists(macroDiagnostics))
        {
            var classifiedMacros = manifest.Symbols.Where(symbol => symbol.Category == "macro")
                .Select(symbol => symbol.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var line in File.ReadLines(macroDiagnostics).Where(line => line.Length > 0 && !line.StartsWith('#')))
            {
                var name = line.Split('=', 2)[0].Trim();
                if (!classifiedMacros.Contains(name))
                    errors.Add($"generator-skipped macro is not classified in the coverage manifest: {name}");
            }
        }

        if (!string.IsNullOrWhiteSpace(ndkPath))
        {
            var regenerated = Generate(ndkPath, rawPath);
            if (regenerated.Baseline.InputSha256 != manifest.Baseline.InputSha256)
                errors.Add($"NDK input hash changed: expected {manifest.Baseline.InputSha256}, got {regenerated.Baseline.InputSha256}");
            if (regenerated.Baseline.InventorySha256 != manifest.Baseline.InventorySha256)
                errors.Add($"authoritative NDK inventory changed: expected {manifest.Baseline.InventorySha256}, got {regenerated.Baseline.InventorySha256}");
        }

        return errors;
    }

    private static void AddSfdFunctions(string ndkPath, RawSurface raw, NdkCoverageManifest manifest)
    {
        var sfdRoot = Path.Combine(ndkPath, "Include", "sfd");
        foreach (var path in Directory.EnumerateFiles(sfdRoot, "*_lib.sfd").Order(StringComparer.Ordinal))
        {
            var library = SfdParser.ParseFile(path);
            var module = ModuleForLibrary(library.LibraryName);
            var scope = ScopeFor(module, library.Includes.Select(NormalizeInclude));
            var category = CategoryForInterface(library.LibraryName);
            var relative = RelativeToNdk(ndkPath, path);
            manifest.Interfaces.Add(new NdkInterface
            {
                Category = category,
                Name = library.LibraryName,
                Scope = scope,
                Source = relative,
                NovusModule = module,
                MinimumVersion = library.Functions.Where(f => f.Version > 0).Select(f => f.Version).DefaultIfEmpty(0).Min()
            });
            foreach (var function in library.Functions.Where(function => !function.IsReserved))
            {
                manifest.Symbols.Add(new NdkSymbol
                {
                    Category = "function",
                    Name = function.Name,
                    Scope = scope,
                    Interface = library.LibraryName,
                    Sources = [relative],
                    MinimumVersion = function.Version,
                    NovusModule = module,
                    Status = HasFunction(raw, module, function.Name) ? "DIRECTLY_SUPPORTED" : "UNSUPPORTED_NEEDS_WORK",
                    Definition = $"{function.ReturnType}({string.Join(',', function.Parameters.Select(p => p.Type))})@{string.Join(',', function.Registers)}:{function.Offset}",
                    Notes = HasFunction(raw, module, function.Name) ? "" : "No extern pub fn with this SFD name."
                });

                foreach (var typeName in AggregateReferences(function.ReturnType)
                    .Concat(function.Parameters.SelectMany(parameter => AggregateReferences(parameter.Type))))
                    AddOpaqueType(manifest, raw, typeName, scope, library.LibraryName, relative);
            }
        }
    }

    private static void AddFdOnlyFunctions(string ndkPath, RawSurface raw, NdkCoverageManifest manifest)
    {
        var fdRoot = Path.Combine(ndkPath, "Include", "fd");
        foreach (var path in Directory.EnumerateFiles(fdRoot, "*.fd").Order(StringComparer.Ordinal))
        {
            // NDK 3.9 ships SFDs for every public FD interface except HDWrench.
            // Several FD names do not match their SFD names (for example
            // cardres.fd/card_resource_lib.sfd), so filename subtraction is unsafe.
            if (!Path.GetFileName(path).Equals("hdwrench.fd", StringComparison.OrdinalIgnoreCase)) continue;
            var fd = ParseFd(path);
            if (fd.Functions.Count == 0)
                continue;

            var libraryName = Path.GetFileName(path).Equals("hdwrench.fd", StringComparison.OrdinalIgnoreCase)
                ? "hdwrench.library"
                : Path.GetFileNameWithoutExtension(path).Replace("_lib", ".library", StringComparison.Ordinal);
            var module = ModuleForLibrary(libraryName);
            var relative = RelativeToNdk(ndkPath, path);
            manifest.Interfaces.Add(new NdkInterface
            {
                Category = CategoryForInterface(libraryName), Name = libraryName, Scope = "CORE_NDK",
                Source = relative, NovusModule = module, MinimumVersion = libraryName == "hdwrench.library" ? 44 : 0,
                Notes = "Public FD-only interface; NDK 3.9 supplies no matching SFD."
            });
            foreach (var function in fd.Functions)
            {
                manifest.Symbols.Add(new NdkSymbol
                {
                    Category = "function", Name = function.Name, Scope = "CORE_NDK", Interface = libraryName,
                    Sources = [relative], MinimumVersion = libraryName == "hdwrench.library" ? 44 : 0,
                    NovusModule = module,
                    Status = HasFunction(raw, module, function.Name) ? "DIRECTLY_SUPPORTED" : "UNSUPPORTED_NEEDS_WORK",
                    Definition = $"fd({string.Join(',', function.Parameters)})@{string.Join(',', function.Registers)}:{function.Offset}",
                    Notes = HasFunction(raw, module, function.Name) ? "FD-only binding." : "Public FD entry has no raw binding."
                });
            }
        }
    }

    private static void AddAmigaLibFunctions(string includeRoot, RawSurface raw, NdkCoverageManifest manifest)
    {
        const string module = "amiga::raw::amiga_lib";
        var path = Path.Combine(includeRoot, "clib", "alib_protos.h");
        var relative = "Include/include_h/clib/alib_protos.h";
        manifest.Interfaces.Add(new NdkInterface
        {
            Category = "static_library", Name = "amiga.lib", Scope = "CORE_NDK", Source = relative,
            NovusModule = module, Notes = "NDK static support library; linked only when referenced."
        });
        foreach (var function in ParseCPrototypes(path))
        {
            manifest.Symbols.Add(new NdkSymbol
            {
                Category = "function", Name = function.Name, Scope = "CORE_NDK", Interface = "amiga.lib",
                Sources = [relative], NovusModule = module,
                Status = HasFunction(raw, module, function.Name) ? "DIRECTLY_SUPPORTED" : "UNSUPPORTED_NEEDS_WORK",
                Definition = function.Definition,
                Notes = HasFunction(raw, module, function.Name) ? "Static support-library call." : "Public amiga.lib prototype has no raw binding."
            });
        }
    }

    private static void AddHeaderSurface(string includeRoot, RawSurface raw, NdkCoverageManifest manifest)
    {
        foreach (var path in PublicHeaders(includeRoot))
        {
            var relative = Path.GetRelativePath(includeRoot, path).Replace('\\', '/');
            var source = "Include/include_h/" + relative;
            var scope = ScopeFor("", [relative]);
            var parsed = CHeaderParser.ParseFile(path);
            foreach (var type in parsed.Structs.Where(type => !string.IsNullOrWhiteSpace(type.Name)))
            {
                manifest.Symbols.Add(new NdkSymbol
                {
                    Category = "type", Name = type.Name, Scope = scope, Interface = HeaderInterface(relative),
                    Sources = [source], NovusModule = "amiga::raw::structs",
                    Status = raw.Types.Contains(type.Name) ? "DIRECTLY_SUPPORTED" : "UNSUPPORTED_NEEDS_WORK",
                    Definition = $"{(type.IsUnion ? "union" : "struct")}({string.Join(';', type.Fields.Select(field => $"{field.Type} {field.Name}{(field.IsArray ? $"[{field.ArraySize}]" : "")}"))})",
                    Notes = raw.Types.Contains(type.Name) ? "" : "Public aggregate is absent from amiga::raw::structs."
                });
            }

            var definedTypes = parsed.Structs.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var typeName in AggregateReferences(StripPreprocessor(StripComments(File.ReadAllText(path))))
                .Where(name => !definedTypes.Contains(name)))
                AddOpaqueType(manifest, raw, typeName, scope, HeaderInterface(relative), source);

            foreach (var typedef in ParseTypedefs(path))
            {
                var direct = raw.Types.Contains(typedef.Name);
                manifest.Symbols.Add(new NdkSymbol
                {
                    Category = "type", Name = typedef.Name, Scope = scope, Interface = HeaderInterface(relative),
                    Sources = [source], NovusModule = direct ? "amiga::raw::types" : "native Novus type system",
                    Status = direct ? "DIRECTLY_SUPPORTED" : "NOVUS_EQUIVALENT",
                    Definition = typedef.Definition,
                    Notes = direct ? "" : $"NDK typedef maps without ABI change to {MapTypedef(typedef.Definition)}."
                });
            }

            var defines = ParseDefines(path);
            foreach (var constant in parsed.Constants)
            {
                var direct = raw.Constants.Contains(constant.Name);
                if (direct)
                {
                    manifest.Symbols.Add(new NdkSymbol
                    {
                        Category = "constant", Name = constant.Name, Scope = scope, Interface = HeaderInterface(relative),
                        Sources = [source], NovusModule = "amiga::raw::consts", Status = "DIRECTLY_SUPPORTED",
                        Definition = constant.Value
                    });
                }
                else
                {
                    manifest.Symbols.Add(ClassifyMacro(constant.Name, constant.Value, false, scope, relative, source));
                }
            }
            foreach (var define in defines.Where(define => define.IsFunctionLike))
                manifest.Symbols.Add(ClassifyMacro(define.Name, define.Body, true, scope, relative, source));
        }
    }

    private static void AddDeviceAndResourceInterfaces(string includeRoot, NdkCoverageManifest manifest)
    {
        foreach (var category in new[] { "devices", "resources" })
        foreach (var path in Directory.EnumerateFiles(Path.Combine(includeRoot, category), "*.h").Order(StringComparer.Ordinal))
        {
            var file = Path.GetFileNameWithoutExtension(path);
            var name = category == "devices" ? $"devices/{file}.h" : $"resources/{file}.h";
            manifest.Interfaces.Add(new NdkInterface
            {
                Category = category == "devices" ? "device" : "resource",
                Name = name,
                Scope = "CORE_NDK",
                Source = $"Include/include_h/{category}/{Path.GetFileName(path)}",
                NovusModule = "amiga::raw::structs + amiga::raw::consts",
                Notes = "IO/resource contract is represented by its public aggregates and constants; operations use exec.library unless an SFD interface exists."
            });
        }
    }

    private static NdkSymbol ClassifyMacro(string name, string body, bool functionLike, string scope, string relative, string source)
    {
        var cOnly = COnlyMacros.Contains(name);
        return new NdkSymbol
        {
            Category = "macro", Name = name, Scope = scope, Interface = HeaderInterface(relative), Sources = [source],
            NovusModule = cOnly ? "not applicable" : "native Novus expression/API",
            Status = cOnly ? "NOT_APPLICABLE_C_ONLY" : "NOVUS_EQUIVALENT",
            Definition = body,
            Notes = cOnly
                ? "C declaration/preprocessor syntax has no runtime API meaning in Novus."
                : functionLike
                    ? "C textual convenience; Novus expresses the same field access, calculation, call, or tag construction directly."
                    : "C alias/initializer/sizeof convenience; use the underlying raw field, constant, call, or @sizeof directly."
        };
    }

    private static NdkSymbol MergeSymbols(IGrouping<string, NdkSymbol> group)
    {
        var values = group.ToList();
        var result = values.OrderByDescending(value => value.Status == "DIRECTLY_SUPPORTED").First();
        result.Sources = values.SelectMany(value => value.Sources).Distinct(StringComparer.Ordinal).Order().ToList();
        if (values.Select(value => value.Definition).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).Count() > 1 &&
            result.Category == "function")
        {
            result.Status = "UNSUPPORTED_NEEDS_WORK";
            result.Notes = "Conflicting authoritative function definitions.";
        }
        return result;
    }

    private static IEnumerable<string> PublicHeaders(string includeRoot) =>
        Directory.EnumerateFiles(includeRoot, "*.h", SearchOption.AllDirectories)
            .Where(path => !ExcludedHeaderDirectories.Contains(Path.GetRelativePath(includeRoot, path).Split(Path.DirectorySeparatorChar)[0]))
            .Order(StringComparer.Ordinal);

    private static RawSurface ReadRawSurface(string rawPath)
    {
        var surface = new RawSurface();
        foreach (var path in Directory.EnumerateFiles(rawPath, "*.novus", SearchOption.AllDirectories))
        {
            var module = "amiga::raw::" + Path.ChangeExtension(Path.GetRelativePath(rawPath, path), null)!
                .Replace(Path.DirectorySeparatorChar.ToString(), "::", StringComparison.Ordinal);
            var text = File.ReadAllText(path);
            var names = PublicFunction.Matches(text).Select(match => match.Groups[1].Value).ToList();
            surface.Functions[module] = names.ToHashSet(StringComparer.Ordinal);
            var duplicates = names.GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToList();
            if (duplicates.Count > 0) surface.DuplicateFunctions[module] = duplicates;
            foreach (Match match in PublicType.Matches(text))
            {
                surface.Types.Add(match.Groups[1].Value);
                AddSurfaceSymbol(surface.TypesByModule, module, match.Groups[1].Value);
            }
            foreach (Match match in PublicConstant.Matches(text))
            {
                surface.Constants.Add(match.Groups[1].Value);
                AddSurfaceSymbol(surface.ConstantsByModule, module, match.Groups[1].Value);
            }
        }
        surface.DuplicateTypes.AddRange(AllMatches(rawPath, PublicType).GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key));
        surface.DuplicateConstants.AddRange(AllMatches(rawPath, PublicConstant).GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key));
        return surface;
    }

    private static IEnumerable<string> AllMatches(string rawPath, Regex pattern) =>
        Directory.EnumerateFiles(rawPath, "*.novus", SearchOption.AllDirectories)
            .SelectMany(path => pattern.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value));

    private static bool HasFunction(RawSurface raw, string module, string name) =>
        raw.Functions.TryGetValue(module, out var functions) && functions.Contains(name);

    private static void AddSurfaceSymbol(Dictionary<string, HashSet<string>> symbols, string module, string name)
    {
        if (!symbols.TryGetValue(module, out var names)) symbols[module] = names = new HashSet<string>(StringComparer.Ordinal);
        names.Add(name);
    }

    private static void VerifyExtraRawSymbols(string category, Dictionary<string, HashSet<string>> rawSymbols,
        NdkCoverageManifest manifest, List<string> errors)
    {
        var baseline = manifest.Symbols.Where(symbol => symbol.Category == category && symbol.Status == "DIRECTLY_SUPPORTED")
            .Select(symbol => symbol.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (module, names) in rawSymbols)
        foreach (var name in names.Where(name => !baseline.Contains(name)))
        {
            var moduleExtension = manifest.Extensions.Any(extension => extension.Module == module);
            var symbolExtension = manifest.ExtensionSymbols.Any(extension => extension.Category == category &&
                extension.Name == name && extension.Module == module && !string.IsNullOrWhiteSpace(extension.Notes));
            if (!moduleExtension && !symbolExtension)
                errors.Add($"raw {category} is outside pinned baseline and is not a classified extension: {module}::{name}");
        }
    }

    private static IEnumerable<string> AggregateReferences(string text) =>
        Regex.Matches(text, @"\b(?:struct|union)\s+([A-Za-z_][A-Za-z0-9_]*)")
            .Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal);

    private static void AddOpaqueType(NdkCoverageManifest manifest, RawSurface raw, string name, string scope,
        string interfaceName, string source)
    {
        manifest.Symbols.Add(new NdkSymbol
        {
            Category = "type", Name = name, Scope = scope, Interface = interfaceName, Sources = [source],
            NovusModule = "amiga::raw::structs",
            Status = raw.Types.Contains(name) ? "DIRECTLY_SUPPORTED" : "UNSUPPORTED_NEEDS_WORK",
            Definition = "opaque aggregate declaration",
            Notes = raw.Types.Contains(name) ? "Opaque or forward-declared NDK aggregate." : "Referenced NDK aggregate is absent from amiga::raw."
        });
    }

    private static string ModuleForLibrary(string libraryName)
    {
        var module = libraryName.Replace(".library", "", StringComparison.OrdinalIgnoreCase).Replace('.', '_');
        if (libraryName.EndsWith(".device", StringComparison.OrdinalIgnoreCase))
            return $"amiga::raw::devices::{module[..^7]}";
        if (libraryName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
            return $"amiga::raw::resources::{module[..^9]}";
        return $"amiga::raw::{module}";
    }

    private static string CategoryForInterface(string name) =>
        name.EndsWith(".device", StringComparison.OrdinalIgnoreCase) ? "device" :
        name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ? "resource" : "library";

    private static string ScopeFor(string module, IEnumerable<string> sources)
    {
        var reactionModule = new HashSet<string>(StringComparer.Ordinal)
        {
            "aml", "arexx", "bevel", "bitmap", "button", "checkbox", "chooser", "clicktab", "datebrowser",
            "drawlist", "dtclass", "fuelgauge", "getfile", "getfont", "getscreenmode", "glyph", "integer",
            "label", "layout", "listbrowser", "palette", "penmap", "popcycle", "radiobutton", "requester",
            "resource", "scroller", "slider", "space", "speedbar", "string", "texteditor", "virtual", "window"
        };
        var shortModule = module.Split("::").LastOrDefault() ?? "";
        return reactionModule.Contains(shortModule) || sources.Any(source =>
            source.StartsWith("reaction/", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("classes/", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("gadgets/", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
            ? "NDK_REACTION"
            : "CORE_NDK";
    }

    private static string NormalizeInclude(string value) => value.Trim('<', '>', '"').Replace('\\', '/');
    private static string HeaderInterface(string relative) =>
        relative.StartsWith("devices/", StringComparison.Ordinal) || relative.StartsWith("resources/", StringComparison.Ordinal)
            ? relative : "NDK headers";

    private static List<(string Name, string Definition)> ParseTypedefs(string path)
    {
        var text = StripComments(File.ReadAllText(path));
        return Regex.Matches(text, @"\btypedef\s+([^;{}]+?)\s*;", RegexOptions.Singleline)
            .Select(match => Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim())
            .Select(definition =>
            {
                var function = Regex.Match(definition, @"\(\s*\*\s*(?:__asm\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*\)");
                var ordinary = Regex.Match(definition, @"([A-Za-z_][A-Za-z0-9_]*)\s*$");
                return (Name: function.Success ? function.Groups[1].Value : ordinary.Groups[1].Value, Definition: definition);
            })
            .Where(value => !string.IsNullOrWhiteSpace(value.Name))
            .DistinctBy(value => value.Name)
            .ToList();
    }

    private static string MapTypedef(string definition)
    {
        if (definition.Contains("(*", StringComparison.Ordinal)) return "a Novus function pointer or opaque *u8 callback";
        var name = Regex.Match(definition, @"([A-Za-z_][A-Za-z0-9_]*)\s*$").Groups[1].Value;
        var underlying = definition[..Math.Max(0, definition.LastIndexOf(name, StringComparison.Ordinal))].Trim();
        return SfdParser.MapAmigaTypeToNovus(underlying);
    }

    private static List<(string Name, string Body, bool IsFunctionLike)> ParseDefines(string path)
    {
        var lines = new List<string>();
        var pending = "";
        foreach (var physical in File.ReadLines(path))
        {
            var line = physical.TrimEnd();
            if (line.EndsWith('\\')) { pending += line[..^1] + " "; continue; }
            lines.Add(pending + line);
            pending = "";
        }
        return lines.Select(line => Regex.Match(line,
                @"^\s*#define\s+([A-Za-z_][A-Za-z0-9_]*)(\([^)]*\))?\s*(.*)$"))
            .Where(match => match.Success && !match.Groups[1].Value.EndsWith("_H", StringComparison.Ordinal))
            .Select(match => (match.Groups[1].Value, match.Groups[3].Value.Trim(), match.Groups[2].Success))
            .ToList();
    }

    private static List<(string Name, string Definition)> ParseCPrototypes(string path)
    {
        var text = StripComments(File.ReadAllText(path));
        return Regex.Matches(text, @"(?:^|\n)\s*([^#{};]+?\([^;{}]*\))\s*;", RegexOptions.Singleline)
            .Select(match => Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim())
            .Select(definition => (Definition: definition, Match: Regex.Match(definition, @"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(")))
            .Where(value => value.Match.Success && value.Match.Groups[1].Value is not ("if" or "while" or "for"))
            .Select(value => (value.Match.Groups[1].Value, value.Definition))
            .DistinctBy(value => value.Item1)
            .ToList();
    }

    private static FdFile ParseFd(string path)
    {
        var result = new FdFile();
        var offset = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim().TrimEnd(';');
            if (line.StartsWith("##bias ", StringComparison.Ordinal) && int.TryParse(line[7..], out var bias)) { offset = bias; continue; }
            if (line.StartsWith("##")) continue;
            var match = Regex.Match(line, @"^([A-Za-z_][A-Za-z0-9_]*)\((.*?)\)\((.*?)\)$");
            if (!match.Success) continue;
            result.Functions.Add(new FdFunction(match.Groups[1].Value,
                match.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                match.Groups[3].Value.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), offset));
            offset += 6;
        }
        return result;
    }

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*", " ");
    }

    private static string StripPreprocessor(string text)
    {
        var result = new List<string>();
        var skipContinuation = false;
        foreach (var line in text.Split('\n'))
        {
            if (skipContinuation)
            {
                skipContinuation = line.TrimEnd().EndsWith('\\');
                continue;
            }
            if (line.TrimStart().StartsWith('#'))
            {
                skipContinuation = line.TrimEnd().EndsWith('\\');
                continue;
            }
            result.Add(line);
        }
        return string.Join('\n', result);
    }

    private static string HashAuthoritativeInputs(string ndkPath)
    {
        var inputs = new[] { Path.Combine(ndkPath, "README") }
            .Concat(Directory.EnumerateFiles(Path.Combine(ndkPath, "Include", "sfd"), "*.sfd"))
            .Concat(Directory.EnumerateFiles(Path.Combine(ndkPath, "Include", "fd"), "*.fd"))
            .Concat(Directory.EnumerateFiles(Path.Combine(ndkPath, "Include", "include_h"), "*.h", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in inputs)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(RelativeToNdk(ndkPath, path) + "\0"));
            hash.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string HashInventory(IEnumerable<NdkSymbol> symbols)
    {
        var text = string.Join('\n', symbols.Select(symbol =>
            $"{symbol.Id}|{symbol.MinimumVersion}|{symbol.Definition}|{string.Join(',', symbol.Sources)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static string RelativeToNdk(string ndkPath, string path) =>
        Path.GetRelativePath(ndkPath, path).Replace('\\', '/');

    private static void ValidateNdk39(string ndkPath)
    {
        var readme = Path.Combine(ndkPath, "README");
        if (!File.Exists(readme) || !File.ReadAllText(readme).Contains("Native Developer Kit for AmigaOS 3.9", StringComparison.Ordinal))
            throw new InvalidDataException($"{ndkPath} is not the pinned NDK 3.9 layout");
        foreach (var relative in new[] { "Include/sfd", "Include/fd", "Include/include_h" })
            if (!Directory.Exists(Path.Combine(ndkPath, relative.Replace('/', Path.DirectorySeparatorChar))))
                throw new InvalidDataException($"NDK 3.9 input is missing {relative}");
    }

    private sealed class RawSurface
    {
        public Dictionary<string, HashSet<string>> Functions { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<string>> DuplicateFunctions { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Types { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Constants { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, HashSet<string>> TypesByModule { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, HashSet<string>> ConstantsByModule { get; } = new(StringComparer.Ordinal);
        public List<string> DuplicateTypes { get; } = [];
        public List<string> DuplicateConstants { get; } = [];
    }

    private sealed class FdFile { public List<FdFunction> Functions { get; } = []; }
    private sealed record FdFunction(string Name, string[] Parameters, string[] Registers, int Offset);
}
