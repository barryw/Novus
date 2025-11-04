using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using LspDiagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;

namespace Novus.LanguageServer;

/// <summary>
/// Handles "Code Action" requests from the LSP client.
/// Provides quick fixes and refactorings like auto-import.
/// This is the "lightbulb" feature in IDEs.
/// </summary>
public class CodeActionHandler : ICodeActionHandler
{
    private readonly DocumentManager _documentManager;
    private readonly string _stdLibPath;

    // Known stdlib modules and what they export
    private static readonly Dictionary<string, string[]> StdLibModules = new()
    {
        { "std::core", new[] { "Option", "Result", "Some", "None", "Ok", "Err" } },
        { "std::error", new[] { "ExecError", "DosError", "IntuitionError", "NovusError" } },
        { "std::mem", new[] { "MemoryBlock", "Allocation", "Box" } },
        { "std::collections", new[] { "Vec" } },
        { "std::strings", new[] { "strlen", "strcmp", "strcpy", "strncpy", "strcat", "strncat", "memset", "memcpy", "memmove", "memcmp" } },
        { "std::io", new[] { "print", "println", "eprint", "eprintln" } },
        { "std::exec", new[] { "get_current_task", "find_task", "set_task_priority", "allocate_signal", "free_signal", "wait_for_signals", "send_signal", "forbid", "permit", "disable", "enable" } },
        { "std::dos", new[] { "System", "CreateProcess", "AllocDos", "LoadSeg", "open_file", "close_file", "read_file", "write_file", "seek_file" } },
        { "std::intuition", new[] { "OpenWindow", "OpenScreen" } },
        { "std::tags", new[] { "make_tags" } },
        { "std::ffi::exec", new[] { "AllocMem", "FreeMem", "AllocVec", "FreeVec", "CopyMem", "FindTask", "SetTaskPri", "AllocSignal", "FreeSignal", "Wait", "Signal", "Forbid", "Permit", "Disable", "Enable" } },
        { "std::ffi::dos", new[] { "Open", "Close", "Read", "Write", "Seek", "SystemTagList", "CreateNewProc", "AllocDosObject", "FreeDosObject", "NewLoadSeg", "UnLoadSeg" } },
        { "std::ffi::intuition", new[] { "OpenWindowTagList", "OpenScreenTagList", "CloseWindow", "CloseScreen", "EasyRequest" } },
        { "std::ffi::amiga_structs", new[] { "TagItem", "Window", "Screen", "NewWindow", "NewScreen", "IntuiMessage", "EasyStruct" } },
        { "std::ffi::amiga_consts", new[] { "MEMF_ANY", "MEMF_PUBLIC", "MEMF_CHIP", "MEMF_FAST", "MEMF_CLEAR", "TAG_DONE", "TAG_END", "TAG_SKIP", "TAG_MORE" } },
    };

    public CodeActionHandler(DocumentManager documentManager, string stdLibPath)
    {
        _documentManager = documentManager;
        _stdLibPath = stdLibPath;
    }

    public Task<CommandOrCodeActionContainer?> Handle(CodeActionParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri.ToString();

        Console.Error.WriteLine($"[LSP] Code action request for {uri}");
        Console.Error.WriteLine($"[LSP] Range: {request.Range.Start.Line}:{request.Range.Start.Character} - {request.Range.End.Line}:{request.Range.End.Character}");
        Console.Error.WriteLine($"[LSP] Diagnostics: {request.Context.Diagnostics.Count()}");

        var state = _documentManager.Get(uri);
        if (state == null)
        {
            Console.Error.WriteLine("[LSP] Document not found");
            return Task.FromResult<CommandOrCodeActionContainer?>(null);
        }

        var codeActions = new List<CommandOrCodeAction>();

        try
        {
            // Check each diagnostic for auto-import opportunities
            foreach (var diagnostic in request.Context.Diagnostics)
            {
                Console.Error.WriteLine($"[LSP] Diagnostic: {diagnostic.Code} - {diagnostic.Message}");

                // Handle undefined variable/type errors
                if (diagnostic.Code?.String == "E0007" || // undefined variable
                    diagnostic.Code?.String == "E0033" || // type not found
                    diagnostic.Code?.String == "E0032")   // trait not found
                {
                    var undefinedSymbol = ExtractSymbolName(diagnostic.Message);
                    if (undefinedSymbol != null)
                    {
                        Console.Error.WriteLine($"[LSP] Found undefined symbol: {undefinedSymbol}");

                        // Find which modules export this symbol
                        var modulesExportingSymbol = FindModulesExportingSymbol(undefinedSymbol);
                        Console.Error.WriteLine($"[LSP] Modules exporting {undefinedSymbol}: {string.Join(", ", modulesExportingSymbol)}");

                        foreach (var module in modulesExportingSymbol)
                        {
                            // Create code action to import from this module
                            var codeAction = CreateImportCodeAction(
                                uri,
                                state.Text,
                                module,
                                undefinedSymbol,
                                diagnostic
                            );

                            if (codeAction != null)
                            {
                                codeActions.Add(codeAction);
                            }
                        }
                    }
                }
            }

            if (codeActions.Count == 0)
            {
                Console.Error.WriteLine("[LSP] No code actions generated");
                return Task.FromResult<CommandOrCodeActionContainer?>(null);
            }

            Console.Error.WriteLine($"[LSP] Returning {codeActions.Count} code actions");
            return Task.FromResult<CommandOrCodeActionContainer?>(new CommandOrCodeActionContainer(codeActions));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error generating code actions: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return Task.FromResult<CommandOrCodeActionContainer?>(null);
        }
    }

    /// <summary>
    /// Extracts the symbol name from an error message like "undefined variable 'Foo'"
    /// </summary>
    private string? ExtractSymbolName(string message)
    {
        // Pattern: "undefined variable 'Name'" or "type 'Name' not found"
        var match = System.Text.RegularExpressions.Regex.Match(message, @"'([^']+)'");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return null;
    }

    /// <summary>
    /// Finds all stdlib modules that export the given symbol
    /// </summary>
    private List<string> FindModulesExportingSymbol(string symbolName)
    {
        var modules = new List<string>();

        foreach (var (moduleName, exports) in StdLibModules)
        {
            if (exports.Contains(symbolName))
            {
                modules.Add(moduleName);
            }
        }

        // TODO: In the future, scan the actual module files instead of using hardcoded list
        // This would support user modules and dynamically discover exports

        return modules;
    }

    /// <summary>
    /// Creates a code action to add an import statement
    /// </summary>
    private CommandOrCodeAction? CreateImportCodeAction(
        string uri,
        string sourceText,
        string moduleName,
        string symbolName,
        LspDiagnostic diagnostic)
    {
        try
        {
            // Find where to insert the import (after existing imports or at the top)
            var insertPosition = FindImportInsertPosition(sourceText);

            // Build the import statement
            var importStatement = $"from {moduleName} import {symbolName}\n";

            // Create the text edit
            var edit = new TextEdit
            {
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
                {
                    Start = new Position { Line = insertPosition, Character = 0 },
                    End = new Position { Line = insertPosition, Character = 0 }
                },
                NewText = importStatement
            };

            // Create workspace edit
            var workspaceEdit = new WorkspaceEdit
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
                {
                    { DocumentUri.Parse(uri), new[] { edit } }
                }
            };

            // Create the code action
            var codeAction = new CodeAction
            {
                Title = $"Import '{symbolName}' from {moduleName}",
                Kind = CodeActionKind.QuickFix,
                Diagnostics = new[] { diagnostic },
                Edit = workspaceEdit,
                IsPreferred = true // Mark as preferred so it shows first
            };

            return new CommandOrCodeAction(codeAction);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LSP] Error creating import code action: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Finds the line number where a new import should be inserted.
    /// Places it after existing imports, or at line 0 if no imports exist.
    /// </summary>
    private int FindImportInsertPosition(string sourceText)
    {
        var lines = sourceText.Split('\n');
        int lastImportLine = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();

            // Check if this is an import line
            if (trimmed.StartsWith("from ") && trimmed.Contains(" import "))
            {
                lastImportLine = i;
            }
            // Stop at first non-import, non-blank, non-comment line
            else if (!string.IsNullOrWhiteSpace(trimmed) &&
                     !trimmed.StartsWith("//") &&
                     lastImportLine >= 0)
            {
                break;
            }
        }

        // Insert after the last import (or at line 0 if no imports)
        return lastImportLine + 1;
    }

    public CodeActionRegistrationOptions GetRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities)
    {
        return new CodeActionRegistrationOptions
        {
            // DocumentSelector will be null, which means it applies to all documents
            CodeActionKinds = new[]
            {
                CodeActionKind.QuickFix,
                CodeActionKind.RefactorRewrite
            },
            ResolveProvider = false  // We provide complete actions immediately
        };
    }
}
