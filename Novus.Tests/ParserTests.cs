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
        // With labeled alternatives, this is now a WhileExprContext
        var whileExpr = Assert.IsType<NovusParser.WhileExprContext>(whileStmt);
        Assert.NotNull(whileExpr.expression());
        Assert.NotNull(whileExpr.block());
    }

    [Fact]
    public void Parse_WhileVarStatement_Success()
    {
        var source = @"
fn test() -> i32 {
    var limit: u32 = 10
    while var i < limit {
        i++
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        var whileStmt = func.block().statement(1).whileStatement();
        Assert.NotNull(whileStmt);
        var whileVar = Assert.IsType<NovusParser.WhileVarContext>(whileStmt);
        Assert.Equal("i", whileVar.IDENTIFIER().GetText());
        Assert.NotNull(whileVar.comparisonOp());
        Assert.NotNull(whileVar.expression());
        Assert.NotNull(whileVar.block());
    }

    [Fact]
    public void Parse_WhileVarWithTypeAnnotation_Success()
    {
        var source = @"
fn test() -> i32 {
    while var i: i32 < 10 {
        i++
    }
    return 0
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        var func = tree.functionDeclaration()[0];
        var whileStmt = func.block().statement(0).whileStatement();
        Assert.NotNull(whileStmt);
        var whileVar = Assert.IsType<NovusParser.WhileVarContext>(whileStmt);
        Assert.Equal("i", whileVar.IDENTIFIER().GetText());
        Assert.NotNull(whileVar.type());
        Assert.NotNull(whileVar.comparisonOp());
        Assert.NotNull(whileVar.expression());
        Assert.NotNull(whileVar.block());
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

    // TODO: Add Parse_ForLoop_Success when RangeExpr is fully implemented

    [Fact]
    public void Parse_MatchExpression_Success()
    {
        var source = @"
fn test(opt: Option<i32>) -> i32 {
    match opt {
        Some(value) => return value,
        None => return 0,
    }
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    // TODO: Add Parse_UnsafeBlock_Success when unsafe blocks are fully implemented in parser
    // TODO: Add Parse_UsingStatement_Success when using statements are fully implemented in parser

    [Fact]
    public void Parse_EnumDeclaration_Success()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Single(tree.enumDeclaration());
    }

    [Fact]
    public void Parse_ImplBlock_Success()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

impl Point {
    pub fn new(x: i32, y: i32) -> Point {
        return Point { x: x, y: y }
    }

    pub fn distance(&self) -> i32 {
        return self.x * self.x + self.y * self.y
    }
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Single(tree.implDeclaration());
    }

    // TODO: Add Parse_GenericImplBlock_Success when generic impl syntax is fully supported

    [Fact]
    public void Parse_AttributeOnFunction_Success()
    {
        var source = @"
#[inline]
#[no_mangle]
pub fn critical_function() -> i32 {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_SelfParameter_Success()
    {
        var source = @"
impl Point {
    pub fn get_x(&self) -> i32 {
        return self.x
    }

    pub fn set_x(&var self, value: i32) {
        self.x = value
    }
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_DeferStatement_Success()
    {
        var source = @"
fn cleanup_test() -> i32 {
    let ptr = allocate(100u32)
    defer {
        free(ptr)
    }
    return process(ptr)
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
    }

    [Fact]
    public void Parse_StructDeclaration_Success()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Single(tree.structDeclaration());
    }

    [Fact]
    public void Parse_GenericStruct_Success()
    {
        var source = @"
struct Vec<T> {
    data: *T,
    len: u32,
    cap: u32
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Single(tree.structDeclaration());
    }

    // Error Recovery Tests

    [Fact]
    public void Parse_FunctionDeclarationWithSemicolon_Parsed()
    {
        // Test that function declarations with semicolon instead of block are parsed
        // (for extern functions or incomplete IDE declarations)
        var source = @"
extern fn write(format: *u8, ...args) -> i32;

fn complete_function() -> i32 {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Parser should not crash and should parse both functions
        Assert.NotNull(tree);
        Assert.Equal(2, tree.functionDeclaration().Length);
    }

    [Fact]
    public void Parse_ExternFunctionWithoutBody_Success()
    {
        var source = @"
extern fn printf(format: *u8) -> i32
extern fn malloc(size: u32) -> *u8
";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Should parse successfully since extern functions don't need bodies
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Equal(2, tree.functionDeclaration().Length);
    }

    [Fact]
    public void Parse_StructWithMissingFieldType_Parsed()
    {
        // Error recovery: struct field without type annotation
        // Parser should recover and continue parsing
        var source = @"
struct TestStruct {
    valid_field: i32,
    missing_type_field,
    another_valid: u32
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Parser should parse the struct despite the error
        Assert.NotNull(tree);
        Assert.Single(tree.structDeclaration());
        var structDecl = tree.structDeclaration()[0];
        // Should have all three fields (including the malformed one)
        Assert.Equal(3, structDecl.structField().Length);
    }

    [Fact]
    public void Parse_EmptyEnum_Parsed()
    {
        // Error recovery: enum with no variants
        var source = @"
enum EmptyEnum {
}

enum ValidEnum {
    Variant1,
    Variant2
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Parser should handle empty enum and continue
        Assert.NotNull(tree);
        Assert.Equal(2, tree.enumDeclaration().Length);
    }

    [Fact]
    public void Parse_EnumWithEmptyParens_Parsed()
    {
        // Error recovery: enum variant with empty parens (should be omitted)
        var source = @"
enum TestEnum {
    Variant1(),
    Variant2(i32),
    Variant3
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Parser should parse all variants
        Assert.NotNull(tree);
        Assert.Single(tree.enumDeclaration());
        var enumDecl = tree.enumDeclaration()[0];
        Assert.Equal(3, enumDecl.enumVariant().Length);
    }

    [Fact]
    public void Parse_VariableDeclarationMissingInitializer_Parsed()
    {
        // Error recovery: variable with type but no initializer
        var source = @"
fn test() -> i32 {
    let x: i32
    let y = 42
    return y
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Parser should parse the function and recover from missing initializer
        Assert.NotNull(tree);
        Assert.Single(tree.functionDeclaration());
    }

    [Fact]
    public void Parse_ParameterWithoutType_Parsed()
    {
        // Error recovery: parameter without type annotation
        var source = @"
fn test(a, b: i32) -> i32 {
    return b
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Parser should parse despite missing type on parameter 'a'
        Assert.NotNull(tree);
        Assert.Single(tree.functionDeclaration());
        var func = tree.functionDeclaration()[0];
        Assert.NotNull(func.parameterList());
        Assert.Equal(2, func.parameterList().parameter().Length);
    }

    [Fact]
    public void Parse_TraitMethodWithSemicolon_Parsed()
    {
        // Trait methods can have semicolon for signature-only declarations
        var source = @"
trait TestTrait {
    fn required_method(x: i32) -> i32;
    fn default_method(x: i32) -> i32 {
        return x + 1
    }
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Both method styles should parse correctly
        Assert.NotNull(tree);
        Assert.Single(tree.traitDeclaration());
    }

    [Fact]
    public void Parse_MixedValidAndErrorRecovery_ContinuesParsing()
    {
        // Test that parser can recover from errors and continue parsing valid code
        var source = @"
struct ValidStruct {
    field: i32
}

struct ErrorStruct {
    bad_field,
    good_field: u32
}

fn valid_function() -> i32 {
    return 42
}";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Parser should recover and parse all declarations
        Assert.NotNull(tree);
        Assert.Equal(2, tree.structDeclaration().Length);
        Assert.Single(tree.functionDeclaration());
    }
}
