using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class StructOperationsTests
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

    [Fact]
    public void BuildIr_Struct_FiveFields_Compiles()
    {
        var source = @"
struct Vector {
    x: i32,
    y: i32,
    z: i32,
    w: i32,
    id: i32
}
pub fn main() -> i32 {
    let v = Vector { x: 1, y: 2, z: 3, w: 4, id: 5 }
    return v.x + v.y + v.z + v.w + v.id
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_WithArrayField_Compiles()
    {
        var source = @"
struct Container {
    data: [5]i32,
    count: i32
}
pub fn main() -> i32 {
    let c = Container {
        data: {1, 2, 3, 4, 5},
        count: 5
    }
    return c.data[0] + c.count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_WithPointerField_Compiles()
    {
        var source = @"
struct Node {
    value: i32,
    next: *Node
}
pub fn main() -> i32 {
    let n = Node {
        value: 42,
        next: 0 as *Node
    }
    return n.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_NestedStructField_Compiles()
    {
        var source = @"
struct Inner {
    x: i32,
    y: i32
}
struct Outer {
    inner: Inner,
    z: i32
}
pub fn main() -> i32 {
    let o = Outer {
        inner: Inner { x: 10, y: 20 },
        z: 30
    }
    return o.inner.x + o.inner.y + o.z
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_FieldMutation_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    var p = Point { x: 10, y: 20 }
    p.x = 100
    p.y = 200
    return p.x + p.y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_FieldCompoundAssignment_Compiles()
    {
        var source = @"
struct Counter {
    count: i32,
    step: i32
}
pub fn main() -> i32 {
    var c = Counter { count: 10, step: 5 }
    c.count += c.step
    return c.count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_FieldIncrement_Compiles()
    {
        var source = @"
struct Counter {
    value: i32
}
pub fn main() -> i32 {
    var c = Counter { value: 10 }
    c.value++
    ++c.value
    return c.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_FieldInConditional_Compiles()
    {
        var source = @"
struct Status {
    active: bool,
    count: i32
}
pub fn main() -> i32 {
    let s = Status { active: true, count: 10 }
    if s.active && s.count > 5 {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_FieldInLoop_Compiles()
    {
        var source = @"
struct Range {
    start: i32,
    end: i32
}
pub fn main() -> i32 {
    var r = Range { start: 0, end: 10 }
    var sum = 0
    while r.start < r.end {
        sum += r.start
        r.start = r.start + 1
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_AllPrimitiveTypes_Compiles()
    {
        var source = @"
struct AllTypes {
    a: i8,
    b: i16,
    c: i32,
    d: i64,
    e: u8,
    f: u16,
    g: u32,
    h: u64,
    flag: bool
}
pub fn main() -> i32 {
    let t = AllTypes {
        a: 1, b: 2, c: 3, d: 4,
        e: 5, f: 6, g: 7, h: 8,
        flag: true
    }
    return (i32)t.a + (i32)t.b + t.c
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_WithEnumField_Compiles()
    {
        var source = @"
enum State {
    Active,
    Inactive
}
struct Object {
    id: i32,
    state: State
}
pub fn main() -> i32 {
    let obj = Object {
        id: 42,
        state: State::Active
    }
    return obj.id
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_InArray_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    let points = {
        Point { x: 1, y: 2 },
        Point { x: 3, y: 4 },
        Point { x: 5, y: 6 }
    }
    return points[0].x + points[1].y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_ArrayElementMutation_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    var points = {
        Point { x: 1, y: 2 },
        Point { x: 3, y: 4 }
    }
    points[0].x = 100
    return points[0].x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_NestedFieldAccess_Compiles()
    {
        var source = @"
struct Inner {
    value: i32
}
struct Middle {
    inner: Inner
}
struct Outer {
    middle: Middle
}
pub fn main() -> i32 {
    let o = Outer {
        middle: Middle {
            inner: Inner { value: 42 }
        }
    }
    return o.middle.inner.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_AsParameter_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
fn sumPoint(p: Point) -> i32 {
    return p.x + p.y
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return sumPoint(p)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_AsReturnType_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
fn makePoint(x: i32, y: i32) -> Point {
    return Point { x: x, y: y }
}
pub fn main() -> i32 {
    let p = makePoint(10, 20)
    return p.x + p.y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_WithReferenceField_Compiles()
    {
        var source = @"
struct Wrapper {
    value: i32,
    ptr: *i32
}
pub fn main() -> i32 {
    var x = 42
    let w = Wrapper {
        value: 10,
        ptr: &x as *i32
    }
    return w.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_MultipleFieldModifications_Compiles()
    {
        var source = @"
struct Stats {
    count: i32,
    sum: i32,
    max: i32
}
pub fn main() -> i32 {
    var s = Stats { count: 0, sum: 0, max: 0 }
    s.count = s.count + 1
    s.sum = s.sum + 10
    s.max = 10
    return s.count + s.sum + s.max
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_LargeStruct_Compiles()
    {
        var source = @"
struct Large {
    f1: i32, f2: i32, f3: i32, f4: i32, f5: i32,
    f6: i32, f7: i32, f8: i32, f9: i32, f10: i32
}
pub fn main() -> i32 {
    let l = Large {
        f1: 1, f2: 2, f3: 3, f4: 4, f5: 5,
        f6: 6, f7: 7, f8: 8, f9: 9, f10: 10
    }
    return l.f1 + l.f5 + l.f10
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_CopySemantics_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    let p1 = Point { x: 10, y: 20 }
    var p2 = p1
    p2.x = 100
    return p1.x + p2.x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_PartialFieldInit_Compiles()
    {
        var source = @"
struct Config {
    width: i32,
    height: i32,
    depth: i32
}
pub fn main() -> i32 {
    let c = Config { width: 800, height: 600, depth: 32 }
    return c.width + c.height + c.depth
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_ZeroInitFields_Compiles()
    {
        var source = @"
struct Stats {
    count: i32,
    sum: i32
}
pub fn main() -> i32 {
    let s = Stats { count: 0, sum: 0 }
    return s.count + s.sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_BooleanFields_Compiles()
    {
        var source = @"
struct Flags {
    enabled: bool,
    visible: bool,
    active: bool
}
pub fn main() -> i32 {
    let f = Flags { enabled: true, visible: false, active: true }
    if f.enabled && f.active {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_MixedSignedUnsigned_Compiles()
    {
        var source = @"
struct Mixed {
    signed: i32,
    unsigned: u32
}
pub fn main() -> i32 {
    let m = Mixed { signed: -10, unsigned: 10 }
    return m.signed + (i32)m.unsigned
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_Struct_EmptyStruct_Compiles()
    {
        var source = @"
struct Empty {
}
pub fn main() -> i32 {
    let e = Empty { }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
