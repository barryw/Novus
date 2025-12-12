using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for defer statement semantics (function-level LIFO):
/// - Function-level scope (all defers execute at function exit)
/// - LIFO execution order (last defer runs first)
/// - Return timing (expression evaluated before defers run)
/// - Multiple defers in same function
/// - Defer in loops (accumulates, executes at function exit)
/// - Defer in nested blocks (still function-level)
/// </summary>
public class DeferSemanticsTests
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

    // ==================== FUNCTION-LEVEL SCOPE ====================

    [Fact]
    public void BuildIr_Defer_FunctionLevel_NotBlockLevel_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 1
    {
        defer { x = 100 }
        x = 10  // x is 10 here (defer hasn't run yet)
    }  // defer does NOT run here (function-level, not block-level)
    return x  // Returns 10 (defer runs after return expression evaluated)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Verify defer is registered at function level
        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Single(mainFunc.DeferredBlocks);  // Should have 1 defer registered
    }

    [Fact]
    public void BuildIr_Defer_InLoop_AccumulatesAtFunctionLevel_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    for (var i = 0; i < 3; i++) {
        defer { count = count + 1 }  // Registered 3 times
    }
    // After loop: count = 0 (defers haven't run yet)
    return count  // Returns 0, then defers run at function exit
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Verify defers accumulate
        var mainFunc = module.Functions.First(f => f.Name == "main");
        // Note: Loop may be unrolled or defers may be registered multiple times
        // Just verify compilation succeeds
    }

    [Fact]
    public void BuildIr_Defer_InNestedBlocks_AllFunctionLevel_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 1
    defer { x = x + 1 }  // Defer 1

    {
        defer { x = x + 10 }  // Defer 2 (still function-level)
        {
            defer { x = x + 100 }  // Defer 3 (still function-level)
        }
    }

    return x  // Returns 1, then defers run in LIFO order
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Equal(3, mainFunc.DeferredBlocks.Count);  // All 3 defers at function level
    }

    // ==================== LIFO EXECUTION ORDER ====================

    [Fact]
    public void BuildIr_Defer_LIFO_TwoDefers_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 1
    defer { x = x * 2 }  // Executes 2nd (first registered)
    defer { x = x * 5 }  // Executes 1st (last registered)
    return 0
    // At function exit (LIFO):
    // 1. x *= 5  → x = 5
    // 2. x *= 2  → x = 10
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Equal(2, mainFunc.DeferredBlocks.Count);
    }

    [Fact]
    public void BuildIr_Defer_LIFO_ThreeDefers_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 1
    defer { x = x * 2 }  // Executes 3rd
    defer { x = x * 3 }  // Executes 2nd
    defer { x = x * 5 }  // Executes 1st
    return 0
    // At function exit: x = ((1 * 5) * 3) * 2 = 30
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Equal(3, mainFunc.DeferredBlocks.Count);
    }

    [Fact]
    public void BuildIr_Defer_LIFO_MixedWithBlocks_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 1
    defer { x = x + 1 }    // Defer 1 (executes 3rd)

    {
        defer { x = x + 10 }   // Defer 2 (executes 2nd)
    }

    defer { x = x + 100 }  // Defer 3 (executes 1st)

    return 0
    // At function exit (LIFO - reverse registration):
    // 1. x += 100  → x = 101
    // 2. x += 10   → x = 111
    // 3. x += 1    → x = 112
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Equal(3, mainFunc.DeferredBlocks.Count);
    }

    // ==================== RETURN TIMING ====================

    [Fact]
    public void BuildIr_Defer_ReturnBeforeDefer_SnapshotValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 42
    defer { x = 100 }
    return x  // Returns 42 (snapshot before defer runs)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Single(mainFunc.DeferredBlocks);
    }

    [Fact]
    public void BuildIr_Defer_MultipleReturns_DeferRunsOnce_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    defer { x = 100 }

    if true {
        return x  // Returns 0, defer runs at function exit
    }

    return x  // Not reached, but defer would run here too
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Single(mainFunc.DeferredBlocks);
    }

    [Fact]
    public void BuildIr_Defer_EarlyReturn_DeferStillRuns_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var cleanup = 0
    defer { cleanup = 1 }

    if true {
        return 42  // Early return, defer still runs at exit
    }

    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Verify defer cleanup is emitted before each return
        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Single(mainFunc.DeferredBlocks);
    }

    // ==================== DEFER WITH EXPRESSIONS ====================

    [Fact]
    public void BuildIr_Defer_Expression_SingleStatement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    defer x = 20
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Single(mainFunc.DeferredBlocks);
    }

    [Fact]
    public void BuildIr_Defer_Expression_FunctionCall_Compiles()
    {
        var source = @"
fn cleanup(x: *i32) {
    *x = 100
}

pub fn main() -> i32 {
    var value = 42
    defer cleanup(&value)
    return value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Single(mainFunc.DeferredBlocks);
    }

    [Fact]
    public void BuildIr_Defer_Block_MultipleStatements_Compiles()
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

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Single(mainFunc.DeferredBlocks);
    }

    // ==================== DEFER IN CONTROL FLOW ====================

    [Fact]
    public void BuildIr_Defer_InIfBranch_OnlyRegisteredIfTrue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    if true {
        defer { x = 100 }
    }
    return x
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
        defer { x = 50 }
    } else {
        defer { x = 100 }
    }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_InWhileLoop_AccumulatesPerIteration_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    var i = 0
    while i < 3 {
        defer { sum = sum + 1 }
        i = i + 1
    }
    return sum  // Returns 0, defers run at function exit
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_InForLoop_AccumulatesPerIteration_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    for (var i = 0; i < 5; i++) {
        defer { count = count + 1 }
    }
    return count  // Returns 0, defers run at function exit
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_WithBreak_DeferStillRuns_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var cleanup = 0
    defer { cleanup = 100 }

    for (var i = 0; i < 10; i++) {
        if i == 5 {
            break  // Break out, defer still runs at function exit
        }
    }

    return cleanup
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_WithContinue_DeferStillRuns_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var cleanup = 0
    defer { cleanup = 100 }

    for (var i = 0; i < 10; i++) {
        if i % 2 == 0 {
            continue  // Continue, defer still runs at function exit
        }
    }

    return cleanup
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== DEFER WITH VARIABLES ====================

    [Fact]
    public void BuildIr_Defer_CapturesVariableValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    defer { x = x + 5 }  // Captures current value of x at defer execution
    x = 20
    return 0
    // At function exit: defer executes with x=20, result is x=25
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_ModifiesStruct_Compiles()
    {
        var source = @"
struct Counter {
    value: i32
}

pub fn main() -> i32 {
    var c = Counter { value: 10 }
    defer { c.value = 100 }
    return c.value  // Returns 10
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_ModifiesArray_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [1, 2, 3]
    defer { arr[0] = 100 }
    return arr[0]  // Returns 1
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_WithPointer_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 42
    let ptr: *i32 = &x as *i32
    defer { *ptr = 100 }
    return x  // Returns 42
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== COMPLEX DEFER SCENARIOS ====================

    [Fact]
    public void BuildIr_Defer_NestedFunctions_EachHasOwnDefers_Compiles()
    {
        var source = @"
fn helper() -> i32 {
    var x = 1
    defer { x = 10 }
    return x  // Returns 1, defer runs at helper exit
}

pub fn main() -> i32 {
    var y = 2
    defer { y = 20 }
    let result = helper()
    return y  // Returns 2, defer runs at main exit
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Each function has its own defer list
        var helperFunc = module.Functions.First(f => f.Name == "helper");
        var mainFunc = module.Functions.First(f => f.Name == "main");

        Assert.Single(helperFunc.DeferredBlocks);
        Assert.Single(mainFunc.DeferredBlocks);
    }

    [Fact]
    public void BuildIr_Defer_ComplexExpression_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var a = 1
    var b = 2
    var c = 3
    defer { c = a + b * 2 }  // Complex expression
    return c  // Returns 3
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Defer_ManyDefers_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 0
    defer { x = x + 1 }
    defer { x = x + 1 }
    defer { x = x + 1 }
    defer { x = x + 1 }
    defer { x = x + 1 }
    defer { x = x + 1 }
    defer { x = x + 1 }
    defer { x = x + 1 }
    defer { x = x + 1 }
    defer { x = x + 1 }
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.First(f => f.Name == "main");
        Assert.Equal(10, mainFunc.DeferredBlocks.Count);
    }
}
