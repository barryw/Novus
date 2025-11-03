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
}
