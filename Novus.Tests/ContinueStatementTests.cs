using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for continue statements in loops
/// </summary>
public class ContinueStatementTests
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
    public void BuildIr_ContinueInWhileLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    while x < 10 {
        x += 1
        if x == 5 {
            continue
        }
        x += 1
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Functions);
    }

    [Fact]
    public void BuildIr_ContinueInForLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for (var i = 0; i < 10; i++) {
        if i == 5 {
            continue
        }
        sum += i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ContinueInForeverLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    forever {
        x += 1
        if x == 10 {
            break
        }
        if x == 5 {
            continue
        }
        x += 1
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ContinueInNestedLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 10 {
        i += 1
        var j = 0
        while j < 10 {
            j += 1
            if j == 5 {
                continue
            }
            sum += 1
        }
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleContinuesInLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 20 {
        i += 1
        if i < 5 {
            continue
        }
        if i > 15 {
            continue
        }
        sum += i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
