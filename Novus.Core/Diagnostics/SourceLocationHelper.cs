using Antlr4.Runtime;

namespace Novus.Diagnostics;

/// <summary>
/// Helper methods for extracting source locations from ANTLR parse contexts
/// </summary>
public static class SourceLocationHelper
{
    public static SourceLocation FromContext(ParserRuleContext context, string filePath, string[] sourceLines)
    {
        var token = context.Start;
        var line = token.Line;
        var column = token.Column + 1; // ANTLR uses 0-based columns
        var length = context.Stop != null
            ? context.Stop.StopIndex - context.Start.StartIndex + 1
            : context.GetText().Length;

        var sourceLine = line > 0 && line <= sourceLines.Length
            ? sourceLines[line - 1]
            : string.Empty;

        return new SourceLocation(filePath, line, column, length, sourceLine);
    }

    public static SourceLocation FromToken(IToken token, string filePath, string[] sourceLines)
    {
        var line = token.Line;
        var column = token.Column + 1;
        var length = token.Text.Length;

        var sourceLine = line > 0 && line <= sourceLines.Length
            ? sourceLines[line - 1]
            : string.Empty;

        return new SourceLocation(filePath, line, column, length, sourceLine);
    }
}
