using Antlr4.Runtime;
using Novus.Formatting;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class FormatterTests
{
    private (NovusParser parser, CommonTokenStream tokenStream) CreateParser(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        return (parser, tokenStream);
    }

    private string Format(string source, int indentSize = 4)
    {
        var (parser, tokenStream) = CreateParser(source);
        var tree = parser.compilationUnit();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);

        var formatter = new NovusFormatter(tokenStream, indentSize);
        return formatter.Format(tree);
    }

    // ==================== Basic Formatting Tests ====================

    [Fact]
    public void Format_SimpleFunction_ProperIndentation()
    {
        var source = "fn main() -> i32 { return 42 }";
        var result = Format(source);

        Assert.Contains("fn main() -> i32 {", result);
        Assert.Contains("    return 42", result);
        Assert.Contains("}", result);
    }

    [Fact]
    public void Format_FunctionWithParameters_ProperSpacing()
    {
        var source = "fn add(a:i32,b:i32)->i32{return a+b}";
        var result = Format(source);

        Assert.Contains("fn add(a: i32, b: i32) -> i32", result);
        Assert.Contains("return a + b", result);
    }

    [Fact]
    public void Format_MultipleStatements_EachOnOwnLine()
    {
        var source = @"fn test() -> i32 {
let x = 1
let y = 2
return x + y
}";
        var result = Format(source);

        var lines = result.Split('\n');
        Assert.Contains(lines, l => l.Trim().StartsWith("let x = 1"));
        Assert.Contains(lines, l => l.Trim().StartsWith("let y = 2"));
        Assert.Contains(lines, l => l.Trim().StartsWith("return x + y"));
    }

    // ==================== Comment Preservation Tests ====================

    [Fact]
    public void Format_LeadingComments_Preserved()
    {
        var source = @"// This is a file comment
// Second line of comment
fn main() -> i32 {
    return 0
}";
        var result = Format(source);

        Assert.Contains("// This is a file comment", result);
        Assert.Contains("// Second line of comment", result);
    }

    [Fact]
    public void Format_InlineComment_Preserved()
    {
        var source = @"fn main() -> i32 {
    let x = 42  // inline comment
    return x
}";
        var result = Format(source);

        Assert.Contains("// inline comment", result);
    }

    [Fact]
    public void Format_CommentBeforeFunction_Preserved()
    {
        var source = @"// Function documentation
fn calculate(x: i32) -> i32 {
    return x * 2
}";
        var result = Format(source);

        Assert.Contains("// Function documentation", result);
        Assert.Contains("fn calculate", result);
    }

    [Fact]
    public void Format_CommentInsideBlock_Preserved()
    {
        var source = @"fn main() -> i32 {
    // This comment explains the next line
    let result = 42
    return result
}";
        var result = Format(source);

        Assert.Contains("// This comment explains the next line", result);
    }

    [Fact]
    public void Format_BlockComment_Preserved()
    {
        var source = @"/* Multi-line
   block comment */
fn main() -> i32 {
    return 0
}";
        var result = Format(source);

        Assert.Contains("/* Multi-line", result);
        Assert.Contains("block comment */", result);
    }

    // ==================== Import Statement Tests ====================

    [Fact]
    public void Format_ImportStatement_ProperSpacing()
    {
        var source = "from std::io import println";
        var result = Format(source);

        Assert.Equal("from std::io import println\n", result);
    }

    [Fact]
    public void Format_MultipleImports_EachOnOwnLine()
    {
        var source = @"from std::io import println
from std::core import Result";
        var result = Format(source);

        Assert.Contains("from std::io import println", result);
        Assert.Contains("from std::core import Result", result);
    }

    [Fact]
    public void Format_ImportWithMultipleItems_CommaSpacing()
    {
        var source = "from std::core import Option,Result,Vec";
        var result = Format(source);

        Assert.Contains("import Option, Result, Vec", result);
    }

    // ==================== Struct Tests ====================

    [Fact]
    public void Format_StructDeclaration_ProperLayout()
    {
        var source = "struct Point { x: i32, y: i32 }";
        var result = Format(source);

        Assert.Contains("struct Point {", result);
        Assert.Contains("    x: i32,", result);
        Assert.Contains("    y: i32,", result);
        Assert.Contains("}", result);
    }

    [Fact]
    public void Format_StructWithGeneric_ProperSpacing()
    {
        var source = "struct Container<T> { value: T }";
        var result = Format(source);

        Assert.Contains("struct Container<T> {", result);
        Assert.Contains("    value: T,", result);
    }

    // ==================== Enum Tests ====================

    [Fact]
    public void Format_EnumDeclaration_ProperLayout()
    {
        var source = "enum Color { Red, Green, Blue }";
        var result = Format(source);

        Assert.Contains("enum Color {", result);
        Assert.Contains("    Red,", result);
        Assert.Contains("    Green,", result);
        Assert.Contains("    Blue,", result);
    }

    [Fact]
    public void Format_EnumWithPayload_ProperFormat()
    {
        var source = "enum Option<T> { Some(T), None }";
        var result = Format(source);

        Assert.Contains("enum Option<T> {", result);
        Assert.Contains("    Some(T),", result);
        Assert.Contains("    None,", result);
    }

    // ==================== Control Flow Tests ====================

    [Fact]
    public void Format_IfStatement_ProperBracing()
    {
        var source = "fn test() { if x > 0 { return 1 } }";
        var result = Format(source);

        Assert.Contains("if x > 0 {", result);
        Assert.Contains("    return 1", result);
    }

    [Fact]
    public void Format_IfElseStatement_ElseOnSameLine()
    {
        var source = "fn test() { if x > 0 { return 1 } else { return 0 } }";
        var result = Format(source);

        Assert.Contains("} else {", result);
    }

    [Fact]
    public void Format_IfElseIfChain_ProperLayout()
    {
        var source = "fn test(x: i32) -> i32 { if x > 0 { return 1 } else if x < 0 { return -1 } else { return 0 } }";
        var result = Format(source);

        Assert.Contains("if x > 0 {", result);
        Assert.Contains("} else if x < 0 {", result);
        Assert.Contains("} else {", result);
    }

    [Fact]
    public void Format_WhileLoop_ProperBracing()
    {
        var source = "fn test() { while x > 0 { x = x - 1 } }";
        var result = Format(source);

        Assert.Contains("while x > 0 {", result);
    }

    [Fact]
    public void Format_ForLoop_ProperLayout()
    {
        var source = "fn test() { for i in 0..10 { println(i) } }";
        var result = Format(source);

        Assert.Contains("for i in 0..10 {", result);
    }

    // ==================== Expression Tests ====================

    [Fact]
    public void Format_BinaryExpression_SpacesAroundOperator()
    {
        var source = "fn test() -> i32 { return 1+2*3 }";
        var result = Format(source);

        Assert.Contains("1 + 2 * 3", result);
    }

    [Fact]
    public void Format_ComparisonExpression_SpacesAroundOperator()
    {
        var source = "fn test() -> bool { return x==y }";
        var result = Format(source);

        Assert.Contains("x == y", result);
    }

    [Fact]
    public void Format_LogicalExpression_SpacesAroundOperator()
    {
        var source = "fn test() -> bool { return a&&b||c }";
        var result = Format(source);

        Assert.Contains("a && b || c", result);
    }

    [Fact]
    public void Format_IfExpression_ProperSpacing()
    {
        var source = "fn test(x: bool) -> i32 { return if x{1}else{0} }";
        var result = Format(source);

        Assert.Contains("if x { 1 } else { 0 }", result);
    }

    [Fact]
    public void Format_FunctionCall_NoSpaceBeforeParen()
    {
        var source = "fn test() { foo ( 1 , 2 ) }";
        var result = Format(source);

        Assert.Contains("foo(1, 2)", result);
    }

    // ==================== Match Expression Tests ====================

    [Fact]
    public void Format_MatchExpression_ProperLayout()
    {
        var source = "fn test(x: i32) -> i32 { match x { 0 => 1, 1 => 2, _ => 0 } }";
        var result = Format(source);

        Assert.Contains("match x {", result);
        Assert.Contains("    0 => 1,", result);
        Assert.Contains("    1 => 2,", result);
        Assert.Contains("    _ => 0", result);
        Assert.Contains("}", result);
    }

    [Fact]
    public void Format_MatchWithBlock_ProperLayout()
    {
        var source = @"fn test(opt: Option<i32>) -> i32 {
    match opt {
        Option::Some(x) => { return x },
        Option::None => { return 0 }
    }
}";
        var result = Format(source);

        Assert.Contains("Option::Some(x) => {", result);
        Assert.Contains("Option::None => {", result);
    }

    // ==================== Type Declaration Tests ====================

    [Fact]
    public void Format_ReferenceType_NoSpaceAfterAmpersand()
    {
        var source = "fn test(x: &i32) -> i32 { return *x }";
        var result = Format(source);

        Assert.Contains("x: &i32", result);
    }

    [Fact]
    public void Format_MutableReference_ProperSpacing()
    {
        var source = "fn test(x: &mut i32) { *x = 42 }";
        var result = Format(source);

        Assert.Contains("x: &mut i32", result);
    }

    [Fact]
    public void Format_PointerType_NoSpaceAfterStar()
    {
        var source = "fn test(x: *i32) -> i32 { return *x }";
        var result = Format(source);

        Assert.Contains("x: *i32", result);
    }

    [Fact]
    public void Format_ArrayType_ProperBrackets()
    {
        var source = "fn test(arr: [i32; 10]) { }";
        var result = Format(source);

        Assert.Contains("arr: [i32; 10]", result);
    }

    [Fact]
    public void Format_GenericType_ProperAngleBrackets()
    {
        var source = "fn test(v: Vec<i32>) { }";
        var result = Format(source);

        Assert.Contains("v: Vec<i32>", result);
    }

    // ==================== Attribute Tests ====================

    [Fact]
    public void Format_AttributeOnFunction_OwnLine()
    {
        var source = "@inline fn test() { }";
        var result = Format(source);

        var lines = result.Split('\n').Select(l => l.Trim()).ToArray();
        var attrIndex = Array.FindIndex(lines, l => l.StartsWith("@inline"));
        var fnIndex = Array.FindIndex(lines, l => l.StartsWith("fn test"));

        Assert.True(attrIndex >= 0);
        Assert.True(fnIndex >= 0);
        Assert.True(fnIndex > attrIndex);
    }

    [Fact]
    public void Format_AttributeWithArgs_ProperFormat()
    {
        var source = "@align(4) struct Data { x: i32 }";
        var result = Format(source);

        Assert.Contains("@align(4)", result);
    }

    // ==================== Impl Block Tests ====================

    [Fact]
    public void Format_ImplBlock_ProperLayout()
    {
        var source = "impl Point { fn new() -> Point { Point { x: 0, y: 0 } } }";
        var result = Format(source);

        Assert.Contains("impl Point {", result);
        Assert.Contains("    fn new() -> Point {", result);
    }

    [Fact]
    public void Format_TraitImpl_ProperLayout()
    {
        var source = "impl Display for Point { fn display(&self) { } }";
        var result = Format(source);

        Assert.Contains("impl Display for Point {", result);
    }

    // ==================== Special Statement Tests ====================

    [Fact]
    public void Format_DeferStatement_ProperFormat()
    {
        var source = "fn test() { defer { cleanup() } }";
        var result = Format(source);

        Assert.Contains("defer {", result);
    }

    [Fact]
    public void Format_UnsafeBlock_ProperFormat()
    {
        var source = "fn test() { unsafe { *ptr = 42 } }";
        var result = Format(source);

        Assert.Contains("unsafe {", result);
    }

    [Fact]
    public void Format_AssertStatement_ProperFormat()
    {
        var source = "fn test() { assert!(x > 0) }";
        var result = Format(source);

        Assert.Contains("assert!(x > 0)", result);
    }

    // ==================== Indentation Tests ====================

    [Fact]
    public void Format_NestedBlocks_CorrectIndentation()
    {
        var source = "fn test() { if true { if true { return 1 } } }";
        var result = Format(source);

        // The innermost return should be indented 3 levels (12 spaces)
        Assert.Contains("            return 1", result);
    }

    [Fact]
    public void Format_CustomIndentSize_Applied()
    {
        var source = "fn test() { return 42 }";
        var result = Format(source, indentSize: 2);

        // With 2-space indent, return should have 2 spaces
        Assert.Contains("  return 42", result);
    }

    // ==================== Blank Line Tests ====================

    [Fact]
    public void Format_MultipleFunctions_BlankLineBetween()
    {
        var source = @"fn foo() { }
fn bar() { }";
        var result = Format(source);

        // There should be a blank line between functions
        Assert.Contains("}\n\nfn bar", result.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Format_ImportsAndFunction_BlankLineBetween()
    {
        var source = @"from std::io import println
fn main() { }";
        var result = Format(source);

        Assert.Contains("println\n\nfn main", result.Replace("\r\n", "\n"));
    }

    // ==================== Struct Literal Tests ====================

    [Fact]
    public void Format_StructLiteral_ProperSpacing()
    {
        var source = "fn test() { let p = Point{x:1,y:2} }";
        var result = Format(source);

        Assert.Contains("Point { x: 1, y: 2 }", result);
    }

    // ==================== Array Literal Tests ====================

    [Fact]
    public void Format_ArrayLiteral_ProperSpacing()
    {
        var source = "fn test() { let arr = [1,2,3,4] }";
        var result = Format(source);

        Assert.Contains("[1, 2, 3, 4]", result);
    }

    [Fact]
    public void Format_ArrayRepeatLiteral_ProperFormat()
    {
        var source = "fn test() { let arr = [0;10] }";
        var result = Format(source);

        Assert.Contains("[0; 10]", result);
    }

    // ==================== Const and Static Tests ====================

    [Fact]
    public void Format_ConstDeclaration_ProperFormat()
    {
        var source = "const MAX_SIZE:i32=100";
        var result = Format(source);

        Assert.Contains("const MAX_SIZE: i32 = 100", result);
    }

    [Fact]
    public void Format_StaticDeclaration_ProperFormat()
    {
        var source = "static mut COUNTER:i32=0";
        var result = Format(source);

        Assert.Contains("static mut COUNTER: i32 = 0", result);
    }

    // ==================== Trait Tests ====================

    [Fact]
    public void Format_TraitDeclaration_ProperLayout()
    {
        var source = "trait Display { fn display(&self) }";
        var result = Format(source);

        Assert.Contains("trait Display {", result);
        Assert.Contains("    fn display(&self)", result);
    }

    // ==================== Idempotency Test ====================

    [Fact]
    public void Format_AlreadyFormatted_NoChange()
    {
        var source = @"fn main() -> i32 {
    let x = 42
    return x
}
";
        var result1 = Format(source);
        var result2 = Format(result1);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Format_ComplexFile_Idempotent()
    {
        var source = @"// File header comment
from std::io import println

// A struct for 2D points
struct Point {
    x: i32,
    y: i32,
}

impl Point {
    fn new(x: i32, y: i32) -> Point {
        Point { x: x, y: y }
    }

    fn distance(&self) -> i32 {
        return self.x * self.x + self.y * self.y
    }
}

fn main() -> i32 {
    let p = Point::new(3, 4)
    let d = p.distance()
    println(d)
    return 0
}
";
        var result1 = Format(source);
        var result2 = Format(result1);

        Assert.Equal(result1, result2);
    }
}
