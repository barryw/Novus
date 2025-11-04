using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class ComplexLvalueOperatorTests
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

    // Array element compound assignment
    [Fact]
    public void BuildIr_ArrayElement_CompoundAdd_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [1, 2, 3, 4, 5]
    arr[0] += 10
    return arr[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayElement_CompoundSub_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [10, 20, 30]
    arr[1] -= 5
    return arr[1]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayElement_CompoundMul_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [2, 3, 4]
    arr[2] *= 5
    return arr[2]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayElement_CompoundDiv_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [100, 50, 25]
    arr[0] /= 10
    return arr[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayElement_Increment_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [1, 2, 3]
    arr[1]++
    return arr[1]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayElement_Decrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [10, 20, 30]
    arr[2]--
    return arr[2]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayElement_PreIncrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [1, 2, 3]
    ++arr[0]
    return arr[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayElement_PreDecrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [10, 20, 30]
    --arr[1]
    return arr[1]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Struct field compound assignment
    [Fact]
    public void BuildIr_StructField_CompoundAdd_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    var p = Point { x: 10, y: 20 }
    p.x += 5
    return p.x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructField_CompoundSub_Compiles()
    {
        var source = @"
struct Counter {
    count: i32
}
pub fn main() -> i32 {
    var c = Counter { count: 100 }
    c.count -= 25
    return c.count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructField_CompoundMul_Compiles()
    {
        var source = @"
struct Value {
    num: i32
}
pub fn main() -> i32 {
    var v = Value { num: 5 }
    v.num *= 3
    return v.num
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructField_CompoundDiv_Compiles()
    {
        var source = @"
struct Value {
    num: i32
}
pub fn main() -> i32 {
    var v = Value { num: 50 }
    v.num /= 5
    return v.num
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructField_Increment_Compiles()
    {
        var source = @"
struct Counter {
    count: i32
}
pub fn main() -> i32 {
    var c = Counter { count: 10 }
    c.count++
    return c.count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructField_Decrement_Compiles()
    {
        var source = @"
struct Counter {
    count: i32
}
pub fn main() -> i32 {
    var c = Counter { count: 10 }
    c.count--
    return c.count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructField_PreIncrement_Compiles()
    {
        var source = @"
struct Counter {
    value: i32
}
pub fn main() -> i32 {
    var c = Counter { value: 5 }
    ++c.value
    return c.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructField_PreDecrement_Compiles()
    {
        var source = @"
struct Counter {
    value: i32
}
pub fn main() -> i32 {
    var c = Counter { value: 10 }
    --c.value
    return c.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Pointer dereference compound assignment
    [Fact]
    public void BuildIr_PointerDeref_CompoundAdd_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    let p = &x as *i32
    *p += 5
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerDeref_CompoundSub_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 20
    let p = &x as *i32
    *p -= 5
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerDeref_CompoundMul_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    let p = &x as *i32
    *p *= 3
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerDeref_CompoundDiv_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 50
    let p = &x as *i32
    *p /= 5
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerDeref_Increment_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    let p = &x as *i32
    (*p)++
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerDeref_Decrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    let p = &x as *i32
    (*p)--
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerDeref_PreIncrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 5
    let p = &x as *i32
    ++(*p)
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerDeref_PreDecrement_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    let p = &x as *i32
    --(*p)
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Nested field access
    [Fact]
    public void BuildIr_NestedField_CompoundAdd_Compiles()
    {
        var source = @"
struct Inner {
    value: i32
}
struct Outer {
    inner: Inner
}
pub fn main() -> i32 {
    var o = Outer { inner: Inner { value: 10 } }
    o.inner.value += 5
    return o.inner.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_NestedField_Increment_Compiles()
    {
        var source = @"
struct Inner {
    count: i32
}
struct Outer {
    inner: Inner
}
pub fn main() -> i32 {
    var o = Outer { inner: Inner { count: 10 } }
    o.inner.count++
    return o.inner.count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Array of structs
    [Fact]
    public void BuildIr_ArrayOfStructs_FieldIncrement_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    var points = [
        Point { x: 1, y: 2 },
        Point { x: 3, y: 4 }
    ]
    points[0].x++
    return points[0].x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayOfStructs_FieldCompoundAdd_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    var points = [
        Point { x: 1, y: 2 },
        Point { x: 3, y: 4 }
    ]
    points[1].y += 10
    return points[1].y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // Struct with array field
    [Fact]
    public void BuildIr_StructArrayField_ElementIncrement_Compiles()
    {
        var source = @"
struct Container {
    data: [i32; 5]
}
pub fn main() -> i32 {
    var c = Container { data: [1, 2, 3, 4, 5] }
    c.data[2]++
    return c.data[2]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StructArrayField_ElementCompoundAdd_Compiles()
    {
        var source = @"
struct Container {
    values: [i32; 3]
}
pub fn main() -> i32 {
    var c = Container { values: [10, 20, 30] }
    c.values[1] += 5
    return c.values[1]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ComplexChain_ArrayStructFieldArray_Compiles()
    {
        var source = @"
struct Inner {
    data: [i32; 3]
}
pub fn main() -> i32 {
    var arr = [
        Inner { data: [1, 2, 3] },
        Inner { data: [4, 5, 6] }
    ]
    arr[0].data[1]++
    return arr[0].data[1]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
