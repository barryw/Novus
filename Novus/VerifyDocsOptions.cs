using CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Novus;

[Verb("verify-docs", HelpText = "Verify code snippets in documentation files")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class VerifyDocsOptions
{
    [Value(0, MetaName = "path", Required = false, HelpText = "Directory containing markdown/mdx files (default: current directory)")]
    public string? Path { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Show detailed output for each snippet")]
    public bool Verbose { get; set; }

    [Option("fix", Required = false, HelpText = "Attempt to suggest fixes for failing snippets")]
    public bool SuggestFixes { get; set; }

    [Option("parse-only", Required = false, HelpText = "Only check syntax (skip type checking)")]
    public bool ParseOnly { get; set; }

    [Option("include-incomplete", Required = false, HelpText = "Include snippets marked as incomplete/illustrative")]
    public bool IncludeIncomplete { get; set; }

    [Option('r', "recursive", Required = false, Default = true, HelpText = "Recursively search directories")]
    public bool Recursive { get; set; } = true;

    [Option("json", Required = false, HelpText = "Output results as JSON")]
    public bool JsonOutput { get; set; }
}
