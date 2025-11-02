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
        return await CommandLine.Parser.Default.ParseArguments<CompilerOptions, BuildOptions, GenerateStubsOptions, NewCommandOptions>(args)
            .MapResult(
                (CompilerOptions options) => RunCompiler(options),
                (BuildOptions options) => RunBuild(options),
                (GenerateStubsOptions options) => Task.FromResult(RunGenerateStubs(options)),
                (NewCommandOptions options) => Task.FromResult(Commands.NewCommand.Run(options)),
                errors => Task.FromResult(1)
            );
    }

    static async Task<int> RunBuild(BuildOptions buildOptions)
    {
        // Delegate to BuildCommand which handles workspace/project detection
        return await Commands.BuildCommand.Run(buildOptions);
    }

    static int RunGenerateStubs(GenerateStubsOptions options)
    {
        // Generate FFI bindings from SFD files (NDK 3.9+)
        var sfdGenerator = new SfdGenerator(options.NdkPath, options.OutputPath);
        sfdGenerator.GenerateAllBindings();
        return 0;
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

            // Run preprocessor
            var preprocessorConstants = new Dictionary<string, bool>
            {
                ["DEBUG"] = options.BuildMode == BuildMode.Debug,
                ["RELEASE"] = options.BuildMode == BuildMode.Release
            };
            var preprocessor = new Preprocessing.Preprocessor(preprocessorConstants, diagnostics, inputFile);
            source = preprocessor.Process(source);

            // Check for preprocessor errors
            if (diagnostics.HasErrors)
            {
                Console.WriteLine(diagnostics.FormatDiagnostics());
                return null;
            }

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
                var tokenStream = new AngleBracketTokenStream(lexer);
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

            // Run preprocessor
            var preprocessorConstants = new Dictionary<string, bool>
            {
                ["DEBUG"] = options.BuildMode == BuildMode.Debug,
                ["RELEASE"] = options.BuildMode == BuildMode.Release
            };
            var preprocessor = new Preprocessing.Preprocessor(preprocessorConstants, diagnostics, inputFile);
            source = preprocessor.Process(source);

            // Check for preprocessor errors
            if (diagnostics.HasErrors)
            {
                Console.WriteLine(diagnostics.FormatDiagnostics());
                return null;
            }

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
                var tokenStream = new AngleBracketTokenStream(lexer);
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
            if (options.OptimizationLevel > 0)
            {
                var optimizer = Novus.Optimizer.OptimizationPipeline.CreatePipeline(
                    options.OptimizationLevel,
                    options.Verbose
                );
                optimizer.Run(module);
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

            var codegen = new CCodeGenerator(module, irBuilder.StringLiterals, options.Cpu, options.Fpu, explicitEntryPoints, false, options.ProjectVersion);
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

    public static async Task<int> RunCompiler(CompilerOptions options)
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
                useSharedTypesHeader: true,
                projectVersion: options.ProjectVersion);

            var mainCCode = mainCodegen.Generate();
            var mainCFile = Path.Combine(outputDir, $"{baseName}.c");
            await File.WriteAllTextAsync(mainCFile, mainCCode);
            cFiles.Add(mainCFile);

            Console.WriteLine($"  → {Path.GetFileName(mainCFile)}");

            // For libraries, generate A6 wrapper assembly and interface files
            // (isLibrary/isDevice will be defined later in the assembly section)
            var projectType = options.ProjectType.ToLowerInvariant();
            if (projectType == "library" || projectType == "device")
            {
                var libraryGen = new LibraryGenerator(mainIR.IrModule);
                if (libraryGen.IsLibrary)
                {
                    // Generate A6 wrappers
                    var wrapperAsm = libraryGen.GenerateA6Wrappers();
                    var wrapperAsmFile = Path.Combine(outputDir, $"{baseName}_wrappers.s");
                    await File.WriteAllTextAsync(wrapperAsmFile, wrapperAsm);
                    Console.WriteLine($"  → {Path.GetFileName(wrapperAsmFile)} (A6 wrappers)");

                    // Generate C header
                    var cHeader = libraryGen.GenerateCHeader();
                    var headerFile = Path.Combine(outputDir, $"{baseName}.h");
                    await File.WriteAllTextAsync(headerFile, cHeader);
                    Console.WriteLine($"  → {Path.GetFileName(headerFile)} (C header)");

                    // Generate Novus FFI binding
                    var novusFfi = libraryGen.GenerateNovusFFI();
                    var ffiFile = Path.Combine(outputDir, $"{baseName}.novus");
                    await File.WriteAllTextAsync(ffiFile, novusFfi);
                    Console.WriteLine($"  → {Path.GetFileName(ffiFile)} (Novus FFI)");

                    // Generate FD file for VBCC auto-library support
                    var fdFile = libraryGen.GenerateFDFile();
                    var fdFilePath = Path.Combine(outputDir, $"{baseName}_lib.fd");
                    await File.WriteAllTextAsync(fdFilePath, fdFile);
                    Console.WriteLine($"  → {Path.GetFileName(fdFilePath)} (FD file)");

                    // Generate library stub for auto-open/close
                    var stubAsm = libraryGen.GenerateLibraryStub();
                    var stubAsmFile = Path.Combine(outputDir, $"{baseName}_lib.s");
                    await File.WriteAllTextAsync(stubAsmFile, stubAsm);
                    Console.WriteLine($"  → {Path.GetFileName(stubAsmFile)} (library stub)");
                }
            }

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
                    useSharedTypesHeader: true,
                    projectVersion: options.ProjectVersion);

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

            // Add runtime library C files
            // These provide runtime support functions (like write() for formatted I/O)
            var runtimeFiles = new[] { "novus_io.c" };
            var hasRuntimeFiles = false;
            foreach (var runtimeFile in runtimeFiles)
            {
                var runtimeSource = Path.Combine(compilerDir, "runtime", runtimeFile);
                if (File.Exists(runtimeSource))
                {
                    cFiles.Add(runtimeSource);
                    hasRuntimeFiles = true;
                }
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

            // Assemble core Novus runtime files (only for executables, not libraries)
            var isLibrary = options.ProjectType.ToLowerInvariant() == "library";
            var isDevice = options.ProjectType.ToLowerInvariant() == "device";

            if (!isLibrary && !isDevice)
            {
                // Only executables need startup code and SysBase
                var coreFiles = new[] { "novus_startup", "exec_base" };
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
            }

            // For libraries, assemble the A6 wrapper file and library stub
            // BUT DON'T ADD TO objectFiles YET - wrappers must come AFTER C code
            string? wrapperObj = null;
            if (isLibrary || isDevice)
            {
                var wrapperAsmFile = Path.Combine(outputDir, $"{baseName}_wrappers.s");
                if (File.Exists(wrapperAsmFile))
                {
                    wrapperObj = Path.Combine(outputDir, $"{baseName}_wrappers.o");
                    if (!await toolchain.Assemble(wrapperAsmFile, wrapperObj, assemblyCpu, false))
                    {
                        Console.WriteLine("Failed to assemble A6 wrappers");
                        return 1;
                    }
                    // DO NOT add to objectFiles here - will add after C files
                    Console.WriteLine($"  ✓ Assembled A6 wrappers: {baseName}_wrappers.o (will link after C code)");
                }

                // Assemble library stub for auto-open support
                var stubAsmFile = Path.Combine(outputDir, $"{baseName}_lib.s");
                if (File.Exists(stubAsmFile))
                {
                    var stubObj = Path.Combine(outputDir, $"{baseName}_lib.o");
                    if (!await toolchain.Assemble(stubAsmFile, stubObj, assemblyCpu, false))
                    {
                        Console.WriteLine("Failed to assemble library stub");
                        return 1;
                    }
                    // Don't add to objectFiles - this is for users to link against
                    Console.WriteLine($"  ✓ Assembled library stub: {baseName}_lib.o");
                }

                // Libraries that use runtime files (novus_io.c) need library base objects
                // These provide the global _SysBase and _DOSBase symbols needed by VBCC's proto headers
                if (hasRuntimeFiles)
                {
                    var libraryBases = new[] { "exec_base", "dos_base" };
                    foreach (var baseFile in libraryBases)
                    {
                        var baseSource = Path.Combine(compilerDir, "stubs", $"{baseFile}.s");
                        if (File.Exists(baseSource))
                        {
                            var baseObj = Path.Combine(outputDir, $"{baseFile}.o");
                            if (!await toolchain.Assemble(baseSource, baseObj, assemblyCpu, false))
                            {
                                Console.WriteLine($"Failed to assemble {baseFile}");
                                return 1;
                            }
                            objectFiles.Add(baseObj);
                        }
                    }
                }
            }

            // Detect which library stubs are actually needed by scanning generated C code
            Console.WriteLine("\n=== DEBUG: Detecting required libraries from C code ===");

            var requiredLibraries = new HashSet<string>();

            // Always include exec (needed for basic Amiga operations like AllocMem/FreeMem)
            requiredLibraries.Add("exec");
            Console.WriteLine("  ✓ Always including 'exec' library");

            // Include DOS library when runtime files are present (they use Output, Write, etc.)
            if (hasRuntimeFiles)
            {
                requiredLibraries.Add("dos");
                Console.WriteLine("  ✓ Including 'dos' library (needed by runtime)");
            }

            // Scan generated C files for DOS library function calls
            foreach (var cFile in cFiles)
            {
                var cCode = await File.ReadAllTextAsync(cFile);

                // Check for DOS function calls in the C code
                if (cCode.Contains("_Output(") ||
                    cCode.Contains("_Input(") ||
                    cCode.Contains("_Write(") ||
                    cCode.Contains("_Read(") ||
                    cCode.Contains("_Printf(") ||
                    cCode.Contains("IoErr("))
                {
                    requiredLibraries.Add("dos");
                    Console.WriteLine($"  ✓ Detected DOS library usage in {Path.GetFileName(cFile)}");
                    break; // Only need to find it once
                }
            }

            Console.WriteLine($"=== Required libraries: {string.Join(", ", requiredLibraries)} ===\n");

            // If DOS library is needed by executables, assemble dos_base.o
            // (Libraries handle this separately when runtime files are present)
            if (requiredLibraries.Contains("dos") && !isLibrary && !isDevice)
            {
                var dosBaseSource = Path.Combine(compilerDir, "stubs", "dos_base.s");
                if (File.Exists(dosBaseSource))
                {
                    var dosBaseObj = Path.Combine(outputDir, "dos_base.o");
                    if (!await toolchain.Assemble(dosBaseSource, dosBaseObj, assemblyCpu, false))
                    {
                        Console.WriteLine("Failed to assemble dos_base");
                        return 1;
                    }
                    objectFiles.Add(dosBaseObj);
                }
            }

            // Assemble library stubs (skip for libraries/devices - they handle SysBase differently)
            if (!isLibrary && !isDevice)
            {
                foreach (var stub in requiredLibraries)
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
            }

            // Add any additional C files from the project (e.g., library wrappers)
            if (options.AdditionalCFiles.Count > 0)
            {
                cFiles.AddRange(options.AdditionalCFiles);
                Console.WriteLine($"\nIncluding {options.AdditionalCFiles.Count} additional C file(s)");
            }

            // Assemble any additional assembly files from the project (e.g., library wrappers)
            if (options.AdditionalAsmFiles.Count > 0)
            {
                Console.WriteLine($"\nIncluding {options.AdditionalAsmFiles.Count} additional ASM file(s)");
                Console.WriteLine("Assembling additional files...");
                foreach (var asmFile in options.AdditionalAsmFiles)
                {
                    var asmFileName = Path.GetFileName(asmFile);
                    var objFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(asmFile) + ".o");

                    Console.WriteLine($"  → {asmFileName}");
                    if (!await toolchain.Assemble(asmFile, objFile, assemblyCpu, false))
                    {
                        Console.WriteLine($"\n✗ Failed to assemble {asmFileName}");
                        return 1;
                    }

                    objectFiles.Add(objFile);
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

            // CRITICAL: Add wrapper object AFTER C code so linker can resolve symbols correctly
            // Wrappers call C functions, so C functions must be linked first
            if (wrapperObj != null)
            {
                objectFiles.Add(wrapperObj);
                Console.WriteLine($"  → {Path.GetFileName(wrapperObj)} (added after C code for correct symbol resolution)");
            }

            // Add additional library object files from workspace dependencies
            if (options.AdditionalLibraries.Count > 0)
            {
                objectFiles.AddRange(options.AdditionalLibraries);
            }

            // Step 2: Link all object files with dead code elimination
            // Use the full output filename (with extension) for the final binary
            var exeFile = options.OutputFile;
            Console.WriteLine("\nLinking with dead code elimination...");
            Console.WriteLine($"  → {objectFiles.Count} object files");
            if (options.AdditionalLibraries.Count > 0)
            {
                Console.WriteLine($"  → {options.AdditionalLibraries.Count} dependency libraries");
            }

            var success = await toolchain.Link(
                objectFiles.ToArray(),
                exeFile,
                options.Fpu,
                includeStartup: false,  // startup already in objectFiles
                isLibrary: isLibrary || isDevice  // libraries and devices need relocations
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