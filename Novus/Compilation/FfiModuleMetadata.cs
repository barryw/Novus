namespace Novus.Compilation;

public enum FfiModuleKind
{
    Library,
    LazyLibrary,
    Device,
    Resource,
    CallerSupplied
}

/// <summary>Machine-readable metadata carried by generated amiga::raw modules.</summary>
public sealed record FfiModuleMetadata(
    string ModulePath,
    string ModuleName,
    string LibraryName,
    string OpenName,
    string BaseSymbol,
    FfiModuleKind Kind,
    int MinimumVersion)
{
    public bool Optional { get; init; }
    public int DeviceUnit { get; init; }
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyDictionary<string, int> FunctionVersions { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> ClassOpenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["arexx"] = "arexx.class",
        ["bevel"] = "images/bevel.image",
        ["bitmap"] = "images/bitmap.image",
        ["button"] = "gadgets/button.gadget",
        ["checkbox"] = "gadgets/checkbox.gadget",
        ["chooser"] = "gadgets/chooser.gadget",
        ["clicktab"] = "gadgets/clicktab.gadget",
        ["colorwheel"] = "gadgets/colorwheel.gadget",
        ["datebrowser"] = "gadgets/datebrowser.gadget",
        ["drawlist"] = "images/drawlist.image",
        ["fuelgauge"] = "gadgets/fuelgauge.gadget",
        ["getfile"] = "gadgets/getfile.gadget",
        ["getfont"] = "gadgets/getfont.gadget",
        ["getscreenmode"] = "gadgets/getscreenmode.gadget",
        ["glyph"] = "images/glyph.image",
        ["integer"] = "gadgets/integer.gadget",
        ["label"] = "images/label.image",
        ["layout"] = "gadgets/layout.gadget",
        ["listbrowser"] = "gadgets/listbrowser.gadget",
        ["palette"] = "gadgets/palette.gadget",
        ["penmap"] = "images/penmap.image",
        ["popcycle"] = "gadgets/popcycle.gadget",
        ["radiobutton"] = "gadgets/radiobutton.gadget",
        ["requester"] = "requester.class",
        ["scroller"] = "gadgets/scroller.gadget",
        ["slider"] = "gadgets/slider.gadget",
        ["space"] = "gadgets/space.gadget",
        ["speedbar"] = "gadgets/speedbar.gadget",
        ["string"] = "gadgets/string.gadget",
        ["texteditor"] = "gadgets/texteditor.gadget",
        ["virtual"] = "gadgets/virtual.gadget",
        ["window"] = "window.class"
    };

    public static FfiModuleMetadata? TryRead(string modulePath)
    {
        string? libraryName = null;
        string? baseSymbol = null;
        var deviceUnit = 0;
        var lazy = false;
        var optional = false;

        foreach (var line in File.ReadLines(modulePath).Take(12))
        {
            if (line.StartsWith("// Library:", StringComparison.Ordinal))
                libraryName = line[11..].Trim();
            else if (line.StartsWith("// Base:", StringComparison.Ordinal))
                baseSymbol = line[8..].Trim();
            else if (line.StartsWith("// Unit:", StringComparison.Ordinal) &&
                     int.TryParse(line[8..].Trim(), out var parsedUnit))
                deviceUnit = parsedUnit;
            else if (line.Equals("// Lifecycle: lazy", StringComparison.OrdinalIgnoreCase))
                lazy = true;
            // An optional interface is absent from some supported systems. Startup records a
            // null base instead of aborting, so callers can probe for it and degrade.
            else if (line.Equals("// Lifecycle: optional", StringComparison.OrdinalIgnoreCase))
                optional = true;
        }

        if (string.IsNullOrWhiteSpace(libraryName) || string.IsNullOrWhiteSpace(baseSymbol))
            return null;

        var physicalName = Path.GetFileNameWithoutExtension(modulePath);
        var moduleName = libraryName.Replace(".library", "", StringComparison.OrdinalIgnoreCase)
            .Replace('.', '_');
        var kind = baseSymbol.Equals("caller-supplied", StringComparison.OrdinalIgnoreCase)
            ? FfiModuleKind.CallerSupplied
            : lazy
            ? FfiModuleKind.LazyLibrary
            : libraryName.EndsWith(".device", StringComparison.OrdinalIgnoreCase)
            ? FfiModuleKind.Device
            : libraryName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)
                ? FfiModuleKind.Resource
                : FfiModuleKind.Library;
        var isClass = ClassOpenNames.TryGetValue(physicalName, out var classOpenName);

        var headers = new List<string>();
        var metadataDirectory = Path.GetDirectoryName(modulePath)!;
        var headerMapPath = Path.Combine(metadataDirectory, "ndk_headers.txt");
        if (!File.Exists(headerMapPath))
            headerMapPath = Path.Combine(Path.GetDirectoryName(metadataDirectory)!, "ndk_headers.txt");
        if (File.Exists(headerMapPath))
        {
            var prefix = moduleName + "|";
            var entry = File.ReadLines(headerMapPath)
                .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));
            if (entry != null)
                headers.AddRange(entry[prefix.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries));
        }

        var functionVersions = new Dictionary<string, int>(StringComparer.Ordinal);
        var versionMapPath = Path.Combine(metadataDirectory, "ndk_versions.txt");
        if (!File.Exists(versionMapPath))
            versionMapPath = Path.Combine(Path.GetDirectoryName(metadataDirectory)!, "ndk_versions.txt");
        if (File.Exists(versionMapPath))
        {
            var prefix = moduleName + "|";
            foreach (var line in File.ReadLines(versionMapPath).Where(line => line.StartsWith(prefix, StringComparison.Ordinal)))
            {
                var parts = line.Split('|');
                if (parts.Length == 3 && int.TryParse(parts[2], out var version))
                    functionVersions[parts[1]] = version;
            }
        }

        return new FfiModuleMetadata(
            modulePath,
            moduleName,
            libraryName,
            isClass ? classOpenName! : libraryName,
            baseSymbol,
            kind,
            Math.Max(isClass ? 44 : 0, functionVersions.Values.DefaultIfEmpty(0).Min()))
        {
            DeviceUnit = deviceUnit,
            Headers = headers,
            FunctionVersions = functionVersions,
            Optional = optional
        };
    }

    public static IEnumerable<FfiModuleMetadata> ReadMappedFunctionDependencies(
        string rawDirectory, IReadOnlySet<string> reachableFunctions)
    {
        var mapPath = Path.Combine(rawDirectory, "ndk_dependencies.txt");
        if (!File.Exists(mapPath)) yield break;

        foreach (var parts in File.ReadLines(mapPath)
                     .Select(line => line.Split('|', StringSplitOptions.TrimEntries)))
        {
            if (parts.Length != 3 || !reachableFunctions.Contains(parts[1])) continue;
            var dependency = TryRead(Path.Combine(rawDirectory, parts[2] + ".novus"));
            if (dependency != null) yield return dependency;
        }
    }
}
