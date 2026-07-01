using System.Text;

namespace Novus.Diagnostics;

/// <summary>
/// Collects and manages diagnostic messages during compilation
/// </summary>
public class DiagnosticBag
{
    private readonly List<Diagnostic> _diagnostics = new();

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public bool HasErrors => _diagnostics.Any(d => d.IsError);
    public bool HasWarnings => _diagnostics.Any(d => d.IsWarning);

    public int ErrorCount => _diagnostics.Count(d => d.IsError);
    public int WarningCount => _diagnostics.Count(d => d.IsWarning);

    public void Add(Diagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    public void ReportError(string code, string message, SourceLocation location,
        List<string>? helpTexts = null, List<(SourceLocation, string)>? relatedLocations = null)
    {
        Add(new Diagnostic(DiagnosticSeverity.Error, code, message, location, helpTexts, relatedLocations));
    }

    public void ReportWarning(string code, string message, SourceLocation location,
        List<string>? helpTexts = null, List<(SourceLocation, string)>? relatedLocations = null)
    {
        Add(new Diagnostic(DiagnosticSeverity.Warning, code, message, location, helpTexts, relatedLocations));
    }

    public void Clear()
    {
        _diagnostics.Clear();
    }

    /// <summary>
    /// Formats diagnostics in a Rust-like style with source snippets and helpful messages
    /// </summary>
    public string FormatDiagnostics(bool useColor = false)
    {
        var sb = new StringBuilder();

        foreach (var diagnostic in _diagnostics)
        {
            FormatDiagnostic(sb, diagnostic, useColor);
            sb.AppendLine();
        }

        // Summary
        if (HasErrors || HasWarnings)
        {
            sb.AppendLine();
            var parts = new List<string>();
            if (HasErrors)
                parts.Add($"{ErrorCount} error{(ErrorCount != 1 ? "s" : "")}");
            if (HasWarnings)
                parts.Add($"{WarningCount} warning{(WarningCount != 1 ? "s" : "")}");

            sb.AppendLine(string.Join(", ", parts) + " generated");
        }

        return sb.ToString();
    }

    private void FormatDiagnostic(StringBuilder sb, Diagnostic diagnostic, bool useColor)
    {
        // Canonical lead line (WHI Toolchain CLI Conventions §2):
        //   <file>:<line>:<col>: <severity>: <message> [<CODE>]
        // Machine-parseable, fixed-shape prefix. The bracketed code is the only
        // optional element and always comes last. Rich context (source snippet,
        // carets, help/note hints) follows BELOW as indented continuation lines,
        // so a consumer reading only lead lines still gets a complete diagnostic.
        var severityLabel = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "note",
            _ => "note"
        };

        var loc = diagnostic.Location;
        var codeSuffix = string.IsNullOrEmpty(diagnostic.Code) ? "" : $" [{diagnostic.Code}]";
        sb.AppendLine($"{loc.FilePath}:{loc.Line}:{loc.Column}: {severityLabel}: {diagnostic.Message}{codeSuffix}");

        // Source snippet with caret pointing to error
        var lineNumStr = diagnostic.Location.Line.ToString();
        var lineNumWidth = lineNumStr.Length;
        var padding = new string(' ', lineNumWidth);

        sb.AppendLine($" {padding} |");
        sb.AppendLine($" {lineNumStr} | {diagnostic.Location.SourceLine}");

        // Caret line pointing to the error
        var caretPadding = new string(' ', Math.Max(0, diagnostic.Location.Column - 1));
        var carets = new string('^', Math.Max(1, diagnostic.Location.Length));
        sb.AppendLine($" {padding} | {caretPadding}{carets}");

        // Related locations
        foreach (var (location, label) in diagnostic.RelatedLocations)
        {
            sb.AppendLine($" {padding} |");
            sb.AppendLine($"  note: {label}");
            sb.AppendLine($"    --> {location}");
        }

        // Help text
        foreach (var helpText in diagnostic.HelpTexts)
        {
            sb.AppendLine($" {padding} |");
            sb.AppendLine($"  help: {helpText}");
        }
    }

    public override string ToString()
    {
        return FormatDiagnostics(useColor: false);
    }
}
