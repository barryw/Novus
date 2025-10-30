using Antlr4.Runtime;
using CommandLine;
using Novus.Codegen;
using Novus.Compilation;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
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
    /// Module IR compilation result (before C code generation)
    /// </summary>
    record ModuleIR(
        string ModulePath,
        string ModuleName,
        IrModule IrModule,
        List<IrStringLiteral> StringLiterals,
        List<string> ImportedModules,
        bool HasMain);

    /// <summary>
    /// Compile a single Novus module to IR (without generating C code yet).
    /// This allows us to collect all modules first, then generate a shared types header.
    /// </summary>
    static async Task<ModuleIR?> CompileModuleToIR(
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

            var moduleName = Path.GetFileNameWithoutExtension(inputFile);
            var hasMain = module.Functions.Any(f => f.Name == "main" && !f.IsExtern);

            return new ModuleIR(
                inputFile,
                moduleName,
                module,
                irBuilder.StringLiterals,
                irBuilder.GetImportedModules(),
                hasMain);
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

    /// <summary>
    /// Compile a single Novus module to assembly (LEGACY - for compatibility)
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
            // OPTIMIZATION: For std::error, only generate functions that are actually imported
            // This enables smart cross-module DCE without needing whole-program analysis
            HashSet<string>? explicitEntryPoints = null;
            var moduleName = Path.GetFileNameWithoutExtension(inputFile);
            if (moduleName == "error" && inputFile.Contains("/std/"))
            {
                // For std::error, only dos_last_error is imported by other modules
                // (dos_error_from_code will be included transitively since dos_last_error calls it)
                explicitEntryPoints = new HashSet<string> { "dos_last_error" };
            }

            var codegen = new CCodeGenerator(module, irBuilder.StringLiterals, options.Cpu, options.Fpu, explicitEntryPoints);

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

            // ============================================================================
            // PHASE 1: Compile all modules to IR (without generating C code yet)
            // ============================================================================

            // Compile the main file to IR
            var mainIR = await CompileModuleToIR(options.InputFile, stdLibPath, options, moduleCache, circularImportDetector);
            if (mainIR == null)
            {
                if (diagnostics.HasErrors)
                {
                    Console.WriteLine(diagnostics.FormatDiagnostics());
                }
                return 1;
            }

            // Record dependencies from the main module
            foreach (var import in mainIR.ImportedModules)
            {
                if (!circularImportDetector.RecordDependency(options.InputFile, import))
                {
                    Console.WriteLine(diagnostics.FormatDiagnostics());
                    return 1;
                }
            }

            // Recursively collect all dependencies to IR
            var allModulesIR = new Dictionary<string, ModuleIR>(); // path -> IR
            var toProcess = new Queue<string>(mainIR.ImportedModules);
            var processed = new HashSet<string>();

            while (toProcess.Count > 0)
            {
                var modulePath = toProcess.Dequeue();
                if (processed.Contains(modulePath))
                    continue;

                processed.Add(modulePath);

                // Show which module is being compiled
                var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                var moduleDir = Path.GetFileName(Path.GetDirectoryName(modulePath));
                var displayName = moduleDir == "std" ? $"std::{moduleName}" : moduleName;
                Console.WriteLine($"  → {displayName}");

                var moduleIR = await CompileModuleToIR(modulePath, stdLibPath, options, moduleCache, circularImportDetector);
                if (moduleIR == null)
                {
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

                allModulesIR[modulePath] = moduleIR;

                // Add transitive dependencies
                foreach (var import in moduleIR.ImportedModules)
                {
                    if (!circularImportDetector.RecordDependency(modulePath, import))
                    {
                        Console.WriteLine(diagnostics.FormatDiagnostics());
                        return 1;
                    }

                    if (!processed.Contains(import))
                    {
                        toProcess.Enqueue(import);
                    }
                }
            }

            if (allModulesIR.Count > 0)
            {
                Console.WriteLine($"  ✓ Compiled {allModulesIR.Count + 1} module{(allModulesIR.Count > 0 ? "s" : "")}");
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

            // ============================================================================
            // PHASE 2: Generate shared types header + per-function C files
            // ============================================================================

            Console.WriteLine("Generating C code...");

            // Build type registry from all modules
            var typeRegistry = new TypeRegistry();
            typeRegistry.RegisterModule(mainIR.IrModule);
            foreach (var moduleIR in allModulesIR.Values)
            {
                typeRegistry.RegisterModule(moduleIR.IrModule);
            }

            // Generate shared types header
            var sharedTypesHeader = CCodeGenerator.GenerateSharedTypesHeader(typeRegistry);

            // Determine output directory
            var outputDir = Path.GetDirectoryName(Path.GetFullPath(options.OutputFile)) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(options.OutputFile);

            // Write shared types header
            var typesHeaderPath = Path.Combine(outputDir, "novus_types.h");
            await File.WriteAllTextAsync(typesHeaderPath, sharedTypesHeader);

            // Generate C files - collect all file paths
            var cFiles = new List<string>();

            // Main module: generate monolithic C file (with string literals and main function)
            var mainCodegen = new CCodeGenerator(
                mainIR.IrModule,
                mainIR.StringLiterals,
                options.Cpu,
                options.Fpu,
                explicitEntryPoints: null,
                useSharedTypesHeader: true);

            var mainCCode = mainCodegen.Generate();
            var mainCFile = Path.Combine(outputDir, $"{baseName}.c");
            await File.WriteAllTextAsync(mainCFile, mainCCode);
            cFiles.Add(mainCFile);

            Console.WriteLine($"  → {Path.GetFileName(mainCFile)}");

            // Library modules: generate one C file per function
            foreach (var (modulePath, moduleIR) in allModulesIR)
            {
                var moduleName = moduleIR.ModuleName;
                var isStdModule = modulePath.Contains("/std/");

                // Get all non-extern functions with implementations
                var functions = moduleIR.IrModule.Functions
                    .Where(f => !f.IsExtern && f.BasicBlocks.Count > 0)
                    .ToList();

                if (functions.Count == 0)
                    continue;

                var moduleCodegen = new CCodeGenerator(
                    moduleIR.IrModule,
                    moduleIR.StringLiterals,
                    options.Cpu,
                    options.Fpu,
                    explicitEntryPoints: null,
                    useSharedTypesHeader: true);

                // Generate one C file per function
                foreach (var function in functions)
                {
                    var functionCCode = moduleCodegen.GenerateFunctionFile(function);
                    var functionCFile = Path.Combine(outputDir, $"{moduleName}_{function.Name}.c");
                    await File.WriteAllTextAsync(functionCFile, functionCCode);
                    cFiles.Add(functionCFile);
                }

                var displayName = isStdModule ? $"std::{moduleName}" : moduleName;
                Console.WriteLine($"  → {displayName} ({functions.Count} function{(functions.Count > 1 ? "s" : "")})");
            }

            // Handle emit-only mode (just generate C files and stop)
            if (options.EmitAsmOnly)
            {
                Console.WriteLine($"\nC files and header written to: {outputDir}");
                Console.WriteLine($"  novus_types.h (shared types)");
                Console.WriteLine($"  {cFiles.Count} function file{(cFiles.Count > 1 ? "s" : "")}");
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

            // Step 1: Compile each C file to an object file
            Console.WriteLine("\nCompiling C files...");
            foreach (var cFile in cFiles)
            {
                var cFileName = Path.GetFileName(cFile);
                var objFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(cFile) + ".o");

                Console.WriteLine($"  → {cFileName}");
                if (!await toolchain.CompileToObject(cFile, objFile, assemblyCpu, options.OptimizationLevel))
                {
                    Console.WriteLine($"\n✗ Failed to compile {cFileName}");
                    return 1;
                }

                objectFiles.Add(objFile);
            }

            // Step 2: Link all object files with dead code elimination
            var exeFile = Path.Combine(outputDir, baseName);
            Console.WriteLine("\nLinking with dead code elimination...");
            Console.WriteLine($"  → {objectFiles.Count} object files");

            var success = await toolchain.Link(
                objectFiles.ToArray(),
                exeFile,
                options.Fpu,
                includeStartup: false  // startup already in objectFiles
            );

            if (success)
            {
                Console.WriteLine($"\n✓ Successfully created: {Path.GetFileName(exeFile)}");
                return 0;
            }
            else
            {
                Console.WriteLine("\n✗ Linking failed");
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