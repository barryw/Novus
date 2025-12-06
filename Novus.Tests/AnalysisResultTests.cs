using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Tests;

/// <summary>
/// Tests for AnalysisResult and the semantic analysis to IR building pipeline.
/// </summary>
public class AnalysisResultTests
{
    [Fact]
    public void AnalysisResult_CreatesFromSemanticAnalyzer()
    {
        var source = @"
fn main() {
    let x: i32 = 42
}
";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var parseResult = CompilerTestHelper.Parse(source);
        analyzer.Analyze(parseResult);

        var result = analyzer.GetResult();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Contains(result.Functions.Keys, k => k == "main");
    }

    [Fact]
    public void AnalysisResult_ContainsStructDefinitions()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn main() {
    let p = Point { x: 1, y: 2 }
}
";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var parseResult = CompilerTestHelper.Parse(source);
        analyzer.Analyze(parseResult);

        var result = analyzer.GetResult();

        Assert.Contains(result.Structs.Keys, k => k == "Point");
        var point = result.Structs["Point"];
        Assert.Equal(2, point.Fields.Count);
    }

    [Fact]
    public void AnalysisResult_ContainsEnumDefinitions()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

fn main() {
    let c = Color::Red
}
";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var parseResult = CompilerTestHelper.Parse(source);
        analyzer.Analyze(parseResult);

        var result = analyzer.GetResult();

        Assert.Contains(result.Enums.Keys, k => k == "Color");
        var color = result.Enums["Color"];
        Assert.Equal(3, color.Variants.Count);
    }

    [Fact]
    public void AnalysisResult_ContainsConstants()
    {
        var source = @"
const MAX_SIZE: i32 = 100

fn main() {
    let x = MAX_SIZE
}
";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var parseResult = CompilerTestHelper.Parse(source);
        analyzer.Analyze(parseResult);

        var result = analyzer.GetResult();

        Assert.Contains(result.Constants.Keys, k => k == "MAX_SIZE");
    }

    [Fact]
    public void AnalysisResult_FailureOnTypeError()
    {
        var source = @"
fn main() {
    let x: i32 = ""not an integer""
}
";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var parseResult = CompilerTestHelper.Parse(source);
        analyzer.Analyze(parseResult);

        var result = analyzer.GetResult();

        Assert.False(result.Success);
        Assert.True(result.Diagnostics.HasErrors);
    }

    [Fact]
    public void AnalysisResult_HasTypeInterner()
    {
        var source = @"
fn main() {
    let x: i32 = 42
}
";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var parseResult = CompilerTestHelper.Parse(source);
        analyzer.Analyze(parseResult);

        var result = analyzer.GetResult();

        Assert.NotNull(result.TypeInterner);
    }

    [Fact]
    public void AnalysisResult_HasTraitResolver()
    {
        var source = @"
fn main() {
    let x: i32 = 42
}
";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var parseResult = CompilerTestHelper.Parse(source);
        analyzer.Analyze(parseResult);

        var result = analyzer.GetResult();

        Assert.NotNull(result.TraitResolver);
    }

    [Fact]
    public void AnalysisResult_ContainsSourceInfo()
    {
        var source = @"
fn main() {
    let x: i32 = 42
}
";
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var parseResult = CompilerTestHelper.Parse(source);
        analyzer.Analyze(parseResult);

        var result = analyzer.GetResult();

        Assert.Equal("test.novus", result.FilePath);
        Assert.Equal(source, result.SourceCode);
    }
}

/// <summary>
/// Helper class for parsing source code in tests.
/// </summary>
internal static class CompilerTestHelper
{
    public static Novus.Parser.NovusParser.CompilationUnitContext Parse(string source)
    {
        var inputStream = new Antlr4.Runtime.AntlrInputStream(source);
        var lexer = new Novus.Parser.NovusLexer(inputStream);
        var tokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
        var parser = new Novus.Parser.NovusParser(tokenStream);
        return parser.compilationUnit();
    }
}
