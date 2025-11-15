using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class StringToReferenceCoercionTests
{
    private (IrModule module, IrBuilder builder) BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: true); // Skip stdlib for simpler tests
        var module = builder.BuildModule(tree);
        return (module, builder);
    }

    [Fact]
    public void TestStringToReferenceCoercion_WithStructType()
    {
        // This test creates a simple struct type and verifies that string values
        // can be automatically coerced to references when passed to functions
        string source = @"
struct TestStruct {
    ptr: *u8,
    len: u32
}

fn test_ref(s: &TestStruct) -> i32 {
    return 42
}

pub fn main() -> i32 {
    let str_val = TestStruct { ptr: 0 as *u8, len: 0 }
    // TestStruct value should automatically coerce to &TestStruct
    return test_ref(str_val)
}
";

        var (module, builder) = BuildIr(source);
        Assert.NotNull(module);
       Assert.False(builder.Diagnostics.HasErrors,
            builder.Diagnostics.HasErrors ?
            $"Compilation failed with error count: {builder.Diagnostics.ErrorCount}" :
            "No errors");
    }

    [Fact]
    public void TestStringToReferenceCoercion_RealStringLiteral()
    {
        // This test uses the actual Str type from the import
        string source = @"
from core import Str

fn test_ref(s: &Str) -> i32 {
    return 42
}

pub fn main() -> i32 {
    // String literal creates a Str value, should auto-coerce to &Str
    return test_ref(""Hello"")
}
";

        var builder = new IrBuilder(skipAutoImports: false); // Enable stdlib
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var module = builder.BuildModule(tree);

        Assert.NotNull(module);
        Assert.False(builder.Diagnostics.HasErrors,
            builder.Diagnostics.HasErrors ?
            $"Compilation failed with error count: {builder.Diagnostics.ErrorCount}" :
            "No errors");
    }

    [Fact]
    public void TestStringToReferenceCoercion_WindowHandleSimple()
    {
        string source = @"
from intuition import WindowHandle

pub fn main() -> i32 {
    // WindowHandle::simple expects &Str for the title parameter
    // String literal should automatically coerce to &Str without manual & operator
    let result = WindowHandle::simple(""Novus Window"", 320, 200)

    return 0
}
";

        var builder = new IrBuilder(skipAutoImports: false); // Enable stdlib
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var module = builder.BuildModule(tree);

        Assert.NotNull(module);
        Assert.False(builder.Diagnostics.HasErrors,
            builder.Diagnostics.HasErrors ?
            $"Compilation failed with error count: {builder.Diagnostics.ErrorCount}" :
            "No errors");
    }
}
