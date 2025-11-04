using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class ConstStaticTests
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
    public void BuildIr_ConstDeclaration_i32_Compiles()
    {
        var source = @"
const MAX_SIZE: i32 = 100
pub fn main() -> i32 {
    return MAX_SIZE
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ConstDeclaration_MultipleTypes_Compiles()
    {
        var source = @"
const SIZE_8: i8 = 10
const SIZE_16: i16 = 1000
const SIZE_32: i32 = 100000
const SIZE_64: i64 = 1000000000
pub fn main() -> i32 {
    return SIZE_32
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ConstDeclaration_TypeInference_Compiles()
    {
        var source = @"
const VALUE = 42
pub fn main() -> i32 {
    return VALUE
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ConstExpression_Arithmetic_Compiles()
    {
        var source = @"
const SIZE = 10 * 20 + 5
pub fn main() -> i32 {
    return SIZE
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ConstExpression_Bitwise_Compiles()
    {
        var source = @"
const MASK = 0xFF & 0x0F
pub fn main() -> i32 {
    return (i32)MASK
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PublicConst_Compiles()
    {
        var source = @"
pub const MAX_VALUE: i32 = 255
pub fn main() -> i32 {
    return MAX_VALUE
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_InternalConst_Compiles()
    {
        var source = @"
internal const BUFFER_SIZE: i32 = 1024
pub fn main() -> i32 {
    return BUFFER_SIZE
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StaticVariable_Compiles()
    {
        var source = @"
static COUNTER: i32 = 0
pub fn main() -> i32 {
    return COUNTER
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StaticMutable_Compiles()
    {
        var source = @"
static mut GLOBAL_STATE: i32 = 0
pub fn main() -> i32 {
    GLOBAL_STATE = 42
    return GLOBAL_STATE
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_PublicStatic_Compiles()
    {
        var source = @"
pub static INSTANCE_COUNT: i32 = 0
pub fn main() -> i32 {
    return INSTANCE_COUNT
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ConstInArraySize_Compiles()
    {
        var source = @"
const ARRAY_SIZE = 5
pub fn main() -> i32 {
    let arr: [i32; ARRAY_SIZE] = [1, 2, 3, 4, 5]
    return arr[0]
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleConsts_Compiles()
    {
        var source = @"
const WIDTH: i32 = 320
const HEIGHT: i32 = 200
const AREA: i32 = WIDTH * HEIGHT
pub fn main() -> i32 {
    return AREA
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ConstBool_Compiles()
    {
        var source = @"
const DEBUG_MODE: bool = true
pub fn main() -> i32 {
    if DEBUG_MODE {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StaticAcrossFunctions_Compiles()
    {
        var source = @"
static mut CALL_COUNT: i32 = 0
fn increment() {
    CALL_COUNT = CALL_COUNT + 1
}
pub fn main() -> i32 {
    increment()
    increment()
    return CALL_COUNT
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ConstShadowing_Compiles()
    {
        var source = @"
const VALUE: i32 = 10
pub fn main() -> i32 {
    let VALUE = 20
    return VALUE
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
