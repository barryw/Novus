using System.Text.RegularExpressions;
using Novus;
using Novus.Diagnostics;

namespace Novus.Tests;

/// <summary>
/// Conformance tests for the WHI Language Toolchain CLI Conventions (ADR 0005 / WAL-77):
/// the diagnostic lead-line grammar (§2), the <c>--version</c> line shape (§4), and the
/// exit-code floor (§3). These lock the CLI surface so future changes stay parseable.
/// </summary>
public class CliConventionsTests
{
    // The reference parser contract from §2 of the conventions. A tool's output
    // must satisfy this on the lead line of every diagnostic.
    private static readonly Regex LeadLine = new(
        @"^(?<file>[^:]+):(?<line>\d+):(?<col>\d+): (?<sev>error|warning|note): (?<msg>.+?)( \[(?<code>[A-Za-z]+\d+)\])?$");

    private static SourceLocation Loc(int line = 5, int col = 10) =>
        new("main.novus", line, col, length: 3, sourceLine: "let x = y");

    private static string FirstLine(string formatted) =>
        formatted.Split('\n').First(l => l.Trim().Length > 0).TrimEnd('\r');

    [Fact]
    public void ErrorLeadLine_MatchesGrammar_AndCarriesCode()
    {
        var bag = new DiagnosticBag();
        bag.ReportError("E2001", "type 'NotAType' not found", Loc());

        var lead = FirstLine(bag.FormatDiagnostics());
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
    public void WarningLeadLine_UsesWarningSeverity()
    {
        var bag = new DiagnosticBag();
        bag.ReportWarning("W1000", "unused variable", Loc());

        var m = LeadLine.Match(FirstLine(bag.FormatDiagnostics()));
        Assert.True(m.Success);
        Assert.Equal("warning", m.Groups["sev"].Value);
    }

    [Fact]
    public void InfoSeverity_IsRenderedAsNote()
    {
        // §2 severity vocabulary is error | warning | note — Info maps to "note".
        var bag = new DiagnosticBag();
        bag.Add(new Diagnostic(DiagnosticSeverity.Info, "E9000", "consider annotating", Loc()));

        var m = LeadLine.Match(FirstLine(bag.FormatDiagnostics()));
        Assert.True(m.Success);
        Assert.Equal("note", m.Groups["sev"].Value);
    }

    [Fact]
    public void EmptyCode_OmitsBracketSuffix()
    {
        var bag = new DiagnosticBag();
        bag.ReportError("", "bare message", Loc());

        var lead = FirstLine(bag.FormatDiagnostics());
        Assert.DoesNotContain("[]", lead);
        Assert.EndsWith("bare message", lead);
        Assert.True(LeadLine.IsMatch(lead), $"Lead line without a code must still match: '{lead}'");
    }

    [Fact]
    public void VersionLine_HasCanonicalShape()
    {
        // §4: `<tool-name> <semver>` — no `v` prefix, no "Compiler" word, no banner.
        Assert.Matches(@"^novus \d+\.\d+\.\d+$", CliConventions.VersionLine());
    }

    [Fact]
    public void ExitCodeFloor_HonoursConventions()
    {
        // §3: 0 = success, 1 = usage/env, >=2 = compilation failed with diagnostics.
        Assert.Equal(0, CliConventions.ExitSuccess);
        Assert.Equal(1, CliConventions.ExitUsage);
        Assert.True(CliConventions.ExitCompileError >= 2,
            "A compilation error must exit >= 2 so it is distinguishable from a usage error.");
    }
}
