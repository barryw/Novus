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
    let null_ptr: *u8 = (*u8)0
    let str_val = TestStruct { ptr: null_ptr, len: 0 }
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
from std::string::core import Str

fn test_ref(s: &Str) -> i32 {
    return 42
}

pub fn main() -> i32 {
    // String literal creates a Str value, should auto-coerce to &Str
    return test_ref(&""Hello"")
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
from amiga::sys::intuition import WindowHandle

pub fn main() -> i32 {
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

    [Fact]
    public void TestStringToPointerCoercion_InOptionSome()
    {
        // Test that string literals automatically coerce to *u8 inside Option::Some
        string source = @"
from std::core import Option
from std::string::core import Str

pub fn main() -> i32 {
    // String literal should auto-coerce from Str to *u8
    let title: Option<*u8> = Option::Some(""Test Title"")

    return 0
}
";

        var builder = new IrBuilder(skipAutoImports: false);
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var module = builder.BuildModule(tree);

        Assert.NotNull(module);
        Assert.False(builder.Diagnostics.HasErrors,
            builder.Diagnostics.HasErrors ?
            $"Compilation failed: {string.Join(", ", builder.Diagnostics.Diagnostics.Where(d => d.IsError).Select(d => d.Message))}" :
            "No errors");
    }

    [Fact]
    public void TestStringToPointerCoercion_InSetTitles()
    {
        // Test set_titles() with automatic string coercion
        string source = @"
from amiga::sys::intuition import WindowHandle
from std::core import Option
from std::string::core import Str

pub fn test_set_titles(window: &WindowHandle) {
    // String literals should auto-coerce to *u8 inside Option::Some
    let new_title: Option<*u8> = Option::Some(""New Title"")
    let no_change: Option<*u8> = Option::None
    window.set_titles(new_title, no_change)
}

pub fn main() -> i32 {
    return 0
}
";

        var builder = new IrBuilder(skipAutoImports: false);
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var module = builder.BuildModule(tree);

        Assert.NotNull(module);
        Assert.False(builder.Diagnostics.HasErrors,
            builder.Diagnostics.HasErrors ?
            $"Compilation failed: {string.Join(", ", builder.Diagnostics.Diagnostics.Where(d => d.IsError).Select(d => d.Message))}" :
            "No errors");
    }

    [Fact]
    public void TestModifyIdcmp_ReturnsResult()
    {
        // Test modify_idcmp() returns Result<(), IntuitionError>
        string source = @"
from amiga::sys::intuition import WindowHandle, IntuitionError, IDCMP_MOUSEBUTTONS
from std::core import Result

pub fn test_modify_idcmp(window: &WindowHandle) -> Result<(), IntuitionError> {
    return window.modify_idcmp(IDCMP_MOUSEBUTTONS)
}

pub fn main() -> i32 {
    return 0
}
";

        var builder = new IrBuilder(skipAutoImports: false);
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var module = builder.BuildModule(tree);

        Assert.NotNull(module);
        Assert.False(builder.Diagnostics.HasErrors,
            builder.Diagnostics.HasErrors ?
            $"Compilation failed: {string.Join(", ", builder.Diagnostics.Diagnostics.Where(d => d.IsError).Select(d => d.Message))}" :
            "No errors");
    }

    [Fact]
    public void TestWindowHandleDrop_WithMenuClearing()
    {
        // Test that WindowHandle Drop implementation compiles correctly
        // The Drop impl should automatically clear menu strips and close the window
        string source = @"
from amiga::sys::intuition import WindowHandle

pub fn test_window_drop() {
    let result = WindowHandle::simple(""Test"", 320, 200)

    match result {
        Result::Ok(window) => {
            // Window will be automatically dropped at end of scope
            // Drop should drain messages, clear menu strip if present, and close window
        },
        Result::Err(_) => {}
    }
}

pub fn main() -> i32 {
    test_window_drop()
    return 0
}
";

        var builder = new IrBuilder(skipAutoImports: false);
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var module = builder.BuildModule(tree);

        Assert.NotNull(module);
        Assert.False(builder.Diagnostics.HasErrors,
            builder.Diagnostics.HasErrors ?
            $"Compilation failed: {string.Join(", ", builder.Diagnostics.Diagnostics.Where(d => d.IsError).Select(d => d.Message))}" :
            "No errors");
    }
}
