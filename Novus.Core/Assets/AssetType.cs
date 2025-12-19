namespace Novus.Assets;

/// <summary>
/// Supported asset types for the @embed attribute.
/// The asset type determines how the file is processed and validated.
/// </summary>
public enum AssetType
{
    /// <summary>
    /// Raw binary data - no conversion, included as-is.
    /// Target types: RawData, [u8; N], *u8
    /// </summary>
    Raw,

    /// <summary>
    /// Audio sample - WAV, AIFF, or 8SVX converted to Paula-compatible signed 8-bit PCM.
    /// Target types: AudioSample, AudioAsset
    /// Supported formats: .wav, .aiff, .8svx
    /// </summary>
    Audio,

    /// <summary>
    /// ProTracker module - MOD, NST, or compatible tracker format.
    /// Target types: ModTracker, ModAsset
    /// Supported formats: .mod, .nst, .m15, .s3m, .xm
    /// </summary>
    Mod,

    /// <summary>
    /// IFF ILBM bitmap - converted to planar format.
    /// Target types: Bitmap, PlanarBitmap
    /// Supported formats: .iff, .lbm
    /// </summary>
    Bitmap,

    /// <summary>
    /// Hardware sprite data - must be 16px wide, converted to interleaved format.
    /// Target types: SpriteData
    /// Supported formats: .iff, .lbm, .raw
    /// </summary>
    Sprite,

    /// <summary>
    /// Color palette - extracted from IFF or specified directly.
    /// Target types: Palette, [u16; N]
    /// Supported formats: .iff, .pal
    /// </summary>
    Palette,

    /// <summary>
    /// Blitter object data - arbitrary size with auto-generated mask.
    /// Target types: BobData
    /// Supported formats: .iff, .lbm
    /// </summary>
    Bob,

    /// <summary>
    /// Copper list data - raw copper instructions.
    /// Target types: CopperList, [u32; N]
    /// Supported formats: .cop, .raw
    /// </summary>
    Copper,

    /// <summary>
    /// Font data - Amiga bitmap font.
    /// Target types: BitmapFont
    /// Supported formats: .font, .raw
    /// </summary>
    Font
}

/// <summary>
/// Maps file extensions to default asset types.
/// </summary>
public static class AssetTypeDetector
{
    private static readonly Dictionary<string, AssetType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Audio formats
        { ".wav", AssetType.Audio },
        { ".wave", AssetType.Audio },
        { ".aiff", AssetType.Audio },
        { ".aif", AssetType.Audio },
        { ".8svx", AssetType.Audio },

        // Tracker formats
        { ".mod", AssetType.Mod },
        { ".nst", AssetType.Mod },
        { ".m15", AssetType.Mod },
        { ".s3m", AssetType.Mod },
        { ".xm", AssetType.Mod },

        // Graphics formats
        { ".iff", AssetType.Bitmap },
        { ".ilbm", AssetType.Bitmap },
        { ".lbm", AssetType.Bitmap },

        // Palette formats
        { ".pal", AssetType.Palette },

        // Copper list format
        { ".cop", AssetType.Copper },

        // Font format
        { ".font", AssetType.Font },

        // Raw/binary (fallback)
        { ".raw", AssetType.Raw },
        { ".bin", AssetType.Raw },
        { ".dat", AssetType.Raw }
    };

    /// <summary>
    /// Detect asset type from file extension.
    /// </summary>
    public static AssetType? DetectFromExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ExtensionMap.TryGetValue(ext, out var type) ? type : null;
    }

    /// <summary>
    /// Get the default asset type for a file, or Raw if unknown.
    /// </summary>
    public static AssetType GetDefaultType(string filePath)
    {
        return DetectFromExtension(filePath) ?? AssetType.Raw;
    }

    /// <summary>
    /// Check if a file extension is recognized.
    /// </summary>
    public static bool IsRecognizedExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ExtensionMap.ContainsKey(ext);
    }

    /// <summary>
    /// Get supported extensions for an asset type.
    /// </summary>
    public static IEnumerable<string> GetExtensionsForType(AssetType type)
    {
        return ExtensionMap.Where(kvp => kvp.Value == type).Select(kvp => kvp.Key);
    }
}
