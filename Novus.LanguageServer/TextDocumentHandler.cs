using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Novus.LanguageServer;

/// <summary>
/// Handles text document synchronization (open, change, save) and publishes diagnostics
/// </summary>
public class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    private readonly DocumentManager _documentManager;
    private readonly ILanguageServerFacade _languageServer;

    public TextDocumentHandler(DocumentManager documentManager, ILanguageServerFacade languageServer)
    {
        _documentManager = documentManager;
        _languageServer = languageServer;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "novus");
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter
                {
                    Pattern = "**/*.novus"
                }
            ),
            Change = TextDocumentSyncKind.Full, // Send full document on every change
            Save = new SaveOptions { IncludeText = true }
        };
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var text = request.TextDocument.Text;
        var version = request.TextDocument.Version ?? 0;

        Console.Error.WriteLine($"[LSP] Document opened: {uri}");
        Console.Error.WriteLine($"[LSP] Text length: {text.Length}");

        _documentManager.Open(uri, text, version);
        PublishDiagnostics(uri);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var version = request.TextDocument.Version ?? 0;

        // Full document sync - get the text from the change
        if (request.ContentChanges.Count() > 0)
        {
            var text = request.ContentChanges.First().Text;
            _documentManager.Update(uri, text, version);
            PublishDiagnostics(uri);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        // Re-parse on save to ensure diagnostics are fresh
        var uri = request.TextDocument.Uri.ToString();
        if (request.Text != null && _documentManager.Get(uri) is DocumentState state)
        {
            _documentManager.Update(uri, request.Text, state.Version);
            PublishDiagnostics(uri);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        _documentManager.Close(uri);

        // Clear diagnostics for closed document
        _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Task;
    }

    private void PublishDiagnostics(string uri)
    {
        var state = _documentManager.Get(uri);
        if (state?.Diagnostics == null)
        {
            Console.Error.WriteLine($"[LSP] No diagnostics for {uri}");
            return;
        }

        Console.Error.WriteLine($"[LSP] Publishing {state.Diagnostics.Diagnostics.Count} diagnostics for {uri}");

        var diagnostics = new List<Diagnostic>();

        foreach (var diagnostic in state.Diagnostics.Diagnostics)
        {
            // Convert from 1-based to 0-based, but ensure we never go negative
            int line = Math.Max(0, diagnostic.Location.Line - 1);
            int col = Math.Max(0, diagnostic.Location.Column - 1);
            int length = Math.Max(1, diagnostic.Location.Length); // Ensure at least 1 character

            var range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(line, col),
                new Position(line, col + length)
            );

            Console.Error.WriteLine($"[LSP]   - {diagnostic.Message}");
            Console.Error.WriteLine($"[LSP]     Location: Line {diagnostic.Location.Line}, Col {diagnostic.Location.Column}, Len {diagnostic.Location.Length}");
            Console.Error.WriteLine($"[LSP]     Range: ({range.Start.Line},{range.Start.Character}) to ({range.End.Line},{range.End.Character})");
            Console.Error.WriteLine($"[LSP]     Severity: {(diagnostic.IsError ? "Error" : "Warning")}");

            diagnostics.Add(new Diagnostic
            {
                Range = range,
                Severity = diagnostic.IsError ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                Code = diagnostic.Code,
                Source = "novus",
                Message = diagnostic.Message
            });
        }

        // Add inactive code regions as "hint" diagnostics with the "unnecessary" tag
        // This causes VS Code and other LSP clients to gray out the code
        if (state.InactiveRegions != null && state.InactiveRegions.Count > 0)
        {
            Console.Error.WriteLine($"[LSP] Adding {state.InactiveRegions.Count} inactive region(s) as diagnostics");

            foreach (var region in state.InactiveRegions)
            {
                // For each line in the inactive region, add a diagnostic
                // We need to handle this line-by-line because the editor needs to know
                // the full extent of the grayed-out text on each line
                for (int lineNum = region.StartLine; lineNum <= region.EndLine; lineNum++)
                {
                    // Get the line text to determine the end column
                    var lineText = GetLineText(state.Text, lineNum);
                    if (string.IsNullOrEmpty(lineText))
                        continue;

                    // Convert from 1-based to 0-based
                    int line = lineNum - 1;

                    var range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                        new Position(line, 0),
                        new Position(line, lineText.TrimEnd().Length)
                    );

                    // The "Unnecessary" tag causes the text to be rendered with reduced opacity (grayed out)
                    diagnostics.Add(new Diagnostic
                    {
                        Range = range,
                        Severity = DiagnosticSeverity.Hint,
                        Code = "inactive-code",
                        Source = "novus-preprocessor",
                        Message = $"Code is inactive (condition: {region.Reason ?? "false"})",
                        Tags = new Container<DiagnosticTag>(DiagnosticTag.Unnecessary)
                    });
                }
            }
        }

        Console.Error.WriteLine($"[LSP] Calling PublishDiagnostics with URI: {uri}");
        _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.From(uri),
            Diagnostics = new Container<Diagnostic>(diagnostics)
        });
        Console.Error.WriteLine($"[LSP] PublishDiagnostics call completed");
    }

    /// <summary>
    /// Gets the text of a specific line (1-based line number).
    /// </summary>
    private string GetLineText(string text, int lineNumber)
    {
        var lines = text.Split('\n');
        if (lineNumber < 1 || lineNumber > lines.Length)
            return "";
        return lines[lineNumber - 1];
    }
}
