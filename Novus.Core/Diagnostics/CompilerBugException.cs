namespace Novus.Diagnostics;

/// <summary>
/// Exception thrown when an internal compiler invariant is violated.
/// This indicates a bug in the compiler itself, not a user error.
/// </summary>
public class CompilerBugException : Exception
{
    public string Context { get; }
    public string? FilePath { get; }
    public int? LineNumber { get; }

    public CompilerBugException(string message, string context, string? filePath = null, int? lineNumber = null)
        : base($"COMPILER BUG: {message}\n\nContext: {context}\n\nThis is a bug in the Novus compiler. Please report this at https://github.com/anthropics/novus/issues")
    {
        Context = context;
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                    COMPILER BUG DETECTED                       ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine(Message);

        if (FilePath != null)
        {
            sb.AppendLine();
            sb.AppendLine($"File: {FilePath}");
            if (LineNumber.HasValue)
                sb.AppendLine($"Line: {LineNumber}");
        }

        sb.AppendLine();
        sb.AppendLine("Please report this bug with:");
        sb.AppendLine("  1. The source code that triggered this error");
        sb.AppendLine("  2. The full error message above");
        sb.AppendLine("  3. Your Novus compiler version");
        sb.AppendLine();
        sb.AppendLine("Stack trace:");
        sb.AppendLine(StackTrace);

        return sb.ToString();
    }
}
