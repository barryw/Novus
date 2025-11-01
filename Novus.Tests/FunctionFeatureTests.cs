using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class FunctionFeatureTests
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
    public void BuildIr_RecursiveFunction_Factorial_Compiles()
    {
        var source = @"
fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}
pub fn main() -> i32 {
    return factorial(5)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_RecursiveFunction_Fibonacci_Compiles()
    {
        var source = @"
fn fib(n: i32) -> i32 {
    if n <= 1 {
        return n
    }
    return fib(n - 1) + fib(n - 2)
}
pub fn main() -> i32 {
    return fib(10)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_TailRecursive_Sum_Compiles()
    {
        var source = @"
fn sumTo(n: i32, acc: i32) -> i32 {
    if n == 0 {
        return acc
    }
    return sumTo(n - 1, acc + n)
}
pub fn main() -> i32 {
    return sumTo(10, 0)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MutualRecursion_EvenOdd_Compiles()
    {
        var source = @"
fn isEven(n: i32) -> bool {
    if n == 0 {
        return true
    }
    return isOdd(n - 1)
}
fn isOdd(n: i32) -> bool {
    if n == 0 {
        return false
    }
    return isEven(n - 1)
}
pub fn main() -> i32 {
    if isEven(10) {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_NoParams_Compiles()
    {
        var source = @"
fn getValue() -> i32 {
    return 42
}
pub fn main() -> i32 {
    return getValue()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_VoidReturn_Compiles()
    {
        var source = @"
fn doNothing() {
}
pub fn main() -> i32 {
    doNothing()
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_ManyParameters_Compiles()
    {
        var source = @"
fn sum8(a: i32, b: i32, c: i32, d: i32, e: i32, f: i32, g: i32, h: i32) -> i32 {
    return a + b + c + d + e + f + g + h
}
pub fn main() -> i32 {
    return sum8(1, 2, 3, 4, 5, 6, 7, 8)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_StructParam_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
fn sumPoint(p: Point) -> i32 {
    return p.x + p.y
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return sumPoint(p)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_StructReturn_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
fn makePoint(x: i32, y: i32) -> Point {
    return Point { x: x, y: y }
}
pub fn main() -> i32 {
    let p = makePoint(5, 10)
    return p.x + p.y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_EnumParam_Compiles()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}
fn colorToInt(c: Color) -> i32 {
    match c {
        Color::Red => { return 1 }
        Color::Green => { return 2 }
        Color::Blue => { return 3 }
    }
}
pub fn main() -> i32 {
    return colorToInt(Color::Green)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_EnumReturn_Compiles()
    {
        var source = @"
enum Result {
    Ok,
    Error
}
fn getResult(success: bool) -> Result {
    if success {
        return Result::Ok
    }
    return Result::Error
}
pub fn main() -> i32 {
    let r = getResult(true)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_BoolReturn_Compiles()
    {
        var source = @"
fn isPositive(x: i32) -> bool {
    return x > 0
}
pub fn main() -> i32 {
    if isPositive(10) {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_PointerParam_Compiles()
    {
        var source = @"
fn deref(p: *i32) -> i32 {
    if p == 0 as *i32 {
        return 0
    }
    return *p
}
pub fn main() -> i32 {
    var x = 42
    return deref(&x as *i32)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_PointerReturn_Compiles()
    {
        var source = @"
fn getPtr(x: *i32) -> *i32 {
    return x
}
pub fn main() -> i32 {
    var x = 42
    let p = getPtr(&x as *i32)
    return *p
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Function_NestedCalls_Compiles()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}
fn mul(a: i32, b: i32) -> i32 {
    return a * b
}
pub fn main() -> i32 {
    return add(mul(2, 3), mul(4, 5))
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
