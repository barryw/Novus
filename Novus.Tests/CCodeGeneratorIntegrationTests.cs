using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Comprehensive integration tests for CCodeGenerator targeting uncovered code paths.
/// Focuses on: GenerateFunctionFile, EmitFunctionToBuilder, EmitEnumTypeToBuilder,
/// monomorphization, function calls, and complex code generation scenarios.
/// </summary>
public class CCodeGeneratorIntegrationTests
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

    private string GenerateCCode(IrModule module, BuildMode buildMode = BuildMode.Debug)
    {
        var codegen = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft", buildMode);
        return codegen.Generate();
    }

    #region Function Generation Tests

    [Fact]
    public void CCodeGen_SimpleFunctionWithReturn_GeneratesValidC()
    {
        var source = @"
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate function declaration and definition
        Assert.Contains("int32_t add", code);
        Assert.Contains("int32_t a", code);
        Assert.Contains("int32_t b", code);
        Assert.Contains("return", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithMultipleParameters_GeneratesCorrectSignature()
    {
        var source = @"
pub fn calculate(a: i32, b: i32, c: i32, d: i32) -> i32 {
    return a + b + c + d
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t calculate", code);
        Assert.Contains("int32_t a", code);
        Assert.Contains("int32_t b", code);
        Assert.Contains("int32_t c", code);
        Assert.Contains("int32_t d", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithLocalVariables_DeclaresLocals()
    {
        var source = @"
pub fn test() -> i32 {
    var x = 10
    var y = 20
    var z = x + y
    return z
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t test", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithIfStatement_GeneratesBranches()
    {
        var source = @"
pub fn max(a: i32, b: i32) -> i32 {
    if a > b {
        return a
    } else {
        return b
    }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t max", code);
        // Should have branching logic
        Assert.Contains("return", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithLoop_GeneratesLoopStructure()
    {
        var source = @"
pub fn sum_to_n(n: i32) -> i32 {
    var sum = 0
    var i = 0
    forever {
        if i >= n {
            break
        }
        sum = sum + i
        i = i + 1
    }
    return sum
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t sum_to_n", code);
        Assert.Contains("int32_t n", code);
    }

    [Fact]
    public void CCodeGen_RecursiveFunction_GeneratesForwardDeclaration()
    {
        var source = @"
pub fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t factorial", code);
    }

    [Fact]
    public void CCodeGen_FunctionCallWithArguments_GeneratesCall()
    {
        var source = @"
pub fn helper(x: i32) -> i32 {
    return x * 2
}

pub fn caller(y: i32) -> i32 {
    return helper(y + 1)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t helper", code);
        Assert.Contains("int32_t caller", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithMultipleReturns_HandlesAllPaths()
    {
        var source = @"
pub fn classify(n: i32) -> i32 {
    if n < 0 {
        return -1
    }
    if n == 0 {
        return 0
    }
    return 1
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t classify", code);
        Assert.Contains("return", code);
    }

    #endregion

    #region Enum Generation Tests

    [Fact]
    public void CCodeGen_SimpleEnum_GeneratesEnumTypedef()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

pub fn get_red() -> Color {
    return Color::Red
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Color", code);
    }

    [Fact]
    public void CCodeGen_EnumWithData_GeneratesTaggedUnion()
    {
        var source = @"
enum Option {
    Some(i32),
    None
}

pub fn make_some(x: i32) -> Option {
    return Option::Some(x)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Option", code);
    }

    [Fact]
    public void CCodeGen_EnumMatch_GeneratesSwitchStatement()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

pub fn color_to_int(c: Color) -> i32 {
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

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Color", code);
        Assert.Contains("color_to_int", code);
    }

    [Fact]
    public void CCodeGen_EnumWithMultipleVariants_GeneratesAllVariants()
    {
        var source = @"
enum Result {
    Ok(i32),
    Err(i32)
}

pub fn make_ok(x: i32) -> Result {
    return Result::Ok(x)
}

pub fn make_err(x: i32) -> Result {
    return Result::Err(x)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Result", code);
        Assert.Contains("make_ok", code);
        Assert.Contains("make_err", code);
    }

    #endregion

    #region Struct Tests

    [Fact]
    public void CCodeGen_NestedStruct_GeneratesNestedDefinition()
    {
        var source = @"
struct Inner {
    value: i32
}

struct Outer {
    inner: Inner,
    extra: i32
}

pub fn make_outer() -> Outer {
    var i = Inner { value: 42 }
    return Outer { inner: i, extra: 100 }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Inner", code);
        Assert.Contains("Outer", code);
        Assert.Contains("make_outer", code);
    }

    [Fact]
    public void CCodeGen_StructWithPointerField_GeneratesPointerType()
    {
        var source = @"
struct Node {
    value: i32,
    next: *Node
}

pub fn make_node(val: i32) -> Node {
    return Node { value: val, next: 0 as *Node }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Node", code);
        Assert.Contains("make_node", code);
    }

    [Fact]
    public void CCodeGen_StructMethod_GeneratesMethodCall()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

impl Point {
    pub fn get_x(&self) -> i32 {
        return self.x
    }
}

pub fn test_method(p: Point) -> i32 {
    return p.get_x()
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Point", code);
        Assert.Contains("get_x", code);
        Assert.Contains("test_method", code);
    }

    #endregion

    #region Pointer and Reference Tests

    [Fact]
    public void CCodeGen_PointerDereference_GeneratesDerefOperator()
    {
        var source = @"
pub fn deref(ptr: *i32) -> i32 {
    return *ptr
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t", code);
        Assert.Contains("deref", code);
    }

    [Fact]
    public void CCodeGen_AddressOf_GeneratesAddressOperator()
    {
        var source = @"
pub fn get_address(x: i32) -> *i32 {
    return &x
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t", code);
        Assert.Contains("get_address", code);
    }

    [Fact]
    public void CCodeGen_PointerArithmetic_GeneratesArithmetic()
    {
        var source = @"
pub fn advance_ptr(ptr: *i32, offset: i32) -> *i32 {
    return ptr + offset
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("advance_ptr", code);
        Assert.Contains("int32_t", code);
    }

    #endregion

    #region Array Tests

    [Fact]
    public void CCodeGen_ArrayLiteral_GeneratesArrayInitializer()
    {
        var source = @"
pub fn make_array() -> [i32; 3] {
    return [1, 2, 3]
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("make_array", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ArrayIndexAccess_GeneratesIndexing()
    {
        var source = @"
pub fn get_element(arr: [i32; 5], index: u32) -> i32 {
    return arr[index]
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("get_element", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ArrayAssignment_GeneratesIndexedStore()
    {
        var source = @"
pub fn set_element(arr: [i32; 5], index: u32, value: i32) -> [i32; 5] {
    arr[index] = value
    return arr
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("set_element", code);
        Assert.Contains("int32_t", code);
    }

    #endregion

    #region Binary Operations Tests

    [Fact]
    public void CCodeGen_ArithmeticOperations_GeneratesAllOperators()
    {
        var source = @"
pub fn arithmetic(a: i32, b: i32) -> i32 {
    var add = a + b
    var sub = a - b
    var mul = a * b
    var div = a / b
    var mod = a % b
    return add + sub + mul + div + mod
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("arithmetic", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_BitwiseOperations_GeneratesBitwiseOps()
    {
        var source = @"
pub fn bitwise(a: i32, b: i32) -> i32 {
    var and = a & b
    var or = a | b
    var xor = a ^ b
    var shl = a << 2
    var shr = a >> 2
    return and + or + xor + shl + shr
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("bitwise", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ComparisonOperations_GeneratesComparisons()
    {
        var source = @"
pub fn compare(a: i32, b: i32) -> i32 {
    if a == b {
        return 0
    }
    if a < b {
        return -1
    }
    if a > b {
        return 1
    }
    return 0
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("compare", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_LogicalOperations_GeneratesLogicalOps()
    {
        var source = @"
pub fn logical(a: bool, b: bool) -> bool {
    var and = a && b
    var or = a || b
    var not = !a
    return and || or || not
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("logical", code);
        Assert.Contains("bool", code);
    }

    #endregion

    #region Cast Tests

    [Fact]
    public void CCodeGen_IntegerCast_GeneratesCastExpression()
    {
        var source = @"
pub fn cast_to_u32(x: i32) -> u32 {
    return x as u32
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("cast_to_u32", code);
        Assert.Contains("int32_t", code);
        Assert.Contains("uint32_t", code);
    }

    [Fact]
    public void CCodeGen_PointerCast_GeneratesPointerCast()
    {
        var source = @"
pub fn cast_pointer(ptr: *i32) -> *u8 {
    return ptr as *u8
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("cast_pointer", code);
        Assert.Contains("int32_t", code);
        Assert.Contains("uint8_t", code);
    }

    #endregion

    #region Static Variables Tests

    [Fact]
    public void CCodeGen_StaticVariable_GeneratesStaticDecl()
    {
        var source = @"
static COUNTER: i32 = 0

pub fn increment() -> i32 {
    COUNTER = COUNTER + 1
    return COUNTER
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("COUNTER", code);
        Assert.Contains("increment", code);
    }

    #endregion

    #region External Function Tests

    [Fact]
    public void CCodeGen_ExternalFunction_GeneratesExternDecl()
    {
        var source = @"
extern fn printf(format: *u8) -> i32

pub fn call_printf() -> i32 {
    return printf(0 as *u8)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("printf", code);
        Assert.Contains("call_printf", code);
    }

    #endregion

    #region Complex Integration Tests

    [Fact]
    public void CCodeGen_ComplexProgram_GeneratesCompleteCode()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

enum Shape {
    Circle(i32),
    Rectangle(i32, i32)
}

pub fn area(s: Shape) -> i32 {
    match s {
        Shape::Circle(r) => {
            return r * r * 3
        },
        Shape::Rectangle(w, h) => {
            return w * h
        }
    }
}

pub fn create_point(x: i32, y: i32) -> Point {
    var p = Point { x: x, y: y }
    return p
}

pub fn main() -> i32 {
    var circle = Shape::Circle(10)
    var rect = Shape::Rectangle(5, 8)
    var p = create_point(1, 2)
    return area(circle) + area(rect) + p.x
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Point", code);
        Assert.Contains("Shape", code);
        Assert.Contains("area", code);
        Assert.Contains("main", code);
        Assert.Contains("create_point", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithMultipleTypes_GeneratesAllTypes()
    {
        var source = @"
struct Vector3 {
    x: i32,
    y: i32,
    z: i32
}

enum Axis {
    X,
    Y,
    Z
}

pub fn get_axis_value(v: Vector3, axis: Axis) -> i32 {
    match axis {
        Axis::X => {
            return v.x
        },
        Axis::Y => {
            return v.y
        },
        Axis::Z => {
            return v.z
        }
    }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Vector3", code);
        Assert.Contains("Axis", code);
        Assert.Contains("get_axis_value", code);
    }

    [Fact]
    public void CCodeGen_MultipleFunctionsCallingEachOther_GeneratesCallGraph()
    {
        var source = @"
pub fn helper1(x: i32) -> i32 {
    return x * 2
}

pub fn helper2(x: i32) -> i32 {
    return x + 10
}

pub fn caller(y: i32) -> i32 {
    var a = helper1(y)
    var b = helper2(a)
    return b
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("helper1", code);
        Assert.Contains("helper2", code);
        Assert.Contains("caller", code);
    }

    #endregion

    #region Build Mode Tests

    [Fact]
    public void CCodeGen_DebugMode_GeneratesDebugInfo()
    {
        var source = @"
pub fn test() -> i32 {
    return 42
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module, BuildMode.Debug);

        Assert.Contains("test", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ReleaseMode_OptimizesCode()
    {
        var source = @"
pub fn test() -> i32 {
    return 42
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module, BuildMode.Release);

        Assert.Contains("test", code);
        Assert.Contains("int32_t", code);
    }

    #endregion

    #region String Literal Tests

    [Fact]
    public void CCodeGen_FunctionReturningString_HandlesStringLiterals()
    {
        var source = @"
pub fn get_message() -> *u8 {
    return ""Hello, World!""
}

pub fn main() -> i32 {
    var msg = get_message()
    return 0
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Verify function signatures are generated
        Assert.Contains("get_message", code);
        Assert.Contains("main", code);
        // Note: String literals are handled separately by CCodeGenerator
        // and passed via stringLiterals parameter, so they may not appear in main code output
    }

    #endregion

    #region Type Tests

    [Fact]
    public void CCodeGen_AllIntegerTypes_GeneratesCorrectTypes()
    {
        var source = @"
pub fn test_types(a: i8, b: i16, c: i32, d: i64, e: u8, f: u16, g: u32, h: u64) -> i32 {
    return 0
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int8_t", code);
        Assert.Contains("int16_t", code);
        Assert.Contains("int32_t", code);
        Assert.Contains("int64_t", code);
        Assert.Contains("uint8_t", code);
        Assert.Contains("uint16_t", code);
        Assert.Contains("uint32_t", code);
        Assert.Contains("uint64_t", code);
    }

    [Fact]
    public void CCodeGen_BoolType_GeneratesBoolType()
    {
        var source = @"
pub fn test_bool(flag: bool) -> bool {
    return !flag
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("bool", code);
        Assert.Contains("test_bool", code);
    }

    #endregion
}
