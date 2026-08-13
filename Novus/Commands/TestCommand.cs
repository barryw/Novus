using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;

namespace Novus.Commands;

/// <summary>
/// Handles the 'novus test' command for discovering and running Novus tests.
///
/// This command:
/// 1. Discovers all functions with @test attribute
/// 2. Generates a test runner main() that calls each test
/// 3. Compiles to a 68k executable that can run on Amiga
/// </summary>
public static class TestCommand
{
    /// <summary>
    /// Information about a discovered test function
    /// </summary>
    private record TestInfo(
        string FunctionName,
        string ModulePath,
        string? Description,
        bool IsSkipped,
        string? SkipReason,
        bool ShouldPanic,
        string? ExpectedPanicMessage
    );

    public static async Task<int> Run(TestOptions options)
    {
        try
        {
            if (options.RunWithVamos && options.TimeoutSeconds <= 0)
            {
                Console.WriteLine("Error: --timeout must be greater than zero");
                return 1;
            }

            // Determine the path to test
            string targetPath;

            if (string.IsNullOrEmpty(options.Path))
            {
                targetPath = Directory.GetCurrentDirectory();
            }
            else
            {
                targetPath = Path.GetFullPath(options.Path);
            }

            // Find the compiler's std lib path
            var compilerDir = AppContext.BaseDirectory;
            // Check for stale stdlib source copies and auto-refresh if needed
            var staleFiles = StdlibBuildCommand.FindStaleSourceCopies(compilerDir);
            if (staleFiles.Count > 0)
            {
                if (options.Verbose)
                {
                    Console.WriteLine($"Found {staleFiles.Count} stale stdlib source file(s), refreshing...");
                }
                StdlibBuildCommand.RefreshBinStdlib(compilerDir, options.Verbose);
            }

            // Collect all source files to scan for tests
            var sourceFiles = new List<string>();

            if (File.Exists(targetPath))
            {
                // Single file
                if (targetPath.EndsWith(".novus", StringComparison.OrdinalIgnoreCase))
                {
                    sourceFiles.Add(targetPath);
                }
                else
                {
                    Console.WriteLine($"Error: Not a Novus file: {targetPath}");
                    return 1;
                }
            }
            else if (Directory.Exists(targetPath))
            {
                var projectTests = Path.Combine(targetPath, "tests");
                var isProject = File.Exists(Path.Combine(targetPath, "project.toml")) && Directory.Exists(projectTests);
                var sourceRoot = isProject
                    ? projectTests
                    : targetPath;

                // Directory - find all .novus files
                // Exclude stdlib files EXCEPT for std/tests/ which contains stdlib unit tests
                sourceFiles.AddRange(
                    Directory.EnumerateFiles(sourceRoot, "*.novus",
                            isProject ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories)
                        .Where(f => !f.Contains("/std/") || f.Contains("/std/tests/"))
                );
            }
            else
            {
                Console.WriteLine($"Error: Path not found: {targetPath}");
                return 1;
            }

            if (sourceFiles.Count == 0)
            {
                Console.WriteLine("No Novus source files found");
                return 0;
            }

            sourceFiles.Sort(StringComparer.Ordinal);
            var outputDir = Path.GetFullPath(options.OutputDir ?? Directory.GetCurrentDirectory());
            Directory.CreateDirectory(outputDir);
            var projectDir = FindProjectDirectory(targetPath);
            var supportFiles = projectDir == null
                ? new List<string>()
                : Directory.EnumerateFiles(Path.Combine(projectDir, "src"), "*.novus", SearchOption.AllDirectories).ToList();
            var testRunnerPath = Path.Combine(outputDir, "_test_runner.novus");

            Console.WriteLine($"Scanning {sourceFiles.Count} file(s) for tests...");

            // Discover tests from all source files
            var allTests = new List<TestInfo>();
            foreach (var sourceFile in sourceFiles)
            {
                var tests = await DiscoverTests(sourceFile, options);
                if (tests.Count > 0)
                {
                    allTests.AddRange(tests);
                }
            }

            // Apply filter if specified
            if (!string.IsNullOrEmpty(options.Filter))
            {
                var pattern = options.Filter.Replace("*", ".*");
                var regex = new System.Text.RegularExpressions.Regex(pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                allTests = allTests.Where(t => regex.IsMatch(t.FunctionName)).ToList();
            }

            // Count skipped tests
            var activeTests = allTests.Where(t => !t.IsSkipped).ToList();
            var skippedTests = allTests.Where(t => t.IsSkipped).ToList();

            Console.WriteLine($"\nDiscovered {allTests.Count} test(s):");
            foreach (var test in allTests)
            {
                var status = test.IsSkipped
                    ? (test.SkipReason != null ? $" [SKIP: {test.SkipReason}]" : " [SKIP]")
                    : test.ShouldPanic
                        ? (test.ExpectedPanicMessage != null ? $" [SHOULD_PANIC: \"{test.ExpectedPanicMessage}\"]" : " [SHOULD_PANIC]")
                        : "";
                var desc = test.Description != null ? $" - {test.Description}" : "";
                Console.WriteLine($"  {test.FunctionName}{status}{desc}");
            }

            if (skippedTests.Count > 0)
            {
                Console.WriteLine($"\n  ({skippedTests.Count} test(s) skipped)");
            }

            // List-only mode stops here
            if (options.ListOnly)
            {
                return 0;
            }

            if (activeTests.Count == 0)
            {
                Console.WriteLine("\nNo active tests to run");
                return 0;
            }

            var selectedSourceFiles = allTests
                .Select(test => test.ModulePath)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var compilationSourceFiles = StageSources(selectedSourceFiles, supportFiles, projectDir, outputDir);

            // Generate test runner source (pass all tests - generator handles skipped ones)
            GenerateTestRunner(testRunnerPath, allTests, options);

            Console.WriteLine($"\nGenerated test runner: {testRunnerPath}");

            // Compile the test runner
            // Change to the output directory so imports resolve correctly
            Console.WriteLine("\nCompiling test runner...");

            var outputExe = Path.Combine(outputDir, "tests");
            var originalDir = Directory.GetCurrentDirectory();

            try
            {
                // Change to output directory for compilation
                // This ensures that local imports (e.g., "from test_file import ...") work
                Directory.SetCurrentDirectory(outputDir);

                var result = await Program.RunCompiler(CreateCompilerOptions(options, compilationSourceFiles, outputDir));

                // Update outputExe to absolute path for reporting
                outputExe = Path.Combine(outputDir, "tests");

                if (result != 0)
                {
                    return result;
                }

                Console.WriteLine($"\n===================================");
                Console.WriteLine($"Test runner built successfully!");
                Console.WriteLine($"Output: {outputExe}");
                Console.WriteLine($"Tests: {activeTests.Count} active, {skippedTests.Count} skipped");

                if (!options.RunWithVamos)
                {
                    Console.WriteLine($"\nCopy to Amiga and run to execute tests, or pass --run to use vamos.");
                    Console.WriteLine($"===================================");
                    return 0;
                }

                Console.WriteLine($"===================================");
                Console.WriteLine($"\nRunning with vamos...\n");

                var startInfo = new ProcessStartInfo("vamos")
                {
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add("-C");
                // vamos only accepts concrete CPU names; "auto" is ours, not its.
                // Map it the same way the assembler path does.
                startInfo.ArgumentList.Add(options.Cpu == "auto" ? "68020" : options.Cpu);
                startInfo.ArgumentList.Add("--vols-base-dir");
                startInfo.ArgumentList.Add(Path.Combine(Path.GetTempPath(), "novus-vamos-volumes"));
                startInfo.ArgumentList.Add(outputExe);

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Console.WriteLine("Error: Failed to start vamos");
                    return 1;
                }

                try
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(options.TimeoutSeconds));
                }
                catch (TimeoutException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                    Console.WriteLine($"\nError: Test run timed out after {options.TimeoutSeconds} seconds");
                    return 124;
                }

                return process.ExitCode;
            }
            finally
            {
                // Restore original directory
                Directory.SetCurrentDirectory(originalDir);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (options.Verbose)
            {
                Console.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static CompilerOptions CreateCompilerOptions(
        TestOptions options, IEnumerable<string> sourceFiles, string outputDir) => new()
    {
        InputFile = Path.Combine(outputDir, "_test_runner.novus"),
        OutputFile = Path.Combine(outputDir, "tests"),
        Cpu = options.Cpu,
        Fpu = options.Fpu,
        Release = options.Release,
        BuildMode = options.Release ? BuildMode.Release : BuildMode.Debug,
        VbccPath = options.VbccPath,
        NdkPath = options.NdkPath,
        Verbose = options.Verbose,
        OptimizationLevel = options.GetOptimizationLevel(),
        SafetyLevelOption = options.SafetyLevel,
        UseStdlibCache = true,
        AdditionalSourceFiles = sourceFiles.Select(Path.GetFullPath).ToList()
    };

    private static string? FindProjectDirectory(string targetPath)
    {
        var directory = File.Exists(targetPath) ? Path.GetDirectoryName(targetPath)! : targetPath;
        for (var current = new DirectoryInfo(directory); current != null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "project.toml")) &&
                Directory.Exists(Path.Combine(current.FullName, "src")))
                return current.FullName;
        }

        return null;
    }

    private static List<string> StageSources(
        IEnumerable<string> testFiles,
        IEnumerable<string> supportFiles,
        string? projectDir,
        string outputDir)
    {
        foreach (var source in supportFiles)
        {
            var relative = Path.GetRelativePath(Path.Combine(projectDir!, "src"), source);
            StageSource(source, Path.Combine(outputDir, relative));
        }

        var stagedTests = new List<string>();
        foreach (var source in testFiles)
        {
            var destination = Path.Combine(outputDir, Path.GetFileName(source));
            StageSource(source, destination);
            stagedTests.Add(destination);
        }

        return stagedTests;
    }

    private static void StageSource(string source, string destination)
    {
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!File.Exists(destination) ||
            !File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(destination)))
            File.Copy(source, destination, overwrite: true);
    }

    /// <summary>
    /// Discover all @test functions in a source file
    /// </summary>
    private static async Task<List<TestInfo>> DiscoverTests(string sourceFile, TestOptions options)
    {
        var tests = new List<TestInfo>();

        try
        {
            var source = await File.ReadAllTextAsync(sourceFile);

            var diagnostics = new DiagnosticBag();
            var preprocessingOptions = new CompilerOptions
            {
                Cpu = options.Cpu,
                Fpu = options.Fpu,
                Release = options.Release,
                BuildMode = options.Release ? BuildMode.Release : BuildMode.Debug,
            };
            var preprocessor = new Preprocessing.Preprocessor(
                Program.GetPreprocessorConstants(preprocessingOptions), diagnostics, sourceFile);
            source = preprocessor.Process(source);
            if (diagnostics.HasErrors)
            {
                Console.WriteLine($"  Preprocessor errors in {Path.GetFileName(sourceFile)}:");
                Console.WriteLine(diagnostics.FormatDiagnostics());
                throw new InvalidOperationException($"Cannot discover tests in {sourceFile}");
            }

            // Parse the file
            var parser = NovusParserFactory.CreateParser(
                source,
                diagnostics,
                sourceFile,
                NovusParserFactory.ParseMode.Compilation
            );

            var compilationUnit = parser.compilationUnit();

            if (diagnostics.HasErrors)
            {
                Console.WriteLine($"  Parse errors in {sourceFile}:");
                Console.WriteLine(diagnostics.FormatDiagnostics());
                throw new InvalidOperationException($"Cannot discover tests in {sourceFile}");
            }

            foreach (var function in compilationUnit.functionDeclaration())
            {
                var testAttr = function.attribute()
                    .FirstOrDefault(attribute => attribute.attributeName().GetText() == KnownAttributes.Test);
                if (testAttr == null)
                    continue;

                var positional = new List<string>();
                var named = new Dictionary<string, string>();
                foreach (var argument in testAttr.attributeArgList()?.attributeArg() ?? [])
                {
                    var value = argument.KW_STATIC() != null
                        ? "static"
                        : ParseAttributeValue(argument.expression().GetText());
                    if (argument.IDENTIFIER() == null)
                        positional.Add(value);
                    else
                        named[argument.IDENTIFIER().GetText()] = value;
                }

                // Check for skip parameter: @test(skip) or @test(skip = "reason")
                named.TryGetValue("skip", out var skipReason);
                var isSkipped = skipReason != null || positional.Contains("skip");

                // Check for should_panic parameter: @test(should_panic) or @test(should_panic = "expected message")
                named.TryGetValue("should_panic", out var expectedPanicMessage);
                var shouldPanic = expectedPanicMessage != null || positional.Contains("should_panic");

                // Get description from @test attribute if provided
                // Can be positional: @test("description") or named: @test(description = "...")
                // But don't treat flag identifiers as descriptions
                var positionalDesc = positional.FirstOrDefault();
                if (positionalDesc == "should_panic" || positionalDesc == "skip")
                    positionalDesc = null;
                var description = named.GetValueOrDefault("description") ?? positionalDesc;

                tests.Add(new TestInfo(
                    FunctionName: function.IDENTIFIER().GetText(),
                    ModulePath: sourceFile,
                    Description: description,
                    IsSkipped: isSkipped,
                    SkipReason: skipReason,
                    ShouldPanic: shouldPanic,
                    ExpectedPanicMessage: expectedPanicMessage
                ));
            }

            if (options.Verbose && tests.Count > 0)
            {
                Console.WriteLine($"  Found {tests.Count} test(s) in {Path.GetFileName(sourceFile)}");
            }
        }
        catch (Exception ex)
        {
            if (options.Verbose)
            {
                Console.WriteLine($"  Error scanning {sourceFile}: {ex.Message}");
            }
            throw;
        }

        return tests;
    }

    private static string ParseAttributeValue(string value) =>
        value.StartsWith('"') && value.EndsWith('"')
            ? value[1..^1].Replace("\\n", "\n").Replace("\\t", "\t")
                .Replace("\\\"", "\"").Replace("\\\\", "\\")
            : value;

    /// <summary>
    /// Generate the test runner Novus source file
    /// </summary>
    private static void GenerateTestRunner(
        string outputPath,
        List<TestInfo> tests,
        TestOptions options)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Auto-generated test runner");
        sb.AppendLine("// Generated by: novus test");
        if (options.Benchmark)
        {
            sb.AppendLine("// Benchmark mode: enabled");
        }
        sb.AppendLine();

        // Import standard test module and IO
        sb.AppendLine("from std::io::file import write");
        sb.AppendLine("from std::test::test import __test_begin, __test_get_failures, __test_reset, expect, expect_true, expect_false");
        sb.AppendLine("from std::test::test import expect_eq_i32, expect_eq_u32, expect_eq_i16, expect_eq_u16");
        sb.AppendLine("from std::test::test import expect_eq_i8, expect_eq_u8, expect_eq_bool");
        sb.AppendLine("from std::test::test import expect_null, expect_not_null, fail");

        // Import panic tracking functions for should_panic tests
        var hasShouldPanicTests = tests.Any(t => t.ShouldPanic && !t.IsSkipped);
        if (hasShouldPanicTests)
        {
            sb.AppendLine("from std::test::test import __novus_test_set_mode, __novus_test_reset_panic, __novus_test_did_panic");
        }

        // Import timer for benchmark mode
        if (options.Benchmark)
        {
            sb.AppendLine("from amiga::timer import TimerHandle");
        }
        sb.AppendLine();

        // Test modules are compiled separately; the runner only needs their entry-point signatures.
        var activeTests = tests.Where(t => !t.IsSkipped).ToList();

        foreach (var test in activeTests)
        {
            sb.AppendLine($"extern fn {test.FunctionName}()");
        }
        sb.AppendLine();

        // Generate main function
        sb.AppendLine("pub fn main() -> i32 {");
        sb.AppendLine("    write(\"\\n=== Novus Test Runner ===\\n\\n\")");
        sb.AppendLine();
        sb.AppendLine("    var passed: u32 = 0");
        sb.AppendLine("    var failed: u32 = 0");
        sb.AppendLine("    var skipped: u32 = 0");
        sb.AppendLine("    var total: u32 = 0");

        // Add timer initialization for benchmark mode
        if (options.Benchmark)
        {
            sb.AppendLine("    var total_time_us: u32 = 0");
            sb.AppendLine("    var start_us: u32 = 0");
            sb.AppendLine("    var elapsed_us: u32 = 0");
            sb.AppendLine();
            sb.AppendLine("    // Initialize timer for benchmarking");
            sb.AppendLine("    var timer = match TimerHandle::microhz() {");
            sb.AppendLine("        Result::Ok(t) => t,");
            sb.AppendLine("        Result::Err(_) => {");
            sb.AppendLine("            write(\"ERROR: Failed to initialize timer for benchmarking\\n\")");
            sb.AppendLine("            return 1");
            sb.AppendLine("        },");
            sb.AppendLine("    }");
        }
        sb.AppendLine();

        // Generate test invocations
        foreach (var test in tests)
        {
            var testName = test.FunctionName;
            var displayName = test.Description ?? testName;

            sb.AppendLine($"    // Test: {displayName}");

            if (test.IsSkipped)
            {
                // Skipped test - just print and count
                var skipMsg = test.SkipReason != null
                    ? $"SKIPPED: {test.SkipReason}"
                    : "SKIPPED";
                sb.AppendLine($"    write(\"{displayName}... {skipMsg}\\n\")");
                sb.AppendLine($"    skipped++");
                sb.AppendLine($"    total++");
            }
            else if (test.ShouldPanic)
            {
                // should_panic test - run in test mode and check for panic
                sb.AppendLine($"    write(\"{displayName}... \")");

                // Add timing for benchmark mode
                if (options.Benchmark)
                {
                    sb.AppendLine($"    start_us = timer.get_micros()");
                }

                sb.AppendLine($"    __novus_test_set_mode(1)");
                sb.AppendLine($"    __novus_test_reset_panic()");
                sb.AppendLine($"    {testName}()");
                sb.AppendLine($"    __novus_test_set_mode(0)");

                // Calculate elapsed time for benchmark mode
                if (options.Benchmark)
                {
                    sb.AppendLine($"    elapsed_us = timer.get_micros() - start_us");
                    sb.AppendLine($"    total_time_us = total_time_us + elapsed_us");
                }

                sb.AppendLine($"    if __novus_test_did_panic() != 0 {{");
                if (options.Benchmark)
                {
                    sb.AppendLine($"        write(\"\\x9b1mPASS\\x9b0m (%lu µs)\\n\", elapsed_us)");  // Bold PASS with timing
                }
                else
                {
                    sb.AppendLine($"        write(\"\\x9b1mPASS\\x9b0m\\n\")");  // Bold PASS, then reset
                }
                sb.AppendLine($"        passed++");
                sb.AppendLine($"    }} else {{");
                if (options.Benchmark)
                {
                    sb.AppendLine($"        write(\"\\x9b7mFAIL\\x9b0m (expected panic) (%lu µs)\\n\", elapsed_us)");
                }
                else
                {
                    sb.AppendLine($"        write(\"\\x9b7mFAIL\\x9b0m (expected panic)\\n\")");  // Inverse FAIL, then reset
                }
                sb.AppendLine($"        failed++");
                sb.AppendLine($"    }}");
                sb.AppendLine($"    total++");
            }
            else
            {
                // Normal test - run it and check for failures
                sb.AppendLine($"    write(\"{displayName}... \")");
                sb.AppendLine($"    __test_reset()");
                sb.AppendLine($"    __test_begin(\"{testName}\")");

                // Add timing for benchmark mode
                if (options.Benchmark)
                {
                    sb.AppendLine($"    start_us = timer.get_micros()");
                }

                sb.AppendLine($"    {testName}()");

                // Calculate elapsed time for benchmark mode
                if (options.Benchmark)
                {
                    sb.AppendLine($"    elapsed_us = timer.get_micros() - start_us");
                    sb.AppendLine($"    total_time_us = total_time_us + elapsed_us");
                }

                sb.AppendLine($"    if __test_get_failures() == 0 {{");
                if (options.Benchmark)
                {
                    sb.AppendLine($"        write(\"\\x9b1mPASS\\x9b0m (%lu µs)\\n\", elapsed_us)");  // Bold PASS with timing
                }
                else
                {
                    sb.AppendLine($"        write(\"\\x9b1mPASS\\x9b0m\\n\")");  // Bold PASS, then reset
                }
                sb.AppendLine($"        passed++");
                sb.AppendLine($"    }} else {{");
                if (options.Benchmark)
                {
                    sb.AppendLine($"        write(\"\\x9b7mFAIL\\x9b0m (%lu µs)\\n\", elapsed_us)");  // Inverse FAIL with timing
                }
                else
                {
                    sb.AppendLine($"        write(\"\\x9b7mFAIL\\x9b0m\\n\")");  // Inverse FAIL, then reset
                }
                sb.AppendLine($"        failed++");
                sb.AppendLine($"    }}");
                sb.AppendLine($"    total++");
            }

            // Ctrl-C check disabled for now - was triggering false positives
            // sb.AppendLine($"    // Check for Ctrl-C to allow breaking out of hung tests");
            // sb.AppendLine($"    if should_exit() {{");
            // sb.AppendLine($"        write(\"\\n\\nTests interrupted by Ctrl-C\\n\")");
            // sb.AppendLine($"        clear_break_signals()");
            // sb.AppendLine($"        return 130");  // Standard exit code for SIGINT
            // sb.AppendLine($"    }}");
            sb.AppendLine();
        }

        // Generate summary
        sb.AppendLine("    write(\"\\n=== Results ===\\n\")");
        sb.AppendLine("    write(\"Passed:  %lu\\n\", passed)");
        sb.AppendLine("    write(\"Failed:  %lu\\n\", failed)");
        sb.AppendLine("    write(\"Skipped: %lu\\n\", skipped)");
        sb.AppendLine("    write(\"Total:   %lu\\n\", total)");

        // Add total timing in benchmark mode
        if (options.Benchmark)
        {
            sb.AppendLine();
            sb.AppendLine("    // Display total time with appropriate unit");
            sb.AppendLine("    if total_time_us >= 1000000 {");
            sb.AppendLine("        let secs = total_time_us / 1000000");
            sb.AppendLine("        let ms = (total_time_us % 1000000) / 1000");
            sb.AppendLine("        write(\"Time:    %lu.%03lu s\\n\", secs, ms)");
            sb.AppendLine("    } else if total_time_us >= 1000 {");
            sb.AppendLine("        let ms = total_time_us / 1000");
            sb.AppendLine("        let us = total_time_us % 1000");
            sb.AppendLine("        write(\"Time:    %lu.%03lu ms\\n\", ms, us)");
            sb.AppendLine("    } else {");
            sb.AppendLine("        write(\"Time:    %lu µs\\n\", total_time_us)");
            sb.AppendLine("    }");
        }

        sb.AppendLine();
        sb.AppendLine("    if failed > 0 {");
        sb.AppendLine("        write(\"\\n\\x9b7m*** TESTS FAILED ***\\x9b0m\\n\")");  // Inverse for failed
        sb.AppendLine("        return 1");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    write(\"\\n\\x9b1m*** ALL TESTS PASSED ***\\x9b0m\\n\")");  // Bold for passed
        sb.AppendLine("    return 0");
        sb.AppendLine("}");

        var content = sb.ToString();
        if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != content)
        {
            File.WriteAllText(outputPath, content);
        }
    }
}
