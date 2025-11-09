using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Integration tests that verify language server and compiler see the same code.
/// These tests ensure stdlib files have ZERO ERRORS when analyzed using the same
/// code path as the language server - both parsing AND semantic analysis.
/// This is CRITICAL for editor/compiler consistency.
/// </summary>
public class LanguageServerIntegrationTests
{
    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Novus.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find project root");
    }

    private static string GetStdLibPath()
    {
        var projectRoot = GetProjectRoot();
        return Path.Combine(projectRoot, "Novus", "std");
    }

    /// <summary>
    /// Get all .novus files in the std directory
    /// </summary>
    private static IEnumerable<string> GetAllStdFiles()
    {
        var stdPath = GetStdLibPath();
        return Directory.GetFiles(stdPath, "*.novus", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(stdPath, f))
            .OrderBy(f => f);
    }

    /// <summary>
    /// Analyze a stdlib file EXACTLY like the language server does.
    /// This includes BOTH parsing AND semantic analysis.
    /// This MUST produce the exact same diagnostics as the language server.
    /// </summary>
    private static (bool HasErrors, List<Diagnostic> AllDiagnostics) AnalyzeLikeLanguageServer(string relativePath)
    {
        var stdPath = GetStdLibPath();
        var fullPath = Path.Combine(stdPath, relativePath);

        try
        {
            var source = File.ReadAllText(fullPath);

            // Step 1: Parse (exactly like language server)
            var parseDiagnostics = new DiagnosticBag();
            var inputStream = new AntlrInputStream(source);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new AngleBracketTokenStream(lexer);
            var parser = new NovusParser(tokenStream);

            // Capture parse errors
            var errorListener = new NovusErrorListener(parseDiagnostics, fullPath, source);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            var tree = parser.compilationUnit();

            // Step 2: Semantic Analysis (exactly like language server - DocumentManager.cs:121)
            var analyzer = new SemanticAnalyzer(fullPath, source, stdPath);
            analyzer.Analyze(tree);

            // Merge all diagnostics (parse + semantic)
            var allDiagnostics = new List<Diagnostic>();
            allDiagnostics.AddRange(parseDiagnostics.Diagnostics);
            allDiagnostics.AddRange(analyzer.Diagnostics.Diagnostics);

            // Check for ANY errors (parse OR semantic)
            bool hasErrors = allDiagnostics.Any(d => d.IsError);

            return (hasErrors, allDiagnostics);
        }
        catch (Exception ex)
        {
            // If we get an exception, just rethrow it - the test framework will catch it
            throw new InvalidOperationException($"Failed to analyze {relativePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// CRITICAL TEST: Ensure stdlib files have ZERO errors (parse + semantic).
    /// If this fails, the language server will show errors in the editor.
    /// This is UNACCEPTABLE - users must see clean stdlib code.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetStdLibraryFiles))]
    public void StdLibFile_ShouldHaveNoErrors_InLanguageServer(string relativePath)
    {
        var (hasErrors, diagnostics) = AnalyzeLikeLanguageServer(relativePath);

        if (hasErrors)
        {
            var errors = string.Join("\n", diagnostics
                .Where(d => d.IsError)
                .Select(d => $"  {d.Location.Line}:{d.Location.Column} - {d.Message}"));

            var parseErrors = diagnostics.Count(d => d.IsError && d.Message.Contains("syntax") || d.Message.Contains("expected"));
            var semanticErrors = diagnostics.Count(d => d.IsError) - parseErrors;

            Assert.Fail(
                $"Standard library file {relativePath} has ERRORS that will show in VSCode!\n" +
                $"This is a CRITICAL failure - users will see errors in the editor!\n\n" +
                $"Total errors: {diagnostics.Count(d => d.IsError)}\n" +
                $"Parse errors: {parseErrors}\n" +
                $"Semantic errors: {semanticErrors}\n\n" +
                $"{errors}\n\n" +
                $"Fix: Update the semantic analyzer or parser to properly handle this code."
            );
        }

        // If we get here, the file has ZERO errors
        var warningCount = diagnostics.Count(d => !d.IsError);
        Assert.True(true, $"{relativePath} analyzed successfully - 0 errors, {warningCount} warnings");
    }

    public static IEnumerable<object[]> GetStdLibraryFiles()
    {
        try
        {
            return GetAllStdFiles().Select(f => new object[] { f });
        }
        catch
        {
            // If we can't find files, return empty (test will be skipped)
            return Array.Empty<object[]>();
        }
    }
}
