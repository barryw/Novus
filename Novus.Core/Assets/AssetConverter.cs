namespace Novus.Assets;

/// <summary>
/// Result of asset conversion.
/// </summary>
public class AssetConversionResult
{
    /// <summary>
    /// Converted binary data.
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Metadata about the converted asset (type-specific).
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Whether the data should be placed in CHIP RAM.
    /// </summary>
    public bool RequiresChipRam { get; set; }

    /// <summary>
    /// The Novus struct type name to use for this asset (e.g., "AudioSample", "ModAsset").
    /// </summary>
    public string? StructTypeName { get; set; }

    /// <summary>
    /// Whether the conversion produced a simple byte array vs a struct with metadata.
    /// </summary>
    public bool IsRawData => StructTypeName == null;
}

/// <summary>
/// Interface for asset converters.
/// </summary>
public interface IAssetConverter
{
    /// <summary>
    /// Asset types this converter handles.
    /// </summary>
    AssetType[] SupportedTypes { get; }

    /// <summary>
    /// File extensions this converter can process.
    /// </summary>
    string[] SupportedExtensions { get; }

    /// <summary>
    /// Convert a file to embeddable binary data.
    /// </summary>
    AssetConversionResult Convert(string filePath, EmbedOptions options);

    /// <summary>
    /// Validate that the file can be converted with the given options.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    string? Validate(string filePath, EmbedOptions options);
}

/// <summary>
/// Registry of asset converters.
/// </summary>
public static class AssetConverterRegistry
{
    private static readonly List<IAssetConverter> _converters = new();

    static AssetConverterRegistry()
    {
        // Register built-in converters
        Register(new AudioAssetConverter());
        Register(new ModAssetConverter());
        Register(new RawAssetConverter());
        // Future: BitmapAssetConverter, SpriteAssetConverter, etc.
    }

    /// <summary>
    /// Register a custom asset converter.
    /// </summary>
    public static void Register(IAssetConverter converter)
    {
        _converters.Add(converter);
    }

    /// <summary>
    /// Get a converter for the given asset type.
    /// </summary>
    public static IAssetConverter? GetConverter(AssetType type)
    {
        return _converters.FirstOrDefault(c => c.SupportedTypes.Contains(type));
    }

    /// <summary>
    /// Get a converter for the given file extension.
    /// </summary>
    public static IAssetConverter? GetConverterForExtension(string extension)
    {
        return _converters.FirstOrDefault(c =>
            c.SupportedExtensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Convert an asset file using the appropriate converter.
    /// </summary>
    public static AssetConversionResult? ConvertAsset(string filePath, EmbedOptions options)
    {
        var type = options.GetEffectiveType();
        var converter = GetConverter(type);

        if (converter == null)
        {
            // Fall back to raw converter for unknown types
            converter = GetConverter(AssetType.Raw);
        }

        return converter?.Convert(filePath, options);
    }

    /// <summary>
    /// Validate an asset file.
    /// </summary>
    public static string? ValidateAsset(string filePath, EmbedOptions options)
    {
        var type = options.GetEffectiveType();
        var converter = GetConverter(type);

        if (converter == null)
        {
            return $"No converter available for asset type: {type}";
        }

        return converter.Validate(filePath, options);
    }
}

/// <summary>
/// Converter for raw binary data (no conversion).
/// </summary>
public class RawAssetConverter : IAssetConverter
{
    public AssetType[] SupportedTypes => new[] { AssetType.Raw };
    public string[] SupportedExtensions => new[] { ".raw", ".bin", ".dat" };

    public AssetConversionResult Convert(string filePath, EmbedOptions options)
    {
        var data = File.ReadAllBytes(filePath);

        return new AssetConversionResult
        {
            Data = data,
            RequiresChipRam = options.ChipRam,
            Metadata =
            {
                ["size"] = data.Length
            }
        };
    }

    public string? Validate(string filePath, EmbedOptions options)
    {
        if (!File.Exists(filePath))
        {
            return $"File not found: {filePath}";
        }

        return null;
    }
}

/// <summary>
/// Converter for audio files (WAV, AIFF, 8SVX) to Paula-compatible PCM.
/// </summary>
public class AudioAssetConverter : IAssetConverter
{
    public AssetType[] SupportedTypes => new[] { AssetType.Audio };
    public string[] SupportedExtensions => new[] { ".wav", ".wave", ".aiff", ".aif", ".8svx" };

    public AssetConversionResult Convert(string filePath, EmbedOptions options)
    {
        // Build conversion options
        var audioOptions = new Audio.AudioConverter.ConversionOptions
        {
            Normalize = options.Normalize,
            TrimSilence = options.TrimSilence,
            ChannelMode = options.ChannelMode
        };

        if (options.SampleRate.HasValue)
        {
            audioOptions.TargetSampleRate = options.SampleRate.Value;
        }

        // Perform conversion
        var result = Audio.AudioConverter.ConvertFile(filePath, audioOptions);
        if (result == null)
        {
            throw new InvalidOperationException($"Failed to convert audio file: {filePath}");
        }

        // Determine struct type based on chip placement
        var structType = options.ChipRam ? "AudioSample" : "AudioAsset";

        return new AssetConversionResult
        {
            Data = result.Data,
            RequiresChipRam = options.ChipRam,
            StructTypeName = structType,
            Metadata =
            {
                ["sample_rate"] = result.FinalSampleRate,
                ["length_bytes"] = result.LengthBytes,
                ["length_words"] = result.LengthWords,
                ["period_pal"] = result.PeriodPal,
                ["period_ntsc"] = result.PeriodNtsc,
                ["duration_ms"] = result.DurationMs
            }
        };
    }

    public string? Validate(string filePath, EmbedOptions options)
    {
        if (!File.Exists(filePath))
        {
            return $"Audio file not found: {filePath}";
        }

        var format = Audio.AudioConverter.DetectFormat(filePath);
        if (format == Audio.AudioFormat.Unknown)
        {
            return $"Unsupported audio format: {Path.GetExtension(filePath)}";
        }

        if (format == Audio.AudioFormat.Mod)
        {
            return "MOD files should use AssetType.Mod, not AssetType.Audio";
        }

        return null;
    }
}

/// <summary>
/// Converter for ProTracker MOD files.
/// </summary>
public class ModAssetConverter : IAssetConverter
{
    public AssetType[] SupportedTypes => new[] { AssetType.Mod };
    public string[] SupportedExtensions => new[] { ".mod", ".nst", ".m15" };

    public AssetConversionResult Convert(string filePath, EmbedOptions options)
    {
        var data = File.ReadAllBytes(filePath);

        // Validate MOD format signature
        ValidateModFormat(data, filePath);

        // Determine struct type based on chip placement
        var structType = options.ChipRam ? null : "ModAsset"; // null = raw data array when chip=true

        return new AssetConversionResult
        {
            Data = data,
            RequiresChipRam = options.ChipRam,
            StructTypeName = structType,
            Metadata =
            {
                ["size"] = data.Length,
                ["format"] = DetectModFormat(data)
            }
        };
    }

    public string? Validate(string filePath, EmbedOptions options)
    {
        if (!File.Exists(filePath))
        {
            return $"MOD file not found: {filePath}";
        }

        try
        {
            var data = File.ReadAllBytes(filePath);
            ValidateModFormat(data, filePath);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return null;
    }

    private void ValidateModFormat(byte[] data, string filePath)
    {
        if (data.Length < 1084)
        {
            throw new InvalidOperationException($"File too small to be a valid MOD: {filePath}");
        }

        // Check for MOD signature at offset 1080
        var signature = System.Text.Encoding.ASCII.GetString(data, 1080, 4);
        var validSignatures = new[] { "M.K.", "M!K!", "FLT4", "FLT8", "4CHN", "6CHN", "8CHN" };

        // Also check for 15-instrument modules (no signature)
        bool isValid = validSignatures.Contains(signature) || Is15InstrumentMod(data);

        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid or unsupported MOD format in: {filePath}");
        }
    }

    private bool Is15InstrumentMod(byte[] data)
    {
        // 15-instrument MODs are 600 bytes header + pattern data
        // This is a heuristic check
        if (data.Length < 600) return false;

        // Check if the data looks like valid sample headers
        // Sample names should be printable ASCII or zeros
        for (int i = 20; i < 470; i++)
        {
            byte b = data[i];
            if (b != 0 && (b < 32 || b > 126))
            {
                return false;
            }
        }

        return true;
    }

    private string DetectModFormat(byte[] data)
    {
        if (data.Length >= 1084)
        {
            var signature = System.Text.Encoding.ASCII.GetString(data, 1080, 4);
            return signature switch
            {
                "M.K." or "M!K!" => "ProTracker",
                "FLT4" => "StarTrekker 4ch",
                "FLT8" => "StarTrekker 8ch",
                "4CHN" => "4-channel",
                "6CHN" => "6-channel",
                "8CHN" => "8-channel",
                _ => "15-instrument"
            };
        }

        return "15-instrument";
    }
}
