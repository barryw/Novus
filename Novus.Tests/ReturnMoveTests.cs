using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for return move tracking (Phase 3a)
/// Ensures that return x properly marks x as moved for non-Copy types
/// </summary>
public class ReturnMoveTests
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
    public void TestReturnMovesValue()
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

            fn get_string() -> MyString {
                let s = MyString::new()
                return s         // s is moved
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestUseAfterReturn()
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

            fn test() -> MyString {
                let s = MyString::new()
                return s         // s is moved
                s.len()          // ERROR: use after move (also unreachable)
            }
        ";

        AssertHasError(code, "E0382", "value moved by return statement");
    }

    [Fact]
    public void TestReturnCopyType()
    {
        var code = @"
            fn test() -> i32 {
                let x: i32 = 42
                return x         // Copy, not move
                x + 1            // OK: x still valid (though unreachable)
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestReturnPointerCopyType()
    {
        var code = @"
            fn test() -> *u8 {
                let p: *u8 = (*u8)0
                return p         // Copy, not move
                let q = p        // OK: p still valid (though unreachable)
            }
        ";

        AssertCompiles(code);
    }
}
