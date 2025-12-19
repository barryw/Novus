namespace Novus.Assets;

/// <summary>
/// Paula audio chip hardware limits.
/// </summary>
public static class PaulaLimits
{
    /// <summary>
    /// PAL clock frequency (Hz).
    /// </summary>
    public const int PalClock = 3546895;

    /// <summary>
    /// NTSC clock frequency (Hz).
    /// </summary>
    public const int NtscClock = 3579545;

    /// <summary>
    /// Minimum period value (hardware limit).
    /// </summary>
    public const int MinPeriod = 124;

    /// <summary>
    /// Maximum practical sample rate for PAL (~28.6 kHz).
    /// </summary>
    public const int MaxSampleRatePal = PalClock / MinPeriod; // ~28603 Hz

    /// <summary>
    /// Maximum practical sample rate for NTSC (~28.9 kHz).
    /// </summary>
    public const int MaxSampleRateNtsc = NtscClock / MinPeriod; // ~28867 Hz

    /// <summary>
    /// Maximum sample rate before error (use PAL as the limit since it's lower).
    /// </summary>
    public const int MaxSampleRate = 28000;

    /// <summary>
    /// Sample rate threshold for warning (diminishing returns above this).
    /// </summary>
    public const int WarnSampleRate = 22050;

    /// <summary>
    /// Default sample rate when not specified.
    /// </summary>
    public const int DefaultSampleRate = 11025;
}

/// <summary>
/// Validation message severity.
/// </summary>
public enum ValidationSeverity
{
    Warning,
    Error
}

/// <summary>
/// A validation message with severity.
/// </summary>
public class ValidationMessage
{
    public ValidationSeverity Severity { get; set; }
    public string Message { get; set; } = "";

    public static ValidationMessage Warning(string message) => new() { Severity = ValidationSeverity.Warning, Message = message };
    public static ValidationMessage Error(string message) => new() { Severity = ValidationSeverity.Error, Message = message };
}

/// <summary>
/// Result of asset validation.
/// </summary>
public class AssetValidationResult
{
    public List<ValidationMessage> Messages { get; set; } = new();

    public bool HasErrors => Messages.Any(m => m.Severity == ValidationSeverity.Error);
    public bool HasWarnings => Messages.Any(m => m.Severity == ValidationSeverity.Warning);

    public IEnumerable<ValidationMessage> Errors => Messages.Where(m => m.Severity == ValidationSeverity.Error);
    public IEnumerable<ValidationMessage> Warnings => Messages.Where(m => m.Severity == ValidationSeverity.Warning);

    /// <summary>
    /// Get the first error message, or null if no errors.
    /// </summary>
    public string? FirstError => Errors.FirstOrDefault()?.Message;
}

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

    /// <summary>
    /// Warnings generated during conversion.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
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
    /// Returns validation result with warnings and errors.
    /// </summary>
    AssetValidationResult Validate(string filePath, EmbedOptions options);
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
    public static AssetValidationResult ValidateAsset(string filePath, EmbedOptions options)
    {
        var type = options.GetEffectiveType();
        var converter = GetConverter(type);

        if (converter == null)
        {
            return new AssetValidationResult
            {
                Messages = { ValidationMessage.Error($"No converter available for asset type: {type}") }
            };
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

    public AssetValidationResult Validate(string filePath, EmbedOptions options)
    {
        var result = new AssetValidationResult();

        if (!File.Exists(filePath))
        {
            result.Messages.Add(ValidationMessage.Error($"File not found: {filePath}"));
        }

        return result;
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

        // Apply sample rate: use specified value or default to 11025
        var targetSampleRate = options.SampleRate ?? PaulaLimits.DefaultSampleRate;
        audioOptions.TargetSampleRate = targetSampleRate;

        // Perform conversion
        var result = Audio.AudioConverter.ConvertFile(filePath, audioOptions);
        if (result == null)
        {
            throw new InvalidOperationException($"Failed to convert audio file: {filePath}");
        }

        // Determine struct type based on chip placement
        var structType = options.ChipRam ? "AudioSample" : "AudioAsset";

        var conversionResult = new AssetConversionResult
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

        // Add warning if sample rate is high (only if explicitly specified)
        if (options.SampleRate.HasValue && options.SampleRate.Value > PaulaLimits.WarnSampleRate)
        {
            conversionResult.Warnings.Add(
                $"Sample rate {options.SampleRate.Value} Hz exceeds {PaulaLimits.WarnSampleRate} Hz. " +
                $"Higher rates consume more DMA bandwidth with minimal audible improvement on Amiga hardware. " +
                $"Consider using {PaulaLimits.DefaultSampleRate} Hz for better performance.");
        }

        return conversionResult;
    }

    public AssetValidationResult Validate(string filePath, EmbedOptions options)
    {
        var result = new AssetValidationResult();

        if (!File.Exists(filePath))
        {
            result.Messages.Add(ValidationMessage.Error($"Audio file not found: {filePath}"));
            return result;
        }

        var format = Audio.AudioConverter.DetectFormat(filePath);
        if (format == Audio.AudioFormat.Unknown)
        {
            result.Messages.Add(ValidationMessage.Error($"Unsupported audio format: {Path.GetExtension(filePath)}"));
            return result;
        }

        if (format == Audio.AudioFormat.Mod)
        {
            result.Messages.Add(ValidationMessage.Error("MOD files should use AssetType.Mod, not AssetType.Audio"));
            return result;
        }

        // Validate sample rate if specified
        if (options.SampleRate.HasValue)
        {
            var sampleRate = options.SampleRate.Value;

            if (sampleRate > PaulaLimits.MaxSampleRate)
            {
                result.Messages.Add(ValidationMessage.Error(
                    $"Sample rate {sampleRate} Hz exceeds Paula's maximum of {PaulaLimits.MaxSampleRate} Hz. " +
                    $"The Amiga's audio hardware cannot play samples faster than ~28 kHz. " +
                    $"Use sample_rate = {PaulaLimits.WarnSampleRate} or lower."));
            }
            else if (sampleRate > PaulaLimits.WarnSampleRate)
            {
                result.Messages.Add(ValidationMessage.Warning(
                    $"Sample rate {sampleRate} Hz exceeds {PaulaLimits.WarnSampleRate} Hz. " +
                    $"Higher rates consume more DMA bandwidth with minimal audible improvement on Amiga hardware. " +
                    $"Consider using {PaulaLimits.DefaultSampleRate} Hz for better performance."));
            }

            if (sampleRate < 1000)
            {
                result.Messages.Add(ValidationMessage.Error(
                    $"Sample rate {sampleRate} Hz is too low. Minimum practical rate is 1000 Hz."));
            }
        }

        return result;
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

    public AssetValidationResult Validate(string filePath, EmbedOptions options)
    {
        var result = new AssetValidationResult();

        if (!File.Exists(filePath))
        {
            result.Messages.Add(ValidationMessage.Error($"MOD file not found: {filePath}"));
            return result;
        }

        try
        {
            var data = File.ReadAllBytes(filePath);
            ValidateModFormat(data, filePath);
        }
        catch (Exception ex)
        {
            result.Messages.Add(ValidationMessage.Error(ex.Message));
        }

        return result;
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
