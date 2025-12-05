using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("clean", HelpText = "Clean all cached artifacts to force fresh rebuilds")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class CleanOptions
{
    [Option('v', "verbose", Required = false, HelpText = "Verbose output showing what's being deleted")]
    public bool Verbose { get; set; }

    [Option("keep-stdlib-cache", Required = false, HelpText = "Don't delete stdlib precompiled cache")]
    public bool KeepStdlibCache { get; set; }

    [Option("keep-bin-std", Required = false, HelpText = "Don't delete bin/std copy")]
    public bool KeepBinStd { get; set; }

    [Option("keep-user-cache", Required = false, HelpText = "Don't delete user cache (~/.novus-cache)")]
    public bool KeepUserCache { get; set; }

    [Option("keep-temp-dirs", Required = false, HelpText = "Don't delete temp build directories")]
    public bool KeepTempDirs { get; set; }
}
