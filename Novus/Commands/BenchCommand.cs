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
/// Handles the 'novus bench' command for discovering and running Novus benchmarks.
///
/// This command:
/// 1. Discovers all functions with @bench attribute
/// 2. Generates a benchmark runner main() that calls each benchmark
/// 3. Compiles to a 68k executable that can run on Amiga
/// </summary>
public static class BenchCommand
{
    /// <summary>
    /// Information about a discovered benchmark function
    /// </summary>
    private record BenchInfo(
        string FunctionName,
        string ModulePath,
        string? Description,
        int Iterations,      // 0 = auto
        int WarmupIterations // 0 = default (3)
    );

    public static async Task<int> Run(BenchOptions options)
    {
        try
        {
            // Determine the path to benchmark
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

            // Collect all source files to scan for benchmarks
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
                // Exclude stdlib and the bench output directory
                var benchDir = Path.Combine(targetPath, "bench") + Path.DirectorySeparatorChar;
                var allFiles = Directory.EnumerateFiles(targetPath, "*.novus", SearchOption.AllDirectories).ToList();
                if (options.Verbose)
                {
                    Console.WriteLine($"DEBUG: benchDir={benchDir}");
                    foreach (var f in allFiles)
                    {
                        Console.WriteLine($"DEBUG: found {f}");
                    }
                }
                sourceFiles.AddRange(
                    allFiles.Where(f => !f.Contains("/std/") && !f.StartsWith(benchDir))
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

            Console.WriteLine($"Scanning {sourceFiles.Count} file(s) for benchmarks...");

            // Discover benchmarks from all source files
            var allBenchmarks = new List<BenchInfo>();
            var sourceFilesWithBenchmarks = new List<string>();

            foreach (var sourceFile in sourceFiles)
            {
                var benchmarks = await DiscoverBenchmarks(sourceFile, stdLibPath, options.Verbose);
                if (benchmarks.Count > 0)
                {
                    allBenchmarks.AddRange(benchmarks);
                    sourceFilesWithBenchmarks.Add(sourceFile);
                }
            }

            // Apply filter if specified
            if (!string.IsNullOrEmpty(options.Filter))
            {
                var pattern = options.Filter.Replace("*", ".*");
                var regex = new System.Text.RegularExpressions.Regex(pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                allBenchmarks = allBenchmarks.Where(b => regex.IsMatch(b.FunctionName)).ToList();
            }

            Console.WriteLine($"\nDiscovered {allBenchmarks.Count} benchmark(s):");
            foreach (var bench in allBenchmarks)
            {
                var iterInfo = bench.Iterations > 0 ? $" [{bench.Iterations} iterations]" : " [auto]";
                var desc = bench.Description != null ? $" - {bench.Description}" : "";
                Console.WriteLine($"  {bench.FunctionName}{iterInfo}{desc}");
            }

            // List-only mode stops here
            if (options.ListOnly)
            {
                return 0;
            }

            if (allBenchmarks.Count == 0)
            {
                Console.WriteLine("\nNo benchmarks to run");
                return 0;
            }

            // Determine output directory
            var outputDir = options.OutputDir ?? Path.Combine(targetPath, "bench");
            Directory.CreateDirectory(outputDir);

            // Generate benchmark runner source
            var benchRunnerPath = Path.Combine(outputDir, "_bench_runner.novus");
            GenerateBenchRunner(benchRunnerPath, sourceFilesWithBenchmarks, allBenchmarks, options);

            Console.WriteLine($"\nGenerated benchmark runner: {benchRunnerPath}");

            // Compile the benchmark runner
            Console.WriteLine("\nCompiling benchmark runner...");

            var outputExe = Path.Combine(outputDir, "benchmarks");
            var originalDir = Directory.GetCurrentDirectory();

            try
            {
                // Change to output directory for compilation
                Directory.SetCurrentDirectory(outputDir);

                var compilerOptions = new CompilerOptions
                {
                    InputFile = Path.GetFileName(benchRunnerPath),
                    OutputFile = "benchmarks",
                    Cpu = options.Cpu,
                    Fpu = options.Fpu,
                    Release = options.Release,
                    BuildMode = options.Release ? BuildMode.Release : BuildMode.Debug,
                    VbccPath = options.VbccPath,
                    NdkPath = options.NdkPath,
                    Verbose = options.Verbose,
                    OptimizationLevel = options.Release ? 2 : 0
                };

                var result = await Program.RunCompiler(compilerOptions);

                outputExe = Path.Combine(outputDir, "benchmarks");

                if (result == 0)
                {
                    Console.WriteLine($"\n===================================");
                    Console.WriteLine($"Benchmark runner built successfully!");
                    Console.WriteLine($"Output: {outputExe}");
                    Console.WriteLine($"Benchmarks: {allBenchmarks.Count}");
                    if (!options.Release)
                    {
                        Console.WriteLine($"\nWARNING: Built in debug mode. Use --release for accurate timing.");
                    }
                    Console.WriteLine($"\nCopy to Amiga and run to execute benchmarks.");
                    Console.WriteLine($"===================================");
                }

                return result;
            }
            finally
            {
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
    /// Discover all @bench functions in a source file
    /// </summary>
    private static async Task<List<BenchInfo>> DiscoverBenchmarks(string sourceFile, string stdLibPath, bool verbose)
    {
        var benchmarks = new List<BenchInfo>();

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
                return benchmarks;
            }

            // Perform semantic analysis to build IR
            var analyzer = new SemanticAnalyzer(sourceFile, source, stdLibPath);
            var analysisSucceeded = analyzer.Analyze(compilationUnit);

            if (!analysisSucceeded)
            {
                Console.WriteLine($"  Analysis errors in {Path.GetFileName(sourceFile)}:");
                Console.WriteLine(analyzer.Diagnostics.FormatDiagnostics());
                return benchmarks;
            }

            // Build IR to get function attributes
            var irBuilder = new IrBuilder();
            irBuilder.SetStdLibPath(stdLibPath);
            irBuilder.SetInputFilePath(sourceFile);
            var module = irBuilder.BuildModule(compilationUnit);

            // Find all @bench functions
            var benchFunctions = module.GetBenchmarkFunctions();

            foreach (var func in benchFunctions)
            {
                // Get bench attribute (check both @bench and @benchmark)
                var benchAttr = func.Attributes?.Get(KnownAttributes.Bench)
                             ?? func.Attributes?.Get(KnownAttributes.Benchmark);

                // Get iterations parameter: @bench(iterations = 1000)
                var iterations = benchAttr?.GetInt("iterations") ?? 0;

                // Get warmup parameter: @bench(warmup = 10)
                var warmup = benchAttr?.GetInt("warmup") ?? 0;

                // Get description from positional arg or named arg
                var description = benchAttr?.GetString("description")
                               ?? benchAttr?.GetPositionalArg<string>(0);

                benchmarks.Add(new BenchInfo(
                    FunctionName: func.Name,
                    ModulePath: sourceFile,
                    Description: description,
                    Iterations: iterations,
                    WarmupIterations: warmup
                ));
            }

            if (verbose && benchmarks.Count > 0)
            {
                Console.WriteLine($"  Found {benchmarks.Count} benchmark(s) in {Path.GetFileName(sourceFile)}");
            }
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"  Error scanning {sourceFile}: {ex.Message}");
            }
        }

        return benchmarks;
    }

    /// <summary>
    /// Generate the benchmark runner Novus source file
    /// </summary>
    private static void GenerateBenchRunner(
        string outputPath,
        List<string> sourceFiles,
        List<BenchInfo> benchmarks,
        BenchOptions options)
    {
        var sb = new StringBuilder();
        var outputDir = Path.GetDirectoryName(outputPath)!;

        sb.AppendLine("// Auto-generated benchmark runner");
        sb.AppendLine("// Generated by: novus bench");
        sb.AppendLine($"// Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Import benchmark runtime
        sb.AppendLine("from std::io::file import write");
        sb.AppendLine("from std::test::bench import BenchRunner, BenchStats, black_box_i32, black_box_u32");
        sb.AppendLine();

        // Import all benchmark source files
        var benchsByFile = benchmarks.GroupBy(b => b.ModulePath).ToList();

        foreach (var group in benchsByFile)
        {
            // Copy source file to output directory
            var sourceFileName = Path.GetFileName(group.Key);
            var destPath = Path.Combine(outputDir, sourceFileName);
            if (group.Key != destPath)
            {
                File.Copy(group.Key, destPath, overwrite: true);
            }

            // Get the module name
            var moduleName = Path.GetFileNameWithoutExtension(group.Key);

            // Import benchmark functions
            var funcNames = string.Join(", ", group.Select(b => b.FunctionName));
            sb.AppendLine($"from {moduleName} import {funcNames}");
        }
        sb.AppendLine();

        // Generate main function
        sb.AppendLine("pub fn main() -> i32 {");
        sb.AppendLine("    write(\"\\n=== Novus Benchmark Runner ===\\n\\n\")");
        sb.AppendLine();

        // Initialize benchmark runner
        sb.AppendLine("    // Initialize timer");
        sb.AppendLine("    let runner_result = BenchRunner::new()");
        sb.AppendLine("    match runner_result {");
        sb.AppendLine("        Result::Err(_) => {");
        sb.AppendLine("            write(\"Error: Failed to initialize timer for benchmarks\\n\")");
        sb.AppendLine("            return 1");
        sb.AppendLine("        },");  // Comma between match arms
        sb.AppendLine("        Result::Ok(runner) => {");

        // Generate benchmark invocations
        foreach (var bench in benchmarks)
        {
            var benchName = bench.FunctionName;
            var displayName = bench.Description ?? benchName;

            // Use command-line iterations if specified, otherwise per-benchmark or auto
            var iterations = options.Iterations > 0 ? options.Iterations : bench.Iterations;

            sb.AppendLine($"            // Benchmark: {displayName}");
            if (iterations > 0)
            {
                sb.AppendLine($"            let stats_{benchName} = runner.run_fixed({benchName}, {iterations})");
            }
            else
            {
                sb.AppendLine($"            let stats_{benchName} = runner.run_auto({benchName})");
            }
            sb.AppendLine($"            stats_{benchName}.print(\"{displayName}\")");
            sb.AppendLine($"            write(\"\\n\")");
        }

        sb.AppendLine("            write(\"=== Benchmark Complete ===\\n\")");
        sb.AppendLine("            return 0");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        File.WriteAllText(outputPath, sb.ToString());
    }
}
