using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class MultiParamFunctionTests
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
    public void BuildIr_Function_FourParams_Compiles()
    {
        var source = @"
fn add4(a: i32, b: i32, c: i32, d: i32) -> i32 {
    return a + b + c + d
}
pub fn main() -> i32 {
    return add4(10, 20, 30, 40)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_FiveParams_Compiles()
    {
        var source = @"
fn sum5(a: i32, b: i32, c: i32, d: i32, e: i32) -> i32 {
    return a + b + c + d + e
}
pub fn main() -> i32 {
    return sum5(1, 2, 3, 4, 5)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_AllLiteralArgs_Compiles()
    {
        var source = @"
fn compute(a: i32, b: i32, c: i32) -> i32 {
    return (a + b) * c
}
pub fn main() -> i32 {
    return compute(10, 20, 2)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_AllVariableArgs_Compiles()
    {
        var source = @"
fn compute(a: i32, b: i32, c: i32) -> i32 {
    return (a + b) * c
}
pub fn main() -> i32 {
    let x = 10
    let y = 20
    let z = 2
    return compute(x, y, z)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_MixedPrimitiveAndPointerParams_Compiles()
    {
        var source = @"
fn process(val: i32, ptr: *i32, flag: bool) -> i32 {
    if flag {
        return val
    }
    return 0
}
pub fn main() -> i32 {
    let p: *i32 = 0 as *i32
    return process(42, p, true)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_DifferentTypedParams_Compiles()
    {
        var source = @"
fn mixed(a: i8, b: i16, c: i32, d: i64) -> i32 {
    return (i32)a + (i32)b + c + (i32)d
}
pub fn main() -> i32 {
    return mixed(10, 20, 30, 40)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
