using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class PointerTests
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
    public void BuildIr_PointerType_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let ptr: *i32 = 0 as *i32
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerType_u8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let ptr: *u8 = 0 as *u8
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerType_Struct_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    let ptr: *Point = 0 as *Point
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerComparison_Null_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let ptr: *i32 = 0 as *i32
    if (u32)ptr == 0 {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerAsParameter_Compiles()
    {
        var source = @"
fn usePointer(ptr: *i32) -> i32 {
    return 0
}
pub fn main() -> i32 {
    let ptr: *i32 = 0 as *i32
    return usePointer(ptr)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerAsReturnType_Compiles()
    {
        var source = @"
fn getPointer() -> *i32 {
    return 0 as *i32
}
pub fn main() -> i32 {
    let ptr = getPointer()
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerToPointer_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let ptr: **i32 = 0 as **i32
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerInStruct_Compiles()
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
    public void BuildIr_PointerCast_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let addr: u32 = 0x1000
    let ptr: *i32 = addr as *i32
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerToU32Cast_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let ptr: *i32 = 0 as *i32
    let addr: u32 = ptr as u32
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerInEnum_Compiles()
    {
        var source = @"
enum Value {
    Int(i32),
    Ptr(*i32)
}
pub fn main() -> i32 {
    let v = Value::Ptr(0 as *i32)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerMatch_Compiles()
    {
        var source = @"
enum Value {
    Int(i32),
    Ptr(*i32)
}
pub fn main() -> i32 {
    let v = Value::Int(42)
    match v {
        Value::Int(x) => return x,
        Value::Ptr(p) => return 0,
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_DereferenceExpr_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 42
    let ptr: *i32 = &x as *i32
    let val = *ptr
    return val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_DereferenceAndAssign_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 42
    let ptr: *i32 = &x as *i32
    *ptr = 100
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerArithmetic_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let ptr: *i32 = 0x1000 as *i32
    let offset: u32 = 4
    let ptr2_addr = (ptr as u32) + offset
    let ptr2: *i32 = ptr2_addr as *i32
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_FunctionPointer_Compiles()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}
pub fn main() -> i32 {
    let fptr: fn(i32, i32) -> i32 = add
    return fptr(10, 20)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_FunctionPointerAsParameter_Compiles()
    {
        var source = @"
fn apply(f: fn(i32) -> i32, x: i32) -> i32 {
    return f(x)
}
fn double(x: i32) -> i32 {
    return x * 2
}
pub fn main() -> i32 {
    return apply(double, 21)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerInLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for (var i = 0; i < 5; i++) {
        let addr: u32 = 0x1000 + ((u32)i * 4)
        let ptr: *i32 = addr as *i32
        sum += (i32)addr
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_NullPointerCheck_Compiles()
    {
        var source = @"
fn isNull(ptr: *i32) -> bool {
    return (u32)ptr == 0
}
pub fn main() -> i32 {
    let ptr: *i32 = 0 as *i32
    if isNull(ptr) {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PointerEquality_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let ptr1: *i32 = 0x1000 as *i32
    let ptr2: *i32 = 0x1000 as *i32
    if (u32)ptr1 == (u32)ptr2 {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
