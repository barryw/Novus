using Antlr4.Runtime;
using CommandLine;
using Novus.Codegen;
using Novus.Compilation;
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
        CompilerOptions options,
        ModuleCache moduleCache,
        CircularImportDetector? circularImportDetector = null)
    {
        // Check for circular imports if detector is provided
        if (circularImportDetector != null)
        {
            if (!circularImportDetector.EnterModule(inputFile))
            {
                // Circular dependency detected - error already reported
                return null;
            }
        }

        try
        {
            // Read source file
            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: Module file not found: {inputFile}");
                return null;
            }

            var source = await File.ReadAllTextAsync(inputFile);

            // Create diagnostic bag for error collection
            var diagnostics = new DiagnosticBag();

            // Try to get cached parse tree
            Antlr4.Runtime.Tree.IParseTree? cachedParseTree;
            NovusParser.CompilationUnitContext compilationUnit;

            if (moduleCache.TryGet(inputFile, out cachedParseTree) && cachedParseTree != null)
            {
                // Cache hit - skip parsing
                if (options.Verbose)
                {
                    Console.WriteLine($"  [Cache hit] {Path.GetFileName(inputFile)}");
                }
                compilationUnit = (NovusParser.CompilationUnitContext)cachedParseTree;
            }
            else
            {
                // Cache miss - parse the file
                var inputStream = new AntlrInputStream(source);
                var lexer = new NovusLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new NovusParser(tokenStream);

                // Remove default error listeners and add our custom one
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new NovusErrorListener(diagnostics, inputFile, source));

                compilationUnit = parser.compilationUnit();

                // Check for parse errors
                if (diagnostics.HasErrors)
                {
                    Console.WriteLine(diagnostics.FormatDiagnostics());
                    return null;
                }

                // Add to cache
                moduleCache.Add(inputFile, compilationUnit);
            }

            // Perform semantic analysis
            var analyzer = new SemanticAnalyzer(inputFile, source, stdLibPath);
            var analysisSucceeded = analyzer.Analyze(compilationUnit);

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
            var module = irBuilder.BuildModule(compilationUnit);

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
        finally
        {
            // Always exit module to maintain proper stack state
            if (circularImportDetector != null)
            {
                circularImportDetector.ExitModule();
            }
        }
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

            // Create module cache for performance
            var moduleCache = new ModuleCache();

            // Create diagnostic bag and circular import detector
            var diagnostics = new DiagnosticBag();
            var circularImportDetector = new CircularImportDetector(diagnostics);

            Console.WriteLine($"Compiling: {options.InputFile}");
            Console.WriteLine("Parsing...");
            Console.WriteLine("Analyzing semantics...");
            Console.WriteLine("Building IR...");

            // Compile the main file and get its dependencies
            var mainResult = await CompileModuleToAssembly(options.InputFile, stdLibPath, options, moduleCache, circularImportDetector);
            if (mainResult == null)
            {
                // Check if it was a circular import error
                if (diagnostics.HasErrors)
                {
                    Console.WriteLine(diagnostics.FormatDiagnostics());
                }
                return 1;
            }

            var (mainAssembly, importedModules) = mainResult.Value;

            // Record dependencies from the main module and check for cycles
            foreach (var import in importedModules)
            {
                if (!circularImportDetector.RecordDependency(options.InputFile, import))
                {
                    // Circular dependency detected
                    Console.WriteLine(diagnostics.FormatDiagnostics());
                    return 1;
                }
            }

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

                // Always show which module is being compiled (not just in verbose mode)
                var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                var moduleDir = Path.GetFileName(Path.GetDirectoryName(modulePath));
                var displayName = moduleDir == "std" ? $"std::{moduleName}" : moduleName;
                Console.WriteLine($"  → {displayName}");

                var moduleResult = await CompileModuleToAssembly(modulePath, stdLibPath, options, moduleCache, circularImportDetector);
                if (moduleResult == null)
                {
                    // Check if it was a circular import error
                    if (diagnostics.HasErrors)
                    {
                        Console.WriteLine(diagnostics.FormatDiagnostics());
                    }
                    else
                    {
                        Console.WriteLine($"Failed to compile dependency: {modulePath}");
                    }
                    return 1;
                }

                var (moduleAssembly, moduleImports) = moduleResult.Value;
                allModules[modulePath] = moduleAssembly;

                // Add transitive dependencies and check for cycles
                foreach (var import in moduleImports)
                {
                    // Record the dependency and check for cycles
                    if (!circularImportDetector.RecordDependency(modulePath, import))
                    {
                        // Circular dependency detected
                        Console.WriteLine(diagnostics.FormatDiagnostics());
                        return 1;
                    }

                    if (!processed.Contains(import))
                    {
                        toProcess.Enqueue(import);
                    }
                }
            }

            if (allModules.Count > 0)
            {
                Console.WriteLine($"  ✓ Compiled {allModules.Count} module{(allModules.Count > 1 ? "s" : "")}");
            }

            if (options.Verbose && moduleCache.Count > 0)
            {
                Console.WriteLine($"  Module cache: {moduleCache.Count} modules cached");
            }

            // Optionally emit IR
            if (options.EmitIr)
            {
                Console.WriteLine("\n=== IR Dump (Before Optimization) ===");
                Console.WriteLine("(IR dump not yet implemented)");
                Console.WriteLine();
            }

            Console.WriteLine("Generating 68k assembly...");

            // Determine output directory and base name (used by both asm-only and full build)
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(options.OutputFile)) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(options.OutputFile);

            if (options.EmitAsmOnly)
            {
                // Output all assembly files (main + dependencies)
                var mainAsmFile = Path.Combine(outputDir, $"{baseName}.s");
                await File.WriteAllTextAsync(mainAsmFile, mainAssembly);
                Console.WriteLine($"  → {Path.GetFileName(mainAsmFile)}");

                // Write dependency assemblies
                foreach (var (modulePath, assembly) in allModules)
                {
                    var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                    var depAsmFile = Path.Combine(outputDir, $"{moduleName}.s");
                    await File.WriteAllTextAsync(depAsmFile, assembly);
                    Console.WriteLine($"  → {Path.GetFileName(depAsmFile)}");
                }

                Console.WriteLine($"\nAssembly files written to: {outputDir}");
                return 0;
            }

            // Assemble and link with VBCC
            Console.WriteLine("Assembling and linking...");
            var toolchain = new VbccToolchain(options.VbccPath, options.NdkPath);

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