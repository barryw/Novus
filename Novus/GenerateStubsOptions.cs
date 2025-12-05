using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("generate-stubs", HelpText = "Generate Amiga library stubs and Novus FFI bindings from NDK 3.9 SFD files")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class GenerateStubsOptions
{
    [Option("ndk-path", Required = false, HelpText = "Path to NDK installation (default: auto-detect from NDK_PATH env var or common locations)")]
    public string NdkPath { get; set; } = PathUtility.GetNdkPath();

    [Option('o', "output", Required = false, HelpText = "Output directory (default: current directory)")]
    public string OutputPath { get; set; } = ".";
}
