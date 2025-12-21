using System.Diagnostics;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Collection definition that disables parallelization for compilation tests.
/// Tests in this collection run sequentially to avoid cache corruption.
/// The StdlibCacheFixture ensures stdlib is pre-compiled before tests run.
/// </summary>
[CollectionDefinition("SequentialCompilation", DisableParallelization = true)]
public class SequentialCompilationCollection : ICollectionFixture<StdlibCacheFixture>
{
    // This class has no code, it's just used to define the collection
    // The StdlibCacheFixture will be created once and shared across all tests in this collection
}

/// <summary>
/// Tests that ensure generated code assembles and links successfully
/// These are critical smoke tests to catch codegen regressions
///
/// NOTE: These tests MUST NOT run in parallel with other CompilationTests
/// because they invoke the compiler which writes to shared cache directories.
/// The DisableParallelization attribute prevents xUnit from running these
/// tests concurrently within the collection.
/// </summary>
[Collection("SequentialCompilation")]
public class CompilationTests
{
    [Theory]
    [InlineData("02_arithmetic")]
    [InlineData("04_optimization")]
    [InlineData("12_control_flow")]
    public void ExampleShouldAssembleAndLink(string exampleName)
    {
        var projectRoot = GetProjectRoot();
        var inputFile = Path.Combine(projectRoot, "Novus.Tests", "Examples", $"{exampleName}.novus");
        var outputFile = Path.Combine(Path.GetTempPath(), $"{exampleName}_test");

        // Verify input file exists
        Assert.True(File.Exists(inputFile), $"Input file not found: {inputFile}");

        // Verify VBCC is available
        var vbccDir = Novus.PathUtility.GetVbccPath();
        var vbccPath = Path.Combine(vbccDir, "bin", "vc");
        Assert.True(File.Exists(vbccPath),
            $"VBCC toolchain not found at {vbccPath}. Set VBCC environment variable or install VBCC.");

        // Run the compiler
        var compilerPath = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net9.0", "Novus.dll");
        Assert.True(File.Exists(compilerPath), $"Compiler not found at {compilerPath}. Build the project first.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            // Use --use-stdlib-cache to reuse pre-compiled stdlib objects
            Arguments = $"\"{compilerPath}\" compile \"{inputFile}\" -o \"{outputFile}\" --use-stdlib-cache",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        process.WaitForExit();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        // Check that compilation succeeded
        Assert.True(process.ExitCode == 0,
            $"Compilation failed with exit code {process.ExitCode}.\nOutput:\n{stdout}\nErrors:\n{stderr}");
        Assert.Contains("Successfully created:", stdout);

        // Check that the executable was created
        Assert.True(File.Exists(outputFile),
            $"Executable not created. Output:\n{stdout}\nErrors:\n{stderr}");

        // Clean up
        if (File.Exists(outputFile))
        {
            File.Delete(outputFile);
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
}
