# Missing Type System Tests - Implementation Examples

This document provides complete, ready-to-implement test cases for gaps in the Novus type system test coverage.

---

## File 1: EdgeCaseNumericTests.cs

Test integer overflow, underflow, and boundary values.

```csharp
using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for numeric edge cases: overflow, underflow, type boundaries
/// </summary>
public class EdgeCaseNumericTests
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

    // ==================== SIGNED INTEGER BOUNDARIES ====================

    [Fact]
    public void BuildIr_I8_Min_Compiles()
    {
        var source = @"
pub fn main() -> i8 {
    return -128i8
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I8_Max_Compiles()
    {
        var source = @"
pub fn main() -> i8 {
    return 127i8
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I16_Min_Compiles()
    {
        var source = @"
pub fn main() -> i16 {
    return -32768i16
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I16_Max_Compiles()
    {
        var source = @"
pub fn main() -> i16 {
    return 32767i16
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32_Min_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return -2147483648i32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32_Max_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    return 2147483647i32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I64_Min_Compiles()
    {
        var source = @"
pub fn main() -> i64 {
    return -9223372036854775808i64
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I64_Max_Compiles()
    {
        var source = @"
pub fn main() -> i64 {
    return 9223372036854775807i64
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== UNSIGNED INTEGER BOUNDARIES ====================

    [Fact]
    public void BuildIr_U8_Max_Compiles()
    {
        var source = @"
pub fn main() -> u8 {
    return 255u8
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U16_Max_Compiles()
    {
        var source = @"
pub fn main() -> u16 {
    return 65535u16
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U32_Max_Compiles()
    {
        var source = @"
pub fn main() -> u32 {
    return 4294967295u32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U64_Max_Compiles()
    {
        var source = @"
pub fn main() -> u64 {
    return 18446744073709551615u64
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== SIGNED OVERFLOW OPERATIONS ====================

    [Fact]
    public void BuildIr_I8_Addition_Overflow_Compiles()
    {
        var source = @"
pub fn main() -> i8 {
    let max: i8 = 127i8
    let one: i8 = 1i8
    return max + one
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I8_Negation_MinValue_Compiles()
    {
        var source = @"
pub fn main() -> i8 {
    let min: i8 = -128i8
    return -min
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32_Multiplication_Overflow_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = 100000i32
    let b: i32 = 100000i32
    return a * b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I64_Multiplication_Overflow_Compiles()
    {
        var source = @"
pub fn main() -> i64 {
    let a: i64 = 9000000000000000000i64
    let b: i64 = 2i64
    return a * b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== UNSIGNED UNDERFLOW OPERATIONS ====================

    [Fact]
    public void BuildIr_U8_Subtraction_Underflow_Compiles()
    {
        var source = @"
pub fn main() -> u8 {
    let zero: u8 = 0u8
    let one: u8 = 1u8
    return zero - one
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_U32_Subtraction_Underflow_Compiles()
    {
        var source = @"
pub fn main() -> u32 {
    let a: u32 = 100u32
    let b: u32 = 200u32
    return a - b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== SIGN EXTENSION AND CASTING ====================

    [Fact]
    public void BuildIr_SignedNegativeToUnsigned_Compiles()
    {
        var source = @"
pub fn main() -> u32 {
    let neg: i32 = -1i32
    return (u32)neg
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_SignExtend_I8ToI32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let neg: i8 = -1i8
    return (i32)neg
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ZeroExtend_U8ToU32_Compiles()
    {
        var source = @"
pub fn main() -> u32 {
    let val: u8 = 255u8
    return (u32)val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I8_ToU8_MinValue_Compiles()
    {
        var source = @"
pub fn main() -> u8 {
    let neg: i8 = -128i8
    return (u8)neg
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== MIXED-WIDTH OPERATIONS ====================

    [Fact]
    public void BuildIr_MixedWidth_I8AddI16_Compiles()
    {
        var source = @"
pub fn main() -> i16 {
    let a: i8 = 100i8
    let b: i16 = 1000i16
    return ((i16)a) + b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MixedWidth_I32AddI64_Compiles()
    {
        var source = @"
pub fn main() -> i64 {
    let a: i32 = 1000000i32
    let b: i64 = 2000000000i64
    return ((i64)a) + b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MixedWidth_U32AddU64_Compiles()
    {
        var source = @"
pub fn main() -> u64 {
    let a: u32 = 1000000u32
    let b: u64 = 2000000000u64
    return ((u64)a) + b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== NARROWING WITH OVERFLOW ====================

    [Fact]
    public void BuildIr_Narrow_I16ToI8_Overflow_Compiles()
    {
        var source = @"
pub fn main() -> i8 {
    let val: i16 = 200i16
    return (i8)val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Narrow_I32ToI8_Overflow_Compiles()
    {
        var source = @"
pub fn main() -> i8 {
    let val: i32 = 1000000i32
    return (i8)val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Narrow_U32ToU8_Overflow_Compiles()
    {
        var source = @"
pub fn main() -> u8 {
    let val: u32 = 1000u32
    return (u8)val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== SUBTRACTION WITH NEGATIVES ====================

    [Fact]
    public void BuildIr_NegativeSubtraction_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = -10i32
    let b: i32 = 20i32
    return a - b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_NegativeSubtraction_I64_Compiles()
    {
        var source = @"
pub fn main() -> i64 {
    let a: i64 = -1000000000i64
    let b: i64 = 1000000000i64
    return a - b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== DIVISION AND MODULO EDGE CASES ====================

    [Fact]
    public void BuildIr_DivisionByMinusOne_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = -2147483648i32
    let b: i32 = -1i32
    return a / b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ModuloNegative_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = -7i32
    let b: i32 = 3i32
    return a % b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ModuloNegativeDivisor_I32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let a: i32 = 7i32
    let b: i32 = -3i32
    return a % b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
```

---

## File 2: AdvancedPointerTests.cs

Test pointer arithmetic edge cases and type combinations.

```csharp
using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for advanced pointer scenarios: arithmetic edge cases, type combinations
/// </summary>
public class AdvancedPointerTests
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

    // ==================== POINTER TO ARRAY ====================

    [Fact]
    public void BuildIr_PointerToArray_Declaration_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = {10, 20, 30, 40, 50}
    let ptr: *[5]i32 = &arr as *[5]i32
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerToArray_DifferentSize_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr: [10]i32 = {1,2,3,4,5,6,7,8,9,10}
    let ptr: *[10]i32 = &arr as *[10]i32
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== ARRAY OF POINTERS ====================

    [Fact]
    public void BuildIr_ArrayOfPointers_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let addr1: u32 = 0x1000u32
    let addr2: u32 = 0x2000u32
    let addr3: u32 = 0x3000u32
    let arr: [3]*i32 = {
        addr1 as *i32,
        addr2 as *i32,
        addr3 as *i32
    }
    return (arr[0] as u32) == 0x1000u32 ? 1 : 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayOfPointers_DifferentTypes_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr: [3]*u8 = {
        0x1000u32 as *u8,
        0x2000u32 as *u8,
        0x3000u32 as *u8
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== POINTER ARITHMETIC EDGE CASES ====================

    [Fact]
    public void BuildIr_PointerArithmetic_MaxAddress_Compiles()
    {
        var source = @"
pub fn main() -> u32 {
    let ptr: *i32 = 0xFFFFFFFCu32 as *i32
    let offset: u32 = 4u32
    let result: u32 = (ptr as u32) + offset
    return result
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerArithmetic_SizeScaling_Compiles()
    {
        var source = @"
pub fn main() -> u32 {
    let base: u32 = 0x1000u32
    let ptr_u8: *u8 = base as *u8
    let ptr_i32: *i32 = base as *i32

    let addr_u8 = (ptr_u8 as u32) + 4u32
    let addr_i32 = (ptr_i32 as u32) + 4u32

    return addr_u8
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerArithmetic_LargeOffset_Compiles()
    {
        var source = @"
pub fn main() -> u32 {
    let ptr: *i32 = 0x1000u32 as *i32
    let large_offset: u32 = 0x10000000u32
    return (ptr as u32) + large_offset
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== POINTER-TO-POINTER ALIASING ====================

    [Fact]
    public void BuildIr_PointerToPointer_Aliasing_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 42
    let ptr1: *i32 = &x as *i32
    let ptr2: **i32 = &ptr1 as **i32
    return *(*ptr2)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerToPointer_Modification_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 42
    let ptr1: *i32 = &x as *i32
    let ptr2: **i32 = &ptr1 as **i32
    *ptr2 = 0 as *i32
    return (ptr1 as u32) == 0u32 ? 1 : 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== POINTERS TO DIFFERENT TYPES ====================

    [Fact]
    public void BuildIr_PointerCast_I32ToU8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let addr: u32 = 0x1000u32
    let ptr_i32: *i32 = addr as *i32
    let ptr_u8: *u8 = (addr as *u8)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerCast_StructToU32_Compiles()
    {
        var source = @"
struct Point { x: i32, y: i32 }
pub fn main() -> i32 {
    let addr: u32 = 0x1000u32
    let ptr: *Point = addr as *Point
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FUNCTION POINTER EDGE CASES ====================

    [Fact]
    public void BuildIr_FunctionPointer_NoParameters_Compiles()
    {
        var source = @"
fn get_value() -> i32 {
    return 42
}
pub fn main() -> i32 {
    let fp: fn() -> i32 = get_value
    return fp()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_FunctionPointer_MultipleParameters_Compiles()
    {
        var source = @"
fn add(a: i32, b: i32, c: i32, d: i32) -> i32 {
    return a + b + c + d
}
pub fn main() -> i32 {
    let fp: fn(i32, i32, i32, i32) -> i32 = add
    return fp(10, 20, 30, 40)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_FunctionPointer_VoidReturn_Compiles()
    {
        var source = @"
fn do_nothing() {
}
pub fn main() -> i32 {
    let fp: fn() = do_nothing
    fp()
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_FunctionPointer_Null_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let fp: fn() -> i32 = 0 as fn() -> i32
    return (fp as u32) == 0u32 ? 1 : 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== POINTER IN STRUCT WITH OPERATIONS ====================

    [Fact]
    public void BuildIr_PointerInStruct_LinkedList_Compiles()
    {
        var source = @"
struct Node {
    value: i32,
    next: *Node
}
pub fn main() -> i32 {
    let n1 = Node { value: 10, next: 0 as *Node }
    let n2 = Node { value: 20, next: &n1 as *Node }
    return n2.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== NULL POINTER OPERATIONS ====================

    [Fact]
    public void BuildIr_NullPointer_Cast_Compiles()
    {
        var source = @"
pub fn main() -> u32 {
    let null_ptr: *i32 = 0 as *i32
    return (null_ptr as u32)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_NullPointer_Equality_Check_Compiles()
    {
        var source = @"
pub fn main() -> bool {
    let ptr1: *i32 = 0 as *i32
    let ptr2: *i32 = 0 as *i32
    return (ptr1 as u32) == (ptr2 as u32)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_NullPointer_Inequality_Check_Compiles()
    {
        var source = @"
pub fn main() -> bool {
    let null_ptr: *i32 = 0 as *i32
    let addr: u32 = 0x1000u32
    let real_ptr: *i32 = addr as *i32
    return (null_ptr as u32) != (real_ptr as u32)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
```

---

## File 3: ComplexTypeCompositionTests.cs

Test combinations of advanced types.

```csharp
using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for complex type compositions: arrays with pointers, enums with references, generics with complex members
/// </summary>
public class ComplexTypeCompositionTests
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

    // ==================== ENUM WITH REFERENCES ====================

    [Fact]
    public void BuildIr_EnumWithReferenceVariant_Compiles()
    {
        var source = @"
enum ValueRef {
    Ref(&i32),
    Int(i32)
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EnumWithMutableReferenceVariant_Compiles()
    {
        var source = @"
enum MutValueRef {
    MutRef(&mut i32),
    Int(i32)
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_EnumWithMultipleReferenceVariants_Compiles()
    {
        var source = @"
enum ComplexRef {
    RefInt(&i32),
    MutRefInt(&mut i32),
    RefStruct(&Point),
    Value(i32)
}
struct Point { x: i32, y: i32 }
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== ENUM WITH POINTERS ====================

    [Fact]
    public void BuildIr_EnumWithPointerVariants_Compiles()
    {
        var source = @"
enum PtrValue {
    Ptr(*i32),
    Null,
    Int(i32)
}
pub fn main() -> i32 {
    let v1 = PtrValue::Ptr(0x1000u32 as *i32)
    let v2 = PtrValue::Null
    let v3 = PtrValue::Int(42)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== STRUCT WITH POINTER FIELDS ====================

    [Fact]
    public void BuildIr_StructWithPointerField_Compiles()
    {
        var source = @"
struct Buffer {
    data: *u8,
    len: u32
}
pub fn main() -> i32 {
    let buf = Buffer {
        data: 0x1000u32 as *u8,
        len: 1024u32
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructWithMultiplePointerFields_Compiles()
    {
        var source = @"
struct GraphNode {
    data: i32,
    left: *GraphNode,
    right: *GraphNode
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== ARRAY OF ARRAYS ====================

    [Fact]
    public void BuildIr_ArrayOfArrays_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let matrix: [[3]i32] = {{1,2,3}, {4,5,6}, {7,8,9}}
    return matrix[0][0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayOfArrays_ThreeDimensional_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let cube: [[[2]i32]] = {
        {{1,2}, {3,4}},
        {{5,6}, {7,8}}
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== STRUCT WITH ARRAY FIELD ====================

    [Fact]
    public void BuildIr_StructWithArrayField_Compiles()
    {
        var source = @"
struct ArrayContainer {
    data: [10]i32,
    count: u32
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructWithMultiDimArrayField_Compiles()
    {
        var source = @"
struct Matrix {
    data: [[5]i32]
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== NESTED STRUCT TYPES ====================

    [Fact]
    public void BuildIr_NestedStructs_Compiles()
    {
        var source = @"
struct Point { x: i32, y: i32 }
struct Rect { topLeft: Point, bottomRight: Point }
pub fn main() -> i32 {
    let p1 = Point { x: 0, y: 0 }
    let p2 = Point { x: 10, y: 10 }
    let rect = Rect { topLeft: p1, bottomRight: p2 }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_DeeplyNestedStructs_Compiles()
    {
        var source = @"
struct A { x: i32 }
struct B { a: A }
struct C { b: B }
struct D { c: C }
pub fn main() -> i32 {
    let d = D { c: C { b: B { a: A { x: 42 } } } }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== ENUM WITH STRUCT VARIANTS ====================

    [Fact]
    public void BuildIr_EnumWithStructVariants_Compiles()
    {
        var source = @"
struct Point { x: i32, y: i32 }
enum Location {
    Point(Point),
    Coordinates(i32, i32),
    Unknown
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== REFERENCE TO ARRAY ====================

    [Fact]
    public void BuildIr_ReferenceToMultiDimArray_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr: [[3]i32] = {{1,2,3}, {4,5,6}, {7,8,9}}
    let r: &[[3]i32] = &arr
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== STRUCT WITH REFERENCE FIELD ====================

    [Fact]
    public void BuildIr_StructWithReferenceField_Compiles()
    {
        var source = @"
struct Ref {
    target: &i32
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructWithMutableReferenceField_Compiles()
    {
        var source = @"
struct MutRef {
    target: &mut i32
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FUNCTION POINTER IN STRUCT ====================

    [Fact]
    public void BuildIr_StructWithFunctionPointer_Compiles()
    {
        var source = @"
struct Handler {
    callback: fn(i32) -> i32,
    data: i32
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructWithMultipleFunctionPointers_Compiles()
    {
        var source = @"
struct Handlers {
    on_init: fn(),
    on_update: fn(),
    on_cleanup: fn()
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== ENUM WITH MULTIPLE DATA TYPES ====================

    [Fact]
    public void BuildIr_EnumWithMixedVariantData_Compiles()
    {
        var source = @"
enum Message {
    Quit,
    MoveTo(i32, i32),
    ChangeSize(u32, u32),
    SetName(i32),
    Data(*u8, u32),
    Complex(i32, i32, i32)
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
```

---

## File 4: FloatingPointEdgeCaseTests.cs

Test IEEE 754 floating-point edge cases.

```csharp
using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for floating-point edge cases: NaN, Infinity, precision loss, special values
/// </summary>
public class FloatingPointEdgeCaseTests
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

    // ==================== INFINITY OPERATIONS ====================

    [Fact]
    public void BuildIr_F32_DivisionByZero_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: f32 = 1.0f32
    let y: f32 = 0.0f32
    return x / y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F64_DivisionByZero_Compiles()
    {
        var source = @"
pub fn main() -> f64 {
    let x: f64 = 1.0f64
    let y: f64 = 0.0f64
    return x / y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32_NegativeDivisionByZero_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: f32 = -1.0f32
    let y: f32 = 0.0f32
    return x / y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== INFINITY ARITHMETIC ====================

    [Fact]
    public void BuildIr_F32_InfinityAddition_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let inf: f32 = 1.0f32 / 0.0f32
    return inf + 1.0f32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32_InfinityMultiplication_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let inf: f32 = 1.0f32 / 0.0f32
    return inf * 2.0f32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32_InfinityDivision_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let inf: f32 = 1.0f32 / 0.0f32
    return inf / 2.0f32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== NaN OPERATIONS ====================

    [Fact]
    public void BuildIr_F32_NaNCreation_CompileS()
    {
        var source = @"
pub fn main() -> f32 {
    let x: f32 = 0.0f32
    return x / x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32_NaNAddition_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let nan: f32 = 0.0f32 / 0.0f32
    return nan + 1.0f32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32_NaNComparison_Compiles()
    {
        var source = @"
pub fn main() -> bool {
    let nan: f32 = 0.0f32 / 0.0f32
    return nan == nan
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== NEGATIVE ZERO ====================

    [Fact]
    public void BuildIr_F32_NegativeZero_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: f32 = -0.0f32
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32_NegativeZeroDivision_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: f32 = -1.0f32
    let z: f32 = -0.0f32
    return 1.0f32 / z
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== PRECISION LOSS ====================

    [Fact]
    public void BuildIr_F32_LargePrecisionLoss_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let large: f32 = 16777216.0f32
    let large_plus_one: f32 = large + 1.0f32
    return large
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F64_SmallPrecisionLoss_Compiles()
    {
        var source = @"
pub fn main() -> f64 {
    let x: f64 = 0.1f64
    let y: f64 = 0.2f64
    return x + y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FLOAT TO INT CONVERSION ====================

    [Fact]
    public void BuildIr_F32ToI32_Normal_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: f32 = 3.14f32
    return (i32)x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32ToI32_Negative_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x: f32 = -3.14f32
    return (i32)x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32ToI32_Infinity_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let inf: f32 = 1.0f32 / 0.0f32
    return (i32)inf
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32ToI32_NaN_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let nan: f32 = 0.0f32 / 0.0f32
    return (i32)nan
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32ToI32_OverflowPositive_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let large: f32 = 3000000000.0f32
    return (i32)large
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32ToI32_OverflowNegative_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let large: f32 = -3000000000.0f32
    return (i32)large
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== INT TO FLOAT CONVERSION ====================

    [Fact]
    public void BuildIr_I32ToF32_Small_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: i32 = 42
    return (f32)x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32ToF32_Large_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: i32 = 2147483647
    return (f32)x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32ToF32_Negative_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: i32 = -2147483648
    return (f32)x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== F32 TO F64 CONVERSION ====================

    [Fact]
    public void BuildIr_F32ToF64_Compiles()
    {
        var source = @"
pub fn main() -> f64 {
    let x: f32 = 3.14f32
    return (f64)x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F64ToF32_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: f64 = 3.141592653589793f64
    return (f32)x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== SUBNORMAL NUMBERS ====================

    [Fact]
    public void BuildIr_F32_VerySmallNumber_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let x: f32 = 0.00000001f32
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== SPECIAL FLOAT COMPARISONS ====================

    [Fact]
    public void BuildIr_F32_ZeroComparison_Compiles()
    {
        var source = @"
pub fn main() -> bool {
    let zero: f32 = 0.0f32
    return zero == 0.0f32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F32_NegativeZeroEquals PositiveZero_Compiles()
    {
        var source = @"
pub fn main() -> bool {
    let pos_zero: f32 = 0.0f32
    let neg_zero: f32 = -0.0f32
    return pos_zero == neg_zero
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
```

---

## File 5: FixedPointTests.cs

Test fixed-point arithmetic edge cases.

```csharp
using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for fixed-point arithmetic: boundaries, overflow, precision, conversions
/// </summary>
public class FixedPointTests
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

    // ==================== FIXED16 BOUNDARIES ====================

    [Fact]
    public void BuildIr_Fixed16_MinBoundary_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    return -128.0fixed16
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed16_MaxBoundary_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    return 127.99609375fixed16
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED32 BOUNDARIES ====================

    [Fact]
    public void BuildIr_Fixed32_MinBoundary_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    return -32768.0fixed32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32_MaxBoundary_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    return 32767.9999fixed32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED-POINT ARITHMETIC ====================

    [Fact]
    public void BuildIr_Fixed16_Addition_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    let a: fixed16 = 10.5fixed16
    let b: fixed16 = 20.25fixed16
    return a + b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed16_Subtraction_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    let a: fixed16 = 50.75fixed16
    let b: fixed16 = 20.25fixed16
    return a - b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32_Multiplication_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    let a: fixed32 = 2.5fixed32
    let b: fixed32 = 3.5fixed32
    return a * b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32_Division_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    let a: fixed32 = 10.0fixed32
    let b: fixed32 = 2.0fixed32
    return a / b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED-POINT OVERFLOW ====================

    [Fact]
    public void BuildIr_Fixed16_AdditionOverflow_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    let max: fixed16 = 127.99609375fixed16
    let small: fixed16 = 0.1fixed16
    return max + small
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32_MultiplicationOverflow_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    let large: fixed32 = 1000.0fixed32
    let factor: fixed32 = 100.0fixed32
    return large * factor
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED TO INT CONVERSION ====================

    [Fact]
    public void BuildIr_Fixed16ToI32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let f: fixed16 = 42.75fixed16
    return (i32)f
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32ToI32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let f: fixed32 = 42.75fixed32
    return (i32)f
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== INT TO FIXED CONVERSION ====================

    [Fact]
    public void BuildIr_I32ToFixed16_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    let i: i32 = 42
    return (fixed16)i
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_I32ToFixed32_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    let i: i32 = 42
    return (fixed32)i
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FLOAT TO FIXED CONVERSION ====================

    [Fact]
    public void BuildIr_F32ToFixed16_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    let f: f32 = 42.5f32
    return (fixed16)f
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_F64ToFixed32_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    let f: f64 = 100.75f64
    return (fixed32)f
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED TO FLOAT CONVERSION ====================

    [Fact]
    public void BuildIr_Fixed16ToF32_Compiles()
    {
        var source = @"
pub fn main() -> f32 {
    let f: fixed16 = 42.5fixed16
    return (f32)f
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32ToF64_Compiles()
    {
        var source = @"
pub fn main() -> f64 {
    let f: fixed32 = 100.75fixed32
    return (f64)f
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED-POINT PRECISION ====================

    [Fact]
    public void BuildIr_Fixed16_PrecisionLimit_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    let a: fixed16 = 1.0fixed16
    let b: fixed16 = 0.00390625fixed16
    return a + b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32_PrecisionLimit_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    let a: fixed32 = 1.0fixed32
    let b: fixed32 = 0.000015258789fixed32
    return a + b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED-POINT WITH NEGATIVES ====================

    [Fact]
    public void BuildIr_Fixed16_Negative_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    let x: fixed16 = -42.5fixed16
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32_NegativeMultiplication_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    let a: fixed32 = -10.5fixed32
    let b: fixed32 = 2.0fixed32
    return a * b
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ==================== FIXED16 TO FIXED32 CONVERSION ====================

    [Fact]
    public void BuildIr_Fixed16ToFixed32_Compiles()
    {
        var source = @"
pub fn main() -> fixed32 {
    let f16: fixed16 = 42.5fixed16
    return (fixed32)f16
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Fixed32ToFixed16_Compiles()
    {
        var source = @"
pub fn main() -> fixed16 {
    let f32: fixed32 = 42.5fixed32
    return (fixed16)f32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
```

---

## Implementation Notes

These test files should be added to `/Users/barry/RiderProjects/Novus/Novus.Tests/`

Key considerations:
1. All tests use `BuildIr()` to verify code compiles to IR successfully
2. Tests don't validate runtime behavior (values) - that would require execution
3. Tests focus on type system correctness and edge case handling
4. Each test is self-contained and independent
5. Comment headers clearly show what category each test covers

## Running the Tests

```bash
cd /Users/barry/RiderProjects/Novus
dotnet test Novus.Tests --filter EdgeCaseNumericTests
dotnet test Novus.Tests --filter AdvancedPointerTests
dotnet test Novus.Tests --filter ComplexTypeCompositionTests
dotnet test Novus.Tests --filter FloatingPointEdgeCaseTests
dotnet test Novus.Tests --filter FixedPointTests
```
