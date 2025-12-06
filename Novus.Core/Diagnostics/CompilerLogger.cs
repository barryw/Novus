using System.Text;

namespace Novus.Diagnostics;

/// <summary>
/// Log levels for compiler output.
/// </summary>
public enum LogLevel
{
    /// <summary>Suppress all output except errors</summary>
    Quiet = 0,

    /// <summary>Normal compilation output (progress, summaries)</summary>
    Normal = 1,

    /// <summary>Verbose output including detailed progress</summary>
    Verbose = 2,

    /// <summary>Debug output including internal compiler state</summary>
    Debug = 3
}

/// <summary>
/// Log categories for filtering compiler output.
/// </summary>
[Flags]
public enum LogCategory
{
    None = 0,

    /// <summary>File I/O operations (reading, writing, caching)</summary>
    FileIO = 1 << 0,

    /// <summary>Parsing progress and details</summary>
    Parsing = 1 << 1,

    /// <summary>Semantic analysis messages</summary>
    Semantic = 1 << 2,

    /// <summary>IR generation and manipulation</summary>
    IR = 1 << 3,

    /// <summary>Code generation (C99, ASM)</summary>
    CodeGen = 1 << 4,

    /// <summary>Optimization passes</summary>
    Optimization = 1 << 5,

    /// <summary>Toolchain invocation (VBCC, vasm, vlink)</summary>
    Toolchain = 1 << 6,

    /// <summary>Build progress and summaries</summary>
    Build = 1 << 7,

    /// <summary>Library detection and linking</summary>
    Linking = 1 << 8,

    /// <summary>Generic instantiation and monomorphization</summary>
    Generics = 1 << 9,

    /// <summary>Type checking and inference</summary>
    TypeSystem = 1 << 10,

    /// <summary>Cache hit/miss information</summary>
    Caching = 1 << 11,

    /// <summary>All categories</summary>
    All = ~0
}

/// <summary>
/// Centralized logging infrastructure for the Novus compiler.
///
/// Provides structured logging with configurable verbosity levels and categories.
/// Replaces ad-hoc Console.WriteLine calls throughout the codebase.
///
/// Usage:
///   CompilerLogger.Info(LogCategory.Build, "Compiling {0}", fileName);
///   CompilerLogger.Debug(LogCategory.IR, "Function {0} has {1} basic blocks", funcName, blockCount);
///   CompilerLogger.Verbose(LogCategory.Toolchain, "Running: vlink {0}", args);
/// </summary>
public static class CompilerLogger
{
    private static LogLevel _currentLevel = LogLevel.Normal;
    private static LogCategory _enabledCategories = LogCategory.All;
    private static TextWriter _output = Console.Out;
    private static TextWriter _errorOutput = Console.Error;
    private static bool _useColor = true;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets or sets the current log level.
    /// Messages with a level higher than this will be suppressed.
    /// </summary>
    public static LogLevel Level
    {
        get => _currentLevel;
        set => _currentLevel = value;
    }

    /// <summary>
    /// Gets or sets which categories are enabled for logging.
    /// Messages in disabled categories will be suppressed.
    /// </summary>
    public static LogCategory EnabledCategories
    {
        get => _enabledCategories;
        set => _enabledCategories = value;
    }

    /// <summary>
    /// Gets or sets whether to use ANSI color codes in output.
    /// </summary>
    public static bool UseColor
    {
        get => _useColor;
        set => _useColor = value;
    }

    /// <summary>
    /// Configure the logger with the specified settings.
    /// </summary>
    public static void Configure(LogLevel level, LogCategory categories = LogCategory.All, bool useColor = true)
    {
        _currentLevel = level;
        _enabledCategories = categories;
        _useColor = useColor;
    }

    /// <summary>
    /// Redirect output to a different writer (useful for testing or capturing output).
    /// </summary>
    public static void SetOutput(TextWriter output, TextWriter? errorOutput = null)
    {
        _output = output;
        _errorOutput = errorOutput ?? output;
    }

    /// <summary>
    /// Reset to default configuration.
    /// </summary>
    public static void Reset()
    {
        _currentLevel = LogLevel.Normal;
        _enabledCategories = LogCategory.All;
        _output = Console.Out;
        _errorOutput = Console.Error;
        _useColor = true;
    }

    /// <summary>
    /// Check if a message at the given level and category would be logged.
    /// </summary>
    public static bool IsEnabled(LogLevel level, LogCategory category)
    {
        return level <= _currentLevel && (_enabledCategories & category) != 0;
    }

    // ========================================================================
    // Core logging methods
    // ========================================================================

    /// <summary>
    /// Log a message at the specified level and category.
    /// </summary>
    public static void Log(LogLevel level, LogCategory category, string message)
    {
        if (!IsEnabled(level, category))
            return;

        lock (_lock)
        {
            _output.WriteLine(message);
        }
    }

    /// <summary>
    /// Log a formatted message at the specified level and category.
    /// </summary>
    public static void Log(LogLevel level, LogCategory category, string format, params object?[] args)
    {
        if (!IsEnabled(level, category))
            return;

        lock (_lock)
        {
            _output.WriteLine(format, args);
        }
    }

    // ========================================================================
    // Normal level (default) - Progress and summaries
    // ========================================================================

    /// <summary>
    /// Log an informational message (Normal level).
    /// </summary>
    public static void Info(LogCategory category, string message)
    {
        Log(LogLevel.Normal, category, message);
    }

    /// <summary>
    /// Log a formatted informational message (Normal level).
    /// </summary>
    public static void Info(LogCategory category, string format, params object?[] args)
    {
        Log(LogLevel.Normal, category, format, args);
    }

    /// <summary>
    /// Log a success message with checkmark (Normal level).
    /// </summary>
    public static void Success(LogCategory category, string message)
    {
        if (!IsEnabled(LogLevel.Normal, category))
            return;

        lock (_lock)
        {
            if (_useColor)
                _output.WriteLine($"\u2713 {message}");
            else
                _output.WriteLine($"OK: {message}");
        }
    }

    /// <summary>
    /// Log a warning message (Normal level, always shown unless Quiet).
    /// </summary>
    public static void Warning(LogCategory category, string message)
    {
        if (_currentLevel == LogLevel.Quiet)
            return;

        lock (_lock)
        {
            if (_useColor)
                _errorOutput.WriteLine($"\u26a0 {message}");
            else
                _errorOutput.WriteLine($"WARNING: {message}");
        }
    }

    /// <summary>
    /// Log a warning message with formatting (Normal level).
    /// </summary>
    public static void Warning(LogCategory category, string format, params object?[] args)
    {
        if (_currentLevel == LogLevel.Quiet)
            return;

        lock (_lock)
        {
            var message = string.Format(format, args);
            if (_useColor)
                _errorOutput.WriteLine($"\u26a0 {message}");
            else
                _errorOutput.WriteLine($"WARNING: {message}");
        }
    }

    /// <summary>
    /// Log an error message (always shown, even in Quiet mode).
    /// </summary>
    public static void Error(string message)
    {
        lock (_lock)
        {
            if (_useColor)
                _errorOutput.WriteLine($"\u2717 {message}");
            else
                _errorOutput.WriteLine($"ERROR: {message}");
        }
    }

    /// <summary>
    /// Log a formatted error message (always shown).
    /// </summary>
    public static void Error(string format, params object?[] args)
    {
        lock (_lock)
        {
            var message = string.Format(format, args);
            if (_useColor)
                _errorOutput.WriteLine($"\u2717 {message}");
            else
                _errorOutput.WriteLine($"ERROR: {message}");
        }
    }

    // ========================================================================
    // Verbose level - Detailed progress
    // ========================================================================

    /// <summary>
    /// Log a verbose message (Verbose level).
    /// </summary>
    public static void Verbose(LogCategory category, string message)
    {
        Log(LogLevel.Verbose, category, message);
    }

    /// <summary>
    /// Log a formatted verbose message (Verbose level).
    /// </summary>
    public static void Verbose(LogCategory category, string format, params object?[] args)
    {
        Log(LogLevel.Verbose, category, format, args);
    }

    // ========================================================================
    // Debug level - Internal compiler state
    // ========================================================================

    /// <summary>
    /// Log a debug message (Debug level).
    /// </summary>
    public static void Debug(LogCategory category, string message)
    {
        if (!IsEnabled(LogLevel.Debug, category))
            return;

        lock (_lock)
        {
            _output.WriteLine($"[DEBUG:{category}] {message}");
        }
    }

    /// <summary>
    /// Log a formatted debug message (Debug level).
    /// </summary>
    public static void Debug(LogCategory category, string format, params object?[] args)
    {
        if (!IsEnabled(LogLevel.Debug, category))
            return;

        lock (_lock)
        {
            var message = string.Format(format, args);
            _output.WriteLine($"[DEBUG:{category}] {message}");
        }
    }

    // ========================================================================
    // Progress indicators
    // ========================================================================

    /// <summary>
    /// Log a progress step with arrow prefix.
    /// </summary>
    public static void Step(LogCategory category, string message)
    {
        if (!IsEnabled(LogLevel.Normal, category))
            return;

        lock (_lock)
        {
            _output.WriteLine($"  \u2192 {message}");
        }
    }

    /// <summary>
    /// Log a formatted progress step.
    /// </summary>
    public static void Step(LogCategory category, string format, params object?[] args)
    {
        if (!IsEnabled(LogLevel.Normal, category))
            return;

        lock (_lock)
        {
            var message = string.Format(format, args);
            _output.WriteLine($"  \u2192 {message}");
        }
    }

    /// <summary>
    /// Log a sub-step (indented further).
    /// </summary>
    public static void SubStep(LogCategory category, string message)
    {
        if (!IsEnabled(LogLevel.Verbose, category))
            return;

        lock (_lock)
        {
            _output.WriteLine($"    - {message}");
        }
    }

    // ========================================================================
    // Section headers
    // ========================================================================

    /// <summary>
    /// Log a section header.
    /// </summary>
    public static void Section(LogCategory category, string title)
    {
        if (!IsEnabled(LogLevel.Normal, category))
            return;

        lock (_lock)
        {
            _output.WriteLine();
            _output.WriteLine($"=== {title} ===");
        }
    }

    /// <summary>
    /// Log a debug section header (only shown in Debug level).
    /// </summary>
    public static void DebugSection(LogCategory category, string title)
    {
        if (!IsEnabled(LogLevel.Debug, category))
            return;

        lock (_lock)
        {
            _output.WriteLine();
            _output.WriteLine($"=== DEBUG: {title} ===");
        }
    }

    // ========================================================================
    // Blank lines and separators
    // ========================================================================

    /// <summary>
    /// Write a blank line (respects log level).
    /// </summary>
    public static void BlankLine(LogLevel level = LogLevel.Normal)
    {
        if (level > _currentLevel)
            return;

        lock (_lock)
        {
            _output.WriteLine();
        }
    }

    // ========================================================================
    // Scoped timing
    // ========================================================================

    /// <summary>
    /// Start a timed operation and return a disposable that logs completion.
    /// </summary>
    public static TimedOperation BeginTimed(LogCategory category, string operationName)
    {
        return new TimedOperation(category, operationName);
    }

    public sealed class TimedOperation : IDisposable
    {
        private readonly LogCategory _category;
        private readonly string _operationName;
        private readonly System.Diagnostics.Stopwatch _stopwatch;
        private bool _disposed;

        internal TimedOperation(LogCategory category, string operationName)
        {
            _category = category;
            _operationName = operationName;
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Verbose(_category, "{0}...", _operationName);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stopwatch.Stop();
            Debug(_category, "{0} completed in {1}ms", _operationName, _stopwatch.ElapsedMilliseconds);
        }
    }
}
