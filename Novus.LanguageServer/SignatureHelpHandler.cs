using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Novus.LanguageServer;

/// <summary>
/// Handles "Signature Help" requests from the LSP client.
/// Shows function parameter information while typing function calls.
/// Triggered when the user types '(' or ','.
/// </summary>
public class SignatureHelpHandler : ISignatureHelpHandler
{
    private readonly DocumentManager _documentManager;

    public SignatureHelpHandler(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    public Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();
        var position = request.Position;

        Console.Error.WriteLine($"[LSP] Signature help request for {uri} at {position.Line}:{position.Character}");

        // Get the document state
        var state = _documentManager.Get(uri);
        if (state?.SemanticAnalyzer == null || state.ParseTree == null)
        {
            Console.Error.WriteLine("[LSP] No semantic analyzer or parse tree available");
            return Task.FromResult<SignatureHelp?>(null);
        }

        try
        {
            // Find the function being called by looking backwards from the cursor
            var functionName = FindFunctionAtCallSite(state.Text, (int)position.Line, (int)position.Character);
            if (functionName == null)
            {
                Console.Error.WriteLine("[LSP] No function call found at position");
                return Task.FromResult<SignatureHelp?>(null);
            }

            Console.Error.WriteLine($"[LSP] Found function call: {functionName}");

            // Look up the function in the semantic analyzer
            if (!state.SemanticAnalyzer.Functions.TryGetValue(functionName, out var function))
            {
                Console.Error.WriteLine($"[LSP] Function '{functionName}' not found in symbol table");
                return Task.FromResult<SignatureHelp?>(null);
            }

            // Build signature help
            var signatureInfo = new SignatureInformation
            {
                Label = BuildFunctionSignature(function),
                Documentation = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = BuildFunctionDocumentation(function)
                },
                Parameters = function.Parameters.Select(p => new ParameterInformation
                {
                    Label = $"{p.Name}: {p.Type.Name}",
                    Documentation = new MarkupContent
                    {
                        Kind = MarkupKind.PlainText,
                        Value = $"Parameter {p.Name} of type {p.Type.Name}"
                    }
                }).ToList()
            };

            // Determine which parameter the cursor is currently on
            var activeParameter = DetermineActiveParameter(state.Text, (int)position.Line, (int)position.Character);

            var signatureHelp = new SignatureHelp
            {
                Signatures = new List<SignatureInformation> { signatureInfo },
                ActiveSignature = 0,
                ActiveParameter = activeParameter
            };

            return Task.FromResult<SignatureHelp?>(signatureHelp);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error resolving signature help: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return Task.FromResult<SignatureHelp?>(null);
        }
    }

    /// <summary>
    /// Finds the function name at a call site by looking backwards from the cursor position.
    /// </summary>
    private string? FindFunctionAtCallSite(string text, int line, int column)
    {
        var lines = text.Split('\n');
        if (line >= lines.Length)
        {
            return null;
        }

        var currentLine = lines[line];
        if (column > currentLine.Length)
        {
            column = currentLine.Length;
        }

        // Look backwards from cursor to find the opening parenthesis
        int parenDepth = 0;
        int searchPos = column - 1;

        while (searchPos >= 0)
        {
            var ch = currentLine[searchPos];

            if (ch == ')')
            {
                parenDepth++;
            }
            else if (ch == '(')
            {
                if (parenDepth == 0)
                {
                    // Found the opening paren - now extract function name
                    int nameEnd = searchPos - 1;
                    while (nameEnd >= 0 && char.IsWhiteSpace(currentLine[nameEnd]))
                    {
                        nameEnd--;
                    }

                    if (nameEnd < 0)
                    {
                        return null;
                    }

                    int nameStart = nameEnd;
                    while (nameStart >= 0 &&
                           (char.IsLetterOrDigit(currentLine[nameStart]) ||
                            currentLine[nameStart] == '_' ||
                            currentLine[nameStart] == ':'))  // Support :: for method calls
                    {
                        nameStart--;
                    }

                    var functionName = currentLine.Substring(nameStart + 1, nameEnd - nameStart);
                    return functionName.Trim();
                }
                parenDepth--;
            }

            searchPos--;
        }

        return null;
    }

    /// <summary>
    /// Determines which parameter position the cursor is currently at by counting commas.
    /// </summary>
    private int DetermineActiveParameter(string text, int line, int column)
    {
        var lines = text.Split('\n');
        if (line >= lines.Length)
        {
            return 0;
        }

        var currentLine = lines[line];
        if (column > currentLine.Length)
        {
            column = currentLine.Length;
        }

        // Count commas from the opening paren to the cursor position
        int parenDepth = 0;
        int commaCount = 0;
        int searchPos = column - 1;

        while (searchPos >= 0)
        {
            var ch = currentLine[searchPos];

            if (ch == ')')
            {
                parenDepth++;
            }
            else if (ch == '(')
            {
                if (parenDepth == 0)
                {
                    // Found the opening paren - stop counting
                    break;
                }
                parenDepth--;
            }
            else if (ch == ',' && parenDepth == 0)
            {
                commaCount++;
            }

            searchPos--;
        }

        return commaCount;
    }

    private string BuildFunctionSignature(Novus.SemanticAnalysis.FunctionSymbol function)
    {
        var signature = new System.Text.StringBuilder();
        signature.Append(function.Name);
        signature.Append("(");
        signature.Append(string.Join(", ", function.Parameters.Select(p =>
            $"{p.Name}: {p.Type.Name}")));
        signature.Append(")");

        if (function.ReturnType.Name != "void")
        {
            signature.Append(" -> ");
            signature.Append(function.ReturnType.Name);
        }

        return signature.ToString();
    }

    private string BuildFunctionDocumentation(Novus.SemanticAnalysis.FunctionSymbol function)
    {
        var doc = new System.Text.StringBuilder();

        if (function.IsExtern)
        {
            doc.AppendLine("*External function (C/FFI)*");
            doc.AppendLine();
        }

        // Doc comment extraction is handled at the semantic analysis level.
        // When available, doc comments will be attached to function symbols.
        doc.AppendLine($"Function `{function.Name}`");

        return doc.ToString();
    }

    public SignatureHelpRegistrationOptions GetRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
    {
        return new SignatureHelpRegistrationOptions
        {
            // DocumentSelector will be null, which means it applies to all documents
            TriggerCharacters = new[] { "(", "," }  // Trigger signature help when user types ( or ,
        };
    }
}
