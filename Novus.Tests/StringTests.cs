using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

[Trait("Category", "CompilerIntegration")]
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
from std::string::core import Str
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
from std::string::core import Str
pub fn main() -> i32 {
    let s: Str = ""test""
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringAsParameter_Compiles()
    {
        var source = @"
from std::string::core import Str
fn process(s: Str) -> i32 {
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
from std::string::core import Str
fn getMessage() -> Str {
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
from std::string::core import Str
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
from std::string::core import Str
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
from std::string::core import Str
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
from std::string::core import Str
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
from std::string::core import Str
struct Person {
    name: Str,
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
from std::string::core import Str
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
from std::string::core import Str
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
from std::string::core import Str
enum Message {
    Text(Str),
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
from std::string::core import Str
enum Message {
    Text(Str),
    Value(i32)
}
pub fn main() -> i32 {
    let m = Message::Text(""test"")
    match m {
        Message::Text(s) => return 1,
        Message::Value(v) => return v,
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StrToU8PtrCoercion_Compiles()
    {
        var source = @"
from std::string::core import Str
fn takes_pointer(ptr: *u8) -> i32 {
    return 42
}
pub fn main() -> i32 {
    return takes_pointer(""Hello"")
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringToU8PtrCoercion_Compiles()
    {
        var source = @"
from std::collections::vec import Vec

// Minimal String type definition for testing
pub struct String {
    vec: Vec<u8>
}

impl String {
    pub fn as_ptr(&self) -> *u8 {
        return self.vec.as_ptr()
    }
}

fn takes_pointer(ptr: *u8) -> i32 {
    return 42
}

fn make_string() -> String {
    let v = Vec::<u8>::new()
    return String { vec: v }
}

pub fn main() -> i32 {
    let s = make_string()
    return takes_pointer(s)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_StringAndStrToU8PtrCoercion_Compiles()
    {
        var source = @"
from std::string::core import Str
from std::collections::vec import Vec

// Minimal String type definition for testing
pub struct String {
    vec: Vec<u8>
}

impl String {
    pub fn as_ptr(&self) -> *u8 {
        return self.vec.as_ptr()
    }
}

fn takes_pointer(ptr: *u8) -> i32 {
    return 1
}

fn make_string() -> String {
    let v = Vec::<u8>::new()
    return String { vec: v }
}

pub fn main() -> i32 {
    let result1 = takes_pointer(""Str literal"")
    let s = make_string()
    let result2 = takes_pointer(s)
    return result1 + result2
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}
