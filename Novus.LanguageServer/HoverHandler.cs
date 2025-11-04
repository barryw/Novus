using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Novus.LanguageServer;

/// <summary>
/// Handles "Hover" requests from the LSP client.
/// When the user hovers over a symbol, this handler shows documentation and type information.
/// </summary>
public class HoverHandler : IHoverHandler
{
    private readonly DocumentManager _documentManager;

    public HoverHandler(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    public Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var position = request.Position;

        Console.Error.WriteLine($"[LSP] Hover request for {uri} at {position.Line}:{position.Character}");

        // Get the document state
        var state = _documentManager.Get(uri);
        if (state?.SemanticAnalyzer == null || state.ParseTree == null)
        {
            Console.Error.WriteLine("[LSP] No semantic analyzer or parse tree available");
            return Task.FromResult<Hover?>(null);
        }

        try
        {
            // Create symbol resolver
            var resolver = new SymbolResolver(state.SemanticAnalyzer, state.ParseTree, state.Text);
            var analyzer = state.SemanticAnalyzer;

            // Find symbol at cursor position (LSP uses 0-based line/column)
            var symbolInfo = resolver.FindSymbolAtPosition((int)position.Line, (int)position.Character);

            if (symbolInfo == null)
            {
                Console.Error.WriteLine("[LSP] No symbol found at position");
                return Task.FromResult<Hover?>(null);
            }

            Console.Error.WriteLine($"[LSP] Found {symbolInfo.Kind} symbol: {symbolInfo.Name}");

            // Build hover content based on symbol kind
            var content = BuildHoverContent(symbolInfo, analyzer);

            var hover = new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = content
                }),
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

            return Task.FromResult<Hover?>(hover);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error resolving hover: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return Task.FromResult<Hover?>(null);
        }
    }

    private string BuildHoverContent(SymbolInfo symbolInfo, Novus.SemanticAnalysis.SemanticAnalyzer analyzer)
    {
        var content = new System.Text.StringBuilder();

        // Add symbol signature/declaration
        content.AppendLine("```novus");
        switch (symbolInfo.Kind)
        {
            case SymbolKind.Function:
                var funcSymbol = (Novus.SemanticAnalysis.FunctionSymbol)symbolInfo.Symbol;
                content.Append("fn ");
                content.Append(funcSymbol.Name);
                content.Append("(");
                content.Append(string.Join(", ", funcSymbol.Parameters.Select(p =>
                    $"{p.Name}: {p.Type.Name}")));
                content.Append(")");
                if (funcSymbol.ReturnType.Name != "void")
                {
                    content.Append(" -> ");
                    content.Append(funcSymbol.ReturnType.Name);
                }
                if (funcSymbol.IsExtern)
                {
                    content.Append("  // extern");
                }
                break;

            case SymbolKind.Variable:
            case SymbolKind.GlobalVariable:
                var varSymbol = (Novus.SemanticAnalysis.VariableSymbol)symbolInfo.Symbol;
                content.Append(varSymbol.IsMutable ? "var " : "let ");
                content.Append(varSymbol.Name);
                content.Append(": ");
                content.Append(varSymbol.Type.Name);
                break;

            case SymbolKind.Constant:
                var constSymbol = (Novus.SemanticAnalysis.ConstantSymbol)symbolInfo.Symbol;
                content.Append("const ");
                content.Append(constSymbol.Name);
                content.Append(": ");
                content.Append(constSymbol.Type.Name);
                content.Append(" = ");
                content.Append(constSymbol.Value);
                break;

            case SymbolKind.Struct:
                var structType = (Novus.IR.IrStructType)symbolInfo.Symbol;
                content.Append("struct ");
                content.Append(structType.StructName);
                if (structType.GenericParameters.Count > 0)
                {
                    content.Append("<");
                    content.Append(string.Join(", ", structType.GenericParameters));
                    content.Append(">");
                }
                break;

            case SymbolKind.Enum:
                var enumType = (Novus.IR.IrEnumType)symbolInfo.Symbol;
                content.Append("enum ");
                content.Append(enumType.EnumName);
                if (enumType.GenericParameters.Count > 0)
                {
                    content.Append("<");
                    content.Append(string.Join(", ", enumType.GenericParameters));
                    content.Append(">");
                }
                break;

            case SymbolKind.Trait:
                var trait = (Novus.IR.IrTrait)symbolInfo.Symbol;
                content.Append("trait ");
                content.Append(trait.TraitName);
                if (trait.GenericParameters.Count > 0)
                {
                    content.Append("<");
                    content.Append(string.Join(", ", trait.GenericParameters));
                    content.Append(">");
                }
                break;

            default:
                content.Append(symbolInfo.Name);
                if (symbolInfo.Type != null)
                {
                    content.Append(": ");
                    content.Append(symbolInfo.Type.Name);
                }
                break;
        }
        content.AppendLine();
        content.AppendLine("```");

        // Add location information
        content.AppendLine();
        var locationPath = symbolInfo.Location.FilePath;
        // Try to make path relative to workspace for cleaner display
        var displayPath = System.IO.Path.GetFileName(locationPath);
        content.AppendLine($"*Defined in {displayPath}:{symbolInfo.Location.Line}*");

        // TODO: Add documentation comments when we parse them
        // For now, we could add some basic info based on symbol kind
        switch (symbolInfo.Kind)
        {
            case SymbolKind.Function:
                var func = (Novus.SemanticAnalysis.FunctionSymbol)symbolInfo.Symbol;
                if (func.IsExtern)
                {
                    content.AppendLine();
                    content.AppendLine("External function declaration (defined in C/FFI)");
                }
                break;

            case SymbolKind.Struct:
                var st = (Novus.IR.IrStructType)symbolInfo.Symbol;
                if (st.Fields.Count > 0)
                {
                    content.AppendLine();
                    content.AppendLine($"**Fields:** {st.Fields.Count}");
                }
                break;

            case SymbolKind.Enum:
                var en = (Novus.IR.IrEnumType)symbolInfo.Symbol;
                if (en.Variants.Count > 0)
                {
                    content.AppendLine();
                    content.AppendLine($"**Variants:** {en.Variants.Count}");
                }
                break;
        }

        // Add documentation comment if available
        var docComment = DocCommentExtractor.ExtractDocComment(analyzer.SourceText, symbolInfo.Location.Line);
        if (!string.IsNullOrWhiteSpace(docComment))
        {
            content.AppendLine();
            content.AppendLine("---");
            content.AppendLine();
            content.AppendLine(docComment);
        }

        return content.ToString();
    }

    public HoverRegistrationOptions GetRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            // DocumentSelector will be null, which means it applies to all documents
            // This is fine for now - we'll filter .novus files in the handler
        };
    }
}
