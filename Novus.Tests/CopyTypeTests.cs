using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for Copy type semantics (Phase 3a)
/// Ensures that primitive types can be copied instead of moved
/// </summary>
public class CopyTypeTests
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

    private void AssertHasError(string source, string errorCode)
    {
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors, "Expected errors but found none");
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == errorCode);
        Assert.NotNull(error);
    }

    private void AssertCompiles(string source)
    {
        var diagnostics = Analyze(source);
        if (diagnostics.HasErrors)
        {
            var errors = string.Join("\n", diagnostics.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
            Assert.Fail($"Expected no errors but found:\n{errors}");
        }
    }

    [Fact]
    public void TestI32IsCopyType()
    {
        var code = @"
            fn test() {
                let x: i32 = 42
                let y = x
                let z = x + y    // OK: both valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestU32IsCopyType()
    {
        var code = @"
            fn test() {
                let x: u32 = 42
                let y = x
                let z = x + y    // OK: both valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestBoolIsCopyType()
    {
        var code = @"
            fn test() {
                let a = true
                let b = a
                let c = a && b   // OK: both valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestPointerIsCopyType()
    {
        var code = @"
            fn test() {
                let p: *u8 = (*u8)0
                let q = p        // Copy pointer value
                let r = p        // OK: both valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestStructIsNotCopyType()
    {
        var code = @"
            struct Point { x: i32, y: i32 }

            fn test() {
                let p = Point { x: 10, y: 20 }
                let q = p        // Move (even though fields are Copy)
                let z = p.x      // ERROR: use after move
            }
        ";

        AssertHasError(code, "E0382");
    }

    [Fact]
    public void TestCopyTypeInLoop()
    {
        var code = @"
            fn test() {
                let x: i32 = 0
                var i: i32 = 0
                while i < 10 {
                    let y = x    // Copy each iteration
                    i = i + 1
                }
                let z = x + 1    // OK: x still valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestMixedCopyAndMoveTypes()
    {
        var code = @"
            struct MyString {
                data: *u8,
                length: u64
            }

            impl MyString {
                fn new() -> MyString {
                    unsafe {
                        return MyString { data: 0 as *u8, length: 0 as u64 }
                    }
                }

                fn len(self) -> u64 {
                    return self.length
                }
            }

            fn test() {
                let x: i32 = 42
                let s = MyString::new()
                let y = x        // Copy: OK
                let t = s        // Move: OK
                let z = x + 1    // OK: x still valid
                s.len()          // ERROR: s was moved
            }
        ";

        AssertHasError(code, "E0382");
    }
}
