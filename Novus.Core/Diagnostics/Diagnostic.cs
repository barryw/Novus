namespace Novus.Diagnostics;

/// <summary>
/// Severity level of a diagnostic message
/// </summary>
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// Represents a diagnostic message (error, warning, or info)
/// </summary>
public class Diagnostic
{
    public DiagnosticSeverity Severity { get; }
    public string Code { get; }
    public string Message { get; }
    public SourceLocation Location { get; }
    public List<string> HelpTexts { get; }
    public List<(SourceLocation Location, string Label)> RelatedLocations { get; }

    public Diagnostic(
        DiagnosticSeverity severity,
        string code,
        string message,
        SourceLocation location,
        List<string>? helpTexts = null,
        List<(SourceLocation, string)>? relatedLocations = null)
    {
        Severity = severity;
        Code = code;
        Message = message;
        Location = location;
        HelpTexts = helpTexts ?? new List<string>();
        RelatedLocations = relatedLocations ?? new List<(SourceLocation, string)>();
    }

    public bool IsError => Severity == DiagnosticSeverity.Error;
    public bool IsWarning => Severity == DiagnosticSeverity.Warning;

    public override string ToString()
    {
        var severityStr = Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "note",
            _ => "note"
        };
        // Canonical lead-line shape (WHI Toolchain CLI Conventions §2):
        //   <file>:<line>:<col>: <severity>: <message> [<CODE>]
        var codeSuffix = string.IsNullOrEmpty(Code) ? "" : $" [{Code}]";
        return $"{Location.FilePath}:{Location.Line}:{Location.Column}: {severityStr}: {Message}{codeSuffix}";
    }
}
