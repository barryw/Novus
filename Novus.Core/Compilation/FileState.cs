using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Novus.Compilation;

/// <summary>
/// Tracks the state of a source file for incremental compilation.
/// Includes file metadata, content hash, and dependencies.
/// </summary>
public class FileState
{
    /// <summary>
    /// Absolute path to the source file
    /// </summary>
    [JsonPropertyName("path")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of file contents
    /// </summary>
    [JsonPropertyName("hash")]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    [JsonPropertyName("size")]
    public long FileSize { get; set; }

    /// <summary>
    /// Last modification time (UTC)
    /// </summary>
    [JsonPropertyName("modified")]
    public DateTime LastModified { get; set; }

    /// <summary>
    /// List of absolute paths to files this file depends on (imports)
    /// </summary>
    [JsonPropertyName("dependencies")]
    public HashSet<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Fingerprint of the dependency graph used to compile this file.
    /// </summary>
    [JsonPropertyName("dependency_graph_hash")]
    public string DependencyGraphHash { get; set; } = string.Empty;

    /// <summary>
    /// When this file was last compiled
    /// </summary>
    [JsonPropertyName("compiled_at")]
    public DateTime CompiledAt { get; set; }

    /// <summary>
    /// Compiler version that produced the cached result
    /// </summary>
    [JsonPropertyName("compiler_version")]
    public int CompilerVersion { get; set; }

    /// <summary>
    /// Build configuration fingerprint (CPU, FPU, optimization level, build mode)
    /// </summary>
    [JsonPropertyName("config_hash")]
    public string ConfigHash { get; set; } = string.Empty;

    /// <summary>
    /// Whether this file had compilation errors last time
    /// </summary>
    [JsonPropertyName("had_errors")]
    public bool HadErrors { get; set; }
}
