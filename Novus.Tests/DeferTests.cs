using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class DeferTests
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
    public void BuildIr_Defer_SingleStatement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    defer x = 20
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_MultipleStatements_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    var y = 20
    defer x = 100
    defer y = 200
    return x + y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_WithBlock_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    defer {
        x = 20
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_MultipleStatementsInBlock_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    var y = 20
    defer {
        x = 100
        y = 200
    }
    return x + y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_WithEarlyReturn_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    defer x = 20
    if true {
        return x
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_WithBreak_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 10 {
        defer i = i + 1
        if i == 5 {
            break
        }
        sum = sum + i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_WithContinue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 10 {
        defer i = i + 1
        if i == 5 {
            continue
        }
        sum = sum + i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_NestedScopes_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    defer x = 100
    {
        var y = 20
        defer y = 200
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_InIfBranch_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    if true {
        defer x = 20
        return x
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_InElseBranch_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    if false {
        return 0
    } else {
        defer x = 20
        return x
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_InLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 5 {
        var temp = i
        defer temp = 0
        sum = sum + temp
        i = i + 1
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_MultipleInSameScope_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var a = 1
    var b = 2
    var c = 3
    defer a = 10
    defer b = 20
    defer c = 30
    return a + b + c
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_ModifyingStruct_Compiles()
    {
        var source = @"
struct Counter {
    value: i32
}
pub fn main() -> i32 {
    var c = Counter { value: 10 }
    defer c.value = 100
    return c.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_ModifyingArray_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = {1, 2, 3}
    defer arr[0] = 100
    return arr[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_ExecutionOrder_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 1
    defer x = x * 2
    defer x = x + 10
    defer x = x + 100
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
