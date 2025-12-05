using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novus.Optimizer;

/// <summary>
/// Profile data collected from instrumented execution.
/// Used for profile-guided optimization (PGO).
/// </summary>
public class ProfileData
{
    /// <summary>
    /// Magic number to identify .pgo files
    /// </summary>
    public const uint Magic = 0x4E565047; // "NVPG" (Novus Profile Guided)

    /// <summary>
    /// Current profile data format version
    /// </summary>
    public const uint Version = 1;

    /// <summary>
    /// SHA-256 hash of source files (first 16 bytes as hex)
    /// Used to detect stale profile data
    /// </summary>
    public string SourceHash { get; set; } = "";

    /// <summary>
    /// Total number of profile runs merged into this data
    /// </summary>
    public int RunCount { get; set; } = 1;

    /// <summary>
    /// Function-level profile data
    /// Key: function name (mangled)
    /// </summary>
    public Dictionary<string, FunctionProfile> Functions { get; set; } = new();

    /// <summary>
    /// Save profile data to a binary file
    /// </summary>
    public void Save(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        // Header
        writer.Write(Magic);
        writer.Write(Version);

        // Source hash (fixed 32 bytes)
        var hashBytes = Encoding.UTF8.GetBytes(SourceHash.PadRight(32));
        writer.Write(hashBytes, 0, 32);

        // Run count
        writer.Write(RunCount);

        // Function count
        writer.Write(Functions.Count);

        // Functions
        foreach (var (name, profile) in Functions)
        {
            WriteString(writer, name);
            profile.Write(writer);
        }
    }

    /// <summary>
    /// Load profile data from a binary file
    /// </summary>
    public static ProfileData Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        // Verify magic
        var magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid profile file: expected magic 0x{Magic:X8}, got 0x{magic:X8}");
        }

        // Verify version
        var version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported profile version: {version} (expected {Version})");
        }

        var data = new ProfileData();

        // Source hash
        var hashBytes = reader.ReadBytes(32);
        data.SourceHash = Encoding.UTF8.GetString(hashBytes).TrimEnd();

        // Run count
        data.RunCount = reader.ReadInt32();

        // Functions
        var functionCount = reader.ReadInt32();
        for (int i = 0; i < functionCount; i++)
        {
            var name = ReadString(reader);
            var profile = FunctionProfile.Read(reader);
            data.Functions[name] = profile;
        }

        return data;
    }

    /// <summary>
    /// Merge another profile into this one
    /// </summary>
    public void Merge(ProfileData other)
    {
        if (SourceHash != other.SourceHash)
        {
            Console.Error.WriteLine($"Warning: Merging profiles with different source hashes");
        }

        RunCount += other.RunCount;

        foreach (var (name, otherProfile) in other.Functions)
        {
            if (Functions.TryGetValue(name, out var existingProfile))
            {
                existingProfile.Merge(otherProfile);
            }
            else
            {
                Functions[name] = otherProfile.Clone();
            }
        }
    }

    /// <summary>
    /// Compute source hash from source files
    /// </summary>
    public static string ComputeSourceHash(IEnumerable<string> sourceFiles)
    {
        using var sha256 = SHA256.Create();
        var combined = new StringBuilder();

        foreach (var file in sourceFiles.OrderBy(f => f))
        {
            if (File.Exists(file))
            {
                combined.Append(File.ReadAllText(file));
            }
        }

        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined.ToString()));
        return Convert.ToHexString(hashBytes)[..32].ToLowerInvariant();
    }

    private static void WriteString(BinaryWriter writer, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
}

/// <summary>
/// Profile data for a single function
/// </summary>
public class FunctionProfile
{
    /// <summary>
    /// Total number of times this function was called
    /// </summary>
    public long CallCount { get; set; }

    /// <summary>
    /// Branch profile data
    /// Key: branch ID (label or instruction index)
    /// </summary>
    public Dictionary<string, BranchProfile> Branches { get; set; } = new();

    /// <summary>
    /// Loop profile data
    /// Key: loop ID (label)
    /// </summary>
    public Dictionary<string, LoopProfile> Loops { get; set; } = new();

    public void Write(BinaryWriter writer)
    {
        writer.Write(CallCount);

        // Branches
        writer.Write(Branches.Count);
        foreach (var (id, profile) in Branches)
        {
            WriteString(writer, id);
            profile.Write(writer);
        }

        // Loops
        writer.Write(Loops.Count);
        foreach (var (id, profile) in Loops)
        {
            WriteString(writer, id);
            profile.Write(writer);
        }
    }

    public static FunctionProfile Read(BinaryReader reader)
    {
        var profile = new FunctionProfile
        {
            CallCount = reader.ReadInt64()
        };

        // Branches
        var branchCount = reader.ReadInt32();
        for (int i = 0; i < branchCount; i++)
        {
            var id = ReadString(reader);
            profile.Branches[id] = BranchProfile.Read(reader);
        }

        // Loops
        var loopCount = reader.ReadInt32();
        for (int i = 0; i < loopCount; i++)
        {
            var id = ReadString(reader);
            profile.Loops[id] = LoopProfile.Read(reader);
        }

        return profile;
    }

    public void Merge(FunctionProfile other)
    {
        CallCount += other.CallCount;

        foreach (var (id, otherBranch) in other.Branches)
        {
            if (Branches.TryGetValue(id, out var existing))
            {
                existing.Merge(otherBranch);
            }
            else
            {
                Branches[id] = otherBranch.Clone();
            }
        }

        foreach (var (id, otherLoop) in other.Loops)
        {
            if (Loops.TryGetValue(id, out var existing))
            {
                existing.Merge(otherLoop);
            }
            else
            {
                Loops[id] = otherLoop.Clone();
            }
        }
    }

    public FunctionProfile Clone()
    {
        var clone = new FunctionProfile { CallCount = CallCount };
        foreach (var (id, branch) in Branches)
            clone.Branches[id] = branch.Clone();
        foreach (var (id, loop) in Loops)
            clone.Loops[id] = loop.Clone();
        return clone;
    }

    /// <summary>
    /// Calculate hotness score (0.0 to 1.0 relative to hottest function)
    /// </summary>
    public double GetHotnessScore(long maxCallCount)
    {
        if (maxCallCount <= 0) return 0.0;
        return (double)CallCount / maxCallCount;
    }

    /// <summary>
    /// Check if this function is hot (called frequently)
    /// </summary>
    public bool IsHot(long threshold = 1000) => CallCount >= threshold;

    private static void WriteString(BinaryWriter writer, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
}

/// <summary>
/// Profile data for a branch (if/else, match arm, etc.)
/// </summary>
public class BranchProfile
{
    /// <summary>
    /// Number of times the "true" branch was taken
    /// </summary>
    public long TrueTaken { get; set; }

    /// <summary>
    /// Number of times the "false" branch was taken
    /// </summary>
    public long FalseTaken { get; set; }

    /// <summary>
    /// Total number of times this branch was executed
    /// </summary>
    public long TotalExecutions => TrueTaken + FalseTaken;

    /// <summary>
    /// Probability of taking the true branch (0.0 to 1.0)
    /// </summary>
    public double TrueProbability => TotalExecutions > 0 ? (double)TrueTaken / TotalExecutions : 0.5;

    /// <summary>
    /// Returns true if this branch is strongly biased (>80% one way)
    /// </summary>
    public bool IsStronglyBiased => TrueProbability > 0.8 || TrueProbability < 0.2;

    /// <summary>
    /// Returns true if the true branch is more likely
    /// </summary>
    public bool IsTrueLikely => TrueProbability > 0.5;

    public void Write(BinaryWriter writer)
    {
        writer.Write(TrueTaken);
        writer.Write(FalseTaken);
    }

    public static BranchProfile Read(BinaryReader reader)
    {
        return new BranchProfile
        {
            TrueTaken = reader.ReadInt64(),
            FalseTaken = reader.ReadInt64()
        };
    }

    public void Merge(BranchProfile other)
    {
        TrueTaken += other.TrueTaken;
        FalseTaken += other.FalseTaken;
    }

    public BranchProfile Clone() => new() { TrueTaken = TrueTaken, FalseTaken = FalseTaken };
}

/// <summary>
/// Profile data for a loop
/// </summary>
public class LoopProfile
{
    /// <summary>
    /// Number of times the loop was entered
    /// </summary>
    public long EntryCount { get; set; }

    /// <summary>
    /// Total number of iterations across all executions
    /// </summary>
    public long TotalIterations { get; set; }

    /// <summary>
    /// Minimum iterations observed in any single execution
    /// </summary>
    public long MinIterations { get; set; } = long.MaxValue;

    /// <summary>
    /// Maximum iterations observed in any single execution
    /// </summary>
    public long MaxIterations { get; set; }

    /// <summary>
    /// Average iterations per loop entry
    /// </summary>
    public double AverageIterations => EntryCount > 0 ? (double)TotalIterations / EntryCount : 0;

    /// <summary>
    /// Returns true if this loop has consistent iteration count (good for unrolling)
    /// </summary>
    public bool HasConsistentIterations => MinIterations == MaxIterations && MinIterations > 0;

    /// <summary>
    /// Suggested unroll factor based on iteration count
    /// Returns 0 if unrolling is not recommended
    /// </summary>
    public int SuggestedUnrollFactor
    {
        get
        {
            if (!HasConsistentIterations) return 0;
            if (MaxIterations <= 4) return (int)MaxIterations; // Full unroll
            if (MaxIterations <= 8) return 2;
            if (MaxIterations <= 16) return 4;
            return 0; // Too large to unroll
        }
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(EntryCount);
        writer.Write(TotalIterations);
        writer.Write(MinIterations == long.MaxValue ? 0 : MinIterations);
        writer.Write(MaxIterations);
    }

    public static LoopProfile Read(BinaryReader reader)
    {
        var profile = new LoopProfile
        {
            EntryCount = reader.ReadInt64(),
            TotalIterations = reader.ReadInt64(),
            MinIterations = reader.ReadInt64(),
            MaxIterations = reader.ReadInt64()
        };
        if (profile.MinIterations == 0 && profile.EntryCount == 0)
            profile.MinIterations = long.MaxValue;
        return profile;
    }

    public void Merge(LoopProfile other)
    {
        EntryCount += other.EntryCount;
        TotalIterations += other.TotalIterations;
        MinIterations = Math.Min(MinIterations, other.MinIterations);
        MaxIterations = Math.Max(MaxIterations, other.MaxIterations);
    }

    public LoopProfile Clone() => new()
    {
        EntryCount = EntryCount,
        TotalIterations = TotalIterations,
        MinIterations = MinIterations,
        MaxIterations = MaxIterations
    };

    /// <summary>
    /// Record a loop execution with a specific iteration count
    /// </summary>
    public void RecordExecution(long iterations)
    {
        EntryCount++;
        TotalIterations += iterations;
        MinIterations = Math.Min(MinIterations, iterations);
        MaxIterations = Math.Max(MaxIterations, iterations);
    }
}
