using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("generate-stubs", HelpText = "Generate Amiga library stubs and Novus FFI bindings from NDK 3.9 SFD files")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class GenerateStubsOptions
{
    [Option("ndk-path", Required = false, HelpText = "Path to your Amiga NDK 3.9 (default: $NDK, then 'novus config set ndk-path')")]
    public string NdkPath { get; set; } = UserConfig.ResolveNdkPath() ?? "";

    [Option('o', "output", Required = false, HelpText = "Output directory (default: current directory)")]
    public string OutputPath { get; set; } = ".";
}
