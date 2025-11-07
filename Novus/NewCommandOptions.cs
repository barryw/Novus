using CommandLine;

namespace Novus;

[Verb("new", HelpText = "Create a new Novus project from a template")]
public class NewCommandOptions
{
    [Value(0, Required = true, MetaName = "name", HelpText = "Project name or '.' for current directory")]
    public string ProjectName { get; set; } = "";

    [Option('t', "type", Required = false, Default = null,
        HelpText = "Project type: cli, workbench, dual, library, device")]
    public string? ProjectType { get; set; } = null;

    [Option('a', "author", Required = false, HelpText = "Author name")]
    public string? Author { get; set; }

    [Option('l', "license", Required = false, HelpText = "License (e.g., MIT, Apache-2.0, GPL-3.0)")]
    public string? License { get; set; }

    [Option('d', "description", Required = false, HelpText = "Project description")]
    public string? Description { get; set; }
}
