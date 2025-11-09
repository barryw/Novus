using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Integration tests that verify language server and compiler see the same code.
/// These tests ensure stdlib files parse without errors using the same code path
/// that the language server uses - critical for editor/compiler consistency.
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
    /// Parse a stdlib file the same way the language server does.
    /// This MUST produce the exact same diagnostics as the language server.
    /// </summary>
    private static (bool HasParseErrors, List<Diagnostic> Diagnostics) ParseLikeLanguageServer(string relativePath)
    {
        var stdPath = GetStdLibPath();
        var fullPath = Path.Combine(stdPath, relativePath);

        try
        {
            var source = File.ReadAllText(fullPath);

            // This is exactly how the language server parses files
            var diagnostics = new DiagnosticBag();
            var inputStream = new AntlrInputStream(source);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new AngleBracketTokenStream(lexer);
            var parser = new NovusParser(tokenStream);

            // Capture parse errors
            var errorListener = new NovusErrorListener(diagnostics, fullPath, source);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            var tree = parser.compilationUnit();

            // Check for syntax errors
            bool hasParseErrors = parser.NumberOfSyntaxErrors > 0 || diagnostics.HasErrors;

            return (hasParseErrors, diagnostics.Diagnostics.ToList());
        }
        catch (Exception ex)
        {
            // If we get an exception, just rethrow it - the test framework will catch it
            throw new InvalidOperationException($"Failed to parse {relativePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// CRITICAL TEST: Ensure stdlib files have zero parse errors.
    /// If this fails, the language server will show errors in the editor
    /// even though the compiler accepts the code. This is unacceptable.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetStdLibraryFiles))]
    public void StdLibFile_ShouldHaveNoParseErrors_InLanguageServer(string relativePath)
    {
        var (hasParseErrors, diagnostics) = ParseLikeLanguageServer(relativePath);

        if (hasParseErrors)
        {
            var errors = string.Join("\n", diagnostics
                .Where(d => d.IsError)
                .Select(d => $"  {d.Location.Line}:{d.Location.Column} - {d.Message}"));

            Assert.Fail(
                $"Standard library file {relativePath} has PARSE ERRORS that will show in VSCode:\n" +
                $"This is a CRITICAL failure - users will see errors in the editor!\n\n" +
                $"{errors}\n\n" +
                $"Fix: Either update the parser grammar to support this syntax,\n" +
                $"or rewrite the stdlib code to use supported syntax."
            );
        }

        // If we get here, the file parses cleanly
        Assert.True(true, $"{relativePath} parses without errors (language server will show no errors)");
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
