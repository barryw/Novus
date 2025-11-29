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

            // Check if we're after a '.' (struct field access)
            if (position.Character > 0 && line[(int)position.Character - 1] == '.')
            {
                var structName = GetStructNameBeforeDot(line, (int)position.Character - 1);
                if (structName != null)
                {
                    Console.Error.WriteLine($"[LSP] Struct field completion for: {structName}");
                    AddStructFieldCompletions(state.SemanticAnalyzer, completions, structName);
                    return Task.FromResult(new CompletionList(completions));
                }
            }

            // Check if we're after '::' (enum variant or method access)
            if (position.Character > 1 &&
                line[(int)position.Character - 1] == ':' &&
                line[(int)position.Character - 2] == ':')
            {
                var typeName = GetTypeNameBeforeColons(line, (int)position.Character - 2);
                if (typeName != null)
                {
                    Console.Error.WriteLine($"[LSP] Path completion for: {typeName}");
                    AddPathCompletions(state.SemanticAnalyzer, completions, typeName);
                    return Task.FromResult(new CompletionList(completions));
                }
            }

            // Normal symbol completion
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

    /// <summary>
    /// Extracts the struct/variable name before a '.' character
    /// </summary>
    private string? GetStructNameBeforeDot(string line, int dotPosition)
    {
        int end = dotPosition - 1;
        while (end >= 0 && char.IsWhiteSpace(line[end]))
        {
            end--;
        }

        if (end < 0) return null;

        int start = end;
        while (start >= 0 && (char.IsLetterOrDigit(line[start]) || line[start] == '_'))
        {
            start--;
        }

        if (start == end) return null;

        return line.Substring(start + 1, end - start);
    }

    /// <summary>
    /// Extracts the type name before '::'
    /// </summary>
    private string? GetTypeNameBeforeColons(string line, int colonPosition)
    {
        int end = colonPosition - 1;
        while (end >= 0 && char.IsWhiteSpace(line[end]))
        {
            end--;
        }

        if (end < 0) return null;

        int start = end;
        while (start >= 0 && (char.IsLetterOrDigit(line[start]) || line[start] == '_'))
        {
            start--;
        }

        if (start == end) return null;

        return line.Substring(start + 1, end - start);
    }

    /// <summary>
    /// Adds struct field completions for a given struct name
    /// </summary>
    private void AddStructFieldCompletions(Novus.SemanticAnalysis.SemanticAnalyzer analyzer,
                                          List<CompletionItem> completions,
                                          string structName)
    {
        // Check if it's a known struct type
        if (!analyzer.Structs.TryGetValue(structName, out var structType))
        {
            Console.Error.WriteLine($"[LSP] Struct '{structName}' not found");
            return;
        }

        // Add each field as a completion item
        foreach (var field in structType.Fields)
        {
            completions.Add(new CompletionItem
            {
                Label = field.Name,
                Kind = CompletionItemKind.Field,
                Detail = field.Type.Name,
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = $"Field of type {field.Type.Name}"
                },
                InsertText = field.Name
            });
        }

        Console.Error.WriteLine($"[LSP] Added {structType.Fields.Count} field completions");
    }

    /// <summary>
    /// Adds enum variants and associated methods for a given type name
    /// </summary>
    private void AddPathCompletions(Novus.SemanticAnalysis.SemanticAnalyzer analyzer,
                                   List<CompletionItem> completions,
                                   string typeName)
    {
        // Check if it's an enum - add variants
        if (analyzer.Enums.TryGetValue(typeName, out var enumType))
        {
            foreach (var variant in enumType.Variants)
            {
                var variantDoc = new System.Text.StringBuilder();
                variantDoc.Append($"Enum variant");
                if (variant.AssociatedData.Count > 0)
                {
                    variantDoc.Append($" with {variant.AssociatedData.Count} associated values: ");
                    variantDoc.Append(string.Join(", ", variant.AssociatedData.Select(t => t.Name)));
                }

                completions.Add(new CompletionItem
                {
                    Label = variant.Name,
                    Kind = CompletionItemKind.EnumMember,
                    Detail = variant.AssociatedData.Count > 0
                        ? $"({string.Join(", ", variant.AssociatedData.Select(t => t.Name))})"
                        : "enum variant",
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.PlainText,
                        Value = variantDoc.ToString()
                    },
                    InsertText = variant.Name
                });
            }

            Console.Error.WriteLine($"[LSP] Added {enumType.Variants.Count} enum variant completions");
        }

        // Check if it's a struct - add associated methods (impl block methods)
        if (analyzer.Structs.ContainsKey(typeName))
        {
            // Find methods for this struct
            foreach (var (funcName, function) in analyzer.Functions)
            {
                // Check if this is an associated function (starts with TypeName::)
                if (funcName.StartsWith($"{typeName}::"))
                {
                    var methodName = funcName.Substring(typeName.Length + 2);

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
                        Label = methodName,
                        Kind = CompletionItemKind.Method,
                        Detail = signature.ToString(),
                        Documentation = new MarkupContent
                        {
                            Kind = MarkupKind.PlainText,
                            Value = $"Method on {typeName}"
                        },
                        InsertText = methodName
                    });
                }
            }
        }

        // Same for enums
        if (analyzer.Enums.TryGetValue(typeName, out var enumTypeForMethods))
        {
            foreach (var (funcName, function) in analyzer.Functions)
            {
                if (funcName.StartsWith($"{typeName}::") && !enumTypeForMethods.Variants.Any(v => v.Name == funcName.Substring(typeName.Length + 2)))
                {
                    var methodName = funcName.Substring(typeName.Length + 2);

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
                        Label = methodName,
                        Kind = CompletionItemKind.Method,
                        Detail = signature.ToString(),
                        Documentation = new MarkupContent
                        {
                            Kind = MarkupKind.PlainText,
                            Value = $"Method on {typeName}"
                        },
                        InsertText = methodName
                    });
                }
            }
        }
    }

    public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new CompletionRegistrationOptions
        {
            // DocumentSelector will be null, which means it applies to all documents
            TriggerCharacters = new[] { ".", ":", },  // Trigger on . for field access and : for :: detection
            ResolveProvider = false  // We provide all info upfront, no need for resolve
        };
    }
}
