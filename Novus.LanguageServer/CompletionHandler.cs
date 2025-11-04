using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Novus.LanguageServer;

/// <summary>
/// Handles "Completion" requests from the LSP client.
/// Provides auto-completion suggestions as the user types.
/// </summary>
public class CompletionHandler : ICompletionHandler
{
    private readonly DocumentManager _documentManager;

    // Novus keywords
    private static readonly string[] Keywords = new[]
    {
        "fn", "let", "var", "const", "if", "else", "while", "for", "in", "return",
        "break", "continue", "struct", "enum", "impl", "trait", "pub", "from",
        "import", "as", "match", "unsafe", "defer", "extern", "true", "false",
        "mut", "static", "where", "type"
    };

    // Built-in types
    private static readonly string[] BuiltInTypes = new[]
    {
        "i8", "i16", "i32", "i64",
        "u8", "u16", "u32", "u64",
        "f32", "f64",
        "bool", "void", "str"
    };

    public CompletionHandler(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var position = request.Position;

        Console.Error.WriteLine($"[LSP] Completion request for {uri} at {position.Line}:{position.Character}");

        var completions = new List<CompletionItem>();

        // Get the document state
        var state = _documentManager.Get(uri);
        if (state?.SemanticAnalyzer == null)
        {
            Console.Error.WriteLine("[LSP] No semantic analyzer available");
            // Still provide keywords even without semantic analysis
            AddKeywordCompletions(completions);
            return Task.FromResult(new CompletionList(completions));
        }

        try
        {
            // Get the current line and partial token being typed
            var lines = state.Text.Split('\n');
            var line = lines[(int)position.Line];
            var partialToken = GetPartialToken(line, (int)position.Character);

            Console.Error.WriteLine($"[LSP] Partial token: '{partialToken}'");

            // Add completions for symbols from semantic analyzer
            AddSymbolCompletions(state.SemanticAnalyzer, completions, partialToken);

            // Add keyword completions
            AddKeywordCompletions(completions, partialToken);

            // Add built-in type completions
            AddBuiltInTypeCompletions(completions, partialToken);

            Console.Error.WriteLine($"[LSP] Returning {completions.Count} completions");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error generating completions: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }

        return Task.FromResult(new CompletionList(completions));
    }

    private string GetPartialToken(string line, int column)
    {
        if (column > line.Length)
        {
            column = line.Length;
        }

        // Find the start of the token (move backwards while alphanumeric or _)
        int start = column - 1;
        while (start >= 0 && (char.IsLetterOrDigit(line[start]) || line[start] == '_'))
        {
            start--;
        }
        start++;  // Move back to the first character of the token

        return line.Substring(start, column - start);
    }

    private void AddSymbolCompletions(Novus.SemanticAnalysis.SemanticAnalyzer analyzer,
                                     List<CompletionItem> completions,
                                     string? filter = null)
    {
        // Add functions
        foreach (var (name, function) in analyzer.Functions)
        {
            if (filter != null && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var signature = new System.Text.StringBuilder();
            signature.Append("(");
            signature.Append(string.Join(", ", function.Parameters.Select(p => $"{p.Name}: {p.Type.Name}")));
            signature.Append(")");
            if (function.ReturnType.Name != "void")
            {
                signature.Append(" -> ");
                signature.Append(function.ReturnType.Name);
            }

            completions.Add(new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Function,
                Detail = signature.ToString(),
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = function.IsExtern ? "External function (C/FFI)" : "Function"
                },
                InsertText = name
            });
        }

        // Add global variables and constants
        foreach (var (name, globalVar) in analyzer.GlobalVariables)
        {
            if (filter != null && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            completions.Add(new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Variable,
                Detail = globalVar.Type.Name,
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = "Global variable"
                }
            });
        }

        foreach (var (name, constant) in analyzer.Constants)
        {
            if (filter != null && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            completions.Add(new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Constant,
                Detail = $"{constant.Type.Name} = {constant.Value}",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = "Constant"
                }
            });
        }

        // Add structs
        foreach (var (name, structType) in analyzer.Structs)
        {
            if (filter != null && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var genericParams = structType.GenericParameters.Count > 0
                ? $"<{string.Join(", ", structType.GenericParameters)}>"
                : "";

            completions.Add(new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Struct,
                Detail = $"struct {name}{genericParams}",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = $"Struct with {structType.Fields.Count} fields"
                }
            });
        }

        // Add enums
        foreach (var (name, enumType) in analyzer.Enums)
        {
            if (filter != null && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var genericParams = enumType.GenericParameters.Count > 0
                ? $"<{string.Join(", ", enumType.GenericParameters)}>"
                : "";

            completions.Add(new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Enum,
                Detail = $"enum {name}{genericParams}",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = $"Enum with {enumType.Variants.Count} variants"
                }
            });
        }

        // Add traits
        foreach (var (name, trait) in analyzer.Traits)
        {
            if (filter != null && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            completions.Add(new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Interface,
                Detail = "trait",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = $"Trait with {trait.Methods.Count} methods"
                }
            });
        }
    }

    private void AddKeywordCompletions(List<CompletionItem> completions, string? filter = null)
    {
        foreach (var keyword in Keywords)
        {
            if (filter != null && !keyword.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            completions.Add(new CompletionItem
            {
                Label = keyword,
                Kind = CompletionItemKind.Keyword,
                Detail = "keyword",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = $"Novus keyword: {keyword}"
                }
            });
        }
    }

    private void AddBuiltInTypeCompletions(List<CompletionItem> completions, string? filter = null)
    {
        foreach (var type in BuiltInTypes)
        {
            if (filter != null && !type.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            completions.Add(new CompletionItem
            {
                Label = type,
                Kind = CompletionItemKind.Class,  // Use Class for types
                Detail = "built-in type",
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = $"Built-in type: {type}"
                }
            });
        }
    }

    public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            // DocumentSelector will be null, which means it applies to all documents
            TriggerCharacters = new[] { ".", "::" },  // Trigger on . for field access and :: for module access
            ResolveProvider = false  // We provide all info upfront, no need for resolve
        };
    }
}
