using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class GenericFunctionTests
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
    public void BuildIr_GenericFunction_SingleTypeParam_Compiles()
    {
        var source = @"
fn identity<T>(x: T) -> T {
    return x
}
pub fn main() -> i32 {
    return identity(42)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_MultipleTypes_Compiles()
    {
        var source = @"
fn identity<T>(x: T) -> T {
    return x
}
pub fn main() -> i32 {
    let a = identity(42)
    let b = identity(true)
    return a
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_TwoTypeParams_Compiles()
    {
        var source = @"
fn first<T, U>(a: T, b: U) -> T {
    return a
}
pub fn main() -> i32 {
    return first(10, true)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_WithGenericStruct_Compiles()
    {
        var source = @"
struct Box<T> {
    value: T
}
fn unbox<T>(b: Box<T>) -> T {
    return b.value
}
pub fn main() -> i32 {
    let box = Box { value: 42 }
    return unbox(box)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_ReturnsGenericType_Compiles()
    {
        var source = @"
struct Box<T> {
    value: T
}
fn makeBox<T>(val: T) -> Box<T> {
    return Box { value: val }
}
pub fn main() -> i32 {
    let box = makeBox(42)
    return box.value
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_CallsGenericFunction_Compiles()
    {
        var source = @"
fn identity<T>(x: T) -> T {
    return x
}
fn double<T>(x: T) -> T {
    return identity(x)
}
pub fn main() -> i32 {
    return double(42)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_WithEnum_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
fn unwrap<T>(opt: Option<T>, default: T) -> T {
    match opt {
        Option::Some(v) => return v,
        Option::None => return default,
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::Some(42)
    return unwrap(opt, 0)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_Inference_Compiles()
    {
        var source = @"
fn identity<T>(x: T) -> T {
    return x
}
pub fn main() -> i32 {
    let val = identity(42)
    return val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_MultipleCallSites_Compiles()
    {
        var source = @"
fn add<T>(a: T, b: T) -> T {
    return a + b
}
pub fn main() -> i32 {
    let x = add(10, 20)
    let y = add(5, 15)
    return x + y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_AsParameter_Compiles()
    {
        var source = @"
fn apply<T>(x: T) -> T {
    return x
}
fn useFunction(val: i32) -> i32 {
    return apply(val)
}
pub fn main() -> i32 {
    return useFunction(42)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_WithPointer_Compiles()
    {
        var source = @"
fn identity<T>(x: *T) -> *T {
    return x
}
pub fn main() -> i32 {
    let ptr: *i32 = 0 as *i32
    let result = identity(ptr)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_ThreeTypeParams_Compiles()
    {
        var source = @"
fn triple<A, B, C>(a: A, b: B, c: C) -> A {
    return a
}
pub fn main() -> i32 {
    return triple(42, true, 100)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_SameTypeMultipleParams_Compiles()
    {
        var source = @"
fn max<T>(a: T, b: T) -> T {
    if a > b {
        return a
    }
    return b
}
pub fn main() -> i32 {
    return max(10, 20)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_NestedGenericCalls_Compiles()
    {
        var source = @"
fn identity<T>(x: T) -> T {
    return x
}
pub fn main() -> i32 {
    return identity(identity(42))
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericFunction_InImplBlock_Compiles()
    {
        var source = @"
struct Container<T> {
    value: T
}
impl<T> Container<T> {
    fn getValue(&self) -> T {
        return (*self).value
    }
}
pub fn main() -> i32 {
    let c = Container { value: 42 }
    return c.getValue()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
