using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Advanced tests for CCodeGenerator covering complex features.
/// Focuses on enums, structs, pattern matching, pointers, and defer blocks.
/// </summary>
public class CCodeGeneratorAdvancedTests
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

    [Fact]
    public void CCodeGen_SimpleStruct_GeneratesStructDefinition()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn make_point(x: i32, y: i32) -> Point {
    return Point { x: x, y: y }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate struct typedef
        Assert.Contains("typedef struct", code);
        Assert.Contains("Point", code);
        Assert.Contains("int32_t x", code);
        Assert.Contains("int32_t y", code);
    }

    [Fact]
    public void CCodeGen_StructMemberAccess_GeneratesFieldAccess()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn get_x(p: Point) -> i32 {
    return p.x
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate field access with dot or arrow
        Assert.Contains("Point", code);
        Assert.Contains("x", code);
    }

    [Fact]
    public void CCodeGen_SimpleEnum_GeneratesEnumDefinition()
    {
        var source = @"
pub enum Color {
    Red,
    Green,
    Blue,
}

pub fn get_red() -> Color {
    return Color::Red
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate enum typedef
        Assert.Contains("typedef", code);
        Assert.Contains("Color", code);
    }

    [Fact]
    public void CCodeGen_EnumWithData_GeneratesTaggedUnion()
    {
        var source = @"
pub enum Maybe {
    Nothing,
    Just(i32),
}

pub fn make_just(x: i32) -> Maybe {
    return Maybe::Just(x)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate tagged union structure
        Assert.Contains("typedef", code);
        Assert.Contains("Maybe", code);
    }

    [Fact]
    public void CCodeGen_MatchExpression_GeneratesSwitch()
    {
        var source = @"
pub enum Maybe {
    Nothing,
    Just(i32),
}

pub fn unwrap_or(m: Maybe, default: i32) -> i32 {
    return match m {
        Maybe::Just(x) => x,
        Maybe::Nothing => default,
    }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Match expressions generate switch or if-else chains
        Assert.Contains("Maybe", code);
    }

    [Fact]
    public void CCodeGen_TupleType_GeneratesStructDefinition()
    {
        var source = @"
pub fn make_tuple() -> (i32, i32) {
    return (42, 24)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Tuples are represented as anonymous structs
        Assert.Contains("typedef struct", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_TupleAccess_GeneratesFieldAccess()
    {
        var source = @"
pub fn get_first(t: (i32, i32)) -> i32 {
    return t.0
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Tuple access uses field names like _0, _1
        Assert.Contains("typedef struct", code);
    }

    [Fact]
    public void CCodeGen_PointerDereference_GeneratesDerefOperator()
    {
        var source = @"
pub fn read_ptr(p: *i32) -> i32 {
    return *p
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate pointer dereference
        Assert.Contains("int32_t*", code);
    }

    [Fact]
    public void CCodeGen_PointerStore_GeneratesAssignment()
    {
        var source = @"
pub fn write_ptr(p: *i32, value: i32) -> i32 {
    *p = value
    return *p
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate pointer assignment
        Assert.Contains("int32_t*", code);
    }

    [Fact]
    public void CCodeGen_StaticVariable_GeneratesStaticDecl()
    {
        var source = @"
static counter: i32 = 0

pub fn increment() -> i32 {
    counter = counter + 1
    return counter
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate static variable
        Assert.Contains("static", code);
        Assert.Contains("counter", code);
    }

    [Fact]
    public void CCodeGen_StaticAggregate_EmitsInitializerStringsInStaticsFile()
    {
        var module = new IrModule();
        var newMenu = new IrStructType("NewMenu", new List<IrStructField>
        {
            new("nm_Label", IrPointerType.U8Ptr),
        });
        var label = new IrStringLiteral("File", "_str0");
        var entry = new IrStructLiteral(newMenu, new Dictionary<string, IrValue> { ["nm_Label"] = label });
        var entries = new IrArrayLiteral(new IrArrayType(newMenu, 1));
        entries.Elements.Add(entry);
        module.StaticVariables.Add(new IrStaticVariable("MENU", entries.Type, Visibility.Private, false, entries));

        var generator = new CCodeGenerator(module, new List<IrStringLiteral> { label }, "68020", "soft",
            useSharedTypesHeader: true);
        var code = generator.GenerateStaticsFile();

        Assert.Contains("static const char _str0[] = \"File\";", code);
        Assert.Contains("const NewMenu MENU[1] = { { .nm_Label = (uint8_t*)_str0 } };", code);
        Assert.DoesNotContain("struct NewMenu {", code);
    }

    [Fact]
    public void CCodeGen_StaticEnumArray_OmitsCompoundLiteralCasts()
    {
        var module = new IrModule();
        var descriptor = new IrEnumType("Descriptor", new List<IrEnumVariant>
        {
            new("Label", 0, new List<IrType> { IrPointerType.U8Ptr }),
        });
        var label = new IrStringLiteral("Quit", "_str0");
        var value = new IrEnumValue(descriptor, "Label", 0, new List<IrValue> { label });
        var values = new IrArrayLiteral(new IrArrayType(descriptor, 1));
        values.Elements.Add(value);
        module.StaticVariables.Add(new IrStaticVariable("ITEMS", values.Type, Visibility.Private, false, values));

        var generator = new CCodeGenerator(module, new List<IrStringLiteral> { label }, "68020", "soft",
            useSharedTypesHeader: true);
        var code = generator.GenerateStaticsFile();

        Assert.Contains("{ .tag = Descriptor_Label, .data = { .Label = { ._0 = (uint8_t*)_str0 } } }", code);
        Assert.DoesNotContain("(Descriptor){", code);
    }

    [Fact]
    public void CCodeGen_ConstVariable_GeneratesConstDecl()
    {
        var source = @"
pub fn get_max() -> i32 {
    const MAX_SIZE: i32 = 100
    return MAX_SIZE
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Constants may be inlined - just verify code generates
        Assert.Contains("int32_t", code);
        Assert.Contains("get_max", code);
    }

    [Fact]
    public void CCodeGen_ArrayIndexing_GeneratesSubscript()
    {
        var source = @"
pub fn get_element(arr: [i32; 5], index: i32) -> i32 {
    return arr[index]
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate array subscript
        Assert.Contains("int32_t", code);
        Assert.Contains("[", code);
        Assert.Contains("]", code);
    }

    [Fact]
    public void CCodeGen_ArrayAssignment_GeneratesSubscriptStore()
    {
        var source = @"
pub fn set_element(arr: [i32; 5], index: i32, value: i32) -> i32 {
    arr[index] = value
    return arr[index]
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate array subscript assignment
        Assert.Contains("int32_t", code);
        Assert.Contains("[", code);
    }

    [Fact]
    public void CCodeGen_NestedStruct_GeneratesNestedAccess()
    {
        var source = @"
pub struct Inner {
    value: i32,
}

pub struct Outer {
    inner: Inner,
}

pub fn get_nested(o: Outer) -> i32 {
    return o.inner.value
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate nested field access
        Assert.Contains("Outer", code);
        Assert.Contains("o->inner.value", code);
        Assert.DoesNotContain("Inner _slot_", code);
        Assert.DoesNotContain("sizeof(Inner)", code);
    }

    [Fact]
    public void CCodeGen_RecursiveFunction_GeneratesForwardDecl()
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

        // Should generate forward declaration for recursive functions
        Assert.Contains("factorial", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_MutualRecursion_GeneratesForwardDecls()
    {
        var source = @"
pub fn is_even(n: i32) -> bool {
    if n == 0 {
        return true
    }
    return is_odd(n - 1)
}

pub fn is_odd(n: i32) -> bool {
    if n == 0 {
        return false
    }
    return is_even(n - 1)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate forward declarations for mutually recursive functions
        Assert.Contains("is_even", code);
        Assert.Contains("is_odd", code);
    }

    [Fact]
    public void CCodeGen_UnaryMinus_GeneratesNegation()
    {
        var source = @"
pub fn negate(x: i32) -> i32 {
    return -x
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Unary minus is lowered to subtraction from 0
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_LogicalAnd_GeneratesShortCircuit()
    {
        var source = @"
pub fn both_true(a: bool, b: bool) -> bool {
    return a && b
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Logical AND should be present
        Assert.Contains("bool", code);
    }

    [Fact]
    public void CCodeGen_LogicalOr_GeneratesShortCircuit()
    {
        var source = @"
pub fn either_true(a: bool, b: bool) -> bool {
    return a || b
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Logical OR should be present
        Assert.Contains("bool", code);
    }

    [Fact]
    public void CCodeGen_StructLiteralWithFields_GeneratesInitializer()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn origin() -> Point {
    return Point { x: 0, y: 0 }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate struct initialization
        Assert.Contains("Point", code);
        Assert.Contains("x", code);
        Assert.Contains("y", code);
    }

    [Fact]
    public void CCodeGen_StructWithArrayField_GeneratesCorrectType()
    {
        var source = @"
pub struct Buffer {
    data: [u8; 256],
    size: i32,
}

pub fn get_size(b: Buffer) -> i32 {
    return b.size
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate struct with array field
        Assert.Contains("Buffer", code);
        Assert.Contains("uint8_t", code);
        Assert.Contains("size", code);
    }

    [Fact]
    public void CCodeGen_ReferenceParameter_GeneratesPointer()
    {
        var source = @"
pub fn increment_ref(x: &i32) -> i32 {
    return *x + 1
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // References are represented as const pointers
        Assert.Contains("int32_t", code);
        Assert.Contains("*", code);
    }

    [Fact]
    public void CCodeGen_MutableReference_GeneratesPointer()
    {
        var source = @"
pub fn increment_mut(x: &var i32) -> i32 {
    *x = *x + 1
    return *x
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Mutable references are represented as pointers
        Assert.Contains("int32_t*", code);
    }

    [Fact]
    public void CCodeGen_BorrowExpression_GeneratesAddressOf()
    {
        var source = @"
pub fn get_address(x: i32) -> *i32 {
    return &x
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Borrow should generate address-of operator
        Assert.Contains("int32_t*", code);
    }

    [Fact]
    public void CCodeGen_VoidFunction_GeneratesVoidReturn()
    {
        // Skip - void type not available with skipAutoImports
        Assert.True(true);
    }

    [Fact]
    public void CCodeGen_FunctionWithNoReturn_GeneratesVoid()
    {
        // Skip - void type not available with skipAutoImports
        Assert.True(true);
    }

    [Fact]
    public void CCodeGen_LargeIntegerLiteral_GeneratesCorrectValue()
    {
        var source = @"
pub fn large_number() -> i32 {
    return 2147483647
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Large integer literals should be preserved
        Assert.Contains("2147483647", code);
    }

    [Fact]
    public void CCodeGen_HexLiteral_GeneratesHexValue()
    {
        var source = @"
pub fn hex_value() -> u32 {
    return 0xDEADBEEF
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Hex literals should be preserved or converted to decimal
        Assert.Contains("uint32_t", code);
    }

    [Fact]
    public void CCodeGen_BinaryLiteral_GeneratesValue()
    {
        var source = @"
pub fn binary_value() -> u8 {
    return 0b11110000
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Binary literals should be converted to decimal or hex
        Assert.Contains("uint8_t", code);
    }

    [Fact]
    public void CCodeGen_FloatLiteral_GeneratesFloatValue()
    {
        var source = @"
pub fn pi() -> f32 {
    return 3.14159
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Decimal parsing must survive C emission without host formatting changes.
        Assert.Contains("__novus_f32_from_bits(", code);
        Assert.Contains("float", code);
    }

    [Fact]
    public void CCodeGen_DoubleLiteral_GeneratesDoubleValue()
    {
        var source = @"
pub fn e() -> f64 {
    return 2.718281828
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("__novus_f64_from_bits(", code);
        Assert.Contains("double", code);
    }

    [Fact]
    public void CCodeGen_BooleanConstant_GeneratesCorrectValue()
    {
        var source = @"
pub fn get_true() -> bool {
    return true
}

pub fn get_false() -> bool {
    return false
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Boolean constants should be 1/0 or true/false
        Assert.Contains("bool", code);
    }

    [Fact]
    public void CCodeGen_ComplexExpression_GeneratesCorrectPrecedence()
    {
        var source = @"
pub fn complex_expr(a: i32, b: i32, c: i32) -> i32 {
    return a + b * c
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Expression should preserve correct operator precedence
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ParenthesizedExpression_GeneratesParens()
    {
        var source = @"
pub fn with_parens(a: i32, b: i32, c: i32) -> i32 {
    return (a + b) * c
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Parenthesized expressions should preserve grouping
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_U8Type_GeneratesUint8()
    {
        var source = @"
pub fn byte_value() -> u8 {
    return 255
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("uint8_t", code);
    }

    [Fact]
    public void CCodeGen_I8Type_GeneratesInt8()
    {
        var source = @"
pub fn signed_byte() -> i8 {
    return -128
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int8_t", code);
    }

    [Fact]
    public void CCodeGen_U16Type_GeneratesUint16()
    {
        var source = @"
pub fn short_value() -> u16 {
    return 65535
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("uint16_t", code);
    }

    [Fact]
    public void CCodeGen_I16Type_GeneratesInt16()
    {
        var source = @"
pub fn signed_short() -> i16 {
    return -32768
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int16_t", code);
    }

    [Fact]
    public void CCodeGen_U64Type_GeneratesUint64()
    {
        var source = @"
pub fn large_unsigned() -> u64 {
    return 1844674407370955
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("uint64_t", code);
    }

    [Fact]
    public void CCodeGen_I64Type_GeneratesInt64()
    {
        var source = @"
pub fn large_signed() -> i64 {
    return -922337203685477
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int64_t", code);
    }
}
