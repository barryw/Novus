using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("build", HelpText = "Build a project using project.toml configuration")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class BuildOptions
{
    [Option('p', "project", Required = false, HelpText = "Path to project directory or project.toml file (default: current directory)")]
    public string? ProjectPath { get; set; }

    [Option("cpu", Required = false, HelpText = "Override target CPU from project.toml")]
    public string? Cpu { get; set; }

    [Option("fpu", Required = false, HelpText = "Override FPU mode from project.toml")]
    public string? Fpu { get; set; }

    [Option("chipset", Required = false, HelpText = "Override target chipset from project.toml (OCS, ECS, AGA)")]
    public string? Chipset { get; set; }

    [Option('O', "optimize", Required = false, HelpText = "Override optimization level from project.toml")]
    public int? OptimizationLevel { get; set; }

    [Option("emit-asm", Required = false, HelpText = "Emit assembly only, don't assemble/link")]
    public bool EmitAsmOnly { get; set; }

    [Option("vbcc-path", Required = false, HelpText = "Path to VBCC installation (default: /Users/barry/amiga-cc/vbcc)")]
    public string? VbccPath { get; set; }

    [Option("ndk-path", Required = false, HelpText = "Path to NDK installation (default: /Users/barry/amiga-cc/NDK3.9)")]
    public string? NdkPath { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Verbose output")]
    public bool Verbose { get; set; }

    [Option("release", Required = false, HelpText = "Build in release mode (optimization level 2, no debug symbols)")]
    public bool Release { get; set; }

    [Option("debug", Required = false, HelpText = "Build in debug mode (no optimization, debug symbols, bounds checking) - this is the default")]
    public bool Debug { get; set; }

    [Option("safety-level", Required = false, HelpText = "Safety level (0=unsafe, 1=basic, 2=full, 3=paranoid). Default: 2 for debug, 1 for release")]
    public int? SafetyLevel { get; set; }

    [Option("unsafe", Required = false, HelpText = "Disable all safety checks (equivalent to --safety-level 0)")]
    public bool UnsafeMode { get; set; }
}
