using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.Transforms.Passes;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for InlineExpansionPass to improve coverage from 0% to 50%+.
/// Tests the Transform method and helper methods (IsInlinable, IsRecursive).
/// </summary>
public class InlineExpansionPassTests
{
    private IrModule BuildIR(string source)
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
    public void InlineExpansionPass_EmptyModule_NoChanges()
    {
        var pass = new InlineExpansionPass();
        var module = new IrModule();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_SimpleFunction_NoChanges()
    {
        var source = @"
pub fn simple(x: i32) -> i32 {
    return x + 1
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        // Pass is skeleton, should return false
        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_MultipleFunctions_NoChanges()
    {
        var source = @"
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}

pub fn mul(a: i32, b: i32) -> i32 {
    return a * b
}

pub fn compute(x: i32) -> i32 {
    var sum: i32 = add(x, 1)
    return mul(sum, 2)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        // Pass is skeleton, should return false
        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_RecursiveFunction_NoChanges()
    {
        var source = @"
pub fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_SmallFunction_NoChanges()
    {
        var source = @"
pub fn increment(x: i32) -> i32 {
    return x + 1
}

pub fn use_increment(y: i32) -> i32 {
    return increment(y)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_LargeFunction_NoChanges()
    {
        var source = @"
pub fn large_function(x: i32) -> i32 {
    var a: i32 = x + 1
    var b: i32 = a * 2
    var c: i32 = b - 3
    var d: i32 = c / 4
    var e: i32 = d + 5
    var f: i32 = e * 6
    var g: i32 = f - 7
    var h: i32 = g / 8
    var i: i32 = h + 9
    var j: i32 = i * 10
    return j
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_MainFunction_NoChanges()
    {
        var source = @"
pub fn main() -> i32 {
    return 0
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_NestedCalls_NoChanges()
    {
        var source = @"
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}

pub fn double(x: i32) -> i32 {
    return add(x, x)
}

pub fn quadruple(x: i32) -> i32 {
    return double(double(x))
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_MutualRecursion_NoChanges()
    {
        var source = @"
pub fn is_even(n: i32) -> bool {
    if n == 0 {
        return true
    }
    return is_odd(n - 1)
}

pub fn is_odd(n: i32) -> bool {
    if n == 0 {
        return false
    }
    return is_even(n - 1)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_ComplexControlFlow_NoChanges()
    {
        var source = @"
pub fn complex(x: i32, y: i32) -> i32 {
    if x > 0 {
        if y > 0 {
            return x + y
        }
        return x - y
    }
    if y > 0 {
        return y - x
    }
    return 0
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_LoopFunction_NoChanges()
    {
        var source = @"
pub fn sum_to_n(n: i32) -> i32 {
    var sum: i32 = 0
    var i: i32 = 0
    while i <= n {
        sum = sum + i
        i = i + 1
    }
    return sum
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_StructReturn_NoChanges()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn make_point(x: i32, y: i32) -> Point {
    return Point { x: x, y: y }
}

pub fn use_point() -> Point {
    return make_point(1, 2)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_EnumReturn_NoChanges()
    {
        var source = @"
pub enum Status {
    Active,
    Inactive,
}

pub fn get_active() -> Status {
    return Status::Active
}

pub fn check_status() -> Status {
    return get_active()
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_Name_ReturnsCorrectName()
    {
        var pass = new InlineExpansionPass();

        Assert.Equal("Inline Expansion", pass.Name);
    }
}
