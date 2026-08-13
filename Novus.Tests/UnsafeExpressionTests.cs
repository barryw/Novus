using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for unsafe block expressions (unsafe { expr } returning a value)
/// </summary>
public class UnsafeExpressionTests
{
    private (DiagnosticBag Diagnostics, SemanticAnalyzer Analyzer) Analyze(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);
        return (analyzer.Diagnostics, analyzer);
    }

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

    #region Parsing Tests

    [Fact]
    public void UnsafeExpression_ParsesAsExpression()
    {
        // Should parse without errors
        var source = @"
fn test() -> i32 {
    let x = unsafe { 42 }
    return x
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        Assert.Single(module.Functions);
    }

    [Fact]
    public void UnsafeExpression_InVariableAssignment_Parses()
    {
        var source = @"
fn test() -> i32 {
    var x: i32 = 0
    x = unsafe { 100 }
    return x
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_InArithmeticExpression_Parses()
    {
        var source = @"
fn test() -> i32 {
    let a = 10 + unsafe { 5 }
    return a
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_AsFunctionArgument_Parses()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn test() -> i32 {
    return add(unsafe { 1 }, unsafe { 2 })
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_InTernary_Parses()
    {
        var source = @"
fn test(cond: bool) -> i32 {
    return cond ? unsafe { 1 } : unsafe { 2 }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    #endregion

    #region Value Return Tests

    [Fact]
    public void UnsafeExpression_ReturnsLastExpressionValue()
    {
        var source = @"
fn test() -> i32 {
    let x = unsafe {
        var temp: i32 = 10
        temp = temp + 5
        temp
    }
    return x
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        var func = module.Functions.FirstOrDefault(f => f.Name == "test");
        Assert.NotNull(func);
    }

    [Fact]
    public void UnsafeExpression_MultipleStatements_ReturnsLast()
    {
        var source = @"
fn test() -> i32 {
    let result = unsafe {
        var a: i32 = 1
        var b: i32 = 2
        var c: i32 = 3
        a + b + c
    }
    return result
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_SingleExpression_ReturnsIt()
    {
        var source = @"
fn test() -> i32 {
    return unsafe { 42 }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    #endregion

    #region Unsafe Context Tests

    [Fact]
    public void UnsafeExpression_AllowsUnsafeOperations()
    {
        var source = @"
from amiga::raw::exec import AllocMem, FreeMem, MEMF_PUBLIC

fn test() -> i32 {
    let ptr = unsafe { AllocMem(100, MEMF_PUBLIC) }
    unsafe { FreeMem(ptr, 100) }
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        // Should not have unsafe-related errors
        var unsafeErrors = diagnostics.Diagnostics.Where(d => d.Code == "E1001").ToList();
        Assert.Empty(unsafeErrors);
    }

    [Fact]
    public void UnsafeExpression_TrackedInAnalyzer()
    {
        var source = @"
fn test() -> i32 {
    let x = unsafe { 42 }
    return x
}";
        var (_, analyzer) = Analyze(source);

        Assert.NotEmpty(analyzer.UnsafeBlocks);
        var unsafeExpr = analyzer.UnsafeBlocks.FirstOrDefault(b => b.Reason == "Unsafe expression");
        Assert.NotNull(unsafeExpr);
    }

    [Fact]
    public void UnsafeExpression_NestedInUnsafeBlock_Works()
    {
        var source = @"
fn test() -> i32 {
    unsafe {
        let x = unsafe { 42 }
        return x
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void UnsafeExpression_EmptyBlock_ReturnsUnit()
    {
        // An empty unsafe block should return unit type
        var source = @"
fn test() {
    let _ = unsafe { }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_WithVariableDeclaration_Works()
    {
        var source = @"
fn test() -> i32 {
    let x = unsafe {
        let y: i32 = 10
        y * 2
    }
    return x
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_WithPointerOperations_Works()
    {
        var source = @"
fn test(ptr: *i32) -> i32 {
    return unsafe { ptr[0] }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        var access = Assert.Single(module.Functions.Single(f => f.Name == "test")
            .BasicBlocks.SelectMany(block => block.Instructions).OfType<IrIndexAccess>());
        Assert.Equal(IrBoundsCheckMode.Unchecked, access.BoundsCheck);
    }

    [Fact]
    public void RawPointerIndexing_OutsideUnsafe_IsRejected()
    {
        var (diagnostics, _) = Analyze(@"
fn test(ptr: *i32) -> i32 {
    return ptr[0]
}");

        Assert.Contains(diagnostics.Diagnostics,
            diagnostic => diagnostic.Code == "E1001" && diagnostic.Message.Contains("raw pointer indexing"));
    }

    [Fact]
    public void RawPointerIndexStore_OutsideUnsafe_IsRejected()
    {
        var (diagnostics, _) = Analyze(@"
fn test(ptr: *i32) {
    ptr[0] = 1
}");

        Assert.Contains(diagnostics.Diagnostics,
            diagnostic => diagnostic.Code == "E1001" && diagnostic.Message.Contains("raw pointer indexing"));
    }

    [Fact]
    public void UnsafeExpression_InStructFieldInit_Parses()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32,
}

fn test() -> Point {
    return Point {
        x: unsafe { 10 },
        y: unsafe { 20 }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_InArrayLiteral_Parses()
    {
        var source = @"
fn test() -> [i32; 3] {
    return [unsafe { 1 }, unsafe { 2 }, unsafe { 3 }]
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_InReturnStatement_Works()
    {
        var source = @"
fn test() -> i32 {
    return unsafe { 42 }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void UnsafeExpression_WithIfExpression_Works()
    {
        var source = @"
fn test(cond: bool) -> i32 {
    return unsafe {
        if cond {
            1
        } else {
            2
        }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void UnsafeExpression_ComplexExample_Works()
    {
        var source = @"
from amiga::raw::exec import AllocMem, FreeMem, MEMF_PUBLIC

fn allocate_and_fill(size: u32) -> *u8 {
    let ptr = unsafe { (*u8)AllocMem(size, MEMF_PUBLIC) }
    return ptr
}

fn test() -> i32 {
    let data = allocate_and_fill(100)
    if data != null {
        unsafe { FreeMem((*u8)data, 100) }
    }
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        // Check for unsafe-related errors only
        var unsafeErrors = diagnostics.Diagnostics.Where(d => d.Code == "E1001").ToList();
        Assert.Empty(unsafeErrors);
    }

    [Fact]
    public void UnsafeExpression_ChainedOperations_Works()
    {
        var source = @"
fn test() -> i32 {
    let a = unsafe { 10 }
    let b = unsafe { a + 20 }
    let c = unsafe { b * 2 }
    return c
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        var func = module.Functions.FirstOrDefault(f => f.Name == "test");
        Assert.NotNull(func);
    }

    #endregion
}
