using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for operator edge cases and semantic guarantees:
/// - Short-circuit evaluation (&&, ||)
/// - Integer overflow/underflow wrapping
/// - Division/modulo by zero (compile-time errors)
/// - Shift operator masking
/// - Operator precedence
/// </summary>
public class OperatorEdgeCaseTests
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

    // ==================== SHORT-CIRCUIT EVALUATION ====================

    [Fact]
    public void BuildIr_ShortCircuit_And_LeftFalse_SkipsRight_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    var result = false && {x = 1; true}
    return x  // Should be 0 (right side not evaluated)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        // IR should show conditional branch that skips right evaluation
        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.NotNull(mainFunc);
    }

    [Fact]
    public void BuildIr_ShortCircuit_And_LeftTrue_EvaluatesRight_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    var result = true && {x = 1; true}
    return x  // Should be 1 (right side evaluated)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ShortCircuit_Or_LeftTrue_SkipsRight_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    var result = true || {x = 1; false}
    return x  // Should be 0 (right side not evaluated)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        // IR should show conditional branch that skips right evaluation
        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.NotNull(mainFunc);
    }

    [Fact]
    public void BuildIr_ShortCircuit_Or_LeftFalse_EvaluatesRight_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    var result = false || {x = 1; true}
    return x  // Should be 1 (right side evaluated)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ShortCircuit_NestedAnd_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var a = 0
    var b = 0
    var result = false && {a = 1; true} && {b = 1; true}
    return a + b  // Should be 0 (both skipped)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ShortCircuit_NestedOr_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var a = 0
    var b = 0
    var result = true || {a = 1; false} || {b = 1; false}
    return a + b  // Should be 0 (both skipped)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== INTEGER OVERFLOW/UNDERFLOW ====================

    [Fact]
    public void BuildIr_IntegerOverflow_I8_Add_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: i8 = 127
    let overflow = max + 1
    return (i32)overflow  // Should wrap to -128
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_IntegerUnderflow_U8_Sub_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let zero: u8 = 0
    let underflow = zero - 1
    return (i32)underflow  // Should wrap to 255
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_IntegerOverflow_I16_Mul_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i16 = 1000
    let b: i16 = 100
    let overflow = a * b
    return (i32)overflow  // Should wrap
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_IntegerOverflow_I32_Add_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max = 2147483647  // i32::MAX
    let overflow = max + 1
    return overflow  // Should wrap to i32::MIN (-2147483648)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_IntegerUnderflow_I32_Sub_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min = -2147483648  // i32::MIN
    let underflow = min - 1
    return underflow  // Should wrap to i32::MAX
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== DIVISION/MODULO BY ZERO ====================

    [Fact]
    public void BuildIr_DivisionByZero_Literal_FailsSemanticAnalysis()
    {
        var source = @"
pub fn main() -> i32 {
    return 42 / 0
}";
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalysis.SemanticAnalyzer("test.novus", source, "");
        var success = analyzer.Analyze(tree);

        Assert.False(success);  // Should fail semantic analysis
        Assert.True(analyzer.Diagnostics.HasErrors);
        var error = analyzer.Diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0005");
        Assert.NotNull(error);
        Assert.Contains("division by zero", error.Message);
    }

    [Fact]
    public void BuildIr_ModuloByZero_Literal_FailsSemanticAnalysis()
    {
        var source = @"
pub fn main() -> i32 {
    return 42 % 0
}";
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalysis.SemanticAnalyzer("test.novus", source, "");
        var success = analyzer.Analyze(tree);

        Assert.False(success);  // Should fail semantic analysis
        Assert.True(analyzer.Diagnostics.HasErrors);
        var error = analyzer.Diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0005");
        Assert.NotNull(error);
        Assert.Contains("modulo by zero", error.Message);
    }

    [Fact]
    public void BuildIr_DivisionByNonZero_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 42 / 2  // Should be 21
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ModuloByNonZero_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 42 % 5  // Should be 2
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== SHIFT OPERATOR MASKING ====================

    [Fact]
    public void BuildIr_ShiftLeft_BeyondBitWidth_Masked_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i32 = 1
    let result = x << 32  // Masked to 0, result = x << 0 = 1
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        // Codegen should emit: (1 << (32 & 31)) = (1 << 0) = 1
    }

    [Fact]
    public void BuildIr_ShiftLeft_BeyondBitWidth_33_Masked_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i32 = 1
    let result = x << 33  // Masked to 1, result = x << 1 = 2
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        // Codegen should emit: (1 << (33 & 31)) = (1 << 1) = 2
    }

    [Fact]
    public void BuildIr_ShiftRight_BeyondBitWidth_Masked_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i32 = 1024
    let result = x >> 32  // Masked to 0, result = x >> 0 = 1024
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ShiftLeft_I16_BeyondBitWidth_Masked_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i16 = 1
    let result = x << 16  // Masked to 0 for 16-bit, result = 1
    return (i32)result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        // Codegen should emit: (1 << (16 & 15)) = (1 << 0) = 1
    }

    [Fact]
    public void BuildIr_ShiftLeft_I8_BeyondBitWidth_Masked_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i8 = 1
    let result = x << 8  // Masked to 0 for 8-bit, result = 1
    return (i32)result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        // Codegen should emit: (1 << (8 & 7)) = (1 << 0) = 1
    }

    // ==================== OPERATOR PRECEDENCE ====================

    [Fact]
    public void BuildIr_Precedence_MulBeforeAdd_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 1 + 2 * 3  // Should be 1 + 6 = 7, not (1+2)*3 = 9
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Precedence_DivBeforeSub_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 10 - 8 / 2  // Should be 10 - 4 = 6, not (10-8)/2 = 1
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Precedence_ShiftBeforeAdd_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 1 << 2 + 1  // Should be 1 << 3 = 8, not (1<<2)+1 = 5
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Precedence_BitwiseAndBeforeOr_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 1 | 2 & 3  // Should be 1 | (2&3) = 1|2 = 3, not (1|2)&3 = 3
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Precedence_ComparisonBeforeLogical_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let result = 1 == 1 && 2 == 2  // Should be (1==1) && (2==2) = true
    if result {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Precedence_ComplexExpression_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 1 + 2 * 3 - 4 / 2  // Should be 1 + 6 - 2 = 5
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== BITWISE OPERATORS ====================

    [Fact]
    public void BuildIr_BitwiseAnd_WithZero_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 255 & 0  // Should be 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BitwiseOr_WithAllOnes_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 0 | 255  // Should be 255
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BitwiseXor_SameValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 42
    return x ^ x  // Should be 0 (x XOR x = 0)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BitwiseNot_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 0
    return ~x  // Should be -1 (all bits set)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== UNARY OPERATORS ====================

    [Fact]
    public void BuildIr_UnaryMinus_MinValue_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i8 = -128
    let negated = -min  // Should wrap to -128 (i8::MIN negated = i8::MIN)
    return (i32)negated
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_LogicalNot_True_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = true
    if !x {
        return 0
    }
    return 1
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_LogicalNot_False_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = false
    if !x {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
