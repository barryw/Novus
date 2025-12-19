namespace Novus.Assets;

/// <summary>
/// Options for the @embed attribute, parsed from attribute arguments.
/// </summary>
public class EmbedOptions
{
    /// <summary>
    /// Path to the file to embed (required).
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// Explicit asset type override. If null, detected from file extension.
    /// </summary>
    public AssetType? ExplicitType { get; set; }

    /// <summary>
    /// Whether to place data in CHIP RAM (for DMA access).
    /// Defaults to true for audio/graphics assets that require DMA.
    /// </summary>
    public bool ChipRam { get; set; } = true;

    /// <summary>
    /// Whether the chip parameter was explicitly specified in the attribute.
    /// Used to determine whether to use smart defaults based on asset type.
    /// </summary>
    public bool ChipRamExplicit { get; set; }

    // Audio-specific options
    /// <summary>
    /// Target sample rate for audio conversion (Hz).
    /// </summary>
    public int? SampleRate { get; set; }

    /// <summary>
    /// Normalize audio to maximum amplitude.
    /// </summary>
    public bool Normalize { get; set; }

    /// <summary>
    /// Trim silence from start/end of audio.
    /// </summary>
    public bool TrimSilence { get; set; }

    /// <summary>
    /// Channel mode for stereo audio: "left", "right", or "mix".
    /// </summary>
    public string ChannelMode { get; set; } = "mix";

    // Graphics-specific options
    /// <summary>
    /// Width of the image/sprite (for raw data).
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the image/sprite (for raw data).
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Bit depth (number of bitplanes, 1-8).
    /// </summary>
    public int? Depth { get; set; }

    /// <summary>
    /// Sprite height (for sprite assets).
    /// </summary>
    public int? SpriteHeight { get; set; }

    // Raw data options
    /// <summary>
    /// Bits per sample for raw audio (8 or 16).
    /// </summary>
    public int? Bits { get; set; }

    /// <summary>
    /// Number of channels for raw audio (1 = mono, 2 = stereo).
    /// </summary>
    public int? Channels { get; set; }

    /// <summary>
    /// Parse embed options from attribute arguments.
    /// </summary>
    public static EmbedOptions FromAttribute(SemanticAnalysis.AttributeInfo attr)
    {
        var options = new EmbedOptions();

        // First positional argument is always the file path
        if (attr.PositionalArgs.Count > 0)
        {
            options.FilePath = attr.PositionalArgs[0]?.ToString() ?? "";
        }

        // Parse named arguments
        if (attr.GetString("type") is string typeStr)
        {
            options.ExplicitType = ParseAssetType(typeStr);
        }

        // Memory placement
        if (attr.GetBool("chip") is bool chip)
        {
            options.ChipRam = chip;
            options.ChipRamExplicit = true;
        }

        // Audio options
        if (attr.GetInt("sample_rate") is int sampleRate)
        {
            options.SampleRate = sampleRate;
        }

        if (attr.GetBool("normalize") is bool normalize)
        {
            options.Normalize = normalize;
        }

        if (attr.GetBool("trim_silence") is bool trimSilence)
        {
            options.TrimSilence = trimSilence;
        }

        if (attr.GetString("channel") is string channel)
        {
            options.ChannelMode = channel;
        }

        // Graphics options
        if (attr.GetInt("width") is int width)
        {
            options.Width = width;
        }

        if (attr.GetInt("height") is int height)
        {
            options.Height = height;
        }

        if (attr.GetInt("depth") is int depth)
        {
            options.Depth = depth;
        }

        if (attr.GetInt("sprite_height") is int spriteHeight)
        {
            options.SpriteHeight = spriteHeight;
        }

        // Raw data options
        if (attr.GetInt("bits") is int bits)
        {
            options.Bits = bits;
        }

        if (attr.GetInt("channels") is int channels)
        {
            options.Channels = channels;
        }

        return options;
    }

    private static AssetType? ParseAssetType(string typeStr)
    {
        return typeStr.ToLowerInvariant() switch
        {
            "raw" => AssetType.Raw,
            "audio" => AssetType.Audio,
            "mod" => AssetType.Mod,
            "bitmap" => AssetType.Bitmap,
            "sprite" => AssetType.Sprite,
            "palette" => AssetType.Palette,
            "bob" => AssetType.Bob,
            "copper" => AssetType.Copper,
            "font" => AssetType.Font,
            _ => null
        };
    }

    /// <summary>
    /// Determine the effective asset type (explicit or detected from extension).
    /// </summary>
    public AssetType GetEffectiveType()
    {
        return ExplicitType ?? AssetTypeDetector.GetDefaultType(FilePath);
    }

    /// <summary>
    /// Check if this asset type requires CHIP RAM by default.
    /// </summary>
    public bool RequiresChipRamByDefault()
    {
        var type = GetEffectiveType();
        return type switch
        {
            AssetType.Audio => true,   // Paula DMA
            AssetType.Mod => true,     // Paula DMA for samples
            AssetType.Sprite => true,  // Sprite DMA
            AssetType.Bob => true,     // Blitter DMA
            AssetType.Bitmap => true,  // Display/Blitter DMA
            AssetType.Copper => true,  // Copper DMA
            AssetType.Palette => false, // CPU access only
            AssetType.Font => false,   // CPU access only
            AssetType.Raw => false,    // Unknown usage
            _ => false
        };
    }
}
