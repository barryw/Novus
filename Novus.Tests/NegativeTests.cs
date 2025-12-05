using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Comprehensive negative test suite verifying that the compiler
/// correctly rejects invalid programs with appropriate error messages.
///
/// These tests complement the positive tests by ensuring:
/// 1. Type safety is enforced
/// 2. Ownership/move rules are checked
/// 3. Invalid syntax is rejected
/// 4. Semantic errors produce helpful messages
/// </summary>
public class NegativeTests
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

    private void AssertError(DiagnosticBag diagnostics, string errorCode, string? messageContains = null)
    {
        Assert.True(diagnostics.HasErrors, "Expected errors but none were reported");
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == errorCode);
        Assert.NotNull(error);
        if (messageContains != null)
        {
            Assert.Contains(messageContains, error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void AssertAnyError(DiagnosticBag diagnostics)
    {
        Assert.True(diagnostics.HasErrors, "Expected errors but none were reported");
    }

    #region Type Mismatch Tests

    [Fact]
    public void TypeMismatch_ReturnWrongTypeExplicit_RejectsWithError()
    {
        // Note: Novus allows implicit coercion of small integer to bool in some cases
        // This test uses a struct to ensure strict type checking
        var source = @"
struct Foo { x: i32 }
fn test() -> bool {
    let f = Foo { x: 1 }
    return f  // Cannot return struct as bool
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Undefined Identifier Tests

    [Fact]
    public void UndefinedVariable_RejectsWithHelpfulError()
    {
        var source = @"
fn test() -> i32 {
    return undefined_var
}";
        var diagnostics = Analyze(source);
        AssertError(diagnostics, "E0007", "undefined");
    }

    [Fact]
    public void UndefinedStructField_RejectsWithError()
    {
        var source = @"
struct Point { x: i32, y: i32 }
fn test() {
    let p = Point { x: 0, y: 0 }
    let z = p.z  // z doesn't exist
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Constant/Overflow Tests

    [Fact]
    public void IntegerOverflow_U8_RejectsWithError()
    {
        var source = @"
fn test() -> u8 {
    return 256u8  // Max u8 is 255
}";
        var diagnostics = Analyze(source);
        AssertError(diagnostics, "E0009");
    }

    [Fact]
    public void IntegerOverflow_I8_RejectsWithError()
    {
        var source = @"
fn test() -> i8 {
    return 128i8  // Max i8 is 127
}";
        var diagnostics = Analyze(source);
        AssertError(diagnostics, "E0009");
    }

    [Fact]
    public void DivisionByZero_RejectsWithError()
    {
        var source = @"
fn test() -> i32 {
    return 42 / 0
}";
        var diagnostics = Analyze(source);
        AssertError(diagnostics, "E0005", "division by zero");
    }

    #endregion

    #region Duplicate Definition Tests

    [Fact]
    public void DuplicateFunction_RejectsWithError()
    {
        var source = @"
fn test() {}
fn test() {}  // Duplicate
";
        var diagnostics = Analyze(source);
        AssertError(diagnostics, "E0001", "multiple times");
    }

    #endregion

    #region Control Flow Tests

    [Fact]
    public void BreakOutsideLoop_RejectsWithError()
    {
        var source = @"
fn test() {
    break  // Not inside a loop
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Unsafe Block Tests

    [Fact]
    public void PointerDereferenceOutsideUnsafe_RejectsWithError()
    {
        var source = @"
fn test() {
    let ptr: *i32 = (*i32)(0x1000)
    let val = *ptr  // Dereference outside unsafe
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Generic Tests

    [Fact]
    public void GenericTypeArgumentMismatch_RejectsWithError()
    {
        var source = @"
struct Container<T> { value: T }
fn test() {
    let c: Container<i32> = Container { value: true }  // bool != i32
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void InvalidBinaryOperator_RejectsWithError()
    {
        var source = @"
fn test() -> bool {
    return true + false  // Cannot add bools
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    [Fact]
    public void InvalidUnaryOperator_RejectsWithError()
    {
        var source = @"
struct Point { x: i32 }
fn test() {
    let p = Point { x: 1 }
    let neg = -p  // Cannot negate struct
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Assignment Tests

    [Fact]
    public void AssignToImmutable_RejectsWithError()
    {
        var source = @"
fn test() {
    let x = 42
    x = 100  // x is immutable
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    [Fact]
    public void AssignToConstant_RejectsWithError()
    {
        var source = @"
const VALUE: i32 = 42
fn test() {
    VALUE = 100  // Cannot assign to constant
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Match Expression Tests

    [Fact]
    public void NonExhaustiveMatch_RejectsWithError()
    {
        var source = @"
enum Color { Red, Green, Blue }
fn test(c: Color) -> i32 {
    match c {
        Color::Red => 1,
        Color::Green => 2
        // Missing Blue arm
    }
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Function Signature Tests

    [Fact]
    public void MissingReturn_RejectsWithError()
    {
        var source = @"
fn test() -> i32 {
    let x = 42
    // Missing return statement
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    [Fact]
    public void WrongArgumentCount_RejectsWithError()
    {
        var source = @"
fn add(a: i32, b: i32) -> i32 { return a + b }
fn test() {
    add(1)  // Missing second argument
}";
        var diagnostics = Analyze(source);
        AssertAnyError(diagnostics);
    }

    #endregion

    #region Use After Move Tests

    [Fact]
    public void UseAfterMove_AssignmentMove_RejectsWithError()
    {
        // This test pattern matches existing move tests that work
        var source = @"
struct MyString {
    data: *u8,
    length: u64
}

impl MyString {
    fn new() -> MyString {
        unsafe {
            return MyString { data: (*u8)0, length: 0 }
        }
    }

    fn len(self) -> u64 {
        return self.length
    }
}

fn test() {
    let x = MyString::new()
    let y = x        // x is moved
    x.len()          // ERROR: use after move
}";
        var diagnostics = Analyze(source);
        AssertError(diagnostics, "E0382", "moved");
    }

    #endregion
}
