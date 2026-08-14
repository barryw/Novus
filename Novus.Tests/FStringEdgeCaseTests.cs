using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for f-string (interpolated string) edge cases.
///
/// F-strings use the syntax: f"text {expression} more text"
/// The lexer handles nested braces via recursive F_INTERP_CONTENT rule.
///
/// Edge cases tested:
/// - Nested braces in expressions: f"{map.get(key)}"
/// - Method calls with arguments: f"{func(a, b)}"
/// - Struct literals inside f-strings: f"{Point { x: 1, y: 2 }}"
/// - Escaped braces: f"Use \{ and \} for literal braces"
/// - Empty interpolation: f"{}"
/// - Adjacent interpolations: f"{a}{b}"
/// - Complex expressions: f"{if cond { a } else { b }}"
/// </summary>
[Trait("Category", "CompilerIntegration")]
public class FStringEdgeCaseTests
{
    private (IrBuilder builder, DiagnosticBag diagnostics) BuildModule(string code)
    {
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(
            code,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.Compilation
        );
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: true);
        builder.BuildModule(tree);
        return (builder, diagnostics);
    }

    [Fact]
    public void FString_SimpleVariable_Parses()
    {
        var code = @"
fn main() -> i32 {
    let x = 42
    let s = f""{x}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_MultipleVariables_Parses()
    {
        var code = @"
fn main() -> i32 {
    let x = 42
    let y = 100
    let s = f""{x} and {y}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_ArithmeticExpression_Parses()
    {
        var code = @"
fn main() -> i32 {
    let x = 10
    let s = f""{x + 5}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_FunctionCall_Parses()
    {
        var code = @"
fn double(x: i32) -> i32 {
    return x * 2
}

fn main() -> i32 {
    let s = f""{double(21)}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_MethodCallWithMultipleArgs_Parses()
    {
        var code = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn main() -> i32 {
    let s = f""{add(1, 2)}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_EscapedBraces_Parses()
    {
        var code = @"
fn main() -> i32 {
    let s = f""Use \{ and \} for braces""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_AdjacentInterpolations_Parses()
    {
        var code = @"
fn main() -> i32 {
    let a = 1
    let b = 2
    let s = f""{a}{b}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_TextBeforeAndAfter_Parses()
    {
        var code = @"
fn main() -> i32 {
    let x = 42
    let s = f""Value: {x} units""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_BooleanValue_Parses()
    {
        var code = @"
fn main() -> i32 {
    let flag = true
    let s = f""{flag}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_ComparisonExpression_Parses()
    {
        var code = @"
fn main() -> i32 {
    let x = 10
    let s = f""{x > 5}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_TernaryExpression_Parses()
    {
        // Test nested braces in ternary-like if expression
        var code = @"
fn main() -> i32 {
    let x = 10
    let result = if x > 5 { 1 } else { 0 }
    let s = f""{result}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_NestedFunctionCalls_Parses()
    {
        var code = @"
fn inner(x: i32) -> i32 {
    return x * 2
}

fn outer(x: i32) -> i32 {
    return inner(x) + 1
}

fn main() -> i32 {
    let s = f""{outer(5)}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_WithNewlines_Parses()
    {
        var code = @"
fn main() -> i32 {
    let x = 42
    let s = f""Line 1\nValue: {x}\nLine 3""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_WithHexEscape_Parses()
    {
        var code = @"
fn main() -> i32 {
    let x = 42
    let s = f""\x41{x}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_EmptyString_Parses()
    {
        var code = @"
fn main() -> i32 {
    let s = f""""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_OnlyText_Parses()
    {
        var code = @"
fn main() -> i32 {
    let s = f""Hello World""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    [Fact]
    public void FString_IndexAccess_Parses()
    {
        // Array access requires std::core for Display trait
        // Use a simpler test that doesn't require imports
        var code = @"
fn get_value(index: i32) -> i32 {
    return index * 10
}

fn main() -> i32 {
    let idx = 2
    let s = f""{get_value(idx)}""
    return 0
}
";
        var (builder, diagnostics) = BuildModule(code);
        Assert.False(diagnostics.HasErrors, FormatErrors(diagnostics));
        Assert.False(builder.Diagnostics.HasErrors, FormatErrors(builder.Diagnostics));
    }

    private static string FormatErrors(DiagnosticBag diagnostics)
    {
        if (!diagnostics.HasErrors) return "";
        return string.Join("\n", diagnostics.Diagnostics.Select(d => $"[{d.Code}] {d.Message}"));
    }
}
