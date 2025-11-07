using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for assignment move tracking (Phase 3a)
/// Ensures that let y = x properly marks x as moved for non-Copy types
/// </summary>
public class AssignmentMoveTests
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

    private void AssertHasError(string source, string errorCode, string? messageFragment = null)
    {
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors, "Expected errors but found none");
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == errorCode);
        Assert.NotNull(error);

        if (messageFragment != null)
        {
            var message = error.Message;
            var helpText = string.Join(" ", error.HelpTexts ?? new List<string>());
            var fullText = message + " " + helpText;
            Assert.Contains(messageFragment, fullText);
        }
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
    public void TestAssignmentMovesValue()
    {
        var code = @"
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
            }
        ";

        AssertHasError(code, "E0382", "value moved by assignment");
    }

    [Fact]
    public void TestAssignmentMoveInConditional()
    {
        var code = @"
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
                if true {
                    let y = x    // x is moved in this branch
                }
                x.len()          // ERROR: may be moved
            }
        ";

        AssertHasError(code, "E0382");
    }

    [Fact]
    public void TestCopyTypesNotMoved()
    {
        var code = @"
            fn test() {
                let x: i32 = 42
                let y = x        // Copy, not move
                let z = x + y    // OK: both valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestBoolCopyType()
    {
        var code = @"
            fn test() {
                let a: bool = true
                let b = a        // Copy
                let c = a && b   // OK: both valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestPointerCopyType()
    {
        var code = @"
            fn test() {
                let p: *u8 = (*u8)0
                let q = p        // Copy (pointer value)
                let r = p        // OK: both valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestAssignmentFromFunctionCallNotTracked()
    {
        var code = @"
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
            }

            fn test() {
                let x = MyString::new()
                let y = MyString::new()  // Function call, not move
                let z = x.data           // OK: x not moved
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestAssignmentToThrowaway()
    {
        var code = @"
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
                let _ = x        // x is moved to throwaway
                x.len()          // ERROR: use after move
            }
        ";

        AssertHasError(code, "E0382", "value moved by assignment");
    }

    [Fact]
    public void TestChainedAssignments()
    {
        var code = @"
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
                let a = MyString::new()
                let b = a        // a moved to b
                let c = b        // b moved to c
                b.len()          // ERROR: b was moved
            }
        ";

        AssertHasError(code, "E0382");
    }
}
