using System.Reflection;

namespace Novus;

/// <summary>
/// Helpers for the WHI Language Toolchain CLI Conventions (ADR 0005 / WAL-77).
/// Centralises the version-line format and the exit-code floor so every entry
/// point in the compiler stays consistent (and testable).
/// </summary>
public static class CliConventions
{
    // Exit-code floor (§3). A toolchain may use finer-grained codes above 2,
    // but these three are fixed: 0 = ok, 1 = couldn't start, >=2 = build failed.

    /// <summary>Success. Artifact produced (or nothing to do).</summary>
    public const int ExitSuccess = 0;

    /// <summary>Usage / environment error — bad flags, missing input file, missing
    /// external tool. No source diagnostics were produced.</summary>
    public const int ExitUsage = 1;

    /// <summary>Compilation error — the source was processed and one or more
    /// <c>error</c> diagnostics were emitted.</summary>
    public const int ExitCompileError = 2;

    /// <summary>
    /// The machine-parseable <c>--version</c> line (§4): <c>novus &lt;Major.Minor.Build&gt;</c>.
    /// No <c>v</c> prefix, no "Compiler" word, no banner. The version is the
    /// assembly version, which the csproj derives from <c>&lt;Version&gt;</c>.
    /// </summary>
    public static string VersionLine()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var semver = version is null
            ? "0.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
        return $"novus {semver}";
    }
}
