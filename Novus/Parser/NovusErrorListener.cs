using System;
using System.IO;
using Antlr4.Runtime;
using Novus.Diagnostics;

namespace Novus.Parser;

/// <summary>
/// Custom ANTLR error listener that integrates parse errors with DiagnosticBag.
/// This ensures all errors (parse, semantic, IR) are reported consistently.
/// </summary>
public class NovusErrorListener : IAntlrErrorListener<IToken>
{
    private readonly DiagnosticBag _diagnostics;
    private readonly string _filePath;
    private readonly string _source;

    public NovusErrorListener(DiagnosticBag diagnostics, string filePath, string source)
    {
        _diagnostics = diagnostics;
        _filePath = filePath;
        _source = source;
    }

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException? e)
    {
        // Convert ANTLR message to our diagnostic format
        var errorMessage = FormatErrorMessage(msg, offendingSymbol);

        // Extract the source line (handle both Unix \n and Windows \r\n line endings)
        var lines = _source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var sourceLine = line > 0 && line <= lines.Length ? lines[line - 1] : "";

        // Determine length of error (use token length if available, otherwise 1)
        var length = offendingSymbol?.Text?.Length ?? 1;

        // Create source location
        var location = new SourceLocation(
            _filePath,
            line,
            charPositionInLine + 1, // ANTLR uses 0-based columns, we use 1-based
            length,
            sourceLine
        );

        _diagnostics.ReportError("E0001", errorMessage, location);
    }

    private string FormatErrorMessage(string antlrMessage, IToken? offendingSymbol)
    {
        // Clean up ANTLR's sometimes verbose messages
        var message = antlrMessage;

        // Remove redundant "syntax error, " prefix if present
        if (message.StartsWith("syntax error, "))
        {
            message = message.Substring("syntax error, ".Length);
        }

        // Capitalize first letter
        if (message.Length > 0)
        {
            message = char.ToUpper(message[0]) + message.Substring(1);
        }

        return message;
    }
}
