using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class SemanticAnalyzerTests
{
    private DiagnosticBag Analyze(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        // For tests, use a dummy stdLibPath since most tests don't use imports
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);
        return analyzer.Diagnostics;
    }

    [Fact]
    public void Analyze_ValidProgram_NoErrors()
    {
        var source = @"
fn main() -> u32 {
    return 42
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
        Assert.Equal(0, diagnostics.ErrorCount);
    }

    [Fact]
    public void Analyze_DuplicateFunction_ReportsError()
    {
        var source = @"
fn test() -> u32 {
    return 42
}

fn test() -> u32 {
    return 24
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        Assert.Equal(1, diagnostics.ErrorCount);
        var error = diagnostics.Diagnostics[0];
        Assert.Equal("E0001", error.Code);
        Assert.Contains("multiple times", error.Message);
    }

    [Fact]
    public void Analyze_ExplicitTypeSuffixOutOfRange_ReportsError()
    {
        var source = @"
fn test() -> u8 {
    return 1000u8
}";
        var diagnostics = Analyze(source);

        // 1000 with explicit u8 suffix should fail - doesn't fit in u8
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0009");
        Assert.NotNull(error);
    }

    [Fact]
    public void Analyze_UndefinedVariable_ReportsError()
    {
        var source = @"
fn test() -> u32 {
    return foo
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0007");
        Assert.NotNull(error);
        Assert.Contains("undefined variable", error.Message);
    }

    [Fact]
    public void Analyze_DivisionByZero_ReportsError()
    {
        var source = @"
fn test() -> u32 {
    return 42 / 0
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0005");
        Assert.NotNull(error);
        Assert.Contains("division by zero", error.Message);
    }

    [Fact]
    public void Analyze_MixedSignedness_ReportsWarning()
    {
        var source = @"
fn test() -> u32 {
    return 42u32 + 10i32
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0001");
        Assert.NotNull(warning);
        Assert.Contains("mixing signed and unsigned", warning.Message);
    }

    [Fact]
    public void Analyze_LiteralOutOfRange_ReportsError()
    {
        var source = @"
fn test() -> u8 {
    return 256u8
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0009");
        Assert.NotNull(error);
        Assert.Contains("out of range", error.Message);
    }

    [Fact]
    public void Analyze_NegativeLiteralForUnsignedType_ReportsError()
    {
        var source = @"
fn test() -> u32 {
    return -42u32
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0009");
        Assert.NotNull(error);
    }

    [Fact]
    public void Analyze_ValidCast_NoErrors()
    {
        var source = @"
fn test() -> u16 {
    return (u16)$BEEF
}";
        var diagnostics = Analyze(source);

        // Should compile (may have warnings about lossy cast)
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_LossyCast_ReportsWarning()
    {
        var source = @"
fn test() -> u8 {
    return (u8)1000
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0002");
        Assert.NotNull(warning);
        Assert.Contains("may lose precision", warning.Message);
    }

    [Fact]
    public void Analyze_BinaryLiteralOutOfRange_ReportsError()
    {
        var source = @"
fn test() -> u8 {
    return %111111111u8
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0009");
        Assert.NotNull(error);
    }

    [Fact]
    public void Analyze_HexLiteralOutOfRange_ReportsError()
    {
        var source = @"
fn test() -> u8 {
    return $FFFu8
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0009");
        Assert.NotNull(error);
    }

    [Fact]
    public void Analyze_ValidParameter_NoErrors()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_SignedToUnsignedCast_ReportsWarning()
    {
        var source = @"
fn test() -> u32 {
    return (u32)-42
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0002");
        Assert.NotNull(warning);
    }

    [Fact]
    public void Analyze_ValidBinaryLiteral_NoErrors()
    {
        var source = @"
fn test() -> u8 {
    return %11111111
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ValidHexLiteral_NoErrors()
    {
        var source = @"
fn test() -> u8 {
    return $FF
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_MultipleErrors_ReportsAll()
    {
        var source = @"
fn test() -> u8 {
    return 1000 + foo / 0
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        // Should have errors for: undefined variable (foo), division by zero, and possibly range error
        Assert.True(diagnostics.ErrorCount >= 1);
    }

    [Fact]
    public void Analyze_ValidMultipleFunctions_NoErrors()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn main() -> u32 {
    return 42
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    // Control Flow Semantic Analysis Tests

    [Fact]
    public void Analyze_ValidIfStatement_NoErrors()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ValidIfElseStatement_NoErrors()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 10 {
        return 100
    } else {
        return 200
    }
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ValidWhileLoop_NoErrors()
    {
        var source = @"
fn test() -> i32 {
    while 1 == 1 {
        return 42
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ValidForeverLoop_NoErrors()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 42
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_BreakOutsideLoop_ReportsError()
    {
        var source = @"
fn test() -> i32 {
    break
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0011");
        Assert.NotNull(error);
        Assert.Contains("outside of loop", error.Message);
    }

    [Fact]
    public void Analyze_BreakInIf_ReportsError()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 0 {
        break
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0011");
        Assert.NotNull(error);
    }

    [Fact]
    public void Analyze_ValidBreakInWhile_NoErrors()
    {
        var source = @"
fn test() -> i32 {
    while 1 == 1 {
        break
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ValidBreakInForever_NoErrors()
    {
        var source = @"
fn test() -> i32 {
    forever {
        break
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_NestedLoopBreak_NoErrors()
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
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_BreakInNestedIfInsideLoop_NoErrors()
    {
        var source = @"
fn test(x: i32) -> i32 {
    while x > 0 {
        if x > 10 {
            break
        }
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_IfConditionNumericType_NoErrors()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if x > 0 {
        return 1
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_WhileConditionNumericType_NoErrors()
    {
        var source = @"
fn test() -> i32 {
    while 1 == 1 {
        break
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_AllComparisonOperators_NoErrors()
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
            var diagnostics = Analyze(source);

            Assert.False(diagnostics.HasErrors);
        }
    }

    [Fact]
    public void Analyze_ComplexCondition_NoErrors()
    {
        var source = @"
fn test(x: i32) -> i32 {
    if (x + 5) > 10 {
        return 1
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_NestedIfStatements_NoErrors()
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
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ElseIfChain_NoErrors()
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
        var diagnostics = Analyze(source);

        // Debug: print all diagnostics
        if (diagnostics.HasErrors)
        {
            Console.WriteLine("Unexpected errors:");
            foreach (var d in diagnostics.Diagnostics.Where(d => d.IsError))
            {
                Console.WriteLine($"  [{d.Code}] {d.Message}");
            }
        }

        Assert.False(diagnostics.HasErrors);
    }

    // Boolean Type Tests

    [Fact]
    public void Analyze_BoolLiterals_NoErrors()
    {
        var source = @"
fn test() -> bool {
    let x: bool = true
    let y: bool = false
    return x
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ComparisonReturnsBool_NoErrors()
    {
        var source = @"
fn test() -> bool {
    let a: u32 = 10
    let b: u32 = 20
    return a < b
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_BoolInIfCondition_NoErrors()
    {
        var source = @"
fn test() -> u32 {
    let condition: bool = true
    if condition {
        return 42
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_LogicalAndOperation_NoErrors()
    {
        var source = @"
fn test() -> bool {
    let x: bool = true
    let y: bool = false
    return x && y
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_LogicalOrOperation_NoErrors()
    {
        var source = @"
fn test() -> bool {
    let x: bool = true
    let y: bool = false
    return x || y
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_LogicalNotOperation_NoErrors()
    {
        var source = @"
fn test() -> bool {
    let x: bool = false
    return !x
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_AllComparisonOperatorsReturnBool_NoErrors()
    {
        var operators = new[] { "==", "!=", "<", ">", "<=", ">=" };

        foreach (var op in operators)
        {
            var source = $@"
fn test() -> bool {{
    let a: i32 = 10
    let b: i32 = 20
    return a {op} b
}}";
            var diagnostics = Analyze(source);

            Assert.False(diagnostics.HasErrors);
        }
    }

    [Fact]
    public void Analyze_BoolTypeMismatch_ReportsError()
    {
        var source = @"
fn test() -> bool {
    let x: bool = 42
    return x
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_BoolAssignedFromComparison_NoErrors()
    {
        var source = @"
fn test(a: u32, b: u32) -> bool {
    let result: bool = a < b
    return result
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ComplexBoolExpression_NoErrors()
    {
        var source = @"
fn test(x: i32, y: i32) -> bool {
    return (x > 0) && (y > 0)
}";
        var diagnostics = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    // Unreachable Code Detection Tests

    [Fact]
    public void Analyze_CodeAfterReturn_ReportsWarning()
    {
        var source = @"
fn test() -> u32 {
    return 42
    let x: u32 = 10
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0003");
        Assert.NotNull(warning);
        Assert.Contains("unreachable", warning.Message.ToLower());
    }

    [Fact]
    public void Analyze_CodeAfterBreak_ReportsWarning()
    {
        var source = @"
fn test() -> u32 {
    forever {
        break
        let x: u32 = 10
    }
    return 42
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0003");
        Assert.NotNull(warning);
    }

    [Fact]
    public void Analyze_MultipleStatementsAfterReturn_ReportsMultipleWarnings()
    {
        var source = @"
fn test() -> u32 {
    return 42
    let x: u32 = 10
    let y: u32 = 20
    return x + y
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        // Should have warnings for all unreachable statements
        var warnings = diagnostics.Diagnostics.Where(d => d.Code == "W0003").ToList();
        Assert.True(warnings.Count >= 2);
    }

    [Fact]
    public void Analyze_IfElseWithBothBranchesReturning_CodeAfterIsUnreachable()
    {
        var source = @"
fn test(x: i32) -> u32 {
    if x > 0 {
        return 1
    } else {
        return 2
    }
    return 3
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0003");
        Assert.NotNull(warning);
    }

    [Fact]
    public void Analyze_IfWithOnlyThenReturning_NoWarningForCodeAfter()
    {
        var source = @"
fn test(x: i32) -> u32 {
    if x > 0 {
        return 1
    }
    return 2
}";
        var diagnostics = Analyze(source);

        // No warning - the else branch doesn't return, so code after is reachable
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0003");
        Assert.Null(warning);
    }

    [Fact]
    public void Analyze_NestedBlocksWithReturn_DetectsUnreachable()
    {
        var source = @"
fn test() -> u32 {
    if true {
        return 42
        let x: u32 = 10
    }
    return 0
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0003");
        Assert.NotNull(warning);
    }

    [Fact]
    public void Analyze_ElseIfChainAllReturning_DetectsUnreachable()
    {
        var source = @"
fn test(x: i32) -> u32 {
    if x > 10 {
        return 1
    } else if x > 5 {
        return 2
    } else {
        return 3
    }
    return 4
}";
        var diagnostics = Analyze(source);

        Assert.True(diagnostics.HasWarnings);
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0003");
        Assert.NotNull(warning);
    }

    [Fact]
    public void Analyze_WhileLoopWithBreakOnly_NoWarningForCodeAfter()
    {
        var source = @"
fn test() -> u32 {
    while true {
        break
    }
    return 42
}";
        var diagnostics = Analyze(source);

        // Code after the while loop is reachable because break exits the loop
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0003");
        Assert.Null(warning);
    }

    [Fact]
    public void Analyze_ValidCodeAfterConditionalReturn_NoWarning()
    {
        var source = @"
fn test(x: i32) -> u32 {
    if x > 0 {
        return 1
    }
    let y: u32 = 2
    return y
}";
        var diagnostics = Analyze(source);

        // No unreachable code - the if doesn't have else, so code after is reachable
        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W0003");
        Assert.Null(warning);
        Assert.False(diagnostics.HasWarnings);
    }

    // ===== Struct Tests =====

    [Fact]
    public void Analyze_StructDefinition_NoErrors()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn main() -> u32 {
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_StructLiteral_NoErrors()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn main() -> u32 {
    let p: Point = Point { x: 10, y: 20 }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_StructMemberAccess_NoErrors()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn main() -> u32 {
    let p: Point = Point { x: 10, y: 20 }
    return p.x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_StructParameter_NoErrors()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn get_x(p: Point) -> u32 {
    return p.x
}

fn main() -> u32 {
    let point: Point = Point { x: 42, y: 100 }
    return get_x(point)
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_StructMissingField_ReportsError()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn main() -> u32 {
    let p: Point = Point { x: 10 }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0026");
        Assert.NotNull(error);
        Assert.Contains("not initialized", error.Message);
    }

    [Fact]
    public void Analyze_StructInvalidField_ReportsError()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn main() -> u32 {
    let p: Point = Point { x: 10, z: 20 }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0024");
        Assert.NotNull(error);
        Assert.Contains("does not have a field", error.Message);
    }

    [Fact]
    public void Analyze_StructDuplicateField_ReportsError()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn main() -> u32 {
    let p: Point = Point { x: 10, y: 20, x: 30 }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0025");
        Assert.NotNull(error);
        Assert.Contains("initialized multiple times", error.Message);
    }

    [Fact]
    public void Analyze_StructMemberAccessNonStruct_ReportsError()
    {
        var source = @"
fn main() -> u32 {
    let x: u32 = 10
    return x.field
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0021");
        Assert.NotNull(error);
        Assert.Contains("non-struct", error.Message);
    }

    [Fact]
    public void Analyze_StructMemberAccessInvalidMember_ReportsError()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn main() -> u32 {
    let p: Point = Point { x: 10, y: 20 }
    return p.z
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0022");
        Assert.NotNull(error);
        Assert.Contains("does not have a field", error.Message);
    }

    [Fact]
    public void Analyze_StructUnknownType_ReportsError()
    {
        var source = @"
fn main() -> u32 {
    let p: Point = Point { x: 10, y: 20 }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        // Could be E0020 (unknown type) or E0023 (unknown struct)
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0020" || d.Code == "E0023");
        Assert.NotNull(error);
        Assert.True(error.Message.Contains("unknown type") || error.Message.Contains("unknown struct"));
    }

    [Fact]
    public void Analyze_StructFieldTypeMismatch_ReportsError()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

fn main() -> u32 {
    let p: Point = Point { x: true, y: 20 }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0011");
        Assert.NotNull(error);
        Assert.Contains("type mismatch", error.Message);
    }

    [Fact]
    public void Analyze_MultipleStructTypes_NoErrors()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

struct Rect {
    width: u32,
    height: u32
}

fn main() -> u32 {
    let p: Point = Point { x: 10, y: 20 }
    let r: Rect = Rect { width: 100, height: 200 }
    return p.x + r.width
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_NestedStructInlineInitialization_NoErrors()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

struct Rect {
    top_left: Point,
    width: u32
}

pub fn main() -> u32 {
    let r: Rect = Rect {
        top_left: Point { x: 10, y: 20 },
        width: 100
    }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_NestedStructVariableAssignment_NoErrors()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

struct Rect {
    top_left: Point,
    width: u32,
    height: u32
}

pub fn main() -> u32 {
    let p: Point = Point { x: 10, y: 20 }
    let r: Rect = Rect {
        top_left: p,
        width: 100,
        height: 200
    }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ChainedMemberAccess_NoErrors()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

struct Rect {
    top_left: Point,
    width: u32
}

pub fn main() -> u32 {
    let r: Rect = Rect {
        top_left: Point { x: 10, y: 20 },
        width: 100
    }
    return r.top_left.x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_MultiLevelNestedStructs_NoErrors()
    {
        var source = @"
struct Position {
    x: u32,
    y: u32
}

struct Size {
    width: u32,
    height: u32
}

struct Rect {
    pos: Position,
    size: Size
}

struct Window {
    bounds: Rect,
    visible: bool
}

pub fn main() -> u32 {
    let w: Window = Window {
        bounds: Rect {
            pos: Position { x: 10, y: 20 },
            size: Size { width: 640, height: 480 }
        },
        visible: true
    }
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_NestedStructInvalidField_ReportsError()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

struct Rect {
    top_left: Point,
    width: u32
}

pub fn main() -> u32 {
    let r: Rect = Rect {
        top_left: Point { x: 10, y: 20 },
        width: 100
    }
    return r.top_left.z
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("does not have a field named 'z'"));
    }

    [Fact]
    public void Analyze_NestedStructFieldTypeMismatch_ReportsError()
    {
        var source = @"
struct Point {
    x: u32,
    y: u32
}

struct Rect {
    top_left: Point,
    width: u32
}

pub fn main() -> u32 {
    let r: Rect = Rect {
        top_left: 42,
        width: 100
    }
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("type mismatch") || d.Message.Contains("incompatible"));
    }

    // Mutability validation tests
    [Fact]
    public void Analyze_AssignToImmutableVariable_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    x = 20
    return x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0019" && d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_AssignToMutableVariable_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    var mut x = 10
    x = 20
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_IncrementImmutableVariable_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    x++
    return x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0019" && d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_IncrementMutableVariable_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    var mut x = 10
    x++
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_CompoundAssignToImmutableVariable_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    x += 5
    return x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0019" && d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_CompoundAssignToMutableVariable_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    var mut x = 10
    x += 5
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_FunctionParametersAreImmutable_ReportsError()
    {
        var source = @"
pub fn test(x: i32) -> i32 {
    x = 20
    return x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0019" && d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_AssignThroughImmutableReference_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    let r = &x
    *r = 20
    return x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0026" && d.Message.Contains("immutable reference"));
    }

    // Variable shadowing tests
    [Fact]
    public void Analyze_VariableShadowingInSameScope_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    let x = 20
    return x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0016" && d.Message.Contains("already defined"));
    }

    [Fact]
    public void Analyze_ParameterShadowing_ReportsError()
    {
        var source = @"
pub fn test(x: i32) -> i32 {
    let x = 20
    return x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0016" && d.Message.Contains("already defined"));
    }

    // Type inference tests
    [Fact]
    public void Analyze_TypeInferenceFromLiteral_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 42
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_TypeInferenceFromExpression_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10 + 20
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_TypeInferenceFromFunctionCall_NoErrors()
    {
        var source = @"
fn get_value() -> i32 {
    return 42
}

pub fn main() -> i32 {
    let x = get_value()
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_MixedMutabilityInCompoundExpression_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let a = 10
    var mut b = 20
    b += a
    return b
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    // Array repeat literal tests
    [Fact]
    public void Analyze_ArrayRepeatLiteralWithDecimalSize_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [42; 10]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ArrayRepeatLiteralWithHexSize_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [1; $A]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ArrayRepeatLiteralWithBinarySize_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [2; %1010]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ArrayRepeatLiteralWithUnderscoresInSize_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [3; 1_000]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ArrayRepeatLiteralWithTypeSuffix_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let arr1 = [4; 20i32]
    let arr2 = [5; 15u32]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ArrayRepeatLiteralWithNegativeSize_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [42; -5]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0999" && d.Message.Contains("non-negative"));
    }

    [Fact]
    public void Analyze_ArrayRepeatLiteralWithZeroSize_NoErrors()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [42; 0]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_ArrayRepeatLiteralWithNonConstantSize_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    let n = 10
    let arr = [42; n]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0999" && d.Message.Contains("compile-time constant"));
    }

    [Fact]
    public void Analyze_ArrayRepeatLiteralWithNonIntegerSize_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [42; true]
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0999" && d.Message.Contains("integer"));
    }

    #region Variable Mutability Regression Tests
    // These tests ensure that variable mutability tracking works correctly
    // Per the language design: var = mutable, let = immutable

    [Fact]
    public void Analyze_Var_IsMutable()
    {
        // var is mutable by default - should allow reassignment
        var source = @"
pub fn test() -> i32 {
    var x: i32 = 0
    x = 5
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_LetWithoutMut_IsImmutable()
    {
        var source = @"
pub fn test() -> i32 {
    let x: i32 = 0
    x = 5
    return x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_VarMut_IsMutable()
    {
        var source = @"
pub fn test() -> i32 {
    var mut x: i32 = 0
    x = 5
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_LetMut_IsMutable()
    {
        var source = @"
pub fn test() -> i32 {
    let mut x: i32 = 0
    x = 5
    return x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_PreIncrementImmutableLet_ReportsError()
    {
        // ++x on immutable let binding should error
        var source = @"
pub fn test() -> i32 {
    let x: i32 = 0
    return ++x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0384" && d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_PostIncrementImmutableLet_ReportsError()
    {
        var source = @"
pub fn test() -> i32 {
    let x: i32 = 0
    return x++
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0384" && d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_PreDecrementImmutableLet_ReportsError()
    {
        var source = @"
pub fn test() -> i32 {
    let x: i32 = 10
    return --x
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0384" && d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_PostDecrementImmutableLet_ReportsError()
    {
        var source = @"
pub fn test() -> i32 {
    let x: i32 = 10
    return x--
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0384" && d.Message.Contains("immutable"));
    }

    [Fact]
    public void Analyze_IncrementMutableVar_NoError()
    {
        // var is mutable by default, so increment should work
        var source = @"
pub fn test() -> i32 {
    var x: i32 = 0
    return ++x
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_DecrementMutableVar_NoError()
    {
        // var is mutable by default, so decrement should work
        var source = @"
pub fn test() -> i32 {
    var x: i32 = 10
    return x--
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion
}
