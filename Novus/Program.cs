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
    public long LastWriteTimeUtcTicks { get; set; }
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
///    - Keyed by: compiler build + C file hash + types header hash + CPU + FPU + build mode + opt level
///    - Validates integrity via .meta files with size/hash verification
///    - Location: {outputDir}/usercache/{cpu}/{buildMode}/
///
/// 3. Stdlib Module Cache (precompiled stdlib)
///    - Pre-compiled standard library modules
///    - Location: {compilerDir}/precompiled/{cpu}/{buildMode}/
///    - Invalidates when stdlib source or either compiler assembly changes
///
/// 4. Infrastructure Cache (stubs and runtime)
///    - Hashes all .s files in stubs/ and runtime/ directories
///    - Stored in .novus_infrastructure_hash per output directory
///    - Forces rebuild of infrastructure .o files when changed
///
/// Cache Invalidation Triggers:
/// - Compiler assembly change: invalidates all generated-code caches
/// - Source file change: invalidates that file's IR and object caches + dependents
/// - Types header change: invalidates all object caches (ABI change)
/// - Build config change (CPU/FPU/mode/opt): invalidates object caches
/// - Dependency change: cascades through IR cache's dependency graph
/// </summary>
public class Program
{
    internal static string GetStartupStub(string projectType) =>
        projectType.Equals("handler", StringComparison.OrdinalIgnoreCase)
            ? "novus_handler_startup"
            : "novus_startup";

    // WHI Toolchain CLI Conventions (docs/toolchain-cli-conventions.md §3) exit-code floor:
    //   0 = success, 1 = usage/environment error (couldn't start),
    //   2 = compilation error (source was processed and diagnostics were emitted).
    // A toolchain may use finer-grained codes above 2, but this floor is fixed.
    public const int EXIT_SUCCESS = 0;
    public const int EXIT_USAGE = 1;
    public const int EXIT_COMPILE_ERROR = 2;

    // IR depends on Novus.Core only. CLI/test-runner edits must not evict every
    // parsed stdlib module; generated C/object caches retain the broader key.
    private static int IrCacheVersion =>
        BitConverter.ToInt32(typeof(IrBuilder).Assembly.ManifestModule.ModuleVersionId.ToByteArray());

    private static int CompilerCacheVersion =>
        BitConverter.ToInt32(typeof(Program).Assembly.ManifestModule.ModuleVersionId.ToByteArray()) ^
        IrCacheVersion;

    internal static string? ResolveGeneratedSourcePath(
        string cFileName,
        IEnumerable<(string Prefix, string SourcePath)> candidates)
    {
        return candidates
            .Where(candidate => cFileName.StartsWith(candidate.Prefix + "_", StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Prefix.Length)
            .Select(candidate => candidate.SourcePath)
            .FirstOrDefault();
    }

    internal static string GetGeneratedModulePrefix(string modulePath, string fallback)
        => ModuleImportHelper.GetGeneratedModulePrefix(modulePath, fallback);

    internal static string ComputeWholeProgramCacheKey(
        IEnumerable<(string Path, string ContentHash)> inputs,
        string typesHeaderHash,
        string cpu,
        string fpu,
        int optimizationLevel)
    {
        var parts = inputs
            .OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => $"{Path.GetFullPath(input.Path)}|{input.ContentHash}")
            .Prepend($"whole-v1|v{CompilerCacheVersion}|{typesHeaderHash}|{cpu}|{fpu}|O{optimizationLevel}");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join('\n', parts)))).ToLowerInvariant();
    }

    internal static string ComputeCompilationConfigHash(CompilerOptions options)
    {
        var mode = options.BuildMode == BuildMode.Release ? "release" : "debug";
        var codegen = string.Join('|', mode, options.GetSafetyLevel(), options.Backend,
            options.Chipset, options.ProjectType, options.PackageName, options.PackageVersion,
            options.PgoGenerate, options.PgoUse,
            string.Join(',', options.AdditionalFfiModules.Select(module =>
                $"{module.LibraryName}:{module.BaseSymbol}:{module.MinimumVersion}:{module.Optional}")));
        return CompilationCache.ComputeConfigHash(
            options.Cpu, options.Fpu, options.OptimizationLevel, codegen);
    }

    private static string ComputeBuildSignature(CompilerOptions options, string configHash, string sourceGraphHash)
    {
        static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }

        var compilerDir = AppContext.BaseDirectory;
        var files = options.AdditionalCFiles
            .Concat(options.AdditionalAsmFiles)
            .Concat(options.AdditionalLibraries)
            .Concat(new[] { options.PgoUse })
            .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path))
            .Cast<string>()
            .Concat(new[] { "runtime", "stubs" }
                .Select(dir => Path.Combine(compilerDir, dir))
                .Where(Directory.Exists)
                .SelectMany(dir => Directory.GetFiles(
                    dir, "*", SearchOption.AllDirectories)))
            .OrderBy(path => path, StringComparer.Ordinal);
        var parts = files.Select(path => $"{Path.GetFullPath(path)}:{HashFile(path)}")
            .Prepend($"codegen:{CompilerCacheVersion}")
            .Prepend(sourceGraphHash)
            .Prepend(configHash);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }

    /// <summary>
    /// Build preprocessor constants dictionary based on compiler options.
    /// Used for conditional compilation with #if/#elif/#else/#endif directives.
    /// </summary>
    internal static Dictionary<string, bool> GetPreprocessorConstants(CompilerOptions options)
    {
        return IrBuilderConfiguration.GetPreprocessorConstantsForTarget(
            options.Cpu, options.Fpu, options.Chipset, options.BuildMode == BuildMode.Debug);
    }

    static async Task<int> Main(string[] args)
    {
        // Canonical `--version` (WHI Toolchain CLI Conventions §4): print a single
        // machine-parseable line "novus <semver>" to stdout and exit 0. Handled
        // before argument parsing so it never emits the compile banner (stderr) or
        // CommandLineParser's own heading, and so its shape stays fixed.
        var versionExit = TryHandleVersion(args, Console.Out);
        if (versionExit.HasValue)
            return versionExit.Value;

        return await CommandLine.Parser.Default.ParseArguments<CompilerOptions, BuildOptions, GenerateStubsOptions, NewCommandOptions, StdlibBuildOptions, FmtOptions, CleanOptions, TestOptions, BenchOptions, VerifyDocsOptions, VerifyNdkOptions, ConfigOptions>(args)
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
                (VerifyDocsOptions options) => Task.FromResult(Commands.VerifyDocsCommand.Run(options)),
                (VerifyNdkOptions options) => Task.FromResult(Commands.VerifyNdkCommand.Run(options)),
                (ConfigOptions options) => Task.FromResult(Commands.ConfigCommand.Run(options)),
                // Parse failures (unknown verb, bad/missing flags) are usage errors.
                errors => Task.FromResult(EXIT_USAGE)
            );
    }

    /// <summary>
    /// Handle the canonical <c>--version</c> flag (WHI Toolchain CLI Conventions §4).
    /// When present, writes "novus &lt;semver&gt;" to <paramref name="stdout"/> and
    /// returns <see cref="EXIT_SUCCESS"/>; otherwise returns <c>null</c> so normal
    /// verb dispatch continues.
    /// </summary>
    public static int? TryHandleVersion(string[] args, TextWriter stdout)
    {
        if (args != null && Array.IndexOf(args, "--version") >= 0)
        {
            stdout.WriteLine(VersionLine());
            return EXIT_SUCCESS;
        }
        return null;
    }

    /// <summary>
    /// Canonical version line: "novus &lt;semver&gt;" (assembly version, no "v"
    /// prefix, no "Compiler" word, no banner). See WHI Toolchain CLI Conventions §4.
    /// </summary>
    public static string VersionLine() => $"novus {AssemblyVersion()}";

    private static string AssemblyVersion()
    {
        var asm = typeof(Program).Assembly;
        var info = asm
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;
        var version = info ?? asm.GetName().Version?.ToString() ?? "0.0.0";
        // Strip any build-metadata suffix the SDK may append (e.g. "0.2.2+abc1234").
        var plus = version.IndexOf('+');
        if (plus >= 0)
            version = version.Substring(0, plus);
        return version;
    }

    static async Task<int> RunBuild(BuildOptions buildOptions)
    {
        // Delegate to BuildCommand which handles workspace/project detection
        return await Commands.BuildCommand.Run(buildOptions);
    }

    private static IReadOnlyList<FfiModuleMetadata> FindRequiredFfiModules(
        ModuleIR mainModule,
        IEnumerable<ModuleIR> importedModules,
        IReadOnlySet<string> reachableFunctions)
    {
        var modules = importedModules.Prepend(mainModule).ToList();

        return modules
            .Select(module => (Module: module, Metadata: FfiModuleMetadata.TryRead(module.ModulePath)))
            .Where(item => item.Metadata != null &&
                           item.Module.IrModule.Functions.Any(f => f.IsExtern && reachableFunctions.Contains(f.Name)))
            .Select(item => item.Metadata! with
            {
                MinimumVersion = Math.Max(
                    item.Metadata!.MinimumVersion,
                    item.Module.IrModule.Functions
                        .Where(function => function.IsExtern && reachableFunctions.Contains(function.Name))
                        .Select(function => Math.Max(
                            function.Attributes?.Get(KnownAttributes.Since)?.GetPositionalArg<int>(0) ?? 0,
                            item.Metadata.FunctionVersions.TryGetValue(function.Name, out var version) ? version : 0))
                        .DefaultIfEmpty()
                        .Max())
            })
            .DistinctBy(metadata => metadata.ModuleName)
            .OrderBy(metadata => metadata.ModuleName, StringComparer.Ordinal)
            .ToList();
    }

    private static HashSet<string> FindReachableFunctionNames(
        ModuleIR mainModule,
        IEnumerable<ModuleIR> importedModules,
        bool preservePublicFunctions,
        bool includeAllDefinitions = false)
    {
        var modules = importedModules.Prepend(mainModule).ToList();
        var definitions = modules
            .SelectMany(module => module.IrModule.Functions
                .Where(function => !function.IsExtern && function.BasicBlocks.Count > 0)
                .Select(function => (function, module.IrModule)))
            .ToLookup(item => item.function.Name, StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();

        void Mark(string name)
        {
            if (reachable.Add(name))
                pending.Enqueue(name);
        }

        if (includeAllDefinitions)
        {
            foreach (var definition in definitions.SelectMany(group => group))
                Mark(definition.function.Name);
        }
        else
        {
            foreach (var function in mainModule.IrModule.Functions.Where(function =>
                         function.Name == "main" ||
                         function.IsExported ||
                         function.IsInterruptHandler ||
                         (preservePublicFunctions && function.IsPublic) ||
                         IsGeneratedEntryPoint(function)))
                Mark(function.Name);
        }

        while (pending.TryDequeue(out var name))
        {
            foreach (var (function, owner) in definitions[name])
            {
                foreach (var block in function.BasicBlocks.Concat(function.DeferredBlocks))
                foreach (var instruction in block.Instructions)
                {
                    switch (instruction)
                    {
                        case IrCall call:
                            Mark(call.FunctionName);
                            foreach (var argument in call.Arguments) ScanValue(argument);
                            break;
                        case IrIndirectCall call:
                            ScanValue(call.FunctionPointer);
                            foreach (var argument in call.Arguments) ScanValue(argument);
                            break;
                        case IrCreateClosure closure:
                            Mark(closure.GeneratedFunctionName);
                            foreach (var captured in closure.CapturedValues) ScanValue(captured.Value);
                            break;
                        case IrInvokeClosure invocation:
                            ScanValue(invocation.Closure);
                            foreach (var argument in invocation.Arguments) ScanValue(argument);
                            break;
                        case IrDropInPlace drop:
                            foreach (var dropFunction in GetDropFunctions(owner, drop.ElementType)) Mark(dropFunction);
                            ScanValue(drop.Pointer);
                            break;
                        case IrLocalDecl local when local.InitialValue != null: ScanValue(local.InitialValue); break;
                        case IrStore store: ScanValue(store.Value); break;
                        case IrStoreCapture store: ScanValue(store.Value); break;
                        case IrDereferenceStore store: ScanValue(store.Pointer); ScanValue(store.Value); break;
                        case IrMemberAccess access: ScanValue(access.Struct); break;
                        case IrMemberStore store: ScanValue(store.Struct); ScanValue(store.Value); break;
                        case IrIndexAccess access: ScanValue(access.Array); ScanValue(access.Index); break;
                        case IrIndexStore store: ScanValue(store.Array); ScanValue(store.Index); ScanValue(store.Value); break;
                        case IrIndexedFieldStore store: ScanValue(store.Array); ScanValue(store.Index); ScanValue(store.Value); break;
                        case IrBinaryOp binary: ScanValue(binary.Left); ScanValue(binary.Right); break;
                        case IrConditionalBranch branch: ScanValue(branch.Condition); break;
                        case IrPhi phi:
                            foreach (var value in phi.IncomingValues) ScanValue(value);
                            break;
                        case IrMatch match: ScanValue(match.MatchValue); break;
                        case IrReturn returned when returned.Value != null: ScanValue(returned.Value); break;
                        case IrAssert assertion: ScanValue(assertion.Condition); break;
                    }
                }
            }
        }

        return reachable;

        void ScanValue(IrValue value)
        {
            switch (value)
            {
                case IrFunctionAddress address: Mark(address.FunctionName); break;
                case IrFunctionRef reference: Mark(reference.Function.Name); break;
                case IrStructLiteral literal:
                    foreach (var field in literal.FieldValues.Values) ScanValue(field);
                    break;
                case IrTupleLiteral literal:
                    foreach (var element in literal.Elements) ScanValue(element);
                    break;
                case IrArrayLiteral literal:
                    foreach (var element in literal.Elements) ScanValue(element);
                    break;
                case IrEnumValue enumValue:
                    foreach (var associated in enumValue.AssociatedValues) ScanValue(associated);
                    break;
                case IrBorrowValue borrow: ScanValue(borrow.BorrowedValue); break;
                case IrDereferenceValue dereference: ScanValue(dereference.PointerValue); break;
                case IrCastValue cast: ScanValue(cast.Value); break;
                case IrPointerOffsetValue offset: ScanValue(offset.Pointer); ScanValue(offset.Index); break;
                case IrFieldReference field: ScanValue(field.Struct); break;
                case IrIndexedFieldAccess field: ScanValue(field.Array); ScanValue(field.Index); break;
                case IrTupleElementAccess element: ScanValue(element.Tuple); break;
            }
        }
    }

    private static bool IsGeneratedEntryPoint(IrFunction function) =>
        function.Attributes != null && new[]
        {
            KnownAttributes.LibFunc, KnownAttributes.LibOpen, KnownAttributes.LibClose,
            KnownAttributes.LibExpunge, KnownAttributes.LibInit, KnownAttributes.ResourceFunc,
            KnownAttributes.ResourceInit, KnownAttributes.DeviceCmd, KnownAttributes.DeviceOpen,
            KnownAttributes.DeviceClose, KnownAttributes.DeviceExpunge, KnownAttributes.DeviceInit,
            KnownAttributes.BeginIO, KnownAttributes.AbortIO,
        }.Any(function.Attributes.Has);

    private static IEnumerable<string> GetDropFunctions(IrModule module, IrType type)
    {
        if (!module.TypeImplementsDrop(type))
            yield break;
        if (type is IrStructType structType)
        {
            if (module.StructImplementsDrop(structType))
            {
                yield return $"{CCodeGenerator.MangleNameStatic(structType.CacheKey ?? structType.StructName)}_Drop_drop";
                yield break;
            }
            foreach (var field in structType.Fields)
            foreach (var function in GetDropFunctions(module, field.Type))
                yield return function;
            yield break;
        }
        var children = type switch
        {
            IrEnumType enumType => enumType.Variants.SelectMany(variant => variant.AssociatedData),
            IrTupleType tupleType => tupleType.ElementTypes,
            IrArrayType arrayType => [arrayType.ElementType],
            _ => [],
        };
        foreach (var child in children)
        foreach (var function in GetDropFunctions(module, child))
            yield return function;
    }

    private static bool UsesNativeFloat(IEnumerable<ModuleIR> modules, IReadOnlySet<string> reachableFunctions) =>
        modules.SelectMany(module => module.IrModule.Functions)
            .Where(function => reachableFunctions.Contains(function.Name))
            .Any(function => function.ReturnType is IrFloatType ||
                             function.Parameters.Any(parameter => parameter.Type is IrFloatType) ||
                             function.LocalVariables.Any(local => local.Type is IrFloatType));

    static int RunGenerateStubs(GenerateStubsOptions options)
    {
        // Bindings are generated from the NDK's own SFD and header files, so this command
        // needs an NDK just as much as a compile does.
        string ndkPath;
        try
        {
            ndkPath = UserConfig.RequireNdkPath(options.NdkPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return EXIT_USAGE;
        }

        // Generate FFI bindings from SFD files (NDK 3.9+)
        var sfdGenerator = new SfdGenerator(ndkPath, options.OutputPath);
        sfdGenerator.GenerateAllBindings();
        return 0;
    }

    static async Task<int> RunStdlibBuild(StdlibBuildOptions options)
    {
        // Pre-compile standard library to .o files for faster linking
        var cpus = options.Cpu == "all"
            ? new[] { "68020", "68030", "68040", "68060" }
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
                    CompilerCacheVersion);

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

    private static void WriteModuleDiagnostics(
        DiagnosticBag diagnostics,
        string inputFile,
        CompilerOptions options)
    {
        var inputPath = Path.GetFullPath(inputFile);
        var showWarnings = options.Verbose ||
            inputPath == Path.GetFullPath(options.InputFile) ||
            options.AdditionalSourceFiles.Any(path => inputPath == Path.GetFullPath(path));
        var visible = diagnostics.Diagnostics
            .Where(diagnostic => diagnostic.IsError || showWarnings && diagnostic.IsWarning)
            .ToList();
        if (visible.Count == 0)
            return;

        var filtered = new DiagnosticBag();
        foreach (var diagnostic in visible)
            filtered.Add(diagnostic);
        Console.Error.WriteLine(filtered.FormatDiagnostics());
    }

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
                Console.Error.WriteLine($"error: module file not found: {inputFile}");
                return null;
            }

            // Check compilation cache first (if enabled)
            if (compilationCache != null)
            {
                var configHash = ComputeCompilationConfigHash(options);

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
                        cachedHasMain,
                        FromCache: true);
                }
            }

            var compilePhaseTimer = System.Diagnostics.Stopwatch.StartNew();
            void ReportModulePhase(string phase)
            {
                if (options.Verbose)
                    Console.WriteLine($"    [Module timing] {Path.GetFileName(inputFile)} {phase}: {compilePhaseTimer.Elapsed.TotalMilliseconds:F0} ms");
                compilePhaseTimer.Restart();
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
                Console.Error.WriteLine(diagnostics.FormatDiagnostics());
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
                    Console.Error.WriteLine(diagnostics.FormatDiagnostics());
                    return null;
                }

                // Add to cache
                moduleCache.Add(inputFile, compilationUnit);
            }

            ReportModulePhase("read + preprocess + parse");

            // Perform semantic analysis
            var analyzer = new SemanticAnalyzer(inputFile, source, stdLibPath);
            var analysisSucceeded = analyzer.Analyze(compilationUnit);
            ReportModulePhase("semantic analysis");

            WriteModuleDiagnostics(analyzer.Diagnostics, inputFile, options);

            if (!analysisSucceeded)
            {
                return null;
            }

            // Build IR - pass analysis result for overload resolution
            var analysisResult = analyzer.GetResult();
            var irBuilder = new IrBuilder(analysisResult);
            irBuilder.SetStdLibPath(stdLibPath);
            irBuilder.SetInputFilePath(inputFile);
            var module = irBuilder.BuildModule(compilationUnit);
            ReportModulePhase("IR construction");

            WriteModuleDiagnostics(irBuilder.Diagnostics, inputFile, options);

            // Check for IR building errors
            if (irBuilder.Diagnostics.HasErrors)
            {
                return null;
            }

            var moduleName = Path.GetFileNameWithoutExtension(inputFile);
            var hasMain = module.Functions.Any(f => f.Name == "main" && !f.IsExtern);
            var importedModules = irBuilder.GetImportedModules();
            var semanticImports = importedModules.ToList();
            if (Path.GetFullPath(inputFile) == Path.GetFullPath(options.InputFile))
            {
                importedModules.AddRange(options.AdditionalSourceFiles.Select(Path.GetFullPath));
                importedModules = importedModules.Distinct(StringComparer.Ordinal).ToList();
            }

            // Cache the successful compilation result
            if (compilationCache != null)
            {
                var configHash = ComputeCompilationConfigHash(options);

                compilationCache.CacheCompilationResult(
                    inputFile,
                    compilationUnit,
                    module,
                    irBuilder.StringLiterals,
                    importedModules,
                    configHash,
                    hadErrors: false,
                    dependencyModules: semanticImports);
                ReportModulePhase("cache snapshot");
            }

            return new ModuleIR(
                inputFile,
                moduleName,
                module,
                irBuilder.StringLiterals,
                importedModules,
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
                Console.Error.WriteLine($"error: module file not found: {inputFile}");
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
                Console.Error.WriteLine(diagnostics.FormatDiagnostics());
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
                    Console.Error.WriteLine(diagnostics.FormatDiagnostics());
                    return null;
                }

                // Add to cache
                moduleCache.Add(inputFile, compilationUnit);
            }

            // Perform semantic analysis
            var analyzer = new SemanticAnalyzer(inputFile, source, stdLibPath);
            var analysisSucceeded = analyzer.Analyze(compilationUnit);

            WriteModuleDiagnostics(analyzer.Diagnostics, inputFile, options);

            if (!analysisSucceeded)
            {
                return null;
            }

            // Build IR - pass analysis result for overload resolution
            var analysisResult = analyzer.GetResult();
            var irBuilder = new IrBuilder(analysisResult);
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
        // Compile banner is progress output, not program output → stderr
        // (WHI Toolchain CLI Conventions §5). Keeps stdout clean for scripts.
        Console.Error.WriteLine("Novus Compiler");
        Console.Error.WriteLine($"Target: {options.Cpu.ToUpper()}");
        Console.Error.WriteLine($"FPU Mode: {options.Fpu}");
        Console.Error.WriteLine("==================================\n");

        try
        {
            CompilerOptions.ValidateCpu(options.Cpu);
            CompilerOptions.ValidateOptimizationLevel(options.OptimizationLevel);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        // Check the NDK up front. It is only needed once the generated C is compiled, but
        // discovering it is missing at that point means the user has already waited through
        // parsing, IR, optimization and codegen for an error we could have given instantly.
        if (!options.EmitIr)
        {
            try
            {
                options.NdkPath = UserConfig.RequireNdkPath(options.NdkPath);
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return EXIT_USAGE;
            }
        }

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
                // Missing input is an environment/usage error (couldn't start),
                // not a compilation error → stderr, exit 1 (§3/§5).
                Console.Error.WriteLine($"error: input file not found: {options.InputFile}");
                return EXIT_USAGE;
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
                compilationCache = new CompilationCache(
                    projectRoot, IrCacheVersion, options.CompilationCacheDirectory);
                compilationCache.BeginBuild();
            }

            string? buildStampPath = null;
            string? buildSignature = null;
            if (compilationCache != null && !options.EmitAsmOnly && !options.EmitIr)
            {
                var configHash = ComputeCompilationConfigHash(options);
                buildStampPath = Path.GetFullPath(options.OutputFile) + ".novus-build";
                var sourceRoots = new[] { options.InputFile }.Concat(options.AdditionalSourceFiles);
                buildSignature = ComputeBuildSignature(
                    options, configHash, compilationCache.ComputeSourceGraphHash(sourceRoots));
                if (File.Exists(options.OutputFile) && File.Exists(buildStampPath) &&
                    await File.ReadAllTextAsync(buildStampPath) == buildSignature &&
                    !compilationCache.NeedsRecompilation(options.InputFile, configHash) &&
                    options.AdditionalSourceFiles.All(path => !compilationCache.NeedsRecompilation(path, configHash)))
                {
                    Console.WriteLine($"✓ Up to date: {Path.GetFileName(options.OutputFile)}");
                    return 0;
                }

                File.Delete(buildStampPath);
            }

            var phaseTimer = System.Diagnostics.Stopwatch.StartNew();
            void ReportPhase(string name)
            {
                if (options.Verbose)
                    Console.WriteLine($"  [Timing] {name}: {phaseTimer.Elapsed.TotalMilliseconds:F0} ms");
                phaseTimer.Restart();
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
            var moduleTimer = System.Diagnostics.Stopwatch.StartNew();
            var mainIR = await CompileModuleToIR(options.InputFile, stdLibPath, options, moduleCache, circularImportDetector, compilationCache);
            if (options.Verbose)
                Console.WriteLine($"  [Timing] {Path.GetFileName(options.InputFile)}: {moduleTimer.Elapsed.TotalMilliseconds:F0} ms");
            if (mainIR == null)
            {
                if (diagnostics.HasErrors)
                {
                    Console.Error.WriteLine(diagnostics.FormatDiagnostics());
                }
                return EXIT_COMPILE_ERROR;
            }

            // Check for test file compiled with 'compile' instead of 'test'
            // If the file has @test functions but no main(), suggest using 'novus test'
            if (!mainIR.HasMain)
            {
                var testFunctions = mainIR.IrModule.GetTestFunctions();
                if (testFunctions.Count > 0)
                {
                    Console.WriteLine($"\n✗ Error: File has {testFunctions.Count} @test function(s) but no main() function.");
                    Console.WriteLine();
                    Console.WriteLine("  This file appears to be a test file. Use 'novus test' instead:");
                    Console.WriteLine($"    novus test {options.InputFile}");
                    Console.WriteLine();
                    Console.WriteLine("  The 'novus test' command will:");
                    Console.WriteLine("    - Discover all @test functions");
                    Console.WriteLine("    - Generate a test runner main()");
                    Console.WriteLine("    - Compile to an executable that runs all tests");
                    return 1;
                }
            }

            // Record dependencies from the main module
            foreach (var import in mainIR.ImportedModules)
            {
                if (!circularImportDetector.RecordDependency(options.InputFile, import))
                {
                    Console.Error.WriteLine(diagnostics.FormatDiagnostics());
                    return EXIT_COMPILE_ERROR;
                }
            }

            // Recursively collect all dependencies to IR
            var allModulesIR = new Dictionary<string, ModuleIR>(); // path -> IR
            var toProcess = new Queue<string>(mainIR.ImportedModules);
            var processed = new HashSet<string>();

            while (toProcess.Count > 0)
            {
                var batch = new List<string>();
                while (toProcess.Count > 0)
                {
                    var modulePath = toProcess.Dequeue();
                    if (processed.Add(modulePath))
                    {
                        batch.Add(modulePath);
                        Console.WriteLine($"  → {PathUtility.GetModuleDisplayName(modulePath)}");
                    }
                }

                // Cache hits are synchronous through deserialization, so start each
                // frontier member on the pool instead of serializing them during Select().
                var compiledBatch = await Task.WhenAll(batch.Select(modulePath => Task.Run(async () =>
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var moduleIR = await CompileModuleToIR(
                        modulePath, stdLibPath, options, moduleCache, null, compilationCache);
                    return (modulePath, moduleIR, elapsedMs: timer.Elapsed.TotalMilliseconds);
                })));

                foreach (var (modulePath, moduleIR, elapsedMs) in compiledBatch)
                {
                    if (options.Verbose)
                        Console.WriteLine($"  [Timing] {Path.GetFileName(modulePath)}: {elapsedMs:F0} ms");
                    if (moduleIR == null)
                    {
                        Console.Error.WriteLine(diagnostics.HasErrors
                            ? diagnostics.FormatDiagnostics()
                            : $"error: failed to compile dependency: {modulePath}");
                        return EXIT_COMPILE_ERROR;
                    }

                    allModulesIR[modulePath] = moduleIR;
                    foreach (var import in moduleIR.ImportedModules)
                    {
                        if (!circularImportDetector.RecordDependency(modulePath, import))
                        {
                            Console.Error.WriteLine(diagnostics.FormatDiagnostics());
                            return EXIT_COMPILE_ERROR;
                        }

                        if (!processed.Contains(import))
                        {
                            toProcess.Enqueue(import);
                        }
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

            ReportPhase("IR load/build");
            if (options.Verbose && compilationCache != null)
            {
                var cachePerformance = compilationCache.GetPerformanceStats();
                Console.WriteLine($"  [Timing] cache validation CPU: {cachePerformance.ValidationCpuTime.TotalMilliseconds:F0} ms");
                Console.WriteLine($"  [Timing] IR deserialize CPU: {cachePerformance.DeserializationCpuTime.TotalMilliseconds:F0} ms " +
                                  $"({cachePerformance.IrBytesLoaded / 1024.0 / 1024.0:F1} MiB)");
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
                    return EXIT_COMPILE_ERROR;
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

            ReportPhase("IR validation");

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

            if (options.OptimizationLevel == 1)
            {
                foreach (var module in allModulesIR.Values.Select(item => item.IrModule).Prepend(mainIR.IrModule))
                {
                    Novus.Optimizer.OptimizationPipeline
                        .CreatePipeline(options.OptimizationLevel, options.Verbose)
                        .Run(module);
                }
            }

            var preservePublicFunctions = options.ProjectType.Equals("library", StringComparison.OrdinalIgnoreCase);
            var reachableFunctions = FindReachableFunctionNames(mainIR, allModulesIR.Values, preservePublicFunctions);
            var linkedFunctions = options.BuildMode == BuildMode.Release && options.OptimizationLevel == 3
                ? reachableFunctions
                : FindReachableFunctionNames(mainIR, allModulesIR.Values, preservePublicFunctions, includeAllDefinitions: true);
            var usesAmiSsl = allModulesIR.Values.Prepend(mainIR).Any(module =>
                module.ModuleName.Equals("amissl", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(module.ModulePath).Equals("amissl.novus", StringComparison.OrdinalIgnoreCase));
            var ffiCompileModules = allModulesIR.Values.Prepend(mainIR)
                .Select(module => FfiModuleMetadata.TryRead(module.ModulePath))
                .Where(metadata => metadata != null)
                .Select(metadata => metadata!)
                .DistinctBy(metadata => metadata.ModuleName)
                .ToList();
            var requiredFfiModules = FindRequiredFfiModules(mainIR, allModulesIR.Values, linkedFunctions)
                .Concat(ffiCompileModules.Where(metadata => metadata.Kind == FfiModuleKind.LazyLibrary))
                .Concat(options.AdditionalFfiModules)
                .DistinctBy(metadata => metadata.BaseSymbol)
                .ToList();
            var dosMetadata = FfiModuleMetadata.TryRead(Path.Combine(stdLibPath, "amiga", "raw", "dos.novus"));
            var needsDosStartup = !new[] { "library", "device", "resource" }
                .Contains(options.ProjectType, StringComparer.OrdinalIgnoreCase);
            if (needsDosStartup && dosMetadata != null && requiredFfiModules.All(item => item.ModuleName != "dos"))
                requiredFfiModules.Add(dosMetadata);
            var usesNativeFloat = UsesNativeFloat(allModulesIR.Values.Prepend(mainIR), linkedFunctions);
            if (options.Fpu is "none" or "soft" && usesNativeFloat)
            {
                foreach (var moduleName in new[] { "mathieeesingbas", "mathieeedoubbas" })
                {
                    var metadata = FfiModuleMetadata.TryRead(Path.Combine(stdLibPath, "amiga", "raw", $"{moduleName}.novus"));
                    if (metadata != null && requiredFfiModules.All(item => item.ModuleName != moduleName))
                        requiredFfiModules.Add(metadata);
                }
            }

            ReportPhase("lowering");

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

            // Collect all functions for the shared header (including closure functions)
            var allFunctionsForHeader = mainIR.IrModule.Functions.ToList();
            foreach (var moduleIR in allModulesIR.Values)
            {
                allFunctionsForHeader.AddRange(moduleIR.IrModule.Functions);
            }

            // Generate shared types header
            var sharedTypesHeader = CCodeGenerator.GenerateSharedTypesHeader(typeRegistry, allFunctionsForHeader);
            var ffiHeaders = ffiCompileModules
                .SelectMany(module => module.Headers)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(header => header, StringComparer.Ordinal)
                .Select(header => $"#include <{header}>")
                .ToList();
            if (ffiHeaders.Count > 0)
            {
                var ffiDirectory = Path.GetDirectoryName(ffiCompileModules[0].ModulePath)!;
                var ndkTypesPath = Path.Combine(ffiDirectory, "ndk_types.h");
                var ndkTypes = File.Exists(ndkTypesPath)
                    ? string.Join(Environment.NewLine, File.ReadLines(ndkTypesPath)
                        // These typedef names collide with the required Amiga library-base globals.
                        .Where(line => line is not "typedef struct GfxBase GfxBase;"
                            and not "typedef struct IntuitionBase IntuitionBase;")
                        .Where(line => !sharedTypesHeader.Contains(line, StringComparison.Ordinal)))
                    : "";
                sharedTypesHeader = string.Join(Environment.NewLine, ffiHeaders) + Environment.NewLine +
                                    ndkTypes + sharedTypesHeader;
            }

            // Determine output directory - NEVER write to repo root
            // Use a dedicated build directory for intermediate files
            var fullOutputPath = Path.GetFullPath(options.OutputFile);
            var outputFileDir = Path.GetDirectoryName(fullOutputPath);
            var baseName = Path.GetFileNameWithoutExtension(options.OutputFile);

            // If output file has no directory component, create a build directory
            string outputDir;
            if (string.IsNullOrEmpty(outputFileDir) || outputFileDir == Directory.GetCurrentDirectory())
            {
                // Keep intermediates out of the source directory, but keep them stable so
                // per-function object caching survives the next compiler invocation.
                outputDir = Path.Combine(Directory.GetCurrentDirectory(), ".novus-cache", "build", baseName);
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

            var generatedManifestDir = Path.Combine(outputDir, ".novus-cfiles");

            string GetGeneratedManifestPath(string modulePath) =>
                Path.Combine(generatedManifestDir, $"{ComputeStringHash(Path.GetFullPath(modulePath))[..16]}.txt");

            List<string>? LoadGeneratedFiles(ModuleIR module)
            {
                if (!module.FromCache || safetyLevel >= SafetyLevel.Paranoid)
                    return null;

                try
                {
                    var lines = File.ReadAllLines(GetGeneratedManifestPath(module.ModulePath));
                    if (lines.Length == 0 || lines[0] != $"v{CompilerCacheVersion}")
                        return null;

                    var files = lines.Skip(1)
                        .Where(name => !string.IsNullOrWhiteSpace(name) && name == Path.GetFileName(name))
                        .Select(name => Path.Combine(outputDir, name))
                        .ToList();
                    return files.Count == lines.Length - 1 && files.All(File.Exists) ? files : null;
                }
                catch
                {
                    return null;
                }
            }

            async Task SaveGeneratedFiles(string modulePath, IEnumerable<string> files)
            {
                Directory.CreateDirectory(generatedManifestDir);
                var contents = string.Join('\n', files.Select(Path.GetFileName).Prepend($"v{CompilerCacheVersion}"));
                await AtomicCacheWriter.WriteFileAtomicallyAsync(GetGeneratedManifestPath(modulePath), contents);
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

            // Track monomorphized functions across all modules to generate each one exactly once
            // Key: mangled function name, Value: (module name, function)
            // IMPORTANT: This must be declared BEFORE processing any modules so that
            // monomorphized functions from main module are tracked and not duplicated in library modules
            var generatedMonomorphizedFunctions = new Dictionary<string, (string moduleName, IrFunction function, CCodeGenerator codegen)>();

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
                    projectVersion: options.PackageVersion,
                    preservePublicFunctions: options.ProjectType == "library");
                allCodeGenerators.Add(mainCodegen);

                // Generate one C file per function
                // Filter out functions with unresolved types to avoid symbol conflicts
                var generableMainFunctions = mainFunctions
                    .Where(f => !mainCodegen.HasUnresolvedTypes(f))
                    .ToList();

                foreach (var function in generableMainFunctions)
                {
                    // Check if this is a monomorphized function (trait method or static generic function)
                    bool isMonomorphized = mainCodegen.IsMonomorphizedFunction(function);
                    var mangledName = mainCodegen.MangleName(function);

                    if (isMonomorphized)
                    {
                        // Track this monomorphized function so library modules don't duplicate it
                        generatedMonomorphizedFunctions[mangledName] = (baseName, function, mainCodegen);
                    }

                    var functionCCode = mainCodegen.GenerateFunctionFile(function);
                    // Always write the C file (even if it's a stub that panics)
                    // This ensures linking succeeds even if the function isn't called
                    // Sanitize function name for use in C filenames (replace :: with _ to match MangleName, and remove < > , & * etc.)
                    var sanitizedFunctionName = function.Name.Replace("::", "_").Replace("()", "unit").Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "").Replace("&", "ref_").Replace("*", "ptr_").Replace("(", "").Replace(")", "");
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

            // For libraries and devices, generate A6 wrapper assembly and interface files
            var projectType = options.ProjectType.ToLowerInvariant();
            if (projectType == "library")
            {
                var libraryGen = new LibraryGenerator(mainIR.IrModule, options.PackageVersion);
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
            else if (projectType == "device")
            {
                var deviceGen = new DeviceGenerator(mainIR.IrModule, options.PackageVersion);
                if (deviceGen.IsDevice)
                {
                    Console.WriteLine($"  [DEVICE] Generating {deviceGen.GetDeviceName()} boilerplate...");

                    // Generate A6 wrappers for device functions
                    var wrapperAsm = deviceGen.GenerateA6Wrappers();
                    var wrapperAsmFile = Path.Combine(outputDir, $"{baseName}_wrappers.s");
                    await File.WriteAllTextAsync(wrapperAsmFile, wrapperAsm);
                    Console.WriteLine($"  → {Path.GetFileName(wrapperAsmFile)} (A6 wrappers)");

                    // Generate C header
                    var cHeader = deviceGen.GenerateCHeader();
                    var headerFile = Path.Combine(outputDir, $"{baseName}.h");
                    await File.WriteAllTextAsync(headerFile, cHeader);
                    Console.WriteLine($"  → {Path.GetFileName(headerFile)} (C header)");

                    // Generate Novus FFI binding
                    var novusFfi = deviceGen.GenerateNovusFFI();
                    var ffiFile = Path.Combine(outputDir, $"{baseName}.novus");
                    await File.WriteAllTextAsync(ffiFile, novusFfi);
                    Console.WriteLine($"  → {Path.GetFileName(ffiFile)} (Novus FFI)");

                    // Generate lifecycle functions (DevInit, DevOpen, DevClose, DevExpunge, BeginIO, AbortIO)
                    var lifecycleCCode = deviceGen.GenerateLifecycleFunctions();
                    if (!string.IsNullOrEmpty(lifecycleCCode))
                    {
                        var lifecycleCFile = Path.Combine(outputDir, $"{baseName}_lifecycle.c");
                        await File.WriteAllTextAsync(lifecycleCFile, lifecycleCCode);
                        cFiles.Add(lifecycleCFile);
                        Console.WriteLine($"  → {Path.GetFileName(lifecycleCFile)} (lifecycle functions)");
                    }
                }
                else
                {
                    Console.WriteLine("  [WARNING] Device project has no @device attribute on any struct");
                }
            }
            else if (projectType == "resource")
            {
                var resourceGen = new LibraryGenerator(mainIR.IrModule, options.PackageVersion);
                if (!resourceGen.IsResource)
                    throw new InvalidOperationException("Resource projects require a struct annotated with @resource(name = \"name.resource\")");

                var wrapperAsmFile = Path.Combine(outputDir, $"{baseName}_wrappers.s");
                await File.WriteAllTextAsync(wrapperAsmFile, resourceGen.GenerateA6Wrappers());
                await File.WriteAllTextAsync(Path.Combine(outputDir, $"{baseName}.h"), resourceGen.GenerateCHeader());
                await File.WriteAllTextAsync(Path.Combine(outputDir, $"{baseName}.novus"), resourceGen.GenerateNovusFFI());
                await File.WriteAllTextAsync(Path.Combine(outputDir, $"{baseName}_lib.fd"), resourceGen.GenerateFDFile());
                var supportFile = Path.Combine(outputDir, $"{baseName}_resource.c");
                await File.WriteAllTextAsync(supportFile,
                    resourceGen.GenerateLibraryBaseStruct() + "\n" +
                    resourceGen.GenerateROMTag() + "\n" +
                    resourceGen.GenerateDefaultLifecycleFunctions());
                cFiles.Add(supportFile);
                Console.WriteLine($"  → {Path.GetFileName(wrapperAsmFile)} (resource vectors)");
            }

            // Library modules: generate one C file per function
            foreach (var (modulePath, moduleIR) in allModulesIR)
            {
                var moduleName = moduleIR.ModuleName;
                var modulePrefix = GetGeneratedModulePrefix(modulePath, moduleName);
                var cachedGeneratedFiles = LoadGeneratedFiles(moduleIR);
                var moduleCFiles = new List<string>();

                // Get all non-extern functions with implementations
                var functions = moduleIR.IrModule.Functions
                    .Where(f => !f.IsExtern && f.BasicBlocks.Count > 0)
                    .ToList();

                if (functions is [])
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

                // Monomorphized function ownership depends on this build's import
                // graph, so a per-module C manifest cannot safely cache the winner.
                if (functions.Any(moduleCodegen.IsMonomorphizedFunction))
                    cachedGeneratedFiles = null;

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

                    if (cachedGeneratedFiles == null)
                    {
                        var functionCCode = moduleCodegen.GenerateFunctionFile(function);
                        // Always write the C file (even if it's a stub that panics)
                        // This ensures linking succeeds even if the function isn't called
                        // Sanitize function name for use in C filenames (replace :: with _ to match MangleName, and remove < > , & * etc.)
                        var sanitizedFunctionName = function.Name.Replace("::", "_").Replace("()", "unit").Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "").Replace("&", "ref_").Replace("*", "ptr_").Replace("(", "").Replace(")", "");
                        var functionCFile = Path.Combine(outputDir, $"{modulePrefix}_{sanitizedFunctionName}.c");
                        await File.WriteAllTextAsync(functionCFile, functionCCode);
                        moduleCFiles.Add(functionCFile);
                    }
                }

                // Generate statics file if module has static variables
                if (cachedGeneratedFiles == null)
                {
                    var staticsCCode = moduleCodegen.GenerateStaticsFile();
                    if (!string.IsNullOrEmpty(staticsCCode))
                    {
                        var staticsCFile = Path.Combine(outputDir, $"{modulePrefix}_statics.c");
                        await File.WriteAllTextAsync(staticsCFile, staticsCCode);
                        moduleCFiles.Add(staticsCFile);
                    }

                    await SaveGeneratedFiles(modulePath, moduleCFiles);
                }

                cFiles.AddRange(cachedGeneratedFiles ?? moduleCFiles);

                var cacheLabel = cachedGeneratedFiles != null ? ", cached C" : "";
                Console.WriteLine($"  → {PathUtility.GetModuleDisplayName(modulePath)} ({functions.Count} function{(functions.Count > 1 ? "s" : "")}{cacheLabel})");
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
            var runtimeFiles = new List<string>
            {
                "runtime_alloc.c",     // Minimal: raw AllocMem/FreeMem wrappers (no deps)
                "runtime_core.c",      // Core: memset, memcpy, strlen, error display
                "runtime_compare.c",   // VBCC-safe comparison sequence points
                "runtime_errors.c",    // Assert, panic, bounds check, div check
                "runtime_hwdetect.c",  // CPU, FPU, chipset detection
                "runtime_fmt.c",       // Integer to string conversions
                "runtime_semaphore.c", // Semaphore wrappers
                "runtime_mmu.c",       // MMU detection and null page protection
                "runtime_memtrack.c",  // Memory tracking for debugging
            };
            if (usesNativeFloat) runtimeFiles.Add("runtime_float.c");

            foreach (var runtimeFile in runtimeFiles)
            {
                var runtimeCFile = PathUtility.FindRuntimeFile(runtimeFile);
                if (runtimeCFile == null)
                {
                    Console.Error.WriteLine($"error: runtime file '{runtimeFile}' not found in {PathUtility.GetRuntimeDir()}");
                    Console.Error.WriteLine("The Novus installation is incomplete: the 'runtime' directory must sit beside the compiler binary.");
                    return 1;
                }
                // VBCC creates intermediate assembly beside each C input even with
                // -notmpfile. Never point concurrent builds at the installed runtime.
                var localRuntimeCFile = Path.Combine(outputDir, runtimeFile);
                File.Copy(runtimeCFile, localRuntimeCFile, overwrite: true);
                cFiles.Add(localRuntimeCFile);
            }
            var runtimeHeader = PathUtility.FindRuntimeFile("novus_runtime.h");
            if (runtimeHeader != null)
                File.Copy(runtimeHeader, Path.Combine(outputDir, "novus_runtime.h"), overwrite: true);
            Console.WriteLine($"  → {runtimeFiles.Count} runtime modules (split for DCE)");

            // Handle emit-only mode (just generate C files and stop)
            if (options.EmitAsmOnly)
            {
                if (compilationCache != null)
                    await compilationCache.FlushAsync();
                Console.WriteLine($"\nC files and header written to: {outputDir}");
                Console.WriteLine($"  novus_types.h (shared types)");
                Console.WriteLine($"  {cFiles.Count} function file{(cFiles.Count > 1 ? "s" : "")}");
                return 0;
            }

            ReportPhase("C generation");

            // Compile C code with VBCC
            Console.WriteLine("Compiling with VBCC...");
            var toolchain = new VbccToolchain(options.VbccPath, options.NdkPath, options.Verbose);

            async Task<bool> AssembleCached(string sourceFile, string objectFile, string cpu, bool withFpu)
            {
                var signature = $"v{CompilerCacheVersion}|{cpu}|{withFpu}|{ComputeFileHash(sourceFile)}";
                var signatureFile = objectFile + ".novus-asm";
                if (File.Exists(objectFile) && File.Exists(signatureFile) &&
                    await File.ReadAllTextAsync(signatureFile) == signature)
                {
                    return true;
                }

                if (!await toolchain.Assemble(sourceFile, objectFile, cpu, withFpu))
                    return false;

                await AtomicCacheWriter.WriteFileAtomicallyAsync(signatureFile, signature);
                return true;
            }

            // Link assembly stubs for AmigaOS library calls
            // Our assembly stubs use i32 signatures, avoiding VBCC's type system (BPTR, CONST_STRPTR, etc.)
            var objectFiles = new List<string>();

            // Map "auto" CPU to a concrete target for assembly (vasm doesn't understand "auto")
            var assemblyCpu = options.Cpu == "auto" ? "68020" : options.Cpu;

            // Determine if FPU instructions should be accepted by the assembler
            // Enable FPU for "auto" mode (runtime dispatch) or explicit FPU modes
            var enableFpu = options.Fpu == "auto" || options.Fpu == "68881" || options.Fpu == "68882" ||
                           options.Fpu == "68040" || options.Fpu == "68060";

            var isLibrary = options.ProjectType.Equals("library", StringComparison.OrdinalIgnoreCase);
            var isDevice = options.ProjectType.Equals("device", StringComparison.OrdinalIgnoreCase);
            var isResource = options.ProjectType.Equals("resource", StringComparison.OrdinalIgnoreCase);
            var isExecutable = !isLibrary && !isDevice && !isResource;

            // Every target gets one exact FFI lifecycle object. For programs it is
            // called by startup; libraries/devices call it from their generated lifecycle.
            var ffiRuntimeSource = Path.Combine(outputDir, "novus_ffi_runtime.s");
            await File.WriteAllTextAsync(ffiRuntimeSource,
                FfiRuntimeGenerator.Generate(requiredFfiModules, includeWorkbenchStartup: isExecutable));
            var ffiRuntimeObj = Path.Combine(outputDir, "novus_ffi_runtime.o");
            if (!await AssembleCached(ffiRuntimeSource, ffiRuntimeObj, assemblyCpu, false))
            {
                Console.WriteLine("Failed to assemble FFI lifecycle");
                return 1;
            }
            // NOTE: deliberately NOT added here. AmigaOS starts execution at the first
            // byte of the first hunk, so novus_startup.o must be the first object in the
            // link. Adding this object first put ___novus_ffi_init at CODE+0, and the
            // program ran that stub instead of _start (exited immediately with d0=1).
            // It is appended after the core files below.

            // Assemble core Novus runtime files (only for executables, not libraries)
            if (isExecutable)
            {
                // Only executables need startup code and library initialization
                var startup = GetStartupStub(options.ProjectType);
                var coreFiles = new List<string> { startup, "debug_gfxbase", "math_sqrt", "math_fixed", "math_trig", "math_vec2", "math_core", "math_angle", "math_interp" };
                if (allModulesIR.Values.Any(module =>
                        Path.GetFileName(module.ModulePath).Equals("bsdsocket.novus", StringComparison.OrdinalIgnoreCase)))
                    coreFiles.Add("bsdsocket_bases");
                if (usesAmiSsl)
                    coreFiles.Add("amissl_bases");
                if (options.ProjectType.Equals("handler", StringComparison.OrdinalIgnoreCase))
                    coreFiles.Add("dos_init");
                if (requiredFfiModules.Any(module => module.BaseSymbol == "_MUIMasterBase"))
                    coreFiles.Add("mui_init");
                // Files that require FPU instructions (68881+)
                var fpuRequiredFiles = new HashSet<string> { "math_sqrt" };
                foreach (var coreFile in coreFiles)
                {
                    var coreSource = Path.Combine(compilerDir, "stubs", $"{coreFile}.s");
                    if (File.Exists(coreSource))
                    {
                        var coreObj = Path.Combine(outputDir, $"{coreFile}.o");
                        var needsFpu = fpuRequiredFiles.Contains(coreFile);
                        if (!await AssembleCached(coreSource, coreObj, assemblyCpu, needsFpu))
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
                if (!await AssembleCached(stackConfigSource, stackConfigObj, assemblyCpu, false))
                {
                    Console.WriteLine("Failed to assemble stack_config");
                    return 1;
                }
                objectFiles.Add(stackConfigObj);
            }

            // Safe to add now: for executables novus_startup.o is already first, so
            // _start keeps CODE+0 (the AmigaOS entry point).
            objectFiles.Add(ffiRuntimeObj);

            // Assemble runtime library assembly files (needed for all project types)
            var runtimeAsmFiles = new List<string> { "runtime_mem", "runtime_library_error" };
            if (linkedFunctions.Contains("write") || linkedFunctions.Contains("_write"))
                runtimeAsmFiles.Insert(0, "novus_io");
            foreach (var runtimeFile in runtimeAsmFiles)
            {
                var runtimeSource = PathUtility.FindRuntimeFile($"{runtimeFile}.s");
                if (runtimeSource == null)
                {
                    Console.Error.WriteLine($"error: runtime file '{runtimeFile}.s' not found in {PathUtility.GetRuntimeDir()}");
                    Console.Error.WriteLine("The Novus installation is incomplete: the 'runtime' directory must sit beside the compiler binary.");
                    return 1;
                }

                var runtimeObj = Path.Combine(outputDir, $"{runtimeFile}.o");
                if (!await AssembleCached(runtimeSource, runtimeObj, assemblyCpu, false))
                {
                    Console.WriteLine($"Failed to assemble {runtimeFile}");
                    return 1;
                }
                objectFiles.Add(runtimeObj);
            }

            // For libraries, assemble the A6 wrapper file and library stub
            // BUT DON'T ADD TO objectFiles YET - wrappers must come AFTER C code
            string? wrapperObj = null;
            if (isLibrary || isDevice || isResource)
            {
                var wrapperAsmFile = Path.Combine(outputDir, $"{baseName}_wrappers.s");
                if (File.Exists(wrapperAsmFile))
                {
                    wrapperObj = Path.Combine(outputDir, $"{baseName}_wrappers.o");
                    if (!await AssembleCached(wrapperAsmFile, wrapperObj, assemblyCpu, false))
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
                    if (!await AssembleCached(stubAsmFile, stubObj, assemblyCpu, false))
                    {
                        Console.WriteLine("Failed to assemble library stub");
                        return 1;
                    }
                    // Don't add to objectFiles - this is for users to link against
                    Console.WriteLine($"  ✓ Assembled library stub: {baseName}_lib.o");
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
                    if (!await AssembleCached(asmFile, objFile, assemblyCpu, false))
                    {
                        Console.Error.WriteLine($"\n✗ Failed to assemble {asmFileName}");
                        return EXIT_COMPILE_ERROR;
                    }

                    objectFiles.Add(objFile);
                }
            }

            // ============================================================================
            // OPTIMIZATION: Use pre-compiled stdlib .o files if available
            // Auto-compile stdlib on first use for this CPU/mode combination
            //
            // BULLETPROOF CACHING: Uses file-based locking and atomic writes to prevent
            // race conditions when multiple compilations run in parallel.
            // ============================================================================

            var buildModeStr = options.BuildMode == BuildMode.Release ? "release" : "debug";
            var stdlibCacheRootDir = Path.Combine(compilerDir, "stdlib");
            var stdlibVariant = $"{options.Fpu}-O{options.OptimizationLevel}-S{(int)safetyLevel}";
            var stdlibPrecompiledDir = Path.Combine(stdlibCacheRootDir, assemblyCpu, buildModeStr, stdlibVariant, typesHeaderHash);

            // Create cache lock manager for cross-process synchronization
            Directory.CreateDirectory(stdlibCacheRootDir);
            using var cacheLockManager = new CacheLockManager(stdlibCacheRootDir);

            // Check if stdlib cache should be used
            // Reuse stdlib objects by default; the cache path includes every
            // compilation mode that can change generated code or object ABI.
            // Use --rebuild-stdlib-cache to rebuild and cache for future use
            bool forceRebuildAndCache = options.RebuildStdlibCache;
            bool useCache = !options.NoCache && !forceRebuildAndCache;
            string? cacheInvalidReason = null;
            bool needsRebuild = forceRebuildAndCache
                || !useCache
                || !Directory.Exists(stdlibPrecompiledDir)
                || !AtomicCacheWriter.IsCacheComplete(stdlibPrecompiledDir);

            // CRITICAL FIX: If stdlib cache is stale, delete ALL cached .o files
            // This prevents using stale object files with old constant values
            // Use locking to prevent race conditions during cache invalidation
            if (needsRebuild && !options.NoCache && Directory.Exists(stdlibPrecompiledDir))
            {
                var reason = cacheInvalidReason ?? (forceRebuildAndCache ? "forced rebuild" : "cache not used by default");
                Console.WriteLine($"\n⚠ Stdlib cache invalidated: {reason}");
                Console.WriteLine($"  Clearing cached stdlib objects for {assemblyCpu}/{buildModeStr}...");

                // Acquire lock before modifying cache
                var lockName = $"stdlib-{assemblyCpu}-{buildModeStr}";
                using var cacheLock = await cacheLockManager.AcquireLockAsync(lockName, TimeSpan.FromSeconds(30));
                if (cacheLock == null)
                {
                    Console.WriteLine($"  Warning: Could not acquire cache lock - another process may be building stdlib");
                }

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
                    // Delete the completion marker
                    var completionMarker = Path.Combine(stdlibPrecompiledDir, ".complete");
                    if (File.Exists(completionMarker))
                    {
                        File.Delete(completionMarker);
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

            // Resolve the longest module prefix so names such as `block_device_read`
            // are not mistaken for files belonging to `block_device`.
            var sourceCandidates = allModulesIR
                .Select(item => (Prefix: GetGeneratedModulePrefix(item.Key, item.Value.ModuleName), SourcePath: item.Key))
                .Append((Prefix: baseName, SourcePath: options.InputFile))
                .ToList();
            foreach (var cFile in cFiles)
            {
                var sourcePath = ResolveGeneratedSourcePath(
                    Path.GetFileNameWithoutExtension(cFile), sourceCandidates);
                if (sourcePath != null)
                {
                    cFileToSource[cFile] = (sourcePath, ComputeFileHash(cFile));
                }
            }

            // Runtime, generated support, and additional C files are just as cacheable
            // as Novus-generated function files when their content is unchanged.
            foreach (var cFile in cFiles.Where(File.Exists))
            {
                cFileToSource.TryAdd(cFile, (cFile, ComputeFileHash(cFile)));
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

            // Release builds let VBCC see every generated/runtime C translation unit at
            // once. -sec-per-obj keeps linker DCE effective; the content signature avoids
            // repeating the expensive combined compile when its inputs are unchanged.
            var wholeProgramRelease = options.BuildMode == BuildMode.Release &&
                                      options.OptimizationLevel == 3 && cFiles.Count > 0;
            if (wholeProgramRelease)
            {
                var wholeProgramCFiles = cFiles
                    .Where(File.Exists)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList();
                var wholeProgramObj = Path.Combine(outputDir, $"{baseName}_whole_program.o");
                var signatureFile = wholeProgramObj + ".novus-whole";
                var metadataFile = wholeProgramObj + ".meta";
                var signature = ComputeWholeProgramCacheKey(
                    wholeProgramCFiles.Select(path => (path, cFileToSource[path].hash)),
                    typesHeaderHash,
                    assemblyCpu,
                    options.Fpu,
                    options.OptimizationLevel);

                var cached = false;
                if (!options.NoCache && File.Exists(wholeProgramObj) && File.Exists(signatureFile) && File.Exists(metadataFile) &&
                    await File.ReadAllTextAsync(signatureFile) == signature)
                {
                    try
                    {
                        var metadata = System.Text.Json.JsonSerializer.Deserialize(
                            await File.ReadAllTextAsync(metadataFile),
                            CacheMetadataJsonContext.Default.CacheMetadata);
                        var info = new FileInfo(wholeProgramObj);
                        cached = metadata != null && metadata.FileSize == info.Length &&
                                 (metadata.LastWriteTimeUtcTicks == info.LastWriteTimeUtc.Ticks ||
                                  metadata.FileHash == ComputeFileHash(wholeProgramObj));
                    }
                    catch
                    {
                        cached = false;
                    }
                }

                if (cached)
                {
                    Console.WriteLine($"\n  ✓ Using cached whole-program object ({wholeProgramCFiles.Count} C files)");
                }
                else
                {
                    File.Delete(signatureFile);
                    Console.WriteLine($"\nCompiling whole program ({wholeProgramCFiles.Count} C files)...");
                    if (!await toolchain.CompileWholeProgramToObject(
                            wholeProgramCFiles,
                            wholeProgramObj,
                            assemblyCpu,
                            options.OptimizationLevel,
                            new[] { outputDir },
                            enableFpu))
                    {
                        Console.Error.WriteLine("\n✗ Whole-program C compilation failed");
                        return EXIT_COMPILE_ERROR;
                    }

                    if (!options.NoCache)
                    {
                        var info = new FileInfo(wholeProgramObj);
                        var metadata = new CacheMetadata
                        {
                            FileSize = info.Length,
                            FileHash = ComputeFileHash(wholeProgramObj),
                            LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks,
                            CachedAt = DateTime.UtcNow
                        };
                        await AtomicCacheWriter.WriteFileAtomicallyAsync(metadataFile,
                            System.Text.Json.JsonSerializer.Serialize(metadata, CacheMetadataJsonContext.Default.CacheMetadata));
                        await AtomicCacheWriter.WriteFileAtomicallyAsync(signatureFile, signature);
                    }
                }

                objectFiles.Add(wholeProgramObj);
                stdlibCFiles.Clear();
                userCFiles.Clear();
            }

            // Step 1: Link pre-compiled stdlib .o files (if available) or compile stdlib from scratch
            var stdlibOFilesToCache = new List<(string source, string obj)>();

            if (usePrecompiledStdlib && stdlibCFiles.Count > 0)
            {
                Console.WriteLine($"\nUsing pre-compiled stdlib modules ({stdlibCFiles.Count} files)...");

                // CRITICAL: Validate types header hash before using cached stdlib
                // If the types header changed (ABI change), we MUST recompile
                var cachedTypesHashPath = Path.Combine(stdlibPrecompiledDir, "novus_types.h.hash");
                var typesHeaderValid = false;
                if (File.Exists(cachedTypesHashPath))
                {
                    try
                    {
                        var cachedTypesHash = await File.ReadAllTextAsync(cachedTypesHashPath);
                        typesHeaderValid = cachedTypesHash.Trim() == typesHeaderHash;
                        if (!typesHeaderValid)
                        {
                            Console.WriteLine($"  ⚠ Types header changed (ABI change) - must recompile stdlib");
                        }
                    }
                    catch
                    {
                        // Hash file read failed - invalidate cache
                    }
                }

                // If types header is invalid, force rebuild
                if (!typesHeaderValid)
                {
                    usePrecompiledStdlib = false;
                    needsRebuild = true;
                }
                else
                {
                    // Map stdlib C files to their corresponding .o files
                    var precompiledFiles = new HashSet<string>();
                    foreach (var cFile in stdlibCFiles)
                    {
                        var cFileName = Path.GetFileNameWithoutExtension(cFile);
                        var precompiledObj = Path.Combine(stdlibPrecompiledDir, $"{cFileName}.o");
                        var cachedHashPath = precompiledObj + ".hash";
                        var generatedHash = cFileToSource[cFile].hash;

                        if (File.Exists(precompiledObj) && File.Exists(cachedHashPath) &&
                            (await File.ReadAllTextAsync(cachedHashPath)).Trim() == generatedHash)
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

                        var missingFiles = stdlibCFiles
                            .Where(cFile => !precompiledFiles.Contains(Path.GetFileNameWithoutExtension(cFile)))
                            .ToList();
                        foreach (var batch in missingFiles.Chunk(Math.Max(1, Environment.ProcessorCount)))
                        {
                            var results = await Task.WhenAll(batch.Select(async cFile =>
                            {
                                var objFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(cFile) + ".o");
                                var success = await toolchain.CompileToObject(cFile, objFile, assemblyCpu,
                                    options.OptimizationLevel, options.BuildMode, enableFpu: enableFpu);
                                return (cFile, objFile, success);
                            }));

                            foreach (var result in results)
                            {
                                if (!result.success)
                                {
                                    Console.Error.WriteLine($"\n✗ Failed to compile {Path.GetFileName(result.cFile)}");
                                    return EXIT_COMPILE_ERROR;
                                }

                                Console.WriteLine($"    → {Path.GetFileName(result.cFile)}");
                                objectFiles.Add(result.objFile);
                                stdlibOFilesToCache.Add((result.cFile, result.objFile));
                            }
                        }
                    }
                }  // end if typesHeaderValid
            }
            if (!usePrecompiledStdlib && stdlibCFiles.Count > 0)
            {
                // No pre-compiled stdlib cache exists - compile and cache all stdlib files
                Console.WriteLine($"\nCompiling stdlib modules ({stdlibCFiles.Count} files)...");
                foreach (var batch in stdlibCFiles.Chunk(Math.Max(1, Environment.ProcessorCount)))
                {
                    var results = await Task.WhenAll(batch.Select(async cFile =>
                    {
                        var objFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(cFile) + ".o");
                        var success = await toolchain.CompileToObject(cFile, objFile, assemblyCpu, options.OptimizationLevel, options.BuildMode, enableFpu: enableFpu);
                        return (cFile, objFile, success);
                    }));

                    foreach (var result in results)
                    {
                        if (!result.success)
                        {
                            Console.Error.WriteLine($"\n✗ Failed to compile {Path.GetFileName(result.cFile)}");
                            return EXIT_COMPILE_ERROR;
                        }

                        Console.WriteLine($"  → {Path.GetFileName(result.cFile)}");
                        objectFiles.Add(result.objFile);
                        stdlibOFilesToCache.Add((result.cFile, result.objFile));
                    }
                }
            }

            // Cache any newly compiled stdlib .o files (from either path above)
            // Use locking and atomic writes to prevent race conditions
            if (stdlibOFilesToCache.Count > 0 && !options.NoCache)
            {
                Console.WriteLine($"\n  ✓ Caching {stdlibOFilesToCache.Count} stdlib object files for future builds...");

                // Acquire lock before writing to cache
                var lockName = $"stdlib-{assemblyCpu}-{buildModeStr}";
                using var cacheLock = await cacheLockManager.AcquireLockAsync(lockName, TimeSpan.FromSeconds(60));
                if (cacheLock == null)
                {
                    Console.WriteLine($"  Warning: Could not acquire cache lock - skipping cache write");
                }
                else
                {
                    var existingStdlibObjects = Directory.Exists(stdlibPrecompiledDir)
                        ? Directory.GetFiles(stdlibPrecompiledDir, "*.o").ToList()
                        : new List<string>();

                    // Use atomic cache writer to ensure all-or-nothing cache updates
                    await AtomicCacheWriter.WriteAtomicallyAsync(stdlibPrecompiledDir, async tempDir =>
                    {
                        foreach (var obj in existingStdlibObjects)
                        {
                            File.Copy(obj, Path.Combine(tempDir, Path.GetFileName(obj)), overwrite: true);
                            var hashPath = obj + ".hash";
                            if (File.Exists(hashPath))
                                File.Copy(hashPath, Path.Combine(tempDir, Path.GetFileName(hashPath)), overwrite: true);
                        }

                        // Copy all object files to temp directory
                        foreach (var (source, obj) in stdlibOFilesToCache)
                        {
                            var cachedPath = Path.Combine(tempDir, Path.GetFileName(obj));
                            File.Copy(obj, cachedPath, overwrite: true);
                            await File.WriteAllTextAsync(cachedPath + ".hash", cFileToSource[source].hash);
                        }

                        // CRITICAL: Store the types header with the cache
                        // This ensures cache invalidation when types header changes (ABI change)
                        var cachedTypesHeaderPath = Path.Combine(tempDir, "novus_types.h");
                        await File.WriteAllTextAsync(cachedTypesHeaderPath, sharedTypesHeader);

                        // Store the types header hash for validation
                        var cachedTypesHashPath = Path.Combine(tempDir, "novus_types.h.hash");
                        await File.WriteAllTextAsync(cachedTypesHashPath, typesHeaderHash);

                        // Write manifest with source file hashes for cache invalidation
                        var stdlibSourcePaths = allModulesIR
                            .Where(kvp =>
                            {
                                var relative = Path.GetRelativePath(stdLibPath, kvp.Key);
                                return relative != ".." &&
                                       !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                       !relative.StartsWith($"tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                            })
                            .Select(kvp => kvp.Key)
                            .ToList();

                        await Commands.StdlibBuildCommand.WriteManifest(
                            tempDir,
                            assemblyCpu,
                            options.BuildMode,
                            stdlibSourcePaths,
                            CompilerCacheVersion);
                    });

                    Console.WriteLine($"  ✓ Cache written atomically with types header");
                }
            }

            Console.WriteLine("\nFFI dependencies:");
            var requiredStubModules = requiredFfiModules
                .Select(binding => binding.ModuleName)
                .Append("exec")
                .Concat(usesAmiSsl ? ["amissl"] : [])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            Console.WriteLine($"  {string.Join(", ", requiredFfiModules.Select(m => m.LibraryName).Prepend("exec.library"))}");

            foreach (var stub in requiredStubModules)
            {
                var stubSource = Path.Combine(compilerDir, "stubs", $"{stub}_stubs.s");
                if (File.Exists(stubSource))
                {
                    var stubObj = Path.Combine(outputDir, $"{stub}_stubs.o");
                    if (!await AssembleCached(stubSource, stubObj, assemblyCpu, false))
                    {
                        Console.WriteLine($"Failed to assemble {stub} stubs");
                        return 1;
                    }
                    objectFiles.Add(stubObj);
                }
            }

            // Step 2: Compile user C files with caching and parallelization
            Console.WriteLine("\nCompiling user code...");

            // Create user code cache directory: {outputDir}/usercache/{cpu}/{buildMode}/
            var userCacheDir = Path.Combine(outputDir, "usercache", assemblyCpu, buildModeStr);
            Directory.CreateDirectory(userCacheDir);

            // Validate cache version and clean if stale
            ValidateAndCleanCache(userCacheDir, CompilerCacheVersion);

            // Track which files need compilation
            var filesToCompile = new List<(string cFile, string objFile, bool cached)>();

            foreach (var cFile in userCFiles)
            {
                var cFileName = Path.GetFileNameWithoutExtension(cFile);
                var objFile = Path.Combine(outputDir, cFileName + ".o");

                // Check if we have a cached .o file
                var cached = false;
                if (cFileToSource.TryGetValue(cFile, out var sourceInfo))
                {
                    var (sourcePath, cFileHash) = sourceInfo;
                    // Cache key must match the format used when caching: codegen version + C file hash + header + CPU + FPU + buildmode + optlevel
                    // We use C file hash (not source hash) to detect compiler codegen changes
                    // CRITICAL: Include FPU mode and build mode to prevent incorrect cache hits
                    var cacheKey = $"v{CompilerCacheVersion}_{cFileHash}_{typesHeaderHash.Substring(0, 8)}_{assemblyCpu}_{options.Fpu}_{buildModeStr}_O{options.OptimizationLevel}_{cFileName}.o";
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
                                var unchanged = meta.FileSize == objInfo.Length &&
                                                meta.LastWriteTimeUtcTicks == objInfo.LastWriteTimeUtc.Ticks;
                                if (unchanged || meta.FileSize == objInfo.Length && meta.FileHash == ComputeFileHash(cachedObjFile))
                                {
                                    // Cache is valid - use it
                                    objectFiles.Add(cachedObjFile);
                                    cached = true;
                                    if (!unchanged)
                                    {
                                        meta.LastWriteTimeUtcTicks = objInfo.LastWriteTimeUtc.Ticks;
                                        var updatedJson = System.Text.Json.JsonSerializer.Serialize(meta, CacheMetadataJsonContext.Default.CacheMetadata);
                                        File.WriteAllText(cachedMetaFile, updatedJson);
                                    }
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
                        var success = await toolchain.CompileToObject(cFile, objFile, assemblyCpu, options.OptimizationLevel, options.BuildMode, enableFpu: enableFpu);
                        if (!success)
                        {
                            return (success: false, cFileName, objFile, cFile, cacheInfo: (string.Empty, string.Empty));
                        }

                        // Prepare cache info (but don't copy yet - wait until all succeed)
                        var cacheInfo = ("", "");
                        var cFileNameNoExt = Path.GetFileNameWithoutExtension(cFile);
                        if (cFileToSource.TryGetValue(cFile, out var sourceInfo))
                        {
                            var (sourcePath, cFileHash) = sourceInfo;
                            // Cache key includes: codegen version + C file hash + types header hash + CPU + FPU + buildmode + optimization level
                            // We use C file hash (not source hash) to detect compiler codegen changes
                            // This ensures cache invalidation when any of these change
                            // CRITICAL: Include FPU mode and build mode to prevent incorrect cache hits
                            var cacheKey = $"v{CompilerCacheVersion}_{cFileHash}_{typesHeaderHash.Substring(0, 8)}_{assemblyCpu}_{options.Fpu}_{buildModeStr}_O{options.OptimizationLevel}_{cFileNameNoExt}.o";
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
                    Console.Error.WriteLine($"\n✗ Failed to compile:");
                    foreach (var failure in failures)
                    {
                        Console.Error.WriteLine($"  → {failure.cFileName}");
                    }
                    return EXIT_COMPILE_ERROR;
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
                        var objInfo = new FileInfo(cachedObjFile);
                        var objHash = ComputeFileHash(objFile);
                        var metadata = new CacheMetadata
                        {
                            FileSize = objInfo.Length,
                            FileHash = objHash,
                            LastWriteTimeUtcTicks = objInfo.LastWriteTimeUtc.Ticks,
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
            ReportPhase("assembly + object cache");
            Console.WriteLine($"  → {objectFiles.Count} object files");
            if (options.AdditionalLibraries.Count > 0)
            {
                Console.WriteLine($"  → {options.AdditionalLibraries.Count} dependency libraries");
            }

            var linkSignatureFile = Path.GetFullPath(exeFile) + ".novus-link";
            var linkInputs = objectFiles.Select(path =>
            {
                var info = new FileInfo(path);
                return $"{Path.GetFullPath(path)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            });
            var linkSignature = ComputeStringHash(string.Join('\n', linkInputs.Prepend(
                $"v{CompilerCacheVersion}|{options.Fpu}|{options.BuildMode}|{isLibrary}|{isDevice}|{isResource}")));
            var success = File.Exists(exeFile) && File.Exists(linkSignatureFile) &&
                          await File.ReadAllTextAsync(linkSignatureFile) == linkSignature;
            if (success)
            {
                Console.WriteLine("  ✓ Link inputs unchanged");
            }
            else
            {
                success = await toolchain.Link(
                    objectFiles.ToArray(),
                    exeFile,
                    options.Fpu,
                    includeStartup: false,  // startup already in objectFiles
                    isLibrary: isLibrary || isDevice || isResource,  // resident modules need relocations
                    buildMode: options.BuildMode
                );
                if (success)
                    await AtomicCacheWriter.WriteFileAtomicallyAsync(linkSignatureFile, linkSignature);
            }
            ReportPhase("link");

            if (success)
            {
                if (compilationCache != null)
                    await compilationCache.FlushAsync();
                if (buildStampPath != null && buildSignature != null)
                {
                    var sourceRoots = new[] { options.InputFile }.Concat(options.AdditionalSourceFiles);
                    buildSignature = ComputeBuildSignature(options, ComputeCompilationConfigHash(options),
                        compilationCache!.ComputeSourceGraphHash(sourceRoots));
                    await AtomicCacheWriter.WriteFileAtomicallyAsync(buildStampPath, buildSignature);
                }
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
                Console.Error.WriteLine("\n✗ Linking failed");
                return EXIT_COMPILE_ERROR;
            }
        }
        catch (ArgumentException aex)
        {
            // Bad flag combinations (e.g. --unsafe with --safety-level) are usage
            // errors detected before/while validating options → exit 1 (§3).
            Console.Error.WriteLine($"error: {aex.Message}");
            return EXIT_USAGE;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nError: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return EXIT_COMPILE_ERROR;
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
