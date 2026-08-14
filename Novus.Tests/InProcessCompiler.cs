using Antlr4.Runtime;
using System.Security.Cryptography;
using System.Text;
using Novus.Codegen;
using Novus.Compilation;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.Preprocessing;
using Novus.SemanticAnalysis;

namespace Novus.Tests;

/// <summary>
/// In-process compiler for fast test execution.
/// Calls compiler phases directly without spawning external processes.
/// Thread-safe for parallel test execution.
/// </summary>
public class InProcessCompiler
{
    private readonly string _stdLibPath;
    private readonly CompilationCache _compilationCache;
    private readonly string _stdLibFingerprint;

    public InProcessCompiler(string stdLibPath, string? cacheDirectory = null)
    {
        _stdLibPath = Path.GetFullPath(stdLibPath);
        var projectRoot = Path.GetFullPath(Path.Combine(_stdLibPath, "..", ".."));
        var compilerVersion =
            BitConverter.ToInt32(typeof(IrBuilder).Assembly.ManifestModule.ModuleVersionId.ToByteArray());
        _compilationCache = new CompilationCache(
            projectRoot,
            compilerVersion,
            cacheDirectory ?? Path.Combine(projectRoot, ".novus-cache", "in-process-tests"));
        _compilationCache.BeginBuild();
        _stdLibFingerprint = ComputeStdLibFingerprint(_stdLibPath);
    }

    private static string ComputeStdLibFingerprint(string stdLibPath)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(stdLibPath, "*.novus", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(stdLibPath, path)));
            hash.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private string GetConfigHash(string cpu, string fpu, BuildMode buildMode)
    {
        var value = $"{cpu}|{fpu}|{buildMode}|full|{_stdLibFingerprint}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Build preprocessor constants for a given compilation configuration
    /// </summary>
    private static Dictionary<string, bool> GetPreprocessorConstants(string cpu, string fpu, BuildMode buildMode, string chipset = "auto")
    {
        return IrBuilderConfiguration.GetPreprocessorConstantsForTarget(
            cpu, fpu, chipset, buildMode == BuildMode.Debug);
    }

    /// <summary>
    /// Result of in-process compilation
    /// </summary>
    public class CompilationResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public IrModule? IrModule { get; init; }
        public List<IrStringLiteral>? StringLiterals { get; init; }
        public string? CCode { get; init; }
        public string? TypesHeader { get; init; }
        public DiagnosticBag Diagnostics { get; init; } = new();
    }

    /// <summary>
    /// Compile a Novus source file to C code (ASM test equivalent).
    /// This performs the same compilation phases as the CLI but in-process:
    /// 1. Preprocess
    /// 2. Parse
    /// 3. Semantic analysis
    /// 4. IR building
    /// 5. C code generation
    /// </summary>
    public async Task<CompilationResult> CompileToCAsync(string inputFile, string cpu = "68020", string fpu = "soft", BuildMode buildMode = BuildMode.Debug)
    {
        try
        {
            // Read source file
            if (!File.Exists(inputFile))
            {
                return new CompilationResult
                {
                    Success = false,
                    ErrorMessage = $"Input file not found: {inputFile}"
                };
            }

            var configHash = GetConfigHash(cpu, fpu, buildMode);
            var (cachedModule, cachedStrings, _) = _compilationCache.GetCachedIrModule(inputFile, configHash);
            if (cachedModule != null && cachedStrings != null)
                return GenerateC(cachedModule, cachedStrings, cpu, fpu, buildMode);

            var source = await File.ReadAllTextAsync(inputFile);
            var diagnostics = new DiagnosticBag();

            // Build preprocessor constants for this compilation
            var preprocessorConstants = GetPreprocessorConstants(cpu, fpu, buildMode);

            // PHASE 1: Preprocess
            var preprocessor = new Preprocessor(preprocessorConstants, diagnostics, inputFile);
            source = preprocessor.Process(source);

            if (diagnostics.HasErrors)
            {
                return new CompilationResult
                {
                    Success = false,
                    ErrorMessage = diagnostics.FormatDiagnostics(),
                    Diagnostics = diagnostics
                };
            }

            // PHASE 2: Parse
            var parser = NovusParserFactory.CreateParser(
                source,
                diagnostics,
                inputFile,
                NovusParserFactory.ParseMode.Compilation
            );

            var compilationUnit = parser.compilationUnit();

            if (diagnostics.HasErrors)
            {
                return new CompilationResult
                {
                    Success = false,
                    ErrorMessage = diagnostics.FormatDiagnostics(),
                    Diagnostics = diagnostics
                };
            }

            // PHASE 3: Semantic Analysis
            var analyzer = new SemanticAnalyzer(inputFile, source, _stdLibPath, preprocessorConstants);
            var analysisSucceeded = analyzer.Analyze(compilationUnit);

            if (!analysisSucceeded || analyzer.Diagnostics.HasErrors)
            {
                return new CompilationResult
                {
                    Success = false,
                    ErrorMessage = analyzer.Diagnostics.FormatDiagnostics(),
                    Diagnostics = analyzer.Diagnostics
                };
            }

            // Get analysis result with overload information for function name mangling
            var analysisResult = analyzer.GetResult();

            // PHASE 4: IR Building - pass analysis result so IrBuilder knows about function overloads
            var irBuilder = new IrBuilder(analysisResult);
            irBuilder.SetStdLibPath(_stdLibPath);
            irBuilder.SetInputFilePath(inputFile);
            var module = irBuilder.BuildModule(compilationUnit);

            if (irBuilder.Diagnostics.HasErrors)
            {
                return new CompilationResult
                {
                    Success = false,
                    ErrorMessage = irBuilder.Diagnostics.FormatDiagnostics(),
                    Diagnostics = irBuilder.Diagnostics
                };
            }

            _compilationCache.CacheCompilationResult(
                inputFile,
                compilationUnit,
                module,
                irBuilder.StringLiterals,
                irBuilder.GetImportedModules(),
                configHash,
                dependencyModules: Array.Empty<string>());
            await _compilationCache.FlushAsync();

            return GenerateC(module, irBuilder.StringLiterals, cpu, fpu, buildMode, irBuilder.Diagnostics);
        }
        catch (Exception ex)
        {
            return new CompilationResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}\n{ex.StackTrace}"
            };
        }
    }

    private static CompilationResult GenerateC(
        IrModule module,
        List<IrStringLiteral> stringLiterals,
        string cpu,
        string fpu,
        BuildMode buildMode,
        DiagnosticBag? diagnostics = null)
    {
        var typeRegistry = new TypeRegistry();
        typeRegistry.RegisterModule(module);

        var allFunctions = module.Functions.ToList();
        var sharedTypesHeader = CCodeGenerator.GenerateSharedTypesHeader(typeRegistry, allFunctions);

        var codeGenerator = new CCodeGenerator(
            module,
            stringLiterals,
            cpu,
            fpu,
            buildMode,
            safetyLevel: SafetyLevel.Full,
            explicitEntryPoints: null,
            useSharedTypesHeader: true,
            projectVersion: null);

        var cCode = codeGenerator.Generate();

        return new CompilationResult
        {
            Success = true,
            IrModule = module,
            StringLiterals = stringLiterals,
            CCode = cCode,
            TypesHeader = sharedTypesHeader,
            Diagnostics = diagnostics ?? new DiagnosticBag()
        };
    }

    /// <summary>
    /// Compile and verify that C code generation succeeds (fast ASM test).
    /// Returns true if compilation succeeded, false otherwise.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> CompileAndVerifyAsync(string inputFile)
    {
        var result = await CompileToCAsync(inputFile);

        if (!result.Success)
        {
            return (false, result.ErrorMessage);
        }

        // Verify that C code was generated
        if (string.IsNullOrWhiteSpace(result.CCode))
        {
            return (false, "C code generation produced empty output");
        }

        // Verify that types header was generated
        if (string.IsNullOrWhiteSpace(result.TypesHeader))
        {
            return (false, "Types header generation produced empty output");
        }

        return (true, null);
    }
}
