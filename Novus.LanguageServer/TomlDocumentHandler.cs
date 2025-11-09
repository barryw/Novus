using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Tomlyn;

namespace Novus.LanguageServer;

/// <summary>
/// Handles TOML text document synchronization (open, change, save, close) and publishes diagnostics.
/// Registers for *.toml file pattern and provides syntax validation.
/// </summary>
public class TomlDocumentHandler : TextDocumentSyncHandlerBase
{
    private readonly ProjectManager _projectManager;
    private readonly ILanguageServerFacade _languageServer;

    public TomlDocumentHandler(ProjectManager projectManager, ILanguageServerFacade languageServer)
    {
        _projectManager = projectManager;
        _languageServer = languageServer;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "toml");
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter
                {
                    Pattern = "**/*.toml"
                }
            ),
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions { IncludeText = true }
        };
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var text = request.TextDocument.Text;
        var version = request.TextDocument.Version ?? 0;

        Console.Error.WriteLine($"[LSP] TOML document opened: {uri}");

        // Register as a project if it's a project.toml file
        if (IsProjectToml(uri))
        {
            _projectManager.RegisterProject(uri, text, version);
        }

        // Publish diagnostics for syntax errors
        PublishTomlDiagnostics(uri, text);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var version = request.TextDocument.Version ?? 0;

        if (request.ContentChanges.Count() > 0)
        {
            var text = request.ContentChanges.First().Text;

            // Update project if it's a project.toml
            if (IsProjectToml(uri))
            {
                _projectManager.UpdateProject(uri, text, version);
            }

            // Re-publish diagnostics
            PublishTomlDiagnostics(uri, text);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();

        if (request.Text != null)
        {
            // Re-validate on save
            PublishTomlDiagnostics(uri, request.Text);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();

        // Unregister project if it's a project.toml
        if (IsProjectToml(uri))
        {
            _projectManager.UnregisterProject(uri);
        }

        // Clear diagnostics
        _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Task;
    }

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

            // TODO: Add schema validation for project.toml files
            // Could validate required keys, type constraints, etc.
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
    private static bool IsProjectToml(string uri)
    {
        var fileName = System.IO.Path.GetFileName(UriToFilePath(uri)).ToLowerInvariant();
        return fileName == "project.toml" || fileName == "novus.toml";
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
