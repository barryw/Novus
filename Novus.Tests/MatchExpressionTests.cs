using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class MatchExpressionTests
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
    public void MatchExpr_ImplicitReturn_FromBlockArm_Compiles()
    {
        var source = @"
pub fn test_implicit(n: i32) -> i32 {
    match n {
        0 => {
            let x = 10
            x * 2
        },
        1 => {
            let y = 20
            y + 5
        },
        _ => 999
    }
}

pub fn main() -> i32 {
    return test_implicit(0)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var function = module.Functions.FirstOrDefault(f => f.Name == "test_implicit");
        Assert.NotNull(function);
    }

    [Fact]
    public void MatchExpr_UsedInReturn_Compiles()
    {
        var source = @"
pub fn classify(n: i32) -> i32 {
    return match n {
        0 => 100,
        1 => 200,
        _ => 300
    }
}

pub fn main() -> i32 {
    return classify(1)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void MatchExpr_UsedInAssignment_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = match 5 {
        0 => 10,
        5 => 50,
        _ => 0
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void MatchExpr_UsedInFunctionCall_Compiles()
    {
        var source = @"
fn double(x: i32) -> i32 {
    return x * 2
}

pub fn main() -> i32 {
    return double(match 3 {
        0 => 1,
        3 => 5,
        _ => 0
    })
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void MatchExpr_NestedMatch_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return match 1 {
        0 => match 2 {
            0 => 10,
            _ => 20
        },
        1 => match 3 {
            0 => 30,
            _ => 40
        },
        _ => 50
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void MatchExpr_WithEnumPatterns_Compiles()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

pub fn color_value(c: Color) -> i32 {
    return match c {
        Color::Red => 1,
        Color::Green => 2,
        Color::Blue => 3
    }
}

pub fn main() -> i32 {
    return color_value(Color::Green)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void MatchExpr_MixedArmForms_Compiles()
    {
        var source = @"
pub fn mixed(n: i32) -> i32 {
    match n {
        0 => 10,
        1 => { return 20 },
        2 => {
            let x = 30
            x
        },
        _ => return 40
    }
}

pub fn main() -> i32 {
    return mixed(2)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
