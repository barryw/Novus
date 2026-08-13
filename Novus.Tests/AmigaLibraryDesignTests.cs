using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class AmigaLibraryDesignTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string StdLibPath = Path.Combine(ProjectRoot, "Novus", "std");

    [Fact]
    public void CanonicalImports_PreserveGenericTraitImplementations()
    {
        const string source = """
            from std::collections::arrayvec import ArrayVec
            from amiga::storage import BlockDevice

            fn main() -> i32 {
                let values = ArrayVec::<u32, 2>::new()
                for value in &values { return value }
                return 0
            }
            """;
        var lexer = new NovusLexer(new AntlrInputStream(source));
        var parser = new NovusParser(new AngleBracketTokenStream(lexer));
        var builder = new IrBuilder(skipAutoImports: false);
        builder.SetStdLibPath(StdLibPath);
        builder.SetInputFilePath("test.novus");

        var module = builder.BuildModule(parser.compilationUnit());

        Assert.NotNull(module.GetTrait("Iterable"));
        Assert.Contains(module.TraitImpls,
            implementation => implementation.TraitName == "Iterable<T>" && implementation.TypeName == "ArrayVec");
        Assert.False(builder.Diagnostics.HasErrors, builder.Diagnostics.FormatDiagnostics());
    }

    [Fact]
    public void ImportedFunctionAliases_LowerToOriginalFunction()
    {
        const string source = """
            from std::string import Str
            from amiga::sys::intuition::dialog import alert as system_alert

            fn alert(title: Str, message: Str) { system_alert(title.as_cstr(), message.as_cstr()) }
            """;
        var parser = NovusParserFactory.CreateParser(source, new Novus.Diagnostics.DiagnosticBag(),
            "test.novus", NovusParserFactory.ParseMode.Compilation);
        var analyzer = new SemanticAnalyzer("test.novus", source, StdLibPath);
        analyzer.Analyze(parser.compilationUnit());
        Assert.False(analyzer.Diagnostics.HasErrors, analyzer.Diagnostics.FormatDiagnostics());

        parser = NovusParserFactory.CreateParser(source, new Novus.Diagnostics.DiagnosticBag(),
            "test.novus", NovusParserFactory.ParseMode.Compilation);
        var builder = new IrBuilder(analyzer.GetResult());
        builder.SetStdLibPath(StdLibPath);
        builder.SetInputFilePath("test.novus");

        var module = builder.BuildModule(parser.compilationUnit());

        Assert.False(builder.Diagnostics.HasErrors, builder.Diagnostics.FormatDiagnostics());
        Assert.Contains(module.Functions.SelectMany(function => function.BasicBlocks)
                .SelectMany(block => block.Instructions).OfType<IrCall>(),
            call => call.FunctionName == "system_alert");
        Assert.Contains(module.Functions,
            function => function.Name == "system_alert" && function.LinkName == "alert");
        Assert.Contains(module.Functions,
            function => function.OriginalName == "alert" && function.LinkName == null && function.BasicBlocks.Count > 0);
    }

    [Fact]
    public void GenericMethodArguments_KeepContextualIntegerInference()
    {
        const string source = """
            from std::core import Option

            struct Box<T> { value: T }
            impl<T> Box<T> {
                fn select(&self, selected: Option<u16>) {}
            }
            fn choose(box: Box<i32>) { box.select(Option::Some(1)) }
            """;
        var parser = NovusParserFactory.CreateParser(source, new Novus.Diagnostics.DiagnosticBag(),
            "test.novus", NovusParserFactory.ParseMode.Compilation);
        var builder = new IrBuilder(skipAutoImports: false);
        builder.SetStdLibPath(StdLibPath);
        builder.SetInputFilePath("test.novus");

        var module = builder.BuildModule(parser.compilationUnit());

        Assert.False(builder.Diagnostics.HasErrors, builder.Diagnostics.FormatDiagnostics());
        var call = module.GetFunction("choose")!.BasicBlocks
            .SelectMany(block => block.Instructions).OfType<IrCall>()
            .Single(instruction => instruction.FunctionName.Contains("select", StringComparison.Ordinal));
        var option = Assert.IsType<IrEnumValue>(call.Arguments[1]);
        Assert.Equal("Option<u16>", Assert.IsType<IrEnumType>(option.Type).CacheKey);
    }

    [Fact]
    public async Task CanonicalApplicationApis_CompileWithoutRawTypes()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"novus-amiga-api-{Guid.NewGuid():N}.novus");
        await File.WriteAllTextAsync(sourcePath, """
            from std::core import Option, Result
            from std::memory import Buffer, MemoryError
            from std::string import Str
            from amiga::dos import DosError, File, FileSystem, FileSystemError
            from amiga::storage import BlockDevice, devices
            from amiga::sys::device import DeviceRequest
            from amiga::sys::dos import OwnedFileHandle
            from amiga::sys::intuition import WindowRef
            from amiga::ui import Window

            fn file_size(path: Str) -> Result<u32, DosError> {
                let file = File::open(path)?
                return file.len()
            }

            fn storage_is_available() -> bool { return devices().is_ok() }

            fn allocate(size: u32) -> Result<Buffer, MemoryError> { return Buffer::new(size) }

            fn resolve_file_system(dos_type: u32) -> Result<FileSystem, FileSystemError> {
                return FileSystem::resolve(dos_type, Option::None)
            }

            fn show_problem(window: &Window) {
                window.alert("Problem", "Something went wrong")
            }

            fn block_system(device: &BlockDevice) -> &DeviceRequest { return device.system() }
            fn window_system(window: &Window) -> WindowRef { return window.system() }
            fn file_system(consuming file: File) -> OwnedFileHandle { return file.into_system() }
            fn adopt_file(consuming file: OwnedFileHandle) -> File { return File::from_system(file) }
            """);
        try
        {
            var result = await new InProcessCompiler(StdLibPath).CompileToCAsync(sourcePath);
            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Theory]
    [InlineData("""
        from amiga::ui import Window
        from amiga::sys::intuition import WindowRef
        fn escape(consuming window: Window) -> WindowRef { return window.system() }
        """)]
    [InlineData("""
        from std::core import Option
        from amiga::sys::dos import DosDeviceEntry, DosDeviceList
        fn escape(consuming list: DosDeviceList) -> Option<DosDeviceEntry> {
            var entries = list.iter()
            return entries.next()
        }
        """)]
    public async Task OwnerTiedAmigaViews_CannotEscapeConsumedOwners(string source)
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"novus-amiga-owner-{Guid.NewGuid():N}.novus");
        await File.WriteAllTextAsync(sourcePath, source);
        try
        {
            var result = await new InProcessCompiler(StdLibPath).CompileToCAsync(sourcePath);
            Assert.False(result.Success);
            Assert.Contains("E0106", result.ErrorMessage, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void HdPartSources_UseCanonicalLibraryLayers()
    {
        var root = PathUtility.FindProjectRoot()
            ?? throw new InvalidOperationException("Novus project root not found");
        var sourceRoot = Path.Combine(root, "ports", "hdpart-novus", "src");
        var forbidden = new[] { "from std::os", "from std::ui", "from std::ffi", "from std::strings", "from amiga::raw" };

        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.novus"))
        {
            var source = File.ReadAllText(path);
            foreach (var import in forbidden)
                Assert.DoesNotContain(import, source, StringComparison.Ordinal);
            foreach (var portable in new[] { "from std::collections::", "from std::memory::", "from std::string::", "from std::io::" })
                Assert.DoesNotContain(portable, source, StringComparison.Ordinal);
        }

        var application = File.ReadAllText(Path.Combine(sourceRoot, "application.novus"));
        Assert.DoesNotContain("from amiga::sys", application, StringComparison.Ordinal);
    }

    [Fact]
    public void AmigaLibrary_HasNoLegacyImplementationTreesOrUpwardDependencies()
    {
        var stdRoot = StdLibPath;
        foreach (var legacy in new[] { "ffi", "os", "ui", "graphics", "hardware", "audio", "args", "prefs", "strings", "ipc", "thread", "sync" })
        {
            var path = Path.Combine(stdRoot, legacy);
            Assert.False(Directory.Exists(path), $"Legacy stdlib tree still exists: std::{legacy}");
        }

        var amigaRoot = Path.Combine(stdRoot, "amiga");
        var violations = new List<string>();
        foreach (var path in Directory.EnumerateFiles(amigaRoot, "*.novus", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(amigaRoot, path);
            var source = File.ReadAllText(path);
            var imports = source.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("from amiga::", StringComparison.Ordinal) ||
                               line.StartsWith("pub use amiga::", StringComparison.Ordinal));

            if (relative.StartsWith("raw" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                violations.AddRange(imports
                    .Where(line => !line.Contains("amiga::raw::", StringComparison.Ordinal))
                    .Select(line => $"Raw module imports upward: {relative}: {line}"));
            }
            else if (relative.StartsWith("sys" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                violations.AddRange(imports
                    .Where(line => !line.Contains("amiga::sys::", StringComparison.Ordinal) &&
                                   !line.Contains("amiga::raw::", StringComparison.Ordinal))
                    .Select(line => $"Systems module imports upward: {relative}: {line}"));
            }
            else if (source.Contains("from amiga::raw", StringComparison.Ordinal) ||
                     source.Contains("pub use amiga::raw", StringComparison.Ordinal))
            {
                violations.Add($"Application module imports raw NDK: {relative}");
            }
        }
        Assert.Empty(violations);

        Assert.False(File.Exists(Path.Combine(amigaRoot, "sys", "errors.novus")));
    }

    [Fact]
    public async Task CanonicalSpecialistSurfaces_Compile()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"novus-amiga-specialists-{Guid.NewGuid():N}.novus");
        await File.WriteAllTextAsync(sourcePath, """
            from amiga::audio import AudioError, Device, Sample
            from amiga::graphics import DrawMode, Point
            from amiga::input import GadToolsEvent
            from amiga::ui import Bounds, Event, UiError, WindowBuilder
            from amiga::workbench import Prefs
            from amiga::sys::resources import Resource

            fn resource_handle(resource: &Resource) -> *u8 { return resource.as_raw() }
            fn audio_device(consuming device: Device) -> Device { return device }
            fn audio_sample(consuming sample: Sample) -> Sample { return sample }
            fn bounds() -> Bounds { return Bounds { x: 1, y: 2, width: 3, height: 4 } }
            fn ui_error() -> UiError { return UiError::WindowFailed }
            fn event() -> Event { return Event::Close }
            fn builder() -> Result<WindowBuilder, UiError> { return Result::Ok(WindowBuilder::workbench()?) }
            """);
        try
        {
            var result = await new InProcessCompiler(StdLibPath).CompileToCAsync(sourcePath);
            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void HdPartFacadeAliases_PreserveImportedEnumVariants()
    {
        var root = PathUtility.FindProjectRoot()
            ?? throw new InvalidOperationException("Novus project root not found");
        var path = Path.Combine(root, "ports", "hdpart-novus", "src", "application.novus");
        var stdlib = Path.Combine(root, "Novus", "std");
        var source = File.ReadAllText(path);
        var parser = NovusParserFactory.CreateParser(source, new Novus.Diagnostics.DiagnosticBag(), path,
            NovusParserFactory.ParseMode.Compilation);
        var unit = parser.compilationUnit();
        var constants = new Dictionary<string, bool>
        {
            ["DEBUG"] = false, ["RELEASE"] = true, ["M68020"] = true,
            ["M68020_PLUS"] = true, ["FPU_SOFT"] = true, ["FPU_NONE"] = true,
        };
        var analyzer = new SemanticAnalyzer(path, source, stdlib, constants);

        Assert.True(analyzer.Analyze(unit), analyzer.Diagnostics.FormatDiagnostics());
        var result = analyzer.GetResult();
        Assert.True(result.Enums.TryGetValue("Result", out var resultType));
        Assert.Contains(resultType!.Variants, variant => variant.Name == "Ok");
        Assert.Contains(resultType.Variants, variant => variant.Name == "Err");

        var builder = new IrBuilder(result);
        builder.SetStdLibPath(stdlib);
        builder.SetInputFilePath(path);
        builder.BuildModule(unit);
        Assert.Contains(resultType.Variants, variant => variant.Name == "Ok");
        Assert.False(builder.Diagnostics.HasErrors, builder.Diagnostics.FormatDiagnostics());
    }
}
