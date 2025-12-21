using System.Diagnostics;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Collection fixture that rebuilds stdlib cache once before all tests run.
/// All tests that use the "StdlibCache" collection will share this fixture.
/// </summary>
public class StdlibCacheFixture : IDisposable
{
    public StdlibCacheFixture()
    {
        Console.WriteLine("=== Rebuilding stdlib cache for test run ===");
        RebuildStdlibCache();
        Console.WriteLine("=== Stdlib cache ready for tests ===\n");
    }

    private void RebuildStdlibCache()
    {
        var projectRoot = GetProjectRoot();
        var novusExe = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net9.0", "Novus.dll");

        if (!File.Exists(novusExe))
        {
            Console.WriteLine($"Warning: Novus compiler not found at {novusExe}");
            return;
        }

        // Use stdlib-build command to rebuild for common test targets
        // Most tests use debug mode with 68020 target
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{novusExe}\" stdlib-build --cpu 68020 --mode debug",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.WriteLine("Failed to start stdlib-build process");
                return;
            }

            process.WaitForExit(timeout: TimeSpan.FromMinutes(5));

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("  ✓ Stdlib cache rebuilt successfully");
            }
            else
            {
                Console.WriteLine($"  ✗ Stdlib rebuild failed with exit code {process.ExitCode}");
                if (!string.IsNullOrEmpty(output))
                    Console.WriteLine($"  Output: {output}");
                if (!string.IsNullOrEmpty(error))
                    Console.WriteLine($"  Error: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Exception during stdlib rebuild: {ex.Message}");
        }
    }

    private static string GetProjectRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(directory, "Novus.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
            if (directory == null)
            {
                throw new InvalidOperationException("Could not find project root");
            }
        }
        return directory;
    }

    public void Dispose()
    {
        // Cleanup if needed
        GC.SuppressFinalize(this);
    }
}

// Note: The StdlibCacheFixture is used by SequentialCompilationCollection in CompilationTests.cs
// This provides stdlib caching for all compilation tests that use [Collection("SequentialCompilation")]
