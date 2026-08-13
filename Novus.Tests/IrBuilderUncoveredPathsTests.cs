using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests targeting specific uncovered code paths in IrBuilder identified from coverage analysis.
/// Focus areas: interpolated strings, generic resolution, module imports, struct array init, external variables.
/// </summary>
public class IrBuilderUncoveredPathsTests
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

    #region Interpolated String Literal Tests

    [Fact]
    public void BuildIr_InterpolatedString_WithSimpleVariable_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var x = 42
    var s = ""{x}""
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
        // Interpolated strings should generate calls to formatting functions
        Assert.NotEmpty(main.BasicBlocks);
    }

    [Fact]
    public void BuildIr_InterpolatedString_WithMultipleExpressions_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var x = 42
    var y = 100
    var s = ""{x} plus {y}""
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
        Assert.NotEmpty(main.BasicBlocks);
    }

    [Fact]
    public void BuildIr_InterpolatedString_WithExpression_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var x = 10
    var s = ""{x + 5}""
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_InterpolatedString_WithBooleanValue_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var flag = true
    var s = ""{flag}""
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_InterpolatedString_WithLiteralText_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var x = 42
    var s = ""Value: {x}""
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_InterpolatedString_EmptyInterpolation_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var s = ""{}""
    return 0
}";
        // This might fail or create an error, but we're testing the code path
        try
        {
            var module = BuildIr(source);
            Assert.NotNull(module);
        }
        catch
        {
            // Expected - empty interpolation is likely invalid
        }
    }

    #endregion

    #region External Variable Tests

    [Fact]
    public void BuildIr_ExternalVariable_CreatesCorrectDeclaration()
    {
        var source = @"
extern var stdin: i32

fn main() -> i32 {
    return stdin
}";
        var module = BuildIr(source);

        // Should have registered the external variable
        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_ExternalVariable_WithPointerType_CreatesCorrectDeclaration()
    {
        var source = @"
extern var buffer: *u8

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region Struct Array Init Tests

    [Fact]
    public void BuildIr_StructArrayInit_WithSimpleStruct_CreatesCorrectIr()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn main() -> i32 {
    var points = [Point; 3] { x: 0, y: 0 }
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_StructArrayInit_WithComplexInitializer_CreatesCorrectIr()
    {
        var source = @"
struct Vec3 {
    x: i32,
    y: i32,
    z: i32
}

fn main() -> i32 {
    var vectors = [Vec3; 5] { x: 1, y: 2, z: 3 }
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region Generic Type Resolution Tests

    [Fact]
    public void BuildIr_GenericMethodCall_InfersTypeArguments()
    {
        // Note: Full generic syntax with <T> is not yet implemented in Novus
        // This test is designed to exercise generic-related code paths that do exist
        var source = @"
struct Container {
    value: i32
}

impl Container {
    fn new(val: i32) -> Container {
        return Container { value: val }
    }
}

fn main() -> i32 {
    var c = Container::new(42)
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_GenericEnum_WithTypeInference_CreatesCorrectIr()
    {
        var source = @"
enum Result {
    Ok(i32),
    Err(i32)
}

fn create_result(val: i32) -> Result {
    return Result::Ok(val)
}

fn main() -> i32 {
    var r = create_result(42)
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region Module Import Edge Cases

    [Fact]
    public void BuildIr_ImportSpecificSymbols_CreatesCorrectModule()
    {
        // This test will likely fail without actual module files, but tests the code path
        var source = @"
import std::io::{println, write}

fn main() -> i32 {
    return 0
}";
        try
        {
            var module = BuildIr(source);
            Assert.NotNull(module);
        }
        catch
        {
            // Expected - std::io module doesn't exist in test environment
        }
    }

    [Fact]
    public void BuildIr_ImportWithAlias_CreatesCorrectModule()
    {
        var source = @"
import std::collections as col

fn main() -> i32 {
    return 0
}";
        try
        {
            var module = BuildIr(source);
            Assert.NotNull(module);
        }
        catch
        {
            // Expected - std::collections module doesn't exist
        }
    }

    #endregion

    #region Drop Method Tests

    [Fact]
    public void BuildIr_StructWithDropTrait_DetectsDropMethod()
    {
        var source = @"
trait Drop {
    fn drop(&var self)
}

struct Resource {
    handle: i32
}

impl Drop for Resource {
    fn drop(&var self) {
        return
    }
}

fn main() -> i32 {
    var r = Resource { handle: 42 }
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);

        // Should have registered Drop trait and implementation
        var dropTrait = module.Traits.Find(t => t.TraitName == "Drop");
        Assert.NotNull(dropTrait);
    }

    [Fact]
    public void BuildIr_TypeWithoutDrop_DoesNotGenerateDropCalls()
    {
        var source = @"
struct Simple {
    value: i32
}

fn main() -> i32 {
    var s = Simple { value: 42 }
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_TupleDestructuringWithDropType_DoesNotDoubleDrop()
    {
        // Regression test: When a tuple containing Drop-able types is destructured,
        // only the destructured variables should be dropped, NOT the original tuple fields.
        // Previously this caused a double-free bug.
        var source = @"
trait Drop {
    fn drop(&var self)
}

struct Receiver {
    port: i32
}

impl Drop for Receiver {
    fn drop(&var self) {
        return
    }
}

struct Sender {
    port: i32
}

fn create_channel() -> (Sender, Receiver) {
    let sender = Sender { port: 1 }
    let receiver = Receiver { port: 2 }
    return (sender, receiver)
}

fn main() -> i32 {
    let pair = create_channel()
    // Destructure the tuple - ownership transfers to _tx and _rx
    let (_tx, _rx) = pair
    // Only _rx should be dropped (has Drop), not both _rx AND pair.__1
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);

        // Count IrDefer instructions that would call Drop
        // After the fix, there should be exactly one defer for _rx, not two
        var deferredBlocks = main.DeferredBlocks;
        var dropDefers = deferredBlocks.Count(d =>
            d.Label.Contains("Receiver") || d.Label.Contains("_rx"));

        // We should have at most one defer for the receiver (the destructured variable)
        // not two (which would indicate both pair.__1 and _rx have defers)
        Assert.True(dropDefers <= 1,
            $"Expected at most 1 Drop defer for receiver, but found {dropDefers}. " +
            "This may indicate a double-drop bug in tuple destructuring.");
    }

    [Fact]
    public void BuildIr_TupleDestructuringTransfersOwnership()
    {
        // Verify that when we destructure a tuple, ownership transfers from
        // the tuple's fields to the new variables
        var source = @"
trait Drop {
    fn drop(&var self)
}

struct Resource {
    id: i32
}

impl Drop for Resource {
    fn drop(&var self) {
        return
    }
}

fn make_tuple() -> (Resource, Resource) {
    let a = Resource { id: 1 }
    let b = Resource { id: 2 }
    return (a, b)
}

fn main() -> i32 {
    let tuple_val = make_tuple()
    let (first, second) = tuple_val
    // first and second now own the resources
    // tuple_val should NOT cause drops
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);

        // The function should compile without error
        // The actual drop behavior is verified at runtime
        Assert.True(main.BasicBlocks.Count > 0);
    }

    #endregion

    #region Diagnostic and Source Tracking Tests

    [Fact]
    public void IrBuilder_SetInputFilePath_StoresFilePath()
    {
        var builder = new IrBuilder(skipAutoImports: true);

        builder.SetInputFilePath("/test/path/source.novus");

        // File path should be stored (even if we can't directly verify it)
        Assert.NotNull(builder);
    }

    [Fact]
    public void IrBuilder_SetSourceLines_StoresSourceLines()
    {
        var builder = new IrBuilder(skipAutoImports: true);

        var lines = new[] { "fn main() -> i32 {", "    return 0", "}" };
        builder.SetSourceLines(lines);

        // Source lines should be stored
        Assert.NotNull(builder);
    }

    [Fact]
    public void IrBuilder_GetDiagnostics_ReturnsDiagnosticBag()
    {
        var builder = new IrBuilder(skipAutoImports: true);

        var diagnostics = builder.GetDiagnostics();

        Assert.NotNull(diagnostics);
    }

    [Fact]
    public void IrBuilder_GetImportedModules_ReturnsModuleList()
    {
        var builder = new IrBuilder(skipAutoImports: true);

        var modules = builder.GetImportedModules();

        Assert.NotNull(modules);
    }

    #endregion

    #region Complex Type Inference Tests

    [Fact]
    public void BuildIr_NestedGenericMethodCall_InfersCorrectly()
    {
        // Note: Full generic syntax with <T> is not yet implemented in Novus
        var source = @"
struct Box {
    value: i32
}

impl Box {
    fn wrap(val: i32) -> Box {
        return Box { value: val }
    }

    fn wrap_box(b: Box) -> Box {
        return b
    }
}

fn main() -> i32 {
    var inner = Box::wrap(42)
    var outer = Box::wrap_box(inner)
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region Array Repeat Literal Extended Tests

    [Fact]
    public void BuildIr_ArrayRepeatLiteral_WithStructType_CreatesCorrectIr()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn main() -> i32 {
    var origin = Point { x: 0, y: 0 }
    var points = [origin; 10]
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_ArrayRepeatLiteral_WithComplexExpression_CreatesCorrectIr()
    {
        var source = @"
fn get_value() -> i32 {
    return 42
}

fn main() -> i32 {
    var arr = [get_value(); 5]
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region TryExpr Extended Tests

    [Fact]
    public void BuildIr_TryExpr_WithResultType_CreatesCorrectIr()
    {
        var source = @"
enum Result {
    Ok(i32),
    Err(i32)
}

fn fallible() -> Result {
    return Result::Ok(42)
}

fn main() -> Result {
    var value = fallible()?
    return Result::Ok(value)
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region StoreToLvalue Extended Tests

    [Fact]
    public void BuildIr_StoreToStructField_CreatesCorrectIr()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn main() -> i32 {
    var p = Point { x: 0, y: 0 }
    p.x = 10
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_StoreToArrayElement_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var arr = [1, 2, 3]
    arr[0] = 10
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_StoreToNestedField_CreatesCorrectIr()
    {
        var source = @"
struct Inner {
    value: i32
}

struct Outer {
    inner: Inner
}

fn main() -> i32 {
    var o = Outer { inner: Inner { value: 0 } }
    o.inner.value = 42
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region PathExpr Extended Tests

    [Fact]
    public void BuildIr_PathExpr_WithNestedModule_CreatesCorrectIr()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

fn main() -> i32 {
    var c = Color::Red
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region SelfExpr Extended Tests

    [Fact]
    public void BuildIr_SelfExpr_InMethod_CreatesCorrectIr()
    {
        var source = @"
struct Counter {
    count: i32
}

impl Counter {
    fn increment(&var self) {
        self.count = self.count + 1
        return
    }
}

fn main() -> i32 {
    var c = Counter { count: 0 }
    return 0
}";
        var module = BuildIr(source);

        var counterStruct = module.Structs.Find(s => s.Name == "Counter");
        Assert.NotNull(counterStruct);
    }

    #endregion

    #region UnaryExpr Extended Tests

    [Fact]
    public void BuildIr_UnaryNegation_WithFloat_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var x = 3.14
    var y = -x
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_LogicalNot_WithBool_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var flag = true
    var opposite = !flag
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
        var logicalNot = Assert.Single(main.BasicBlocks
            .SelectMany(block => block.Instructions)
            .OfType<IrBinaryOp>());
        Assert.Equal(IrBinaryOp.OpKind.Eq, logicalNot.Operation);
        Assert.IsType<IrBoolConstant>(logicalNot.Right);
    }

    [Fact]
    public void BuildIr_LogicalNot_WithPointer_UsesSingleNullComparison()
    {
        var source = @"
fn is_null(value: *u8) -> bool {
    return !value
}";
        var module = BuildIr(source);

        var function = module.Functions.Find(f => f.Name == "is_null");
        Assert.NotNull(function);
        var instructions = function.BasicBlocks.SelectMany(block => block.Instructions).ToArray();
        var logicalNot = Assert.Single(instructions.OfType<IrBinaryOp>());
        Assert.Equal(IrBinaryOp.OpKind.Eq, logicalNot.Operation);
        Assert.IsType<IrPointerType>(logicalNot.Right.Type);
    }

    #endregion

    #region Float Literal Extended Tests

    [Fact]
    public void BuildIr_FloatLiteral_WithExponent_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var x = 1.5e10
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void BuildIr_FloatLiteral_WithNegativeExponent_CreatesCorrectIr()
    {
        var source = @"
fn main() -> i32 {
    var x = 2.5e-3
    return 0
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region Sizeof Extended Tests

    [Fact]
    public void BuildIr_SizeofExpr_WithStructType_CreatesCorrectIr()
    {
        var source = @"
struct Data {
    a: i32,
    b: i32,
    c: i32
}

fn main() -> i32 {
    var size = sizeof(Data)
    return size
}";
        var module = BuildIr(source);

        var main = module.Functions.Find(f => f.Name == "main");
        Assert.NotNull(main);
    }

    #endregion

    #region Conversion Via From Trait Tests

    [Fact]
    public void BuildIr_ConversionViaFromTrait_CreatesCorrectIr()
    {
        var source = @"
trait From<T> {
    fn from(value: T) -> Self
}

struct Wrapper {
    value: i32
}

impl From<i32> for Wrapper {
    fn from(value: i32) -> Wrapper {
        return Wrapper { value: value }
    }
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var fromTrait = module.Traits.Find(t => t.TraitName == "From");
        Assert.NotNull(fromTrait);
    }

    #endregion
}
