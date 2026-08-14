using System.Diagnostics;
using Novus.Compilation;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Base class with shared utilities for example compilation tests.
/// </summary>
public abstract class ExampleCompilationTestBase
{
    // Examples that get full VBCC compilation (representative subset for linking tests)
    protected static readonly HashSet<string> FullCompilationExamples = new(StringComparer.OrdinalIgnoreCase)
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
        "ffi_asl_smoke",            // Generated struct-typed FFI + library lifecycle
        "ffi_callback_smoke",       // Typed NDK callback ABI
        "ffi_ndk_varargs_smoke",    // Native NDK varargs convenience ABI
        "ffi_cia_resource_smoke",   // Caller-supplied A6 resource ABI
        "ffi_device_resource_smoke",// Device/resource lifecycle generation
        "ffi_mathffp_smoke",        // Generated floating-point FFI
        "ffi_ndk_completeness_smoke", // Static amiga.lib plus FD-only hdwrench.library
        "ffi_ndk_contract_layout_smoke", // Device/resource aggregate layouts reach VBCC
        "idiomatic_gui",            // Tier-1 facade must link at non-DCE optimization levels
        "mem_block_demo",           // Memory management
        "operator_overload_test",   // Trait-based operators
        "type_alias_smoke",         // Local and imported transparent aliases
    };

    protected static string GetProjectRoot()
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
    protected static IEnumerable<string> GetAllExampleNames()
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
            // These use the CopperListBuilder pattern which requires amiga::sys::graphics::copper
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
}

/// <summary>
/// Fast ASM-only compilation tests that run IN PARALLEL.
/// These tests use in-process compilation to test parsing/IR/C codegen.
/// Since they don't spawn processes or write to shared caches, they can safely run in parallel.
/// </summary>
public class ExampleAsmTests : ExampleCompilationTestBase
{
    // Shared compiler instance (thread-safe - stateless except for readonly fields)
    private static readonly Lazy<InProcessCompiler> _compiler = new(() =>
    {
        var projectRoot = GetProjectRoot();
        var stdLibPath = Path.Combine(projectRoot, "Novus", "std");
        return new InProcessCompiler(stdLibPath);
    });

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
    /// Fast test: verify parsing, semantic analysis, IR generation, and C codegen.
    /// Uses IN-PROCESS compilation (no external processes) for blazing speed.
    /// These tests run in PARALLEL for maximum performance.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetExampleFilesForAsmTest))]
    public async Task Example_ShouldGenerateAssembly(string exampleName)
    {
        var projectRoot = GetProjectRoot();
        var inputFile = Path.Combine(projectRoot, "Novus.Tests", "Examples", $"{exampleName}.novus");

        // Verify input file exists
        Assert.True(File.Exists(inputFile), $"Input file not found: {inputFile}");

        // Compile in-process (no external process spawning)
        var (success, errorMessage) = await _compiler.Value.CompileAndVerifyAsync(inputFile);

        Assert.True(success,
            $"Example '{exampleName}' failed to generate C code.\n" +
            $"Errors:\n{errorMessage}");
    }

    [Fact]
    public void IdiomaticGuiSurfaces_DoNotUseApplicationLevelInterop()
    {
        var root = GetProjectRoot();
        foreach (var path in new[]
        {
            Path.Combine(root, "Novus.Tests", "Examples", "idiomatic_gui.novus"),
            Path.Combine(root, "templates", "gui", "modern", "src", "main.novus")
        })
        {
            var source = string.Join('\n', File.ReadLines(path)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

            foreach (var forbidden in new[] { "std::ffi", "amiga::sys", "ReAction", "unsafe", "extern", "asm!", "*u8", "*u16", "*u32" })
                Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void IdiomaticGui_UsesOnlyV36IntuitionAndGadToolsCalls()
    {
        var root = GetProjectRoot();
        var ffi = Path.Combine(root, "Novus", "std", "amiga", "raw");
        var gadtools = FfiModuleMetadata.TryRead(Path.Combine(ffi, "gadtools.novus"))!;
        var intuition = FfiModuleMetadata.TryRead(Path.Combine(ffi, "intuition.novus"))!;
        var surfaces = new[]
        {
            Path.Combine(root, "Novus", "std", "amiga", "sys", "gadtools", "builder.novus"),
            Path.Combine(root, "Novus", "std", "amiga", "sys", "gadtools", "menu.novus")
        };

        foreach (var (module, metadata) in new[]
        {
            ("gadtools", gadtools),
            ("intuition", intuition)
        })
        {
            var prefix = $"from amiga::raw::{module} import ";
            var functions = surfaces.SelectMany(path => File.ReadLines(path))
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
                .SelectMany(line => line[prefix.Length..].Split(',', StringSplitOptions.TrimEntries))
                .ToArray();

            Assert.NotEmpty(functions);
            foreach (var function in functions)
                Assert.True(metadata.FunctionVersions.GetValueOrDefault(function) <= 36,
                    $"{module}.{function} requires V{metadata.FunctionVersions.GetValueOrDefault(function)}");
        }
    }
}

/// <summary>
/// Full VBCC compilation tests that run SEQUENTIALLY.
/// These tests use the full VBCC pipeline and share the stdlib cache,
/// so they must run sequentially to avoid race conditions.
/// </summary>
[Collection("SequentialCompilation")]
public class ExampleFullCompilationTests : ExampleCompilationTestBase
{
    [Fact]
    [Trait("Category", "FullCompilation")]
    public async Task IdiomaticGui_ReleaseBinaryFits3072ByteBudget()
    {
        var root = GetProjectRoot();
        var compiler = Path.Combine(root, "Novus", "bin", "Debug", "net10.0", "Novus.dll");
        var source = Path.Combine(root, "Novus.Tests", "Examples", "idiomatic_gui.novus");
        var output = Path.Combine(Path.GetTempPath(), $"novus_gui_size_{Guid.NewGuid():N}");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            compiler, "compile", source, "-o", output, "--cpu", "68020",
            "-O", "3", "--release", "--safety-level", "1",
        })
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(TimeSpan.FromSeconds(300)), "GUI size build timed out");
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            Assert.True(process.ExitCode == 0 && File.Exists(output),
                $"GUI size build failed.\n{stdout}\n{stderr}");
            Assert.True(new FileInfo(output).Length <= 3072,
                $"Idiomatic GUI grew to {new FileInfo(output).Length} bytes (budget: 3072)");
            Assert.False(stdout.Contains("asl.library", StringComparison.Ordinal),
                "Importing amiga::ui must not initialize its unused requester dependency");
        }
        finally
        {
            if (File.Exists(output))
                File.Delete(output);
        }
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
    /// Full compilation test: verify complete pipeline including VBCC assembler/linker.
    /// Runs on a representative subset of examples to catch linking issues.
    /// These tests run SEQUENTIALLY because they share the stdlib cache.
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
        var compilerPath = Path.Combine(projectRoot, "Novus", "bin", "Debug", "net10.0", "Novus.dll");
        Assert.True(File.Exists(compilerPath),
            $"Compiler not found at {compilerPath}. Build the project first.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            // Use --use-stdlib-cache to reuse pre-compiled stdlib objects
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
