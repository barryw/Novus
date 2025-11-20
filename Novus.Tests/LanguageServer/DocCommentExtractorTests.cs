using Novus.LanguageServer;
using Xunit;

namespace Novus.Tests.LanguageServer;

/// <summary>
/// Tests for the DocCommentExtractor utility
/// Tests extraction of /// documentation comments from source code
/// </summary>
public class DocCommentExtractorTests
{
    [Fact]
    public void ExtractDocComment_SingleLineComment_ExtractsCorrectly()
    {
        var source = @"
/// This is a test function
fn test() {}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 3);

        Assert.NotNull(result);
        Assert.Equal("This is a test function", result);
    }

    [Fact]
    public void ExtractDocComment_MultiLineComment_ExtractsCorrectly()
    {
        var source = @"
/// First line
/// Second line
/// Third line
fn test() {}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 5);

        Assert.NotNull(result);
        var lines = result.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("First line", lines[0]);
        Assert.Contains("Second line", lines[1]);
        Assert.Contains("Third line", lines[2]);
    }

    [Fact]
    public void ExtractDocComment_NoComment_ReturnsNull()
    {
        var source = @"
fn test() {}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 2);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractDocComment_BlankLinesInComment_Continues()
    {
        var source = @"
/// First line

/// Second line after blank
fn test() {}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 5);

        Assert.NotNull(result);
        Assert.Contains("First line", result);
        Assert.Contains("Second line after blank", result);
    }

    [Fact]
    public void ExtractDocComment_NonDocComment_StopsExtraction()
    {
        var source = @"
// Regular comment (not doc comment)
/// Doc comment
fn test() {}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 4);

        Assert.NotNull(result);
        Assert.Equal("Doc comment", result);
        Assert.DoesNotContain("Regular comment", result);
    }

    [Fact]
    public void ExtractDocComment_InvalidLineNumber_ReturnsNull()
    {
        var source = "fn test() {}";

        var resultNegative = DocCommentExtractor.ExtractDocComment(source, -1);
        var resultZero = DocCommentExtractor.ExtractDocComment(source, 0);
        var resultTooHigh = DocCommentExtractor.ExtractDocComment(source, 100);

        Assert.Null(resultNegative);
        Assert.Null(resultZero);
        Assert.Null(resultTooHigh);
    }

    [Fact]
    public void ExtractDocComment_WithLeadingSpaces_TrimsCorrectly()
    {
        var source = @"
///    Text with leading spaces
fn test() {}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 3);

        Assert.NotNull(result);
        Assert.Equal("Text with leading spaces", result);
    }

    [Fact]
    public void ExtractDocComment_EmptyDocComment_ReturnsEmpty()
    {
        var source = @"
///
fn test() {}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 3);

        Assert.NotNull(result);
        Assert.Equal("", result);
    }

    [Fact]
    public void ExtractSummary_NullInput_ReturnsNull()
    {
        var result = DocCommentExtractor.ExtractSummary(null);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractSummary_EmptyInput_ReturnsNull()
    {
        var result = DocCommentExtractor.ExtractSummary("");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractSummary_WhitespaceInput_ReturnsNull()
    {
        var result = DocCommentExtractor.ExtractSummary("   \n  \t  ");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractSummary_SingleLine_ReturnsSameLine()
    {
        var docComment = "This is a summary";
        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.Equal("This is a summary", result);
    }

    [Fact]
    public void ExtractSummary_MultipleLines_CombinesIntoSingleLine()
    {
        var docComment = @"First line
Second line
Third line";
        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.Equal("First line Second line Third line", result);
    }

    [Fact]
    public void ExtractSummary_StopsAtBlankLine()
    {
        var docComment = @"First paragraph line 1
First paragraph line 2

Second paragraph should not be included";
        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.Equal("First paragraph line 1 First paragraph line 2", result);
        Assert.DoesNotContain("Second paragraph", result);
    }

    [Fact]
    public void ExtractSummary_StopsAtExampleSection()
    {
        var docComment = @"Summary line

Example:
some_code()";
        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.Equal("Summary line", result);
        Assert.DoesNotContain("Example", result);
    }

    [Fact]
    public void ExtractSummary_StopsAtReturnsSection()
    {
        var docComment = @"Summary of function

Returns:
The result value";
        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.Equal("Summary of function", result);
        Assert.DoesNotContain("Returns", result);
    }

    [Fact]
    public void ExtractSummary_StopsAtParametersSection()
    {
        var docComment = @"Summary

Parameters:
- x: first param";
        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.Equal("Summary", result);
        Assert.DoesNotContain("Parameters", result);
    }

    [Fact]
    public void ExtractSummary_StopsAtNoteSection()
    {
        var docComment = @"Summary text

Note: Important information";
        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.Equal("Summary text", result);
        Assert.DoesNotContain("Note:", result);
    }

    [Fact]
    public void ExtractSummary_StopsAtHeaderMarker()
    {
        var docComment = @"Summary text

# Details Section
More details here";
        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.Equal("Summary text", result);
        Assert.DoesNotContain("Details", result);
    }

    [Fact]
    public void ExtractParameterDoc_ColonSyntax_Extracts()
    {
        var docComment = @"
Function description
- x: The X coordinate
- y: The Y coordinate";

        var resultX = DocCommentExtractor.ExtractParameterDoc(docComment, "x");
        var resultY = DocCommentExtractor.ExtractParameterDoc(docComment, "y");

        Assert.Equal("The X coordinate", resultX);
        Assert.Equal("The Y coordinate", resultY);
    }

    [Fact]
    public void ExtractParameterDoc_DashSyntax_Extracts()
    {
        var docComment = @"
- count - The number of items
- offset - The starting position";

        var resultCount = DocCommentExtractor.ExtractParameterDoc(docComment, "count");
        var resultOffset = DocCommentExtractor.ExtractParameterDoc(docComment, "offset");

        Assert.Equal("The number of items", resultCount);
        Assert.Equal("The starting position", resultOffset);
    }

    [Fact]
    public void ExtractParameterDoc_AtParamSyntax_Extracts()
    {
        var docComment = @"
@param value the input value
@param default the default to use";

        var resultValue = DocCommentExtractor.ExtractParameterDoc(docComment, "value");
        var resultDefault = DocCommentExtractor.ExtractParameterDoc(docComment, "default");

        Assert.Equal("the input value", resultValue);
        Assert.Equal("the default to use", resultDefault);
    }

    [Fact]
    public void ExtractParameterDoc_NotFound_ReturnsNull()
    {
        var docComment = @"
- x: The X coordinate
- y: The Y coordinate";

        var result = DocCommentExtractor.ExtractParameterDoc(docComment, "z");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractParameterDoc_NullInput_ReturnsNull()
    {
        var result = DocCommentExtractor.ExtractParameterDoc(null, "param");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractParameterDoc_EmptyInput_ReturnsNull()
    {
        var result = DocCommentExtractor.ExtractParameterDoc("", "param");
        Assert.Null(result);
    }

    [Fact]
    public void HasDocComments_WithComments_ReturnsTrue()
    {
        var source = @"
/// This is a doc comment
fn test() {}
";
        Assert.True(DocCommentExtractor.HasDocComments(source));
    }

    [Fact]
    public void HasDocComments_WithoutComments_ReturnsFalse()
    {
        var source = @"
// Regular comment
fn test() {}
";
        Assert.False(DocCommentExtractor.HasDocComments(source));
    }

    [Fact]
    public void HasDocComments_EmptySource_ReturnsFalse()
    {
        Assert.False(DocCommentExtractor.HasDocComments(""));
    }

    [Fact]
    public void ExtractDocComment_ComplexExample_ExtractsCorrectly()
    {
        var source = @"
/// Calculates the Fibonacci number at position n
///
/// This function uses recursion to compute Fibonacci numbers.
/// For production use, consider using memoization.
///
/// # Examples
/// ```
/// let fib5 = fibonacci(5);
/// ```
fn fibonacci(n: i32) -> i32 {
    if n <= 1 { return n; }
    return fibonacci(n - 1) + fibonacci(n - 2);
}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 11);

        Assert.NotNull(result);
        Assert.Contains("Calculates the Fibonacci number", result);
        Assert.Contains("recursion", result);
        Assert.Contains("Examples", result);
        Assert.Contains("fibonacci(5)", result);
    }

    [Fact]
    public void ExtractSummary_ComplexExample_ExtractsFirstParagraph()
    {
        var docComment = @"Calculates the Fibonacci number at position n

This function uses recursion to compute Fibonacci numbers.
For production use, consider using memoization.

# Examples";

        var result = DocCommentExtractor.ExtractSummary(docComment);

        Assert.NotNull(result);
        Assert.Equal("Calculates the Fibonacci number at position n", result);
        Assert.DoesNotContain("recursion", result);
        Assert.DoesNotContain("Examples", result);
    }

    [Fact]
    public void ExtractDocComment_StructWithFields_ExtractsCorrectly()
    {
        var source = @"
/// Represents a 2D point in space
/// with integer coordinates
pub struct Point {
    x: i32,
    y: i32
}
";
        var result = DocCommentExtractor.ExtractDocComment(source, 4);

        Assert.NotNull(result);
        Assert.Contains("Represents a 2D point", result);
        Assert.Contains("integer coordinates", result);
    }
}
