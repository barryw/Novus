using Novus.Compilation;
using Novus.Diagnostics;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for compilation phase infrastructure.
/// </summary>
public class CompilationPhaseTests
{
    [Fact]
    public void CompilationContext_InitializesCorrectly()
    {
        var options = new CompilerOptions
        {
            InputFile = "test.novus",
            OutputFile = "test",
            Cpu = "68020",
            Fpu = "soft",
            BuildMode = BuildMode.Debug
        };

        var context = new CompilationContext
        {
            Options = options,
            CompilerDir = "/test/compiler",
            StdLibPath = "/test/std",
            OutputDir = "/test/output",
            BaseName = "test",
            SafetyLevel = SafetyLevel.Full
        };

        Assert.Equal("68020", context.AssemblyCpu);
        Assert.Equal("debug", context.BuildModeStr);
        Assert.False(context.IsLibrary);
        Assert.False(context.IsDevice);
        Assert.NotNull(context.CFiles);
        Assert.NotNull(context.ObjectFiles);
        Assert.NotNull(context.AllModulesIR);
        Assert.NotNull(context.AllCodeGenerators);
        Assert.NotNull(context.ModuleCache);
        Assert.NotNull(context.Diagnostics);
    }

    [Fact]
    public void CompilationContext_AutoCpu_ResolvesTo68020()
    {
        var options = new CompilerOptions
        {
            InputFile = "test.novus",
            OutputFile = "test",
            Cpu = "auto",
            Fpu = "soft",
            BuildMode = BuildMode.Debug
        };

        var context = new CompilationContext
        {
            Options = options,
            CompilerDir = "/test/compiler",
            StdLibPath = "/test/std",
            OutputDir = "/test/output",
            BaseName = "test",
            SafetyLevel = SafetyLevel.Full
        };

        Assert.Equal("68020", context.AssemblyCpu);
    }

    [Fact]
    public void CompilationContext_ReleaseMode_BuildModeStrIsRelease()
    {
        var options = new CompilerOptions
        {
            InputFile = "test.novus",
            OutputFile = "test",
            Cpu = "68020",
            Fpu = "soft",
            BuildMode = BuildMode.Release
        };

        var context = new CompilationContext
        {
            Options = options,
            CompilerDir = "/test/compiler",
            StdLibPath = "/test/std",
            OutputDir = "/test/output",
            BaseName = "test",
            SafetyLevel = SafetyLevel.Basic
        };

        Assert.Equal("release", context.BuildModeStr);
    }

    [Fact]
    public void CompilationContext_LibraryProjectType_IsLibraryTrue()
    {
        var options = new CompilerOptions
        {
            InputFile = "test.novus",
            OutputFile = "test.library",
            Cpu = "68020",
            Fpu = "soft",
            BuildMode = BuildMode.Debug,
            ProjectType = "library"
        };

        var context = new CompilationContext
        {
            Options = options,
            CompilerDir = "/test/compiler",
            StdLibPath = "/test/std",
            OutputDir = "/test/output",
            BaseName = "test",
            SafetyLevel = SafetyLevel.Full
        };

        Assert.True(context.IsLibrary);
        Assert.False(context.IsDevice);
    }

    [Fact]
    public void CompilationContext_DeviceProjectType_IsDeviceTrue()
    {
        var options = new CompilerOptions
        {
            InputFile = "test.novus",
            OutputFile = "test.device",
            Cpu = "68020",
            Fpu = "soft",
            BuildMode = BuildMode.Debug,
            ProjectType = "device"
        };

        var context = new CompilationContext
        {
            Options = options,
            CompilerDir = "/test/compiler",
            StdLibPath = "/test/std",
            OutputDir = "/test/output",
            BaseName = "test",
            SafetyLevel = SafetyLevel.Full
        };

        Assert.False(context.IsLibrary);
        Assert.True(context.IsDevice);
    }

    [Fact]
    public void LoweringPhase_HasCorrectName()
    {
        var phase = new LoweringPhase();
        Assert.Equal("HIR Lowering", phase.Name);
    }

    [Fact]
    public async Task LoweringPhase_NullMainModule_ReturnsTrue()
    {
        var options = new CompilerOptions
        {
            InputFile = "test.novus",
            OutputFile = "test",
            Cpu = "68020",
            Fpu = "soft",
            BuildMode = BuildMode.Debug
        };

        var context = new CompilationContext
        {
            Options = options,
            CompilerDir = "/test/compiler",
            StdLibPath = "/test/std",
            OutputDir = "/test/output",
            BaseName = "test",
            SafetyLevel = SafetyLevel.Full
        };

        // MainModuleIR is null by default
        Assert.Null(context.MainModuleIR);

        var phase = new LoweringPhase();
        var result = await phase.ExecuteAsync(context);

        // Should succeed even with no modules
        Assert.True(result);
    }

    [Fact]
    public void ModuleIR_Record_InitializesCorrectly()
    {
        var irModule = new Novus.IR.IrModule();
        var stringLiterals = new List<Novus.IR.IrStringLiteral>();
        var imports = new List<string> { "std::io" };

        var moduleIR = new ModuleIR(
            ModulePath: "/test/test.novus",
            ModuleName: "test",
            IrModule: irModule,
            StringLiterals: stringLiterals,
            ImportedModules: imports,
            HasMain: true);

        Assert.Equal("/test/test.novus", moduleIR.ModulePath);
        Assert.Equal("test", moduleIR.ModuleName);
        Assert.Same(irModule, moduleIR.IrModule);
        Assert.Same(stringLiterals, moduleIR.StringLiterals);
        Assert.Same(imports, moduleIR.ImportedModules);
        Assert.True(moduleIR.HasMain);
    }
}
