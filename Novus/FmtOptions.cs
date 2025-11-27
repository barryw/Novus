using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("fmt", HelpText = "Format Novus source code")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class FmtOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "File or directory to format (default: current directory)")]
    public string? Path { get; set; }

    [Option('c', "check", Required = false, HelpText = "Check if formatting is needed without modifying files")]
    public bool Check { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Verbose output")]
    public bool Verbose { get; set; }

    [Option("indent", Required = false, Default = 4, HelpText = "Number of spaces per indentation level (default: 4)")]
    public int IndentSize { get; set; } = 4;

    [Option("max-width", Required = false, Default = 100, HelpText = "Maximum line width (default: 100)")]
    public int MaxWidth { get; set; } = 100;
}
