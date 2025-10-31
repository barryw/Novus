using Antlr4.Runtime;
using Novus.Frontend;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class ParserTests
{
    private NovusParser CreateParser(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        return new NovusParser(tokenStream);
    }

    [Fact]
    public void Parse_SimpleFunction_Success()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.NotNull(tree);
        Assert.Single(tree.functionDeclaration());
    }

    [Fact]
    public void Parse_FunctionWithParameters_Success()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        Assert.Equal("add", func.IDENTIFIER().GetText());
        Assert.NotNull(func.parameterList());
        Assert.Equal(2, func.parameterList().parameter().Length);
    }

    [Fact]
    public void Parse_ArithmeticExpression_Success()
    {
        var source = @"
fn calc() -> u32 {
    return (10 + 20) * 2
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.NotNull(tree);
    }

    [Fact]
    public void Parse_IntegerLiteralWithSuffix_Success()
    {
        var source = @"
fn test() -> u16 {
    return 42u16
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_MultipleTypeSizes_Success()
    {
        var testCases = new[]
        {
            ("u8", "255u8"),
            ("u16", "65535u16"),
            ("u32", "4294967295u32"),
            ("i8", "127i8"),
            ("i16", "32767i16"),
            ("i32", "2147483647i32")
        };

        foreach (var (returnType, literal) in testCases)
        {
            var source = $@"
fn test() -> {returnType} {{
    return {literal}
}}";
            var parser = CreateParser(source);
            var tree = parser.compilationUnit();

            Assert.Equal(0, parser.NumberOfSyntaxErrors);
        }
    }

    [Fact]
    public void Parse_LetStatement_Success()
    {
        var source = @"
fn test() -> u32 {
    let x = 42
    return x
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_LetStatementWithType_Success()
    {
        var source = @"
fn test() -> u32 {
    let x: u32 = 42
    return x
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_LineComment_Success()
    {
        var source = @"
// This is a comment
fn main() -> u32 {
    // Another comment
    return 42  // Inline comment
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_BlockComment_Success()
    {
        var source = @"
/* Multi-line
   comment */
fn main() -> u32 {
    /* Inline block comment */ return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_AllArithmeticOperators_Success()
    {
        var operators = new[] { "+", "-", "*", "/", "%" };

        foreach (var op in operators)
        {
            var source = $@"
fn test() -> u32 {{
    return 10 {op} 5
}}";
            var parser = CreateParser(source);
            var tree = parser.compilationUnit();

            Assert.Equal(0, parser.NumberOfSyntaxErrors);
        }
    }

    [Fact]
    public void Parse_ComparisonOperators_Success()
    {
        var operators = new[] { "==", "!=", "<", ">", "<=", ">=" };

        foreach (var op in operators)
        {
            var source = $@"
fn test() -> u32 {{
    return 10 {op} 5
}}";
            var parser = CreateParser(source);
            var tree = parser.compilationUnit();

            Assert.Equal(0, parser.NumberOfSyntaxErrors);
        }
    }

    [Fact]
    public void Parse_NestedExpressions_Success()
    {
        var source = @"
fn test() -> u32 {
    return ((10 + 5) * (20 - 3)) / 2
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_MultipleFunctions_Success()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn multiply(a: i32, b: i32) -> i32 {
    return a * b
}

fn main() -> u32 {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Equal(3, tree.functionDeclaration().Length);
    }

    [Fact]
    public void Parse_VoidReturnType_Success()
    {
        var source = @"
fn test() {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        Assert.Null(func.type());
    }

    [Fact]
    public void Parse_UnderscoredLiteral_Success()
    {
        var source = @"
fn test() -> u32 {
    return 1_000_000
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_UnderscoredLiteralWithTypeSuffix_Success()
    {
        var source = @"
fn test() -> u32 {
    return 1_000_000u32
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_MultipleUnderscoresInLiteral_Success()
    {
        var source = @"
fn test() -> u64 {
    return 1_234_567_890u64
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_BinaryLiteral_Success()
    {
        var source = @"
fn test() -> u8 {
    return %11111111
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_HexLiteral_Success()
    {
        var source = @"
fn test() -> u8 {
    return $FF
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_BinaryLiteralWithUnderscore_Success()
    {
        var source = @"
fn test() -> u16 {
    return %1111_1111_0000_0000u16
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_HexLiteralWithUnderscore_Success()
    {
        var source = @"
fn test() -> u32 {
    return $DEAD_BEEFu32
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_NegativeBinaryLiteral_Success()
    {
        var source = @"
fn test() -> i32 {
    return -%1010
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_NegativeHexLiteral_Success()
    {
        var source = @"
fn test() -> i32 {
    return -$FF
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_CastExpression_Success()
    {
        var source = @"
fn test() -> u16 {
    return (u16)$DEAD_BEEF
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_CastWithDecimal_Success()
    {
        var source = @"
fn test() -> u8 {
    return (u8)255
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_NestedCast_Success()
    {
        var source = @"
fn test() -> u8 {
    return (u8)((u16)1000)
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_InvalidSyntax_ReportsErrors()
    {
        var source = @"
fn test() -> u32
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.True(parser.NumberOfSyntaxErrors > 0);
    }

    // Control Flow Tests

    [Fact]
    public void Parse_IfStatement_Success()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 1
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        var block = func.block();
        Assert.NotNull(block.statement(0).ifStatement());
    }

    [Fact]
    public void Parse_IfElseStatement_Success()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    } else {
        return 200
    }
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        var ifStmt = func.block().statement(0).ifStatement();
        Assert.NotNull(ifStmt);
        Assert.NotNull(ifStmt.ifCondition());
        Assert.Equal(2, ifStmt.block().Length); // then and else blocks
    }

    [Fact]
    public void Parse_IfElseIfStatement_Success()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 1
    } else if x > 5 {
        return 2
    } else {
        return 3
    }
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        var ifStmt = func.block().statement(0).ifStatement();
        Assert.NotNull(ifStmt);
        // else if is represented as nested ifStatement
        Assert.NotNull(ifStmt.GetChild(4)); // else part exists
    }

    [Fact]
    public void Parse_NestedIfStatement_Success()
    {
        var source = @"
fn test(x: i32, y: i32) -> i32 {
    if x > 0 {
        if y > 0 {
            return 1
        }
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_WhileStatement_Success()
    {
        var source = @"
fn test() -> i32 {
    while 1 == 1 {
        return 42
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        var whileStmt = func.block().statement(0).whileStatement();
        Assert.NotNull(whileStmt);
        Assert.NotNull(whileStmt.expression());
        Assert.NotNull(whileStmt.block());
    }

    [Fact]
    public void Parse_ForeverStatement_Success()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        var foreverStmt = func.block().statement(0).foreverStatement();
        Assert.NotNull(foreverStmt);
        Assert.NotNull(foreverStmt.block());
    }

    [Fact]
    public void Parse_BreakStatement_Success()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        var foreverStmt = func.block().statement(0).foreverStatement();
        var breakStmt = foreverStmt.block().statement(0).breakStatement();
        Assert.NotNull(breakStmt);
    }

    [Fact]
    public void Parse_WhileWithBreak_Success()
    {
        var source = @"
fn test() -> i32 {
    while 1 == 1 {
        break
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_AllComparisonOperatorsInConditions_Success()
    {
        var operators = new[] { "==", "!=", "<", ">", "<=", ">=" };

        foreach (var op in operators)
        {
            var source = $@"
fn test(x: i32) -> i32 {{
    if x {op} 10 {{
        return 1
    }}
    return 0
}}";
            var parser = CreateParser(source);
            var tree = parser.compilationUnit();

            Assert.Equal(0, parser.NumberOfSyntaxErrors);
        }
    }

    [Fact]
    public void Parse_ComplexCondition_Success()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if (x + 5) > 10 {
        return 1
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_NestedLoops_Success()
    {
        var source = @"
fn test() -> i32 {
    while 1 == 1 {
        forever {
            break
        }
        break
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    // Visibility Tests

    [Fact]
    public void Parse_PublicFunction_Success()
    {
        var source = @"
pub fn exported() -> u32 {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        Assert.Equal("pub", func.GetChild(0)?.GetText());
    }

    [Fact]
    public void Parse_PrivateFunction_Success()
    {
        var source = @"
fn helper() -> u32 {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        Assert.Equal("fn", func.GetChild(0)?.GetText());
    }

    [Fact]
    public void Parse_MixedVisibility_Success()
    {
        var source = @"
pub fn public_api() -> u32 {
    return 42
}

fn helper() -> u32 {
    return 10
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Equal(2, tree.functionDeclaration().Length);
        Assert.Equal("pub", tree.functionDeclaration()[0].GetChild(0)?.GetText());
        Assert.Equal("fn", tree.functionDeclaration()[1].GetChild(0)?.GetText());
    }

    [Fact]
    public void Parse_FunctionCallNoArguments_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return helper()
}

pub fn helper() -> u32 {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Equal(2, tree.functionDeclaration().Length);
    }

    [Fact]
    public void Parse_FunctionCallWithOneArgument_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return double(21)
}

pub fn double(x: u32) -> u32 {
    return x + x
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_FunctionCallWithMultipleArguments_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return add(10, 32)
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_FunctionCallInExpression_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return add(10, 20) + 12
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_NestedFunctionCalls_Success()
    {
        var source = @"
pub fn main() -> u32 {
    return add(double(5), 10)
}

pub fn add(a: u32, b: u32) -> u32 {
    return a + b
}

pub fn double(x: u32) -> u32 {
    return x + x
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Equal(3, tree.functionDeclaration().Length);
    }
}
