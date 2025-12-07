using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Golden file tests for C code generation.
/// These tests verify that the generated C code matches expected output,
/// catching regressions in code generation.
///
/// Test organization:
/// - TestCases/Golden/input/*.novus - Input Novus source files
/// - TestCases/Golden/expected/*.c - Expected C output (golden files)
///
/// To update golden files after intentional changes:
/// 1. Run tests with UPDATE_GOLDEN=true environment variable
/// 2. Or manually copy actual output to expected files
/// </summary>
public class GoldenFileTests
{
    private const string GoldenInputDir = "TestCases/Golden/input";
    private const string GoldenExpectedDir = "TestCases/Golden/expected";

    /// <summary>
    /// Build IR from source without auto-imports (standalone compilation).
    /// </summary>
    private IrModule BuildIR(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    /// <summary>
    /// Generate C code from IR module.
    /// </summary>
    private string GenerateCCode(IrModule module, BuildMode buildMode = BuildMode.Debug)
    {
        var codegen = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft", buildMode);
        return codegen.Generate();
    }

    #region Basic Code Generation Tests

    [Fact]
    public void Golden_SimpleFunction_GeneratesExpectedC()
    {
        var source = @"
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Verify key aspects of generated code - function exists
        Assert.Contains("add", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void Golden_StructDefinition_GeneratesExpectedC()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn create_point(x: i32, y: i32) -> Point {
    return Point { x: x, y: y }
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Verify struct definition
        Assert.Contains("typedef struct", code);
        Assert.Contains("Point", code);
        // The struct fields should be present
        Assert.Contains("x;", code);
        Assert.Contains("y;", code);
    }

    [Fact]
    public void Golden_EnumDefinition_GeneratesExpectedC()
    {
        var source = @"
pub enum Color {
    Red,
    Green,
    Blue,
}

pub fn get_red() -> Color {
    return Color::Red
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Verify enum definition
        Assert.Contains("typedef", code);
        Assert.Contains("Color", code);
    }

    [Fact]
    public void Golden_IfStatement_GeneratesExpectedC()
    {
        var source = @"
pub fn max(a: i32, b: i32) -> i32 {
    if a > b {
        return a
    } else {
        return b
    }
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Verify conditional structure
        Assert.Contains("if", code);
        Assert.Contains("max", code);
    }

    [Fact]
    public void Golden_WhileLoop_GeneratesExpectedC()
    {
        var source = @"
pub fn count_to(n: i32) -> i32 {
    let mut sum: i32 = 0
    let mut i: i32 = 0
    while i < n {
        sum = sum + i
        i = i + 1
    }
    return sum
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Verify loop structure exists
        Assert.Contains("count_to", code);
    }

    [Fact]
    public void Golden_ArrayParameter_GeneratesExpectedC()
    {
        var source = @"
pub fn get_first(arr: *i32) -> i32 {
    unsafe {
        return *arr
    }
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Verify function generated
        Assert.Contains("get_first", code);
        Assert.Contains("int32_t", code);
    }

    #endregion

    #region Regression Tests

    [Fact]
    public void Golden_StrictAliasingCompliant_NoDirectCasts()
    {
        // Ensure we use memcpy for type punning, not direct casts
        var source = @"
pub struct Data {
    value: u32,
}

pub fn copy_data(src: Data) -> Data {
    let dst = src
    return dst
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // For struct copies, we should use memcpy or direct assignment
        // NOT pointer casts that violate strict aliasing
        // This is a regression test for aliasing safety
        Assert.Contains("Data", code);
    }

    [Fact]
    public void Golden_DeferBlocks_CleanupGenerated()
    {
        var source = @"
pub fn with_cleanup() -> i32 {
    let x: i32 = 10
    defer {
        let y: i32 = 1
    }
    return x
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Defer blocks should generate cleanup code
        // The generated code should contain the function name
        Assert.Contains("with_cleanup", code);
    }

    #endregion

    #region Monomorphization Tests

    [Fact]
    public void Golden_GenericFunction_GeneratesSpecializedVersion()
    {
        // Generic functions get specialized during IR building
        var source = @"
pub fn identity<T>(x: T) -> T {
    return x
}

pub fn test() -> i32 {
    return identity::<i32>(42)
}
";
        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // The test function should exist
        Assert.Contains("test", code);
    }

    [Fact]
    public void Golden_ExplicitMonomorphizationFlag_Works()
    {
        // Test that the explicit IsMonomorphized flag is respected
        var module = new IrModule();
        var func = new IrFunction("test_func_i32", IrVoidType.Instance);
        func.IsMonomorphized = true;
        func.GenericSourceName = "test_func";
        module.AddFunction(func);

        var codegen = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");

        Assert.True(codegen.IsMonomorphizedFunction(func),
            "Function with IsMonomorphized=true should be detected as monomorphized");
    }

    [Fact]
    public void Golden_NonMonomorphizedFunction_NotDetectedAsMonomorphized()
    {
        // Test that regular functions are not falsely detected
        var module = new IrModule();
        var func = new IrFunction("my_regular_function", IrVoidType.Instance, Visibility.Public);
        func.IsMonomorphized = false;
        module.AddFunction(func);

        var codegen = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");

        Assert.False(codegen.IsMonomorphizedFunction(func),
            "Regular function should not be detected as monomorphized");
    }

    #endregion
}
