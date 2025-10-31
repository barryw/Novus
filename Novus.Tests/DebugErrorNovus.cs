using Xunit;
using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using System.Linq;
using System.IO;

namespace Novus.Tests;

public class DebugErrorNovus
{
    [Fact]
    public void Debug_ErrorNovus()
    {
        var stdPath = "/Users/barry/RiderProjects/Novus/Novus/std";
        var fullPath = Path.Combine(stdPath, "error.novus");
        var source = File.ReadAllText(fullPath);

        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var semanticAnalyzer = new SemanticAnalyzer(fullPath, source, stdPath);
        semanticAnalyzer.Analyze(tree);

        System.Console.WriteLine($"HasErrors: {semanticAnalyzer.Diagnostics.HasErrors}");
        System.Console.WriteLine($"Diagnostic count: {semanticAnalyzer.Diagnostics.Diagnostics.Count}");

        var bareReturnErrors = semanticAnalyzer.Diagnostics.Diagnostics
            .Where(d => d.Message.Contains("bare return"))
            .ToList();

        System.Console.WriteLine($"\nBare return errors: {bareReturnErrors.Count}");
        foreach (var diag in bareReturnErrors)
        {
            System.Console.WriteLine($"  Line {diag.Location.Line}: {diag.Message}");
            System.Console.WriteLine($"    Function context: {diag.Location.SourceLine}");
        }

        System.Console.WriteLine($"\nAll errors:");
        foreach (var diag in semanticAnalyzer.Diagnostics.Diagnostics.Where(d => d.IsError))
        {
            System.Console.WriteLine($"  Line {diag.Location.Line}: {diag.Message}");
        }
    }
}
