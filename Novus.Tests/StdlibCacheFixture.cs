using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Fixture that clears and rebuilds the stdlib cache once before any tests run.
/// This ensures all tests use a fresh, consistent stdlib cache.
/// </summary>
public class StdlibCacheFixture : IDisposable
{
    private static readonly object _lock = new();
    private static bool _initialized = false;

    public StdlibCacheFixture()
    {
        lock (_lock)
        {
            if (!_initialized)
            {
                InitializeStdlibCache();
                _initialized = true;
            }
        }
    }

    private void InitializeStdlibCache()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var compilerDir = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net9.0");
        var stdlibCacheDir = Path.Combine(compilerDir, "stdlib");

        // Clear entire stdlib cache directory
        if (Directory.Exists(stdlibCacheDir))
        {
            Console.WriteLine($"[StdlibCacheFixture] Clearing stdlib cache at: {stdlibCacheDir}");
            try
            {
                Directory.Delete(stdlibCacheDir, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StdlibCacheFixture] Warning: Could not delete cache: {ex.Message}");
            }
        }

        // Build stdlib cache by compiling a minimal test file
        // This will populate the cache for the 68020/debug target (default)
        Console.WriteLine("[StdlibCacheFixture] Building fresh stdlib cache...");

        var compilerPath = Path.Combine(compilerDir, "Novus.dll");
        if (!File.Exists(compilerPath))
        {
            Console.WriteLine($"[StdlibCacheFixture] Warning: Compiler not found at {compilerPath}");
            return;
        }

        // Create a minimal test file that imports core stdlib
        var tempDir = Path.Combine(Path.GetTempPath(), "novus_stdlib_init");
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "stdlib_init.novus");
        var outputFile = Path.Combine(tempDir, "stdlib_init");

        File.WriteAllText(testFile, @"
from std::io::core import write

pub fn main() -> i32 {
    return 0
}
");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{compilerPath}\" \"{testFile}\" -o \"{outputFile}\" --use-stdlib-cache",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process != null)
        {
            process.WaitForExit(TimeSpan.FromMinutes(5));

            if (process.ExitCode == 0)
            {
                Console.WriteLine("[StdlibCacheFixture] Stdlib cache built successfully");
            }
            else
            {
                var stderr = process.StandardError.ReadToEnd();
                Console.WriteLine($"[StdlibCacheFixture] Warning: Stdlib build failed: {stderr}");
            }
        }

        // Cleanup temp files
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public void Dispose()
    {
        // Nothing to clean up - cache persists for future test runs
    }
}

/// <summary>
/// Collection definition that uses the stdlib cache fixture.
/// All test classes that need stdlib should use this collection.
/// </summary>
[CollectionDefinition("StdlibCache")]
public class StdlibCacheCollection : ICollectionFixture<StdlibCacheFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition]
}
