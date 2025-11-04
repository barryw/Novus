using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Antlr4.Runtime.Tree;
using Novus.Parser;

namespace Novus.LanguageServer;

/// <summary>
/// Handles "Find All References" requests from the LSP client.
/// Shows all locations where a symbol is used in the code.
/// </summary>
public class ReferencesHandler : IReferencesHandler
{
    private readonly DocumentManager _documentManager;

    public ReferencesHandler(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    public Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var position = request.Position;

        Console.Error.WriteLine($"[LSP] Find references request for {uri} at {position.Line}:{position.Character}");

        var state = _documentManager.Get(uri);
        if (state?.SemanticAnalyzer == null || state.ParseTree == null)
        {
            Console.Error.WriteLine("[LSP] No semantic analyzer or parse tree available");
            return Task.FromResult<LocationContainer?>(null);
        }

        try
        {
            // Find the symbol at the cursor
            var resolver = new SymbolResolver(state.SemanticAnalyzer, state.ParseTree, state.Text);
            var symbolInfo = resolver.FindSymbolAtPosition((int)position.Line, (int)position.Character);

            if (symbolInfo == null)
            {
                Console.Error.WriteLine("[LSP] No symbol found at position");
                return Task.FromResult<LocationContainer?>(null);
            }

            Console.Error.WriteLine($"[LSP] Finding references to {symbolInfo.Kind} symbol: {symbolInfo.Name}");

            // Find all references to this symbol
            var locations = FindAllReferences(state.Text, state.ParseTree, symbolInfo.Name, uri);

            // Optionally include the declaration itself
            if (request.Context.IncludeDeclaration)
            {
                var declLocation = new Location
                {
                    Uri = new Uri(symbolInfo.Location.FilePath),
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    {
                        Start = new Position
                        {
                            Line = symbolInfo.Location.Line - 1,
                            Character = symbolInfo.Location.Column - 1
                        },
                        End = new Position
                        {
                            Line = symbolInfo.Location.Line - 1,
                            Character = symbolInfo.Location.Column - 1 + symbolInfo.Location.Length
                        }
                    }
                };
                locations.Insert(0, declLocation);
            }

            Console.Error.WriteLine($"[LSP] Found {locations.Count} references");

            return Task.FromResult<LocationContainer?>(new LocationContainer(locations));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error finding references: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return Task.FromResult<LocationContainer?>(null);
        }
    }

    /// <summary>
    /// Finds all references to a symbol by walking the parse tree
    /// </summary>
    private List<Location> FindAllReferences(string sourceText, IParseTree tree, string symbolName, string uri)
    {
        var locations = new List<Location>();
        var lines = sourceText.Split('\n');

        WalkTreeForReferences(tree, symbolName, locations, uri, lines);

        return locations;
    }

    /// <summary>
    /// Recursively walks the parse tree to find identifier tokens matching the symbol name
    /// </summary>
    private void WalkTreeForReferences(IParseTree tree, string symbolName, List<Location> locations, string uri, string[] lines)
    {
        if (tree is Antlr4.Runtime.Tree.TerminalNodeImpl terminal)
        {
            var token = terminal.Symbol;

            // Check if this is an IDENTIFIER token matching our symbol name
            if (token.Type == NovusLexer.IDENTIFIER && token.Text == symbolName)
            {
                var line = token.Line - 1;  // Convert to 0-based
                var column = token.Column;
                var length = token.Text.Length;

                // Create location
                var location = new Location
                {
                    Uri = new Uri(uri),
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    {
                        Start = new Position { Line = line, Character = column },
                        End = new Position { Line = line, Character = column + length }
                    }
                };

                locations.Add(location);
            }
        }

        // Recursively check all children
        for (int i = 0; i < tree.ChildCount; i++)
        {
            WalkTreeForReferences(tree.GetChild(i), symbolName, locations, uri, lines);
        }
    }

    public ReferenceRegistrationOptions GetRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities)
    {
        return new ReferenceRegistrationOptions
        {
            // DocumentSelector will be null, which means it applies to all documents
        };
    }
}
