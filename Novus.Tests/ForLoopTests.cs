using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for for loop statements
/// </summary>
public class ForLoopTests
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
    public void BuildIr_BasicForLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for (var i = 0; i < 10; i++) {
        sum += i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Functions);
    }

    [Fact]
    public void BuildIr_ForLoopWithBreak_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for (var i = 0; i < 100; i++) {
        if i == 10 {
            break
        }
        sum += i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ForLoopWithContinue_Compiles()
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
    public void BuildIr_NestedForLoops_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for (var i = 0; i < 5; i++) {
        for (var j = 0; j < 5; j++) {
            sum += i + j
        }
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ForLoopCountingDown_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for (var i = 10; i > 0; i--) {
        sum += i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ForLoopWithStep_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for (var i = 0; i < 20; i += 2) {
        sum += i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ForLoopVariableScoping_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var total = 0
    for (var i = 0; i < 5; i++) {
        total += i
    }
    for (var i = 0; i < 5; i++) {
        total += i
    }
    return total
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ForLoopEmptyBody_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    for (; i < 10; i++) {
    }
    return i
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
