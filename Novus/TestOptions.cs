using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

/// <summary>
/// Command line options for the 'test' verb.
/// Discovers @test attributed functions and generates a test runner executable.
/// </summary>
[Verb("test", HelpText = "Build and run tests for Novus projects")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class TestOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "File, directory, or project to test (default: current directory)")]
    public string? Path { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output directory for the test runner executable (default: ./tests/)")]
    public string? OutputDir { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Verbose output showing compilation details")]
    public bool Verbose { get; set; }

    [Option("release", Required = false, HelpText = "Build test runner in release mode (default: debug)")]
    public bool Release { get; set; }

    [Option("cpu", Required = false, Default = "68020", HelpText = "Target CPU (68000, 68020, 68040, 68060)")]
    public string Cpu { get; set; } = "68020";

    [Option("fpu", Required = false, Default = "none", HelpText = "FPU mode (none, 68881, 68882, 68040, 68060)")]
    public string Fpu { get; set; } = "none";

    [Option("safety-level", Required = false, HelpText = "Safety level (0=unsafe, 1=basic, 2=full, 3=paranoid). Default: 2 for debug, 1 for release")]
    public int? SafetyLevel { get; set; }

    [Option("filter", Required = false, HelpText = "Only run tests matching this pattern (e.g., 'test_math_*')")]
    public string? Filter { get; set; }

    [Option("list", Required = false, HelpText = "List discovered tests without building")]
    public bool ListOnly { get; set; }

    [Option('b', "benchmark", Required = false, HelpText = "Enable timing for each test, showing duration in microseconds")]
    public bool Benchmark { get; set; }

    [Option("vbcc-path", Required = false, HelpText = "Path to VBCC installation")]
    public string VbccPath { get; set; } = PathUtility.GetVbccPath();

    [Option("ndk-path", Required = false, HelpText = "Path to Amiga NDK")]
    public string NdkPath { get; set; } = PathUtility.GetNdkPath();
}
