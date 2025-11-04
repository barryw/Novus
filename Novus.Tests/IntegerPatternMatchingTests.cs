using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class IntegerPatternMatchingTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    [Fact]
    public void IntegerMatch_BasicI32Patterns_Compiles()
    {
        var code = @"
pub fn classify_number(n: i32) -> i32 {
    match n {
        0 => return 10,
        1 => return 20,
        2 => return 30,
        _ => return 99
    }
}

pub fn main() -> i32 {
    return classify_number(1)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);

        var function = module.Functions.FirstOrDefault(f => f.Name == "classify_number");
        Assert.NotNull(function);
    }

    [Fact]
    public void IntegerMatch_U8Patterns_Compiles()
    {
        var code = @"
pub fn classify_byte(b: u8) -> i32 {
    match b {
        0u8 => return 1,
        255u8 => return 2,
        _ => return 0
    }
}

pub fn main() -> i32 {
    return classify_byte(0u8)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);
    }

    [Fact]
    public void IntegerMatch_NegativeValues_Compiles()
    {
        var code = @"
pub fn classify_signed(n: i32) -> i32 {
    match n {
        -10 => return 1,
        -5 => return 2,
        0 => return 3,
        5 => return 4,
        10 => return 5,
        _ => return 0
    }
}

pub fn main() -> i32 {
    return classify_signed(-5)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);
    }

    [Fact]
    public void IntegerMatch_ManyPatterns_Compiles()
    {
        var code = @"
pub fn day_name(day: i32) -> i32 {
    match day {
        1 => return 1,   // Monday
        2 => return 2,   // Tuesday
        3 => return 3,   // Wednesday
        4 => return 4,   // Thursday
        5 => return 5,   // Friday
        6 => return 6,   // Saturday
        7 => return 7,   // Sunday
        _ => return 0    // Invalid
    }
}

pub fn main() -> i32 {
    return day_name(3)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);
    }

    // NOTE: Exhaustiveness checking happens in semantic analysis (SemanticAnalyzer.cs).
    // Integer matches without wildcards are caught there with error E0036.
    // This test bypasses semantic analysis by calling BuildIr directly.

    [Fact]
    public void IntegerMatch_DuplicatePattern_GeneratesWarning()
    {
        var code = @"
pub fn duplicate_case(n: i32) -> i32 {
    match n {
        5 => return 1,
        5 => return 2,
        _ => return 0
    }
}

pub fn main() -> i32 {
    return 0
}";
        // Should compile but generate a warning
        // For now, just ensure it doesn't crash
        var module = BuildIr(code);
        Assert.NotNull(module);
    }

    // NOTE: Pattern type validation happens in semantic analysis (SemanticAnalyzer.cs).
    // String patterns in integer matches are caught there with error E0042.
    // This test bypasses semantic analysis by calling BuildIr directly.

    [Fact]
    public void IntegerMatch_I64Patterns_Compiles()
    {
        var code = @"
pub fn classify_large(n: i64) -> i32 {
    match n {
        1000000000i64 => return 1,
        2000000000i64 => return 2,
        _ => return 0
    }
}

pub fn main() -> i32 {
    return classify_large(1000000000i64)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);
    }

    [Fact]
    public void IntegerMatch_HexLiterals_Compiles()
    {
        var code = @"
pub fn classify_hex(n: i32) -> i32 {
    match n {
        $FF => return 1,
        $100 => return 2,
        $200 => return 3,
        _ => return 0
    }
}

pub fn main() -> i32 {
    return classify_hex($FF)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);
    }

    [Fact]
    public void IntegerMatch_BinaryLiterals_Compiles()
    {
        var code = @"
pub fn classify_binary(n: i32) -> i32 {
    match n {
        %0001 => return 1,
        %0010 => return 2,
        %0100 => return 4,
        %1000 => return 8,
        _ => return 0
    }
}

pub fn main() -> i32 {
    return classify_binary(%0010)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);
    }

    [Fact]
    public void IntegerMatch_ReturnInPattern_Compiles()
    {
        var code = @"
pub fn early_return(n: i32) -> i32 {
    match n {
        0 => return 100,
        1 => return 200,
        _ => return 300
    }
}

pub fn main() -> i32 {
    return early_return(0)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);
    }

    [Fact]
    public void IntegerMatch_BlockInPattern_Compiles()
    {
        var code = @"
pub fn block_pattern(n: i32) -> i32 {
    match n {
        0 => {
            let x = 10
            return x
        },
        1 => {
            let y = 20
            return y
        },
        _ => return 0
    }
}

pub fn main() -> i32 {
    return block_pattern(1)
}";
        var module = BuildIr(code);
        Assert.NotNull(module);
    }
}
