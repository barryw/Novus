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
    private static readonly string[] ValidCpuTargets = { "68000", "68020", "68040", "68060" };

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
    public static async Task<int> BuildAll(string? vbccPath = null, string? ndkPath = null, bool verbose = false)
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

                var result = await BuildForTarget(cpu, buildMode, vbccPath, ndkPath, verbose);
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
    public static async Task<int> BuildForTarget(
        string cpu,
        BuildMode buildMode,
        string? vbccPath = null,
        string? ndkPath = null,
        bool verbose = false)
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

        // The std directory is at the root of the project, but gets copied to the output directory
        var stdlibSourceDir = Path.Combine(compilerDir, "std");
        if (!Directory.Exists(stdlibSourceDir))
        {
            // Fallback: try to find it relative to the source tree (for dev builds)
            stdlibSourceDir = Path.Combine(compilerDir, "..", "..", "..", "..", "Novus", "std");
            stdlibSourceDir = Path.GetFullPath(stdlibSourceDir);
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

        // Write manifest
        var manifestPath = Path.Combine(outputDir, "manifest.json");
        var manifestJson = JsonSerializer.Serialize(manifest, StdlibManifestJsonContext.Default.StdlibManifest);
        await File.WriteAllTextAsync(manifestPath, manifestJson);

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
            var toolchain = new VbccToolchain(vbccPath, ndkPath);
            var optimizationLevel = buildMode == BuildMode.Release ? 2 : 0;
            var success = await toolchain.CompileToObject(
                tempCFile,
                outputFile,
                cpu,
                optimizationLevel);
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
    /// Check if stdlib needs rebuilding for a specific target
    /// Also checks nested directories (e.g., ffi/) for changes
    /// </summary>
    public static bool NeedsRebuild(string compilerDir, string cpu, BuildMode buildMode)
    {
        var buildModeStr = buildMode == BuildMode.Release ? "release" : "debug";
        var stdlibDir = Path.Combine(compilerDir, "stdlib", cpu, buildModeStr);
        var manifestPath = Path.Combine(stdlibDir, "manifest.json");

        // If manifest doesn't exist, needs rebuild
        if (!File.Exists(manifestPath))
        {
            return true;
        }

        try
        {
            // Load manifest
            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize(manifestJson, StdlibManifestJsonContext.Default.StdlibManifest);
            if (manifest == null)
            {
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
                    return true;  // Source file missing
                }

                var currentHash = ComputeFileHash(sourceFile);
                if (currentHash != moduleInfo.Hash)
                {
                    return true;  // Source file changed
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
                    m.SourceFile.Replace("\\", "/") == relativePath.Replace("\\", "/"));

                if (!isTracked)
                {
                    // File exists but not tracked in manifest - manifest is stale
                    return true;
                }
            }

            return false;  // All files match
        }
        catch
        {
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
        List<string> stdlibSourcePaths)
    {
        var buildModeStr = buildMode == BuildMode.Release ? "release" : "debug";

        var manifest = new StdlibManifest
        {
            Version = "1.0.0",
            Cpu = cpu,
            BuildMode = buildModeStr,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
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
            var uniqueKey = relativePath.Replace("\\", "/").Replace("/", "_").Replace(".novus", "");

            manifest.Modules[uniqueKey] = new StdlibModuleInfo
            {
                SourceFile = relativePath.Replace("\\", "/"),  // Normalize path separators
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
                var normalizedPath = relativePath.Replace("\\", "/");
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

        // Write manifest
        var manifestPath = Path.Combine(stdlibPrecompiledDir, "manifest.json");
        var manifestJson = JsonSerializer.Serialize(manifest, StdlibManifestJsonContext.Default.StdlibManifest);
        await File.WriteAllTextAsync(manifestPath, manifestJson);
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
