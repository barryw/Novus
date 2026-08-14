using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Antlr4.Runtime.Tree;
using Novus.IR;
using Novus.Parser;

namespace Novus.Compilation;

/// <summary>
/// Persistent compilation cache for incremental compilation.
/// Caches parse trees, IR modules, and tracks file dependencies across compiler invocations.
/// </summary>
public partial class CompilationCache
{
    private readonly string _cacheDirectory;
    private readonly int _compilerVersion;
    private readonly ConcurrentDictionary<string, FileState> _fileStates = new();
    private readonly ConcurrentDictionary<string, CachedParseResult> _parseTrees = new();
    private readonly ConcurrentDictionary<string, CachedIrModule> _compiledModules = new();
    private readonly ConcurrentDictionary<(string Path, string ConfigHash), bool> _recompilationMemo = new();
    private bool _memoizeRecompilationChecks;
    private readonly List<Task> _pendingSaves = new();
    private readonly object _pendingSavesLock = new();

    // Statistics
    private int _parseHits = 0;
    private int _parseMisses = 0;
    private int _irHits = 0;
    private int _irMisses = 0;
    private long _validationTicks;
    private long _deserializationTicks;
    private long _irBytesLoaded;

    /// <summary>
    /// Represents a cached parse tree with metadata
    /// </summary>
    private class CachedParseResult
    {
        public IParseTree ParseTree { get; set; } = null!;
        public FileState FileState { get; set; } = null!;
    }

    /// <summary>
    /// Represents a cached IR module with metadata
    /// </summary>
    private class CachedIrModule
    {
        public IrModule Module { get; set; } = null!;
        public List<IrStringLiteral> StringLiterals { get; set; } = new();
        public List<string> ImportedModules { get; set; } = new();
        public FileState FileState { get; set; } = null!;
    }

    /// <summary>
    /// JSON context for AOT-compatible serialization
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(FileState))]
    [JsonSerializable(typeof(Dictionary<string, FileState>))]
    private partial class CacheJsonContext : JsonSerializerContext
    {
    }

    public CompilationCache(string projectRoot, int compilerVersion, string? cacheDirectory = null)
    {
        _compilerVersion = compilerVersion;
        _cacheDirectory = cacheDirectory ?? Path.Combine(projectRoot, ".novus-cache");

        // Create cache directory if it doesn't exist
        Directory.CreateDirectory(_cacheDirectory);

        // Load existing cache state
        LoadCacheState();
    }

    /// <summary>
    /// Compute configuration hash from build settings for cache validation
    /// </summary>
    public static string ComputeConfigHash(string cpu, string fpu, int optimizationLevel, string buildMode)
    {
        var config = $"{cpu}|{fpu}|{optimizationLevel}|{buildMode}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(config));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Check if a file needs recompilation based on:
    /// 1. File modification time/content hash
    /// 2. Compiler version changes
    /// 3. Build configuration changes
    /// 4. Dependency changes (transitive)
    /// </summary>
    public bool NeedsRecompilation(string filePath, string configHash)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!_memoizeRecompilationChecks)
            return NeedsRecompilationCore(fullPath, configHash);

        return _recompilationMemo.GetOrAdd((fullPath, configHash), key =>
            NeedsRecompilationCore(key.Path, key.ConfigHash));
    }

    /// <summary>
    /// Reuse dependency validation results for one compiler invocation.
    /// Source files are treated as an immutable build snapshot after this call.
    /// </summary>
    public void BeginBuild()
    {
        _recompilationMemo.Clear();
        _memoizeRecompilationChecks = true;
    }

    private bool NeedsRecompilationCore(string fullPath, string configHash)
    {

        // File doesn't exist -> can't compile
        if (!File.Exists(fullPath))
        {
            return true;
        }

        // No cached state -> needs compilation
        if (!_fileStates.TryGetValue(fullPath, out var cachedState))
        {
            return true;
        }

        // Check compiler version
        if (cachedState.CompilerVersion != _compilerVersion)
        {
            return true;
        }

        // Check build configuration
        if (cachedState.ConfigHash != configHash)
        {
            return true;
        }

        // Check if file had errors last time (always recompile)
        if (cachedState.HadErrors)
        {
            return true;
        }

        // Fast path: Check timestamp first
        var lastModified = File.GetLastWriteTimeUtc(fullPath);
        if (cachedState.LastModified != lastModified)
        {
            return true;
        }

        // Verify content hash (handles rapid modifications within filesystem time granularity)
        var currentHash = ComputeFileHash(fullPath);
        if (cachedState.ContentHash != currentHash)
        {
            return true;
        }

        // Check dependencies recursively
        foreach (var depPath in cachedState.Dependencies)
        {
            if (NeedsRecompilation(depPath, configHash))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get cached parse tree if still valid
    /// </summary>
    public NovusParser.CompilationUnitContext? GetCachedParseTree(string filePath, string configHash)
    {
        var fullPath = Path.GetFullPath(filePath);

        if (NeedsRecompilation(fullPath, configHash))
        {
            _parseMisses++;
            return null;
        }

        if (_parseTrees.TryGetValue(fullPath, out var cached))
        {
            _parseHits++;
            return cached.ParseTree as NovusParser.CompilationUnitContext;
        }

        _parseMisses++;
        return null;
    }

    /// <summary>
    /// Get cached IR module if still valid
    /// </summary>
    public (IrModule? Module, List<IrStringLiteral>? StringLiterals, List<string>? ImportedModules) GetCachedIrModule(
        string filePath,
        string configHash)
    {
        var fullPath = Path.GetFullPath(filePath);

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var needsRecompilation = NeedsRecompilation(fullPath, configHash);
        Interlocked.Add(ref _validationTicks, System.Diagnostics.Stopwatch.GetTimestamp() - started);
        if (needsRecompilation)
        {
            _irMisses++;
            return (null, null, null);
        }

        if (_compiledModules.TryGetValue(fullPath, out var cached))
        {
            _irHits++;
            return (cached.Module, cached.StringLiterals, cached.ImportedModules);
        }

        try
        {
            using var stream = File.OpenRead(GetIrFilePath(fullPath));
            Interlocked.Add(ref _irBytesLoaded, stream.Length);
            started = System.Diagnostics.Stopwatch.GetTimestamp();
            var persisted = IrCacheSerializer.Deserialize(stream);
            Interlocked.Add(ref _deserializationTicks, System.Diagnostics.Stopwatch.GetTimestamp() - started);
            cached = new CachedIrModule
            {
                Module = persisted.Module,
                StringLiterals = persisted.StringLiterals,
                ImportedModules = persisted.ImportedModules,
                FileState = _fileStates[fullPath]
            };
            _compiledModules[fullPath] = cached;
            _irHits++;
            return (cached.Module, cached.StringLiterals, cached.ImportedModules);
        }
        catch
        {
            // Missing, stale, or corrupt IR is a normal cache miss.
            TryDelete(GetIrFilePath(fullPath));
        }

        _irMisses++;
        return (null, null, null);
    }

    /// <summary>
    /// Cache a successfully compiled module
    /// </summary>
    public void CacheCompilationResult(
        string filePath,
        IParseTree parseTree,
        IrModule module,
        List<IrStringLiteral> stringLiterals,
        List<string> importedModules,
        string configHash,
        bool hadErrors = false,
        IEnumerable<string>? dependencyModules = null)
    {
        var fullPath = Path.GetFullPath(filePath);
        var lastModified = File.GetLastWriteTimeUtc(fullPath);
        var contentHash = ComputeFileHash(fullPath);
        var fileInfo = new FileInfo(fullPath);

        // Normalize dependency paths
        var dependencies = new HashSet<string>();
        foreach (var import in dependencyModules ?? importedModules)
        {
            try
            {
                dependencies.Add(Path.GetFullPath(import));
            }
            catch
            {
                // Skip invalid paths
            }
        }

        var fileState = new FileState
        {
            FilePath = fullPath,
            ContentHash = contentHash,
            FileSize = fileInfo.Length,
            LastModified = lastModified,
            Dependencies = dependencies,
            CompiledAt = DateTime.UtcNow,
            CompilerVersion = _compilerVersion,
            ConfigHash = configHash,
            HadErrors = hadErrors
        };

        // Update in-memory caches
        _fileStates[fullPath] = fileState;

        _parseTrees[fullPath] = new CachedParseResult
        {
            ParseTree = parseTree,
            FileState = fileState
        };

        _compiledModules[fullPath] = new CachedIrModule
        {
            Module = module,
            StringLiterals = stringLiterals,
            ImportedModules = importedModules,
            FileState = fileState
        };
        foreach (var key in _recompilationMemo.Keys.Where(key => key.Path == fullPath))
        {
            _recompilationMemo.TryRemove(key, out _);
        }

        // Snapshot synchronously: lowering mutates the module after this method returns.
        byte[]? persistedIr = null;
        try
        {
            persistedIr = IrCacheSerializer.Serialize(new PersistedIrModule
            {
                Module = module,
                StringLiterals = stringLiterals,
                ImportedModules = importedModules
            });
        }
        catch
        {
            // Unsupported IR remains usable in memory and will compile normally next time.
        }

        // Disk I/O stays asynchronous.
        var saveTask = Task.Run(() =>
        {
            if (persistedIr != null)
            {
                SaveIr(fullPath, persistedIr);
            }
            else
            {
                TryDelete(GetIrFilePath(fullPath));
            }
            SaveFileState(fullPath, fileState);
        });
        lock (_pendingSavesLock)
        {
            // Remove completed tasks to prevent unbounded growth
            _pendingSaves.RemoveAll(t => t.IsCompleted);
            _pendingSaves.Add(saveTask);
        }
    }

    /// <summary>
    /// Wait for all pending save operations to complete.
    /// Useful for testing or ensuring persistence before process exit.
    /// </summary>
    public async Task FlushAsync()
    {
        Task[] tasksToWait;
        lock (_pendingSavesLock)
        {
            tasksToWait = _pendingSaves.ToArray();
        }

        if (tasksToWait.Length > 0)
        {
            await Task.WhenAll(tasksToWait);
        }

        lock (_pendingSavesLock)
        {
            _pendingSaves.RemoveAll(t => t.IsCompleted);
        }
    }

    /// <summary>
    /// Synchronously wait for all pending save operations to complete.
    /// </summary>
    public void Flush()
    {
        FlushAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Invalidate cache for a specific file and all its dependents
    /// </summary>
    public void Invalidate(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);

        // Remove from in-memory caches
        _fileStates.TryRemove(fullPath, out _);
        _parseTrees.TryRemove(fullPath, out _);
        _compiledModules.TryRemove(fullPath, out _);
        _recompilationMemo.Clear();

        // Find and invalidate all files that depend on this file
        var dependents = _fileStates
            .Where(kvp => kvp.Value.Dependencies.Contains(fullPath))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var dependent in dependents)
        {
            Invalidate(dependent); // Recursive invalidation
        }

        // Delete from disk
        TryDelete(GetStateFilePath(fullPath));
        TryDelete(GetIrFilePath(fullPath));
    }

    /// <summary>
    /// Get cache statistics for reporting
    /// </summary>
    public (int ParseHits, int ParseMisses, int IrHits, int IrMisses, int TotalFiles) GetStats()
    {
        return (_parseHits, _parseMisses, _irHits, _irMisses, _fileStates.Count);
    }

    public (TimeSpan ValidationCpuTime, TimeSpan DeserializationCpuTime, long IrBytesLoaded) GetPerformanceStats() =>
        (TimeSpan.FromSeconds((double)Interlocked.Read(ref _validationTicks) / System.Diagnostics.Stopwatch.Frequency),
         TimeSpan.FromSeconds((double)Interlocked.Read(ref _deserializationTicks) / System.Diagnostics.Stopwatch.Frequency),
         Interlocked.Read(ref _irBytesLoaded));

    /// <summary>
    /// Identifies the cached source graph used to produce a linked artifact.
    /// Unlike NeedsRecompilation, this changes when a dependency is refreshed by
    /// another target, so that target cannot leave an older executable "up to date".
    /// </summary>
    public string ComputeSourceGraphHash(IEnumerable<string> roots)
    {
        var visited = new HashSet<string>();
        var parts = new List<string>();

        void Visit(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!visited.Add(fullPath))
                return;

            if (!_fileStates.TryGetValue(fullPath, out var state))
            {
                parts.Add($"{fullPath}|missing");
                return;
            }

            parts.Add($"{fullPath}|{state.ContentHash}");
            foreach (var dependency in state.Dependencies)
                Visit(dependency);
        }

        foreach (var root in roots)
            Visit(root);

        parts.Sort(StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }

    /// <summary>
    /// Clear all caches
    /// </summary>
    public void Clear()
    {
        _fileStates.Clear();
        _parseTrees.Clear();
        _compiledModules.Clear();
        _recompilationMemo.Clear();

        // Delete cache directory
        if (Directory.Exists(_cacheDirectory))
        {
            try
            {
                Directory.Delete(_cacheDirectory, recursive: true);
                Directory.CreateDirectory(_cacheDirectory);
            }
            catch
            {
                // Best effort
            }
        }
    }

    /// <summary>
    /// Compute SHA256 hash of file contents
    /// </summary>
    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Get the path to the state file for a source file
    /// </summary>
    private string GetStateFilePath(string filePath)
    {
        // Use hash of full path to avoid filesystem path length limits
        var pathHash = ComputePathHash(filePath);
        return Path.Combine(_cacheDirectory, $"{pathHash}.state.json");
    }

    private string GetIrFilePath(string filePath) =>
        Path.Combine(_cacheDirectory, $"{ComputePathHash(filePath)}.ir.bin");

    /// <summary>
    /// Compute a short hash of a file path for cache file naming
    /// </summary>
    private static string ComputePathHash(string path)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(path));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Load cache state from disk
    /// </summary>
    private void LoadCacheState()
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            return;
        }

        var stateFiles = Directory.GetFiles(_cacheDirectory, "*.state.json");

        foreach (var stateFile in stateFiles)
        {
            try
            {
                var json = File.ReadAllText(stateFile);
                var state = JsonSerializer.Deserialize(json, CacheJsonContext.Default.FileState);

                if (state != null && File.Exists(state.FilePath))
                {
                    _fileStates[state.FilePath] = state;
                }
                else
                {
                    // Stale cache entry - delete it
                    File.Delete(stateFile);
                }
            }
            catch
            {
                // Corrupted cache file - delete it
                try
                {
                    File.Delete(stateFile);
                }
                catch
                {
                    // Best effort
                }
            }
        }
    }

    /// <summary>
    /// Save file state to disk
    /// </summary>
    private void SaveFileState(string filePath, FileState state)
    {
        try
        {
            var stateFilePath = GetStateFilePath(filePath);
            var json = JsonSerializer.Serialize(state, CacheJsonContext.Default.FileState);
            File.WriteAllText(stateFilePath, json);
        }
        catch
        {
            // Best effort - don't fail compilation if cache write fails
        }
    }

    private void SaveIr(string filePath, byte[] data)
    {
        try
        {
            File.WriteAllBytes(GetIrFilePath(filePath), data);
        }
        catch
        {
            // Best effort - don't fail compilation if cache write fails
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort
        }
    }
}
