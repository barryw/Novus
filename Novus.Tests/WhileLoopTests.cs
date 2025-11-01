using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class WhileLoopTests
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
    public void BuildIr_WhileLoop_Counter_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    while count < 10 {
        count = count + 1
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_WhileLoop_CountDown_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 10
    while count > 0 {
        count = count - 1
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_WhileLoop_Nested_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    var sum = 0
    while i < 5 {
        var j = 0
        while j < 5 {
            sum = sum + 1
            j = j + 1
        }
        i = i + 1
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_WhileLoop_ComplexCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    var y = 10
    while x < 5 && y > 5 {
        x = x + 1
        y = y - 1
    }
    return x + y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_WhileLoop_BreakInMiddle_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    while count < 100 {
        count = count + 1
        if count == 10 {
            break
        }
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_WhileLoop_MultipleVariables_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var a = 0
    var b = 10
    var c = 5
    while a < c {
        a = a + 1
        b = b - 1
        c = c - 1
    }
    return a + b + c
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_WhileLoop_EmptyBody_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    while x < 0 {
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_WhileLoop_ContinueAtDifferentPositions_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    var sum = 0
    while count < 10 {
        count = count + 1
        if count == 5 {
            continue
        }
        sum = sum + count
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
