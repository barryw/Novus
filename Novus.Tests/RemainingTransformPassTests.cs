using Novus.IR;
using Novus.Transforms;
using Novus.Transforms.Passes;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for remaining transform passes with 0% coverage.
/// Tests AsyncLoweringPass, BlitterLoweringPass, CopperLoweringPass, GenericMonomorphizationPass.
/// Note: HIR classes don't exist yet, so tests focus on skeleton implementation.
/// </summary>
public class RemainingTransformPassTests
{
    [Fact]
    public void AsyncLoweringPass_Name_ReturnsCorrectName()
    {
        var pass = new AsyncLoweringPass();

        Assert.Equal("Async Lowering", pass.Name);
    }

    [Fact]
    public void AsyncLoweringPass_EmptyModule_ReturnsFalse()
    {
        var pass = new AsyncLoweringPass();
        var module = new IrModule();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void AsyncLoweringPass_ModuleWithFunctions_ReturnsNoChanges()
    {
        var pass = new AsyncLoweringPass();
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);
        module.Functions.Add(function);

        var changed = pass.Transform(module);

        // Skeleton implementation returns false
        Assert.False(changed);
    }

    [Fact]
    public void GenericMonomorphizationPass_Name_ReturnsCorrectName()
    {
        var pass = new GenericMonomorphizationPass();

        Assert.Equal("Generic Monomorphization", pass.Name);
    }

    [Fact]
    public void GenericMonomorphizationPass_EmptyModule_ReturnsFalse()
    {
        var pass = new GenericMonomorphizationPass();
        var module = new IrModule();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void GenericMonomorphizationPass_ModuleWithFunctions_ReturnsNoChanges()
    {
        var pass = new GenericMonomorphizationPass();
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);
        module.Functions.Add(function);

        var changed = pass.Transform(module);

        // Skeleton implementation returns false
        Assert.False(changed);
    }

    [Fact]
    public void BlitterLoweringPass_Name_ReturnsCorrectName()
    {
        var pass = new BlitterLoweringPass();

        Assert.Equal("Blitter Lowering", pass.Name);
    }

    [Fact]
    public void BlitterLoweringPass_EmptyModule_ReturnsFalse()
    {
        var pass = new BlitterLoweringPass();
        var module = new IrModule();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void BlitterLoweringPass_ModuleWithoutHirInstructions_ReturnsNoChanges()
    {
        var pass = new BlitterLoweringPass();
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);
        module.Functions.Add(function);

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    // Note: HIR classes (HirBlitterJob, HirCopperList) don't exist yet
    // These are forward references in the lowering passes
    // Tests focus on basic pass functionality

    [Fact]
    public void CopperLoweringPass_Name_ReturnsCorrectName()
    {
        var pass = new CopperLoweringPass();

        Assert.Equal("Copper Lowering", pass.Name);
    }

    [Fact]
    public void CopperLoweringPass_EmptyModule_ReturnsFalse()
    {
        var pass = new CopperLoweringPass();
        var module = new IrModule();

        var changed = pass.Transform(module);

        Assert.False(changed);
    }

    [Fact]
    public void CopperLoweringPass_ModuleWithoutHirInstructions_ReturnsNoChanges()
    {
        var pass = new CopperLoweringPass();
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);
        module.Functions.Add(function);

        var changed = pass.Transform(module);

        Assert.False(changed);
    }


    [Fact]
    public void AllTransformPasses_ImplementIIrTransformPass()
    {
        // Verify all passes implement the interface
        var asyncPass = new AsyncLoweringPass();
        var blitterPass = new BlitterLoweringPass();
        var copperPass = new CopperLoweringPass();
        var genericPass = new GenericMonomorphizationPass();

        Assert.IsAssignableFrom<IIrTransformPass>(asyncPass);
        Assert.IsAssignableFrom<IIrTransformPass>(blitterPass);
        Assert.IsAssignableFrom<IIrTransformPass>(copperPass);
        Assert.IsAssignableFrom<IIrTransformPass>(genericPass);
    }

    [Fact]
    public void AllTransformPasses_HaveDistinctNames()
    {
        var passes = new IIrTransformPass[]
        {
            new AsyncLoweringPass(),
            new BlitterLoweringPass(),
            new CopperLoweringPass(),
            new GenericMonomorphizationPass(),
            new InlineExpansionPass()
        };

        var names = passes.Select(p => p.Name).ToHashSet();

        // All names should be distinct
        Assert.Equal(passes.Length, names.Count);
    }
}
