using System;
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
            var stdLibPath = Path.Combine(compilerDir, "std");

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
                // Directory - find all .novus files
                // Exclude stdlib files EXCEPT for std/tests/ which contains stdlib unit tests
                sourceFiles.AddRange(
                    Directory.EnumerateFiles(targetPath, "*.novus", SearchOption.AllDirectories)
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

            Console.WriteLine($"Scanning {sourceFiles.Count} file(s) for tests...");

            // Discover tests from all source files
            var allTests = new List<TestInfo>();
            var sourceFilesWithTests = new List<string>();

            foreach (var sourceFile in sourceFiles)
            {
                var tests = await DiscoverTests(sourceFile, stdLibPath, options.Verbose);
                if (tests.Count > 0)
                {
                    allTests.AddRange(tests);
                    sourceFilesWithTests.Add(sourceFile);
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

            // Determine output directory
            var outputDir = options.OutputDir ?? Path.Combine(targetPath, "tests");
            Directory.CreateDirectory(outputDir);

            // Generate test runner source (pass all tests - generator handles skipped ones)
            var testRunnerPath = Path.Combine(outputDir, "_test_runner.novus");
            GenerateTestRunner(testRunnerPath, sourceFilesWithTests, allTests, options);

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

                var compilerOptions = new CompilerOptions
                {
                    InputFile = Path.GetFileName(testRunnerPath), // Use relative path
                    OutputFile = "tests", // Output in current directory
                    Cpu = options.Cpu,
                    Fpu = options.Fpu,
                    Release = options.Release,
                    BuildMode = options.Release ? BuildMode.Release : BuildMode.Debug,
                    VbccPath = options.VbccPath,
                    NdkPath = options.NdkPath,
                    Verbose = options.Verbose,
                    OptimizationLevel = options.Release ? 2 : 0,
                    SafetyLevelOption = options.SafetyLevel
                };

                var result = await Program.RunCompiler(compilerOptions);

                // Update outputExe to absolute path for reporting
                outputExe = Path.Combine(outputDir, "tests");

                if (result == 0)
                {
                    Console.WriteLine($"\n===================================");
                    Console.WriteLine($"Test runner built successfully!");
                    Console.WriteLine($"Output: {outputExe}");
                    Console.WriteLine($"Tests: {activeTests.Count} active, {skippedTests.Count} skipped");
                    Console.WriteLine($"\nCopy to Amiga and run to execute tests.");
                    Console.WriteLine($"===================================");
                }

                return result;
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

    /// <summary>
    /// Discover all @test functions in a source file
    /// </summary>
    private static async Task<List<TestInfo>> DiscoverTests(string sourceFile, string stdLibPath, bool verbose)
    {
        var tests = new List<TestInfo>();

        try
        {
            var source = await File.ReadAllTextAsync(sourceFile);

            // Parse the file
            var diagnostics = new DiagnosticBag();
            var parser = NovusParserFactory.CreateParser(
                source,
                diagnostics,
                sourceFile,
                NovusParserFactory.ParseMode.Compilation
            );

            var compilationUnit = parser.compilationUnit();

            if (diagnostics.HasErrors)
            {
                if (verbose)
                {
                    Console.WriteLine($"  Parse errors in {sourceFile}:");
                    Console.WriteLine(diagnostics.FormatDiagnostics());
                }
                return tests;
            }

            // Perform semantic analysis to build IR
            var analyzer = new SemanticAnalyzer(sourceFile, source, stdLibPath);
            var analysisSucceeded = analyzer.Analyze(compilationUnit);

            if (!analysisSucceeded)
            {
                // Always show analysis errors when discovering tests
                Console.WriteLine($"  Analysis errors in {Path.GetFileName(sourceFile)}:");
                Console.WriteLine(analyzer.Diagnostics.FormatDiagnostics());
                return tests;
            }

            // Build IR to get function attributes
            var irBuilder = new IrBuilder();
            irBuilder.SetStdLibPath(stdLibPath);
            irBuilder.SetInputFilePath(sourceFile);
            var module = irBuilder.BuildModule(compilationUnit);

            // Find all @test functions
            var testFunctions = module.GetTestFunctions();

            foreach (var func in testFunctions)
            {
                // Get test attribute
                var testAttr = func.Attributes?.Get(KnownAttributes.Test);

                // Check for skip parameter: @test(skip) or @test(skip = "reason")
                var skipReason = testAttr?.GetString("skip");
                var isSkipped = skipReason != null || testAttr?.HasFlag("skip") == true;

                // Check for should_panic parameter: @test(should_panic) or @test(should_panic = "expected message")
                var expectedPanicMessage = testAttr?.GetString("should_panic");
                var shouldPanic = expectedPanicMessage != null || testAttr?.HasFlag("should_panic") == true;

                // Get description from @test attribute if provided
                // Can be positional: @test("description") or named: @test(description = "...")
                // But don't treat flag identifiers as descriptions
                var positionalDesc = testAttr?.GetPositionalArg<string>(0);
                if (positionalDesc == "should_panic" || positionalDesc == "skip")
                    positionalDesc = null;
                var description = testAttr?.GetString("description") ?? positionalDesc;

                tests.Add(new TestInfo(
                    FunctionName: func.Name,
                    ModulePath: sourceFile,
                    Description: description,
                    IsSkipped: isSkipped,
                    SkipReason: skipReason,
                    ShouldPanic: shouldPanic,
                    ExpectedPanicMessage: expectedPanicMessage
                ));
            }

            if (verbose && tests.Count > 0)
            {
                Console.WriteLine($"  Found {tests.Count} test(s) in {Path.GetFileName(sourceFile)}");
            }
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"  Error scanning {sourceFile}: {ex.Message}");
            }
        }

        return tests;
    }

    /// <summary>
    /// Generate the test runner Novus source file
    /// </summary>
    private static void GenerateTestRunner(
        string outputPath,
        List<string> sourceFiles,
        List<TestInfo> tests,
        TestOptions options)
    {
        var sb = new StringBuilder();
        var outputDir = Path.GetDirectoryName(outputPath)!;

        sb.AppendLine("// Auto-generated test runner");
        sb.AppendLine("// Generated by: novus test");
        sb.AppendLine($"// Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
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
        sb.AppendLine();

        // Import all test source files
        // Group tests by source file for proper imports
        // We copy the source files to the output directory so imports work correctly
        // Only import active (non-skipped) test functions since skipped tests aren't called
        var activeTests = tests.Where(t => !t.IsSkipped).ToList();
        var testsByFile = activeTests.GroupBy(t => t.ModulePath).ToList();

        foreach (var group in testsByFile)
        {
            // Copy source file to output directory for compilation
            var sourceFileName = Path.GetFileName(group.Key);
            var destPath = Path.Combine(outputDir, sourceFileName);
            if (group.Key != destPath)
            {
                File.Copy(group.Key, destPath, overwrite: true);
            }

            // Get the module name from file path (just the filename without extension)
            var moduleName = Path.GetFileNameWithoutExtension(group.Key);

            // Import test functions from this module using module name (file must be in same directory)
            var funcNames = string.Join(", ", group.Select(t => t.FunctionName));

            sb.AppendLine($"from {moduleName} import {funcNames}");
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
                sb.AppendLine($"    __novus_test_set_mode(1)");
                sb.AppendLine($"    __novus_test_reset_panic()");
                sb.AppendLine($"    {testName}()");
                sb.AppendLine($"    __novus_test_set_mode(0)");
                sb.AppendLine($"    if __novus_test_did_panic() != 0 {{");
                sb.AppendLine($"        write(\"\\x9b1mPASS\\x9b0m\\n\")");  // Bold PASS, then reset
                sb.AppendLine($"        passed++");
                sb.AppendLine($"    }} else {{");
                sb.AppendLine($"        write(\"\\x9b7mFAIL\\x9b0m (expected panic)\\n\")");  // Inverse FAIL, then reset
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
                sb.AppendLine($"    {testName}()");
                sb.AppendLine($"    if __test_get_failures() == 0 {{");
                sb.AppendLine($"        write(\"\\x9b1mPASS\\x9b0m\\n\")");  // Bold PASS, then reset
                sb.AppendLine($"        passed++");
                sb.AppendLine($"    }} else {{");
                sb.AppendLine($"        write(\"\\x9b7mFAIL\\x9b0m\\n\")");  // Inverse FAIL, then reset
                sb.AppendLine($"        failed++");
                sb.AppendLine($"    }}");
                sb.AppendLine($"    total++");
            }
            sb.AppendLine();
        }

        // Generate summary
        sb.AppendLine("    write(\"\\n=== Results ===\\n\")");
        sb.AppendLine("    write(\"Passed:  %lu\\n\", passed)");
        sb.AppendLine("    write(\"Failed:  %lu\\n\", failed)");
        sb.AppendLine("    write(\"Skipped: %lu\\n\", skipped)");
        sb.AppendLine("    write(\"Total:   %lu\\n\", total)");
        sb.AppendLine();
        sb.AppendLine("    if failed > 0 {");
        sb.AppendLine("        write(\"\\n\\x9b7m*** TESTS FAILED ***\\x9b0m\\n\")");  // Inverse for failed
        sb.AppendLine("        return 1");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    write(\"\\n\\x9b1m*** ALL TESTS PASSED ***\\x9b0m\\n\")");  // Bold for passed
        sb.AppendLine("    return 0");
        sb.AppendLine("}");

        File.WriteAllText(outputPath, sb.ToString());
    }
}
