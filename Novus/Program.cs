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

    /// <summary>
    /// Compile a single Novus module to assembly
    /// Returns the assembly string and list of imported module paths
    /// </summary>
    static async Task<(string Assembly, List<string> ImportedModules)?> CompileModuleToAssembly(
        string inputFile,
        string stdLibPath,
        CompilerOptions options)
    {
        // Read source file
        if (!File.Exists(inputFile))
        {
            Console.WriteLine($"Error: Module file not found: {inputFile}");
            return null;
        }

        var source = await File.ReadAllTextAsync(inputFile);

        // Lex and parse
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var parseTree = parser.compilationUnit();

        if (parser.NumberOfSyntaxErrors > 0)
        {
            Console.WriteLine($"Parse failed for {inputFile} with {parser.NumberOfSyntaxErrors} error(s)");
            return null;
        }

        // Perform semantic analysis
        var analyzer = new SemanticAnalyzer(inputFile, source, stdLibPath);
        var analysisSucceeded = analyzer.Analyze(parseTree);

        if (!analysisSucceeded)
        {
            if (analyzer.Diagnostics.HasErrors || analyzer.Diagnostics.HasWarnings)
            {
                Console.WriteLine(analyzer.Diagnostics.FormatDiagnostics());
            }
            return null;
        }

        // Build IR
        var irBuilder = new IrBuilder();
        irBuilder.SetStdLibPath(stdLibPath);
        var module = irBuilder.BuildModule(parseTree);

        // Run optimization passes
        if (options.OptimizationLevel > 0)
        {
            var optimizer = Novus.Optimizer.OptimizationPipeline.CreatePipeline(
                options.OptimizationLevel,
                options.Verbose
            );
            optimizer.Run(module);
        }

        // Generate 68k assembly
        var codegen = new M68kCodeGenerator(module, irBuilder.StringLiterals, options.Cpu, options.Fpu);
        var assembly = codegen.Generate();

        return (assembly, irBuilder.GetImportedModules());
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

            // Find standard library path
            var compilerDir = AppContext.BaseDirectory;
            var stdLibPath = Path.Combine(compilerDir, "std");

            Console.WriteLine($"Compiling: {options.InputFile}");
            Console.WriteLine("Parsing...");
            Console.WriteLine("Analyzing semantics...");
            Console.WriteLine("Building IR...");

            // Compile the main file and get its dependencies
            var mainResult = await CompileModuleToAssembly(options.InputFile, stdLibPath, options);
            if (mainResult == null)
            {
                return 1;
            }

            var (mainAssembly, importedModules) = mainResult.Value;

            // Recursively collect all dependencies
            var allModules = new Dictionary<string, string>(); // path -> assembly
            var toProcess = new Queue<string>(importedModules);
            var processed = new HashSet<string>();

            while (toProcess.Count > 0)
            {
                var modulePath = toProcess.Dequeue();
                if (processed.Contains(modulePath))
                    continue;

                processed.Add(modulePath);

                if (options.Verbose)
                {
                    Console.WriteLine($"  Compiling dependency: {Path.GetFileName(modulePath)}");
                }

                var moduleResult = await CompileModuleToAssembly(modulePath, stdLibPath, options);
                if (moduleResult == null)
                {
                    Console.WriteLine($"Failed to compile dependency: {modulePath}");
                    return 1;
                }

                var (moduleAssembly, moduleImports) = moduleResult.Value;
                allModules[modulePath] = moduleAssembly;

                // Add transitive dependencies
                foreach (var import in moduleImports)
                {
                    if (!processed.Contains(import))
                    {
                        toProcess.Enqueue(import);
                    }
                }
            }

            if (allModules.Count > 0)
            {
                Console.WriteLine($"  Compiled {allModules.Count} dependencies");
            }

            // Optionally emit IR
            if (options.EmitIr)
            {
                Console.WriteLine("\n=== IR Dump (Before Optimization) ===");
                Console.WriteLine("(IR dump not yet implemented)");
                Console.WriteLine();
            }

            Console.WriteLine("Generating 68k assembly...");

            if (options.EmitAsmOnly)
            {
                // Just output the main assembly
                var asmFile = Path.ChangeExtension(options.OutputFile, ".s");
                await File.WriteAllTextAsync(asmFile, mainAssembly);
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

            // Pass all assemblies (main + dependencies) to the toolchain
            var success = await toolchain.CompileToExecutableWithDependencies(
                mainAssembly,
                allModules,
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