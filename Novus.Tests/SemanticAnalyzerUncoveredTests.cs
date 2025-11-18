using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for previously uncovered SemanticAnalyzer code paths.
/// Focuses on features with low/no test coverage.
/// </summary>
public class SemanticAnalyzerUncoveredTests
{
    private DiagnosticBag Analyze(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);
        return analyzer.Diagnostics;
    }

    #region Assert Statement Tests

    [Fact]
    public void Analyze_AssertWithBoolLiteral_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    assert!(true)
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_AssertWithComparison_NoErrors()
    {
        var source = @"
pub fn test(x: i32) -> i32 {
    assert!(x > 0)
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_AssertWithMessage_NoErrors()
    {
        var source = @"
pub fn test(x: i32) -> i32 {
    assert!(x > 0, ""x must be positive"")
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_AssertWithNonBoolCondition_ReportsError()
    {
        var source = @"
pub fn test() -> i32 {
    assert!(42)
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
    }

    #endregion

    #region Panic Statement Tests

    [Fact]
    public void Analyze_PanicWithMessage_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    panic!(""something went wrong"")
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Defer Tests

    [Fact]
    public void Analyze_DeferBlock_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut x: i32 = 42
    defer {
        x = 0
    }
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_DeferExpression_NoErrors()
    {
        var source = @"
pub fn cleanup(x: i32) -> void {
    return
}

pub fn test() -> i32 {
    var x: i32 = 42
    defer cleanup(x)
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_MultipleDeferBlocks_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut x: i32 = 1
    defer {
        x = x + 1
    }
    defer {
        x = x * 2
    }
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region For Loop Tests

    [Fact]
    public void Analyze_ForCStyleLoop_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut sum: i32 = 0
    for var mut i: i32 = 0; i < 10; i = i + 1 {
        sum = sum + i
    }
    return sum
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ForInLoop_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var arr: [i32; 5] = [1, 2, 3, 4, 5]
    var mut sum: i32 = 0
    for x in arr {
        sum = sum + x
    }
    return sum
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ForInLoopWithMutableBinding_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var arr: [i32; 3] = [1, 2, 3]
    for mut x in arr {
        x = x * 2
    }
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ForLoopBreak_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    for var i: i32 = 0; i < 100; i = i + 1 {
        if i == 10 {
            break
        }
    }
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Sizeof Tests

    [Fact]
    public void Analyze_SizeofPrimitiveType_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var size: i32 = sizeof(i32)
    return size
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_SizeofStructType_NoErrors()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn test() -> i32 {
    var size: i32 = sizeof(Point)
    return size
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_SizeofArrayType_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var size: i32 = sizeof([i32; 10])
    return size
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Borrow/Reference Tests

    [Fact]
    public void Analyze_ImmutableBorrow_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var x: i32 = 42
    var r: &i32 = &x
    return *r
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_MutableBorrow_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut x: i32 = 42
    var r: &mut i32 = &mut x
    *r = 100
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_DereferenceImmutableReference_NoErrors()
    {
        var source = @"
pub fn test(r: &i32) -> i32 {
    return *r
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_DereferenceMutableReference_NoErrors()
    {
        var source = @"
pub fn test(r: &mut i32) -> void {
    *r = 42
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Increment/Decrement Tests

    [Fact]
    public void Analyze_PreIncrementMutableVariable_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut x: i32 = 0
    return ++x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_PostIncrementMutableVariable_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut x: i32 = 0
    return x++
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_PreDecrementMutableVariable_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut x: i32 = 10
    return --x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_PostDecrementMutableVariable_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut x: i32 = 10
    return x--
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_PreIncrementImmutableVariable_ReportsError()
    {
        // Using 'let' which is immutable by design
        var source = @"
pub fn test() -> i32 {
    let x: i32 = 0
    return ++x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_PostDecrementImmutableVariable_ReportsError()
    {
        // Using 'let' which is immutable by design
        var source = @"
pub fn test() -> i32 {
    let x: i32 = 10
    return x--
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
    }

    #endregion

    #region Match Expression Tests

    [Fact]
    public void Analyze_MatchOnInteger_NoErrors()
    {
        var source = @"
pub fn test(x: i32) -> i32 {
    return match x {
        0 => 1,
        1 => 2,
        _ => 0,
    }
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_MatchOnBool_NoErrors()
    {
        var source = @"
pub fn test(b: bool) -> i32 {
    return match b {
        true => 1,
        false => 0,
    }
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_MatchWithGuards_NoErrors()
    {
        var source = @"
pub fn test(x: i32) -> i32 {
    return match x {
        n if n > 0 => 1,
        n if n < 0 => -1,
        _ => 0,
    }
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region String Interpolation Tests

    [Fact]
    public void Analyze_InterpolatedStringWithVariable_NoErrors()
    {
        var source = @"
pub fn test(x: i32) -> *u8 {
    return ""Value: {x}""
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_InterpolatedStringWithExpression_NoErrors()
    {
        var source = @"
pub fn test(x: i32, y: i32) -> *u8 {
    return ""Sum: {x + y}""
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_InterpolatedStringMultipleValues_NoErrors()
    {
        var source = @"
pub fn test(name: *u8, age: i32) -> *u8 {
    return ""Name: {name}, Age: {age}""
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Path Expression Tests

    [Fact]
    public void Analyze_PathExpressionModuleAccess_NoErrors()
    {
        var source = @"
pub fn helper() -> i32 {
    return 42
}

pub fn test() -> i32 {
    return helper()
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Bitwise Operation Tests

    [Fact]
    public void Analyze_BitwiseAnd_NoErrors()
    {
        var source = @"
pub fn test(a: i32, b: i32) -> i32 {
    return a & b
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_BitwiseOr_NoErrors()
    {
        var source = @"
pub fn test(a: i32, b: i32) -> i32 {
    return a | b
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_BitwiseXor_NoErrors()
    {
        var source = @"
pub fn test(a: i32, b: i32) -> i32 {
    return a ^ b
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_LeftShift_NoErrors()
    {
        var source = @"
pub fn test(x: i32) -> i32 {
    return x << 2
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_RightShift_NoErrors()
    {
        var source = @"
pub fn test(x: i32) -> i32 {
    return x >> 2
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Array Tests

    [Fact]
    public void Analyze_ArrayIndexing_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var arr: [i32; 5] = [1, 2, 3, 4, 5]
    return arr[2]
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ArrayIndexAssignment_NoErrors()
    {
        var source = @"
pub fn test() -> i32 {
    var mut arr: [i32; 5] = [1, 2, 3, 4, 5]
    arr[2] = 100
    return arr[2]
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion
}
