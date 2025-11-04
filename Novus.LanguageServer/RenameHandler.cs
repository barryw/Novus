using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Antlr4.Runtime.Tree;
using Novus.Parser;

namespace Novus.LanguageServer;

/// <summary>
/// Handles "Rename Symbol" requests from the LSP client.
/// Allows refactoring by renaming a symbol across all its uses.
/// </summary>
public class RenameHandler : IRenameHandler
{
    private readonly DocumentManager _documentManager;

    public RenameHandler(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    public Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var position = request.Position;
        var newName = request.NewName;

        Console.Error.WriteLine($"[LSP] Rename request for {uri} at {position.Line}:{position.Character} to '{newName}'");

        var state = _documentManager.Get(uri);
        if (state?.SemanticAnalyzer == null || state.ParseTree == null)
        {
            Console.Error.WriteLine("[LSP] No semantic analyzer or parse tree available");
            return Task.FromResult<WorkspaceEdit?>(null);
        }

        try
        {
            // Find the symbol at the cursor
            var resolver = new SymbolResolver(state.SemanticAnalyzer, state.ParseTree, state.Text);
            var symbolInfo = resolver.FindSymbolAtPosition((int)position.Line, (int)position.Character);

            if (symbolInfo == null)
            {
                Console.Error.WriteLine("[LSP] No symbol found at position");
                return Task.FromResult<WorkspaceEdit?>(null);
            }

            Console.Error.WriteLine($"[LSP] Renaming {symbolInfo.Kind} symbol '{symbolInfo.Name}' to '{newName}'");

            // Find all occurrences of this symbol
            var textEdits = FindAllOccurrences(state.Text, state.ParseTree, symbolInfo.Name, newName);

            Console.Error.WriteLine($"[LSP] Found {textEdits.Count} occurrences to rename");

            // Create workspace edit
            var workspaceEdit = new WorkspaceEdit
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
                {
                    { DocumentUri.Parse(uri), textEdits }
                }
            };

            return Task.FromResult<WorkspaceEdit?>(workspaceEdit);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error performing rename: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return Task.FromResult<WorkspaceEdit?>(null);
        }
    }

    /// <summary>
    /// Finds all occurrences of a symbol and creates text edits to rename them
    /// </summary>
    private List<TextEdit> FindAllOccurrences(string sourceText, IParseTree tree, string oldName, string newName)
    {
        var textEdits = new List<TextEdit>();
        WalkTreeForOccurrences(tree, oldName, newName, textEdits);
        return textEdits;
    }

    /// <summary>
    /// Recursively walks the parse tree to find all identifiers matching the old name
    /// </summary>
    private void WalkTreeForOccurrences(IParseTree tree, string oldName, string newName, List<TextEdit> textEdits)
    {
        if (tree is Antlr4.Runtime.Tree.TerminalNodeImpl terminal)
        {
            var token = terminal.Symbol;

            // Check if this is an IDENTIFIER token matching our symbol name
            if (token.Type == NovusLexer.IDENTIFIER && token.Text == oldName)
            {
                var line = token.Line - 1;  // Convert to 0-based
                var column = token.Column;
                var length = token.Text.Length;

                // Create text edit to replace old name with new name
                var edit = new TextEdit
                {
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                    {
                        Start = new Position { Line = line, Character = column },
                        End = new Position { Line = line, Character = column + length }
                    },
                    NewText = newName
                };

                textEdits.Add(edit);
            }
        }

        // Recursively check all children
        for (int i = 0; i < tree.ChildCount; i++)
        {
            WalkTreeForOccurrences(tree.GetChild(i), oldName, newName, textEdits);
        }
    }

    public RenameRegistrationOptions GetRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
    {
        return new RenameRegistrationOptions
        {
            // DocumentSelector will be null, which means it applies to all documents
            PrepareProvider = false  // We don't need prepare support (pre-validation)
        };
    }
}
