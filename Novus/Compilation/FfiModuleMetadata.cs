namespace Novus.Compilation;

public enum FfiModuleKind
{
    Library,
    Device,
    Resource,
    CallerSupplied
}

/// <summary>Machine-readable metadata carried by generated std::ffi modules.</summary>
public sealed record FfiModuleMetadata(
    string ModulePath,
    string ModuleName,
    string LibraryName,
    string OpenName,
    string BaseSymbol,
    FfiModuleKind Kind,
    int MinimumVersion)
{
    public IReadOnlyList<string> Headers { get; init; } = [];

    private static readonly Dictionary<string, string> ClassOpenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bevel"] = "images/bevel.image",
        ["bitmap"] = "images/bitmap.image",
        ["button"] = "gadgets/button.gadget",
        ["checkbox"] = "gadgets/checkbox.gadget",
        ["chooser"] = "gadgets/chooser.gadget",
        ["clicktab"] = "gadgets/clicktab.gadget",
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
        ["radiobutton"] = "gadgets/radiobutton.gadget",
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

        foreach (var line in File.ReadLines(modulePath).Take(12))
        {
            if (line.StartsWith("// Library:", StringComparison.Ordinal))
                libraryName = line[11..].Trim();
            else if (line.StartsWith("// Base:", StringComparison.Ordinal))
                baseSymbol = line[8..].Trim();
        }

        if (string.IsNullOrWhiteSpace(libraryName) || string.IsNullOrWhiteSpace(baseSymbol))
            return null;

        var moduleName = Path.GetFileNameWithoutExtension(modulePath);
        var kind = baseSymbol.Equals("caller-supplied", StringComparison.OrdinalIgnoreCase)
            ? FfiModuleKind.CallerSupplied
            : libraryName.EndsWith(".device", StringComparison.OrdinalIgnoreCase)
            ? FfiModuleKind.Device
            : libraryName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)
                ? FfiModuleKind.Resource
                : FfiModuleKind.Library;
        var isClass = ClassOpenNames.TryGetValue(moduleName, out var classOpenName);

        var headers = new List<string>();
        var headerMapPath = Path.Combine(Path.GetDirectoryName(modulePath)!, "ndk_headers.txt");
        if (File.Exists(headerMapPath))
        {
            var prefix = moduleName + "|";
            var entry = File.ReadLines(headerMapPath)
                .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));
            if (entry != null)
                headers.AddRange(entry[prefix.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries));
        }

        return new FfiModuleMetadata(
            modulePath,
            moduleName,
            libraryName,
            isClass ? classOpenName! : libraryName,
            baseSymbol,
            kind,
            isClass ? 44 : 0)
        {
            Headers = headers
        };
    }
}
