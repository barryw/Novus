using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.Toolchain;

namespace Novus.Commands;

/// <summary>
/// Pre-compiles the standard library to .o files for faster linking.
/// Each function gets its own section for dead code elimination via vlink's -gc-all.
/// </summary>
public static class StdlibBuildCommand
{
    /// <summary>
    /// Valid CPU targets for pre-compilation
    /// </summary>
    private static readonly string[] ValidCpuTargets = { "68020", "68030", "68040", "68060" };

    /// <summary>
    /// Valid build modes
    /// </summary>
    private static readonly string[] ValidBuildModes = { "debug", "release" };

    /// <summary>
    /// Stdlib modules to compile (excluding C files and FFI directory)
    /// </summary>
    private static readonly string[] StdlibModules =
    {
        "amiga_types.novus",
        "collections.novus",
        "core.novus",
        "dos.novus",
        "error.novus",
        "exec.novus",
        "intuition.novus",
        "io.novus",
        "mem.novus",
        "panic.novus",
        "strings.novus",
        "system.novus",
        "tags.novus"
    };

    /// <summary>
    /// Build stdlib for all CPU targets and build modes
    /// </summary>
    /// <param name="codegenVersion">Compiler codegen version for cache invalidation</param>
    public static async Task<int> BuildAll(string? vbccPath = null, string? ndkPath = null, bool verbose = false, int codegenVersion = 0)
    {
        Console.WriteLine("Building standard library for all targets...\n");

        int failureCount = 0;
        int successCount = 0;

        foreach (var cpu in ValidCpuTargets)
        {
            foreach (var modeStr in ValidBuildModes)
            {
                var buildMode = modeStr == "release" ? BuildMode.Release : BuildMode.Debug;
                Console.WriteLine($"Building stdlib for {cpu}/{modeStr}...");

                var result = await BuildForTarget(cpu, buildMode, vbccPath, ndkPath, verbose, codegenVersion);
                if (result == 0)
                {
                    Console.WriteLine($"  ✓ {cpu}/{modeStr} completed\n");
                    successCount++;
                }
                else
                {
                    Console.WriteLine($"  ✗ {cpu}/{modeStr} failed\n");
                    failureCount++;
                }
            }
        }

        Console.WriteLine(new string('═', 60));
        Console.WriteLine($"Stdlib build complete: {successCount} succeeded, {failureCount} failed");
        Console.WriteLine(new string('═', 60));

        return failureCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Build stdlib for a specific CPU target and build mode
    /// </summary>
    /// <param name="codegenVersion">Compiler codegen version for cache invalidation</param>
    public static async Task<int> BuildForTarget(
        string cpu,
        BuildMode buildMode,
        string? vbccPath = null,
        string? ndkPath = null,
        bool verbose = false,
        int codegenVersion = 0)
    {
        // Validate CPU
        if (!ValidCpuTargets.Contains(cpu))
        {
            Console.WriteLine($"Error: Invalid CPU target '{cpu}'");
            Console.WriteLine($"Valid targets: {string.Join(", ", ValidCpuTargets)}");
            return 1;
        }

        // Get paths - prioritize vendored VBCC
        vbccPath ??= Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "vendor", "vbcc");
        ndkPath ??= Environment.GetEnvironmentVariable("NDK")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "amiga-cc", "NDK3.9");

        // Determine compiler location - use AppContext.BaseDirectory for compatibility with single-file deployment
        var compilerDir = AppContext.BaseDirectory;

        // CRITICAL: For dev builds, ALWAYS use the PROJECT source tree, not the copied files in bin/
        // This ensures we get the latest edited sources, not stale cached copies
        var projectStdlibDir = Path.Combine(compilerDir, "..", "..", "..", "..", "Novus", "std");
        projectStdlibDir = Path.GetFullPath(projectStdlibDir);

        string stdlibSourceDir;
        if (Directory.Exists(projectStdlibDir))
        {
            // Dev build - use project source tree
            stdlibSourceDir = projectStdlibDir;
        }
        else
        {
            // Installed/published build - use copied files
            stdlibSourceDir = Path.Combine(compilerDir, "std");
        }

        if (!Directory.Exists(stdlibSourceDir))
        {
            Console.WriteLine($"Error: Stdlib source directory not found: {stdlibSourceDir}");
            return 1;
        }

        // Create output directory: {compilerDir}/stdlib/{cpu}/{mode}/
        var buildModeStr = buildMode == BuildMode.Release ? "release" : "debug";
        var outputDir = Path.Combine(compilerDir, "stdlib", cpu, buildModeStr);
        Directory.CreateDirectory(outputDir);

        if (verbose)
        {
            Console.WriteLine($"  Source: {stdlibSourceDir}");
            Console.WriteLine($"  Output: {outputDir}");
            Console.WriteLine($"  VBCC: {vbccPath}");
            Console.WriteLine($"  NDK: {ndkPath}");
        }

        // NOTE: We can't compile stdlib modules individually because they have circular dependencies
        // (e.g., error.novus → dos.novus, etc.). Instead, we rely on the normal compiler flow
        // which compiles ALL imported modules together, then we just copy the resulting .o files.
        //
        // For now, we'll just create an empty manifest to mark that this target has been "built"
        // The actual .o files will be generated on-demand during normal compilation and cached here.

        var manifest = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = cpu,
            BuildMode = buildModeStr,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = codegenVersion,  // CRITICAL: Track compiler version for cache invalidation
            Modules = new Dictionary<string, StdlibModuleInfo>()
        };

        // For now, just mark stdlib modules in manifest for tracking
        foreach (var moduleName in StdlibModules)
        {
            var sourceFile = Path.Combine(stdlibSourceDir, moduleName);
            if (!File.Exists(sourceFile))
            {
                continue;
            }

            var moduleBaseName = Path.GetFileNameWithoutExtension(moduleName);
            var hash = ComputeFileHash(sourceFile);

            manifest.Modules[moduleBaseName] = new StdlibModuleInfo
            {
                SourceFile = moduleName,
                OutputFile = $"{moduleBaseName}_*.o",  // Wildcard for function-level files
                Hash = hash
            };
        }

        int compiledCount = StdlibModules.Length;

        // CRITICAL FIX: Copy all stdlib source files to the compiler directory
        // This ensures the compiler always has the latest source files for compilation
        // Only copy if source and destination are different directories
        var stdlibDestDir = Path.Combine(compilerDir, "std");
        var normalizedSourceDir = Path.GetFullPath(stdlibSourceDir);
        var normalizedDestDir = Path.GetFullPath(stdlibDestDir);

        if (!normalizedSourceDir.Equals(normalizedDestDir, StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(stdlibDestDir);

            // Copy all .novus files from source to destination
            foreach (var sourceFile in Directory.GetFiles(stdlibSourceDir, "*.novus", SearchOption.TopDirectoryOnly))
            {
                var destFile = Path.Combine(stdlibDestDir, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, destFile, overwrite: true);
            }

            // Copy subdirectories (ffi, graphics, etc.)
            foreach (var sourceSubDir in Directory.GetDirectories(stdlibSourceDir))
            {
                var subDirName = Path.GetFileName(sourceSubDir);
                var destSubDir = Path.Combine(stdlibDestDir, subDirName);
                Directory.CreateDirectory(destSubDir);

                foreach (var sourceFile in Directory.GetFiles(sourceSubDir, "*.novus", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(sourceSubDir, sourceFile);
                    var destFile = Path.Combine(destSubDir, relativePath);
                    var destFileDir = Path.GetDirectoryName(destFile);
                    if (destFileDir != null)
                    {
                        Directory.CreateDirectory(destFileDir);
                    }
                    File.Copy(sourceFile, destFile, overwrite: true);
                }
            }

            if (verbose)
            {
                Console.WriteLine($"  Copied source files to {stdlibDestDir}");
            }
        }
        else if (verbose)
        {
            Console.WriteLine($"  Source and destination are the same, skipping copy");
        }

        // Write manifest atomically to prevent race conditions
        var manifestPath = Path.Combine(outputDir, "manifest.json");
        var manifestJson = JsonSerializer.Serialize(manifest, StdlibManifestJsonContext.Default.StdlibManifest);
        await AtomicCacheWriter.WriteFileAtomicallyAsync(manifestPath, manifestJson);

        if (verbose)
        {
            Console.WriteLine($"  Manifest written to {manifestPath}");
        }

        Console.WriteLine($"  Compiled {compiledCount} modules");

        return 0;
    }

    /// <summary>
    /// Compile a single Novus module to .o file
    /// </summary>
    private static async Task<int> CompileModule(
        string sourceFile,
        string outputFile,
        string cpu,
        BuildMode buildMode,
        string vbccPath,
        string ndkPath,
        bool verbose)
    {
        try
        {
            // Parse the source file
            var sourceText = await File.ReadAllTextAsync(sourceFile);
            var diagnostics = new DiagnosticBag();
            var parser = NovusParserFactory.CreateParser(
                sourceText,
                diagnostics,
                sourceFile,
                NovusParserFactory.ParseMode.Compilation
            );
            var tree = parser.compilationUnit();

            // Check for parse errors
            if (diagnostics.HasErrors)
            {
                Console.WriteLine($"  Parse errors in {sourceFile}");
                return 1;
            }

            // Build IR
            // Use the stdlib path so modules can import each other (e.g., collections imports core)
            var stdlibPath = Path.GetDirectoryName(sourceFile);
            var irBuilder = new IrBuilder(skipAutoImports: false);  // Allow imports within stdlib
            irBuilder.SetStdLibPath(stdlibPath!);
            var module = irBuilder.BuildModule(tree);

            // Generate C code
            var stringLiterals = new List<IrStringLiteral>();  // Collect string literals
            var codegen = new CCodeGenerator(module, stringLiterals, cpu, "soft", buildMode);
            var cCode = codegen.Generate();

            // Write C code to temporary file
            var tempCFile = Path.ChangeExtension(outputFile, ".c");
            await File.WriteAllTextAsync(tempCFile, cCode);

            // Compile C to .o using VBCC
            // Enable FPU for stdlib since it may contain FPU-optimized code paths
            var toolchain = new VbccToolchain(vbccPath, ndkPath);
            var optimizationLevel = buildMode == BuildMode.Release ? 2 : 0;
            var success = await toolchain.CompileToObject(
                tempCFile,
                outputFile,
                cpu,
                optimizationLevel,
                buildMode,
                enableFpu: true);
            var result = success ? 0 : 1;

            // Clean up temporary C file (keep it for debugging if verbose)
            if (!verbose && File.Exists(tempCFile))
            {
                File.Delete(tempCFile);
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Exception compiling {sourceFile}: {ex.Message}");
            if (verbose)
            {
                Console.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    /// <summary>
    /// Compute SHA256 hash of a file for cache invalidation
    /// </summary>
    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Get the path to the project source tree stdlib directory (if in dev build)
    /// Returns null if not in a dev build
    /// </summary>
    public static string? GetProjectSourceTreePath(string compilerDir)
    {
        var projectStdlibDir = Path.Combine(compilerDir, "..", "..", "..", "..", "Novus", "std");
        projectStdlibDir = Path.GetFullPath(projectStdlibDir);
        return Directory.Exists(projectStdlibDir) ? projectStdlibDir : null;
    }

    /// <summary>
    /// Validate that bin/std/ is in sync with project source tree.
    /// Returns list of files that are stale (source is newer than bin copy).
    /// </summary>
    public static List<string> FindStaleSourceCopies(string compilerDir)
    {
        var staleFiles = new List<string>();
        var projectStdlibDir = GetProjectSourceTreePath(compilerDir);

        if (projectStdlibDir == null)
        {
            // Not a dev build - no source tree to compare against
            return staleFiles;
        }

        var binStdlibDir = Path.Combine(compilerDir, "std");
        if (!Directory.Exists(binStdlibDir))
        {
            return staleFiles;
        }

        // Compare all .novus files
        var sourceFiles = Directory.GetFiles(projectStdlibDir, "*.novus", SearchOption.AllDirectories);
        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(projectStdlibDir, sourceFile);
            var binFile = Path.Combine(binStdlibDir, relativePath);

            if (File.Exists(binFile))
            {
                var sourceTime = File.GetLastWriteTimeUtc(sourceFile);
                var binTime = File.GetLastWriteTimeUtc(binFile);

                // Check if source is newer than bin copy
                if (sourceTime > binTime)
                {
                    staleFiles.Add(relativePath);
                }
                else
                {
                    // Also compare hashes to catch same-timestamp edits
                    var sourceHash = ComputeFileHash(sourceFile);
                    var binHash = ComputeFileHash(binFile);
                    if (sourceHash != binHash)
                    {
                        staleFiles.Add(relativePath);
                    }
                }
            }
        }

        return staleFiles;
    }

    /// <summary>
    /// Force refresh of bin/std/ from project source tree.
    /// Called when stale copies are detected.
    /// </summary>
    public static int RefreshBinStdlib(string compilerDir, bool verbose = false)
    {
        var projectStdlibDir = GetProjectSourceTreePath(compilerDir);
        if (projectStdlibDir == null)
        {
            return 0; // Not a dev build
        }

        var binStdlibDir = Path.Combine(compilerDir, "std");
        Directory.CreateDirectory(binStdlibDir);

        using var lockManager = new CacheLockManager(compilerDir);
        using var refreshLock = lockManager.AcquireLockAsync("stdlib-refresh", TimeSpan.FromSeconds(30))
            .GetAwaiter().GetResult()
            ?? throw new TimeoutException("Timed out waiting to refresh the compiler standard library");

        int copiedCount = 0;
        var sourceFiles = Directory.GetFiles(projectStdlibDir, "*.novus", SearchOption.AllDirectories);
        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(projectStdlibDir, sourceFile);
            var binFile = Path.Combine(binStdlibDir, relativePath);

            // Ensure target directory exists
            var targetDir = Path.GetDirectoryName(binFile);
            if (targetDir != null)
            {
                Directory.CreateDirectory(targetDir);
            }

            File.Copy(sourceFile, binFile, overwrite: true);
            copiedCount++;
        }

        if (verbose && copiedCount > 0)
        {
            Console.WriteLine($"  Refreshed {copiedCount} stdlib source files in bin/");
        }

        return copiedCount;
    }

    /// <summary>
    /// Check if stdlib needs rebuilding for a specific target
    /// Also checks nested directories (e.g., ffi/) for changes and compiler version
    /// </summary>
    public static bool NeedsRebuild(string compilerDir, string cpu, BuildMode buildMode, int codegenVersion)
    {
        return NeedsRebuild(compilerDir, cpu, buildMode, codegenVersion, out _);
    }

    /// <summary>
    /// Check if stdlib needs rebuilding for a specific target (with reason)
    /// Also checks nested directories (e.g., ffi/) for changes and compiler version
    /// </summary>
    public static bool NeedsRebuild(string compilerDir, string cpu, BuildMode buildMode, int codegenVersion, out string? reason, string? cacheDir = null)
    {
        reason = null;
        var buildModeStr = buildMode == BuildMode.Release ? "release" : "debug";
        var stdlibDir = cacheDir ?? Path.Combine(compilerDir, "stdlib", cpu, buildModeStr);
        var manifestPath = Path.Combine(stdlibDir, "manifest.json");

        // If manifest doesn't exist, needs rebuild
        if (!File.Exists(manifestPath))
        {
            reason = "manifest.json not found";
            return true;
        }

        try
        {
            // Load manifest
            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize(manifestJson, StdlibManifestJsonContext.Default.StdlibManifest);
            if (manifest == null)
            {
                reason = "manifest.json could not be parsed";
                return true;
            }

            // CRITICAL: Check compiler codegen version
            // If codegen changed, stdlib must be rebuilt even if source files unchanged
            if (manifest.CodegenVersion != codegenVersion)
            {
                reason = $"codegen version changed ({manifest.CodegenVersion} → {codegenVersion})";
                return true;
            }

            // Check if source files have changed
            var stdlibSourceDir = Path.Combine(compilerDir, "std");
            if (!Directory.Exists(stdlibSourceDir))
            {
                // Fallback for dev builds
                stdlibSourceDir = Path.Combine(compilerDir, "..", "..", "..", "..", "Novus", "std");
                stdlibSourceDir = Path.GetFullPath(stdlibSourceDir);
            }

            // Check all .novus files in manifest
            foreach (var (moduleName, moduleInfo) in manifest.Modules)
            {
                var sourceFile = Path.Combine(stdlibSourceDir, moduleInfo.SourceFile);
                if (!File.Exists(sourceFile))
                {
                    reason = $"source file missing: {moduleInfo.SourceFile}";
                    return true;
                }

                var currentHash = ComputeFileHash(sourceFile);
                if (currentHash != moduleInfo.Hash)
                {
                    reason = $"source file changed: {moduleInfo.SourceFile}";
                    return true;
                }
            }

            // CRITICAL FIX: Also check ALL .novus files in subdirectories (ffi/, etc.)
            // These files may be imported but not tracked in the manifest
            var allStdlibFiles = Directory.GetFiles(stdlibSourceDir, "*.novus", SearchOption.AllDirectories);
            foreach (var sourceFile in allStdlibFiles)
            {
                // Get relative path for matching against manifest
                var relativePath = Path.GetRelativePath(stdlibSourceDir, sourceFile);
                var fileName = Path.GetFileName(sourceFile);
                var moduleBaseName = Path.GetFileNameWithoutExtension(fileName);

                // Check if this file is tracked in manifest
                bool isTracked = manifest.Modules.Values.Any(m =>
                    m.SourceFile == fileName ||
                    m.SourceFile == relativePath ||
                    PathUtility.NormalizeForStorage(m.SourceFile) == PathUtility.NormalizeForStorage(relativePath));

                if (!isTracked)
                {
                    // File exists but not tracked in manifest - manifest is stale
                    reason = $"new untracked file: {relativePath}";
                    return true;
                }
            }

            return false;  // All files match
        }
        catch (Exception ex)
        {
            reason = $"error reading manifest: {ex.Message}";
            return true;  // Error reading manifest, rebuild to be safe
        }
    }

    /// <summary>
    /// Write manifest with hashes of stdlib source files for cache invalidation
    /// Includes ALL .novus files in the stdlib directory (including subdirectories like ffi/)
    /// </summary>
    public static async Task WriteManifest(
        string stdlibPrecompiledDir,
        string cpu,
        BuildMode buildMode,
        List<string> stdlibSourcePaths,
        int codegenVersion)
    {
        var buildModeStr = buildMode == BuildMode.Release ? "release" : "debug";

        var manifest = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = cpu,
            BuildMode = buildModeStr,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CodegenVersion = codegenVersion,
            CompilerHash = "", // CodegenVersion is the generated-object compatibility contract.
            Modules = new Dictionary<string, StdlibModuleInfo>()
        };

        // Find the stdlib root directory to compute relative paths
        string? stdlibRootDir = null;
        if (stdlibSourcePaths.Count > 0)
        {
            var firstPath = stdlibSourcePaths[0];
            stdlibRootDir = firstPath.Contains("/std/")
                ? firstPath.Substring(0, firstPath.IndexOf("/std/") + 5)
                : Path.GetDirectoryName(firstPath);
        }

        // Track ALL stdlib source files (including those in subdirectories)
        // This ensures changes to files like ffi/amiga_consts.novus invalidate the cache
        foreach (var modulePath in stdlibSourcePaths)
        {
            // Compute relative path from stdlib root for tracking
            string relativePath;
            if (stdlibRootDir != null && modulePath.StartsWith(stdlibRootDir))
            {
                relativePath = Path.GetRelativePath(stdlibRootDir, modulePath);
            }
            else
            {
                relativePath = Path.GetFileName(modulePath);
            }

            var moduleBaseName = Path.GetFileNameWithoutExtension(modulePath);

            // Compute hash of source file
            var hash = ComputeFileHash(modulePath);

            // Use a unique key that includes path to handle files with same name in different dirs
            var normalizedPath = PathUtility.NormalizeForStorage(relativePath);
            var uniqueKey = normalizedPath.Replace("/", "_").Replace(".novus", "");

            manifest.Modules[uniqueKey] = new StdlibModuleInfo
            {
                SourceFile = normalizedPath,  // Normalize path separators
                OutputFile = $"{moduleBaseName}_*.o",  // Wildcard for function-level files
                Hash = hash
            };
        }

        // CRITICAL FIX: Also scan for ANY additional .novus files in the stdlib directory
        // that weren't explicitly compiled but might be imported
        if (stdlibRootDir != null && Directory.Exists(stdlibRootDir))
        {
            var allStdlibFiles = Directory.GetFiles(stdlibRootDir, "*.novus", SearchOption.AllDirectories);
            foreach (var sourceFile in allStdlibFiles)
            {
                var relativePath = Path.GetRelativePath(stdlibRootDir, sourceFile);
                var normalizedPath = PathUtility.NormalizeForStorage(relativePath);
                var uniqueKey = normalizedPath.Replace("/", "_").Replace(".novus", "");

                // Only add if not already tracked
                if (!manifest.Modules.ContainsKey(uniqueKey))
                {
                    var hash = ComputeFileHash(sourceFile);
                    var moduleBaseName = Path.GetFileNameWithoutExtension(sourceFile);

                    manifest.Modules[uniqueKey] = new StdlibModuleInfo
                    {
                        SourceFile = normalizedPath,
                        OutputFile = $"{moduleBaseName}_*.o",
                        Hash = hash
                    };
                }
            }
        }

        // Write manifest atomically to prevent race conditions
        var manifestPath = Path.Combine(stdlibPrecompiledDir, "manifest.json");
        var manifestJson = JsonSerializer.Serialize(manifest, StdlibManifestJsonContext.Default.StdlibManifest);
        await AtomicCacheWriter.WriteFileAtomicallyAsync(manifestPath, manifestJson);
    }
}

/// <summary>
/// Manifest file tracking stdlib build metadata
/// </summary>
public class StdlibManifest
{
    public string Version { get; set; } = "";
    public string Cpu { get; set; } = "";
    public string BuildMode { get; set; } = "";
    public long Timestamp { get; set; }
    public int CodegenVersion { get; set; }  // Compiler codegen version - invalidates cache on breaking changes
    public string CompilerHash { get; set; } = "";  // Retained for old manifest compatibility.
    public Dictionary<string, StdlibModuleInfo> Modules { get; set; } = new();
}

/// <summary>
/// Information about a compiled stdlib module
/// </summary>
public class StdlibModuleInfo
{
    public string SourceFile { get; set; } = "";
    public string OutputFile { get; set; } = "";
    public string Hash { get; set; } = "";
}

/// <summary>
/// JSON source generator context for AOT compatibility
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StdlibManifest))]
[JsonSerializable(typeof(StdlibModuleInfo))]
[JsonSerializable(typeof(Dictionary<string, StdlibModuleInfo>))]
internal partial class StdlibManifestJsonContext : JsonSerializerContext
{
}
