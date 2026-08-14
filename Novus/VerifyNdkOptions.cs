using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("verify-ndk", HelpText = "Verify amiga::raw against the pinned classic AmigaOS NDK inventory")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class VerifyNdkOptions
{
    [Option("ndk-path", Required = false, HelpText = "Optional NDK 3.9 path; verifies the checked-in inventory against the original headers")]
    public string? NdkPath { get; set; }

    [Option("manifest", Required = false, HelpText = "Coverage manifest path")]
    public string? ManifestPath { get; set; }

    [Option("raw-path", Required = false, HelpText = "amiga::raw source directory")]
    public string? RawPath { get; set; }

    [Option("update", Required = false, HelpText = "Regenerate the manifest from --ndk-path and the current raw bindings")]
    public bool Update { get; set; }
}
