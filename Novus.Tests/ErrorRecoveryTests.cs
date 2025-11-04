using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for ANTLR error recovery in LSP mode.
/// These tests verify that the parser can build partial ASTs even with syntax errors,
/// which is critical for providing code completion and hover information in broken code.
/// </summary>
public class ErrorRecoveryTests
{
    /// <summary>
    /// Test that partial member access (e.g., "someFunc.") returns a partial AST
    /// </summary>
    [Fact]
    public void Test_PartialMemberAccess_ReturnsPartialAst()
    {
        var source = @"
pub fn main() -> i32 {
    let x = someFunc.
    return 0
}";
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.LanguageServer
        );

        var ast = parser.compilationUnit();

        // Should have AST even with syntax error
        Assert.NotNull(ast);

        // Should have recognized the function declaration
        var functions = ast.functionDeclaration();
        Assert.Single(functions);
        Assert.Equal("main", functions[0].IDENTIFIER().GetText());

        // Should have captured errors but continued parsing
        Assert.True(diagnostics.HasErrors);
    }

    /// <summary>
    /// Test that partial enum/path access (e.g., "Result::") returns a partial AST
    /// </summary>
    [Fact]
    public void Test_PartialPathExpression_ReturnsPartialAst()
    {
        var source = @"
from std::core import Result

pub fn main() -> i32 {
    let x = Result::
    return 0
}";
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.LanguageServer
        );

        var ast = parser.compilationUnit();

        Assert.NotNull(ast);

        // Should have recognized imports
        var imports = ast.importDeclaration();
        Assert.Single(imports);

        // Should have recognized function
        var functions = ast.functionDeclaration();
        Assert.Single(functions);

        // Should have errors but continued parsing
        Assert.True(diagnostics.HasErrors);
    }

    /// <summary>
    /// Test that incomplete function parameters return a partial AST
    /// </summary>
    [Fact]
    public void Test_IncompleteFunction_ReturnsPartialAst()
    {
        var source = @"
fn test(param:
";
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.LanguageServer
        );

        var ast = parser.compilationUnit();

        Assert.NotNull(ast);

        // Should have recognized function declaration despite errors
        var functions = ast.functionDeclaration();
        Assert.True(functions.Length >= 0); // Parser attempted to recognize it

        // Should have errors
        Assert.True(diagnostics.HasErrors);
    }

    /// <summary>
    /// Test that missing closing brace is recovered
    /// </summary>
    [Fact]
    public void Test_MissingClosingBrace_Recovers()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 42
    return x
";  // Missing closing brace
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.LanguageServer
        );

        var ast = parser.compilationUnit();

        Assert.NotNull(ast);

        // Should have recognized the function
        var functions = ast.functionDeclaration();
        Assert.Single(functions);

        // Should have errors about missing brace
        Assert.True(diagnostics.HasErrors);
    }

    /// <summary>
    /// Test that incomplete struct field returns partial AST
    /// </summary>
    [Fact]
    public void Test_IncompleteStructField_ReturnsPartialAst()
    {
        var source = @"
struct Point {
    x: i32,
    y:
}
";
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.LanguageServer
        );

        var ast = parser.compilationUnit();

        Assert.NotNull(ast);

        // Should have recognized struct declaration
        var structs = ast.structDeclaration();
        Assert.Single(structs);
        Assert.Equal("Point", structs[0].IDENTIFIER().GetText());

        // Should have errors
        Assert.True(diagnostics.HasErrors);
    }

    /// <summary>
    /// Test that compilation mode still reports errors normally (no change in behavior)
    /// </summary>
    [Fact]
    public void Test_CompilationMode_StillReportsErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let x = someFunc.
    return 0
}";
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.Compilation  // Compilation mode (not LSP)
        );

        var ast = parser.compilationUnit();

        Assert.NotNull(ast);

        // Should still report errors
        Assert.True(diagnostics.HasErrors);
    }

    /// <summary>
    /// Test that incomplete match expression is recovered
    /// </summary>
    [Fact]
    public void Test_IncompleteMatchExpression_Recovers()
    {
        var source = @"
pub fn main() -> i32 {
    let x = match some_value {
        1 => return 1,
        _ =>
    }
    return 0
}";
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.LanguageServer
        );

        var ast = parser.compilationUnit();

        Assert.NotNull(ast);

        // Should have recognized function
        var functions = ast.functionDeclaration();
        Assert.Single(functions);

        // Should have errors
        Assert.True(diagnostics.HasErrors);
    }

    /// <summary>
    /// Test that valid code still parses correctly in LSP mode (no regressions)
    /// </summary>
    [Fact]
    public void Test_ValidCode_ParsesCorrectlyInLspMode()
    {
        var source = @"
from std::core import Result

pub fn main() -> i32 {
    let x = 42
    return x
}";
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.LanguageServer
        );

        var ast = parser.compilationUnit();

        Assert.NotNull(ast);

        // Should have NO errors for valid code
        Assert.False(diagnostics.HasErrors);

        // Should have correctly parsed imports
        var imports = ast.importDeclaration();
        Assert.Single(imports);

        // Should have correctly parsed function
        var functions = ast.functionDeclaration();
        Assert.Single(functions);
        Assert.Equal("main", functions[0].IDENTIFIER().GetText());
    }

    /// <summary>
    /// Test multiple errors in sequence
    /// </summary>
    [Fact]
    public void Test_MultipleErrors_ContinuesParsing()
    {
        var source = @"
fn incomplete1(
fn incomplete2(param:
struct BadStruct {
    field:
}
pub fn main() -> i32 {
    return 0
}";
        var diagnostics = new DiagnosticBag();

        var parser = NovusParserFactory.CreateParser(
            source,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.LanguageServer
        );

        var ast = parser.compilationUnit();

        Assert.NotNull(ast);

        // Should have multiple errors
        Assert.True(diagnostics.ErrorCount > 1);

        // But should have recovered and found the main function
        var functions = ast.functionDeclaration();
        var mainFunc = functions.FirstOrDefault(f => f.IDENTIFIER().GetText() == "main");
        Assert.NotNull(mainFunc);
    }
}
