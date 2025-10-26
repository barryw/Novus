using Antlr4.Runtime;
using CommandLine;
using Novus.Codegen;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Novus.Toolchain;
using Novus.Tools;

namespace Novus;

class Program
{
    static async Task<int> Main(string[] args)
    {
        return await CommandLine.Parser.Default.ParseArguments<CompilerOptions, GenerateStubsOptions>(args)
            .MapResult(
                (CompilerOptions options) => RunCompiler(options),
                (GenerateStubsOptions options) => Task.FromResult(RunGenerateStubs(options)),
                errors => Task.FromResult(1)
            );
    }

    static int RunGenerateStubs(GenerateStubsOptions options)
    {
        var generator = new NdkStubGenerator(options.NdkPath, options.OutputPath);

        if (options.ListLibraries)
        {
            generator.ListAvailableLibraries();
            return 0;
        }

        if (options.GenerateAll)
        {
            generator.GenerateCommonLibraries();
            return 0;
        }

        if (!string.IsNullOrEmpty(options.Library))
        {
            generator.GenerateLibraryStubs(options.Library);
            return 0;
        }

        Console.WriteLine("Error: Specify --library, --all, or --list");
        Console.WriteLine("Use --help for usage information");
        return 1;
    }

    static async Task<int> RunCompiler(CompilerOptions options)
    {
        Console.WriteLine("Novus Compiler - Proof of Concept");
        Console.WriteLine($"Target: {options.Cpu.ToUpper()}");
        Console.WriteLine($"FPU Mode: {options.Fpu}");
        Console.WriteLine("==================================\n");

        try
        {
            // Read source file
            if (!File.Exists(options.InputFile))
            {
                Console.WriteLine($"Error: File not found: {options.InputFile}");
                return 1;
            }

            if (options.Verbose)
            {
                Console.WriteLine($"Input: {options.InputFile}");
                Console.WriteLine($"Output: {options.OutputFile}");
                Console.WriteLine($"CPU Target: {options.Cpu}");
                Console.WriteLine();
            }

            Console.WriteLine($"Compiling: {options.InputFile}");
            var source = await File.ReadAllTextAsync(options.InputFile);

            // Lex and parse
            Console.WriteLine("Parsing...");
            var inputStream = new AntlrInputStream(source);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new NovusParser(tokenStream);

            // Parse the compilation unit
            var parseTree = parser.compilationUnit();

            // Check for parse errors
            if (parser.NumberOfSyntaxErrors > 0)
            {
                Console.WriteLine($"Parse failed with {parser.NumberOfSyntaxErrors} error(s)");
                return 1;
            }

            // Perform semantic analysis
            Console.WriteLine("Analyzing semantics...");

            // Find standard library path - it's in std/ relative to the compiler
            var compilerDir = AppContext.BaseDirectory;
            var stdLibPath = Path.Combine(compilerDir, "std");

            var analyzer = new SemanticAnalyzer(options.InputFile, source, stdLibPath);
            var analysisSucceeded = analyzer.Analyze(parseTree);

            // Report diagnostics
            if (analyzer.Diagnostics.HasErrors || analyzer.Diagnostics.HasWarnings)
            {
                Console.WriteLine();
                Console.WriteLine(analyzer.Diagnostics.FormatDiagnostics());
            }

            // Stop if there were errors
            if (!analysisSucceeded)
            {
                return 1;
            }

            // Build IR
            Console.WriteLine("Building IR...");
            var irBuilder = new IrBuilder();
            irBuilder.SetStdLibPath(stdLibPath);
            var module = irBuilder.BuildModule(parseTree);

            Console.WriteLine($"  Functions: {module.Functions.Count}");

            // Optionally emit IR
            if (options.EmitIr)
            {
                Console.WriteLine("\n=== IR Dump (Before Optimization) ===");
                // TODO: Implement IR dumper
                Console.WriteLine("(IR dump not yet implemented)");
                Console.WriteLine();
            }

            // Run optimization passes
            if (options.OptimizationLevel > 0)
            {
                Console.WriteLine($"Running optimizations (level {options.OptimizationLevel})...");
                var optimizer = Novus.Optimizer.OptimizationPipeline.CreatePipeline(
                    options.OptimizationLevel,
                    options.Verbose
                );
                optimizer.Run(module);
            }

            // Generate 68k assembly
            Console.WriteLine("Generating 68k assembly...");
            var codegen = new M68kCodeGenerator(module, irBuilder.StringLiterals, options.Cpu, options.Fpu);
            var assembly = codegen.Generate();

            if (options.EmitAsmOnly)
            {
                // Just output the assembly
                var asmFile = Path.ChangeExtension(options.OutputFile, ".s");
                await File.WriteAllTextAsync(asmFile, assembly);
                Console.WriteLine($"Assembly written to: {asmFile}");
                return 0;
            }

            // Assemble and link with VBCC
            Console.WriteLine("Assembling and linking...");
            var toolchain = new VbccToolchain(options.VbccPath, options.NdkPath);
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(options.OutputFile)) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(options.OutputFile);

            // Enable FPU instructions in assembler if using fat binary or explicit FPU mode
            bool enableFpu = options.Fpu != "soft";

            var success = await toolchain.CompileToExecutable(
                assembly,
                outputDir,
                baseName,
                options.Cpu,
                enableFpu,
                options.Fpu
            );

            if (success)
            {
                Console.WriteLine("\n✓ Compilation successful!");
                return 0;
            }
            else
            {
                Console.WriteLine("\n✗ Compilation failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}