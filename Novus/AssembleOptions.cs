using System.Diagnostics.CodeAnalysis;
using CommandLine;

namespace Novus;

[Verb("assemble", HelpText = "Assemble Motorola 68020+ source to an Amiga Hunk object")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class AssembleOptions
{
    [Value(0, MetaName = "input", Required = true, HelpText = "Input assembly file (.s)")]
    public string InputFile { get; set; } = "";

    [Option('o', "output", Required = true, HelpText = "Output Amiga Hunk object (.o)")]
    public string OutputFile { get; set; } = "";

    [Option("cpu", Default = "68020", HelpText = "Target CPU: 68020, 68030, 68040, or 68060")]
    public string Cpu { get; set; } = "68020";
}
