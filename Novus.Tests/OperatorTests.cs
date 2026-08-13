using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Comprehensive tests for increment/decrement and compound assignment operators
/// </summary>
public class OperatorTests
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

    // ==================== PRE-INCREMENT TESTS ====================

    [Fact]
    public void BuildIr_PreIncrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    ++x
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Functions);
    }

    [Fact]
    public void BuildIr_PreDecrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    --x
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultiplePreIncrements_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 1
    var y = 2
    ++x
    ++y
    return x + y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== POST-INCREMENT TESTS ====================

    [Fact]
    public void BuildIr_PostIncrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    x++
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostDecrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    x--
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MixedPrePostIncrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    var y = 5
    ++x
    y++
    return x + y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== COMPOUND ASSIGNMENT TESTS ====================

    [Theory]
    [InlineData("+=", IrBinaryOp.OpKind.Add)]
    [InlineData("-=", IrBinaryOp.OpKind.Sub)]
    [InlineData("*=", IrBinaryOp.OpKind.Mul)]
    [InlineData("/=", IrBinaryOp.OpKind.Div)]
    [InlineData("%=", IrBinaryOp.OpKind.Mod)]
    [InlineData("&=", IrBinaryOp.OpKind.And)]
    [InlineData("|=", IrBinaryOp.OpKind.Or)]
    [InlineData("^=", IrBinaryOp.OpKind.Xor)]
    [InlineData("<<=", IrBinaryOp.OpKind.Shl)]
    [InlineData(">>=", IrBinaryOp.OpKind.Shr)]
    public void BuildIr_CompoundAssignment_LowersToExactBinaryOperator(
        string assignmentOperator,
        IrBinaryOp.OpKind expectedOperator)
    {
        var module = BuildIr($$"""
            fn apply() -> u32 {
                var value: u32 = 8
                value {{assignmentOperator}} 2
                return value
            }
            """);

        var binary = Assert.Single(module.Functions.Single().BasicBlocks
            .SelectMany(block => block.Instructions).OfType<IrBinaryOp>());
        Assert.Equal(expectedOperator, binary.Operation);
        Assert.Equal("u32", binary.Left.Type.Name);
        Assert.Equal("u32", binary.Right.Type.Name);
        Assert.True(new IrValidator().Validate(module).IsValid);
    }

    [Fact]
    public void BuildIr_PlusEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    x += 5
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MinusEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    x -= 3
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_TimesEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    x *= 2
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_DivideEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    x /= 2
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ModEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    x %= 3
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_AndEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 15
    x &= 7
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_OrEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 8
    x |= 4
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_XorEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 15
    x ^= 7
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_LeftShiftEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 1
    x <<= 3
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_RightShiftEquals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 16
    x >>= 2
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    //==================== BITWISE NOT TESTS ====================

    [Fact]
    public void BuildIr_BitwiseNot_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 0
    let result = ~x
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BitwiseNotOnUnsigned_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: u8 = 0
    let result = ~x
    return (i32)result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== LOGICAL NOT TESTS ====================

    [Fact]
    public void BuildIr_LogicalNot_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = true
    let result = !x
    if result {
        return 0
    }
    return 1
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_DoubleLogicalNot_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = false
    if !!x {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
