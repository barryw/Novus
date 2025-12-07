using System.Text.Json.Serialization;
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

/// <summary>
/// Metadata stored alongside cached object files for integrity validation
/// </summary>
public class CacheMetadata
{
    public long FileSize { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// JSON source generator context for CacheMetadata (AOT-compatible)
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CacheMetadata))]
internal partial class CacheMetadataJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Novus Compiler - Main entry point and compilation orchestration.
///
/// INCREMENTAL COMPILATION ARCHITECTURE:
/// =====================================
/// The compiler implements a multi-layer caching strategy to minimize rebuild times:
///
/// 1. IR Module Cache (CompilationCache in Novus.Core)
///    - Persists compiled IR modules to disk (.novus-cache directory)
///    - Keyed by: source file hash + compiler version + config hash
///    - Invalidates on: source change, compiler update, build config change, dependency change
///    - Tracks transitive dependencies for cascade invalidation
///
/// 2. Object File Cache (usercache directory)
///    - Caches compiled .o files for user C code
///    - Keyed by: CODEGEN_VERSION + C file hash + types header hash + CPU + FPU + build mode + opt level
///    - Validates integrity via .meta files with size/hash verification
///    - Location: {outputDir}/usercache/{cpu}/{buildMode}/
///
/// 3. Stdlib Module Cache (precompiled stdlib)
///    - Pre-compiled standard library modules
///    - Location: {compilerDir}/precompiled/{cpu}/{buildMode}/
///    - Invalidates when stdlib source changes or CODEGEN_VERSION bumps
///
/// 4. Infrastructure Cache (stubs and runtime)
///    - Hashes all .s files in stubs/ and runtime/ directories
///    - Stored in .novus_infrastructure_hash per output directory
///    - Forces rebuild of infrastructure .o files when changed
///
/// Cache Invalidation Triggers:
/// - CODEGEN_VERSION bump: invalidates ALL caches (use for breaking codegen changes)
/// - Source file change: invalidates that file's IR and object caches + dependents
/// - Types header change: invalidates all object caches (ABI change)
/// - Build config change (CPU/FPU/mode/opt): invalidates object caches
/// - Dependency change: cascades through IR cache's dependency graph
/// </summary>
class Program
{
    // Codegen format version - increment to invalidate all cached object files
    // when making breaking changes to code generation or compilation process.
    // This is the "nuclear option" - prefer targeted invalidation when possible.
    // v4: Added debug label injection via assembly post-processing in debug builds
    private const int CODEGEN_VERSION = 4;

    /// <summary>
    /// Build preprocessor constants dictionary based on compiler options.
    /// Used for conditional compilation with #if/#elif/#else/#endif directives.
    /// </summary>
    private static Dictionary<string, bool> GetPreprocessorConstants(CompilerOptions options)
    {
        // Determine effective CPU for "auto" mode
        var cpu = options.Cpu == "auto" ? "68020" : options.Cpu;

        // CPU hierarchy: 68000 < 68010 < 68020 < 68030 < 68040 < 68060
        var cpuLevel = cpu switch
        {
            "68000" => 0,
            "68010" => 1,
            "68020" => 2,
            "68030" => 3,
            "68040" => 4,
            "68060" => 5,
            _ => 2 // default to 68020
        };

        return new Dictionary<string, bool>
        {
            ["DEBUG"] = options.BuildMode == BuildMode.Debug,
            ["RELEASE"] = options.BuildMode == BuildMode.Release,

            // Exact CPU target constants
            ["M68000"] = cpu == "68000",
            ["M68010"] = cpu == "68010",
            ["M68020"] = cpu == "68020",
            ["M68030"] = cpu == "68030",
            ["M68040"] = cpu == "68040",
            ["M68060"] = cpu == "68060",

            // "At least" CPU constants - true if target CPU is this level or higher
            // Useful for: #if M68020_PLUS to use 68020+ instructions like BFFFO, MULS.L
            ["M68000_PLUS"] = cpuLevel >= 0, // Always true (all targets are at least 68000)
            ["M68010_PLUS"] = cpuLevel >= 1, // 68010, 68020, 68030, 68040, 68060
            ["M68020_PLUS"] = cpuLevel >= 2, // 68020, 68030, 68040, 68060
            ["M68030_PLUS"] = cpuLevel >= 3, // 68030, 68040, 68060
            ["M68040_PLUS"] = cpuLevel >= 4, // 68040, 68060
            ["M68060_PLUS"] = cpuLevel >= 5, // 68060 only

            // FPU target constants
            ["FPU_NONE"] = options.Fpu == "none" || options.Fpu == "soft",
            ["FPU_SOFT"] = options.Fpu == "soft",
            ["FPU_68881"] = options.Fpu == "68881",
            ["FPU_68882"] = options.Fpu == "68882",
            ["FPU_68040"] = options.Fpu == "68040" || (cpu == "68040" && options.Fpu != "none" && options.Fpu != "soft"),
            ["FPU_68060"] = options.Fpu == "68060" || (cpu == "68060" && options.Fpu != "none" && options.Fpu != "soft"),

            // "Has FPU" - true if any hardware FPU is available
            ["HAS_FPU"] = options.Fpu != "none" && options.Fpu != "soft" && options.Fpu != "auto",

            // Chipset target constants
            ["OCS"] = options.Chipset == "OCS",
            ["ECS"] = options.Chipset == "ECS",
            ["AGA"] = options.Chipset == "AGA",

            // "At least" chipset constants
            ["ECS_PLUS"] = options.Chipset == "ECS" || options.Chipset == "AGA",
            ["AGA_PLUS"] = options.Chipset == "AGA"
        };
    }

    static async Task<int> Main(string[] args)
    {
        return await CommandLine.Parser.Default.ParseArguments<CompilerOptions, BuildOptions, GenerateStubsOptions, NewCommandOptions, StdlibBuildOptions, FmtOptions, CleanOptions, TestOptions, BenchOptions>(args)
            .MapResult(
                (CompilerOptions options) => RunCompiler(options),
                (BuildOptions options) => RunBuild(options),
                (GenerateStubsOptions options) => Task.FromResult(RunGenerateStubs(options)),
                (NewCommandOptions options) => Task.FromResult(Commands.NewCommand.Run(options)),
                (StdlibBuildOptions options) => RunStdlibBuild(options),
                (FmtOptions options) => Task.FromResult(Commands.FmtCommand.Run(options)),
                (CleanOptions options) => Task.FromResult(Commands.CleanCommand.Run(options)),
                (TestOptions options) => Commands.TestCommand.Run(options),
                (BenchOptions options) => Commands.BenchCommand.Run(options),
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

    static async Task<int> RunStdlibBuild(StdlibBuildOptions options)
    {
        // Pre-compile standard library to .o files for faster linking
        var cpus = options.Cpu == "all"
            ? new[] { "68000", "68020", "68040", "68060" }
            : new[] { options.Cpu };

        var modes = options.Mode == "both"
            ? new[] { BuildMode.Debug, BuildMode.Release }
            : new[] { options.Mode == "release" ? BuildMode.Release : BuildMode.Debug };

        int totalSuccess = 0;
        int totalFailed = 0;

        foreach (var cpu in cpus)
        {
            foreach (var mode in modes)
            {
                var result = await Commands.StdlibBuildCommand.BuildForTarget(
                    cpu!,
                    mode,
                    options.VbccPath,
                    options.NdkPath,
                    options.Verbose,
                    CODEGEN_VERSION);

                if (result == 0)
                {
                    totalSuccess++;
                }
                else
                {
                    totalFailed++;
                }
            }
        }

        if (totalFailed > 0)
        {
            Console.WriteLine($"\nStdlib build completed with {totalFailed} failure(s)");
            return 1;
        }

        Console.WriteLine($"\n✓ Stdlib build completed successfully ({totalSuccess} target(s))");
        return 0;
    }

    // ModuleIR is now defined in Novus.Compilation.ModuleIR

    /// <summary>
    /// Compile a single Novus module to IR (without generating C code yet).
    /// This allows us to collect all modules first, then generate a shared types header.
    /// </summary>
    static async Task<ModuleIR?> CompileModuleToIR(
        string inputFile,
        string stdLibPath,
        CompilerOptions options,
        ModuleCache moduleCache,
        CircularImportDetector? circularImportDetector = null,
        CompilationCache? compilationCache = null)
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

            // Check compilation cache first (if enabled)
            if (compilationCache != null)
            {
                var configHash = CompilationCache.ComputeConfigHash(
                    options.Cpu,
                    options.Fpu,
                    options.OptimizationLevel,
                    options.BuildMode == BuildMode.Release ? "release" : "debug");

                var (cachedModule, cachedStringLiterals, cachedImports) =
                    compilationCache.GetCachedIrModule(inputFile, configHash);

                if (cachedModule != null && cachedStringLiterals != null && cachedImports != null)
                {
                    // Cache hit - return cached compilation result
                    if (options.Verbose)
                    {
                        Console.WriteLine($"  [Cache hit - IR] {Path.GetFileName(inputFile)}");
                    }

                    var cachedModuleName = Path.GetFileNameWithoutExtension(inputFile);
                    var cachedHasMain = cachedModule.Functions.Any(f => f.Name == "main" && !f.IsExtern);

                    return new ModuleIR(
                        inputFile,
                        cachedModuleName,
                        cachedModule,
                        cachedStringLiterals,
                        cachedImports,
                        cachedHasMain);
                }
            }

            var source = await File.ReadAllTextAsync(inputFile);

            // Inject package metadata constants ({PKG_NAME}, {PKG_VERSION})
            // Replace placeholder tokens in string literals with actual values
            if (!string.IsNullOrEmpty(options.PackageName))
            {
                source = source.Replace("{PKG_NAME}", options.PackageName);
            }
            if (!string.IsNullOrEmpty(options.PackageVersion))
            {
                source = source.Replace("{PKG_VERSION}", options.PackageVersion);
            }

            // Create diagnostic bag for error collection
            var diagnostics = new DiagnosticBag();

            // Run preprocessor
            var preprocessorConstants = GetPreprocessorConstants(options);
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
                // Cache miss - parse the file using factory
                var parser = NovusParserFactory.CreateParser(
                    source,
                    diagnostics,
                    inputFile,
                    NovusParserFactory.ParseMode.Compilation
                );

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

            // Check for IR building errors
            if (irBuilder.Diagnostics.HasErrors)
            {
                Console.WriteLine(irBuilder.Diagnostics.FormatDiagnostics());
                return null;
            }

            var moduleName = Path.GetFileNameWithoutExtension(inputFile);
            var hasMain = module.Functions.Any(f => f.Name == "main" && !f.IsExtern);

            // Cache the successful compilation result
            if (compilationCache != null)
            {
                var configHash = CompilationCache.ComputeConfigHash(
                    options.Cpu,
                    options.Fpu,
                    options.OptimizationLevel,
                    options.BuildMode == BuildMode.Release ? "release" : "debug");

                compilationCache.CacheCompilationResult(
                    inputFile,
                    compilationUnit,
                    module,
                    irBuilder.StringLiterals,
                    irBuilder.GetImportedModules(),
                    configHash,
                    hadErrors: false);
            }

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

            // Inject package metadata constants ({PKG_NAME}, {PKG_VERSION})
            // Replace placeholder tokens in string literals with actual values
            if (!string.IsNullOrEmpty(options.PackageName))
            {
                source = source.Replace("{PKG_NAME}", options.PackageName);
            }
            if (!string.IsNullOrEmpty(options.PackageVersion))
            {
                source = source.Replace("{PKG_VERSION}", options.PackageVersion);
            }

            // Create diagnostic bag for error collection
            var diagnostics = new DiagnosticBag();

            // Run preprocessor
            var preprocessorConstants = GetPreprocessorConstants(options);
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
                // Cache miss - parse the file using factory
                var parser = NovusParserFactory.CreateParser(
                    source,
                    diagnostics,
                    inputFile,
                    NovusParserFactory.ParseMode.Compilation
                );

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

            // Run required HIR lowering passes (ALWAYS run, regardless of optimization level)
            // These convert high-level DSLs (copper/blitter) to standard IR
            var requiredLoweringPipeline = Novus.Transforms.TransformPipeline.CreateLoweringPipeline(options.Verbose);
            requiredLoweringPipeline.Run(module);

            // Run optional transformation passes (only when optimizing)
            // Transformations can modify IR structure (inlining, monomorphization, etc.)
            if (options.OptimizationLevel > 0)
            {
                var transformPipeline = Novus.Transforms.TransformPipeline.CreateOptimizationPipeline(
                    enableInlining: options.OptimizationLevel >= 2, // Enable inlining at -O2 and above
                    verbose: options.Verbose
                );
                if (transformPipeline != null)
                {
                    transformPipeline.Run(module);
                }
            }

            // Run optimization passes
            if (options.PgoGenerate)
            {
                // PGO instrumentation mode - add profiling counters
                var instrumentationPipeline = Novus.Optimizer.OptimizationPipeline.CreateInstrumentationPipeline(options.Verbose);
                instrumentationPipeline.Run(module);

                if (options.Verbose)
                {
                    var instrPass = instrumentationPipeline.GetPass<Novus.Optimizer.Passes.InstrumentationPass>();
                    if (instrPass != null)
                    {
                        Console.WriteLine($"Instrumented: {instrPass.InstrumentationData.FunctionCounters.Count} functions, " +
                                          $"{instrPass.InstrumentationData.BranchCounters.Count} branches, " +
                                          $"{instrPass.InstrumentationData.LoopCounters.Count} loops");
                    }
                }
            }
            else if (!string.IsNullOrEmpty(options.PgoUse))
            {
                // PGO-guided optimization mode - use profile data
                Novus.Optimizer.ProfileData? profileData = null;
                try
                {
                    profileData = Novus.Optimizer.ProfileData.Load(options.PgoUse);
                    if (options.Verbose)
                    {
                        Console.WriteLine($"Loaded profile data from {options.PgoUse}");
                        Console.WriteLine($"  Run count: {profileData.RunCount}");
                        Console.WriteLine($"  Functions profiled: {profileData.Functions.Count}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: Failed to load profile data from {options.PgoUse}: {ex.Message}");
                    Console.Error.WriteLine("Falling back to standard optimization pipeline.");
                }

                if (profileData != null)
                {
                    var pgoPipeline = Novus.Optimizer.OptimizationPipeline.CreatePgoPipeline(
                        options.OptimizationLevel,
                        profileData,
                        options.Verbose
                    );
                    pgoPipeline.Run(module);
                }
                else if (options.OptimizationLevel > 0)
                {
                    // Fallback to standard pipeline if profile load failed
                    var optimizer = Novus.Optimizer.OptimizationPipeline.CreatePipeline(
                        options.OptimizationLevel,
                        options.Verbose
                    );
                    optimizer.Run(module);
                }
            }
            else if (options.OptimizationLevel > 0)
            {
                // Standard optimization pipeline
                var optimizer = Novus.Optimizer.OptimizationPipeline.CreatePipeline(
                    options.OptimizationLevel,
                    options.Verbose
                );
                optimizer.Run(module);
            }

            // Generate code (C or M68k assembly based on --backend flag)
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

            var safetyLevel = options.GetSafetyLevel();

            // Select code generation backend based on --backend flag
            string generatedCode;
            if (options.Backend == "m68k")
            {
                // Direct M68k assembly code generation
                var m68kCodegen = new Codegen.M68k.M68kCodeGenerator(module, irBuilder.StringLiterals, options.Cpu);
                generatedCode = m68kCodegen.Generate();
            }
            else
            {
                // C code generation (default)
                var cCodegen = new CCodeGenerator(module, irBuilder.StringLiterals, options.Cpu, options.Fpu, options.BuildMode, safetyLevel, explicitEntryPoints, false, options.PackageVersion);
                generatedCode = cCodegen.Generate();
            }

            return (generatedCode, irBuilder.GetImportedModules());
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
        Console.WriteLine("Novus Compiler");
        Console.WriteLine($"Target: {options.Cpu.ToUpper()}");
        Console.WriteLine($"FPU Mode: {options.Fpu}");
        Console.WriteLine("==================================\n");

        // EXPERIMENTAL WARNING: M68k backend is not production-ready
        if (options.Backend == "m68k")
        {
            Console.WriteLine("⚠ EXPERIMENTAL: The M68k direct assembly backend is experimental.");
            Console.WriteLine("  It may produce incorrect code or fail to compile valid programs.");
            Console.WriteLine("  The default 'c' backend (via VBCC) is recommended for production use.\n");
        }

        // Set build mode from --release flag FIRST (before computing safety level)
        // Safety level defaults depend on build mode, so this must come first!
        if (options.Release)
        {
            options.BuildMode = BuildMode.Release;
        }

        // Compute safety level from command-line options (uses build mode for default)
        var safetyLevel = options.GetSafetyLevel();

        // ============================================================================
        // STALE ARTIFACT DETECTION - Prevent bugs from stale stdlib sources
        // ============================================================================
        var compilerDir = AppContext.BaseDirectory;
        var staleFiles = Commands.StdlibBuildCommand.FindStaleSourceCopies(compilerDir);
        if (staleFiles.Count > 0)
        {
            Console.WriteLine($"⚠ WARNING: {staleFiles.Count} stdlib source file(s) in bin/ are STALE!");
            Console.WriteLine("  The project source tree has newer versions.");
            if (options.Verbose)
            {
                foreach (var file in staleFiles.Take(5))
                {
                    Console.WriteLine($"    - {file}");
                }
                if (staleFiles.Count > 5)
                {
                    Console.WriteLine($"    ... and {staleFiles.Count - 5} more");
                }
            }
            Console.WriteLine("  Auto-refreshing from project source tree...\n");
            Commands.StdlibBuildCommand.RefreshBinStdlib(compilerDir, options.Verbose);
        }

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

            // Find standard library path (compilerDir already defined above)
            var stdLibPath = Path.Combine(compilerDir, "std");

            // Create module cache for performance
            var moduleCache = new ModuleCache();

            // Create compilation cache for incremental compilation (unless --no-cache is specified)
            CompilationCache? compilationCache = null;
            if (!options.NoCache)
            {
                var projectRoot = Path.GetDirectoryName(Path.GetFullPath(options.InputFile)) ?? ".";
                compilationCache = new CompilationCache(projectRoot, CODEGEN_VERSION);
            }

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
            var mainIR = await CompileModuleToIR(options.InputFile, stdLibPath, options, moduleCache, circularImportDetector, compilationCache);
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

                var moduleIR = await CompileModuleToIR(modulePath, stdLibPath, options, moduleCache, circularImportDetector, compilationCache);
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

            // ============================================================================
            // PHASE 1.25: Validate IR correctness (debug builds only)
            // Catches malformed IR early, before optimization or code generation
            // ============================================================================
#if DEBUG
            if (options.Verbose)
            {
                Console.WriteLine("Validating IR...");
            }

            var irValidator = new Novus.IR.IrValidator();
            var validationErrors = new List<string>();

            // Validate main module
            var mainValidation = irValidator.Validate(mainIR.IrModule);
            if (!mainValidation.IsValid)
            {
                var moduleName = Path.GetFileNameWithoutExtension(options.InputFile);
                foreach (var error in mainValidation.Errors)
                {
                    validationErrors.Add($"[{moduleName}] {error}");
                }
            }

            // Validate all imported modules
            foreach (var (modulePath, moduleIR) in allModulesIR)
            {
                var result = irValidator.Validate(moduleIR.IrModule);
                if (!result.IsValid)
                {
                    var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                    foreach (var error in result.Errors)
                    {
                        validationErrors.Add($"[{moduleName}] {error}");
                    }
                }
            }

            if (validationErrors.Count > 0)
            {
                // IR validation found issues. In strict mode, these are fatal errors.
                // In normal mode, log as warnings to allow development to proceed.
                //
                // Known edge cases that may trigger validation warnings:
                // - Complex control flow in try/? operator handling
                // - Pattern matching in if-let/while-let constructs
                //
                // Enable strict validation via --strict-ir flag for debugging.
                bool strictMode = Environment.GetEnvironmentVariable("NOVUS_STRICT_IR") == "1";

                if (strictMode)
                {
                    Console.Error.WriteLine($"IR validation failed with {validationErrors.Count} errors:");
                    foreach (var error in validationErrors)
                    {
                        Console.Error.WriteLine($"  - {error}");
                    }
                    return 1;
                }

                if (options.Verbose)
                {
                    Console.WriteLine($"IR validation warnings: {validationErrors.Count}");
                    foreach (var error in validationErrors.Take(5))
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    if (validationErrors.Count > 5)
                    {
                        Console.WriteLine($"  ... and {validationErrors.Count - 5} more");
                    }
                }
            }

            if (options.Verbose)
            {
                Console.WriteLine($"  ✓ IR validated ({allModulesIR.Count + 1} modules)");
            }
#endif

            // ============================================================================
            // PHASE 1.5: Run HIR lowering passes on all modules
            // These convert high-level DSLs (copper/blitter) to standard IR
            // MUST run before code generation, regardless of optimization level
            // ============================================================================

            var loweringPipeline = Novus.Transforms.TransformPipeline.CreateLoweringPipeline(options.Verbose);

            // Run lowering on main module
            loweringPipeline.Run(mainIR.IrModule);

            // Run lowering on all imported modules
            foreach (var moduleIR in allModulesIR.Values)
            {
                loweringPipeline.Run(moduleIR.IrModule);
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

            // Determine output directory - NEVER write to repo root
            // Use a dedicated build directory for intermediate files
            var fullOutputPath = Path.GetFullPath(options.OutputFile);
            var outputFileDir = Path.GetDirectoryName(fullOutputPath);
            var baseName = Path.GetFileNameWithoutExtension(options.OutputFile);

            // If output file has no directory component, create a build directory
            string outputDir;
            if (string.IsNullOrEmpty(outputFileDir) || outputFileDir == Directory.GetCurrentDirectory())
            {
                // CRITICAL: Clean up stale novus-build directories from previous compilations
                // These can cause linker to pick up old object files with different static data
                CleanupStaleBuildDirectories();

                // Output is in current directory - use /tmp/novus-build-{pid} for intermediate files
                outputDir = Path.Combine(Path.GetTempPath(), $"novus-build-{Environment.ProcessId}");
                Directory.CreateDirectory(outputDir);
            }
            else
            {
                // Output has a specific directory - use that
                outputDir = outputFileDir;
            }

            // Helper function to compute file hash
            string ComputeFileHash(string filePath)
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hashBytes = sha256.ComputeHash(stream);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            // Helper function to compute string hash (avoids file I/O race conditions)
            string ComputeStringHash(string content)
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            // CRITICAL: Compute hash of types header BEFORE writing to avoid race conditions
            // If we write then read, an external process could modify the file between operations
            var typesHeaderHash = ComputeStringHash(sharedTypesHeader);

            // Write shared types header
            var typesHeaderPath = Path.Combine(outputDir, "novus_types.h");
            await File.WriteAllTextAsync(typesHeaderPath, sharedTypesHeader);

            // Generate C files - collect all file paths
            var cFiles = new List<string>();

            // Collect all code generators for statement-level debug symbol collection
            var allCodeGenerators = new List<CCodeGenerator>();

            // Main module: generate one C file per function (consistent with library modules)
            var mainFunctions = mainIR.IrModule.Functions
                .Where(f => !f.IsExtern && f.BasicBlocks.Count > 0)
                .ToList();

            if (mainFunctions.Count > 0)
            {
                var mainCodegen = new CCodeGenerator(
                    mainIR.IrModule,
                    mainIR.StringLiterals,
                    options.Cpu,
                    options.Fpu,
                    options.BuildMode,
                    safetyLevel: safetyLevel,
                    explicitEntryPoints: null,
                    useSharedTypesHeader: true,
                    projectVersion: options.PackageVersion);
                allCodeGenerators.Add(mainCodegen);

                // Generate one C file per function
                // Filter out functions with unresolved types to avoid symbol conflicts
                var generableMainFunctions = mainFunctions
                    .Where(f => !mainCodegen.HasUnresolvedTypes(f))
                    .ToList();

                foreach (var function in generableMainFunctions)
                {
                    var functionCCode = mainCodegen.GenerateFunctionFile(function);
                    // Always write the C file (even if it's a stub that panics)
                    // This ensures linking succeeds even if the function isn't called
                    // Sanitize function name for use in C filenames (replace :: with _ to match MangleName, and remove < > , & * etc.)
                    var sanitizedFunctionName = function.Name.Replace("::", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "").Replace("&", "ref_").Replace("*", "ptr_");
                    var functionCFile = Path.Combine(outputDir, $"{baseName}_{sanitizedFunctionName}.c");
                    await File.WriteAllTextAsync(functionCFile, functionCCode);
                    cFiles.Add(functionCFile);
                }

                // Generate statics file if module has static variables
                var staticsCCode = mainCodegen.GenerateStaticsFile();
                if (!string.IsNullOrEmpty(staticsCCode))
                {
                    var staticsCFile = Path.Combine(outputDir, $"{baseName}_statics.c");
                    await File.WriteAllTextAsync(staticsCFile, staticsCCode);
                    cFiles.Add(staticsCFile);
                }

                // Generate exports header if there are any exported functions
                var exportsHeader = mainCodegen.GenerateHeader();
                var exportedFunctions = mainIR.IrModule.Functions.Where(f => f.IsExported && !f.IsExtern).ToList();
                if (exportedFunctions.Count > 0)
                {
                    var exportsHeaderPath = Path.Combine(outputDir, $"{baseName}_exports.h");
                    await File.WriteAllTextAsync(exportsHeaderPath, exportsHeader);
                    Console.WriteLine($"  → {baseName}_exports.h ({exportedFunctions.Count} exported function{(exportedFunctions.Count > 1 ? "s" : "")})");
                }
                else if (options.Verbose)
                {
                    // Debug: show which functions exist and their export status
                    Console.WriteLine($"  Debug: {mainIR.IrModule.Functions.Count} functions in module:");
                    foreach (var f in mainIR.IrModule.Functions.Take(10))
                    {
                        Console.WriteLine($"    - {f.Name}: IsExported={f.IsExported}, IsExtern={f.IsExtern}");
                    }
                }

                Console.WriteLine($"  → {baseName} ({mainFunctions.Count} function{(mainFunctions.Count > 1 ? "s" : "")})");
            }

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

                    // Generate client call stubs for calling the library from other programs
                    var callStubAsm = libraryGen.GenerateClientCallStubs();
                    var callStubFile = Path.Combine(outputDir, $"{baseName}_calls.s");
                    await File.WriteAllTextAsync(callStubFile, callStubAsm);
                    Console.WriteLine($"  → {Path.GetFileName(callStubFile)} (client call stubs)");

                    // Generate default lifecycle functions (LibInit, LibOpen, LibClose, LibExpunge, etc)
                    var lifecycleCCode = libraryGen.GenerateDefaultLifecycleFunctions();
                    if (!string.IsNullOrEmpty(lifecycleCCode))
                    {
                        var lifecycleCFile = Path.Combine(outputDir, $"{baseName}_lifecycle.c");
                        await File.WriteAllTextAsync(lifecycleCFile, lifecycleCCode);
                        cFiles.Add(lifecycleCFile);
                        Console.WriteLine($"  → {Path.GetFileName(lifecycleCFile)} (lifecycle functions)");
                    }
                }
            }

            // Track monomorphized functions across all modules to generate each one exactly once
            // Key: mangled function name, Value: (module name, function)
            var generatedMonomorphizedFunctions = new Dictionary<string, (string moduleName, IrFunction function, CCodeGenerator codegen)>();

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
                    options.BuildMode,
                    safetyLevel: safetyLevel,
                    explicitEntryPoints: null,
                    useSharedTypesHeader: true,
                    projectVersion: options.PackageVersion);
                allCodeGenerators.Add(moduleCodegen);

                // Generate one C file per function
                // Filter out functions with unresolved types to avoid symbol conflicts
                var generableFunctions = functions
                    .Where(f =>
                    {
                        bool hasUnresolved = moduleCodegen.HasUnresolvedTypes(f);
                        if (hasUnresolved)
                        {
                            Console.WriteLine($"    [FILTERED OUT] {f.Name} has unresolved types");
                        }
                        return !hasUnresolved;
                    })
                    .ToList();

                foreach (var function in generableFunctions)
                {
                    // Check if this is a monomorphized function (trait method or static generic function)
                    bool isMonomorphized = moduleCodegen.IsMonomorphizedFunction(function);
                    var mangledName = moduleCodegen.MangleName(function);

                    if (isMonomorphized)
                    {
                        // If we've already generated this monomorphized function, skip it
                        if (generatedMonomorphizedFunctions.ContainsKey(mangledName))
                        {
                            Console.WriteLine($"    [SKIPPED DUPLICATE] {mangledName} (already in {generatedMonomorphizedFunctions[mangledName].moduleName})");
                            continue;
                        }

                        // Track this monomorphized function
                        generatedMonomorphizedFunctions[mangledName] = (moduleName, function, moduleCodegen);
                    }

                    var functionCCode = moduleCodegen.GenerateFunctionFile(function);
                    // Always write the C file (even if it's a stub that panics)
                    // This ensures linking succeeds even if the function isn't called
                    // Sanitize function name for use in C filenames (replace :: with _ to match MangleName, and remove < > , & * etc.)
                    var sanitizedFunctionName = function.Name.Replace("::", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "").Replace("&", "ref_").Replace("*", "ptr_");
                    var functionCFile = Path.Combine(outputDir, $"{moduleName}_{sanitizedFunctionName}.c");
                    await File.WriteAllTextAsync(functionCFile, functionCCode);
                    cFiles.Add(functionCFile);
                }

                // Generate statics file if module has static variables
                var staticsCCode = moduleCodegen.GenerateStaticsFile();
                if (!string.IsNullOrEmpty(staticsCCode))
                {
                    var staticsCFile = Path.Combine(outputDir, $"{moduleName}_statics.c");
                    await File.WriteAllTextAsync(staticsCFile, staticsCCode);
                    cFiles.Add(staticsCFile);
                }

                var displayName = isStdModule ? $"std::{moduleName}" : moduleName;
                Console.WriteLine($"  → {displayName} ({functions.Count} function{(functions.Count > 1 ? "s" : "")})");
            }

            // Always generate debug_symbols.c - it provides __novus_init_debug_symbols()
            // which is called from the runtime. In Paranoid safety mode it contains real symbols,
            // otherwise it's a no-op stub that still provides the required function.
            {
                string debugSymbolsCode;
                if (safetyLevel >= SafetyLevel.Paranoid)
                {
                    var allModules = new List<IrModule> { mainIR.IrModule };
                    allModules.AddRange(allModulesIR.Values.Select(m => m.IrModule));

                    // Collect statement-level debug markers from all code generators
                    var allDebugMarkers = new List<(string LabelName, string FileName, int Line, string FuncName)>();
                    foreach (var codegen in allCodeGenerators)
                    {
                        allDebugMarkers.AddRange(codegen.GetDebugLineMarkers());
                    }

                    debugSymbolsCode = CCodeGenerator.GenerateDebugSymbolsFile(allModules, allDebugMarkers);
                    var markerCount = allDebugMarkers.Count;
                    Console.WriteLine($"  → debug_symbols.c ({markerCount} statement marker{(markerCount != 1 ? "s" : "")} for precise line info)");
                }
                else
                {
                    // Generate stub for non-debug builds
                    debugSymbolsCode = "// No debug symbols in release build\n" +
                                       "void __novus_init_debug_symbols(void) { /* no-op */ }\n";
                }
                var debugSymbolsCFile = Path.Combine(outputDir, "debug_symbols.c");
                await File.WriteAllTextAsync(debugSymbolsCFile, debugSymbolsCode);
                cFiles.Add(debugSymbolsCFile);
            }

            // Add Novus runtime library (split into separate files for better DCE)
            // The linker can eliminate entire unused .o files, so splitting the runtime
            // into logical groups allows programs that don't use (e.g.) MMU protection
            // to avoid linking that code entirely.
            var runtimeDir = Path.Combine(compilerDir, "runtime");
            var runtimeFiles = new[]
            {
                "runtime_core.c",      // Core: memset, memcpy, strlen, error display
                "runtime_errors.c",    // Assert, panic, bounds check, div check
                "runtime_hwdetect.c",  // CPU, FPU, chipset detection
                "runtime_fmt.c",       // Integer to string conversions
                "runtime_semaphore.c", // Semaphore wrappers
                "runtime_mmu.c",       // MMU detection and null page protection
                "runtime_memtrack.c",  // Memory tracking for debugging
            };

            foreach (var runtimeFile in runtimeFiles)
            {
                var runtimeCFile = Path.Combine(runtimeDir, runtimeFile);
                if (File.Exists(runtimeCFile))
                {
                    cFiles.Add(runtimeCFile);
                }
            }
            Console.WriteLine($"  → {runtimeFiles.Length} runtime modules (split for DCE)");

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
                // Only executables need startup code and library initialization
                var coreFiles = new[] { "novus_startup", "library_bases", "dos_init", "graphics_init", "debug_gfxbase" };
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

                // Generate per-program stack configuration based on #[stack_size(N)] attribute
                // Uses the stack size from the main module (default 65536 if not specified)
                var stackSize = mainIR.IrModule.StackSize;
                var stackConfigSource = Path.Combine(outputDir, "stack_config.s");
                var stackConfigAsm = $@"; Auto-generated stack configuration for this program
; Stack size: {stackSize} bytes (from #[stack_size] attribute or default)
	section	""__MERGED"",data

	; AmigaOS $STACK: cookie - the loader scans the executable for this string
	; and automatically allocates the specified stack size BEFORE running the program
	xdef	___stack_cookie
___stack_cookie:
	dc.b	'$STACK:{stackSize}',0
	even

	; VBCC ___stack symbol - for compatibility with VBCC's startup code
	xdef	___stack
___stack:
	dc.l	{stackSize}
";
                await File.WriteAllTextAsync(stackConfigSource, stackConfigAsm);

                var stackConfigObj = Path.Combine(outputDir, "stack_config.o");
                if (!await toolchain.Assemble(stackConfigSource, stackConfigObj, assemblyCpu, false))
                {
                    Console.WriteLine("Failed to assemble stack_config");
                    return 1;
                }
                objectFiles.Add(stackConfigObj);
            }

            // Assemble runtime library assembly files (needed for all project types)
            var runtimeAsmFiles = new[] { "novus_io" };
            foreach (var runtimeFile in runtimeAsmFiles)
            {
                var runtimeSource = Path.Combine(compilerDir, "..", "..", "..", "runtime", $"{runtimeFile}.s");
                if (File.Exists(runtimeSource))
                {
                    var runtimeObj = Path.Combine(outputDir, $"{runtimeFile}.o");
                    if (!await toolchain.Assemble(runtimeSource, runtimeObj, assemblyCpu, false))
                    {
                        Console.WriteLine($"Failed to assemble {runtimeFile}");
                        return 1;
                    }
                    objectFiles.Add(runtimeObj);
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

                // Libraries need library base objects for AmigaOS calls
                // These provide the global _SysBase and _DOSBase symbols
                if (true) // Always include for libraries
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

                    // Skip wrapper files - they're already assembled and added above
                    if (asmFileName.EndsWith("_wrappers.s") || asmFileName.EndsWith("_lib.s"))
                    {
                        continue;
                    }

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

            // ============================================================================
            // OPTIMIZATION: Use pre-compiled stdlib .o files if available
            // Auto-compile stdlib on first use for this CPU/mode combination
            // ============================================================================

            var buildModeStr = options.BuildMode == BuildMode.Release ? "release" : "debug";
            var stdlibPrecompiledDir = Path.Combine(compilerDir, "stdlib", assemblyCpu, buildModeStr);

            // Check if stdlib cache should be used
            // By default, always rebuild stdlib fresh to avoid stale cache issues
            // Use --use-stdlib-cache to opt-in to using cached stdlib
            // Use --rebuild-stdlib-cache to rebuild and cache for future use
            bool forceRebuildAndCache = options.RebuildStdlibCache;
            bool useCache = options.UseStdlibCache && !forceRebuildAndCache;
            string? cacheInvalidReason = null;
            bool needsRebuild = forceRebuildAndCache
                || !useCache
                || !Directory.Exists(stdlibPrecompiledDir)
                || Commands.StdlibBuildCommand.NeedsRebuild(compilerDir, assemblyCpu, options.BuildMode, CODEGEN_VERSION, out cacheInvalidReason);

            // CRITICAL FIX: If stdlib cache is stale, delete ALL cached .o files
            // This prevents using stale object files with old constant values
            if (needsRebuild && Directory.Exists(stdlibPrecompiledDir))
            {
                var reason = cacheInvalidReason ?? (forceRebuildAndCache ? "forced rebuild" : "cache not used by default");
                Console.WriteLine($"\n⚠ Stdlib cache invalidated: {reason}");
                Console.WriteLine($"  Clearing cached stdlib objects for {assemblyCpu}/{buildModeStr}...");
                try
                {
                    // Delete all .o files in the cache directory
                    var cachedOFiles = Directory.GetFiles(stdlibPrecompiledDir, "*.o", SearchOption.TopDirectoryOnly);
                    foreach (var oFile in cachedOFiles)
                    {
                        File.Delete(oFile);
                    }
                    // Delete the manifest to force rebuild
                    var manifestPath = Path.Combine(stdlibPrecompiledDir, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        File.Delete(manifestPath);
                    }
                    Console.WriteLine($"  ✓ Deleted {cachedOFiles.Length} stale object file(s)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Warning: Failed to clear cache: {ex.Message}");
                }
            }

            var usePrecompiledStdlib = !needsRebuild;

            // Build mapping of C files to their generated content hashes for caching
            // Format: cFile -> (sourcePath, cFileHash)
            // IMPORTANT: We hash the generated C file content, NOT the source Novus file,
            // because compiler changes can alter C codegen without changing the source
            var cFileToSource = new Dictionary<string, (string path, string hash)>();

            // Map main module functions to source file
            var mainSourcePath = options.InputFile;
            foreach (var cFile in cFiles)
            {
                var cFileName = Path.GetFileNameWithoutExtension(cFile);
                if (cFileName.StartsWith(baseName + "_"))
                {
                    // Hash the generated C file content (not the source)
                    var cFileHash = ComputeFileHash(cFile);
                    cFileToSource[cFile] = (mainSourcePath, cFileHash);
                }
            }

            // Map imported module functions to their source files
            foreach (var (modulePath, moduleIR) in allModulesIR)
            {
                var moduleName = moduleIR.ModuleName;

                foreach (var cFile in cFiles)
                {
                    var cFileName = Path.GetFileNameWithoutExtension(cFile);
                    if (cFileName.StartsWith(moduleName + "_"))
                    {
                        // Hash the generated C file content (not the source)
                        var cFileHash = ComputeFileHash(cFile);
                        cFileToSource[cFile] = (modulePath, cFileHash);
                    }
                }
            }

            // Separate stdlib C files from user C files
            var stdlibCFiles = new List<string>();
            var userCFiles = new List<string>();

            foreach (var cFile in cFiles)
            {
                if (cFileToSource.TryGetValue(cFile, out var sourceInfo))
                {
                    var (sourcePath, _) = sourceInfo;
                    if (sourcePath.Contains("/std/"))
                    {
                        stdlibCFiles.Add(cFile);
                    }
                    else
                    {
                        userCFiles.Add(cFile);
                    }
                }
                else
                {
                    // No source mapping (e.g., runtime files) - treat as user code
                    userCFiles.Add(cFile);
                }
            }

            // Step 1: Link pre-compiled stdlib .o files (if available) or compile stdlib from scratch
            var stdlibOFilesToCache = new List<(string source, string obj)>();

            if (usePrecompiledStdlib && stdlibCFiles.Count > 0)
            {
                Console.WriteLine($"\nUsing pre-compiled stdlib modules ({stdlibCFiles.Count} files)...");

                // Map stdlib C files to their corresponding .o files
                var precompiledFiles = new HashSet<string>();
                foreach (var cFile in stdlibCFiles)
                {
                    var cFileName = Path.GetFileNameWithoutExtension(cFile);
                    var precompiledObj = Path.Combine(stdlibPrecompiledDir, $"{cFileName}.o");

                    if (File.Exists(precompiledObj))
                    {
                        objectFiles.Add(precompiledObj);
                        precompiledFiles.Add(cFileName);
                    }
                }

                Console.WriteLine($"  ✓ Linked {precompiledFiles.Count} pre-compiled stdlib object files");

                // If some stdlib files are missing from precompiled dir, compile and cache them
                if (precompiledFiles.Count < stdlibCFiles.Count)
                {
                    Console.WriteLine($"  → Compiling {stdlibCFiles.Count - precompiledFiles.Count} missing stdlib files...");

                    // Compile missing stdlib files
                    foreach (var cFile in stdlibCFiles)
                    {
                        var cFileName = Path.GetFileNameWithoutExtension(cFile);
                        if (!precompiledFiles.Contains(cFileName))
                        {
                            var objFile = Path.Combine(outputDir, cFileName + ".o");
                            Console.WriteLine($"    → {Path.GetFileName(cFile)}");

                            if (!await toolchain.CompileToObject(cFile, objFile, assemblyCpu, options.OptimizationLevel, options.BuildMode))
                            {
                                Console.WriteLine($"\n✗ Failed to compile {Path.GetFileName(cFile)}");
                                return 1;
                            }

                            objectFiles.Add(objFile);
                            stdlibOFilesToCache.Add((cFile, objFile));  // Mark for caching
                        }
                    }
                }
            }
            else if (stdlibCFiles.Count > 0)
            {
                // No pre-compiled stdlib cache exists - compile and cache all stdlib files
                Console.WriteLine($"\nCompiling stdlib modules ({stdlibCFiles.Count} files)...");
                foreach (var cFile in stdlibCFiles)
                {
                    var cFileName = Path.GetFileName(cFile);
                    var objFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(cFile) + ".o");

                    Console.WriteLine($"  → {cFileName}");
                    if (!await toolchain.CompileToObject(cFile, objFile, assemblyCpu, options.OptimizationLevel, options.BuildMode))
                    {
                        Console.WriteLine($"\n✗ Failed to compile {cFileName}");
                        return 1;
                    }

                    objectFiles.Add(objFile);
                    stdlibOFilesToCache.Add((cFile, objFile));  // Mark for caching
                }
            }

            // Cache any newly compiled stdlib .o files (from either path above)
            if (stdlibOFilesToCache.Count > 0)
            {
                Console.WriteLine($"\n  ✓ Caching {stdlibOFilesToCache.Count} stdlib object files for future builds...");
                Directory.CreateDirectory(stdlibPrecompiledDir);

                foreach (var (source, obj) in stdlibOFilesToCache)
                {
                    var cachedPath = Path.Combine(stdlibPrecompiledDir, Path.GetFileName(obj));
                    File.Copy(obj, cachedPath, overwrite: true);
                }

                // Write manifest with source file hashes for cache invalidation
                var stdlibSourcePaths = allModulesIR
                    .Where(kvp => kvp.Key.Contains("/std/"))
                    .Select(kvp => kvp.Key)
                    .ToList();

                await Commands.StdlibBuildCommand.WriteManifest(
                    stdlibPrecompiledDir,
                    assemblyCpu,
                    options.BuildMode,
                    stdlibSourcePaths,
                    CODEGEN_VERSION);
            }

            // ============================================================================
            // DETECT REQUIRED LIBRARIES: Scan C code to determine which stubs to link
            // ============================================================================

            // Detect which library stubs are actually needed by scanning generated C code
            Console.WriteLine("\n=== DEBUG: Detecting required libraries from C code ===");

            var requiredLibraries = new HashSet<string>();

            // Always include exec (needed for basic Amiga operations like AllocMem/FreeMem)
            requiredLibraries.Add("exec");
            Console.WriteLine("  ✓ Always including 'exec' library");

            // Scan generated C files for DOS and Intuition library function calls
            // Note: DOS is NOT unconditionally included - only if actually used
            // Also scan stdlib C files to detect library dependencies from stdlib modules
            var allCFilesToScan = new List<string>(cFiles);
            allCFilesToScan.AddRange(stdlibCFiles);

            foreach (var cFile in allCFilesToScan)
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
                }

                // Check for Intuition library function calls (used by assert handler)
                if (cCode.Contains("EasyRequest") ||
                    cCode.Contains("__novus_assert_failed"))
                {
                    requiredLibraries.Add("intuition");
                    Console.WriteLine($"  ✓ Detected Intuition library usage in {Path.GetFileName(cFile)}");
                }

                // Check for Graphics library function calls
                if (cCode.Contains("LoadView(") ||
                    cCode.Contains("WaitTOF(") ||
                    cCode.Contains("WaitBlit(") ||
                    cCode.Contains("SetRast(") ||
                    cCode.Contains("BltBitMap(") ||
                    cCode.Contains("Text(") ||
                    cCode.Contains("_GfxBase"))
                {
                    requiredLibraries.Add("graphics");
                    Console.WriteLine($"  ✓ Detected Graphics library usage in {Path.GetFileName(cFile)}");
                }
            }

            Console.WriteLine($"=== Required libraries: {string.Join(", ", requiredLibraries)} ===\n");

            // NOTE: Library bases are handled differently for different project types:
            // - Executables: Use library_bases.s (provides both _SysBase and _DOSBase) - assembled at line 627
            // - Libraries: Use exec_base.s and dos_base.s separately - assembled at line 697
            // No additional dos_base assembly needed here

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

                        // NOTE: dos_init.o is already assembled earlier for executables
                        // No need to assemble it again here
                    }
                }
            }

            // Step 2: Compile user C files with caching and parallelization
            Console.WriteLine("\nCompiling user code...");

            // Create user code cache directory: {outputDir}/usercache/{cpu}/{buildMode}/
            var userCacheDir = Path.Combine(outputDir, "usercache", assemblyCpu, buildModeStr);
            Directory.CreateDirectory(userCacheDir);

            // Validate cache version and clean if stale
            ValidateAndCleanCache(userCacheDir, CODEGEN_VERSION);

            // Track which files need compilation
            var filesToCompile = new List<(string cFile, string objFile, bool cached)>();

            foreach (var cFile in userCFiles)
            {
                var cFileName = Path.GetFileNameWithoutExtension(cFile);
                var objFile = Path.Combine(outputDir, cFileName + ".o");

                // Check if we have a cached .o file
                // Note: _statics.c files are NEVER cached - they must be compiled fresh
                var cached = false;
                if (!cFileName.EndsWith("_statics") && cFileToSource.TryGetValue(cFile, out var sourceInfo))
                {
                    var (sourcePath, cFileHash) = sourceInfo;
                    // Cache key must match the format used when caching: codegen version + C file hash + header + CPU + FPU + buildmode + optlevel
                    // We use C file hash (not source hash) to detect compiler codegen changes
                    // CRITICAL: Include FPU mode and build mode to prevent incorrect cache hits
                    var cacheKey = $"v{CODEGEN_VERSION}_{cFileHash}_{typesHeaderHash.Substring(0, 8)}_{assemblyCpu}_{options.Fpu}_{buildModeStr}_O{options.OptimizationLevel}_{cFileName}.o";
                    var cachedObjFile = Path.Combine(userCacheDir, cacheKey);
                    var cachedMetaFile = cachedObjFile + ".meta";

                    // Validate cached object file exists and is not corrupted
                    if (File.Exists(cachedObjFile) && File.Exists(cachedMetaFile))
                    {
                        try
                        {
                            // Read metadata
                            var metaJson = File.ReadAllText(cachedMetaFile);
                            var meta = System.Text.Json.JsonSerializer.Deserialize(metaJson, CacheMetadataJsonContext.Default.CacheMetadata);

                            // Verify object file integrity
                            if (meta != null)
                            {
                                var objInfo = new FileInfo(cachedObjFile);
                                var objHash = ComputeFileHash(cachedObjFile);

                                if (meta.FileSize == objInfo.Length && meta.FileHash == objHash)
                                {
                                    // Cache is valid - use it
                                    File.Copy(cachedObjFile, objFile, overwrite: true);
                                    objectFiles.Add(objFile);
                                    cached = true;
                                }
                                else
                                {
                                    // Cache corrupted - delete both files
                                    File.Delete(cachedObjFile);
                                    File.Delete(cachedMetaFile);
                                }
                            }
                        }
                        catch
                        {
                            // Metadata read/parse failed - delete cache
                            try
                            {
                                if (File.Exists(cachedObjFile)) File.Delete(cachedObjFile);
                                if (File.Exists(cachedMetaFile)) File.Delete(cachedMetaFile);
                            }
                            catch { /* Best effort cleanup */ }
                        }
                    }
                }

                filesToCompile.Add((cFile, objFile, cached));
            }

            // Count how many are cached vs need compilation
            var cachedCount = filesToCompile.Count(f => f.cached);
            var compileCount = filesToCompile.Count - cachedCount;

            if (cachedCount > 0)
            {
                Console.WriteLine($"  ✓ Using {cachedCount} cached object file{(cachedCount > 1 ? "s" : "")}");
            }

            if (compileCount > 0)
            {
                Console.WriteLine($"  → Compiling {compileCount} file{(compileCount > 1 ? "s" : "")}...");

                // Compile files in parallel
                var compileTasks = filesToCompile
                    .Where(f => !f.cached)
                    .Select(async f =>
                    {
                        var (cFile, objFile, _) = f;
                        var cFileName = Path.GetFileName(cFile);

                        // Compile
                        var success = await toolchain.CompileToObject(cFile, objFile, assemblyCpu, options.OptimizationLevel, options.BuildMode);
                        if (!success)
                        {
                            return (success: false, cFileName, objFile, cFile, cacheInfo: (string.Empty, string.Empty));
                        }

                        // Prepare cache info (but don't copy yet - wait until all succeed)
                        // Note: _statics.c files are NEVER cached - skip them
                        var cacheInfo = ("", "");
                        var cFileNameNoExt = Path.GetFileNameWithoutExtension(cFile);
                        if (!cFileNameNoExt.EndsWith("_statics") && cFileToSource.TryGetValue(cFile, out var sourceInfo))
                        {
                            var (sourcePath, cFileHash) = sourceInfo;
                            // Cache key includes: codegen version + C file hash + types header hash + CPU + FPU + buildmode + optimization level
                            // We use C file hash (not source hash) to detect compiler codegen changes
                            // This ensures cache invalidation when any of these change
                            // CRITICAL: Include FPU mode and build mode to prevent incorrect cache hits
                            var cacheKey = $"v{CODEGEN_VERSION}_{cFileHash}_{typesHeaderHash.Substring(0, 8)}_{assemblyCpu}_{options.Fpu}_{buildModeStr}_O{options.OptimizationLevel}_{cFileNameNoExt}.o";
                            var cachedObjFile = Path.Combine(userCacheDir, cacheKey);
                            cacheInfo = (objFile, cachedObjFile);
                        }

                        return (success: true, cFileName, objFile, cFile, cacheInfo);
                    })
                    .ToArray();

                // Wait for all compilations to complete
                var results = await Task.WhenAll(compileTasks);

                // Check for failures
                var failures = results.Where(r => !r.success).ToList();
                if (failures.Any())
                {
                    Console.WriteLine($"\n✗ Failed to compile:");
                    foreach (var failure in failures)
                    {
                        Console.WriteLine($"  → {failure.cFileName}");
                    }
                    return 1;
                }

                // All compilations succeeded - now cache the .o files and add to objectFiles list
                foreach (var result in results.Where(r => r.success))
                {
                    // Cache the .o file if applicable
                    if (!string.IsNullOrEmpty(result.cacheInfo.Item1))
                    {
                        var objFile = result.cacheInfo.Item1;
                        var cachedObjFile = result.cacheInfo.Item2;
                        var cachedMetaFile = cachedObjFile + ".meta";

                        // Copy object file to cache
                        File.Copy(objFile, cachedObjFile, overwrite: true);

                        // Write metadata for integrity validation
                        var objInfo = new FileInfo(objFile);
                        var objHash = ComputeFileHash(objFile);
                        var metadata = new CacheMetadata
                        {
                            FileSize = objInfo.Length,
                            FileHash = objHash,
                            CachedAt = DateTime.UtcNow
                        };

                        var metaJson = System.Text.Json.JsonSerializer.Serialize(metadata, CacheMetadataJsonContext.Default.CacheMetadata);
                        File.WriteAllText(cachedMetaFile, metaJson);
                    }

                    // Thread-safe: Add to objectFiles list sequentially after all compilations complete
                    objectFiles.Add(result.objFile);

                    // Show what was compiled
                    Console.WriteLine($"    → {result.cFileName}");
                }
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

            // Detect and add vendor library dependencies
            // Check if ptplayer (MOD player) is being used
            var usesPtplayer = processed.Any(p => p.Contains("ptplayer")) ||
                               mainIR.ImportedModules.Any(m => m.Contains("ptplayer"));
            if (usesPtplayer)
            {
                // Look for vendor files - check multiple possible locations
                // compilerDir is bin/Debug/net8.0/ so we need to find the repo root
                string? vendorDir = null;

                // First try: alongside the compiler binary (for installed compiler)
                var candidateDir = Path.Combine(compilerDir, "vendor", "ptplayer");
                if (Directory.Exists(candidateDir))
                {
                    vendorDir = candidateDir;
                }
                else
                {
                    // Try development paths - go up from bin/Debug/net8.0 to repo root
                    // bin/Debug/net8.0 -> bin/Debug -> bin -> Novus -> Novus (repo root)
                    var dir = compilerDir;
                    for (int i = 0; i < 5 && dir != null; i++)
                    {
                        dir = Path.GetDirectoryName(dir);
                        if (dir != null)
                        {
                            candidateDir = Path.Combine(dir, "vendor", "ptplayer");
                            if (Directory.Exists(candidateDir))
                            {
                                vendorDir = candidateDir;
                                break;
                            }
                        }
                    }
                }

                vendorDir ??= Path.Combine(compilerDir, "vendor", "ptplayer");

                var ptplayerAsm = Path.Combine(vendorDir, "ptplayer.asm");
                var ptplayerStubsAsm = Path.Combine(vendorDir, "ptplayer_stubs.asm");
                var ptplayerHelpersC = Path.Combine(vendorDir, "ptplayer_helpers.c");
                var ptplayerObj = Path.Combine(vendorDir, "ptplayer.o");
                var ptplayerStubsObj = Path.Combine(vendorDir, "ptplayer_stubs.o");
                var ptplayerHelpersObj = Path.Combine(vendorDir, "ptplayer_helpers.o");

                // Auto-build ptplayer object files if source is newer or objects don't exist
                async Task<bool> BuildPtplayerIfNeeded()
                {
                    var needsBuild = new List<(string src, string obj, string type)>();

                    // Check each source file
                    if (File.Exists(ptplayerAsm))
                    {
                        if (!File.Exists(ptplayerObj) || File.GetLastWriteTime(ptplayerAsm) > File.GetLastWriteTime(ptplayerObj))
                            needsBuild.Add((ptplayerAsm, ptplayerObj, "asm"));
                    }
                    if (File.Exists(ptplayerStubsAsm))
                    {
                        if (!File.Exists(ptplayerStubsObj) || File.GetLastWriteTime(ptplayerStubsAsm) > File.GetLastWriteTime(ptplayerStubsObj))
                            needsBuild.Add((ptplayerStubsAsm, ptplayerStubsObj, "asm"));
                    }
                    if (File.Exists(ptplayerHelpersC))
                    {
                        if (!File.Exists(ptplayerHelpersObj) || File.GetLastWriteTime(ptplayerHelpersC) > File.GetLastWriteTime(ptplayerHelpersObj))
                            needsBuild.Add((ptplayerHelpersC, ptplayerHelpersObj, "c"));
                    }

                    if (needsBuild.Count > 0)
                    {
                        Console.WriteLine($"  → Building {needsBuild.Count} ptplayer object file(s)...");
                        foreach (var (src, obj, type) in needsBuild)
                        {
                            bool success;
                            if (type == "asm")
                            {
                                success = await toolchain.AssembleFile(src, obj);
                            }
                            else
                            {
                                success = await toolchain.CompileCFile(src, obj, options.BuildMode);
                            }
                            if (!success)
                            {
                                Console.WriteLine($"    ✗ Failed to build {Path.GetFileName(obj)}");
                                return false;
                            }
                            Console.WriteLine($"    → {Path.GetFileName(obj)}");
                        }
                    }
                    return true;
                }

                if (!await BuildPtplayerIfNeeded())
                {
                    Console.WriteLine("  ⚠ Warning: Failed to build ptplayer objects");
                }
                else if (File.Exists(ptplayerObj) && File.Exists(ptplayerStubsObj) && File.Exists(ptplayerHelpersObj))
                {
                    Console.WriteLine("  → Detected ptplayer usage - adding MOD player library");
                    objectFiles.Add(ptplayerObj);
                    objectFiles.Add(ptplayerStubsObj);
                    objectFiles.Add(ptplayerHelpersObj);
                }
                else
                {
                    Console.WriteLine("  ⚠ Warning: ptplayer used but vendor/ptplayer source files not found");
                    Console.WriteLine($"    Expected: {ptplayerAsm}");
                    Console.WriteLine($"    Expected: {ptplayerStubsAsm}");
                    Console.WriteLine($"    Expected: {ptplayerHelpersC}");
                }
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
                isLibrary: isLibrary || isDevice,  // libraries and devices need relocations
                buildMode: options.BuildMode
            );

            if (success)
            {
                Console.WriteLine($"\n✓ Successfully created: {Path.GetFileName(exeFile)}");

                // Display cache statistics if requested
                if (options.CacheStats && compilationCache != null)
                {
                    var (parseHits, parseMisses, irHits, irMisses, totalFiles) = compilationCache.GetStats();
                    var totalAttempts = parseHits + parseMisses + irHits + irMisses;
                    var totalHits = parseHits + irHits;

                    if (totalAttempts > 0)
                    {
                        var hitRate = (double)totalHits / totalAttempts * 100.0;
                        Console.WriteLine("\n=== Compilation Cache Statistics ===");
                        Console.WriteLine($"Parse cache:  {parseHits} hits, {parseMisses} misses");
                        Console.WriteLine($"IR cache:     {irHits} hits, {irMisses} misses");
                        Console.WriteLine($"Overall:      {totalHits}/{totalAttempts} ({hitRate:F1}% hit rate)");
                        Console.WriteLine($"Cached files: {totalFiles}");
                    }
                }

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

    /// <summary>
    /// Clean up stale novus-build directories from previous compilations.
    /// This prevents the linker from accidentally picking up old object files
    /// with different static data (e.g., embedded MOD files from other builds).
    /// </summary>
    private static void CleanupStaleBuildDirectories()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var currentPid = Environment.ProcessId;
            var currentBuildDir = $"novus-build-{currentPid}";

            // Find all novus-build-* directories
            var buildDirs = Directory.GetDirectories(tempPath, "novus-build-*");
            var deletedCount = 0;

            foreach (var dir in buildDirs)
            {
                var dirName = Path.GetFileName(dir);

                // Don't delete our own build directory
                if (dirName == currentBuildDir)
                    continue;

                // Extract PID from directory name
                if (dirName.StartsWith("novus-build-") &&
                    int.TryParse(dirName.Substring("novus-build-".Length), out var pid))
                {
                    // Check if the process is still running
                    bool processRunning = false;
                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById(pid);
                        processRunning = !process.HasExited;
                    }
                    catch
                    {
                        // Process doesn't exist - safe to delete
                        processRunning = false;
                    }

                    if (!processRunning)
                    {
                        try
                        {
                            Directory.Delete(dir, recursive: true);
                            deletedCount++;
                        }
                        catch
                        {
                            // Best effort - might be locked by another process
                        }
                    }
                }
            }

            // Only print if we actually cleaned something
            if (deletedCount > 0)
            {
                Console.WriteLine($"  ✓ Cleaned {deletedCount} stale build director{(deletedCount == 1 ? "y" : "ies")}");
            }
        }
        catch
        {
            // Best effort cleanup - don't fail the build if cleanup fails
        }
    }

    /// <summary>
    /// Validate cache version and clean stale cache if version mismatch
    /// </summary>
    private static void ValidateAndCleanCache(string cacheDir, int expectedVersion)
    {
        var versionFile = Path.Combine(cacheDir, ".cache_version");

        try
        {
            // Check if version file exists
            if (File.Exists(versionFile))
            {
                var versionText = File.ReadAllText(versionFile).Trim();
                if (int.TryParse(versionText, out var cachedVersion))
                {
                    if (cachedVersion == expectedVersion)
                    {
                        return; // Cache is valid
                    }
                }
            }

            // Version mismatch or missing - clean cache
            Console.WriteLine($"  Cache version mismatch - cleaning stale cache...");

            // Delete all files EXCEPT the version file
            foreach (var file in Directory.GetFiles(cacheDir))
            {
                // Skip the version file itself
                if (file == versionFile)
                    continue;

                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            // Write new version file
            File.WriteAllText(versionFile, expectedVersion.ToString());
        }
        catch
        {
            // If we can't validate/clean, continue anyway
            // Better to have stale cache than crash
        }
    }
}