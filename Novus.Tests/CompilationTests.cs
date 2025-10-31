using System.Diagnostics;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests that ensure generated code assembles and links successfully
/// These are critical smoke tests to catch codegen regressions
/// </summary>
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
        var vbccPath = Environment.GetEnvironmentVariable("VBCC_PATH")
                      ?? "/Users/barry/amiga-cc/vbcc/bin/vc";
        Assert.True(File.Exists(vbccPath),
            $"VBCC toolchain not found at {vbccPath}. Set VBCC_PATH environment variable or install VBCC.");

        // Run the compiler
        var compilerPath = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net9.0", "Novus.dll");
        Assert.True(File.Exists(compilerPath), $"Compiler not found at {compilerPath}. Build the project first.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{compilerPath}\" \"{inputFile}\" -o \"{outputFile}\"",
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
