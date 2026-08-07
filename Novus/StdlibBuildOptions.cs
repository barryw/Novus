using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("stdlib-build", HelpText = "Pre-compile standard library for faster linking")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class StdlibBuildOptions
{
    [Option("cpu", Required = false, HelpText = "Build for specific CPU (68000, 68020, 68040, 68060) or 'all' for all targets (default: all)")]
    public string? Cpu { get; set; } = "all";

    [Option("mode", Required = false, HelpText = "Build mode (debug, release, or 'both' for both modes) (default: both)")]
    public string? Mode { get; set; } = "both";

    [Option("vbcc-path", Required = false, HelpText = "Path to VBCC installation (default: $VBCC or ~/amiga-cc/vbcc)")]
    public string? VbccPath { get; set; }

    [Option("ndk-path", Required = false, HelpText = "Path to your Amiga NDK 3.9 (default: $NDK, then 'novus config set ndk-path')")]
    public string? NdkPath { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Verbose output")]
    public bool Verbose { get; set; }
}
