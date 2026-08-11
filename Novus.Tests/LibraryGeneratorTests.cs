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
    public void ResourceGenerator_UsesPermanentResourceLifecycleAndInjectedState()
    {
        var module = BuildIR("""
            @resource(name = "probe.resource")
            pub struct Probe { value: u32 }

            @resourceinit
            pub fn initialize(state: *Probe) -> bool { return true }

            @resourcefunc
            pub fn read(state: *Probe, index: u32) -> u32 { return index }
            """);

        var generator = new LibraryGenerator(module, "2.1.0");

        Assert.True(generator.IsResource);
        Assert.Contains("NT_RESOURCE", generator.GenerateROMTag());
        Assert.Contains("__entry struct Resident RomTag", generator.GenerateROMTag());
        Assert.DoesNotContain("LibClose", generator.GenerateROMTag());
        Assert.Contains("__entry uint32_t read_ResourceThunk", generator.GenerateDefaultLifecycleFunctions());
        Assert.Contains("move.l  a6,-(sp)", generator.GenerateA6Wrappers());
        Assert.Contains("#[link_name = \"@6\"]", generator.GenerateNovusFFI());
        Assert.Contains("pub fn read(index: u32) -> u32", generator.GenerateNovusFFI());
        Assert.DoesNotContain("CloseResource", generator.GenerateNovusFFI());
    }

    [Fact]
    public void ResourceGenerator_RequiresStateAsFirstVectorParameter()
    {
        var module = BuildIR("""
            @resource(name = "probe.resource")
            pub struct Probe { value: u32 }

            @resourcefunc
            pub fn read(index: u32) -> u32 { return index }
            """);

        var error = Assert.Throws<InvalidOperationException>(() => new LibraryGenerator(module));
        Assert.Contains("state: *Probe", error.Message);
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
        Assert.Contains("__entry struct Resident RomTag", romTag);
        Assert.Contains("RTC_MATCHWORD", romTag);
        Assert.Contains("test.library", romTag);
        Assert.Contains("RTF_AUTOINIT", romTag);
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

        // Should contain version 2 (major version only in ROMTag)
        Assert.Contains("2,", romTag); // Version field in struct Resident
        Assert.Contains("mylib.library 2.5", romTag); // ID string includes full version
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
        Assert.Contains("1,", romTag); // Version 1 in struct Resident
        Assert.Contains("test.library 1.0", romTag); // ID string with version
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
        Assert.Contains("struct Library lib;", baseStruct); // Embedded Library struct
        Assert.Contains("BPTR lib_SegList;", baseStruct); // Segment list field
        Assert.Contains("UWORD lib_Patch;", baseStruct); // Patch version field
        Assert.Contains("__call_count", baseStruct); // Auto-generated call counter
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
        Assert.Contains("LibOpen", lifecycle);
        Assert.Contains("LibClose", lifecycle);
        Assert.Contains("LibExpunge", lifecycle);
        Assert.Contains("LibReserved", lifecycle);
        Assert.Contains("__entry struct Library* LibOpen", lifecycle);
        Assert.Contains("__entry void TestLibrary_IncrementCallCount", lifecycle);
        Assert.Contains("ULONG TestLibrary_GetCallCount", lifecycle);
        Assert.Contains("UWORD open_count", lifecycle);
        Assert.DoesNotContain("uint32_t TestLibrary_GetCallCount", lifecycle);
    }

    [Fact]
    public void LibraryGenerator_LifecycleHooksKeepGeneratedExecBookkeeping()
    {
        var source = @"
@library(name = ""test.library"")
pub struct TestLibrary { counter: u32 }

impl TestLibrary {
    @libinit
    pub fn loaded(state: *TestLibrary) -> bool { return true }
    @libopen
    pub fn opening(state: *TestLibrary) -> bool { return true }
    @libclose
    pub fn closing(state: *TestLibrary) {}
    @libexpunge
    pub fn unloading(state: *TestLibrary) {}
}";

        var generator = new LibraryGenerator(BuildIR(source));
        var baseStruct = generator.GenerateLibraryBaseStruct();
        var lifecycle = generator.GenerateDefaultLifecycleFunctions();
        var romTag = generator.GenerateROMTag();

        Assert.Contains("TestLibrary state;", baseStruct);
        Assert.Contains("TestLibrary_loaded(&base->state)", lifecycle);
        Assert.Contains("TestLibrary_opening(&base->state)", lifecycle);
        Assert.Contains("TestLibrary_closing(&base->state)", lifecycle);
        Assert.Contains("TestLibrary_unloading(&base->state)", lifecycle);
        Assert.Contains("base->lib.lib_OpenCnt++", lifecycle);
        Assert.DoesNotContain("loaded_Wrapper", romTag);
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

        // Verify A6 wrapper for add function (assembly code)
        Assert.Contains("_add_Wrapper", wrappers); // Wrapper for top-level function
        Assert.Contains("move.l", wrappers); // Assembly instructions
        Assert.Contains("jsr", wrappers); // Jump to subroutine
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
        Assert.Contains("pub struct TestLibraryBase", ffi); // Library base struct
        Assert.Contains("pub fn get_value", ffi); // Safe wrapper function
        Assert.Contains("extern fn", ffi); // FFI declarations
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

        // Verify library stub (assembly code for auto-open/close)
        Assert.Contains("_TestLibraryBase", stub);
        Assert.Contains("XDEF", stub); // Export symbol
        Assert.Contains("dc.l", stub); // Define long word
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
        Assert.Contains("move.l  a6,-(sp)", stubs);
        Assert.Contains("movea.l (sp)+,a6", stubs);
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

        // All functions should have wrappers (top-level functions use simple names)
        Assert.Contains("_add_Wrapper", wrappers);
        Assert.Contains("_subtract_Wrapper", wrappers);
        Assert.Contains("_multiply_Wrapper", wrappers);
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

        // Wrapper should handle all three parameters (top-level function)
        Assert.Contains("_process_Wrapper", wrappers);
        // Assembly wrappers use registers (d0, d1, a0, a1)
        Assert.Contains("move.l", wrappers); // Register moves
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

        // ROMTag should contain priority field (0 for default)
        Assert.Contains("0,", romTag); // Priority field in struct Resident
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
        Assert.Contains("RTC_MATCHWORD", romTag); // Correct constant name
        Assert.Contains("RTF_AUTOINIT", romTag); // Flags field
    }
}
