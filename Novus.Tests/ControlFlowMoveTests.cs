using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for control flow sensitivity in move tracking (Phase 2)
/// Ensures that moves are properly tracked through if/else, match, and while constructs
/// </summary>
public class ControlFlowMoveTests
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
            var helpText = string.Join(" ", error.HelpTexts ?? new List<string>());
            Assert.Contains(messageFragment, helpText);
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
    public void TestIfBranchMove()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: MyString) {}

            fn test() {
                let x = MyString::new()
                if true {
                    consume(x)
                }
                let _ = x.get_length()
            }";

        AssertHasError(code, "E0382", "conditional branch");
    }

    [Fact]
    public void TestIfElseBothBranches()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: MyString) {}

            fn test() {
                let x = MyString::new()
                if true {
                    consume(x)
                } else {
                    consume(x)
                }
                let _ = x.get_length()
            }";

        AssertHasError(code, "E0382");
    }

    [Fact]
    public void TestIfElseOnlyThenBranchMoves()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: MyString) {}

            fn test() {
                let x = MyString::new()
                if true {
                    consume(x)
                } else {
                    // x not moved here
                }
                let _ = x.get_length()
            }";

        // Should still error - moved in one branch
        AssertHasError(code, "E0382", "conditional branch");
    }

    [Fact]
    public void TestMatchArmMove()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: String) {}

            enum Option<T> {
                Some(T),
                None
            }

            fn test(opt: Option<i32>) {
                let x = MyString::new()
                match opt {
                    Option::Some(v) => {
                        consume(x)
                    },
                    Option::None => {}
                }
                let _ = x.get_length()
            }";

        AssertHasError(code, "E0382", "conditional branch");
    }

    [Fact]
    public void TestMatchAllArmsMoveInOne()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: String) {}

            enum Option<T> {
                Some(T),
                None
            }

            fn test(opt: Option<i32>) {
                let x = MyString::new()
                match opt {
                    Option::Some(_) => {},
                    Option::None => {
                        consume(x)
                    }
                }
                let _ = x.get_length()
            }";

        AssertHasError(code, "E0382", "conditional branch");
    }

    [Fact]
    public void TestWhileLoopMove()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: MyString) {}

            fn test() {
                let x = MyString::new()
                while true {
                    consume(x)
                    break
                }
                let _ = x.get_length()
            }";

        AssertHasError(code, "E0382", "loop body");
    }

    [Fact]
    public void TestValidSequentialUse()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn test() {
                let x = MyString::new()
                if true {
                    let _ = x.get_length()
                }
                let _ = x.get_length()
            }";

        AssertCompiles(code);
    }

    [Fact]
    public void TestNestedIfBranches()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: MyString) {}

            fn test() {
                let x = MyString::new()
                if true {
                    if false {
                        consume(x)
                    }
                }
                let _ = x.get_length()
            }";

        AssertHasError(code, "E0382", "conditional branch");
    }

    [Fact]
    public void TestMoveInElseBranch()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: MyString) {}

            fn test() {
                let x = MyString::new()
                if false {
                    // nothing
                } else {
                    consume(x)
                }
                let _ = x.get_length()
            }";

        AssertHasError(code, "E0382", "conditional branch");
    }

    [Fact]
    public void TestNoMoveInBranches()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn test() {
                let x = MyString::new()
                if true {
                    let _ = x.get_length()
                } else {
                    let _ = x.get_length()
                }
                let _ = x.get_length()
            }";

        AssertCompiles(code);
    }

    [Fact]
    public void TestMatchWithIntegerPattern()
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

                fn get_length(self) -> u64 {
                    return self.length
                }
            }

            fn consume(consuming s: String) {}

            fn test(n: i32) {
                let x = MyString::new()
                match n {
                    0 => {
                        consume(x)
                    },
                    _ => {}
                }
                let _ = x.get_length()
            }";

        AssertHasError(code, "E0382", "conditional branch");
    }
}
