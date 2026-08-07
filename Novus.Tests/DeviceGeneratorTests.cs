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
/// Tests for DeviceGenerator - AmigaOS device driver generation.
/// Tests ROMTag creation, device lifecycle, BeginIO/AbortIO dispatch, and unit management.
/// </summary>
public class DeviceGeneratorTests
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
    public void DeviceGenerator_SimpleDevice_GeneratesROMTag()
    {
        var source = @"
@device(name = ""test.device"", units = 4)
pub struct TestDevice {
    custom_data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module, "1.0.0");
        Assert.True(generator.IsDevice);

        var romTag = generator.GenerateROMTag();

        // Verify ROMTag structure
        Assert.Contains("struct Resident RomTag", romTag);
        Assert.Contains("RTC_MATCHWORD", romTag);
        Assert.Contains("test.device", romTag);
        Assert.Contains("NT_DEVICE", romTag);  // Should be device, not library
    }

    [Fact]
    public void DeviceGenerator_WithVersion_ParsesSemanticVersion()
    {
        var source = @"
@device(name = ""mydev.device"", units = 2)
pub struct MyDev {
    flags: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module, "2.5.3");

        Assert.Equal(2, generator.GetDeviceVersion());
        Assert.Equal(5, generator.GetDeviceRevision());
        Assert.Equal(3, generator.GetDevicePatch());
    }

    [Fact]
    public void DeviceGenerator_DefaultVersion_Uses1_0_0()
    {
        var source = @"
@device(name = ""test.device"")
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);

        Assert.Equal(1, generator.GetDeviceVersion());
        Assert.Equal(0, generator.GetDeviceRevision());
        Assert.Equal(0, generator.GetDevicePatch());
    }

    [Fact]
    public void DeviceGenerator_ExtractsDeviceName()
    {
        var source = @"
@device(name = ""mytest.device"", units = 1)
pub struct MyTest {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);

        Assert.Equal("mytest.device", generator.GetDeviceName());
    }

    [Fact]
    public void DeviceGenerator_ExtractsMaxUnits()
    {
        var source = @"
@device(name = ""test.device"", units = 8)
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);

        Assert.Equal(8, generator.GetMaxUnits());
    }

    [Fact]
    public void DeviceGenerator_DefaultUnits_Is1()
    {
        var source = @"
@device(name = ""test.device"")
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);

        Assert.Equal(1, generator.GetMaxUnits());
    }

    [Fact]
    public void DeviceGenerator_GeneratesDeviceBaseStruct()
    {
        var source = @"
@device(name = ""test.device"", units = 4)
pub struct TestDevice {
    custom_field: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var baseStruct = generator.GenerateDeviceBaseStruct();

        // Verify device base structure contains required fields
        Assert.Contains("struct Device dev;", baseStruct);  // Embedded Device struct
        Assert.Contains("BPTR dev_SegList;", baseStruct);   // Segment list field
        Assert.Contains("UWORD dev_Patch;", baseStruct);    // Patch version field
        Assert.Contains("dev_Units[4]", baseStruct);        // Unit pointer array
        Assert.Contains("dev_TotalCommands", baseStruct);   // Statistics
        Assert.Contains("TestDevice state", baseStruct);    // Novus-defined state, embedded once
    }

    [Fact]
    public void DeviceGenerator_GeneratesLifecycleFunctions()
    {
        var source = @"
@device(name = ""test.device"", units = 2)
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var lifecycle = generator.GenerateLifecycleFunctions();

        // Verify all required lifecycle functions
        Assert.Contains("DevInit", lifecycle);
        Assert.Contains("DevOpen", lifecycle);
        Assert.Contains("DevClose", lifecycle);
        Assert.Contains("DevExpunge", lifecycle);
        Assert.Contains("DevReserved", lifecycle);
        Assert.Contains("DevBeginIO", lifecycle);
        Assert.Contains("DevAbortIO", lifecycle);
    }

    [Fact]
    public void DeviceGenerator_GeneratesA6Wrappers()
    {
        var source = @"
@device(name = ""test.device"")
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var wrappers = generator.GenerateA6Wrappers();

        // Verify A6 wrapper assembly
        Assert.Contains("DevOpen_Wrapper", wrappers);
        Assert.Contains("DevClose_Wrapper", wrappers);
        Assert.Contains("DevExpunge_Wrapper", wrappers);
        Assert.Contains("DevBeginIO_Wrapper", wrappers);
        Assert.Contains("DevAbortIO_Wrapper", wrappers);
        Assert.Contains("SECTION CODE", wrappers);
    }

    [Fact]
    public void DeviceGenerator_GeneratesCHeader()
    {
        var source = @"
@device(name = ""test.device"", units = 4)
pub struct TestDevice {
    custom_data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var header = generator.GenerateCHeader();

        // Verify C header structure
        Assert.Contains("#ifndef", header);
        Assert.Contains("#define", header);
        Assert.Contains("struct TestDeviceBase", header);
        Assert.Contains("struct TestDeviceUnit", header);
        Assert.Contains("#endif", header);
    }

    [Fact]
    public void DeviceGenerator_GeneratesNovusFFI()
    {
        var source = @"
@device(name = ""test.device"", units = 2)
pub struct TestDevice {
    flags: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var ffi = generator.GenerateNovusFFI();

        // Verify FFI binding
        Assert.Contains("pub enum TestDeviceError", ffi);
        Assert.Contains("pub struct TestDevice", ffi);
        Assert.Contains("pub fn open", ffi);
        Assert.Contains("pub fn command", ffi);
        Assert.Contains("impl Drop for TestDevice", ffi);
    }

    [Fact]
    public void DeviceGenerator_NoDeviceAttribute_IsDeviceReturnsFalse()
    {
        var source = @"
pub struct NotADevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        Assert.False(generator.IsDevice);
    }

    [Fact]
    public void DeviceGenerator_DeviceOpen_HandlesUnitValidation()
    {
        var source = @"
@device(name = ""test.device"", units = 4)
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var lifecycle = generator.GenerateLifecycleFunctions();

        // Should validate unit number
        Assert.Contains("if (unitNum >= 4)", lifecycle);
        Assert.Contains("IOERR_OPENFAIL", lifecycle);
    }

    [Fact]
    public void DeviceGenerator_ROMTag_HasDeviceType()
    {
        var source = @"
@device(name = ""test.device"")
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var romTag = generator.GenerateROMTag();

        // Should use NT_DEVICE type, not NT_LIBRARY
        Assert.Contains("NT_DEVICE", romTag);
        Assert.DoesNotContain("NT_LIBRARY", romTag);
    }

    [Fact]
    public void DeviceGenerator_FunctionTable_HasFixedLayout()
    {
        var source = @"
@device(name = ""test.device"")
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var romTag = generator.GenerateROMTag();

        // Verify function table has correct fixed offsets
        Assert.Contains("-6: Open", romTag);
        Assert.Contains("-12: Close", romTag);
        Assert.Contains("-18: Expunge", romTag);
        Assert.Contains("-24: Reserved", romTag);
        Assert.Contains("-30: BeginIO", romTag);
        Assert.Contains("-36: AbortIO", romTag);
    }

    [Fact]
    public void DeviceGenerator_BeginIO_HasCommandDispatch()
    {
        var source = @"
@device(name = ""test.device"")
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var lifecycle = generator.GenerateLifecycleFunctions();

        // Should have switch on io_Command
        Assert.Contains("switch (ioReq->io_Command)", lifecycle);
        Assert.Contains("default:", lifecycle);
        Assert.Contains("IOERR_NOCMD", lifecycle);
    }

    [Fact]
    public void DeviceGenerator_AbortIO_HandlesAbort()
    {
        var source = @"
@device(name = ""test.device"")
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var lifecycle = generator.GenerateLifecycleFunctions();

        // Should have abort handling
        Assert.Contains("DevAbortIO", lifecycle);
        Assert.Contains("IOERR_ABORTED", lifecycle);
        Assert.Contains("NT_MESSAGE", lifecycle);  // Check message state
    }

    [Fact]
    public void DeviceGenerator_DeviceClose_CallsExpungeIfNeeded()
    {
        var source = @"
@device(name = ""test.device"")
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var lifecycle = generator.GenerateLifecycleFunctions();

        // DevClose should check for delayed expunge
        Assert.Contains("LIBF_DELEXP", lifecycle);
        Assert.Contains("DevExpunge", lifecycle);
    }

    [Fact]
    public void DeviceGenerator_DeviceExpunge_FreesUnits()
    {
        var source = @"
@device(name = ""test.device"", units = 4)
pub struct TestDevice {
    data: u32,
}";

        var module = BuildIR(source);

        var generator = new DeviceGenerator(module);
        var lifecycle = generator.GenerateLifecycleFunctions();

        // DevExpunge should free unit structures
        Assert.Contains("FreeMem(base->dev_Units[i]", lifecycle);
    }
}
