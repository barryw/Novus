using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class StringTests
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
    public void BuildIr_StringLiteral_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let s = ""Hello, World!""
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringType_Explicit_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let s: String = ""test""
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringAsParameter_Compiles()
    {
        var source = @"
fn process(s: String) -> i32 {
    return 42
}
pub fn main() -> i32 {
    return process(""hello"")
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringAsReturnType_Compiles()
    {
        var source = @"
fn getMessage() -> String {
    return ""Hello""
}
pub fn main() -> i32 {
    let msg = getMessage()
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringEscapeSequence_Newline_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let s = ""Line 1\nLine 2""
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringEscapeSequence_Tab_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let s = ""Column1\tColumn2""
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringEscapeSequence_Quote_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let s = ""He said \""Hello\""""
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringEscapeSequence_Backslash_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let s = ""Path\\To\\File""
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringInStruct_Compiles()
    {
        var source = @"
struct Person {
    name: String,
    age: i32
}
pub fn main() -> i32 {
    let p = Person {
        name: ""Alice"",
        age: 30
    }
    return p.age
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringEmpty_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let s = """"
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_MultipleStrings_Compiles()
    {
        var source = @"
pub fn main() -> i32 {
    let s1 = ""First""
    let s2 = ""Second""
    let s3 = ""Third""
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringInEnum_Compiles()
    {
        var source = @"
enum Message {
    Text(String),
    Number(i32)
}
pub fn main() -> i32 {
    let m = Message::Text(""Hello"")
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringInMatch_Compiles()
    {
        var source = @"
enum Message {
    Text(String),
    Value(i32)
}
pub fn main() -> i32 {
    let m = Message::Text(""test"")
    match m {
        Message::Text(s) => return 1
        Message::Value(v) => return v
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
