using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Novus.Audio;

/// <summary>
/// Converts audio files to Paula-compatible format (signed 8-bit PCM).
/// Supports WAV, AIFF, and raw PCM input formats.
/// </summary>
public static class AudioConverter
{
    /// <summary>
    /// Paula clock frequencies for period calculation
    /// </summary>
    public const int PaulaClockPal = 3546895;
    public const int PaulaClockNtsc = 3579545;

    /// <summary>
    /// Valid period range for Paula
    /// </summary>
    public const int MinPeriod = 124;
    public const int MaxPeriod = 65535;

    /// <summary>
    /// Maximum sample length (128KB in words)
    /// </summary>
    public const int MaxLengthBytes = 131070;

    /// <summary>
    /// Options for audio conversion
    /// </summary>
    public class ConversionOptions
    {
        /// <summary>
        /// Target sample rate in Hz. If null, keeps original rate.
        /// </summary>
        public int? TargetSampleRate { get; set; }

        /// <summary>
        /// Normalize audio to maximum amplitude
        /// </summary>
        public bool Normalize { get; set; } = false;

        /// <summary>
        /// Trim silence from start and end (threshold in dB)
        /// </summary>
        public bool TrimSilence { get; set; } = false;

        /// <summary>
        /// Silence threshold for trimming (0.0 to 1.0)
        /// </summary>
        public float SilenceThreshold { get; set; } = 0.01f;

        /// <summary>
        /// Mix to mono (required for Paula single channel)
        /// </summary>
        public bool Mono { get; set; } = true;

        /// <summary>
        /// Which channel to use if not mixing: "left", "right", or "mix"
        /// </summary>
        public string ChannelMode { get; set; } = "mix";
    }

    /// <summary>
    /// Result of audio conversion
    /// </summary>
    public class ConversionResult
    {
        /// <summary>
        /// Converted audio data (signed 8-bit PCM)
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Original sample rate
        /// </summary>
        public int OriginalSampleRate { get; set; }

        /// <summary>
        /// Final sample rate after conversion
        /// </summary>
        public int FinalSampleRate { get; set; }

        /// <summary>
        /// Length in bytes
        /// </summary>
        public int LengthBytes => Data.Length;

        /// <summary>
        /// Length in words (for Paula AUDxLEN register)
        /// </summary>
        public int LengthWords => (Data.Length + 1) / 2;

        /// <summary>
        /// Pre-calculated PAL period for this sample rate
        /// </summary>
        public int PeriodPal => CalculatePeriod(FinalSampleRate, true);

        /// <summary>
        /// Pre-calculated NTSC period for this sample rate
        /// </summary>
        public int PeriodNtsc => CalculatePeriod(FinalSampleRate, false);

        /// <summary>
        /// Duration in milliseconds
        /// </summary>
        public int DurationMs => FinalSampleRate > 0 ? (Data.Length * 1000) / FinalSampleRate : 0;
    }

    /// <summary>
    /// Calculate Paula period from sample rate
    /// </summary>
    public static int CalculatePeriod(int sampleRate, bool pal)
    {
        if (sampleRate <= 0) return 322; // Default ~11kHz

        int clock = pal ? PaulaClockPal : PaulaClockNtsc;
        int period = clock / sampleRate;

        // Clamp to valid range
        return Math.Clamp(period, MinPeriod, MaxPeriod);
    }

    /// <summary>
    /// Convert a WAV file to Paula format
    /// </summary>
    public static ConversionResult ConvertWav(string filePath, ConversionOptions? options = null)
    {
        options ??= new ConversionOptions();

        using var reader = new WaveFileReader(filePath);
        return ConvertFromReader(reader, options);
    }

    /// <summary>
    /// Convert a WAV file from a stream
    /// </summary>
    public static ConversionResult ConvertWav(Stream stream, ConversionOptions? options = null)
    {
        options ??= new ConversionOptions();

        using var reader = new WaveFileReader(stream);
        return ConvertFromReader(reader, options);
    }

    /// <summary>
    /// Convert an AIFF file to Paula format
    /// </summary>
    public static ConversionResult ConvertAiff(string filePath, ConversionOptions? options = null)
    {
        options ??= new ConversionOptions();

        using var reader = new AiffFileReader(filePath);
        return ConvertFromReader(reader, options);
    }

    /// <summary>
    /// Convert raw PCM data to Paula format
    /// </summary>
    public static ConversionResult ConvertRaw(byte[] rawData, int sampleRate, int bitsPerSample, int channels, ConversionOptions? options = null)
    {
        options ??= new ConversionOptions();

        var format = new WaveFormat(sampleRate, bitsPerSample, channels);
        using var ms = new MemoryStream(rawData);
        using var reader = new RawSourceWaveStream(ms, format);
        return ConvertFromReader(reader, options);
    }

    /// <summary>
    /// Convert from any WaveStream to Paula format
    /// </summary>
    private static ConversionResult ConvertFromReader(WaveStream reader, ConversionOptions options)
    {
        var result = new ConversionResult
        {
            OriginalSampleRate = reader.WaveFormat.SampleRate
        };

        // Convert to sample provider for processing
        ISampleProvider sampleProvider = reader.ToSampleProvider();

        // Convert to mono if needed
        if (reader.WaveFormat.Channels > 1 && options.Mono)
        {
            sampleProvider = options.ChannelMode.ToLower() switch
            {
                "left" => new StereoToMonoSampleProvider(sampleProvider) { LeftVolume = 1.0f, RightVolume = 0.0f },
                "right" => new StereoToMonoSampleProvider(sampleProvider) { LeftVolume = 0.0f, RightVolume = 1.0f },
                _ => new StereoToMonoSampleProvider(sampleProvider) { LeftVolume = 0.5f, RightVolume = 0.5f }
            };
        }

        // Resample if needed
        int targetRate = options.TargetSampleRate ?? reader.WaveFormat.SampleRate;
        if (targetRate != reader.WaveFormat.SampleRate)
        {
            // Use WdlResamplingSampleProvider for high-quality resampling
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, targetRate);
        }

        result.FinalSampleRate = targetRate;

        // Read all samples
        var samples = ReadAllSamples(sampleProvider);

        // Trim silence if requested
        if (options.TrimSilence)
        {
            samples = TrimSilence(samples, options.SilenceThreshold);
        }

        // Normalize if requested
        if (options.Normalize)
        {
            samples = Normalize(samples);
        }

        // Convert to signed 8-bit PCM
        result.Data = ConvertToSigned8Bit(samples);

        // Ensure even length (Paula reads words)
        if (result.Data.Length % 2 != 0)
        {
            var paddedData = new byte[result.Data.Length + 1];
            Array.Copy(result.Data, paddedData, result.Data.Length);
            result.Data = paddedData;
        }

        // Note: We no longer truncate large files. The streaming audio system
        // (amiga::sys::device::audio_streaming) handles files larger than Paula's 128KB limit
        // by using triple-buffered playback. The compiler will generate an
        // AudioAsset for large files which can be played via AudioStreamer.
        //
        // For files <= 128KB, they can be played directly via AudioChannel.
        // For files > 128KB, use AudioStreamer for seamless playback.

        return result;
    }

    /// <summary>
    /// Read all samples from a sample provider
    /// </summary>
    private static float[] ReadAllSamples(ISampleProvider provider)
    {
        var samples = new List<float>();
        var buffer = new float[4096];
        int read;

        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                samples.Add(buffer[i]);
            }
        }

        return samples.ToArray();
    }

    /// <summary>
    /// Trim silence from start and end of sample data
    /// </summary>
    private static float[] TrimSilence(float[] samples, float threshold)
    {
        if (samples is []) return samples;

        // Find first non-silent sample
        int start = 0;
        while (start < samples.Length && Math.Abs(samples[start]) < threshold)
        {
            start++;
        }

        // Find last non-silent sample
        int end = samples.Length - 1;
        while (end > start && Math.Abs(samples[end]) < threshold)
        {
            end--;
        }

        // Extract non-silent portion
        int length = end - start + 1;
        if (length <= 0) return new float[1]; // Return single sample to avoid empty

        var trimmed = new float[length];
        Array.Copy(samples, start, trimmed, 0, length);
        return trimmed;
    }

    /// <summary>
    /// Normalize samples to maximum amplitude
    /// </summary>
    private static float[] Normalize(float[] samples)
    {
        if (samples is []) return samples;

        // Find peak amplitude
        float peak = 0;
        foreach (var sample in samples)
        {
            float abs = Math.Abs(sample);
            if (abs > peak) peak = abs;
        }

        // Avoid division by zero
        if (peak < 0.0001f) return samples;

        // Scale to full range
        float scale = 1.0f / peak;
        var normalized = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            normalized[i] = samples[i] * scale;
        }

        return normalized;
    }

    /// <summary>
    /// Convert float samples (-1.0 to 1.0) to signed 8-bit PCM (-128 to 127)
    /// Uses simple dithering for better quality
    /// </summary>
    private static byte[] ConvertToSigned8Bit(float[] samples)
    {
        var result = new byte[samples.Length];
        var random = new Random(42); // Deterministic seed for reproducible builds

        for (int i = 0; i < samples.Length; i++)
        {
            // Clamp to valid range
            float sample = Math.Clamp(samples[i], -1.0f, 1.0f);

            // Add triangular dither (reduces quantization noise)
            float dither = (float)(random.NextDouble() + random.NextDouble() - 1.0) * (1.0f / 256.0f);
            sample += dither;

            // Scale to 8-bit range (-128 to 127)
            int value = (int)(sample * 127.0f);
            value = Math.Clamp(value, -128, 127);

            // Store as signed byte (two's complement)
            result[i] = (byte)(sbyte)value;
        }

        return result;
    }

    /// <summary>
    /// Detect audio file format from extension
    /// </summary>
    public static AudioFormat DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".wav" => AudioFormat.Wav,
            ".wave" => AudioFormat.Wav,
            ".aif" => AudioFormat.Aiff,
            ".aiff" => AudioFormat.Aiff,
            ".8svx" => AudioFormat.Iff8Svx,
            ".iff" => AudioFormat.Iff8Svx,
            ".raw" => AudioFormat.Raw,
            ".pcm" => AudioFormat.Raw,
            ".mod" => AudioFormat.Mod,
            ".nst" => AudioFormat.Mod,
            ".m15" => AudioFormat.Mod,
            _ => AudioFormat.Unknown
        };
    }

    /// <summary>
    /// Convert a file based on its detected format
    /// </summary>
    public static ConversionResult? ConvertFile(string filePath, ConversionOptions? options = null)
    {
        var format = DetectFormat(filePath);
        return format switch
        {
            AudioFormat.Wav => ConvertWav(filePath, options),
            AudioFormat.Aiff => ConvertAiff(filePath, options),
            AudioFormat.Iff8Svx => Iff8SvxReader.Convert(filePath, options),
            AudioFormat.Raw => throw new ArgumentException("Raw format requires explicit sample rate"),
            AudioFormat.Mod => throw new ArgumentException("MOD files use @mod attribute, not @audio"),
            _ => throw new ArgumentException($"Unsupported audio format: {Path.GetExtension(filePath)}")
        };
    }
}

/// <summary>
/// Supported audio formats
/// </summary>
public enum AudioFormat
{
    Unknown,
    Wav,
    Aiff,
    Iff8Svx,
    Raw,
    Mod
}
