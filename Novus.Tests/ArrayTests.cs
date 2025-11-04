using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class ArrayTests
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
    public void BuildIr_ArrayLiteral_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [1, 2, 3, 4, 5]
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayIndexRead_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [10, 20, 30]
    return arr[1]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayIndexWrite_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [1, 2, 3]
    arr[0] = 10
    return arr[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayType_ExplicitSizeAndType_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr: [i32; 3] = [1, 2, 3]
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayInLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [1, 2, 3, 4, 5]
    var sum = 0
    for (var i = 0; i < 5; i++) {
        sum += arr[i]
    }
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayAsParameter_Compiles()
    {
        var source = @"
fn getFirst(arr: [i32; 3]) -> i32 {
    return arr[0]
}
pub fn main() -> i32 {
    let arr = [10, 20, 30]
    return getFirst(arr)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayAsReturnType_Compiles()
    {
        var source = @"
fn makeArray() -> [i32; 3] {
    return {1, 2, 3}
}
pub fn main() -> i32 {
    let arr = makeArray()
    return arr[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayOfDifferentTypes_i8_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr: [i8; 3] = [1, 2, 3]
    let val = arr[0]
    return (i32)val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayOfDifferentTypes_u32_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr: [u32; 3] = [100, 200, 300]
    let val = arr[0]
    return (i32)val
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayModificationInLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [0, 0, 0, 0, 0]
    for (var i = 0; i < 5; i++) {
        arr[i] = i * 2
    }
    return arr[3]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayInStruct_Compiles()
    {
        var source = @"
struct Container {
    data: [i32; 5],
    count: i32
}
pub fn main() -> i32 {
    let c = Container {
        data: [1, 2, 3, 4, 5],
        count: 5
    }
    return c.data[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArraySizeZero_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
    return arr[5]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayCompoundAssignment_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var arr = [1, 2, 3]
    arr[0] += 10
    return arr[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayVariableIndex_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr = [10, 20, 30, 40, 50]
    var idx = 2
    return arr[idx]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ArrayNested_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let arr1 = [1, 2, 3]
    let arr2 = [4, 5, 6]
    return arr1[arr2[0] - 4]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
