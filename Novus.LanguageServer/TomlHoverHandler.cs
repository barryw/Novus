using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Novus.LanguageServer;

/// <summary>
/// Provides hover information for TOML keys in Novus project.toml files.
/// Shows descriptions, value types, and examples for schema keys.
/// </summary>
public class TomlHoverHandler : IHoverHandler
{
    private readonly ProjectManager _projectManager;

    // Schema documentation for project.toml keys
    private static readonly Dictionary<string, TomlKeyInfo> KeyDocumentation = new()
    {
        // [package] section
        ["package.name"] = new TomlKeyInfo(
            "Package Name",
            "The name of your Novus package. Used as the output binary name.",
            "string",
            "my_project"
        ),
        ["package.version"] = new TomlKeyInfo(
            "Package Version",
            "Semantic version of your package (e.g., 1.0.0, 0.2.1).",
            "string",
            "0.1.0"
        ),
        ["package.type"] = new TomlKeyInfo(
            "Package Type",
            "The type of executable or library to build.",
            "string",
            "cli",
            new[] { "cli", "workbench", "dual", "library", "device", "handler", "resource" }
        ),
        ["package.description"] = new TomlKeyInfo(
            "Package Description",
            "A brief description of your package.",
            "string (optional)",
            "My awesome Amiga application"
        ),
        ["package.entry"] = new TomlKeyInfo(
            "Entry Point",
            "Entry point file. Defaults to main.novus or <name>.novus.",
            "string (optional)",
            "main.novus"
        ),
        ["package.authors"] = new TomlKeyInfo(
            "Authors",
            "List of package authors.",
            "array of strings (optional)",
            "[\"Your Name <you@example.com>\"]"
        ),
        ["package.license"] = new TomlKeyInfo(
            "License",
            "Package license identifier (e.g., MIT, Apache-2.0, GPL-3.0).",
            "string (optional)",
            "MIT"
        ),

        // [build] section
        ["build.target_cpu"] = new TomlKeyInfo(
            "Target CPU",
            "Target Motorola 68k CPU architecture.",
            "string",
            "68020",
            new[] { "68000", "68020", "68040", "68060", "auto" }
        ),
        ["build.fpu"] = new TomlKeyInfo(
            "Floating Point Unit",
            "FPU configuration for floating-point operations.",
            "string",
            "auto",
            new[] { "soft", "68881", "68040", "auto" }
        ),
        ["build.output"] = new TomlKeyInfo(
            "Output Directory",
            "Directory where compiled binaries will be placed.",
            "string",
            "build"
        ),
        ["build.optimization_level"] = new TomlKeyInfo(
            "Optimization Level",
            "Compiler optimization level (0-3). Default is 2.",
            "integer",
            "2"
        ),
        ["build.emit_asm"] = new TomlKeyInfo(
            "Emit Assembly",
            "Whether to emit .s assembly files alongside binaries.",
            "boolean",
            "false"
        ),
        ["build.asm_files"] = new TomlKeyInfo(
            "Assembly Files",
            "External assembly files to link with the project.",
            "array of strings (optional)",
            "[\"startup.s\", \"custom.s\"]"
        ),

        // [paths] section
        ["paths.src"] = new TomlKeyInfo(
            "Source Directory",
            "Directory containing Novus source files.",
            "string",
            "."
        ),
        ["paths.lib"] = new TomlKeyInfo(
            "Library Paths",
            "Additional library search paths for dependencies.",
            "array of strings (optional)",
            "[\"lib\", \"vendor/libs\"]"
        ),

        // Section headers
        ["[package]"] = new TomlKeyInfo(
            "Package Section",
            "Package metadata and configuration.",
            "section",
            null
        ),
        ["[build]"] = new TomlKeyInfo(
            "Build Section",
            "Build configuration and compiler settings.",
            "section",
            null
        ),
        ["[paths]"] = new TomlKeyInfo(
            "Paths Section",
            "Source and library path configuration.",
            "section",
            null
        ),
        ["[dependencies]"] = new TomlKeyInfo(
            "Dependencies Section",
            "External package dependencies.",
            "section",
            null
        ),

        // Dependency keys (generic for any dependency)
        ["dependencies.*.version"] = new TomlKeyInfo(
            "Dependency Version",
            "Version constraint for the dependency (e.g., \"1.0.0\", \"^0.2\").",
            "string (optional)",
            "1.0.0"
        ),
        ["dependencies.*.path"] = new TomlKeyInfo(
            "Local Path Dependency",
            "Path to a local dependency on disk.",
            "string (optional)",
            "../my-library"
        ),
        ["dependencies.*.git"] = new TomlKeyInfo(
            "Git Repository",
            "Git repository URL for the dependency.",
            "string (optional)",
            "https://github.com/user/repo.git"
        ),
        ["dependencies.*.branch"] = new TomlKeyInfo(
            "Git Branch",
            "Git branch to use (requires git field).",
            "string (optional)",
            "main"
        ),
        ["dependencies.*.tag"] = new TomlKeyInfo(
            "Git Tag",
            "Git tag to use (requires git field).",
            "string (optional)",
            "v1.0.0"
        ),
    };

    public TomlHoverHandler(ProjectManager projectManager)
    {
        _projectManager = projectManager;
    }

    public Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var position = request.Position;

        Console.Error.WriteLine($"[LSP] TOML hover request for {uri} at {position.Line}:{position.Character}");

        // Get the project state
        var project = _projectManager.GetProject(uri);
        if (project == null)
        {
            Console.Error.WriteLine("[LSP] No TOML project state found");
            return Task.FromResult<Hover?>(null);
        }

        try
        {
            // Get the line at the cursor
            var lines = project.Text.Split('\n');
            if (position.Line >= lines.Length)
            {
                return Task.FromResult<Hover?>(null);
            }

            var line = lines[(int)position.Line];
            var keyPath = ExtractKeyPath(line, lines, (int)position.Line);

            if (keyPath == null)
            {
                Console.Error.WriteLine("[LSP] Could not extract key path");
                return Task.FromResult<Hover?>(null);
            }

            Console.Error.WriteLine($"[LSP] Extracted key path: {keyPath}");

            // Look up documentation for this key
            var keyInfo = FindKeyDocumentation(keyPath);
            if (keyInfo == null)
            {
                Console.Error.WriteLine($"[LSP] No documentation found for key: {keyPath}");
                return Task.FromResult<Hover?>(null);
            }

            // Build hover content
            var content = BuildHoverContent(keyInfo);

            var hover = new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = content
                })
            };

            return Task.FromResult<Hover?>(hover);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error resolving TOML hover: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return Task.FromResult<Hover?>(null);
        }
    }

    /// <summary>
    /// Extracts the TOML key path from the current line and context.
    /// </summary>
    private static string? ExtractKeyPath(string line, string[] lines, int lineIndex)
    {
        // Check if we're on a section header
        var sectionMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\s*\[([^\]]+)\]\s*$");
        if (sectionMatch.Success)
        {
            return $"[{sectionMatch.Groups[1].Value}]";
        }

        // Check if we're on a key = value line
        var keyMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*=");
        if (keyMatch.Success)
        {
            var key = keyMatch.Groups[1].Value;

            // Find the current section
            var section = FindCurrentSection(lines, lineIndex);
            if (section != null)
            {
                return $"{section}.{key}";
            }

            return key;
        }

        return null;
    }

    /// <summary>
    /// Finds the current TOML section by looking backwards from the current line.
    /// </summary>
    private static string? FindCurrentSection(string[] lines, int currentLine)
    {
        for (int i = currentLine - 1; i >= 0; i--)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"^\s*\[([^\]]+)\]\s*$");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds documentation for a given key path, supporting wildcards.
    /// </summary>
    private static TomlKeyInfo? FindKeyDocumentation(string keyPath)
    {
        // Try exact match first
        if (KeyDocumentation.TryGetValue(keyPath, out var info))
        {
            return info;
        }

        // Try wildcard match for dependencies
        if (keyPath.StartsWith("dependencies."))
        {
            var parts = keyPath.Split('.');
            if (parts.Length == 3)
            {
                // dependencies.<name>.<field>
                var wildcardKey = $"dependencies.*.{parts[2]}";
                if (KeyDocumentation.TryGetValue(wildcardKey, out var wildcardInfo))
                {
                    return wildcardInfo;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Builds markdown hover content from key information.
    /// </summary>
    private static string BuildHoverContent(TomlKeyInfo keyInfo)
    {
        var content = new System.Text.StringBuilder();

        // Title
        content.AppendLine($"**{keyInfo.Title}**");
        content.AppendLine();

        // Description
        content.AppendLine(keyInfo.Description);
        content.AppendLine();

        // Type
        content.AppendLine($"*Type:* `{keyInfo.Type}`");

        // Valid values (if any)
        if (keyInfo.ValidValues != null && keyInfo.ValidValues.Length > 0)
        {
            content.AppendLine();
            content.AppendLine($"*Valid values:* `{string.Join("`, `", keyInfo.ValidValues)}`");
        }

        // Example (if available)
        if (keyInfo.Example != null)
        {
            content.AppendLine();
            content.AppendLine("*Example:*");
            content.AppendLine("```toml");
            content.AppendLine(keyInfo.Example);
            content.AppendLine("```");
        }

        return content.ToString();
    }

    public HoverRegistrationOptions GetRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter
                {
                    Pattern = "**/*.toml"
                }
            )
        };
    }

    /// <summary>
    /// Information about a TOML key in the project schema.
    /// </summary>
    private class TomlKeyInfo
    {
        public string Title { get; }
        public string Description { get; }
        public string Type { get; }
        public string? Example { get; }
        public string[]? ValidValues { get; }

        public TomlKeyInfo(string title, string description, string type, string? example, string[]? validValues = null)
        {
            Title = title;
            Description = description;
            Type = type;
            Example = example;
            ValidValues = validValues;
        }
    }
}
