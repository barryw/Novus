using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for runtime library integration (novus_io.c, startup code, etc.)
/// </summary>
public class RuntimeLibraryTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: false);
        return builder.BuildModule(tree);
    }

    [Fact]
    public void WriteFunction_IsAvailableInStdIo()
    {
        var source = @"
from std::io::file import write
from std::strings::core import Str

pub fn main() -> i32 {
    let msg: Str = ""Hello\n""
    write(msg.ptr, 0)
    return 0
}";
        var module = BuildIr(source);

        // Verify write function exists and is variadic
        var writeFunc = module.Functions.FirstOrDefault(f => f.Name == "write");
        Assert.NotNull(writeFunc);
        Assert.True(writeFunc.IsVariadic);
        Assert.True(writeFunc.IsExtern);
    }

    [Fact]
    public void WriteFunction_WithFormatSpecifiers_Compiles()
    {
        var source = @"
from std::io::file import write
from std::strings::core import Str

pub fn main() -> i32 {
    let msg: Str = ""Value: %ld\n""
    write(msg.ptr, 42)
    return 0
}";
        var module = BuildIr(source);

        // Verify module compiles without errors
        Assert.NotNull(module);
        var main = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void WriteFunction_WithMultipleArgs_Compiles()
    {
        var source = @"
from std::io::file import write
from std::strings::core import Str

pub fn main() -> i32 {
    let msg: Str = ""Values: %ld, %ld, %ld\n""
    write(msg.ptr, 1, 2, 3)
    return 0
}";
        var module = BuildIr(source);

        // Verify module compiles without errors
        Assert.NotNull(module);
        var main = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void LibraryVersion_StructLiteral_Compiles()
    {
        var source = @"
from std::core import LibraryVersion

pub fn main() -> i32 {
    let maj: u16 = 1
    let min: u16 = 0
    let pat: u16 = 0
    let version: LibraryVersion = LibraryVersion { major: maj, minor: min, patch: pat }
    return 0
}";
        var module = BuildIr(source);

        // Verify module compiles with struct literal
        Assert.NotNull(module);
        var main = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void LibraryVersion_AccessorMethods_Compile()
    {
        var source = @"
from std::core import LibraryVersion

pub fn main() -> i32 {
    let maj: u16 = 1
    let min: u16 = 2
    let pat: u16 = 3
    let version: LibraryVersion = LibraryVersion { major: maj, minor: min, patch: pat }
    let m1: u16 = version.major()
    let m2: u16 = version.minor()
    let m3: u16 = version.patch()
    return 0
}";
        var module = BuildIr(source);

        // Verify module compiles successfully (accessors are called)
        Assert.NotNull(module);
        var main = module.Functions.FirstOrDefault(f => f.Name == "main");
        Assert.NotNull(main);
    }

    [Fact]
    public void WriteWithLibraryVersion_Compiles()
    {
        var source = @"
from std::core import LibraryVersion
from std::io::file import write
from std::strings::core import Str

pub fn main() -> i32 {
    let maj: u16 = 1
    let min: u16 = 0
    let pat: u16 = 0
    let version: LibraryVersion = LibraryVersion { major: maj, minor: min, patch: pat }
    let msg: Str = ""Version: %ld.%ld.%ld\n""
    write(msg.ptr, version.major(), version.minor(), version.patch())
    return 0
}";
        var module = BuildIr(source);

        // Verify module compiles correctly with both imports
        Assert.NotNull(module);
        var writeFunc = module.Functions.FirstOrDefault(f => f.Name == "write");
        Assert.NotNull(writeFunc);
        Assert.True(writeFunc.IsVariadic);
    }

    [Fact]
    public void VariadicFunction_IsMarkedCorrectly()
    {
        var source = @"
from std::io::file import write

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        // Find the write function in imports
        var writeFunc = module.Functions.FirstOrDefault(f => f.Name == "write");
        Assert.NotNull(writeFunc);
        Assert.True(writeFunc.IsVariadic, "write() should be marked as variadic");
        Assert.True(writeFunc.IsExtern, "write() should be extern");
    }

    [Theory]
    [InlineData("novus_io.s")]
    [InlineData("runtime_mem.s")]
    [InlineData("runtime_core.c")]
    [InlineData("runtime_compare.c")]
    [InlineData("runtime_errors.c")]
    [InlineData("runtime_library_error.s")]
    [InlineData("runtime_mmu.c")]
    public void RuntimeFile_IsCopiedToBuildOutput(string fileName)
    {
        var runtimeFile = Path.Combine(
            Path.GetDirectoryName(typeof(RuntimeLibraryTests).Assembly.Location)!,
            "runtime",
            fileName
        );

        Assert.True(File.Exists(runtimeFile),
            $"{fileName} should be copied to the output directory by MSBuild; not found at {runtimeFile}");
    }

    [Fact]
    public void RuntimeStartup_DoesNotMutateTheSystemMmuContext()
    {
        var runtimeFile = Path.Combine(
            Path.GetDirectoryName(typeof(RuntimeLibraryTests).Assembly.Location)!,
            "runtime",
            "runtime_mmu.c"
        );
        var content = File.ReadAllText(runtimeFile);
        var start = content.IndexOf("void __novus_init_mmu_protection(void)", StringComparison.Ordinal);
        var end = content.IndexOf("void __novus_cleanup_mmu_protection(void)", start, StringComparison.Ordinal);

        Assert.DoesNotContain("__novus_enable_null_page_protection();", content[start..end]);
    }

    [Fact]
    public void RuntimeFailures_EmitAMachineReadableMarker()
    {
        var runtimeFile = Path.Combine(
            Path.GetDirectoryName(typeof(RuntimeLibraryTests).Assembly.Location)!,
            "runtime",
            "runtime_core.c"
        );

        Assert.Contains("NOVUS_RUNTIME_ERROR\\n", File.ReadAllText(runtimeFile));
    }

    [Fact]
    public void ReleaseRuntime_FunctionsHaveIndependentLinkerSections()
    {
        var runtimeDir = Path.Combine(
            Path.GetDirectoryName(typeof(RuntimeLibraryTests).Assembly.Location)!,
            "runtime"
        );

        Assert.Contains("\tsection\t__novus_memcpy,code",
            File.ReadAllText(Path.Combine(runtimeDir, "runtime_mem.s")));
        var libraryReporter = File.ReadAllText(Path.Combine(runtimeDir, "runtime_library_error.s"));
        Assert.Contains("\tsection\t__novus_library_not_found,code", libraryReporter);
        Assert.DoesNotContain("_IntuitionBase", libraryReporter);
        Assert.Contains("'NOVUS_RUNTIME_ERROR',10,'Library: '", libraryReporter);
        Assert.Contains("%ld+", libraryReporter);
        Assert.Contains("LIBS:", libraryReporter);
        Assert.Equal(2, libraryReporter.Split("jsr\t-48(a6)", StringSplitOptions.None).Length - 1);
        Assert.Contains("NOVUS_RUNTIME_SECTION(__novus_panic)",
            File.ReadAllText(Path.Combine(runtimeDir, "runtime_errors.c")));
    }

    [Fact]
    public void VbccRuntime_UsesInlineAmigaLibraryVectors()
    {
        var runtimeHeader = Path.Combine(
            Path.GetDirectoryName(typeof(RuntimeLibraryTests).Assembly.Location)!,
            "runtime",
            "novus_runtime.h"
        );
        var content = File.ReadAllText(runtimeHeader);

        Assert.Contains("#include <inline/exec_protos.h>", content);
        Assert.Contains("#include <inline/dos_protos.h>", content);
        Assert.Contains("#include <inline/intuition_protos.h>", content);
    }

    [Fact]
    public void StartupStub_UsesExactFfiLifecycleForDOS()
    {
        // Verify novus_startup.s exists and contains DOS initialization
        var startupFile = Path.Combine(
            Path.GetDirectoryName(typeof(RuntimeLibraryTests).Assembly.Location)!,
            "stubs",
            "novus_startup.s"
        );

        if (File.Exists(startupFile))
        {
            var content = File.ReadAllText(startupFile);
            Assert.Contains("___novus_ffi_init", content);
            Assert.DoesNotContain("___dos_init", content);
            Assert.DoesNotContain("___dos_cleanup", content);
        }
    }

    [Fact]
    public void StartupStub_DelegatesExactLibraryCleanupToFfiLifecycle()
    {
        var startupFile = Path.Combine(
            Path.GetDirectoryName(typeof(RuntimeLibraryTests).Assembly.Location)!,
            "stubs",
            "novus_startup.s"
        );
        var content = File.ReadAllText(startupFile);

        Assert.Contains("jsr\t___novus_ffi_cleanup", content);
        Assert.DoesNotContain("movea.l\t_GadToolsBase,a1", content);
        Assert.DoesNotContain("movea.l\t_IntuitionBase,a1", content);
    }
}
