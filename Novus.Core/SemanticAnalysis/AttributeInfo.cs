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
    public const string Benchmark = "benchmark";
    public const string Ignore = "ignore";

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

    /// <summary>
    /// All known attribute names for validation
    /// </summary>
    public static readonly HashSet<string> All = new()
    {
        Library, LibFunc, LibOpen, LibClose, LibExpunge, LibInit,
        Inline, NoInline, Packed, Align,
        Test, Benchmark, Ignore,
        Deprecated, Since, Experimental,
        Unsafe, ThreadSafe, SingleThreaded,
        Cold, Hot, Const,
        Target, Cfg,
        Export
    };

    /// <summary>
    /// Check if an attribute name is known/valid
    /// </summary>
    public static bool IsKnown(string name) => All.Contains(name);
}
