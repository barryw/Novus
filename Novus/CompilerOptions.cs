using CommandLine;
using System.Diagnostics.CodeAnalysis;
using Novus.Compilation;

namespace Novus;

[Verb("compile", isDefault: true, HelpText = "Compile a Novus source file to an Amiga executable")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class CompilerOptions
{
    [Value(0, MetaName = "input", Required = true, HelpText = "Input Novus source file (.novus)")]
    public string InputFile { get; set; } = "";

    [Option('o', "output", Required = false, HelpText = "Output file name (default: a.out)")]
    public string OutputFile { get; set; } = "a.out";

    [Option("cpu", Required = false, Default = "auto", HelpText = "Target CPU (68020 minimum): auto, 68020, 68030, 68040, 68060, 68080")]
    public string Cpu { get; set; } = "auto";

    [Option("fpu", Required = false, Default = "auto", HelpText = "FPU mode: auto (fat binary with runtime detection), soft (software only), 68881 (68881/68882), 68040 (built-in FPU)")]
    public string Fpu { get; set; } = "auto";

    [Option("chipset", Required = false, Default = "auto", HelpText = "Target chipset: auto (runtime detection), OCS, ECS, AGA")]
    public string Chipset { get; set; } = "auto";

    [Option("emit-asm", Required = false, HelpText = "Emit assembly only, don't assemble/link")]
    public bool EmitAsmOnly { get; set; }

    [Option("emit-ir", Required = false, HelpText = "Emit IR (intermediate representation) to stdout")]
    public bool EmitIr { get; set; }

    [Option("vbcc-path", Required = false, HelpText = "Path to VBCC installation (default: vendored VBCC)")]
    public string VbccPath { get; set; } = PathUtility.GetVbccPath();

    [Option("ndk-path", Required = false, HelpText = "Path to your Amiga NDK 3.9 (default: $NDK, then 'novus config set ndk-path')")]
    public string NdkPath { get; set; } = UserConfig.ResolveNdkPath() ?? "";

    [Option('O', "optimize", Required = false, Default = 1, HelpText = "Optimization level (0-3, default: 1)")]
    public int OptimizationLevel { get; set; } = 1;

    [Option('v', "verbose", Required = false, HelpText = "Verbose output")]
    public bool Verbose { get; set; }

    [Option("release", Required = false, HelpText = "Build in release mode (optimized, no debug symbols)")]
    public bool Release { get; set; }

    [Option("safety-level", Required = false, HelpText = "Safety level (0=unsafe, 1=basic, 2=full, 3=paranoid). Default: 2 for debug, 1 for release")]
    public int? SafetyLevelOption { get; set; }

    [Option("unsafe", Required = false, HelpText = "Disable optional runtime instrumentation (safe indexing remains checked; equivalent to --safety-level 0)")]
    public bool UnsafeMode { get; set; }

    /// <summary>
    /// Build mode (Debug or Release). Set by build command based on --debug/--release flags.
    /// </summary>
    public BuildMode BuildMode { get; set; } = BuildMode.Debug;

    /// <summary>
    /// Highest optimization level we will emit code at.
    ///
    /// This was capped at 2 for a while: vbcc's emit-level peephole collapsed a
    /// "move.l d0,a1 / move.l a1,d0" round trip and removed the second move, which was
    /// the only instruction setting the condition codes. The preceding tst had already
    /// been elided on the strength of it, so the branch read whatever the callee left:
    ///
    ///     jsr     _ReadArgs
    ///     move.l  d0,a1          ; MOVEA - sets no flags
    ///     add.w   #12,a7         ; ADDQ to An - sets no flags
    ///     bne     l44            ; branched on stale flags
    ///
    /// Args::parse took the failure path under vamos and the success path on a real
    /// 68040, on the same binary. Fixed in vendor/vbcc: the peephole's guard only
    /// covered cc_set naming the address register, and now also covers it naming the
    /// data register the removed move writes, re-emitting a tst in that case.
    ///
    /// Note this only holds because the toolchain pins PATH to the vendored vbcc.
    /// A vbcc from elsewhere on PATH would still carry the bug.
    /// </summary>
    public const int MaxOptimizationLevel = 3;
    public static readonly string[] ValidCpuValues = { "68020", "68030", "68040", "68060", "68080", "auto" };

    public static void ValidateCpu(string? cpu)
    {
        if (cpu != null && !ValidCpuValues.Contains(cpu))
            throw new ArgumentException(
                $"Invalid CPU target '{cpu}'. Novus requires 68020 or newer; valid values are: {string.Join(", ", ValidCpuValues)}.");
    }

    /// <summary>
    /// Validate an optimization level.
    /// </summary>
    public static void ValidateOptimizationLevel(int level)
    {
        if (level < 0 || level > MaxOptimizationLevel)
        {
            throw new ArgumentException(
                $"Invalid optimization level: {level}. Must be 0 to {MaxOptimizationLevel}.");
        }
    }

    /// <summary>
    /// Computed safety level based on command-line flags and build mode
    /// </summary>
    public SafetyLevel GetSafetyLevel()
    {
        // Validate conflicting flags
        if (UnsafeMode && SafetyLevelOption.HasValue)
        {
            throw new ArgumentException(
                "Cannot specify both --unsafe and --safety-level. " +
                "Use --unsafe for minimal instrumentation, or --safety-level N for a specific level.");
        }

        // --unsafe flag overrides everything
        if (UnsafeMode)
            return SafetyLevel.Unsafe;

        // Explicit --safety-level flag
        if (SafetyLevelOption.HasValue)
        {
            // Validate range
            if (SafetyLevelOption.Value < 0 || SafetyLevelOption.Value > 3)
            {
                throw new ArgumentException(
                    $"Invalid safety level: {SafetyLevelOption.Value}. " +
                    "Must be 0 (unsafe), 1 (basic), 2 (full), or 3 (paranoid).");
            }

            // Safe to cast after validation
            return (SafetyLevel)SafetyLevelOption.Value;
        }

        // Default based on build mode
        return SafetyLevelExtensions.GetDefaultForBuildMode(BuildMode);
    }

    /// <summary>
    /// Project type (cli, library, device, etc.). Used to determine linking strategy.
    /// </summary>
    public string ProjectType { get; set; } = "cli";

    /// <summary>
    /// Additional library object files to link (for workspace dependencies)
    /// </summary>
    public List<string> AdditionalLibraries { get; set; } = new();

    /// <summary>
    /// Additional C source files to compile and link (e.g., library wrappers)
    /// </summary>
    public List<string> AdditionalCFiles { get; set; } = new();

    /// <summary>
    /// Additional assembly files to assemble and link (e.g., library wrappers)
    /// </summary>
    public List<string> AdditionalAsmFiles { get; set; } = new();

    /// <summary>FFI lifecycles contributed by workspace library dependencies.</summary>
    public List<FfiModuleMetadata> AdditionalFfiModules { get; set; } = new();

    /// <summary>
    /// Additional Novus modules compiled into the target without importing their symbols.
    /// Used by generated test runners to avoid re-analyzing every test module.
    /// </summary>
    public List<string> AdditionalSourceFiles { get; set; } = new();

    /// <summary>
    /// Project name from project.toml. Injected as PKG_NAME compile-time constant.
    /// </summary>
    public string? PackageName { get; set; }

    /// <summary>
    /// Project version in semver format (e.g., "1.0.0"). Injected as PKG_VERSION compile-time constant.
    /// Also used for @library version generation.
    /// </summary>
    public string? PackageVersion { get; set; }

    /// <summary>
    /// Use cached stdlib if available (retained for CLI compatibility; now the default)
    /// </summary>
    [Option("use-stdlib-cache", Required = false, HelpText = "Use cached stdlib if available (default; retained for compatibility)")]
    public bool UseStdlibCache { get; set; }

    /// <summary>
    /// Rebuild stdlib and refresh the cache
    /// </summary>
    [Option("rebuild-stdlib-cache", Required = false, HelpText = "Rebuild stdlib and cache it for future use")]
    public bool RebuildStdlibCache { get; set; }

    /// <summary>
    /// Disable incremental compilation cache (force full rebuild)
    /// </summary>
    [Option("no-cache", Required = false, HelpText = "Disable incremental compilation cache (force full rebuild)")]
    public bool NoCache { get; set; }

    /// <summary>
    /// Display cache hit/miss statistics after compilation
    /// </summary>
    [Option("cache-stats", Required = false, HelpText = "Display cache hit/miss statistics after compilation")]
    public bool CacheStats { get; set; }

    /// <summary>
    /// Code generation backend: c (VBCC) or m68k (direct assembly - EXPERIMENTAL)
    /// </summary>
    [Option("backend", Required = false, Default = "c", HelpText = "Code generation backend: c (VBCC) or m68k (EXPERIMENTAL: direct assembly)")]
    public string Backend { get; set; } = "c";

    /// <summary>
    /// Generate instrumented code for profile collection
    /// </summary>
    [Option("pgo-generate", Required = false, HelpText = "Generate instrumented code for profile collection")]
    public bool PgoGenerate { get; set; }

    /// <summary>
    /// Use profile data for guided optimization
    /// </summary>
    [Option("pgo-use", Required = false, HelpText = "Path to profile data file (.pgo) for profile-guided optimization")]
    public string? PgoUse { get; set; }
}
