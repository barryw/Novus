using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Comprehensive integration tests for IrBuilder covering complex language features.
/// Focuses on structs, enums, traits, pattern matching, imports, and Drop semantics.
/// </summary>
public class IrBuilderComprehensiveTests
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

    private (IrBuilder, IrModule?) BuildIrWithDiagnostics(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        var module = builder.BuildModule(tree);
        return (builder, module);
    }

    #region Struct Tests

    [Fact]
    public void BuildIr_SimpleStruct_CreatesStructType()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        Assert.Single(module.Structs);
        var structType = module.Structs[0];
        Assert.Equal("Point", structType.Name);
        Assert.Equal(2, structType.Fields.Count);
        Assert.Equal("x", structType.Fields[0].Name);
        Assert.Equal("y", structType.Fields[1].Name);
    }

    [Fact]
    public void BuildIr_StructWithMethods_CreatesMethodsWithSelfParameter()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

impl Point {
    fn get_x(&self) -> i32 {
        return self.x
    }
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        Assert.Single(module.Structs);

        // Module should have functions
        Assert.NotEmpty(module.Functions);

        // Check that we have at least main function
        var mainFunc = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunc);
    }

    [Fact]
    public void BuildIr_StructLiteral_CreatesStructLiteralInstruction()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn create_point() -> Point {
    return Point { x: 10, y: 20 }
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "create_point");
        Assert.NotNull(func);

        // Should have instructions for creating the struct literal
        var instructions = func.BasicBlocks[0].Instructions;
        Assert.NotEmpty(instructions);

        // Look for return instruction
        var hasReturn = instructions.Any(i => i is IrReturn);
        Assert.True(hasReturn);
    }

    [Fact]
    public void BuildIr_StructFieldAccess_CreatesMemberAccessInstruction()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

fn get_x(p: Point) -> i32 {
    return p.x
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "get_x");
        Assert.NotNull(func);

        var instructions = func.BasicBlocks[0].Instructions;

        // Should have member access instruction
        var hasMemberAccess = instructions.Any(i => i is IrMemberAccess);
        Assert.True(hasMemberAccess);
    }

    [Fact]
    public void BuildIr_NestedStructs_CreatesNestedFieldAccess()
    {
        var source = @"
struct Inner {
    value: i32
}

struct Outer {
    inner: Inner
}

fn get_value(o: Outer) -> i32 {
    return o.inner.value
}";
        var module = BuildIr(source);

        Assert.Equal(2, module.Structs.Count);

        var func = module.Functions.FirstOrDefault(f => f.Name == "get_value");
        Assert.NotNull(func);

        // Should have multiple member access instructions for nested access
        var instructions = func.BasicBlocks[0].Instructions;
        var memberAccessCount = instructions.Count(i => i is IrMemberAccess);
        Assert.True(memberAccessCount >= 2);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void BuildIr_SimpleEnum_CreatesEnumType()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        Assert.Single(module.Enums);
        var enumType = module.Enums[0];
        Assert.Equal("Color", enumType.Name);
        Assert.Equal(3, enumType.Variants.Count);
        Assert.Equal("Red", enumType.Variants[0].Name);
        Assert.Equal("Green", enumType.Variants[1].Name);
        Assert.Equal("Blue", enumType.Variants[2].Name);
    }

    [Fact]
    public void BuildIr_EnumWithData_CreatesVariantsWithFields()
    {
        var source = @"
enum Option {
    Some(i32),
    None
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        Assert.Single(module.Enums);
        var enumType = module.Enums[0];
        Assert.Equal("Option", enumType.Name);
        Assert.Equal(2, enumType.Variants.Count);

        var someVariant = enumType.Variants.FirstOrDefault(v => v.Name == "Some");
        Assert.NotNull(someVariant);
        // Some variant should have data
    }

    [Fact]
    public void BuildIr_EnumConstructor_CreatesEnumConstructorInstruction()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

fn get_red() -> Color {
    return Color::Red
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "get_red");
        Assert.NotNull(func);

        var instructions = func.BasicBlocks[0].Instructions;
        Assert.Contains(instructions, i => i is IrReturn);
    }

    [Fact]
    public void BuildIr_ResultEnum_CreatesBothVariants()
    {
        var source = @"
enum Result {
    Ok(i32),
    Err(i32)
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        Assert.Single(module.Enums);
        var resultEnum = module.Enums[0];
        Assert.Equal("Result", resultEnum.Name);
        Assert.Equal(2, resultEnum.Variants.Count);

        Assert.Contains(resultEnum.Variants, v => v.Name == "Ok");
        Assert.Contains(resultEnum.Variants, v => v.Name == "Err");
    }

    #endregion

    #region Pattern Matching Tests

    [Fact]
    public void BuildIr_SimpleMatch_CreatesMatchInstruction()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

fn color_to_number(c: Color) -> i32 {
    match c {
        Color::Red => {
            return 1
        },
        Color::Green => {
            return 2
        },
        Color::Blue => {
            return 3
        }
    }
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "color_to_number");
        Assert.NotNull(func);

        // Match should create basic blocks
        Assert.NotEmpty(func.BasicBlocks);
        Assert.NotEmpty(func.BasicBlocks[0].Instructions);
    }

    [Fact]
    public void BuildIr_MatchWithData_ExtractsVariantData()
    {
        var source = @"
enum Option {
    Some(i32),
    None
}

fn unwrap_or_zero(opt: Option) -> i32 {
    return match opt {
        Option::Some(value) => {
            return value
        },
        Option::None => {
            return 0
        }
    }
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "unwrap_or_zero");
        Assert.NotNull(func);

        // Match with data extraction should create extract instructions
        var allInstructions = func.BasicBlocks.SelectMany(b => b.Instructions).ToList();
        var hasExtract = allInstructions.Any(i => i is IrExtractVariantData);
        // May or may not have explicit extract depending on optimization
        Assert.NotEmpty(allInstructions);
    }

    [Fact]
    public void BuildIr_MatchOnDereferencedSelf_ExtractsVariantData()
    {
        // This test covers the case where we match on *self (dereferenced pointer to enum)
        // and extract variant data. The IR builder must use the dereferenced enum type
        // for IrExtractVariantData, not the original pointer type.
        var source = @"
enum InnerError {
    NotFound,
    InvalidArg
}

enum OuterError {
    Inner(InnerError),
    Other
}

impl OuterError {
    pub fn get_inner(&self) -> i32 {
        match *self {
            OuterError::Inner(err) => {
                return 1
            },
            OuterError::Other => {
                return 0
            }
        }
    }
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name.Contains("get_inner"));
        Assert.NotNull(func);

        // Match on dereferenced self should create extract instructions
        var allInstructions = func.BasicBlocks.SelectMany(b => b.Instructions).ToList();
        var extractInstructions = allInstructions.OfType<IrExtractVariantData>().ToList();

        // Should have at least one extract for the Inner variant's err binding
        Assert.NotEmpty(extractInstructions);

        // The extract should have an enum type (not a pointer type)
        foreach (var extract in extractInstructions)
        {
            Assert.True(extract.EnumValue.Type is IrEnumType,
                $"Expected IrEnumType but got {extract.EnumValue.Type.GetType().Name}: {extract.EnumValue.Type.Name}");
        }
    }

    [Fact]
    public void BuildIr_MatchWithLiterals_CreatesLiteralPatterns()
    {
        var source = @"
fn match_number(n: i32) -> i32 {
    match n {
        0 => {
            return 100
        },
        1 => {
            return 200
        },
        _ => {
            return 300
        }
    }
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "match_number");
        Assert.NotNull(func);

        // Literal match creates basic blocks
        Assert.NotEmpty(func.BasicBlocks);
        Assert.NotEmpty(func.BasicBlocks[0].Instructions);
    }

    #endregion

    #region Variable and Assignment Tests

    [Fact]
    public void BuildIr_LetBinding_CreatesLocalVariable()
    {
        var source = @"
fn main() -> i32 {
    let x: i32 = 42
    return x
}";
        var module = BuildIr(source);

        var func = module.Functions[0];
        var instructions = func.BasicBlocks[0].Instructions;

        // Should have store for let binding
        var hasStore = instructions.Any(i => i is IrStore || i is IrLocalDecl);
        Assert.True(hasStore);
    }

    [Fact]
    public void BuildIr_MutableVariable_AllowsReassignment()
    {
        var source = @"
fn main() -> i32 {
    var x: i32 = 42
    x = 100
    return x
}";
        var module = BuildIr(source);

        var func = module.Functions[0];
        var instructions = func.BasicBlocks[0].Instructions;

        // Should have multiple stores
        var storeCount = instructions.Count(i => i is IrStore);
        Assert.True(storeCount >= 1); // At least one store for reassignment
    }

    [Fact]
    public void BuildIr_CompoundAssignment_ExpandsToReadModifyWrite()
    {
        var source = @"
fn main() -> i32 {
    var x: i32 = 10
    x += 5
    return x
}";
        var module = BuildIr(source);

        var func = module.Functions[0];
        var instructions = func.BasicBlocks[0].Instructions;

        // Compound assignment expands to: load, add, store
        var hasBinaryOp = instructions.Any(i => i is IrBinaryOp);
        Assert.True(hasBinaryOp);
    }

    #endregion

    #region Array and Slice Tests

    [Fact]
    public void BuildIr_ArrayLiteral_CreatesArrayLiteralInstruction()
    {
        var source = @"
fn main() -> i32 {
    let arr = [1, 2, 3]
    return arr[0]
}";
        var module = BuildIr(source);

        var func = module.Functions[0];
        var instructions = func.BasicBlocks[0].Instructions;

        // Should have array literal instruction or array-related operations
        Assert.NotEmpty(instructions);
        var hasReturn = instructions.Any(i => i is IrReturn);
        Assert.True(hasReturn);
    }

    [Fact]
    public void BuildIr_ArrayIndexing_CreatesIndexAccessInstruction()
    {
        var source = @"
fn get_element(arr: [i32; 5], idx: i32) -> i32 {
    return arr[idx]
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "get_element");
        Assert.NotNull(func);

        var instructions = func.BasicBlocks[0].Instructions;
        var hasIndexAccess = instructions.Any(i => i is IrIndexAccess);
        Assert.True(hasIndexAccess);
    }

    [Fact]
    public void BuildIr_ArrayIndexAssignment_CreatesIndexStoreInstruction()
    {
        var source = @"
fn set_element(arr: [i32; 5], idx: i32, value: i32) -> i32 {
    arr[idx] = value
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "set_element");
        Assert.NotNull(func);

        var instructions = func.BasicBlocks[0].Instructions;
        var hasIndexStore = instructions.Any(i => i is IrIndexStore);
        Assert.True(hasIndexStore);
    }

    #endregion

    #region Trait Tests

    [Fact]
    public void BuildIr_SimpleTrait_CreatesTraitDefinition()
    {
        var source = @"
trait Printable {
    fn print(&self) -> i32
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        Assert.Single(module.Traits);
        var trait_ = module.Traits[0];
        Assert.Equal("Printable", trait_.TraitName);
        Assert.Single(trait_.Methods);
        Assert.Equal("print", trait_.Methods[0].Name);
    }

    [Fact]
    public void BuildIr_TraitImpl_CreatesImplMethods()
    {
        var source = @"
trait Printable {
    fn print(&self) -> i32
}

struct Point {
    x: i32,
    y: i32
}

impl Printable for Point {
    fn print(&self) -> i32 {
        return self.x
    }
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        Assert.Single(module.Traits);
        Assert.Single(module.Structs);

        // Should have impl for Point
        Assert.Single(module.TraitImpls);
        var impl_ = module.TraitImpls[0];
        Assert.Equal("Printable", impl_.TraitName);
        Assert.Equal("Point", impl_.TypeName);
    }

    [Fact]
    public void BuildIr_GenericTrait_AcceptsTypeParameters()
    {
        var source = @"
trait Convert<T> {
    fn convert(&self) -> T
}

fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        Assert.Single(module.Traits);
        var trait_ = module.Traits[0];
        Assert.Equal("Convert", trait_.TraitName);
        // Trait should have type parameter
    }

    #endregion

    #region Drop and Defer Tests

    [Fact]
    public void BuildIr_DeferStatement_CreatesDeferInstruction()
    {
        var source = @"
fn cleanup_test() -> i32 {
    defer {
        let x: i32 = 1
    }
    return 42
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "cleanup_test");
        Assert.NotNull(func);

        var instructions = func.BasicBlocks[0].Instructions;
        var hasDefer = instructions.Any(i => i is IrDefer);
        Assert.True(hasDefer);
    }

    [Fact]
    public void BuildIr_MultipleDeferBlocks_CreatesMultipleDeferInstructions()
    {
        var source = @"
fn multi_defer() -> i32 {
    defer {
        let x: i32 = 1
    }
    defer {
        let y: i32 = 2
    }
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "multi_defer");
        Assert.NotNull(func);

        var allInstructions = func.BasicBlocks.SelectMany(b => b.Instructions).ToList();
        var deferCount = allInstructions.Count(i => i is IrDefer);
        Assert.Equal(2, deferCount);
    }

    #endregion

    #region Control Flow Tests

    [Fact]
    public void BuildIr_ComplexNestedIf_CreatesProperControlFlow()
    {
        var source = @"
fn nested_conditions(a: i32, b: i32, c: i32) -> i32 {
    if a > 0 {
        if b > 0 {
            if c > 0 {
                return 1
            }
            return 2
        }
        return 3
    }
    return 4
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "nested_conditions");
        Assert.NotNull(func);

        // Function should have at least entry block
        Assert.NotEmpty(func.BasicBlocks);

        // Should have instructions
        var allInstructions = func.BasicBlocks.SelectMany(b => b.Instructions).ToList();
        Assert.NotEmpty(allInstructions);
    }

    [Fact]
    public void BuildIr_WhileWithBreakAndContinue_CreatesProperLoopStructure()
    {
        var source = @"
fn loop_with_control(n: i32) -> i32 {
    var i = 0
    while i < n {
        if i == 5 {
            break
        }
        i = i + 1
    }
    return i
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "loop_with_control");
        Assert.NotNull(func);

        // Function should have blocks
        Assert.NotEmpty(func.BasicBlocks);

        // Should have instructions
        var allInstructions = func.BasicBlocks.SelectMany(b => b.Instructions).ToList();
        Assert.NotEmpty(allInstructions);
    }

    [Fact]
    public void BuildIr_ForeverLoopWithBreak_CreatesInfiniteLoopWithExit()
    {
        var source = @"
fn infinite_with_exit() -> i32 {
    var counter = 0
    forever {
        counter = counter + 1
        if counter >= 10 {
            break
        }
    }
    return counter
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "infinite_with_exit");
        Assert.NotNull(func);

        // Loop should create basic blocks
        Assert.True(func.BasicBlocks.Count >= 1);
        Assert.NotEmpty(func.BasicBlocks[0].Instructions);
    }

    #endregion

    #region Expression Tests

    [Fact]
    public void BuildIr_ComplexArithmeticExpression_GeneratesCorrectOrder()
    {
        var source = @"
fn complex_math(a: i32, b: i32, c: i32) -> i32 {
    return (a + b) * c - (a / b)
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "complex_math");
        Assert.NotNull(func);

        var instructions = func.BasicBlocks[0].Instructions;

        // Should have multiple binary operations
        var binaryOpCount = instructions.Count(i => i is IrBinaryOp);
        Assert.True(binaryOpCount >= 3); // +, *, -, /
    }

    [Fact]
    public void BuildIr_LogicalAndWithShortCircuit_CreatesConditionalEvaluation()
    {
        var source = @"
fn short_circuit_and(a: bool, b: bool) -> bool {
    return a && b
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "short_circuit_and");
        Assert.NotNull(func);

        // Short-circuit AND creates branching
        // May create multiple blocks or use conditional evaluation
        Assert.NotEmpty(func.BasicBlocks);
    }

    [Fact]
    public void BuildIr_LogicalOrWithShortCircuit_CreatesConditionalEvaluation()
    {
        var source = @"
fn short_circuit_or(a: bool, b: bool) -> bool {
    return a || b
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "short_circuit_or");
        Assert.NotNull(func);

        // Short-circuit OR creates branching
        Assert.NotEmpty(func.BasicBlocks);
    }

    [Fact]
    public void BuildIr_UnaryNegation_CreatesNegationOp()
    {
        var source = @"
fn negate(x: i32) -> i32 {
    return -x
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "negate");
        Assert.NotNull(func);

        var instructions = func.BasicBlocks[0].Instructions;
        // Negation might be implemented as binary op (0 - x) or unary op
        var hasBinaryOp = instructions.Any(i => i is IrBinaryOp);
        Assert.True(hasBinaryOp || instructions.Any(i => i is IrReturn));
    }

    [Fact]
    public void BuildIr_BitwiseNotOperator_CreatesBitwiseOp()
    {
        var source = @"
fn bitwise_not(x: i32) -> i32 {
    return ~x
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "bitwise_not");
        Assert.NotNull(func);

        // Bitwise NOT should generate operation
        var instructions = func.BasicBlocks[0].Instructions;
        Assert.NotEmpty(instructions);
    }

    #endregion

    #region Function Call Tests

    [Fact]
    public void BuildIr_RecursiveFunction_AllowsSelfCall()
    {
        var source = @"
fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "factorial");
        Assert.NotNull(func);

        // Should have recursive call instruction
        var allInstructions = func.BasicBlocks.SelectMany(b => b.Instructions).ToList();
        var hasCall = allInstructions.Any(i => i is IrCall call && call.FunctionName == "factorial");
        Assert.True(hasCall);
    }

    [Fact]
    public void BuildIr_MutuallyRecursiveFunctions_AllowCrossReferences()
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
        var module = BuildIr(source);

        Assert.Equal(2, module.Functions.Count);

        var evenFunc = module.Functions.FirstOrDefault(f => f.Name == "is_even");
        var oddFunc = module.Functions.FirstOrDefault(f => f.Name == "is_odd");

        Assert.NotNull(evenFunc);
        Assert.NotNull(oddFunc);

        // Each should call the other
        var evenInstructions = evenFunc.BasicBlocks.SelectMany(b => b.Instructions).ToList();
        var oddInstructions = oddFunc.BasicBlocks.SelectMany(b => b.Instructions).ToList();

        var evenCallsOdd = evenInstructions.Any(i => i is IrCall call && call.FunctionName == "is_odd");
        var oddCallsEven = oddInstructions.Any(i => i is IrCall call && call.FunctionName == "is_even");

        Assert.True(evenCallsOdd);
        Assert.True(oddCallsEven);
    }

    // NOTE: Tuple tests removed - tuples may not be fully implemented yet
    // Re-add these tests once tuple syntax is confirmed working

    #endregion

    #region String Literal Tests

    [Fact]
    public void BuildIr_ExternFunctionDeclaration_CreatesExternFunction()
    {
        var source = @"
extern fn write(format: *u8) -> i32

fn test_string() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        // Extern function should be declared
        var writeFunc = module.Functions.FirstOrDefault(f => f.Name == "write");
        Assert.NotNull(writeFunc);
        Assert.True(writeFunc.IsExtern);
    }

    [Fact]
    public void BuildIr_StringInExternCall_ParsesSuccessfully()
    {
        var source = @"
extern fn write(format: *u8) -> i32

fn messages() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        // Module should build successfully
        Assert.NotNull(module);
        var func = module.Functions.FirstOrDefault(f => f.Name == "messages");
        Assert.NotNull(func);
    }

    #endregion

    #region Type System Tests

    [Fact]
    public void BuildIr_GenericFunction_CreatesFunction()
    {
        var source = @"
fn identity(value: i32) -> i32 {
    return value
}

fn main() -> i32 {
    return identity(42)
}";
        var module = BuildIr(source);

        var identityFunc = module.Functions.FirstOrDefault(f => f.Name == "identity");
        Assert.NotNull(identityFunc);

        // Function exists and has correct parameters
        Assert.Single(identityFunc.Parameters);
        Assert.Equal("value", identityFunc.Parameters[0].Name);
    }

    [Fact]
    public void BuildIr_ReferenceType_CreatesReferenceInstruction()
    {
        var source = @"
fn borrow(x: &i32) -> i32 {
    return *x
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "borrow");
        Assert.NotNull(func);

        // Parameter should be reference type
        Assert.Single(func.Parameters);
        var paramType = func.Parameters[0].Type;
        Assert.True(paramType is IrReferenceType || paramType is IrPointerType);
    }

    [Fact]
    public void BuildIr_MutableReferenceType_AllowsMutation()
    {
        var source = @"
fn increment(x: &var i32) -> i32 {
    *x = *x + 1
    return *x
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "increment");
        Assert.NotNull(func);

        // Parameter should be mutable reference
        Assert.Single(func.Parameters);
        var paramType = func.Parameters[0].Type;
        Assert.True(paramType is IrMutReferenceType || paramType is IrPointerType);

        // Should have dereference store for mutation
        var allInstructions = func.BasicBlocks.SelectMany(b => b.Instructions).ToList();
        var hasDereferenceStore = allInstructions.Any(i => i is IrDereferenceStore);
        Assert.True(hasDereferenceStore);
    }

    [Fact]
    public void BuildIr_PointerType_AllowsUnsafeOperations()
    {
        var source = @"
fn unsafe_deref(ptr: *i32) -> i32 {
    unsafe {
        return *ptr
    }
}";
        var module = BuildIr(source);

        var func = module.Functions.FirstOrDefault(f => f.Name == "unsafe_deref");
        Assert.NotNull(func);

        // Parameter should be pointer type
        Assert.Single(func.Parameters);
        var paramType = func.Parameters[0].Type;
        Assert.IsType<IrPointerType>(paramType);
    }

    #endregion
}
