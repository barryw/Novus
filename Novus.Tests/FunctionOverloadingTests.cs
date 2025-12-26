using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for C#-style function overloading support.
/// Function overloading allows multiple functions with the same name but different parameter types.
/// The compiler resolves which overload to call based on argument types at the call site.
/// </summary>
public class FunctionOverloadingTests
{
    private DiagnosticBag Analyze(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);
        return analyzer.Diagnostics;
    }

    private AnalysisResult? AnalyzeWithResult(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        var success = analyzer.Analyze(tree);
        return success ? analyzer.GetResult() : null;
    }

    #region Basic Overloading Tests

    [Fact]
    public void Overload_DifferentParameterTypes_Allowed()
    {
        // Two functions with same name but different parameter types should be allowed
        var source = @"
fn abs(x: i32) -> i32 {
    if x < 0 { return -x }
    return x
}

fn abs(x: i16) -> i16 {
    if x < 0 { return -x }
    return x
}

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", diagnostics.Diagnostics.Select(d => d.Message))}");
    }

    [Fact]
    public void Overload_DifferentParameterCount_Allowed()
    {
        // Functions with different parameter counts should be allowed
        var source = @"
fn print(x: i32) {}
fn print(x: i32, y: i32) {}
fn print(x: i32, y: i32, z: i32) {}

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", diagnostics.Diagnostics.Select(d => d.Message))}");
    }

    [Fact]
    public void Overload_SameSignature_ReportsError()
    {
        // Duplicate functions with identical signatures should report E0030
        var source = @"
fn test(x: i32) -> i32 {
    return x
}

fn test(x: i32) -> i32 {
    return x * 2
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0030");
        Assert.NotNull(error);
        Assert.Contains("already exists", error.Message);
    }

    [Fact]
    public void Overload_DifferentReturnType_SameParams_ReportsError()
    {
        // Overloading only on return type is not allowed (same param types)
        var source = @"
fn get() -> i32 {
    return 42
}

fn get() -> i64 {
    return 42
}";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0030");
        Assert.NotNull(error);
    }

    #endregion

    #region Overload Detection Tests

    [Fact]
    public void Overload_IsDetected()
    {
        var source = @"
fn process(x: i32) -> i32 { return x }
fn process(x: i64) -> i64 { return x }
fn process(x: u32) -> u32 { return x }
";
        var result = AnalyzeWithResult(source);
        Assert.NotNull(result);
        Assert.True(result.OverloadedFunctionNames.Contains("process"));
        Assert.Equal(3, result.FunctionOverloads["process"].Count);
    }

    [Fact]
    public void NonOverloaded_NotFlagged()
    {
        var source = @"
fn foo(x: i32) -> i32 { return x }
fn bar(x: i32) -> i32 { return x }
fn baz(x: i32) -> i32 { return x }
";
        var result = AnalyzeWithResult(source);
        Assert.NotNull(result);
        Assert.False(result.OverloadedFunctionNames.Contains("foo"));
        Assert.False(result.OverloadedFunctionNames.Contains("bar"));
        Assert.False(result.OverloadedFunctionNames.Contains("baz"));
    }

    #endregion

    #region Parameter Type Variations

    [Fact]
    public void Overload_SignedVsUnsigned_Allowed()
    {
        var source = @"
fn convert(x: i32) -> i32 { return x }
fn convert(x: u32) -> u32 { return x }

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Overload_DifferentBitWidths_Allowed()
    {
        var source = @"
fn to_string(x: i8) {}
fn to_string(x: i16) {}
fn to_string(x: i32) {}
fn to_string(x: i64) {}

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Overload_ReferenceVsValue_Allowed()
    {
        var source = @"
struct Point { x: i32, y: i32 }

fn process(p: Point) {}
fn process(p: &Point) {}

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Overload_MutRefVsRef_Allowed()
    {
        var source = @"
struct Data { value: i32 }

fn update(d: &Data) {}
fn update(d: &var Data) {}

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Overload_PointerVsValue_Allowed()
    {
        var source = @"
fn store(x: i32) {}
fn store(x: *i32) {}

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Complex Parameter Scenarios

    [Fact]
    public void Overload_ArrayTypes_Allowed()
    {
        var source = @"
fn sum(arr: [i32; 3]) -> i32 { return 0 }
fn sum(arr: [i32; 5]) -> i32 { return 0 }

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Overload_TupleTypes_Allowed()
    {
        var source = @"
fn extract(t: (i32, i32)) -> i32 { return 0 }
fn extract(t: (i32, i32, i32)) -> i32 { return 0 }

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Overload_MultipleParams_Mixed()
    {
        var source = @"
fn combine(a: i32, b: i32) -> i32 { return a + b }
fn combine(a: i32, b: i64) -> i64 { return b }
fn combine(a: i64, b: i32) -> i64 { return a }
fn combine(a: i64, b: i64) -> i64 { return a + b }

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Overload_NoParams_Vs_OneParam()
    {
        var source = @"
fn get() -> i32 { return 0 }
fn get(def: i32) -> i32 { return def }

fn main() -> i32 {
    return 0
}";
        var diagnostics = Analyze(source);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Overload_ParameterNamesCanDiffer()
    {
        // Same types but different param names - this is a duplicate signature
        var source = @"
fn test(x: i32) -> i32 { return x }
fn test(y: i32) -> i32 { return y }
";
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E0030");
        Assert.NotNull(error);
    }

    #endregion
}
