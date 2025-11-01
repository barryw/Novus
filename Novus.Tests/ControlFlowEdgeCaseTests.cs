using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class ControlFlowEdgeCaseTests
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
    public void BuildIr_NestedLoop_TripleNesting_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 3 {
        var j = 0
        while j < 3 {
            var k = 0
            while k < 3 {
                sum = sum + 1
                k = k + 1
            }
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
    public void BuildIr_Break_InNestedLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    var found = 0
    while i < 10 {
        var j = 0
        while j < 10 {
            if j == 5 {
                found = 1
                break
            }
            j = j + 1
        }
        i = i + 1
    }
    return found
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Continue_InNestedLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 5 {
        var j = 0
        while j < 5 {
            j = j + 1
            if j == 3 {
                continue
            }
            sum = sum + 1
        }
        i = i + 1
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleReturns_InFunction_Compiles()
    {
        var source = @"
fn compute(x: i32) -> i32 {
    if x < 0 {
        return -1
    }
    if x == 0 {
        return 0
    }
    if x < 10 {
        return 1
    }
    if x < 100 {
        return 2
    }
    return 3
}
pub fn main() -> i32 {
    return compute(50)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EarlyReturn_InLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    while i < 100 {
        if i == 10 {
            return i
        }
        i = i + 1
    }
    return -1
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_IfElseIf_AllPathsReturn_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    if x < 0 {
        return 1
    } else if x < 10 {
        return 2
    } else if x < 100 {
        return 3
    } else {
        return 4
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_NestedIf_DeepNesting_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let y = 10
    if x > 0 {
        if y > 0 {
            if x < 10 {
                if y < 20 {
                    return 1
                }
            }
        }
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_WhileTrue_WithBreak_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    while true {
        count = count + 1
        if count >= 10 {
            break
        }
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ComplexCondition_InWhile_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    var y = 10
    var z = 5
    while (x < 10) && (y > 0 || z < 10) {
        x = x + 1
        y = y - 1
        z = z + 1
    }
    return x + y + z
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BreakContinue_InSameLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 20 {
        i = i + 1
        if i > 15 {
            break
        }
        if i == 10 {
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
    public void BuildIr_EmptyIf_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    if x > 0 {
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EmptyElse_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    if x < 0 {
        return 1
    } else {
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_IfInLoop_ReturnInIf_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    while i < 10 {
        if i == 5 {
            return i
        }
        i = i + 1
    }
    return -1
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleBreaks_SameLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    while i < 100 {
        if i == 10 {
            break
        }
        if i == 20 {
            break
        }
        if i == 30 {
            break
        }
        i = i + 1
    }
    return i
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleContinues_SameLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 10 {
        i = i + 1
        if i == 2 {
            continue
        }
        if i == 5 {
            continue
        }
        if i == 8 {
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
    public void BuildIr_IfElseChain_MixedReturns_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var result = 0
    let x = 5
    if x < 3 {
        result = 1
    } else if x < 7 {
        return 2
    } else if x < 10 {
        result = 3
    } else {
        return 4
    }
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Loop_NoIterationGuaranteed_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 10
    while i < 5 {
        sum = sum + i
        i = i + 1
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ConditionalAssignment_AllBranches_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    var result = 0
    if x < 0 {
        result = -1
    } else if x == 0 {
        result = 0
    } else {
        result = 1
    }
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ShortCircuit_AndOr_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a = true
    let b = false
    let c = true
    if a && (b || c) {
        return 1
    }
    if b || (a && c) {
        return 2
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReturnInAllBranches_NoFinalReturn_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    if x < 0 {
        return 1
    } else {
        return 2
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
