namespace Novus.Audio;

/// <summary>
/// Reader for IFF-8SVX (8-bit Sampled Voice) audio files.
/// This is the native Amiga audio format, already in Paula-compatible format.
/// </summary>
public static class Iff8SvxReader
{
    /// <summary>
    /// IFF chunk identifiers
    /// </summary>
    private const uint FORM = 0x464F524D; // "FORM"
    private const uint BODY = 0x424F4459; // "BODY"
    private const uint VHDR = 0x56484452; // "VHDR"
    private const uint NAME = 0x4E414D45; // "NAME"
    private const uint _8SVX = 0x38535658; // "8SVX"

    /// <summary>
    /// Voice header structure
    /// </summary>
    public class VoiceHeader
    {
        public uint OneShotSamples { get; set; }
        public uint RepeatSamples { get; set; }
        public uint SamplesPerCycle { get; set; }
        public ushort SampleRate { get; set; }
        public byte NumOctaves { get; set; }
        public byte Compression { get; set; }
        public int Volume { get; set; }  // Fixed-point 16.16
    }

    /// <summary>
    /// Result of parsing an 8SVX file
    /// </summary>
    public class ParseResult
    {
        public VoiceHeader? Header { get; set; }
        public byte[] SampleData { get; set; } = Array.Empty<byte>();
        public string? Name { get; set; }
        public bool IsCompressed => Header?.Compression != 0;
    }

    /// <summary>
    /// Parse an IFF-8SVX file
    /// </summary>
    public static ParseResult Parse(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Parse(stream);
    }

    /// <summary>
    /// Parse an IFF-8SVX file from a stream
    /// </summary>
    public static ParseResult Parse(Stream stream)
    {
        var result = new ParseResult();
        using var reader = new BinaryReader(stream);

        // Read FORM header
        uint formId = ReadBigEndianUInt32(reader);
        if (formId != FORM)
        {
            throw new FormatException("Not a valid IFF file (missing FORM header)");
        }

        uint formSize = ReadBigEndianUInt32(reader);
        uint formType = ReadBigEndianUInt32(reader);

        if (formType != _8SVX)
        {
            throw new FormatException($"Not an 8SVX file (type is 0x{formType:X8})");
        }

        // Parse chunks
        long formEnd = stream.Position + formSize - 4; // -4 for the type we already read

        while (stream.Position < formEnd && stream.Position < stream.Length)
        {
            uint chunkId = ReadBigEndianUInt32(reader);
            uint chunkSize = ReadBigEndianUInt32(reader);
            long chunkStart = stream.Position;

            switch (chunkId)
            {
                case VHDR:
                    result.Header = ParseVoiceHeader(reader);
                    break;

                case BODY:
                    result.SampleData = reader.ReadBytes((int)chunkSize);
                    break;

                case NAME:
                    var nameBytes = reader.ReadBytes((int)chunkSize);
                    result.Name = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                    break;

                default:
                    // Skip unknown chunks
                    break;
            }

            // Move to next chunk (chunks are word-aligned)
            long nextPos = chunkStart + chunkSize;
            if ((chunkSize & 1) != 0) nextPos++; // Pad to word boundary
            stream.Position = Math.Min(nextPos, stream.Length);
        }

        // Handle Fibonacci Delta compression if present
        if (result.IsCompressed && result.SampleData.Length > 0)
        {
            result.SampleData = DecompressFibonacciDelta(result.SampleData);
        }

        return result;
    }

    /// <summary>
    /// Parse VHDR (Voice Header) chunk
    /// </summary>
    private static VoiceHeader ParseVoiceHeader(BinaryReader reader)
    {
        return new VoiceHeader
        {
            OneShotSamples = ReadBigEndianUInt32(reader),
            RepeatSamples = ReadBigEndianUInt32(reader),
            SamplesPerCycle = ReadBigEndianUInt32(reader),
            SampleRate = ReadBigEndianUInt16(reader),
            NumOctaves = reader.ReadByte(),
            Compression = reader.ReadByte(),
            Volume = ReadBigEndianInt32(reader)
        };
    }

    /// <summary>
    /// Decompress Fibonacci Delta encoded data
    /// </summary>
    private static byte[] DecompressFibonacciDelta(byte[] compressed)
    {
        // Fibonacci delta compression uses a table of deltas
        sbyte[] deltaTable = { -34, -21, -13, -8, -5, -3, -2, -1, 0, 1, 2, 3, 5, 8, 13, 21 };

        var decompressed = new List<byte>();
        sbyte value = 0;

        foreach (byte b in compressed)
        {
            // Each byte contains two 4-bit indices
            int index1 = (b >> 4) & 0x0F;
            int index2 = b & 0x0F;

            value += deltaTable[index1];
            decompressed.Add((byte)value);

            value += deltaTable[index2];
            decompressed.Add((byte)value);
        }

        return decompressed.ToArray();
    }

    /// <summary>
    /// Convert an 8SVX file to AudioConverter.ConversionResult
    /// </summary>
    public static AudioConverter.ConversionResult Convert(string filePath, AudioConverter.ConversionOptions? options = null)
    {
        var parsed = Parse(filePath);

        var result = new AudioConverter.ConversionResult
        {
            Data = parsed.SampleData,
            OriginalSampleRate = parsed.Header?.SampleRate ?? 8363,
            FinalSampleRate = parsed.Header?.SampleRate ?? 8363
        };

        // Apply options if provided
        if (options != null)
        {
            // Resample if requested
            if (options.TargetSampleRate.HasValue && options.TargetSampleRate.Value != result.OriginalSampleRate)
            {
                result.Data = Resample(result.Data, result.OriginalSampleRate, options.TargetSampleRate.Value);
                result.FinalSampleRate = options.TargetSampleRate.Value;
            }

            // Normalize if requested
            if (options.Normalize)
            {
                result.Data = NormalizeSigned8Bit(result.Data);
            }

            // Trim silence if requested
            if (options.TrimSilence)
            {
                result.Data = TrimSilenceSigned8Bit(result.Data, (sbyte)(options.SilenceThreshold * 127));
            }
        }

        // Ensure even length
        if (result.Data.Length % 2 != 0)
        {
            var paddedData = new byte[result.Data.Length + 1];
            Array.Copy(result.Data, paddedData, result.Data.Length);
            result.Data = paddedData;
        }

        return result;
    }

    /// <summary>
    /// Simple linear resampling for 8-bit signed data
    /// </summary>
    private static byte[] Resample(byte[] data, int fromRate, int toRate)
    {
        if (fromRate == toRate) return data;

        double ratio = (double)fromRate / toRate;
        int newLength = (int)(data.Length / ratio);
        var result = new byte[newLength];

        for (int i = 0; i < newLength; i++)
        {
            double srcIndex = i * ratio;
            int index1 = (int)srcIndex;
            int index2 = Math.Min(index1 + 1, data.Length - 1);
            double frac = srcIndex - index1;

            sbyte s1 = (sbyte)data[index1];
            sbyte s2 = (sbyte)data[index2];

            sbyte interpolated = (sbyte)(s1 + (s2 - s1) * frac);
            result[i] = (byte)interpolated;
        }

        return result;
    }

    /// <summary>
    /// Normalize signed 8-bit data to maximum amplitude
    /// </summary>
    private static byte[] NormalizeSigned8Bit(byte[] data)
    {
        if (data.Length == 0) return data;

        // Find peak amplitude
        int peak = 0;
        foreach (byte b in data)
        {
            int abs = Math.Abs((sbyte)b);
            if (abs > peak) peak = abs;
        }

        if (peak == 0) return data;

        // Scale to maximum
        var result = new byte[data.Length];
        double scale = 127.0 / peak;

        for (int i = 0; i < data.Length; i++)
        {
            sbyte sample = (sbyte)data[i];
            sbyte scaled = (sbyte)Math.Clamp((int)(sample * scale), -128, 127);
            result[i] = (byte)scaled;
        }

        return result;
    }

    /// <summary>
    /// Trim silence from signed 8-bit data
    /// </summary>
    private static byte[] TrimSilenceSigned8Bit(byte[] data, sbyte threshold)
    {
        if (data.Length == 0) return data;

        // Find first non-silent sample
        int start = 0;
        while (start < data.Length && Math.Abs((sbyte)data[start]) <= threshold)
        {
            start++;
        }

        // Find last non-silent sample
        int end = data.Length - 1;
        while (end > start && Math.Abs((sbyte)data[end]) <= threshold)
        {
            end--;
        }

        int length = end - start + 1;
        if (length <= 0) return new byte[2]; // Minimum for Paula

        var result = new byte[length];
        Array.Copy(data, start, result, 0, length);
        return result;
    }

    /// <summary>
    /// Read big-endian uint32
    /// </summary>
    private static uint ReadBigEndianUInt32(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
    }

    /// <summary>
    /// Read big-endian uint16
    /// </summary>
    private static ushort ReadBigEndianUInt16(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        return (ushort)((bytes[0] << 8) | bytes[1]);
    }

    /// <summary>
    /// Read big-endian int32
    /// </summary>
    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        return (int)ReadBigEndianUInt32(reader);
    }
}
