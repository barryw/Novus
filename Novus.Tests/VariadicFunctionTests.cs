using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class VariadicFunctionTests
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
    public void VariadicFunction_ExternDeclaration_ParsesCorrectly()
    {
        var source = @"
extern fn printf(format: *u8, ...args) -> i32
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var printf = module.Functions.FirstOrDefault(f => f.Name == "printf");
        Assert.NotNull(printf);
        Assert.True(printf.IsExtern);
        Assert.True(printf.IsVariadic);
        Assert.Equal(2, printf.Parameters.Count);
        Assert.Equal("format", printf.Parameters[0].Name);
        Assert.Equal("args", printf.Parameters[1].Name);
        Assert.True(printf.Parameters[1].IsVariadic);
    }

    [Fact]
    public void VariadicFunction_WithoutRegularParams_ParsesCorrectly()
    {
        var source = @"
extern fn test(...args) -> i32
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var test = module.Functions.FirstOrDefault(f => f.Name == "test");
        Assert.NotNull(test);
        Assert.True(test.IsVariadic);
        Assert.Single(test.Parameters);
        Assert.True(test.Parameters[0].IsVariadic);
    }

    [Fact]
    public void VariadicFunction_MultipleRegularParamsPlusVariadic_ParsesCorrectly()
    {
        var source = @"
extern fn custom_printf(stream: i32, flags: i32, format: *u8, ...args) -> i32
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var func = module.Functions.FirstOrDefault(f => f.Name == "custom_printf");
        Assert.NotNull(func);
        Assert.True(func.IsVariadic);
        Assert.Equal(4, func.Parameters.Count);
        Assert.False(func.Parameters[0].IsVariadic);
        Assert.False(func.Parameters[1].IsVariadic);
        Assert.False(func.Parameters[2].IsVariadic);
        Assert.True(func.Parameters[3].IsVariadic);
        Assert.Equal("args", func.Parameters[3].Name);
    }

    [Fact]
    public void VariadicFunction_CallWithExtraArgs_CompilesSuccessfully()
    {
        var source = @"
from std::string::core import Str
extern fn printf(format: *u8, ...args) -> i32
pub fn main() -> i32 {
    let msg: Str = ""test""
    printf(msg.ptr, 42, 3.14)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void VariadicFunction_CallWithMinimumArgs_CompilesSuccessfully()
    {
        var source = @"
from std::string::core import Str
extern fn printf(format: *u8, ...args) -> i32
pub fn main() -> i32 {
    let msg: Str = ""test""
    printf(msg.ptr)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void VariadicFunction_OnlyVariadicParam_CallWithArgs_CompilesSuccessfully()
    {
        var source = @"
extern fn test(...args) -> i32
pub fn main() -> i32 {
    test(1, 2, 3)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void VariadicFunction_GeneratesCorrectCSignature()
    {
        var source = @"
from std::string::core import Str
extern fn my_variadic_func(format: *u8, ...args) -> i32
pub fn main() -> i32 {
    let msg: Str = ""test""
    my_variadic_func(msg.ptr, 42)
    return 0
}";
        var module = BuildIr(source);
        var codegen = new Codegen.CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "auto");
        var cCode = codegen.Generate();

        // Should generate extern declaration with variadic: extern int32_t my_variadic_func(uint8_t* format, ...);
        Assert.Contains("my_variadic_func(uint8_t* format, ...)", cCode);
    }

    [Fact]
    public void VariadicFunction_OnlyVariadic_GeneratesCorrectCSignature()
    {
        var source = @"
extern fn my_test_func(...args) -> i32
pub fn main() -> i32 {
    my_test_func(1, 2, 3)
    return 0
}";
        var module = BuildIr(source);
        var codegen = new Codegen.CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "auto");
        var cCode = codegen.Generate();

        // Should generate extern declaration with only variadic: extern int32_t my_test_func(...);
        Assert.Contains("my_test_func(...)", cCode);
    }

    [Fact]
    public void NonVariadicFunction_IsVariadicFlagIsFalse()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        var add = module.Functions.FirstOrDefault(f => f.Name == "add");
        Assert.NotNull(add);
        Assert.False(add.IsVariadic);
        Assert.All(add.Parameters, p => Assert.False(p.IsVariadic));
    }

    [Fact]
    public void VariadicFunction_InImplBlock_ParsesCorrectly()
    {
        var source = @"
struct Logger {
    prefix: *u8,
}

impl Logger {
    pub fn log(self, format: *u8, ...args) -> i32 {
        return 0
    }
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        var log = module.Functions.FirstOrDefault(f => f.Name == "Logger::log");
        Assert.NotNull(log);
        Assert.True(log.IsVariadic);
        Assert.Equal(3, log.Parameters.Count); // self, format, ...args
        Assert.Equal("self", log.Parameters[0].Name);
        Assert.Equal("format", log.Parameters[1].Name);
        Assert.Equal("args", log.Parameters[2].Name);
        Assert.True(log.Parameters[2].IsVariadic);
    }
}
