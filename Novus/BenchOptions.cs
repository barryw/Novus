using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

/// <summary>
/// Command line options for the 'bench' verb.
/// Discovers @bench attributed functions and generates a benchmark runner executable.
/// </summary>
[Verb("bench", HelpText = "Build and run benchmarks for Novus projects")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class BenchOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "File, directory, or project to benchmark (default: current directory)")]
    public string? Path { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output directory for the benchmark runner executable (default: ./bench/)")]
    public string? OutputDir { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Verbose output showing compilation details")]
    public bool Verbose { get; set; }

    [Option("release", Required = false, Default = true, HelpText = "Build benchmark runner in release mode (default: true for accurate timing)")]
    public bool Release { get; set; } = true;

    [Option("cpu", Required = false, Default = "68020", HelpText = "Target CPU (68020, 68030, 68040, 68060, 68080)")]
    public string Cpu { get; set; } = "68020";

    [Option("fpu", Required = false, Default = "none", HelpText = "FPU mode (none, 68881, 68882, 68040, 68060)")]
    public string Fpu { get; set; } = "none";

    [Option("filter", Required = false, HelpText = "Only run benchmarks matching this pattern (e.g., 'bench_math_*')")]
    public string? Filter { get; set; }

    [Option("list", Required = false, HelpText = "List discovered benchmarks without building")]
    public bool ListOnly { get; set; }

    [Option("iterations", Required = false, Default = 0, HelpText = "Fixed iteration count for all benchmarks (0 = auto-detect)")]
    public int Iterations { get; set; }

    [Option("vbcc-path", Required = false, HelpText = "Path to VBCC installation")]
    public string VbccPath { get; set; } = PathUtility.GetVbccPath();

    [Option("ndk-path", Required = false, HelpText = "Path to your Amiga NDK 3.9 (default: $NDK, then 'novus config set ndk-path')")]
    public string NdkPath { get; set; } = UserConfig.ResolveNdkPath() ?? "";
}
