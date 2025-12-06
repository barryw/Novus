using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for parsing @test attributes with various parameters
/// </summary>
public class TestAttributeParsingTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    [Fact]
    public void TestAttribute_Basic_IsDiscovered()
    {
        var source = @"
@test
pub fn test_example() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Single(testFunctions);
        Assert.Equal("test_example", testFunctions[0].Name);
    }

    [Fact]
    public void TestAttribute_WithDescription_IsParsed()
    {
        var source = @"
@test(""My test description"")
pub fn test_example() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Single(testFunctions);
        var testAttr = testFunctions[0].Attributes?.Get(KnownAttributes.Test);
        Assert.NotNull(testAttr);

        var desc = testAttr.GetPositionalArg<string>(0);
        Assert.Equal("My test description", desc);
    }

    [Fact]
    public void TestAttribute_ShouldPanic_Flag_IsParsed()
    {
        var source = @"
@test(should_panic)
pub fn test_panic_expected() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Single(testFunctions);
        var testAttr = testFunctions[0].Attributes?.Get(KnownAttributes.Test);
        Assert.NotNull(testAttr);
        Assert.True(testAttr.HasFlag("should_panic"));
    }

    [Fact]
    public void TestAttribute_ShouldPanic_WithMessage_IsParsed()
    {
        var source = @"
@test(should_panic = ""expected error"")
pub fn test_panic_expected() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Single(testFunctions);
        var testAttr = testFunctions[0].Attributes?.Get(KnownAttributes.Test);
        Assert.NotNull(testAttr);
        Assert.True(testAttr.HasArg("should_panic"));
        Assert.Equal("expected error", testAttr.GetString("should_panic"));
    }

    [Fact]
    public void TestAttribute_Skip_Flag_IsParsed()
    {
        var source = @"
@test(skip)
pub fn test_skipped() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Single(testFunctions);
        var testAttr = testFunctions[0].Attributes?.Get(KnownAttributes.Test);
        Assert.NotNull(testAttr);
        Assert.True(testAttr.HasFlag("skip"));
    }

    [Fact]
    public void TestAttribute_Skip_WithReason_IsParsed()
    {
        var source = @"
@test(skip = ""Not implemented yet"")
pub fn test_skipped() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Single(testFunctions);
        var testAttr = testFunctions[0].Attributes?.Get(KnownAttributes.Test);
        Assert.NotNull(testAttr);
        Assert.True(testAttr.HasArg("skip"));
        Assert.Equal("Not implemented yet", testAttr.GetString("skip"));
    }

    [Fact]
    public void TestAttribute_DescriptionAndShouldPanic_BothParsed()
    {
        // @test("Description", should_panic)
        var source = @"
@test(""Test description"", should_panic)
pub fn test_with_both() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Single(testFunctions);
        var testAttr = testFunctions[0].Attributes?.Get(KnownAttributes.Test);
        Assert.NotNull(testAttr);

        // First positional arg is description
        Assert.Equal("Test description", testAttr.GetPositionalArg<string>(0));
        // should_panic is a flag
        Assert.True(testAttr.HasFlag("should_panic"));
    }

    [Fact]
    public void TestAttribute_MultipleTestFunctions_AllDiscovered()
    {
        var source = @"
@test
pub fn test_one() {
}

@test
pub fn test_two() {
}

@test(skip)
pub fn test_three() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Equal(3, testFunctions.Count);
    }

    [Fact]
    public void NonTestFunction_NotDiscovered()
    {
        var source = @"
pub fn not_a_test() {
}

@test
pub fn test_one() {
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Single(testFunctions);
        Assert.Equal("test_one", testFunctions[0].Name);
    }

    [Fact]
    public void GetTestFunctions_EmptyModule_ReturnsEmptyList()
    {
        var source = @"
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        var testFunctions = module.GetTestFunctions();

        Assert.Empty(testFunctions);
    }
}
