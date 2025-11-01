using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class BitwiseShiftTests
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
    public void BuildIr_BitwiseAnd_Basic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a = 0xFF
    let b = 0x0F
    return a & b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BitwiseOr_Basic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a = 0xF0
    let b = 0x0F
    return a | b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BitwiseXor_Basic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a = 0xFF
    let b = 0xAA
    return a ^ b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ComplexBitwiseExpression_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a = 0xF0
    let b = 0x0F
    let c = 0xFF
    let d = 0x55
    return (a & b) | (c ^ d)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BitwiseWithLiterals_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 0x1234
    return x & 0xFF
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BitmaskOperations_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var flags = 0
    flags = flags | 0x01
    flags = flags | 0x04
    flags = flags & ~0x01
    return flags
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_LeftShift_Basic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 1
    return x << 3
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_RightShift_Basic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 16
    return x >> 2
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ShiftByVariable_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 8
    let amount = 2
    return x << amount
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
