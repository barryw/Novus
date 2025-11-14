using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class PostfixConditionalTests
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

    // ========================================
    // Return Statement Tests
    // ========================================

    [Fact]
    public void BuildIr_ReturnWithPostfixIf_SimpleCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let y = 10
    return 42 if x < y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(mainFunc);

        // Verify that conditional branch instructions are present
        var hasBranch = mainFunc.BasicBlocks.SelectMany(bb => bb.Instructions).Any(i => i is IrConditionalBranch);
        Assert.True(hasBranch, "Expected conditional branch for postfix if");
    }

    [Fact]
    public void BuildIr_ReturnWithPostfixUnless_SimpleCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let y = 10
    return 42 unless x > y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunc = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(mainFunc);

        // Verify that conditional branch instructions are present
        var hasBranch = mainFunc.BasicBlocks.SelectMany(bb => bb.Instructions).Any(i => i is IrConditionalBranch);
        Assert.True(hasBranch, "Expected conditional branch for postfix unless");
    }

    [Fact]
    public void BuildIr_ReturnWithPostfixIf_ComplexCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let y = 10
    let z = 15
    return 42 if x < y && y < z
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReturnWithPostfixUnless_ComplexCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let y = 10
    let z = 15
    return 42 unless x > y || y > z
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleReturnsWithPostfixConditions_Compiles()
    {
        var source = @"
fn classify(x: i32) -> i32 {
    return -1 if x < 0
    return 0 if x == 0
    return 1 if x > 0
    return 99
}

pub fn main() -> i32 {
    return classify(5)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReturnWithoutValue_PostfixIf_Compiles()
    {
        var source = @"
fn early_exit(x: i32) {
    return if x < 0
}

pub fn main() -> i32 {
    early_exit(-5)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReturnWithoutValue_PostfixUnless_Compiles()
    {
        var source = @"
fn early_exit(x: i32) {
    return unless x >= 0
}

pub fn main() -> i32 {
    early_exit(-5)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ========================================
    // Break Statement Tests
    // ========================================

    [Fact]
    public void BuildIr_BreakWithPostfixIf_InLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    var i = 0
    while i < 100 {
        i = i + 1
        break if i == 50
        count = count + 1
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BreakWithPostfixUnless_InLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    var i = 0
    while i < 100 {
        i = i + 1
        break unless i < 50
        count = count + 1
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_BreakWithPostfixCondition_NestedLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var found = 0
    var i = 0
    while i < 10 {
        var j = 0
        while j < 10 {
            j = j + 1
            break if i == 5 && j == 5
            found = found + 1
        }
        i = i + 1
    }
    return found
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ========================================
    // Assignment Statement Tests
    // ========================================

    [Fact]
    public void BuildIr_AssignmentWithPostfixIf_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var result = 0
    let x = 5
    let y = 10
    result = 42 if x < y
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_AssignmentWithPostfixUnless_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var result = 0
    let x = 5
    let y = 10
    result = 42 unless x > y
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_CompoundAssignmentWithPostfixIf_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 10
    let condition = true
    sum += 5 if condition
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_CompoundAssignmentWithPostfixUnless_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 10
    let condition = false
    sum += 5 unless condition
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_IncrementWithPostfixIf_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    let should_increment = true
    count++ if should_increment
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_DecrementWithPostfixUnless_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 10
    let should_skip = false
    count-- unless should_skip
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ========================================
    // Expression Statement Tests
    // ========================================

    [Fact]
    public void BuildIr_FunctionCallWithPostfixIf_Compiles()
    {
        var source = @"
fn side_effect(x: i32) -> i32 {
    return x * 2
}

pub fn main() -> i32 {
    let condition = true
    side_effect(5) if condition
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_FunctionCallWithPostfixUnless_Compiles()
    {
        var source = @"
fn side_effect(x: i32) -> i32 {
    return x * 2
}

pub fn main() -> i32 {
    let condition = false
    side_effect(5) unless condition
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ========================================
    // Edge Cases and Complex Scenarios
    // ========================================

    [Fact]
    public void BuildIr_PostfixCondition_WithNegation_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let flag = true
    return 42 if !flag
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_WithComparison_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    return 42 if x == 5
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_WithFunctionCall_Compiles()
    {
        var source = @"
fn is_valid(x: i32) -> bool {
    return x > 0
}

pub fn main() -> i32 {
    let x = 5
    return 42 if is_valid(x)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MixedPostfixConditions_IfAndUnless_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var result = 0
    let x = 5
    let y = 10

    result = 1 if x < y
    result = 2 unless x > y
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_InNestedScopes_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let outer = 5
    {
        let inner = 10
        return 42 if outer < inner
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_WithStructField_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

pub fn main() -> i32 {
    let p = Point { x: 5, y: 10 }
    return 42 if p.x < p.y
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_WithArrayElement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr: [i32; 3] = [1, 2, 3]
    return 42 if arr[0] < arr[2]
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_WithPointerDeref_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let ptr = &x
    return 42 if *ptr == 5
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_InForLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for i in 0..10 {
        sum += i if i % 2 == 0
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_WithBitwiseOperators_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let flags = 0b1010
    let masked = flags & 0b0010
    return 42 if masked != 0
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_WithArithmeticExpression_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    let y = 10
    return 42 if (x + y) > 10
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ========================================
    // Multiple Postfix Conditions in Same Function
    // ========================================

    [Fact]
    public void BuildIr_MultiplePostfixConditions_Sequential_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var result = 0
    let x = 5
    let y = 10
    let z = 15

    result = 1 if x < y
    result = 2 if y < z
    result = 3 if x < z

    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultiplePostfixConditions_WithBreaks_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var count = 0
    var i = 0
    while i < 100 {
        i = i + 1
        break if i == 30
        break if i == 60
        break if i == 90
        count = count + 1
    }
    return count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ========================================
    // Boolean Type Conditions
    // ========================================

    [Fact]
    public void BuildIr_PostfixIf_BooleanVariable_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let flag = true
    return 42 if flag
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixUnless_BooleanVariable_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let flag = false
    return 42 unless flag
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_BooleanLiteral_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 42 if true
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ========================================
    // Integration with Other Control Flow
    // ========================================

    [Fact]
    public void BuildIr_PostfixCondition_WithRegularIf_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    if x > 0 {
        return 42 if x < 10
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PostfixCondition_InsideMatch_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 5
    match x {
        0 => {
            return 1
        }
        5 => {
            return 42 if x == 5
        }
        _ => {
            return 0
        }
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
