using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.Transforms.Passes;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for InlineExpansionPass to improve coverage from 0% to 50%+.
/// Tests the Transform method and helper methods (IsInlinable, IsRecursive).
/// </summary>
public class InlineExpansionPassTests
{
    private IrModule BuildIR(string source)
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
    public void InlineExpansionPass_EmptyModule_NoChanges()
    {
        var pass = new InlineExpansionPass();
        var module = new IrModule();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_SimpleFunction_NoChanges()
    {
        var source = @"
fn simple(x: i32) -> i32 {
    return x + 1
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        // Pass is skeleton, should return false
        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_MultipleFunctions_Inlines()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn mul(a: i32, b: i32) -> i32 {
    return a * b
}

fn compute(x: i32) -> i32 {
    var sum: i32 = add(x, 1)
    return mul(sum, 2)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        // add and mul should be inlined into compute
        Assert.True(changed);
    }

    [Fact]
    public void InlineExpansionPass_RecursiveFunction_NoChanges()
    {
        var source = @"
fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_SmallFunction_Inlines()
    {
        var source = @"
fn increment(x: i32) -> i32 {
    return x + 1
}

fn use_increment(y: i32) -> i32 {
    return increment(y)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        // increment should be inlined into use_increment
        Assert.True(changed);
    }

    [Fact]
    public void InlineExpansionPass_BorrowArgument_IsBoundWithoutCasting()
    {
        var reference = new IrReferenceType(IrIntType.I32);
        var identity = new IrFunction("identity", reference);
        identity.Parameters.Add(new IrParameter("value", reference));
        var identityBlock = new IrBasicBlock("entry");
        identityBlock.Instructions.Add(new IrReturn(new IrVariable("value", reference)));
        identity.BasicBlocks.Add(identityBlock);

        var main = new IrFunction("main", IrIntType.I32);
        var mainBlock = new IrBasicBlock("entry");
        var call = new IrCall("identity", reference, "%result");
        call.Arguments.Add(new IrBorrowValue(new IrVariable("source", IrIntType.I32), reference, false));
        mainBlock.Instructions.Add(call);
        mainBlock.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));
        main.BasicBlocks.Add(mainBlock);

        var module = new IrModule();
        module.Functions.Add(identity);
        module.Functions.Add(main);

        Assert.True(new InlineExpansionPass().Transform(module));
        Assert.DoesNotContain(mainBlock.Instructions, instruction => instruction is IrCall);
        Assert.Contains(mainBlock.Instructions.OfType<IrLocalDecl>(), declaration =>
            declaration.InitialValue is IrBorrowValue);
    }

    [Fact]
    public void InlineExpansionPass_UnsupportedInstruction_LeavesCallIntact()
    {
        var target = new IrFunction("mutate", IrIntType.I32);
        target.Parameters.Add(new IrParameter("value", IrIntType.I32));
        var targetBlock = new IrBasicBlock("entry");
        targetBlock.Instructions.Add(new IrStore("value", new IrConstant(1, IrIntType.I32)));
        targetBlock.Instructions.Add(new IrReturn(new IrVariable("value", IrIntType.I32)));
        target.BasicBlocks.Add(targetBlock);

        var main = new IrFunction("main", IrIntType.I32);
        var mainBlock = new IrBasicBlock("entry");
        var call = new IrCall("mutate", IrIntType.I32, "%result");
        call.Arguments.Add(new IrConstant(0, IrIntType.I32));
        mainBlock.Instructions.Add(call);
        mainBlock.Instructions.Add(new IrReturn(new IrVariable("%result", IrIntType.I32)));
        main.BasicBlocks.Add(mainBlock);

        var module = new IrModule();
        module.Functions.Add(target);
        module.Functions.Add(main);

        Assert.False(new InlineExpansionPass().Transform(module));
        Assert.Contains(call, mainBlock.Instructions);
    }

    [Fact]
    public void InlineExpansionPass_LargeFunction_NoChanges()
    {
        var source = @"
fn large_function(x: i32) -> i32 {
    var a: i32 = x + 1
    var b: i32 = a * 2
    var c: i32 = b - 3
    var d: i32 = c / 4
    var e: i32 = d + 5
    var f: i32 = e * 6
    var g: i32 = f - 7
    var h: i32 = g / 8
    var i: i32 = h + 9
    var j: i32 = i * 10
    return j
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_MainFunction_NoChanges()
    {
        var source = @"
fn main() -> i32 {
    return 0
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_NestedCalls_Inlines()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn double(x: i32) -> i32 {
    return add(x, x)
}

fn quadruple(x: i32) -> i32 {
    return double(double(x))
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        // add should be inlined into double, and double into quadruple
        Assert.True(changed);
    }

    [Fact]
    public void InlineExpansionPass_MutualRecursion_IsNotCloned()
    {
        var source = @"
fn is_even(n: i32) -> bool {
    if n == 0 {
        return true
    }
    return is_odd(n - 1)
}

fn is_odd(n: i32) -> bool {
    if n == 0 {
        return false
    }
    return is_even(n - 1)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_ComplexControlFlow_NoChanges()
    {
        var source = @"
fn complex(x: i32, y: i32) -> i32 {
    if x > 0 {
        if y > 0 {
            return x + y
        }
        return x - y
    }
    if y > 0 {
        return y - x
    }
    return 0
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_LoopFunction_NoChanges()
    {
        var source = @"
fn sum_to_n(n: i32) -> i32 {
    var sum: i32 = 0
    var i: i32 = 0
    while i <= n {
        sum = sum + i
        i = i + 1
    }
    return sum
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_StructReturn_IsNotCloned()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

fn make_point(x: i32, y: i32) -> Point {
    return Point { x: x, y: y }
}

fn use_point() -> Point {
    return make_point(1, 2)
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_EnumReturn_IsNotCloned()
    {
        var source = @"
pub enum Status {
    Active,
    Inactive,
}

fn get_active() -> Status {
    return Status::Active
}

fn check_status() -> Status {
    return get_active()
}";

        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void InlineExpansionPass_Name_ReturnsCorrectName()
    {
        var pass = new InlineExpansionPass();

        Assert.Equal("Inline Expansion", pass.Name);
    }

    #region IsInlinable Tests

    [Fact]
    public void IsInlinable_MainFunction_ReturnsFalse()
    {
        var source = @"
fn main() -> i32 {
    return 0
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var mainFunc = module.Functions.First(f => f.Name == "main");

        var result = pass.IsInlinable(mainFunc);

        Assert.False(result);
    }

    [Fact]
    public void IsInlinable_SmallSimpleFunction_ReturnsTrue()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var addFunc = module.Functions.First(f => f.Name == "add");

        var result = pass.IsInlinable(addFunc);

        Assert.True(result);
    }

    [Fact]
    public void IsInlinable_TwoInstructions_ReturnsTrue()
    {
        var source = @"
fn increment(x: i32) -> i32 {
    return x + 1
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "increment");

        var result = pass.IsInlinable(func);

        Assert.True(result);
    }

    [Fact]
    public void IsInlinable_LargeFunction_ReturnsFalse()
    {
        var source = @"
fn large_function(x: i32) -> i32 {
    var a: i32 = x + 1
    var b: i32 = a * 2
    var c: i32 = b - 3
    var d: i32 = c / 4
    var e: i32 = d + 5
    var f: i32 = e * 6
    var g: i32 = f - 7
    var h: i32 = g / 8
    var i: i32 = h + 9
    var j: i32 = i * 10
    var k: i32 = j + 11
    var l: i32 = k * 12
    var m: i32 = l - 13
    var n: i32 = m / 14
    var o: i32 = n + 15
    var p: i32 = o * 16
    var q: i32 = p - 17
    var r: i32 = q / 18
    var s: i32 = r + 19
    var t: i32 = s * 20
    return t
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "large_function");

        var result = pass.IsInlinable(func);

        // Should be false due to instruction count > 20
        Assert.False(result);
    }

    [Fact]
    public void IsInlinable_ComplexControlFlow_ReturnsFalse()
    {
        var source = @"
fn complex(x: i32, y: i32, z: i32) -> i32 {
    if x > 0 {
        if y > 0 {
            if z > 0 {
                return x + y + z
            }
            return x + y
        }
        return x
    }
    return 0
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "complex");

        var result = pass.IsInlinable(func);

        Assert.False(result);
    }

    [Fact]
    public void IsInlinable_SimpleIf_ReturnsFalse()
    {
        var source = @"
fn max(a: i32, b: i32) -> i32 {
    if a > b {
        return a
    }
    return b
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "max");

        var result = pass.IsInlinable(func);

        Assert.False(result);
    }

    [Fact]
    public void IsInlinable_RecursiveFunction_ReturnsFalse()
    {
        var source = @"
fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "factorial");

        var result = pass.IsInlinable(func);

        Assert.False(result);
    }

    [Fact]
    public void IsInlinable_FunctionWithLoop_ReturnsFalse()
    {
        var source = @"
fn sum_to_n(n: i32) -> i32 {
    var sum: i32 = 0
    var i: i32 = 0
    while i <= n {
        sum = sum + i
        i = i + 1
    }
    return sum
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "sum_to_n");

        var result = pass.IsInlinable(func);

        Assert.False(result);
    }

    #endregion

    #region IsRecursive Tests

    [Fact]
    public void IsRecursive_NonRecursiveFunction_ReturnsFalse()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "add");

        var result = pass.IsRecursive(func);

        Assert.False(result);
    }

    [Fact]
    public void IsRecursive_DirectRecursion_ReturnsTrue()
    {
        var source = @"
fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "factorial");

        var result = pass.IsRecursive(func);

        Assert.True(result);
    }

    [Fact]
    public void IsRecursive_TailRecursion_ReturnsTrue()
    {
        var source = @"
fn countdown(n: i32) -> i32 {
    if n <= 0 {
        return 0
    }
    return countdown(n - 1)
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "countdown");

        var result = pass.IsRecursive(func);

        Assert.True(result);
    }

    [Fact]
    public void IsRecursive_MultipleRecursiveCalls_ReturnsTrue()
    {
        var source = @"
fn fibonacci(n: i32) -> i32 {
    if n <= 1 {
        return n
    }
    return fibonacci(n - 1) + fibonacci(n - 2)
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "fibonacci");

        var result = pass.IsRecursive(func);

        Assert.True(result);
    }

    [Fact]
    public void IsRecursive_CallsOtherFunction_ReturnsFalse()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn compute(x: i32) -> i32 {
    return add(x, 1)
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var computeFunc = module.Functions.First(f => f.Name == "compute");

        var result = pass.IsRecursive(computeFunc);

        Assert.False(result);
    }

    [Fact]
    public void IsRecursive_NoFunctionCalls_ReturnsFalse()
    {
        var source = @"
fn square(x: i32) -> i32 {
    return x * x
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "square");

        var result = pass.IsRecursive(func);

        Assert.False(result);
    }

    [Fact]
    public void IsRecursive_ComplexRecursion_ReturnsTrue()
    {
        var source = @"
fn ackermann(m: i32, n: i32) -> i32 {
    if m == 0 {
        return n + 1
    }
    if n == 0 {
        return ackermann(m - 1, 1)
    }
    return ackermann(m - 1, ackermann(m, n - 1))
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "ackermann");

        var result = pass.IsRecursive(func);

        Assert.True(result);
    }

    #endregion

    #region Const Fn Inlining Tests

    [Fact]
    public void IsInlinable_SmallConstFn_ReturnsTrue()
    {
        var source = @"
const fn double(x: i32) -> i32 {
    return x * 2
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "double");

        var result = pass.IsInlinable(func);

        Assert.True(result);
    }

    [Fact]
    public void IsInlinable_ConstFn_LargerThreshold_ReturnsTrue()
    {
        // Create a const fn that's larger than MaxInlineInstructions (20) but smaller than MaxConstFnInlineInstructions (50)
        var source = @"
const fn process(x: i32) -> i32 {
    let a = x + 1
    let b = a + 2
    let c = b + 3
    let d = c + 4
    let e = d + 5
    let f = e + 6
    let g = f + 7
    let h = g + 8
    let i = h + 9
    let j = i + 10
    return j
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "process");

        var result = pass.IsInlinable(func);

        Assert.True(result);
    }

    [Fact]
    public void IsInlinable_ConstFnWithNoInline_ReturnsFalse()
    {
        var source = @"
@noinline
const fn double(x: i32) -> i32 {
    return x * 2
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "double");

        var result = pass.IsInlinable(func);

        Assert.False(result);
    }

    [Fact]
    public void IsInlinable_RecursiveConstFn_ReturnsFalse()
    {
        var source = @"
const fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();
        var func = module.Functions.First(f => f.Name == "factorial");

        var result = pass.IsInlinable(func);

        Assert.False(result);
    }

    [Fact]
    public void Transform_ConstFnCall_IsInlined()
    {
        var source = @"
const fn double(x: i32) -> i32 {
    return x * 2
}

fn main() -> i32 {
    let result = double(5)
    return result
}";
        var module = BuildIR(source);
        var pass = new InlineExpansionPass();

        var changed = pass.Transform(module);

        Assert.True(changed);

        // The call to double should be inlined
        var mainFn = module.Functions.First(f => f.Name == "main");
        var hasDoubleCall = mainFn.BasicBlocks
            .SelectMany(b => b.Instructions)
            .Any(i => i is IrCall call && call.FunctionName == "double");
        Assert.False(hasDoubleCall, "The call to double should have been inlined");
    }

    #endregion
}
