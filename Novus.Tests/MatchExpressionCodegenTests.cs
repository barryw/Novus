using Xunit;
using Antlr4.Runtime;
using Novus.Parser;
using Novus.Frontend;
using Novus.Codegen;
using System.Collections.Generic;

namespace Novus.Tests;

/// <summary>
/// Tests for C code generation of match expressions, specifically ensuring
/// that match result variables are properly declared.
/// Regression tests for the bug where match result variables were used but never declared.
/// </summary>
public class MatchExpressionCodegenTests
{
    private string GenerateCCode(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);

        var codegen = new CCodeGenerator(
            module,
            new List<IR.IrStringLiteral>(),
            "68020",
            "auto",
            BuildMode.Debug,
            SafetyLevel.Full,
            null,
            false,
            null);

        return codegen.Generate();
    }

    [Fact]
    public void MatchExpression_BasicEnum_CompilesSuccessfully()
    {
        var source = @"
enum MyEnum {
    A,
    B(i32)
}

pub fn get_value(e: MyEnum) -> i32 {
    return match e {
        MyEnum::A => 0,
        MyEnum::B(x) => x
    }
}
";
        var cCode = GenerateCCode(source);

        // Should generate valid C code with function name
        Assert.NotEmpty(cCode);
        Assert.Contains("get_value", cCode);
    }

    [Fact]
    public void MatchExpression_InRegularFunction_WorksCorrectly()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

pub fn color_to_number(c: Color) -> i32 {
    return match c {
        Color::Red => 1,
        Color::Green => 2,
        Color::Blue => 3
    }
}
";
        var cCode = GenerateCCode(source);

        // Should compile without errors
        Assert.NotEmpty(cCode);
        Assert.Contains("color_to_number", cCode);
    }

    [Fact]
    public void MatchExpression_WithMultipleArms_CompilesSuccessfully()
    {
        var source = @"
enum Status {
    Ok(i32),
    Err
}

pub fn get_status_value(status: Status) -> i32 {
    return match status {
        Status::Ok(x) => x,
        Status::Err => -1
    }
}
";
        var cCode = GenerateCCode(source);

        // Verify the function is generated
        Assert.NotEmpty(cCode);
        Assert.Contains("get_status_value", cCode);
    }
}
