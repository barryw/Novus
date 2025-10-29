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
    [InlineData("01_hello_world")]
    [InlineData("02_arithmetic")]
    [InlineData("03_variables")]
    [InlineData("vec_amiga_test")]
    public void ExampleShouldAssembleAndLink(string exampleName)
    {
        var projectRoot = GetProjectRoot();
        var inputFile = Path.Combine(projectRoot, "Novus.Tests", "Examples", $"{exampleName}.novus");
        var outputFile = Path.Combine(Path.GetTempPath(), $"{exampleName}_test");

        // Skip if input file doesn't exist
        if (!File.Exists(inputFile))
        {
            return;
        }

        // Run the compiler
        var compilerPath = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net9.0", "Novus.dll");
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
        Assert.Equal(0, process.ExitCode);
        Assert.Contains("Compilation successful", stdout);

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
