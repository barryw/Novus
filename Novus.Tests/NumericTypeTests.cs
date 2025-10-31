using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for floating-point (f32, f64), fixed-point (fixed16, fixed32), and 64-bit integer types
/// </summary>
public class NumericTypeTests
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

    // ==================== F32 TESTS ====================

    [Fact]
    public void BuildIr_F32Literal_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: f32 = 3.14f32
    return 0
}";
        var module = BuildIr(source);
        var function = module.Functions[0];
        var localDecl = function.BasicBlocks[0].Instructions
            .OfType<IrLocalDecl>()
            .FirstOrDefault(decl => decl.Name == "x");
        Assert.NotNull(localDecl);
        Assert.IsType<IrFloatType>(localDecl.Type);
    }

    [Fact]
    public void BuildIr_F32Arithmetic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: f32 = 1.5f32
    let y: f32 = 2.5f32
    let result = x + y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== F64 TESTS ====================

    [Fact]
    public void BuildIr_F64Literal_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: f64 = 3.141592653589793f64
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F64Arithmetic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: f64 = 1.0f64
    let y: f64 = 2.0f64
    let sum = x + y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED16 TESTS ====================

    [Fact]
    public void BuildIr_Fixed16Literal_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: fixed16 = 1.5fixed16
    return 0
}";
        var module = BuildIr(source);
        var function = module.Functions[0];
        var localDecl = function.BasicBlocks[0].Instructions
            .OfType<IrLocalDecl>()
            .FirstOrDefault(decl => decl.Name == "x");
        Assert.NotNull(localDecl);
        Assert.IsType<IrFixedType>(localDecl.Type);
    }

    [Fact]
    public void BuildIr_Fixed16Arithmetic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: fixed16 = 2.5fixed16
    let y: fixed16 = 1.5fixed16
    let result = x + y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED32 TESTS ====================

    [Fact]
    public void BuildIr_Fixed32Literal_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: fixed32 = 123.456fixed32
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32Arithmetic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: fixed32 = 100.25fixed32
    let y: fixed32 = 50.75fixed32
    let result = x + y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== U64 TESTS ====================

    [Fact]
    public void BuildIr_U64Literal_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: u64 = 4000000000u64
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U64Addition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: u64 = 1000000000u64
    let y: u64 = 2000000000u64
    let result = x + y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U64Multiplication_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: u64 = 1000000u64
    let y: u64 = 1000000u64
    let result = x * y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== I64 TESTS ====================

    [Fact]
    public void BuildIr_I64Literal_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i64 = -1234567890123456789i64
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I64Arithmetic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i64 = 1000000000i64
    let y: i64 = -500000000i64
    let result = x + y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== TYPE CASTING TESTS ====================

    [Fact]
    public void BuildIr_F32ToI32Cast_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: f32 = 3.14f32
    let y: i32 = (i32)x
    return y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32ToF32Cast_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: i32 = 42
    let y: f32 = (f32)x
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
