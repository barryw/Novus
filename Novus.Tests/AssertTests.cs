using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class AssertTests
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

    private string GenerateCCode(IrModule module, BuildMode buildMode)
    {
        var codegen = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft", buildMode);
        return codegen.Generate();
    }

    [Fact]
    public void BuildIr_Assert_SimpleCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    assert!(x == 10)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_WithMessage_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    assert!(x > 5, ""x should be greater than 5"")
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_BooleanLiteral_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    assert!(true)
    assert!(1)
    assert!(42)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_ComplexCondition_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    let y = 20
    assert!(x < y && y > 15)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_MultipleAsserts_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    assert!(x == 10, ""first assert"")
    assert!(x > 0, ""second assert"")
    assert!(x < 100, ""third assert"")
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_WithDeferBlock_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var cleanup = 0
    defer cleanup = 1

    let x = 10
    assert!(x == 10)

    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_InIfBlock_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    if x > 5 {
        assert!(x < 100, ""x should be in range"")
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_InLoop_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var i = 0
    while i < 10 {
        assert!(i >= 0, ""counter should be non-negative"")
        i = i + 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_NumericExpression_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    assert!(x + 5)
    assert!(x * 2)
    assert!(x - 1)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Assert_FunctionParameter_Compiles()
    {
        var source = @"
pub fn check_value(x: i32) -> i32 {
    assert!(x > 0, ""value must be positive"")
    return x * 2
}

pub fn main() -> i32 {
    return check_value(10)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var checkFunction = module.Functions.FirstOrDefault(f => f.Name == "check_value");
        Assert.NotNull(checkFunction);
    }

    [Fact]
    public void BuildIr_Assert_ComparisonOperators_Compile()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    assert!(x == 10)
    assert!(x != 5)
    assert!(x > 5)
    assert!(x >= 10)
    assert!(x < 20)
    assert!(x <= 10)
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void CodeGen_Assert_InDebugMode_GeneratesAssertCode()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    assert!(x == 10, ""x should be 10"")
    return 0
}";
        var module = BuildIr(source);
        var cCode = GenerateCCode(module, BuildMode.Debug);

        // In debug mode, assert code should be present
        Assert.Contains("__novus_assert_failed", cCode);
        Assert.Contains("x should be 10", cCode);
    }

    [Fact]
    public void CodeGen_Assert_InReleaseMode_ElidesAssertCode()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    assert!(x == 10, ""x should be 10"")
    return 0
}";
        var module = BuildIr(source);
        var cCode = GenerateCCode(module, BuildMode.Release);

        // In release mode, assert code should be completely elided
        Assert.DoesNotContain("__novus_assert_failed", cCode);
        Assert.DoesNotContain("x should be 10", cCode);
    }

    [Fact]
    public void BuildIr_Panic_SimpleMessage_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    panic!(""Something went wrong"")
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Panic_InIfBlock_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let x = 10
    if x < 0 {
        panic!(""x cannot be negative"")
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Panic_WithDefer_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    var cleanup = 0
    defer cleanup = 1

    let file_found = 0
    if file_found == 0 {
        panic!(""File not found"")
    }

    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mainFunction = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(mainFunction);
    }

    [Fact]
    public void BuildIr_Panic_InFunction_Compiles()
    {
        var source = @"
pub fn divide(a: i32, b: i32) -> i32 {
    if b == 0 {
        panic!(""Division by zero"")
    }
    return a / b
}

pub fn main() -> i32 {
    return divide(10, 2)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var divideFunction = module.Functions.FirstOrDefault(f => f.Name == "divide");
        Assert.NotNull(divideFunction);
    }

    [Fact]
    public void CodeGen_Panic_InDebugMode_GeneratesPanicCode()
    {
        var source = @"
pub fn main() -> i32 {
    panic!(""Unrecoverable error"")
    return 0
}";
        var module = BuildIr(source);
        var cCode = GenerateCCode(module, BuildMode.Debug);

        // Panic code should be present in debug mode
        Assert.Contains("__novus_panic", cCode);
        Assert.Contains("Unrecoverable error", cCode);
    }

    [Fact]
    public void CodeGen_Panic_InReleaseMode_KeepsPanicCode()
    {
        var source = @"
pub fn main() -> i32 {
    panic!(""Unrecoverable error"")
    return 0
}";
        var module = BuildIr(source);
        var cCode = GenerateCCode(module, BuildMode.Release);

        // Unlike assert, panic is NEVER elided (even in release mode)
        Assert.Contains("__novus_panic", cCode);
        Assert.Contains("Unrecoverable error", cCode);
    }

    [Fact]
    public void CodeGen_Panic_VersusAssert_BehaviorDifference()
    {
        var sourceWithPanic = @"
pub fn main() -> i32 {
    panic!(""error"")
    return 0
}";
        var sourceWithAssert = @"
pub fn main() -> i32 {
    assert!(false, ""error"")
    return 0
}";

        var panicModule = BuildIr(sourceWithPanic);
        var assertModule = BuildIr(sourceWithAssert);

        // In release mode: panic stays, assert disappears
        var panicCodeRelease = GenerateCCode(panicModule, BuildMode.Release);
        var assertCodeRelease = GenerateCCode(assertModule, BuildMode.Release);

        Assert.Contains("__novus_panic", panicCodeRelease);
        Assert.DoesNotContain("__novus_assert_failed", assertCodeRelease);
    }
}
