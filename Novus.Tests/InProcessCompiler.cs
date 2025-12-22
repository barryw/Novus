using Antlr4.Runtime;
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

    public InProcessCompiler(string stdLibPath)
    {
        _stdLibPath = stdLibPath;
    }

    /// <summary>
    /// Build preprocessor constants for a given compilation configuration
    /// </summary>
    private static Dictionary<string, bool> GetPreprocessorConstants(string cpu, string fpu, BuildMode buildMode, string chipset = "auto")
    {
        // Determine effective CPU for "auto" mode
        cpu = cpu == "auto" ? "68020" : cpu;

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
            ["DEBUG"] = buildMode == BuildMode.Debug,
            ["RELEASE"] = buildMode == BuildMode.Release,

            // Exact CPU target constants
            ["M68000"] = cpu == "68000",
            ["M68010"] = cpu == "68010",
            ["M68020"] = cpu == "68020",
            ["M68030"] = cpu == "68030",
            ["M68040"] = cpu == "68040",
            ["M68060"] = cpu == "68060",

            // "At least" CPU constants
            ["M68000_PLUS"] = cpuLevel >= 0,
            ["M68010_PLUS"] = cpuLevel >= 1,
            ["M68020_PLUS"] = cpuLevel >= 2,
            ["M68030_PLUS"] = cpuLevel >= 3,
            ["M68040_PLUS"] = cpuLevel >= 4,
            ["M68060_PLUS"] = cpuLevel >= 5,

            // FPU target constants
            ["FPU_NONE"] = fpu == "none" || fpu == "soft",
            ["FPU_SOFT"] = fpu == "soft",
            ["FPU_68881"] = fpu == "68881",
            ["FPU_68882"] = fpu == "68882",
            ["FPU_68040"] = fpu == "68040" || (cpu == "68040" && fpu != "none" && fpu != "soft"),
            ["FPU_68060"] = fpu == "68060" || (cpu == "68060" && fpu != "none" && fpu != "soft"),

            // "Has FPU" - true if any hardware FPU is available
            ["HAS_FPU"] = fpu != "none" && fpu != "soft" && fpu != "auto",

            // Chipset target constants
            ["OCS"] = chipset == "OCS",
            ["ECS"] = chipset == "ECS",
            ["AGA"] = chipset == "AGA",

            // "At least" chipset constants
            ["ECS_PLUS"] = chipset == "ECS" || chipset == "AGA",
            ["AGA_PLUS"] = chipset == "AGA"
        };
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

            // PHASE 4: IR Building
            var irBuilder = new IrBuilder();
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

            // PHASE 5: C Code Generation
            var typeRegistry = new TypeRegistry();
            typeRegistry.RegisterModule(module);

            var allFunctions = module.Functions.ToList();
            var sharedTypesHeader = CCodeGenerator.GenerateSharedTypesHeader(typeRegistry, allFunctions);

            // Generate C code for all non-extern functions
            var codeGenerator = new CCodeGenerator(
                module,
                irBuilder.StringLiterals,
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
                StringLiterals = irBuilder.StringLiterals,
                CCode = cCode,
                TypesHeader = sharedTypesHeader,
                Diagnostics = irBuilder.Diagnostics
            };
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
