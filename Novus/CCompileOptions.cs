using System.Diagnostics.CodeAnalysis;
using CommandLine;

namespace Novus;

[Verb("cc", HelpText = "Compile C to Motorola 68020+ assembly using the Novus backend")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class CCompileOptions
{
    [Value(0, MetaName = "input", Required = true, HelpText = "Input C file (.c)")]
    public string InputFile { get; set; } = "";

    [Option('o', "output", Required = true, HelpText = "Output object file, or assembly with -S")]
    public string OutputFile { get; set; } = "";

    [Option('S', HelpText = "Emit assembly instead of an Amiga Hunk object")]
    public bool EmitAssembly { get; set; }

    [Option("cpu", Default = "68020", HelpText = "Target CPU: 68020, 68030, 68040, or 68060")]
    public string Cpu { get; set; } = "68020";
}
