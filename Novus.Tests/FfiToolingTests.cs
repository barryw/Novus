using Novus.Codegen;
using Novus.Compilation;
using Novus.Tools;
using Xunit;

namespace Novus.Tests;

public class FfiToolingTests
{
    [Fact]
    public void CHeaderParser_ExpandsCommaSeparatedFields()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                struct Example
                {
                    WORD left, right;
                    struct Interrupt first, second;
                    UBYTE *red, *green;
                    void (*callback)(int x, int y);
                    WORD (*handler) __CLIB_PROTOTYPE((struct Example *));
                    LONG (*handlers[16]) __CLIB_PROTOTYPE((struct Example *, WORD));
                    union {
                        ULONG value;
                        UBYTE bytes[4];
                    } choice;
                    ULONG after;
                    union Named *named;
                    ULONG *buffers[9];
                    char languages[10][30];
                    UWORD cylinders; // inline comment must not become a field
                };
                typedef struct tPoint
                {
                    WORD x, y;
                } Point;
                """);

            var structs = CHeaderParser.ParseFile(path).Structs;
            var example = Assert.Single(structs, value => value.Name == "Example");
            var fields = example.Fields;

            Assert.Equal(
                ["left", "right", "first", "second", "red", "green", "callback", "handler", "handlers", "after", "named", "buffers", "languages", "cylinders"],
                fields.Select(field => field.Name));
            Assert.Equal("struct Interrupt", fields[2].Type);
            Assert.Equal("UBYTE *", fields[5].Type);
            Assert.Equal("WORD (*handler)(struct Example *)", fields[7].Type);
            Assert.True(fields[8].IsArray);
            Assert.Equal("16", fields[8].ArraySize);
            Assert.Equal("ULONG *", fields[11].Type);
            Assert.Equal("10][30", fields[12].ArraySize);
            Assert.True(example.HasUnion);
            Assert.Equal(["x", "y"], Assert.Single(structs, value => value.Name == "Point").Fields.Select(field => field.Name));
            Assert.Empty(Assert.Single(structs, value => value.Name == "tPoint").Fields);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FfiRuntime_UsesModuleMetadataAndDeduplicatesBases()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"novus-ffi-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "window.novus");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, """
                // Generated binding
                // Library: window.library
                // Base: _WindowBase
                """);

            var window = Assert.IsType<FfiModuleMetadata>(FfiModuleMetadata.TryRead(path));
            var timer = new FfiModuleMetadata(path, "timer_device", "timer.device", "timer.device", "_TimerBase", FfiModuleKind.Device, 0);
            var resource = new FfiModuleMetadata(path, "battmem_resource", "battmem.resource", "battmem.resource", "_BattMemBase", FfiModuleKind.Resource, 0);

            Assert.Equal("window.class", window.OpenName);
            Assert.Equal(44, window.MinimumVersion);

            var assembly = FfiRuntimeGenerator.Generate([window, window, timer, resource]);
            Assert.Equal(1, Count(assembly, "__novus_window_name:"));
            Assert.Contains("jsr\t-444(a6)\t; OpenDevice", assembly);
            Assert.Contains("jsr\t-498(a6)\t; OpenResource", assembly);
            Assert.Contains("dc.b\t'window.class',0", assembly);
            Assert.DoesNotContain(".__novus_battmem_resource_closed:", assembly);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SfdGenerator_PreservesCallbacksAndDoubleRegisterPairs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"novus-sfd-{Guid.NewGuid():N}");
        var ndk = Path.Combine(root, "ndk");
        var output = Path.Combine(root, "output");
        var sfdDirectory = Path.Combine(ndk, "Include", "sfd");
        var includeDirectory = Path.Combine(ndk, "Include", "include_h");
        try
        {
            Directory.CreateDirectory(sfdDirectory);
            Directory.CreateDirectory(includeDirectory);
            File.WriteAllText(Path.Combine(includeDirectory, "test.h"), """
                #define POINTERSIZE (1 + 16 + 1) * 2
                #define BASE_TAG 100
                #define ALIAS_TAG (BASE_TAG + 0x01)
                #define TAG_USER ((ULONG)(1L << 31))
                #define WA_Dummy (TAG_USER + 99)
                #define CONST const
                #define BROKEN BASE_TAG)
                #define BROKEN_ALIAS BROKEN
                struct PointerData {
                    UBYTE words[POINTERSIZE];
                    APTR blitter;
                    char languages[10][30];
                };
                """);
            File.WriteAllText(Path.Combine(sfdDirectory, "test_lib.sfd"), """
                ==base _TestBase
                ==libname test.library
                ==bias 30
                ==include <test.h>
                DOUBLE AddDouble(DOUBLE left, DOUBLE right)(d0-d1,d2-d3)
                ==varargs
                DOUBLE AddDoubleTags(Tag first, ...)(d0-d1)
                ULONG SetHook(ULONG (*hook)(APTR object, APTR message))(a0)
                """);

            new SfdGenerator(ndk, output).GenerateAllBindings();

            var binding = File.ReadAllText(Path.Combine(output, "std", "ffi", "test.novus"));
            var stub = File.ReadAllText(Path.Combine(output, "stubs", "test_stubs.s"));
            var constants = File.ReadAllText(Path.Combine(output, "std", "ffi", "amiga_consts.novus"));
            var structs = File.ReadAllText(Path.Combine(output, "std", "ffi", "amiga_structs.novus"));
            Assert.Contains("hook: fn(*u8, *u8) -> u32", binding);
            Assert.Equal("i8", SfdParser.MapAmigaTypeToNovus("char"));
            Assert.Equal("i32", SfdParser.MapAmigaTypeToNovus("BSTR"));
            Assert.Equal("*u32", SfdParser.MapAmigaTypeToNovus("ULONG *"));
            Assert.Equal("*u8", SfdParser.MapAmigaTypeToNovus("void *"));
            Assert.Contains("pub const POINTERSIZE: u32 = (1 + 16 + 1) * 2", constants);
            Assert.Contains("pub const ALIAS_TAG: u32 = (BASE_TAG + $01)", constants);
            Assert.Contains("pub const TAG_USER: u32 = ((1 << 31))", constants);
            Assert.Contains("pub const WA_Dummy: u32 = (TAG_USER + 99)", constants);
            Assert.True(constants.IndexOf("pub const BASE_TAG", StringComparison.Ordinal) <
                        constants.IndexOf("pub const ALIAS_TAG", StringComparison.Ordinal));
            Assert.DoesNotContain("pub const CONST", constants);
            Assert.DoesNotContain("pub const BROKEN", constants);
            Assert.DoesNotContain("pub const BROKEN_ALIAS", constants);
            Assert.Contains("from std::ffi::amiga_consts import *", structs);
            Assert.Contains("#[extern_type]\npub struct PointerData", structs);
            Assert.Contains("words: [u8; ((1 + 16 + 1) * 2)]", structs);
            Assert.Contains("_blitter: *u8", structs);
            Assert.Contains("languages: [[i8; 30]; 10]", structs);
            Assert.Contains("test|test.h", File.ReadAllText(Path.Combine(output, "std", "ffi", "ndk_headers.txt")));
            var ndkTypes = File.ReadAllText(Path.Combine(output, "std", "ffi", "ndk_types.h"));
            Assert.Contains("typedef struct PointerData PointerData;", ndkTypes);
            Assert.DoesNotContain("typedef struct tPoint Point;", ndkTypes);
            Assert.Contains("test.h", Assert.IsType<FfiModuleMetadata>(
                FfiModuleMetadata.TryRead(Path.Combine(output, "std", "ffi", "test.novus"))).Headers);
            Assert.Contains("movem.l\t4(sp),d0-d1", stub);
            Assert.Contains("movem.l\t12(sp),d2-d3", stub);
            Assert.Contains("jsr\t-36(a6)", stub);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static int Count(string value, string needle) =>
        value.Split(needle, StringSplitOptions.None).Length - 1;
}
