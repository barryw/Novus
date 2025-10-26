using CommandLine;

namespace Novus;

[Verb("generate-stubs", HelpText = "Generate Amiga library stubs and Novus modules from NDK .fd files")]
public class GenerateStubsOptions
{
    [Option('l', "library", Required = false, HelpText = "Generate stubs for a specific library (e.g., 'dos', 'graphics')")]
    public string? Library { get; set; }

    [Option('a', "all", Required = false, HelpText = "Generate stubs for all common libraries")]
    public bool GenerateAll { get; set; }

    [Option("list", Required = false, HelpText = "List all available libraries in the NDK")]
    public bool ListLibraries { get; set; }

    [Option("ndk-path", Required = false, HelpText = "Path to NDK installation (default: /Users/barry/amiga-cc/NDK3.9)")]
    public string NdkPath { get; set; } = "/Users/barry/amiga-cc/NDK3.9";

    [Option('o', "output", Required = false, HelpText = "Output directory (default: current directory)")]
    public string OutputPath { get; set; } = ".";
}
