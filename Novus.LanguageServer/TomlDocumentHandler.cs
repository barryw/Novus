using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Tomlyn;

namespace Novus.LanguageServer;

/// <summary>
/// Handles TOML text document synchronization (open, change, save, close) and publishes diagnostics.
/// Registers for *.toml file pattern and provides syntax validation.
/// </summary>
public class TomlDocumentHandler
{
    private readonly ProjectManager _projectManager;
    private readonly ILanguageServerFacade _languageServer;

    public TomlDocumentHandler(ProjectManager projectManager, ILanguageServerFacade languageServer)
    {
        _projectManager = projectManager;
        _languageServer = languageServer;
    }

    public void Open(string uri, string text, int version)
    {
        Console.Error.WriteLine($"[LSP] TOML document opened: {uri}");

        if (IsNovusConfigToml(uri))
        {
            _projectManager.RegisterProject(uri, text, version);
        }

        // Publish diagnostics for syntax errors
        PublishTomlDiagnostics(uri, text);

    }

    public void Update(string uri, string text, int version)
    {
        if (IsNovusConfigToml(uri))
        {
            _projectManager.UpdateProject(uri, text, version);
        }

        PublishTomlDiagnostics(uri, text);
    }

    public void Save(string uri, string text)
    {
        PublishTomlDiagnostics(uri, text);
    }

    public void Close(string uri)
    {
        if (IsNovusConfigToml(uri))
        {
            _projectManager.UnregisterProject(uri);
        }

        // Clear diagnostics
        _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.From(uri),
            Diagnostics = new Container<Diagnostic>()
        });
    }

    internal static bool HandlesDocument(string uri) =>
        Path.GetExtension(new Uri(uri).LocalPath).Equals(".toml", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses TOML content and publishes diagnostics for syntax errors.
    /// </summary>
    private void PublishTomlDiagnostics(string uri, string text)
    {
        var diagnostics = new List<Diagnostic>();

        try
        {
            // Try to parse the TOML
            var tomlTable = Toml.ToModel(text);
            Console.Error.WriteLine($"[LSP] TOML parsed successfully: {uri}");

            // Validate schema for project.toml and workspace.toml files
            var filePath = UriToFilePath(uri);
            var fileName = System.IO.Path.GetFileName(filePath).ToLowerInvariant();
            if (fileName == "project.toml" || fileName == "novus.toml")
            {
                var schemaDiagnostics = TomlSchemaValidator.ValidateProjectToml(tomlTable, text, filePath);
                diagnostics.AddRange(schemaDiagnostics);
            }
            else if (fileName == "workspace.toml")
            {
                var schemaDiagnostics = TomlSchemaValidator.ValidateWorkspaceToml(tomlTable, text, filePath);
                diagnostics.AddRange(schemaDiagnostics);
            }
        }
        catch (Exception ex)
        {
            // Parse error - extract line information if available
            var errorMessage = ex.Message;
            int line = 0;
            int column = 0;

            // Try to extract line/column from error message
            // Tomlyn error messages often include line numbers
            var lineMatch = System.Text.RegularExpressions.Regex.Match(errorMessage, @"line\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out var lineNum))
            {
                line = lineNum - 1; // Convert to 0-based
            }

            var colMatch = System.Text.RegularExpressions.Regex.Match(errorMessage, @"column\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (colMatch.Success && int.TryParse(colMatch.Groups[1].Value, out var colNum))
            {
                column = colNum - 1; // Convert to 0-based
            }

            // Create a diagnostic for the syntax error
            diagnostics.Add(new Diagnostic
            {
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(line, column),
                    new Position(line, column + 1)
                ),
                Severity = DiagnosticSeverity.Error,
                Source = "toml",
                Message = errorMessage
            });

            Console.Error.WriteLine($"[LSP] TOML parse error: {errorMessage}");
        }

        _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.From(uri),
            Diagnostics = new Container<Diagnostic>(diagnostics)
        });
    }

    /// <summary>
    /// Checks if the URI represents a project.toml or novus.toml file.
    /// </summary>
    private static bool IsNovusConfigToml(string uri)
    {
        var fileName = System.IO.Path.GetFileName(UriToFilePath(uri)).ToLowerInvariant();
        return fileName is "project.toml" or "novus.toml" or "workspace.toml";
    }

    /// <summary>
    /// Converts a URI to a file path.
    /// </summary>
    private static string UriToFilePath(string uri)
    {
        if (uri.StartsWith("file://"))
        {
            return Uri.UnescapeDataString(uri.Substring("file://".Length));
        }
        return uri;
    }
}
