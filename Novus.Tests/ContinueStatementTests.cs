using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for continue and labeled loop statements
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

    private (bool HasErrors, IrBuilder Builder) BuildIrWithDiagnostics(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        builder.BuildModule(tree);
        return (builder.Diagnostics.HasErrors, builder);
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

    #region Continue with Postfix Condition

    [Fact]
    public void BuildIr_ContinueWithPostfixCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 10 {
        i += 1
        continue if i % 3 == 0  // Skip multiples of 3
        sum += i
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    #endregion

    #region Labeled Loop Positive Tests

    [Fact]
    public void BuildIr_LabeledWhileLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    outer: while true {
        var inner = 0
        while inner < 5 {
            count += 1
            if count == 7 {
                break outer
            }
            inner += 1
        }
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_LabeledForeverLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    outer: forever {
        forever {
            count += 1
            if count >= 10 {
                break outer
            }
        }
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_LabeledForLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    outer: for (var i = 0; i < 5; i++) {
        for (var j = 0; j < 5; j++) {
            if i == 2 && j == 3 {
                break outer
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
    public void BuildIr_ContinueWithLabel_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    outer: while i < 5 {
        i += 1
        var j = 0
        while j < 3 {
            j += 1
            if i == 3 && j == 2 {
                continue outer
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
    public void BuildIr_MultipleLabeledLoops_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var result = 0
    outer: while true {
        middle: while true {
            var x = 0
            while x < 3 {
                x += 1
                result += 1
                if result == 3 {
                    break middle
                }
                if result == 5 {
                    break outer
                }
            }
        }
    }
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BreakLabelWithPostfixCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    outer: while true {
        var k = 0
        while k < 100 {
            count += 1
            k += 1
            break outer if count == 5
        }
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ContinueLabelWithPostfixCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    outer: while i < 10 {
        i += 1
        var j = 0
        while j < 5 {
            j += 1
            continue outer if j == 3
            sum += 1
        }
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    #endregion

    #region Negative Tests - Error Cases

    [Fact]
    public void BuildIr_ContinueOutsideLoop_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    continue
    return 0
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("continue statement outside of loop"));
    }

    [Fact]
    public void BuildIr_BreakOutsideLoop_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    break
    return 0
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("break statement outside of loop"));
    }

    [Fact]
    public void BuildIr_ContinueWithUnknownLabel_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    while i < 10 {
        i += 1
        continue nonexistent
    }
    return i
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("unknown loop label 'nonexistent'"));
    }

    [Fact]
    public void BuildIr_BreakWithUnknownLabel_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    while i < 10 {
        i += 1
        break nonexistent
    }
    return i
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("unknown loop label 'nonexistent'"));
    }

    [Fact]
    public void BuildIr_DuplicateLoopLabel_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    outer: while true {
        outer: while true {
            break outer
        }
    }
    return 0
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("duplicate loop label 'outer'"));
    }

    [Fact]
    public void BuildIr_ContinueWithLabelOutsideScope_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    outer: while true {
        break
    }
    while true {
        continue outer  // outer is out of scope here
    }
    return 0
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("unknown loop label 'outer'"));
    }

    [Fact]
    public void BuildIr_BreakWithLabelOutsideScope_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    outer: while true {
        break
    }
    while true {
        break outer  // outer is out of scope here
    }
    return 0
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("unknown loop label 'outer'"));
    }

    [Fact]
    public void BuildIr_ContinueInFunction_OutsideLoop_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    if x > 3 {
        continue  // not inside a loop
    }
    return 0
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("continue statement outside of loop"));
    }

    [Fact]
    public void BuildIr_BreakInFunction_OutsideLoop_ReportsError()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    if x > 3 {
        break  // not inside a loop
    }
    return 0
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);
        Assert.True(hasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics, d => d.Message.Contains("break statement outside of loop"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void BuildIr_SameLabelInDifferentFunctions_Compiles()
    {
        var source = @"
fn foo() -> i32 {
    var sum = 0
    outer: while true {
        sum += 1
        if sum >= 10 {
            break outer
        }
    }
    return sum
}

pub fn main() -> i32 {
    var result = 0
    outer: while true {
        result += 1
        if result >= 5 {
            break outer
        }
    }
    return result + foo()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Equal(2, module.Functions.Count);
    }

    [Fact]
    public void BuildIr_DeeplyNestedLabeledLoops_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    level1: while true {
        level2: while true {
            level3: while true {
                sum += 1
                if sum == 5 {
                    break level2
                }
                if sum == 10 {
                    break level1
                }
            }
        }
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BreakInnerAndContinueOuter_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    outer: while i < 5 {
        i += 1
        var j = 0
        inner: while j < 10 {
            j += 1
            if j == 3 {
                break inner
            }
            if i == 2 {
                continue outer
            }
            sum += 1
        }
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    #endregion
}
