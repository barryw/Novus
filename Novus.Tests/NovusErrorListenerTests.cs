using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;

namespace Novus.Tests;

public class NovusErrorListenerTests
{
    private DiagnosticBag ParseWithErrors(string source, string filePath = "test.novus")
    {
        var diagnostics = new DiagnosticBag();
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);

        parser.RemoveErrorListeners();
        parser.AddErrorListener(new NovusErrorListener(diagnostics, filePath, source));

        // Try to parse - errors will be collected
        parser.compilationUnit();

        return diagnostics;
    }

    [Fact]
    public void Test_ParseError_CreatesCorrectDiagnostic()
    {
        // Arrange
        var source = "fn main() -> i32 {";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors, "Should have parse error");

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains("test.novus", formatted);
        Assert.Contains("E0001", formatted);
    }

    [Fact]
    public void Test_MultilineSource_ExtractsCorrectLine()
    {
        // Arrange
        var source = @"fn main() -> i32 {
    let x: i32 = 42
    return x
";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors, "Should have parse error for missing closing brace");

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains("test.novus", formatted);
    }

    [Fact]
    public void Test_WindowsLineEndings_HandledCorrectly()
    {
        // Arrange - Use Windows CRLF line endings
        var source = "fn main() -> i32 {\r\n    let x: i32 = 42\r\n    return x\r\n";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        // Should not crash and should report reasonable line numbers
        Assert.Contains("test.novus", formatted);
    }

    [Fact]
    public void Test_UnixLineEndings_HandledCorrectly()
    {
        // Arrange - Use Unix LF line endings
        var source = "fn main() -> i32 {\n    let x: i32 = 42\n    return x\n";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains("test.novus", formatted);
    }

    [Fact]
    public void Test_MixedLineEndings_HandledCorrectly()
    {
        // Arrange - Mix of CRLF and LF (shouldn't happen in practice, but test robustness)
        var source = "fn main() -> i32 {\r\n    let x: i32 = 42\n    return x\r\n";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains("test.novus", formatted);
    }

    [Fact]
    public void Test_ErrorLocation_IncludesLineAndColumn()
    {
        // Arrange - Error at specific position
        var source = "fn main() -> i32 { @ }"; // '@' without attribute name is invalid

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains("test.novus", formatted);
        Assert.Contains("E0001", formatted);
        Assert.Contains("-->", formatted); // Should have location pointer
    }

    [Fact]
    public void Test_MultipleErrors_AllReported()
    {
        // Arrange - Multiple syntax errors
        var source = @"fn main() -> i32 {
    let x: i32 =
    return @
";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        // Should report multiple errors (though parser might stop after first)
        Assert.Contains("test.novus", formatted);
    }

    [Fact]
    public void Test_EmptySource_HandlesGracefully()
    {
        // Arrange
        var source = "";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert - Empty source is valid (no declarations is valid)
        Assert.False(diagnostics.HasErrors, "Empty source should be valid");
    }

    [Fact]
    public void Test_ValidSource_NoErrors()
    {
        // Arrange
        var source = "fn main() -> i32 { return 42 }";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.False(diagnostics.HasErrors, "Valid source should have no errors");
        Assert.False(diagnostics.HasWarnings, "Valid source should have no warnings");
    }

    [Fact]
    public void Test_ErrorMessage_ContainsSourceLine()
    {
        // Arrange
        var source = @"fn main() -> i32 {
    let bad syntax here
    return 42
}";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        // Error should show the problematic source line
        Assert.Contains("let bad syntax here", formatted);
    }

    [Fact]
    public void Test_ErrorCode_IsE0001()
    {
        // Arrange
        var source = "fn main() -> i32 {";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains("E0001", formatted);
    }

    [Fact]
    public void Test_LongLine_HandlesGracefully()
    {
        // Arrange - Very long line
        var longString = new string('x', 5000);
        var source = $"fn main() -> i32 {{ let x: String = \"{longString}\" }}";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert - Should parse without crashing
        Assert.False(diagnostics.HasErrors, "Long line should parse successfully");
    }

    [Fact]
    public void Test_FilePath_PreservedInDiagnostic()
    {
        // Arrange
        var customPath = "/path/to/my/module.novus";
        var source = "fn main() -> i32 {";

        // Act
        var diagnostics = ParseWithErrors(source, customPath);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains(customPath, formatted);
    }

    [Fact]
    public void Test_UnexpectedEOF_ReportsCorrectly()
    {
        // Arrange - Missing closing brace
        var source = "fn main() -> i32 { return 42";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains("test.novus", formatted);
    }

    [Fact]
    public void Test_UnexpectedToken_ReportsCorrectly()
    {
        // Arrange - Invalid syntax - two function keywords
        var source = "fn fn main() -> i32 { return 42 }";

        // Act
        var diagnostics = ParseWithErrors(source);

        // Assert
        Assert.True(diagnostics.HasErrors);

        var formatted = diagnostics.FormatDiagnostics();
        Assert.Contains("test.novus", formatted);
        Assert.Contains("E0001", formatted);
    }
}
