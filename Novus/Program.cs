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
        return await CommandLine.Parser.Default.ParseArguments<CompilerOptions, BuildOptions, GenerateStubsOptions>(args)
            .MapResult(
                (CompilerOptions options) => RunCompiler(options),
                (BuildOptions options) => RunBuild(options),
                (GenerateStubsOptions options) => Task.FromResult(RunGenerateStubs(options)),
                errors => Task.FromResult(1)
            );
    }

    static async Task<int> RunBuild(BuildOptions buildOptions)
    {
        // Determine project directory
        var projectDir = buildOptions.ProjectPath ?? Directory.GetCurrentDirectory();
        if (File.Exists(projectDir) && projectDir.EndsWith(".toml"))
        {
            // Project path is a toml file directly
            projectDir = Path.GetDirectoryName(projectDir) ?? Directory.GetCurrentDirectory();
        }

        projectDir = Path.GetFullPath(projectDir);

        // Load novus.toml
        var projectFile = Path.Combine(projectDir, "novus.toml");
        if (!File.Exists(projectFile))
        {
            Console.WriteLine($"Error: No novus.toml found in {projectDir}");
            Console.WriteLine("Run 'novusc new <name>' to create a new project");
            return 1;
        }

        Console.WriteLine($"Building project: {projectFile}\n");

        Novus.Project.NovusProject project;
        try
        {
            project = Novus.Project.ProjectLoader.LoadFromFile(projectFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading project file: {ex.Message}");
            return 1;
        }

        // Validate project
        if (string.IsNullOrEmpty(project.Package.Name))
        {
            Console.WriteLine("Error: [package] section must specify 'name'");
            return 1;
        }

        // Determine entry point
        var entryFile = project.Package.Entry;
        if (string.IsNullOrEmpty(entryFile))
        {
            // Try main.novus or <package-name>.novus
            var mainPath = Path.Combine(projectDir, project.Paths.Src, "main.novus");
            var packagePath = Path.Combine(projectDir, project.Paths.Src, $"{project.Package.Name}.novus");

            if (File.Exists(mainPath))
                entryFile = Path.Combine(project.Paths.Src, "main.novus");
            else if (File.Exists(packagePath))
                entryFile = Path.Combine(project.Paths.Src, $"{project.Package.Name}.novus");
            else
            {
                Console.WriteLine("Error: No entry point found");
                Console.WriteLine($"  Looked for: {mainPath}");
                Console.WriteLine($"  Looked for: {packagePath}");
                Console.WriteLine("  Or specify 'entry' in [package] section");
                return 1;
            }
        }

        var inputFile = Path.Combine(projectDir, entryFile);
        if (!File.Exists(inputFile))
        {
            Console.WriteLine($"Error: Entry file not found: {inputFile}");
            return 1;
        }

        // Create output directory
        var outputDir = Path.Combine(projectDir, project.Build.Output);
        Directory.CreateDirectory(outputDir);

        // Convert to CompilerOptions
        var compilerOptions = new CompilerOptions
        {
            InputFile = inputFile,
            OutputFile = Path.Combine(outputDir, project.Package.Name),
            Cpu = buildOptions.Cpu ?? project.Build.TargetCpu,
            Fpu = buildOptions.Fpu ?? project.Build.Fpu,
            OptimizationLevel = buildOptions.OptimizationLevel ?? (buildOptions.Release ? 2 : project.Build.OptimizationLevel),
            EmitAsmOnly = buildOptions.EmitAsmOnly || project.Build.EmitAsm,
            VbccPath = buildOptions.VbccPath ?? "/Users/barry/amiga-cc/vbcc",
            NdkPath = buildOptions.NdkPath ?? "/Users/barry/amiga-cc/NDK3.9",
            Verbose = buildOptions.Verbose
        };

        Console.WriteLine($"Package: {project.Package.Name} v{project.Package.Version}");
        if (!string.IsNullOrEmpty(project.Package.Description))
        {
            Console.WriteLine($"Description: {project.Package.Description}");
        }
        Console.WriteLine($"Entry: {entryFile}");
        Console.WriteLine($"Output: {project.Build.Output}/{project.Package.Name}");
        Console.WriteLine();

        // Run the compiler
        return await RunCompiler(compilerOptions);
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

        Console.WriteLine("Error: Specify --library, --all, --list");
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
            irBuilder.SetInputFilePath(inputFile);
            var module = irBuilder.BuildModule(compilationUnit);

            // Run transformation passes (before optimization)
            // Transformations can modify IR structure (inlining, monomorphization, etc.)
            if (options.OptimizationLevel > 0)
            {
                var transformPipeline = Novus.Transforms.TransformPipeline.CreatePipeline(
                    enableInlining: false, // TODO: Enable when implemented
                    verbose: options.Verbose
                );
                if (transformPipeline != null)
                {
                    transformPipeline.Run(module);
                }
            }

            // Run optimization passes
            Novus.Optimizer.Passes.RegisterAllocationPass? regAllocPass = null;
            if (options.OptimizationLevel > 0)
            {
                var optimizer = Novus.Optimizer.OptimizationPipeline.CreatePipeline(
                    options.OptimizationLevel,
                    options.Verbose
                );
                optimizer.Run(module);

                // Extract register allocation results if optimization level includes register allocation (level 2+)
                if (options.OptimizationLevel >= 2)
                {
                    // Find the RegisterAllocationPass in the pipeline to get its results
                    regAllocPass = optimizer.GetPass<Novus.Optimizer.Passes.RegisterAllocationPass>();
                }
            }

            // Generate C code
            var codegen = new CCodeGenerator(module, irBuilder.StringLiterals, options.Cpu, options.Fpu);

            // Note: Register allocation is not used with C backend - VBCC handles register allocation

            var cCode = codegen.Generate();

            return (cCode, irBuilder.GetImportedModules());
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

            var (mainCCode, importedModules) = mainResult.Value;

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
            var allModules = new Dictionary<string, string>(); // path -> C code
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

                var (moduleCCode, moduleImports) = moduleResult.Value;
                allModules[modulePath] = moduleCCode;

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

            Console.WriteLine("Generating C code...");

            // Determine output directory and base name (used by both emit-only and full build)
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(options.OutputFile)) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(options.OutputFile);

            // Write all C files to disk first (needed for both emit-only and full build)
            var mainCFile = Path.Combine(outputDir, $"{baseName}.c");
            await File.WriteAllTextAsync(mainCFile, mainCCode);
            var cFiles = new List<string> { mainCFile };

            // Write dependency C files
            foreach (var (modulePath, cCode) in allModules)
            {
                var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                var depCFile = Path.Combine(outputDir, $"{moduleName}.c");
                await File.WriteAllTextAsync(depCFile, cCode);
                cFiles.Add(depCFile);
            }

            if (options.EmitAsmOnly)
            {
                // Just output the C files and stop
                Console.WriteLine($"  → {Path.GetFileName(mainCFile)}");
                foreach (var cFile in cFiles.Skip(1))
                {
                    Console.WriteLine($"  → {Path.GetFileName(cFile)}");
                }

                Console.WriteLine($"\nC files written to: {outputDir}");
                return 0;
            }

            // Compile C code with VBCC
            Console.WriteLine("Compiling with VBCC...");
            var toolchain = new VbccToolchain(options.VbccPath, options.NdkPath);

            // Link assembly stubs for AmigaOS library calls
            // Our assembly stubs use i32 signatures, avoiding VBCC's type system (BPTR, CONST_STRPTR, etc.)
            var objectFiles = new List<string>();

            // Map "auto" CPU to a concrete target for assembly (vasm doesn't understand "auto")
            var assemblyCpu = options.Cpu == "auto" ? "68020" : options.Cpu;

            // Assemble core Novus runtime files (always needed)
            var coreFiles = new[] { "novus_startup", "library_bases" };
            foreach (var coreFile in coreFiles)
            {
                var coreSource = Path.Combine(compilerDir, "stubs", $"{coreFile}.s");
                if (File.Exists(coreSource))
                {
                    var coreObj = Path.Combine(outputDir, $"{coreFile}.o");
                    if (!await toolchain.Assemble(coreSource, coreObj, assemblyCpu, false))
                    {
                        Console.WriteLine($"Failed to assemble {coreFile}");
                        return 1;
                    }
                    objectFiles.Add(coreObj);
                }
            }

            // Assemble library stubs
            var stubsToAssemble = new[] { "exec", "dos" }; // Common stubs for basic programs

            foreach (var stub in stubsToAssemble)
            {
                var stubSource = Path.Combine(compilerDir, "stubs", $"{stub}_stubs.s");
                if (File.Exists(stubSource))
                {
                    var stubObj = Path.Combine(outputDir, $"{stub}_stubs.o");
                    if (!await toolchain.Assemble(stubSource, stubObj, assemblyCpu, false))
                    {
                        Console.WriteLine($"Failed to assemble {stub} stubs");
                        return 1;
                    }
                    objectFiles.Add(stubObj);

                    // If using DOS library, also include dos_init.o for automatic DOSBase initialization
                    if (stub == "dos")
                    {
                        var dosInitSource = Path.Combine(compilerDir, "stubs", "dos_init.s");
                        if (File.Exists(dosInitSource))
                        {
                            var dosInitObj = Path.Combine(outputDir, "dos_init.o");
                            if (!await toolchain.Assemble(dosInitSource, dosInitObj, assemblyCpu, false))
                            {
                                Console.WriteLine("Failed to assemble dos_init");
                                return 1;
                            }
                            objectFiles.Add(dosInitObj);
                        }
                    }
                }
            }

            // Compile and link with vc (vc understands "auto" for fat binaries, but use concrete target for now)
            var exeFile = Path.Combine(outputDir, baseName);
            var success = await toolchain.CompileWithVC(
                cFiles,
                objectFiles,
                exeFile,
                assemblyCpu,
                options.OptimizationLevel
            );

            if (success)
            {
                Console.WriteLine($"\n✓ Successfully created: {Path.GetFileName(exeFile)}");
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