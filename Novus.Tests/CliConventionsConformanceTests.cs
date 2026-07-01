using System.Text.RegularExpressions;
using Novus;
using Novus.Diagnostics;

namespace Novus.Tests;

/// <summary>
/// Conformance tests for the Walker Heavy Industries "Language Toolchain CLI
/// Conventions" (docs/toolchain-cli-conventions.md, ADR 0005, whi-brand).
///
/// These assert the machine-parseable surface the family shares: the §2
/// diagnostic lead-line grammar, the §3 exit-code floor, and the §4
/// <c>--version</c> line shape. They intentionally test seams that do NOT
/// require an external toolchain (VBCC/vasm/vlink) so they run in any CI.
/// </summary>
public class CliConventionsConformanceTests
{
    // Reference parser contract for a diagnostic lead line (verbatim from §2).
    private static readonly Regex LeadLine = new(
        @"^(?<file>[^:]+):(?<line>\d+):(?<col>\d+): (?<sev>error|warning|note): (?<msg>.+?)( \[(?<code>[A-Za-z]+\d+)\])?$",
        RegexOptions.Compiled);

    private static DiagnosticBag BagWith(DiagnosticSeverity severity, string code, string message)
    {
        var bag = new DiagnosticBag();
        var location = new SourceLocation("main.novus", 5, 10, 8, "let x: NotAType = 42");
        bag.Add(new Diagnostic(severity, code, message, location,
            helpTexts: new List<string> { "did you mean 'i32'?" }));
        return bag;
    }

    private static string LeadLineOf(string formatted) =>
        formatted.Split('\n')[0].TrimEnd('\r');

    // ----- §2 Diagnostics --------------------------------------------------

    [Fact]
    public void ErrorDiagnostic_LeadLine_MatchesCanonicalGrammar()
    {
        var formatted = BagWith(DiagnosticSeverity.Error, "E2001", "type 'NotAType' not found")
            .FormatDiagnostics();

        var lead = LeadLineOf(formatted);
        var m = LeadLine.Match(lead);

        Assert.True(m.Success, $"Lead line did not match §2 grammar: '{lead}'");
        Assert.Equal("main.novus", m.Groups["file"].Value);
        Assert.Equal("5", m.Groups["line"].Value);
        Assert.Equal("10", m.Groups["col"].Value);
        Assert.Equal("error", m.Groups["sev"].Value);
        Assert.Equal("type 'NotAType' not found", m.Groups["msg"].Value);
        Assert.Equal("E2001", m.Groups["code"].Value);
    }

    [Fact]
    public void WarningDiagnostic_LeadLine_MatchesCanonicalGrammar()
    {
        var formatted = BagWith(DiagnosticSeverity.Warning, "W0100", "value truncated")
            .FormatDiagnostics();

        var m = LeadLine.Match(LeadLineOf(formatted));
        Assert.True(m.Success);
        Assert.Equal("warning", m.Groups["sev"].Value);
        Assert.Equal("W0100", m.Groups["code"].Value);
    }

    [Fact]
    public void Diagnostic_Code_IsPresent_AndTrailsInBrackets()
    {
        var lead = LeadLineOf(
            BagWith(DiagnosticSeverity.Error, "E2001", "boom").FormatDiagnostics());

        // Code is the last element, in trailing square brackets.
        Assert.EndsWith("[E2001]", lead);
    }

    [Fact]
    public void Diagnostic_RichContext_StaysBelowLeadLine()
    {
        var formatted = BagWith(DiagnosticSeverity.Error, "E2001", "boom").FormatDiagnostics();
        var lines = formatted.Split('\n');

        // The lead line must be first; the source snippet / help block follows below.
        Assert.Matches(LeadLine, LeadLineOf(formatted));
        Assert.Contains(lines, l => l.Contains("let x: NotAType = 42")); // snippet below
        Assert.Contains(lines, l => l.Contains("help:"));                 // help below
    }

    [Fact]
    public void Diagnostic_DoesNotUse_OldRustStyleHeader()
    {
        var formatted = BagWith(DiagnosticSeverity.Error, "E2001", "boom").FormatDiagnostics();
        var lead = LeadLineOf(formatted);
        Assert.DoesNotContain("error[E2001]", lead); // old Rust-style prefix is gone
    }

    // ----- §4 --version ----------------------------------------------------

    [Fact]
    public void Version_Line_HasCanonicalShape()
    {
        // "<tool-name> <semver>": lowercase tool name, no "v" prefix, no banner word.
        Assert.Matches(@"^novus \d+\.\d+\.\d+", Program.VersionLine());
        Assert.DoesNotContain("Compiler", Program.VersionLine());
    }

    [Fact]
    public void Version_Flag_PrintsToStdout_AndExitsZero()
    {
        var sw = new StringWriter();
        var exit = Program.TryHandleVersion(new[] { "--version" }, sw);

        Assert.True(exit.HasValue);
        Assert.Equal(Program.EXIT_SUCCESS, exit!.Value);
        var printed = sw.ToString().Trim();
        Assert.Matches(@"^novus \d+\.\d+\.\d+", printed);
        Assert.DoesNotContain("Novus Compiler", printed); // banner suppressed under --version
    }

    [Fact]
    public void Version_Flag_Absent_DoesNotHandle()
    {
        var sw = new StringWriter();
        Assert.Null(Program.TryHandleVersion(new[] { "compile", "foo.novus" }, sw));
        Assert.Equal(string.Empty, sw.ToString());
    }

    // ----- §3 Exit codes ---------------------------------------------------

    [Fact]
    public void ExitCode_Floor_IsFixed()
    {
        Assert.Equal(0, Program.EXIT_SUCCESS);
        Assert.Equal(1, Program.EXIT_USAGE);          // usage / environment
        Assert.Equal(2, Program.EXIT_COMPILE_ERROR);  // compilation with diagnostics
    }

    [Fact]
    public async Task MissingInputFile_ReturnsUsageExit()
    {
        var options = new CompilerOptions
        {
            InputFile = Path.Combine(Path.GetTempPath(), "novus-cli-conformance-does-not-exist.novus")
        };

        var exit = await Program.RunCompiler(options);

        // Missing input = couldn't start → floor code 1, never 0.
        Assert.Equal(Program.EXIT_USAGE, exit);
    }
}
