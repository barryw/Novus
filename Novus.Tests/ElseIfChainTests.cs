using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class ElseIfChainTests
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
    public void BuildIr_IfElseIfElse_ThreeBranches_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    if x < 3 {
        return 1
    } else if x < 7 {
        return 2
    } else {
        return 3
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ElseIfChain_FiveBranches_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let score = 85
    if score >= 90 {
        return 5
    } else if score >= 80 {
        return 4
    } else if score >= 70 {
        return 3
    } else if score >= 60 {
        return 2
    } else {
        return 1
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ElseIfChain_DifferentReturnTypes_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    var result = 0
    if x < 5 {
        result = 1
    } else if x < 10 {
        result = 2
    } else if x < 15 {
        result = 3
    } else {
        result = 4
    }
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ElseIfChain_NestedIf_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let y = 10
    if x < 3 {
        if y < 5 {
            return 1
        } else {
            return 2
        }
    } else if x < 7 {
        return 3
    } else {
        return 4
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ElseIfChain_WithoutFinalElse_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    var result = 0
    if x < 3 {
        result = 1
    } else if x < 7 {
        result = 2
    } else if x < 10 {
        result = 3
    }
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ElseIfChain_ComplexConditions_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let y = 10
    if x > 0 && y > 0 {
        return 1
    } else if x < 0 || y < 0 {
        return 2
    } else {
        return 3
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ElseIfChain_WithEarlyReturns_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    if x < 0 {
        return -1
    } else if x == 0 {
        return 0
    } else if x < 10 {
        return 1
    } else if x < 100 {
        return 2
    } else {
        return 3
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
