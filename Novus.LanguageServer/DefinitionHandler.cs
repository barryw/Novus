using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Novus.LanguageServer;

/// <summary>
/// Handles "Go to Definition" requests from the LSP client.
/// When the user Ctrl+clicks or F12s on a symbol, this handler finds the symbol's definition location.
/// </summary>
public class DefinitionHandler : IDefinitionHandler
{
    private readonly DocumentManager _documentManager;

    public DefinitionHandler(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    public Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var position = request.Position;

        Console.Error.WriteLine($"[LSP] Definition request for {uri} at {position.Line}:{position.Character}");

        // Get the document state
        var state = _documentManager.Get(uri);
        if (state?.SemanticAnalyzer == null || state.ParseTree == null)
        {
            Console.Error.WriteLine("[LSP] No semantic analyzer or parse tree available");
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        try
        {
            // Create symbol resolver
            var resolver = new SymbolResolver(state.SemanticAnalyzer, state.ParseTree, state.Text);

            // Find symbol at cursor position (LSP uses 0-based line/column)
            var symbolInfo = resolver.FindSymbolAtPosition((int)position.Line, (int)position.Character);

            if (symbolInfo == null)
            {
                Console.Error.WriteLine("[LSP] No symbol found at position");
                return Task.FromResult<LocationOrLocationLinks?>(null);
            }

            Console.Error.WriteLine($"[LSP] Found {symbolInfo.Kind} symbol: {symbolInfo.Name}");

            // Convert SourceLocation to LSP Location
            var location = new Location
            {
                Uri = new Uri(symbolInfo.Location.FilePath),
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new Position
                    {
                        Line = symbolInfo.Location.Line - 1,  // LSP uses 0-based lines
                        Character = symbolInfo.Location.Column - 1  // LSP uses 0-based columns
                    },
                    End = new Position
                    {
                        Line = symbolInfo.Location.Line - 1,
                        Character = symbolInfo.Location.Column - 1 + symbolInfo.Location.Length
                    }
                }
            };

            Console.Error.WriteLine($"[LSP] Returning definition at {location.Uri}:{location.Range.Start.Line}:{location.Range.Start.Character}");

            return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(location));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error resolving definition: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }
    }

    public DefinitionRegistrationOptions GetRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DefinitionRegistrationOptions
        {
            // DocumentSelector will be null, which means it applies to all documents
            // This is fine for now - we'll filter .novus files in the handler
        };
    }
}
