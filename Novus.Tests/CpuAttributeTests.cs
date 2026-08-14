using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for the #[cpu("68XXX")] module-level attribute.
/// This attribute allows source code to override the target CPU,
/// taking precedence over project.toml and command-line settings.
/// </summary>
public class CpuAttributeTests
{
    private IrBuilder BuildModule(string code)
    {
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(
            code,
            diagnostics,
            "test.novus",
            NovusParserFactory.ParseMode.Compilation
        );
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: true);
        builder.BuildModule(tree);
        return builder;
    }

    [Fact]
    public void DefaultCpuOverride_IsNull()
    {
        var code = @"
fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Null(builder.Module.CpuOverride);
    }

    [Theory]
    [InlineData("68000")]
    [InlineData("68010")]
    public void CpuAttribute_RejectsUnsupportedCpu(string cpu)
    {
        var code = $"#[cpu(\"{cpu}\")]\nfn main() -> i32 {{ return 0 }}";
        var builder = BuildModule(code);
        Assert.True(builder.Diagnostics.HasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics,
            d => d.Message.Contains("68020 or newer"));
    }

    [Fact]
    public void CpuAttribute_Sets68020()
    {
        var code = @"
#[cpu(""68020"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal("68020", builder.Module.CpuOverride);
    }

    [Fact]
    public void CpuAttribute_Sets68030()
    {
        var code = @"
#[cpu(""68030"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal("68030", builder.Module.CpuOverride);
    }

    [Fact]
    public void CpuAttribute_Sets68040()
    {
        var code = @"
#[cpu(""68040"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal("68040", builder.Module.CpuOverride);
    }

    [Fact]
    public void CpuAttribute_Sets68060()
    {
        var code = @"
#[cpu(""68060"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal("68060", builder.Module.CpuOverride);
    }

    [Fact]
    public void CpuAttribute_Sets68080_ApolloVampire()
    {
        var code = @"
#[cpu(""68080"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal("68080", builder.Module.CpuOverride);
    }

    [Fact]
    public void CpuAttribute_WithoutArgument_ReportsError()
    {
        var code = @"
#[cpu]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.True(builder.Diagnostics.HasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics,
            d => d.Message.Contains("cpu") && d.Message.Contains("requires"));
    }

    [Fact]
    public void CpuAttribute_WithInvalidCpu_ReportsError()
    {
        var code = @"
#[cpu(""68999"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.True(builder.Diagnostics.HasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics,
            d => d.Message.Contains("Invalid CPU") && d.Message.Contains("68999"));
    }

    [Fact]
    public void CpuAttribute_EmitsWarning()
    {
        var code = @"
#[cpu(""68020"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        // Should emit a warning about overriding project/CLI settings
        Assert.Contains(builder.Diagnostics.Diagnostics,
            d => d.Severity == DiagnosticSeverity.Warning &&
                 d.Message.Contains("overrides") &&
                 d.Message.Contains("68020"));
    }

    [Fact]
    public void MultipleCpuAttributes_LastOneWins()
    {
        var code = @"
#[cpu(""68020"")]
#[cpu(""68060"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal("68060", builder.Module.CpuOverride);
    }

    [Fact]
    public void CpuAttribute_WithStackSize_BothWork()
    {
        var code = @"
#[stack_size(131072)]
#[cpu(""68040"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal(131072, builder.Module.StackSize);
        Assert.Equal("68040", builder.Module.CpuOverride);
    }
}

/// <summary>
/// Tests for IrBuilderConfiguration.GetPreprocessorConstantsForCpu()
/// </summary>
public class CpuPreprocessorConstantsTests
{
    [Fact]
    public void GetPreprocessorConstantsForTarget_IncludesFpuChipsetAndBuildMode()
    {
        var constants = IrBuilderConfiguration.GetPreprocessorConstantsForTarget(
            "68040", "68040", "AGA", debug: false);

        Assert.True(constants["M68040"]);
        Assert.True(constants["FPU_68040"]);
        Assert.True(constants["AGA"]);
        Assert.True(constants["ECS_PLUS"]);
        Assert.True(constants["RELEASE"]);
        Assert.False(constants["DEBUG"]);
    }

    [Theory]
    [InlineData("68000")]
    [InlineData("68010")]
    public void GetPreprocessorConstantsForCpu_RejectsUnsupportedCpu(string cpu)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            IrBuilderConfiguration.GetPreprocessorConstantsForCpu(cpu));
        Assert.Contains("68020 or newer", error.Message);
    }

    [Fact]
    public void GetPreprocessorConstantsForCpu_68020_SetsHierarchy()
    {
        var constants = IrBuilderConfiguration.GetPreprocessorConstantsForCpu("68020");

        Assert.True(constants["M68020"]);
        Assert.True(constants["M68020_PLUS"]);
        Assert.False(constants["M68030_PLUS"]);
    }

    [Fact]
    public void GetPreprocessorConstantsForCpu_68040_SetsHierarchy()
    {
        var constants = IrBuilderConfiguration.GetPreprocessorConstantsForCpu("68040");

        Assert.True(constants["M68040"]);
        Assert.True(constants["M68020_PLUS"]);
        Assert.True(constants["M68030_PLUS"]);
        Assert.True(constants["M68040_PLUS"]);
        Assert.False(constants["M68060_PLUS"]);
    }

    [Fact]
    public void GetPreprocessorConstantsForCpu_68060_SetsAllHierarchy()
    {
        var constants = IrBuilderConfiguration.GetPreprocessorConstantsForCpu("68060");

        Assert.True(constants["M68060"]);
        Assert.True(constants["M68020_PLUS"]);
        Assert.True(constants["M68030_PLUS"]);
        Assert.True(constants["M68040_PLUS"]);
        Assert.True(constants["M68060_PLUS"]);
    }

    [Fact]
    public void GetPreprocessorConstantsForCpu_68080_SetsVampireAndAllHierarchy()
    {
        var constants = IrBuilderConfiguration.GetPreprocessorConstantsForCpu("68080");

        Assert.True(constants["M68080"]);
        Assert.True(constants["M68020_PLUS"]);
        Assert.True(constants["M68030_PLUS"]);
        Assert.True(constants["M68040_PLUS"]);
        Assert.True(constants["M68060_PLUS"]);
    }

    [Fact]
    public void GetPreprocessorConstantsForCpu_Auto_DefaultsTo68020()
    {
        var constants = IrBuilderConfiguration.GetPreprocessorConstantsForCpu("auto");

        Assert.True(constants["M68020"]);
        Assert.True(constants["M68020_PLUS"]);
        Assert.False(constants["M68030_PLUS"]);
    }

    [Fact]
    public void GetPreprocessorConstantsForCpu_Null_DefaultsTo68020()
    {
        var constants = IrBuilderConfiguration.GetPreprocessorConstantsForCpu(null);

        Assert.True(constants["M68020"]);
        Assert.True(constants["M68020_PLUS"]);
    }
}
