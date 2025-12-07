using Novus.Diagnostics;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Represents a parsed attribute with its arguments
/// </summary>
public record AttributeInfo
{
    /// <summary>
    /// The attribute name (e.g., "library", "inline", "test")
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Named arguments (e.g., name = "foo", version = 1)
    /// </summary>
    public Dictionary<string, object> NamedArgs { get; init; }

    /// <summary>
    /// Positional arguments (arguments without names)
    /// </summary>
    public List<object> PositionalArgs { get; init; }

    /// <summary>
    /// Source location of the attribute for error reporting
    /// </summary>
    public SourceLocation Location { get; init; }

    public AttributeInfo(string name, SourceLocation location)
    {
        Name = name;
        Location = location;
        NamedArgs = new Dictionary<string, object>();
        PositionalArgs = new List<object>();
    }

    /// <summary>
    /// Get a named argument as a specific type
    /// </summary>
    public T? GetNamedArg<T>(string argName)
    {
        if (NamedArgs.TryGetValue(argName, out var value))
        {
            if (value is T typedValue)
                return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Get a named argument as string, or null if not present
    /// </summary>
    public string? GetString(string argName) => GetNamedArg<string>(argName);

    /// <summary>
    /// Get a named argument as int, or null if not present
    /// </summary>
    public int? GetInt(string argName)
    {
        if (NamedArgs.TryGetValue(argName, out var value) && value is int intValue)
            return intValue;
        return null;
    }

    /// <summary>
    /// Get a named argument as bool, or null if not present
    /// </summary>
    public bool? GetBool(string argName)
    {
        if (NamedArgs.TryGetValue(argName, out var value) && value is bool boolValue)
            return boolValue;
        return null;
    }

    /// <summary>
    /// Check if a named argument exists
    /// </summary>
    public bool HasArg(string argName) => NamedArgs.ContainsKey(argName);

    /// <summary>
    /// Check if a bare identifier exists as a positional argument (e.g., @test(should_panic))
    /// </summary>
    public bool HasFlag(string flagName) =>
        NamedArgs.ContainsKey(flagName) || PositionalArgs.Any(p => p is string s && s == flagName);

    /// <summary>
    /// Get positional argument at index, or default if not present
    /// </summary>
    public T? GetPositionalArg<T>(int index)
    {
        if (index >= 0 && index < PositionalArgs.Count)
        {
            if (PositionalArgs[index] is T typedValue)
                return typedValue;
        }
        return default;
    }

    public override string ToString()
    {
        var args = new List<string>();

        foreach (var (key, value) in NamedArgs)
        {
            args.Add($"{key} = {value}");
        }

        foreach (var value in PositionalArgs)
        {
            args.Add(value.ToString() ?? "");
        }

        return args.Count > 0
            ? $"@{Name}({string.Join(", ", args)})"
            : $"@{Name}";
    }
}

/// <summary>
/// Collection of attributes with helper methods for common queries
/// </summary>
public class AttributeCollection
{
    private readonly List<AttributeInfo> _attributes;

    public AttributeCollection(List<AttributeInfo>? attributes = null)
    {
        _attributes = attributes ?? new List<AttributeInfo>();
    }

    public IReadOnlyList<AttributeInfo> All => _attributes;

    /// <summary>
    /// Check if an attribute with the given name exists
    /// </summary>
    public bool Has(string name) => _attributes.Any(a => a.Name == name);

    /// <summary>
    /// Get the first attribute with the given name, or null
    /// </summary>
    public AttributeInfo? Get(string name) => _attributes.FirstOrDefault(a => a.Name == name);

    /// <summary>
    /// Get all attributes with the given name
    /// </summary>
    public List<AttributeInfo> GetAll(string name) => _attributes.Where(a => a.Name == name).ToList();

    /// <summary>
    /// Add an attribute
    /// </summary>
    public void Add(AttributeInfo attr) => _attributes.Add(attr);

    /// <summary>
    /// Get count of attributes
    /// </summary>
    public int Count => _attributes.Count;

    public override string ToString()
    {
        return string.Join(" ", _attributes.Select(a => a.ToString()));
    }
}

/// <summary>
/// Well-known attribute names for validation
/// </summary>
public static class KnownAttributes
{
    // Library/Device attributes
    public const string Library = "library";
    public const string LibFunc = "libfunc";
    public const string LibOpen = "libopen";
    public const string LibClose = "libclose";
    public const string LibExpunge = "libexpunge";
    public const string LibInit = "libinit";

    // Code generation attributes
    public const string Inline = "inline";
    public const string NoInline = "noinline";
    public const string Packed = "packed";
    public const string Align = "align";

    // Testing attributes
    public const string Test = "test";
    public const string Bench = "bench";        // Short form
    public const string Benchmark = "benchmark"; // Long form (alias for bench)

    // Documentation attributes
    public const string Deprecated = "deprecated";
    public const string Since = "since";
    public const string Experimental = "experimental";

    // Safety attributes
    public const string Unsafe = "unsafe";
    public const string ThreadSafe = "threadsafe";
    public const string SingleThreaded = "singlethreaded";

    // Optimization attributes
    public const string Cold = "cold";
    public const string Hot = "hot";
    public const string Const = "const";

    // Platform attributes
    public const string Target = "target";
    public const string Cfg = "cfg";

    // C interop attributes
    public const string Export = "export";
    public const string ExternType = "extern_type";

    // Diagnostic attributes
    public const string Suppress = "suppress";

    // Audio asset attributes
    public const string Audio = "audio";         // Compile-time audio conversion: @audio("file.wav", sample_rate: 11025)
    public const string AudioRaw = "audio_raw";  // Raw PCM include: @audio_raw("file.raw", sample_rate: 8000)
    public const string Mod = "mod";             // ProTracker module include: @mod("music.mod")

    // Memory section attributes
    public const string ChipRam = "chip_ram";    // Force static into chip RAM

    // Module-level configuration attributes
    public const string StackSize = "stack_size"; // Set program stack size: #[stack_size(65536)]
    public const string Cpu = "cpu";               // Override target CPU: #[cpu("68020")] - emits warning if overriding project/CLI settings

    // Interrupt handling attributes
    public const string Interrupt = "interrupt";           // Mark function as interrupt handler
    public const string InterruptSafe = "interrupt_safe";  // Mark function as safe to call from interrupts

    // Synchronization attributes
    public const string Atomic = "atomic";                 // Wrap function body in Forbid/Permit critical section
    public const string NoInterrupts = "no_interrupts";    // Wrap function body in Disable/Enable (use sparingly!)

    /// <summary>
    /// All known attribute names for validation
    /// </summary>
    public static readonly HashSet<string> All = new()
    {
        Library, LibFunc, LibOpen, LibClose, LibExpunge, LibInit,
        Inline, NoInline, Packed, Align,
        Test, Bench, Benchmark,
        Deprecated, Since, Experimental,
        Unsafe, ThreadSafe, SingleThreaded,
        Cold, Hot, Const,
        Target, Cfg,
        Export,
        ExternType,
        Suppress,
        Audio, AudioRaw, Mod,
        ChipRam,
        StackSize, Cpu,
        Interrupt, InterruptSafe,
        Atomic, NoInterrupts
    };

    /// <summary>
    /// Check if an attribute name is known/valid
    /// </summary>
    public static bool IsKnown(string name) => All.Contains(name);
}
