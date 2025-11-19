using System.Diagnostics;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Integration tests that ensure ALL example files compile successfully.
/// These tests catch regressions as the grammar and compiler evolve.
/// </summary>
public class ExampleCompilationTests
{
    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Novus.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new Exception("Could not find project root");
    }

    /// <summary>
    /// Gets all .novus files from Examples directory, excluding error test cases
    /// </summary>
    public static IEnumerable<object[]> GetExampleFiles()
    {
        var projectRoot = GetProjectRoot();
        var examplesDir = Path.Combine(projectRoot, "Novus.Tests", "Examples");
        
        var allFiles = Directory.GetFiles(examplesDir, "*.novus")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(name => !string.IsNullOrEmpty(name))
            // Exclude error test cases (these are expected to fail)
            .Where(name => !name.Contains("error", StringComparison.OrdinalIgnoreCase) 
                        || name == "error_handling_demo") // This one is valid
            .OrderBy(name => name)
            .Select(name => new object[] { name });

        return allFiles;
    }

    [Theory]
    [MemberData(nameof(GetExampleFiles))]
    public void Example_ShouldCompileSuccessfully(string exampleName)
    {
        var projectRoot = GetProjectRoot();
        var inputFile = Path.Combine(projectRoot, "Novus.Tests", "Examples", $"{exampleName}.novus");
        var outputFile = Path.Combine(Path.GetTempPath(), $"test_{exampleName}");

        // Verify input file exists
        Assert.True(File.Exists(inputFile), $"Input file not found: {inputFile}");

        // Run the compiler
        var compilerPath = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net9.0", "Novus.dll");
        Assert.True(File.Exists(compilerPath), 
            $"Compiler not found at {compilerPath}. Build the project first.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{compilerPath}\" \"{inputFile}\" -o \"{outputFile}\" --use-stdlib-cache",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        // Read output asynchronously to avoid deadlock when buffer fills
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        var exited = process.WaitForExit(timeout: TimeSpan.FromSeconds(300));

        // If process didn't exit within timeout, kill it and fail the test
        if (!exited)
        {
            process.Kill();
            Assert.Fail($"Example '{exampleName}' compilation timed out after 300 seconds.");
        }

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;

        // Check that compilation succeeded
        var success = process.ExitCode == 0 && stdout.Contains("Successfully created:");

        Assert.True(success,
            $"Example '{exampleName}' failed to compile.\n" +
            $"Exit code: {process.ExitCode}\n" +
            $"Output:\n{stdout}\n" +
            $"Errors:\n{stderr}");

        // Verify the executable was created
        Assert.True(File.Exists(outputFile),
            $"Executable not created for '{exampleName}'.\n" +
            $"Output:\n{stdout}");

        // Clean up
        try
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
