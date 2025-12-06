using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for the #[stack_size(N)] module-level attribute.
/// </summary>
public class StackSizeAttributeTests
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
    public void DefaultStackSize_Is64KB()
    {
        var code = @"
fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal(65536, builder.Module.StackSize);
    }

    [Fact]
    public void StackSizeAttribute_SetsCustomSize()
    {
        var code = @"
#[stack_size(131072)]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal(131072, builder.Module.StackSize);
    }

    [Fact]
    public void StackSizeAttribute_AcceptsSmallSize()
    {
        var code = @"
#[stack_size(8192)]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal(8192, builder.Module.StackSize);
    }

    [Fact]
    public void StackSizeAttribute_AcceptsLargeSize()
    {
        var code = @"
#[stack_size(1048576)]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal(1048576, builder.Module.StackSize); // 1MB
    }

    [Fact]
    public void StackSizeAttribute_WithoutArgument_ReportsError()
    {
        var code = @"
#[stack_size]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.True(builder.Diagnostics.HasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics,
            d => d.Message.Contains("stack_size") && d.Message.Contains("requires"));
    }

    [Fact]
    public void StackSizeAttribute_WithInvalidArgument_ReportsError()
    {
        var code = @"
#[stack_size(""not_a_number"")]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.True(builder.Diagnostics.HasErrors);
        Assert.Contains(builder.Diagnostics.Diagnostics,
            d => d.Message.Contains("stack_size") && d.Message.Contains("integer"));
    }

    [Fact]
    public void StackSizeAttribute_CanAppearBeforeImports()
    {
        // Module attributes must come after imports per grammar, but this tests
        // that stack_size is recognized when it appears at module level
        var code = @"
#[stack_size(32768)]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        Assert.Equal(32768, builder.Module.StackSize);
    }

    [Fact]
    public void MultipleModuleAttributes_OnlyLastStackSizeApplies()
    {
        // If multiple stack_size attributes are present, only the last one applies
        var code = @"
#[stack_size(8192)]
#[stack_size(131072)]

fn main() -> i32 {
    return 0
}
";
        var builder = BuildModule(code);
        Assert.False(builder.Diagnostics.HasErrors);
        // Last one wins
        Assert.Equal(131072, builder.Module.StackSize);
    }
}
