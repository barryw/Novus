using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class TypeCastTests
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

    // Signed to signed casts
    [Fact]
    public void BuildIr_Cast_i8_to_i16_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 100
    let b: i16 = (i16)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i8_to_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 50
    return (i32)a
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i8_to_i64_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 25
    let b: i64 = (i64)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i16_to_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i16 = 1000
    return (i32)a
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i16_to_i64_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i16 = 500
    let b: i64 = (i64)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i32_to_i64_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = 100000
    let b: i64 = (i64)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Narrowing casts (larger to smaller)
    [Fact]
    public void BuildIr_Cast_i16_to_i8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i16 = 100
    let b: i8 = (i8)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i32_to_i8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = 100
    let b: i8 = (i8)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i32_to_i16_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = 1000
    let b: i16 = (i16)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i64_to_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i64 = 100000
    return (i32)a
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Unsigned to unsigned casts
    [Fact]
    public void BuildIr_Cast_u8_to_u16_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u8 = 200
    let b: u16 = (u16)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u8_to_u32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u8 = 150
    let b: u32 = (u32)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u16_to_u32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u16 = 50000
    let b: u32 = (u32)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u32_to_u64_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u32 = 1000000
    let b: u64 = (u64)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Unsigned narrowing casts
    [Fact]
    public void BuildIr_Cast_u16_to_u8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u16 = 200
    let b: u8 = (u8)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u32_to_u16_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u32 = 50000
    let b: u16 = (u16)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u64_to_u32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u64 = 1000000
    let b: u32 = (u32)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Signed to unsigned casts
    [Fact]
    public void BuildIr_Cast_i8_to_u8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 100
    let b: u8 = (u8)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i16_to_u16_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i16 = 1000
    let b: u16 = (u16)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i32_to_u32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = 50000
    let b: u32 = (u32)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i64_to_u64_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i64 = 100000
    let b: u64 = (u64)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Unsigned to signed casts
    [Fact]
    public void BuildIr_Cast_u8_to_i8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u8 = 100
    let b: i8 = (i8)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u16_to_i16_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u16 = 1000
    let b: i16 = (i16)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u32_to_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u32 = 50000
    return (i32)a
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u64_to_i64_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u64 = 100000
    let b: i64 = (i64)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Cross-size signed/unsigned casts
    [Fact]
    public void BuildIr_Cast_i8_to_u32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 50
    let b: u32 = (u32)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u8_to_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u8 = 200
    return (i32)a
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i16_to_u32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i16 = 1000
    let b: u32 = (u32)a
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_u16_to_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: u16 = 50000
    return (i32)a
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Bool casts
    [Fact]
    public void BuildIr_Cast_bool_to_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let b = true
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Cast_i32_to_bool_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let b = (bool)x
    if b {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Chained casts
    [Fact]
    public void BuildIr_Cast_Chained_i8_i32_i16_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i8 = 100
    let b = (i16)((i32)a)
    return (i32)b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
