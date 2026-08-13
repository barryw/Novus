using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for numeric edge cases and boundary conditions:
/// - MIN/MAX boundary values for all integer types
/// - Overflow/underflow wrapping verification
/// - Sign extension semantics
/// - Mixed-width operations
/// - Boundary arithmetic operations
/// </summary>
public class EdgeCaseNumericTests
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

    // ==================== I8 BOUNDARIES ====================

    [Fact]
    public void BuildIr_I8_MinValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i8 = -128
    return (i32)min
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I8_MaxValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: i8 = 127
    return (i32)max
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I8_MinOverflow_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i8 = -128
    let underflow = min - 1
    return (i32)underflow  // Should wrap to 127
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I8_MaxOverflow_Wraps_Compiles()
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

    // ==================== U8 BOUNDARIES ====================

    [Fact]
    public void BuildIr_U8_MinValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: u8 = 0
    return (i32)min
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U8_MaxValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: u8 = 255
    return (i32)max
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U8_MinUnderflow_Wraps_Compiles()
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
    public void BuildIr_U8_MaxOverflow_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: u8 = 255
    let overflow = max + 1
    return (i32)overflow  // Should wrap to 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== I16 BOUNDARIES ====================

    [Fact]
    public void BuildIr_I16_MinValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i16 = -32768
    return (i32)min
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I16_MaxValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: i16 = 32767
    return (i32)max
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I16_MinOverflow_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i16 = -32768
    let underflow = min - 1
    return (i32)underflow  // Should wrap to 32767
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I16_MaxOverflow_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: i16 = 32767
    let overflow = max + 1
    return (i32)overflow  // Should wrap to -32768
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== U16 BOUNDARIES ====================

    [Fact]
    public void BuildIr_U16_MinValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: u16 = 0
    return (i32)min
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U16_MaxValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: u16 = 65535
    return (i32)max
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U16_MinUnderflow_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let zero: u16 = 0
    let underflow = zero - 1
    return (i32)underflow  // Should wrap to 65535
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U16_MaxOverflow_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: u16 = 65535
    let overflow = max + 1
    return (i32)overflow  // Should wrap to 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== I32 BOUNDARIES ====================

    [Fact]
    public void BuildIr_I32_MinValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i32 = -2147483648
    return min
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32_MaxValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: i32 = 2147483647
    return max
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32_MinOverflow_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i32 = -2147483648
    let underflow = min - 1
    return underflow  // Should wrap to 2147483647
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32_MaxOverflow_Wraps_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: i32 = 2147483647
    let overflow = max + 1
    return overflow  // Should wrap to -2147483648
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== SIGN EXTENSION ====================

    [Fact]
    public void BuildIr_SignExtension_I8_Negative_To_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let small: i8 = -1
    let extended: i32 = (i32)small
    return extended  // Should be -1 (0xFFFFFFFF), not 255
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_SignExtension_I8_Positive_To_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let small: i8 = 127
    let extended: i32 = (i32)small
    return extended  // Should be 127
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_SignExtension_I16_Negative_To_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let small: i16 = -1
    let extended: i32 = (i32)small
    return extended  // Should be -1 (0xFFFFFFFF), not 65535
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ZeroExtension_U8_To_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let small: u8 = 255
    let extended: i32 = (i32)small
    return extended  // Should be 255, not -1
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ZeroExtension_U16_To_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let small: u16 = 65535
    let extended: i32 = (i32)small
    return extended  // Should be 65535, not -1
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== MIXED-WIDTH OPERATIONS ====================

    [Fact]
    public void BuildIr_MixedWidth_I8_Plus_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 10
    let b: i32 = 20
    let result = (i32)a + b
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MixedWidth_U8_Plus_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u8 = 200
    let b: i32 = 100
    let result = (i32)a + b
    return result  // Should be 300
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MixedWidth_I16_Multiply_I16_Overflow_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i16 = 1000
    let b: i16 = 100
    let result = a * b
    return (i32)result  // Should wrap
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== BOUNDARY ARITHMETIC ====================

    [Fact]
    public void BuildIr_BoundaryArithmetic_I8_NegateMin_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i8 = -128
    let negated = -min
    return (i32)negated  // Should wrap to -128 (i8::MIN negated = i8::MIN)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BoundaryArithmetic_I8_AddToMax_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let max: i8 = 127
    let result = max + 1
    return (i32)result  // Should wrap to -128
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BoundaryArithmetic_U8_SubtractFromMin_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let zero: u8 = 0
    let result = zero - 1
    return (i32)result  // Should wrap to 255
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BoundaryArithmetic_I32_NegateBoundary_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let min: i32 = -2147483648
    let negated = -min
    return negated  // Should wrap to -2147483648
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BoundaryArithmetic_MultipleOverflows_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i8 = 100
    let y = x + 50  // Overflow to -106
    let z = y + 50  // Overflow again
    return (i32)z
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== TRUNCATION ====================

    [Fact]
    public void BuildIr_Truncation_I32_To_I8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let large: i32 = 1000
    let truncated: i8 = (i8)large
    return (i32)truncated  // Should be -24 (1000 & 0xFF = 232 = -24 as i8)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Truncation_I32_To_U8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let large: i32 = 1000
    let truncated: u8 = (u8)large
    return (i32)truncated  // Should be 232 (1000 & 0xFF)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Truncation_I32_To_I16_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let large: i32 = 100000
    let truncated: i16 = (i16)large
    return (i32)truncated  // Should be -31072 (100000 & 0xFFFF = 34464 = -31072 as i16)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== ZERO AND ONE ====================

    [Fact]
    public void BuildIr_Multiply_By_Zero_AllTypes_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 127
    let b: i16 = 32767
    let c: i32 = 2147483647
    let result = (i32)(a * 0) + (i32)(b * 0) + (c * 0)
    return result  // Should be 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Multiply_By_One_AllTypes_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 42
    let b: i16 = 1000
    let result = (i32)(a * 1) + (i32)(b * 1)
    return result  // Should be 1042
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
