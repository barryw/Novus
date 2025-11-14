using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for LibraryGenerator - AmigaOS shared library generation.
/// Tests ROMTag creation, function vector tables, A6 wrappers, and FFI bindings.
/// </summary>
public class LibraryGeneratorTests
{
    private IrModule BuildIR(string source)
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
    public void LibraryGenerator_SimpleLibrary_GeneratesROMTag()
    {
        var source = @"
@library(name = ""test.library"", version = ""1.0.0"")
pub struct TestLibrary {
    lib_version: u32,
}

pub fn test_func() -> i32 {
    return 0
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module, "1.0.0");
        var romTag = generator.GenerateROMTag();

        // Verify ROMTag structure
        Assert.Contains("struct RomTag", romTag);
        Assert.Contains("RT_MATCHWORD", romTag);
        Assert.Contains("test.library", romTag);
        Assert.Contains("VERSION:", romTag);
    }

    [Fact]
    public void LibraryGenerator_WithVersion_ParsesSemanticVersion()
    {
        var source = @"
@library(name = ""mylib.library"", version = ""2.5.3"")
pub struct MyLib {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module, "2.5.3");
        var romTag = generator.GenerateROMTag();

        // Should contain version 2.5
        Assert.Contains("VERSION: 2", romTag);
        Assert.Contains("REVISION: 5", romTag);
    }

    [Fact]
    public void LibraryGenerator_DefaultVersion_Uses1_0_0()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var romTag = generator.GenerateROMTag();

        // Default version should be 1.0.0
        Assert.Contains("VERSION: 1", romTag);
        Assert.Contains("REVISION: 0", romTag);
    }

    [Fact]
    public void LibraryGenerator_GeneratesLibraryBaseStruct()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var baseStruct = generator.GenerateLibraryBaseStruct();

        // Verify library base structure contains exec library fields
        Assert.Contains("struct Library", baseStruct);
        Assert.Contains("lib_Node", baseStruct);
        Assert.Contains("lib_Flags", baseStruct);
        Assert.Contains("lib_Version", baseStruct);
    }

    [Fact]
    public void LibraryGenerator_GeneratesDefaultLifecycleFunctions()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var lifecycle = generator.GenerateDefaultLifecycleFunctions();

        // Verify all required lifecycle functions
        Assert.Contains("Library_Open", lifecycle);
        Assert.Contains("Library_Close", lifecycle);
        Assert.Contains("Library_Expunge", lifecycle);
        Assert.Contains("Library_Reserved", lifecycle);
    }

    [Fact]
    public void LibraryGenerator_GeneratesA6Wrappers()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}

@libvec(offset = -30)
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var wrappers = generator.GenerateA6Wrappers();

        // Verify A6 wrapper for add function
        Assert.Contains("_Library_add_a6", wrappers);
        Assert.Contains("__attribute__((saveds))", wrappers); // Saveds attribute for A6 base
        Assert.Contains("register", wrappers); // Register parameters
    }

    [Fact]
    public void LibraryGenerator_GeneratesCHeader()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}

pub fn get_value() -> i32 {
    return 42
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var header = generator.GenerateCHeader();

        // Verify C header
        Assert.Contains("#ifndef", header);
        Assert.Contains("struct TestLibrary", header);
        Assert.Contains("int32_t get_value", header);
    }

    [Fact]
    public void LibraryGenerator_GeneratesNovusFFI()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}

pub fn get_value() -> i32 {
    return 42
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var ffi = generator.GenerateNovusFFI();

        // Verify Novus FFI bindings
        Assert.Contains("pub struct TestLibrary", ffi);
        Assert.Contains("pub fn get_value", ffi);
        Assert.Contains("@extern", ffi);
    }

    [Fact]
    public void LibraryGenerator_GeneratesFDFile()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}

@libvec(offset = -30)
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var fd = generator.GenerateFDFile();

        // Verify FD file format (AmigaOS function descriptor)
        Assert.Contains("##base", fd);
        Assert.Contains("##bias", fd);
        Assert.Contains("add", fd);
    }

    [Fact]
    public void LibraryGenerator_GeneratesLibraryStub()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var stub = generator.GenerateLibraryStub();

        // Verify library stub (for linking)
        Assert.Contains("_TestLibraryBase", stub);
        Assert.Contains("extern", stub);
    }

    [Fact]
    public void LibraryGenerator_GeneratesClientCallStubs()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary {
    lib_version: u32,
}

@libvec(offset = -30)
pub fn get_value() -> i32 {
    return 42
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var stubs = generator.GenerateClientCallStubs();

        // Verify client stubs for calling library functions
        Assert.Contains("get_value", stubs);
        Assert.Contains("_TestLibraryBase", stubs); // Uses library base
    }

    [Fact]
    public void LibraryGenerator_AutoGeneratedFunctions_IncludesGetLibraryVersion()
    {
        var source = @"
@library(name = ""test.library"", version = ""1.2.3"")
pub struct TestLibrary {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module, "1.2.3");
        var lifecycle = generator.GenerateDefaultLifecycleFunctions();

        // Auto-generated functions should be included
        Assert.Contains("GetLibraryVersion", lifecycle);
        Assert.Contains("LibraryVersion", lifecycle); // Return type struct
    }

    [Fact]
    public void LibraryGenerator_AutoGeneratedFunctions_IncludesGetLibraryName()
    {
        var source = @"
@library(name = ""myawesome.library"")
pub struct MyLib {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var lifecycle = generator.GenerateDefaultLifecycleFunctions();

        // GetLibraryName should be auto-generated
        Assert.Contains("GetLibraryName", lifecycle);
    }

    [Fact]
    public void LibraryGenerator_MultipleLibraryFunctions_GeneratesAllVectors()
    {
        var source = @"
@library(name = ""math.library"")
pub struct MathLib {
    lib_version: u32,
}

@libvec(offset = -30)
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}

@libvec(offset = -36)
pub fn subtract(a: i32, b: i32) -> i32 {
    return a - b
}

@libvec(offset = -42)
pub fn multiply(a: i32, b: i32) -> i32 {
    return a * b
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var wrappers = generator.GenerateA6Wrappers();

        // All functions should have wrappers
        Assert.Contains("_Library_add_a6", wrappers);
        Assert.Contains("_Library_subtract_a6", wrappers);
        Assert.Contains("_Library_multiply_a6", wrappers);
    }

    [Fact]
    public void LibraryGenerator_FunctionWithMultipleParameters_GeneratesCorrectWrapper()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLib {
    lib_version: u32,
}

@libvec(offset = -30)
pub fn process(a: i32, b: u32, c: i32) -> i32 {
    return a
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var wrappers = generator.GenerateA6Wrappers();

        // Wrapper should handle all three parameters
        Assert.Contains("_Library_process_a6", wrappers);
        // Parameters should be in register specifications
        Assert.Contains("register", wrappers);
    }

    [Fact]
    public void LibraryGenerator_ROMTag_ContainsCorrectPriority()
    {
        var source = @"
@library(name = ""test.library"", priority = 10)
pub struct TestLib {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var romTag = generator.GenerateROMTag();

        // ROMTag should contain priority field
        Assert.Contains("RT_PRI", romTag);
    }

    [Fact]
    public void LibraryGenerator_ROMTag_ContainsCorrectFlags()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLib {
    lib_version: u32,
}";

        var module = BuildIR(source);

        var generator = new LibraryGenerator(module);
        var romTag = generator.GenerateROMTag();

        // ROMTag should contain type and flags
        Assert.Contains("RT_MATCHWORD", romTag);
        Assert.Contains("RT", romTag); // RomTag references
    }
}
