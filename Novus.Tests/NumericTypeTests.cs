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
    let x: f32 = 3.14
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
    let x: f32 = 1.5
    let y: f32 = 2.5
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
    let x: f64 = 3.141592653589793
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
    let x: f64 = 1.0
    let y: f64 = 2.0
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
    let x: fixed16 = 1.5
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
    let x: fixed16 = 2.5
    let y: fixed16 = 1.5
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
    let x: fixed32 = 123.456
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
    let x: fixed32 = 100.25
    let y: fixed32 = 50.75
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
    let x: u64 = 4000000000
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
    let x: u64 = 1000000000
    let y: u64 = 2000000000
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
    let x: u64 = 1000000
    let y: u64 = 1000000
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
    let x: i64 = -1234567890123456789
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
    let x: i64 = 1000000000
    let y: i64 = -500000000
    let result = x + y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== TYPE CASTING TESTS ====================

    [Fact]
    public void BuildIr_ExplicitLiteralCasts_Compile()
    {
        var module = BuildIr(@"
pub fn main() -> i32 {
    let byte = (i8)0
    let precise = (f64)1.5
    let fixed = (fixed16)2.0
    return (i32)byte
}");

        var locals = module.Functions[0].LocalVariables.ToDictionary(local => local.Name, local => local.Type);
        Assert.Equal(IrIntType.I8, locals["byte"]);
        Assert.Equal(IrFloatType.F64, locals["precise"]);
        Assert.Equal(IrFixedType.Fixed16, locals["fixed"]);
    }

    [Fact]
    public void BuildIr_F32ToI32Cast_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: f32 = 3.14
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
