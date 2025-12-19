using System.Diagnostics;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Integration tests that ensure ALL example files compile successfully.
/// These tests catch regressions as the grammar and compiler evolve.
///
/// Strategy for fast tests:
/// - Most examples use --emit-asm to test parsing/IR/codegen without VBCC
/// - A representative subset uses full VBCC compilation (marked with FullCompilation trait)
///
/// NOTE: These tests do NOT use --use-stdlib-cache to avoid race conditions.
/// Each test compiles fresh to ensure consistent results. The performance
/// cost is acceptable because:
/// 1. Most tests use --emit-asm which skips VBCC entirely
/// 2. Full compilation tests are a small subset
/// 3. Fresh compilation ensures no stale cache artifacts
///
/// IMPORTANT: This collection runs sequentially (DisableParallelization = true)
/// because the compiler writes to shared cache directories and parallel
/// execution causes race conditions and cache corruption.
/// </summary>
[Collection("SequentialCompilation")]
public class ExampleCompilationTests
{
    // Examples that get full VBCC compilation (representative subset for linking tests)
    private static readonly HashSet<string> FullCompilationExamples = new(StringComparer.OrdinalIgnoreCase)
    {
        "01_hello_world",           // Basic smoke test
        "02_arithmetic",            // Math operations
        "03_variables",             // Variable handling
        "05_functions",             // Function calling conventions
        "07_pointers",              // Pointer operations
        "12_control_flow",          // Control flow
        "15_structs",               // Struct layout
        "19_enums",                 // Enum handling
        "20_generics",              // Generic monomorphization
        "38_amiga_library_calls",   // Amiga FFI
        "mem_block_demo",           // Memory management
        "operator_overload_test",   // Trait-based operators
    };

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
    private static IEnumerable<string> GetAllExampleNames()
    {
        var projectRoot = GetProjectRoot();
        var examplesDir = Path.Combine(projectRoot, "Novus.Tests", "Examples");

        return Directory.GetFiles(examplesDir, "*.novus")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(name => !string.IsNullOrEmpty(name))
            // Exclude error test cases (these are expected to fail)
            .Where(name => !name.Contains("error", StringComparison.OrdinalIgnoreCase)
                        || name == "error_handling_demo") // This one is valid
            // Exclude copper/blitter demos that depend on unimplemented stdlib modules
            // These use the CopperListBuilder pattern which requires std::graphics::copper
            .Where(name => !name.StartsWith("copper_", StringComparison.OrdinalIgnoreCase))
            .Where(name => !name.StartsWith("blitter_", StringComparison.OrdinalIgnoreCase))
            // Exclude examples with known compiler bugs (tracked as TODOs):
            // - WBStartup: C code generation forward declaration issues
            .Where(name => !name.StartsWith("workbench_startup", StringComparison.OrdinalIgnoreCase))
            .Where(name => name != "test_amiga_abi")
            // Exclude test framework examples (these use @test and have no main())
            // They should be run with 'novus test', not 'novus compile'
            .Where(name => !name.StartsWith("test_framework", StringComparison.OrdinalIgnoreCase))
            // Exclude additional @test-only files (these have @test functions and no main())
            .Where(name => name != "test_file_io")
            .Where(name => name != "test_const_fn")
            .Where(name => name != "channel_comprehensive_test")
            .Where(name => name != "extended_assertions_test")
            .Where(name => name != "str_equals_test")
            .OrderBy(name => name);
    }

    /// <summary>
    /// Gets all example files for ASM-only compilation tests (fast)
    /// </summary>
    public static IEnumerable<object[]> GetExampleFilesForAsmTest()
    {
        return GetAllExampleNames()
            .Where(name => !FullCompilationExamples.Contains(name))
            .Select(name => new object[] { name });
    }

    /// <summary>
    /// Gets representative examples for full VBCC compilation tests (slower)
    /// </summary>
    public static IEnumerable<object[]> GetExampleFilesForFullCompilation()
    {
        return GetAllExampleNames()
            .Where(name => FullCompilationExamples.Contains(name))
            .Select(name => new object[] { name });
    }

    /// <summary>
    /// Fast test: verify parsing, semantic analysis, IR generation, and C codegen.
    /// Uses --emit-asm to skip VBCC assembler/linker for speed.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetExampleFilesForAsmTest))]
    public async Task Example_ShouldGenerateAssembly(string exampleName)
    {
        var projectRoot = GetProjectRoot();
        var inputFile = Path.Combine(projectRoot, "Novus.Tests", "Examples", $"{exampleName}.novus");
        var outputFile = Path.Combine(Path.GetTempPath(), $"test_asm_{exampleName}");

        // Verify input file exists
        Assert.True(File.Exists(inputFile), $"Input file not found: {inputFile}");

        // Run the compiler with --emit-asm (skip VBCC)
        var compilerPath = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net9.0", "Novus.dll");
        Assert.True(File.Exists(compilerPath),
            $"Compiler not found at {compilerPath}. Build the project first.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{compilerPath}\" \"{inputFile}\" -o \"{outputFile}\" --emit-asm",
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

        var exited = process.WaitForExit(timeout: TimeSpan.FromSeconds(60));

        // If process didn't exit within timeout, kill it and fail the test
        if (!exited)
        {
            process.Kill();
            Assert.Fail($"Example '{exampleName}' ASM generation timed out after 60 seconds.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        // Check that code generation succeeded
        // With --emit-asm, success is indicated by exit code 0 and "C files and header written" message
        var success = process.ExitCode == 0 && stdout.Contains("C files and header written");

        Assert.True(success,
            $"Example '{exampleName}' failed to generate assembly.\n" +
            $"Exit code: {process.ExitCode}\n" +
            $"Output:\n{stdout}\n" +
            $"Errors:\n{stderr}");
    }

    /// <summary>
    /// Full compilation test: verify complete pipeline including VBCC assembler/linker.
    /// Runs on a representative subset of examples to catch linking issues.
    /// </summary>
    [Theory]
    [Trait("Category", "FullCompilation")]
    [MemberData(nameof(GetExampleFilesForFullCompilation))]
    public async Task Example_ShouldCompileAndLink(string exampleName)
    {
        var projectRoot = GetProjectRoot();
        var inputFile = Path.Combine(projectRoot, "Novus.Tests", "Examples", $"{exampleName}.novus");
        var outputFile = Path.Combine(Path.GetTempPath(), $"test_full_{exampleName}");

        // Verify input file exists
        Assert.True(File.Exists(inputFile), $"Input file not found: {inputFile}");

        // Run the compiler (full compilation)
        var compilerPath = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net9.0", "Novus.dll");
        Assert.True(File.Exists(compilerPath),
            $"Compiler not found at {compilerPath}. Build the project first.");

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

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

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
