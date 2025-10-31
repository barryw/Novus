using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("generate-stubs", HelpText = "Generate Amiga library stubs and Novus FFI bindings from NDK 3.9 SFD files")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class GenerateStubsOptions
{
    [Option("ndk-path", Required = false, HelpText = "Path to NDK installation (default: /Users/barry/amiga-cc/NDK3.9)")]
    public string NdkPath { get; set; } = "/Users/barry/amiga-cc/NDK3.9";

    [Option('o', "output", Required = false, HelpText = "Output directory (default: current directory)")]
    public string OutputPath { get; set; } = ".";
}
