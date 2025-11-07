using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for partial move semantics (Phase 3b)
/// Ensures that individual struct fields can be moved independently
/// </summary>
public class PartialMoveTests
{
    // Helper string with common struct definitions
    private const string CommonDefs = @"
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

            fn len(&self) -> u64 {
                return (*self).length
            }
        }

        fn consume_string(consuming s: MyString) {}
    ";

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
    public void TestBasicPartialMove()
    {
        var code = CommonDefs + @"
            struct Pair {
                first: MyString,
                second: MyString
            }

            fn test() {
                let pair = Pair { first: MyString::new(), second: MyString::new() }
                consume_string(pair.first)   // Move first field
                consume_string(pair.second)  // OK: second is still valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestUseMovedField()
    {
        var code = CommonDefs + @"
            struct Pair {
                first: MyString,
                second: MyString
            }

            fn test() {
                let pair = Pair { first: MyString::new(), second: MyString::new() }
                consume_string(pair.first)   // Move first field
                pair.first.len()             // ERROR: first was moved
            }
        ";

        AssertHasError(code, "E0382", "use of moved field");
    }

    [Fact]
    public void TestAccessUnmovedFieldAfterPartialMove()
    {
        var code = CommonDefs + @"
            struct Pair {
                first: MyString,
                second: MyString
            }

            fn test() {
                let pair = Pair { first: MyString::new(), second: MyString::new() }
                consume_string(pair.first)   // Move first field
                pair.second.len()            // OK: second is still valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestMultipleFieldMoves()
    {
        var code = CommonDefs + @"
            struct Triple {
                first: MyString,
                second: MyString,
                third: MyString
            }

            fn test() {
                let t = Triple {
                    first: MyString::new(),
                    second: MyString::new(),
                    third: MyString::new()
                }
                consume_string(t.first)    // Move first
                consume_string(t.second)   // Move second
                t.third.len()              // OK: third is still valid
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestUseFirstMovedFieldAfterMultipleMoves()
    {
        var code = CommonDefs + @"
            struct Triple {
                first: MyString,
                second: MyString,
                third: MyString
            }

            fn test() {
                let t = Triple {
                    first: MyString::new(),
                    second: MyString::new(),
                    third: MyString::new()
                }
                consume_string(t.first)    // Move first
                consume_string(t.second)   // Move second
                t.first.len()              // ERROR: first was moved
            }
        ";

        AssertHasError(code, "E0382", "use of moved field");
    }

    [Fact]
    public void TestUseSecondMovedFieldAfterMultipleMoves()
    {
        var code = CommonDefs + @"
            struct Triple {
                first: MyString,
                second: MyString,
                third: MyString
            }

            fn test() {
                let t = Triple {
                    first: MyString::new(),
                    second: MyString::new(),
                    third: MyString::new()
                }
                consume_string(t.first)    // Move first
                consume_string(t.second)   // Move second
                t.second.len()             // ERROR: second was moved
            }
        ";

        AssertHasError(code, "E0382", "use of moved field");
    }

    [Fact]
    public void TestWholeStructMoveBlocksFieldAccess()
    {
        var code = CommonDefs + @"
            struct Pair {
                first: MyString,
                second: MyString
            }

            fn consume_pair(consuming p: Pair) {}

            fn test() {
                let pair = Pair { first: MyString::new(), second: MyString::new() }
                consume_pair(pair)        // Move whole struct
                pair.first.len()          // ERROR: whole struct was moved
            }
        ";

        AssertHasError(code, "E0382", "value moved");
    }

    [Fact]
    public void TestPartialMoveInConditional()
    {
        var code = CommonDefs + @"
            struct Pair {
                first: MyString,
                second: MyString
            }

            fn test() {
                let pair = Pair { first: MyString::new(), second: MyString::new() }
                if true {
                    consume_string(pair.first)   // Move first in this branch
                }
                pair.first.len()         // ERROR: may be moved
            }
        ";

        AssertHasError(code, "E0382");
    }

    [Fact]
    public void TestFieldMoveErrorMessage()
    {
        var code = CommonDefs + @"
            struct Triple {
                first: MyString,
                second: MyString,
                third: MyString
            }

            fn test() {
                let t = Triple {
                    first: MyString::new(),
                    second: MyString::new(),
                    third: MyString::new()
                }
                consume_string(t.first)    // Move first
                t.first.len()              // ERROR with helpful message
            }
        ";

        // Verify the error message mentions which other fields are still valid
        AssertHasError(code, "E0382", "second");
        var code2 = CommonDefs + @"
            struct Triple {
                first: MyString,
                second: MyString,
                third: MyString
            }

            fn test() {
                let t = Triple {
                    first: MyString::new(),
                    second: MyString::new(),
                    third: MyString::new()
                }
                consume_string(t.first)    // Move first
                t.first.len()              // ERROR with helpful message
            }
        ";
        AssertHasError(code2, "E0382", "third");
    }

    [Fact]
    public void TestCopyFieldsNotAffected()
    {
        var code = CommonDefs + @"
            struct MixedPair {
                value: i32,
                str: MyString
            }

            fn consume_int(consuming x: i32) {}

            fn test() {
                let pair = MixedPair { value: 42, str: MyString::new() }
                consume_int(pair.value)   // Copy, not move (i32 is Copy)
                let x = pair.value        // OK: value is Copy, can use again
                consume_string(pair.str)  // Move str field
                let y = pair.value        // OK: value is still Copy
            }
        ";

        AssertCompiles(code);
    }

    [Fact]
    public void TestNestedStructFieldAccess()
    {
        var code = CommonDefs + @"
            struct Inner {
                value: MyString
            }

            struct Outer {
                inner: Inner,
                other: MyString
            }

            fn test() {
                let outer = Outer {
                    inner: Inner { value: MyString::new() },
                    other: MyString::new()
                }
                consume_string(outer.other)  // Move other field
                outer.other.len()            // ERROR: other was moved
            }
        ";

        AssertHasError(code, "E0382", "use of moved field");
    }
}
