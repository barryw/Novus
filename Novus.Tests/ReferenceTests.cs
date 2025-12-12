using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class ReferenceTests
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
    public void BuildIr_ImmutableReference_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 42
    let r: &i32 = &x
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MutableReference_i32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 42
    let r: &var i32 = &var x
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReferenceDereference_Read_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 42
    let r: &i32 = &x
    let val = *r
    return val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MutableReferenceDereference_Write_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 42
    let r: &var i32 = &var x
    *r = 100
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReferenceAsParameter_Immutable_Compiles()
    {
        var source = @"
fn readValue(r: &i32) -> i32 {
    return *r
}
pub fn main() -> i32 {
    let x = 42
    return readValue(&x)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReferenceAsParameter_Mutable_Compiles()
    {
        var source = @"
fn increment(r: &var i32) {
    *r = *r + 1
}
pub fn main() -> i32 {
    var x = 42
    increment(&var x)
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReferenceAsReturnType_Compiles()
    {
        var source = @"
fn getRef(x: &i32) -> &i32 {
    return x
}
pub fn main() -> i32 {
    let x = 42
    let r = getRef(&x)
    return *r
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReferenceToStruct_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    let r: &Point = &p
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }


    [Fact]
    public void BuildIr_ReferenceInLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var sum = 0
    for (var i = 0; i < 5; i++) {
        let r: &i32 = &i
        sum += *r
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleReferences_SameValue_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 42
    let r1: &i32 = &x
    let r2: &i32 = &x
    return *r1 + *r2
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReferenceToArray_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [1, 2, 3, 4, 5]
    let r: &[i32; 5] = &arr
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }


    [Fact]
    public void BuildIr_SelfReference_Method_Immutable_Compiles()
    {
        var source = @"
struct Counter {
    count: i32
}
impl Counter {
    fn get(&self) -> i32 {
        return (*self).count
    }
}
pub fn main() -> i32 {
    let c = Counter { count: 42 }
    return c.get()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_SelfReference_Method_Mutable_Compiles()
    {
        var source = @"
struct Counter {
    count: i32
}
impl Counter {
    fn increment(&var self) {
        (*self).count = (*self).count + 1
    }
}
pub fn main() -> i32 {
    var c = Counter { count: 42 }
    c.increment()
    return c.count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReferenceCompoundAssignment_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var x = 10
    let r: &var i32 = &var x
    *r += 5
    return x
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ReferenceChain_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 42
    let r1: &i32 = &x
    let r2: &i32 = r1
    return *r2
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

}
