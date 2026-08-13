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
                enum ResultCode
                {
                    RESULT_OK = 4,
                    RESULT_RETRY,
                    RESULT_FAILED
                };
                union Choice {
                    ULONG value;
                    UBYTE bytes[4];
                };
                struct Single { ULONG first; UWORD second; };
                """);

            var header = CHeaderParser.ParseFile(path);
            var structs = header.Structs;
            var example = Assert.Single(structs, value => value.Name == "Example");
            var fields = example.Fields;

            Assert.Equal(
                ["left", "right", "first", "second", "red", "green", "callback", "handler", "handlers", "choice", "after", "named", "buffers", "languages", "cylinders"],
                fields.Select(field => field.Name));
            Assert.Equal("struct Interrupt", fields[2].Type);
            Assert.Equal("UBYTE *", fields[5].Type);
            Assert.Equal("WORD (*handler)(struct Example *)", fields[7].Type);
            Assert.True(fields[8].IsArray);
            Assert.Equal("16", fields[8].ArraySize);
            Assert.Equal("Example_choice", fields[9].Type);
            Assert.Equal("ULONG *", fields[12].Type);
            Assert.Equal("10][30", fields[13].ArraySize);
            Assert.True(example.HasUnion);
            var nestedUnion = Assert.Single(structs, value => value.Name == "Example_choice");
            Assert.True(nestedUnion.IsUnion);
            Assert.True(nestedUnion.IsSynthetic);
            Assert.Equal(["value", "bytes"], nestedUnion.Fields.Select(field => field.Name));
            Assert.Equal(["x", "y"], Assert.Single(structs, value => value.Name == "Point").Fields.Select(field => field.Name));
            Assert.Empty(Assert.Single(structs, value => value.Name == "tPoint").Fields);
            Assert.Equal("4", Assert.Single(header.Constants, value => value.Name == "RESULT_OK").Value);
            Assert.Equal("(RESULT_OK + 1)", Assert.Single(header.Constants, value => value.Name == "RESULT_RETRY").Value);
            Assert.Equal("(RESULT_RETRY + 1)", Assert.Single(header.Constants, value => value.Name == "RESULT_FAILED").Value);
            var union = Assert.Single(structs, value => value.Name == "Choice");
            Assert.True(union.IsUnion);
            Assert.Equal(["value", "bytes"], union.Fields.Select(field => field.Name));
            Assert.Equal(["first", "second"], Assert.Single(structs, value => value.Name == "Single").Fields.Select(field => field.Name));
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
            var dos = new FfiModuleMetadata(path, "dos", "dos.library", "dos.library", "_DOSBase", FfiModuleKind.Library, 0);

            Assert.Equal("window.class", window.OpenName);
            Assert.Equal(44, window.MinimumVersion);

            var assembly = FfiRuntimeGenerator.Generate([window, window, timer, resource, dos]);
            Assert.Equal(1, Count(assembly, "__novus_window_name:"));
            Assert.Equal(1, Count(assembly, "_WindowBase:\tds.l\t1"));
            Assert.Equal(1, Count(assembly, "_DOSBase:\tds.l\t1"));
            Assert.DoesNotContain("_GadToolsBase:\tds.l\t1", assembly);
            Assert.Contains("_SysBase:\tds.l\t1", assembly);
            Assert.Contains("_WBStartupMsg:\tds.l\t1", assembly);
            Assert.Contains("jsr\t-444(a6)\t; OpenDevice", assembly);
            Assert.Contains("jsr\t-498(a6)\t; OpenResource", assembly);
            Assert.Contains("dc.b\t'window.class',0", assembly);
            Assert.DoesNotContain(".__novus_battmem_resource_closed:", assembly);
            Assert.True(assembly.IndexOf("__novus_dos_name:", StringComparison.Ordinal) <
                        assembly.IndexOf("__novus_window_name:", StringComparison.Ordinal));
            Assert.Contains("jsr\t___novus_library_not_found", assembly);
            Assert.Contains("moveq\t#0,d1", assembly);
            Assert.DoesNotContain("move.l\td0,_DOSBase\n\ttst.l\t_DOSBase", assembly);
            Assert.DoesNotContain("_WBStartupMsg", FfiRuntimeGenerator.Generate([dos], includeWorkbenchStartup: false));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FfiRuntime_UsesCompactTableForLibraryOnlyPrograms()
    {
        var path = Path.GetTempFileName();
        try
        {
            var bindings = new[]
            {
                new FfiModuleMetadata(path, "dos", "dos.library", "dos.library", "_DOSBase", FfiModuleKind.Library, 37),
                new FfiModuleMetadata(path, "intuition", "intuition.library", "intuition.library", "_IntuitionBase", FfiModuleKind.Library, 39),
                new FfiModuleMetadata(path, "gadtools", "gadtools.library", "gadtools.library", "_GadToolsBase", FfiModuleKind.Library, 37)
            };

            var assembly = FfiRuntimeGenerator.Generate(bindings);

            Assert.Contains("__novus_ffi_table:", assembly);
            Assert.Contains("\tdc.l\t_IntuitionBase", assembly);
            Assert.Contains("\tmove.w\t(a4)+,d0", assembly);
            Assert.Contains("\tdbra\td4,.__novus_ffi_close_next", assembly);
            Assert.DoesNotContain(".__novus_intuition_ready:", assembly);
        }
        finally
        {
            File.Delete(path);
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
                #define TEST_CLASS "test.class"
                #define TEST_CLASS_ALIAS TEST_CLASS
                #define TEST_ID MAKE_ID('T','E','S','T')
                #define MULTILINE_BITS (BASE_TAG | \
                    ALIAS_TAG)
                #define NULL_SENTINEL ((struct Hook *) NULL)
                #define CONST const
                #define BROKEN BASE_TAG)
                #define BROKEN_ALIAS BROKEN
                struct PointerData {
                    UBYTE words[POINTERSIZE];
                    union {
                        ULONG value;
                        UBYTE bytes[4];
                    } choice;
                    APTR blitter;
                    char languages[10][30];
                };
                struct Hook {
                    struct MinNode h_MinNode;
                    ULONG (*h_Entry)();
                    ULONG (*h_SubEntry)();
                    APTR h_Data;
                };
                struct Interrupt {
                    struct Node is_Node;
                    APTR is_Data;
                    VOID (*is_Code)();
                };
                struct IntVector {
                    APTR iv_Data;
                    VOID (*iv_Code)();
                    struct Node *iv_Node;
                };
                """);
            File.WriteAllText(Path.Combine(sfdDirectory, "test_lib.sfd"), """
                ==base _TestBase
                ==libname test.library
                ==bias 30
                ==include <test.h>
                DOUBLE AddDouble(DOUBLE left, DOUBLE right)(d0-d1,d2-d3)
                struct Missing * FindMissing(void)()
                struct PointerData * CreateContext(struct PointerData **glistptr)(a0)
                ==version 39
                ULONG OpenTagList(ULONG object, const struct TagItem * tags)(a0,a1)
                ==varargs
                ULONG OpenTags(ULONG object, Tag first, ...)(a0,a1)
                ULONG VPrintArgs(ULONG object, APTR args)(d2,d3)
                ==varargs
                ULONG PrintArgs(ULONG object, ...)(d2,d3)
                ==alias
                ULONG PrintArgsAlias(ULONG object, APTR args)(d2,d3)
                ULONG SetHook(ULONG (*hook)(APTR object, APTR message))(a0)
                """);
            File.WriteAllText(Path.Combine(sfdDirectory, "cia_lib.sfd"), """
                ==bias 6
                ==public
                ==include <test.h>
                struct Interrupt * AddICRVector(struct Library * resource, WORD bit, struct Interrupt * interrupt) (a6,d0,a1)
                """);
            File.WriteAllText(Path.Combine(sfdDirectory, "intuition_lib.sfd"), """
                ==base _IntuitionBase
                ==libname intuition.library
                ==bias 234
                ==include <test.h>
                VOID ReportMouse(BOOL flag, struct Window * window) (d0,a0)
                ==alias
                VOID ReportMouse1(struct Window * flag, BOOL window) (d0,a0)
                """);

            new SfdGenerator(ndk, output).GenerateAllBindings();

            var binding = File.ReadAllText(Path.Combine(output, "std", "amiga", "raw", "test.novus"));
            var stub = File.ReadAllText(Path.Combine(output, "stubs", "test_stubs.s"));
            var constants = File.ReadAllText(Path.Combine(output, "std", "amiga", "raw", "consts.novus"));
            var structs = File.ReadAllText(Path.Combine(output, "std", "amiga", "raw", "structs.novus"));
            Assert.Contains("hook: fn(*u8, *u8) -> u32", binding);
            Assert.Contains("CreateContext(glistptr: **PointerData) -> *PointerData", binding);
            Assert.Equal(39, Assert.IsType<FfiModuleMetadata>(FfiModuleMetadata.TryRead(
                Path.Combine(output, "std", "amiga", "raw", "test.novus"))).FunctionVersions["OpenTagList"]);
            Assert.Contains("extern pub fn OpenTags(object: u32, first: u32, ...args) -> u32", binding);
            Assert.Contains("extern pub fn PrintArgs(object: u32, ...args) -> u32", binding);
            Assert.Contains("extern pub fn PrintArgsAlias(object: u32, args: *u8) -> u32", binding);
            Assert.Equal("i8", SfdParser.MapAmigaTypeToNovus("char"));
            Assert.Equal("i32", SfdParser.MapAmigaTypeToNovus("BSTR"));
            Assert.Equal("i64", SfdParser.MapAmigaTypeToNovus("QUAD"));
            Assert.Equal("u64", SfdParser.MapAmigaTypeToNovus("UQUAD"));
            Assert.Equal("u16", SfdParser.MapAmigaTypeToNovus("USHORT"));
            Assert.Equal("u32", SfdParser.MapAmigaTypeToNovus("CPTR"));
            Assert.Equal("u32", SfdParser.MapAmigaTypeToNovus("RESOURCEID"));
            Assert.Equal("*u8", SfdParser.MapAmigaTypeToNovus("const DisplayInfoHandle"));
            Assert.Equal("*u32", SfdParser.MapAmigaTypeToNovus("Msg"));
            Assert.Equal("*u32", SfdParser.MapAmigaTypeToNovus("ULONG *"));
            Assert.Equal("*u8", SfdParser.MapAmigaTypeToNovus("void *"));
            Assert.Contains("pub const POINTERSIZE: u32 = (1 + 16 + 1) * 2", constants);
            Assert.Contains("pub const ALIAS_TAG: u32 = (BASE_TAG + $01)", constants);
            Assert.Contains("pub const TAG_USER: u32 = ((1 << 31))", constants);
            Assert.Contains("pub const WA_Dummy: u32 = (TAG_USER + 99)", constants);
            Assert.Contains("pub const TEST_CLASS: *u8 = \"test.class\"", constants);
            Assert.Contains("pub const TEST_CLASS_ALIAS: *u8 = TEST_CLASS", constants);
            Assert.Contains("pub const TEST_ID: u32 = $54455354", constants);
            Assert.Contains("pub const MULTILINE_BITS: u32 =", constants);
            Assert.Contains("pub const NULL_SENTINEL: u32 =", constants);
            Assert.True(constants.IndexOf("pub const BASE_TAG", StringComparison.Ordinal) <
                        constants.IndexOf("pub const ALIAS_TAG", StringComparison.Ordinal));
            Assert.DoesNotContain("pub const CONST", constants);
            Assert.DoesNotContain("pub const BROKEN", constants);
            Assert.DoesNotContain("pub const BROKEN_ALIAS", constants);
            Assert.Contains("BROKEN = BASE_TAG)", File.ReadAllText(
                Path.Combine(output, "std", "amiga", "raw", "ndk_unsupported_macros.txt")));
            Assert.Contains("from amiga::raw::consts import *", structs);
            Assert.Contains("#[extern_type]\npub struct PointerData", structs);
            Assert.Contains("words: [u8; ((1 + 16 + 1) * 2)]", structs);
            Assert.Contains("pub union PointerData_choice", structs);
            Assert.Contains("choice: PointerData_choice", structs);
            Assert.Contains("_blitter: *u8", structs);
            Assert.Contains("languages: [[i8; 30]; 10]", structs);
            Assert.Contains("h_Entry: amiga fn(*Hook in a0, *u8 in a2, *u8 in a1) -> u32 in d0", structs);
            Assert.Contains("h_SubEntry: amiga fn(*Hook in a0, *u8 in a2, *u8 in a1) -> u32 in d0", structs);
            Assert.Contains("is_Code: amiga fn(*u8 in a1) -> u32 in d0", structs);
            Assert.Contains("iv_Code: amiga fn(*u8 in a1) -> u32 in d0", structs);
            Assert.Contains("pub struct Missing", structs);
            Assert.Contains("test|test.h", File.ReadAllText(Path.Combine(output, "std", "amiga", "raw", "ndk_headers.txt")));
            var ndkTypes = File.ReadAllText(Path.Combine(output, "std", "amiga", "raw", "ndk_types.h"));
            Assert.Contains("typedef struct PointerData PointerData;", ndkTypes);
            Assert.DoesNotContain("typedef struct tPoint Point;", ndkTypes);
            Assert.Contains("test.h", Assert.IsType<FfiModuleMetadata>(
                FfiModuleMetadata.TryRead(Path.Combine(output, "std", "amiga", "raw", "test.novus"))).Headers);
            Assert.Contains("movem.l\td2/d3/a6,-(sp)", stub);
            Assert.Contains("movem.l\t16(sp),d0-d1", stub);
            Assert.Contains("movem.l\t24(sp),d2-d3", stub);
            Assert.Contains("jsr\t-30(a6)\n\tmovem.l\t(sp)+,d2/d3/a6", stub);
            Assert.Contains("jsr\t-36(a6)", stub);
            Assert.Contains("lea\t12(sp),a1", stub);
            Assert.Contains("lea\t20(sp),a6\n\tmove.l\ta6,d3", stub);
            Assert.Equal(
                SfdParser.ParseFile(Path.Combine(sfdDirectory, "test_lib.sfd")).Functions.Count,
                Count(binding, "extern pub fn "));
            Assert.Equal(Count(binding, "extern pub fn "), Count(stub, "\txdef\t_"));

            var ciaBindingPath = Path.Combine(output, "std", "amiga", "raw", "resources", "cia.novus");
            var ciaStub = File.ReadAllText(Path.Combine(output, "stubs", "cia_resource_stubs.s"));
            var ciaMetadata = Assert.IsType<FfiModuleMetadata>(FfiModuleMetadata.TryRead(ciaBindingPath));
            Assert.Equal(FfiModuleKind.CallerSupplied, ciaMetadata.Kind);
            Assert.Contains("resource: *Library", File.ReadAllText(ciaBindingPath));
            Assert.Contains("movea.l\t8(sp),a6", ciaStub);
            Assert.DoesNotContain("\txref\t", ciaStub);
            Assert.DoesNotContain("movea.l\tcaller-supplied,a6", ciaStub);
            Assert.DoesNotContain("__novus_cia_resource_name", FfiRuntimeGenerator.Generate([ciaMetadata]));
            var intuitionBinding = File.ReadAllText(Path.Combine(output, "std", "amiga", "raw", "intuition.novus"));
            var intuitionStub = File.ReadAllText(Path.Combine(output, "stubs", "intuition_stubs.s"));
            Assert.Contains("ReportMouse1(window: *Window, flag: i32)", intuitionBinding);
            Assert.Contains("movea.l\t8(sp),a0\n\tmove.l\t12(sp),d0", intuitionStub);
            Assert.Equal(3, Directory.GetFiles(sfdDirectory, "*_lib.sfd").Length);
            Assert.Equal(3, Directory.GetFiles(Path.Combine(output, "stubs"), "*_stubs.s").Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static int Count(string value, string needle) =>
        value.Split(needle, StringSplitOptions.None).Length - 1;
}
