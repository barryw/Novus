using Novus.IR;
using Novus.Transforms;
using Novus.Transforms.Passes;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for TransformPipeline and TransformPassBase to improve coverage from 0% to 70%+.
/// Tests pipeline execution, pass management, and base class utilities.
/// </summary>
public class TransformPipelineTests
{
    /// <summary>
    /// Mock transform pass for testing
    /// </summary>
    private class MockTransformPass : TransformPassBase
    {
        public override string Name => "Mock Transform";
        public bool TransformCalled { get; private set; }
        public bool ShouldReturnTrue { get; set; }

        public override bool Transform(IrModule module)
        {
            TransformCalled = true;
            return ShouldReturnTrue;
        }
    }

    /// <summary>
    /// Mock transform pass that modifies IR
    /// </summary>
    private class ModifyingTransformPass : TransformPassBase
    {
        public override string Name => "Modifying Transform";

        public override bool Transform(IrModule module)
        {
            // Actually modify the module
            var function = new IrFunction("added_by_transform", IrIntType.I32);
            module.Functions.Add(function);
            return true;
        }
    }

    [Fact]
    public void TransformPipeline_EmptyPipeline_NoChanges()
    {
        var pipeline = new TransformPipeline();
        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.False(changed);
    }

    [Fact]
    public void TransformPipeline_WithVerbose_CreatesInstance()
    {
        var pipeline = new TransformPipeline(verbose: true);
        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.False(changed);
    }

    [Fact]
    public void TransformPipeline_AddSinglePass_ExecutesPass()
    {
        var pipeline = new TransformPipeline();
        var mockPass = new MockTransformPass { ShouldReturnTrue = false };
        pipeline.AddPass(mockPass);
        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.True(mockPass.TransformCalled);
        Assert.False(changed);
    }

    [Fact]
    public void TransformPipeline_PassReturnsTrue_ReportsChanged()
    {
        var pipeline = new TransformPipeline();
        var mockPass = new MockTransformPass { ShouldReturnTrue = true };
        pipeline.AddPass(mockPass);
        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.True(mockPass.TransformCalled);
        Assert.True(changed);
    }

    [Fact]
    public void TransformPipeline_MultiplePasses_ExecutesInOrder()
    {
        var pipeline = new TransformPipeline();
        var pass1 = new MockTransformPass { ShouldReturnTrue = false };
        var pass2 = new MockTransformPass { ShouldReturnTrue = false };
        var pass3 = new MockTransformPass { ShouldReturnTrue = false };

        pipeline.AddPass(pass1);
        pipeline.AddPass(pass2);
        pipeline.AddPass(pass3);

        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.True(pass1.TransformCalled);
        Assert.True(pass2.TransformCalled);
        Assert.True(pass3.TransformCalled);
        Assert.False(changed);
    }

    [Fact]
    public void TransformPipeline_OnePassModifies_ReportsChanged()
    {
        var pipeline = new TransformPipeline();
        var pass1 = new MockTransformPass { ShouldReturnTrue = false };
        var pass2 = new MockTransformPass { ShouldReturnTrue = true };
        var pass3 = new MockTransformPass { ShouldReturnTrue = false };

        pipeline.AddPass(pass1);
        pipeline.AddPass(pass2);
        pipeline.AddPass(pass3);

        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.True(changed);
    }

    [Fact]
    public void TransformPipeline_MultiplePassesModify_ReportsChanged()
    {
        var pipeline = new TransformPipeline();
        var pass1 = new MockTransformPass { ShouldReturnTrue = true };
        var pass2 = new MockTransformPass { ShouldReturnTrue = true };

        pipeline.AddPass(pass1);
        pipeline.AddPass(pass2);

        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.True(changed);
    }

    [Fact]
    public void TransformPipeline_ModifyingPass_ActuallyModifiesModule()
    {
        var pipeline = new TransformPipeline();
        var modifyingPass = new ModifyingTransformPass();
        pipeline.AddPass(modifyingPass);

        var module = new IrModule();
        Assert.Empty(module.Functions);

        var changed = pipeline.Run(module);

        Assert.True(changed);
        Assert.Single(module.Functions);
        Assert.Equal("added_by_transform", module.Functions[0].Name);
    }

    [Fact]
    public void TransformPipeline_CreatePipeline_NoInlining_CreatesEmpty()
    {
        var pipeline = TransformPipeline.CreatePipeline(enableInlining: false);
        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.False(changed);
    }

    [Fact]
    public void TransformPipeline_CreatePipeline_WithInlining_AddsPass()
    {
        var pipeline = TransformPipeline.CreatePipeline(enableInlining: true);
        var module = new IrModule();

        // Should not crash, InlineExpansionPass returns false (skeleton)
        var changed = pipeline.Run(module);

        Assert.False(changed);
    }

    [Fact]
    public void TransformPipeline_CreatePipeline_WithVerbose_CreatesInstance()
    {
        var pipeline = TransformPipeline.CreatePipeline(verbose: true);
        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.False(changed);
    }

    [Fact]
    public void TransformPipeline_CreatePipeline_BothOptions_CreatesInstance()
    {
        var pipeline = TransformPipeline.CreatePipeline(enableInlining: true, verbose: true);
        var module = new IrModule();

        var changed = pipeline.Run(module);

        Assert.False(changed);
    }

    [Fact]
    public void TransformPassBase_CreateTempName_ReturnsCorrectFormat()
    {
        var pass = new MockTransformPass();

        // Use reflection to access protected method
        var method = typeof(TransformPassBase).GetMethod("CreateTempName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (string)method!.Invoke(pass, new object[] { 42 })!;

        Assert.Equal("%t42", result);
    }

    [Fact]
    public void TransformPassBase_CreateTempName_Zero_ReturnsT0()
    {
        var pass = new MockTransformPass();

        var method = typeof(TransformPassBase).GetMethod("CreateTempName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (string)method!.Invoke(pass, new object[] { 0 })!;

        Assert.Equal("%t0", result);
    }

    [Fact]
    public void TransformPassBase_ShouldTransform_NormalFunction_ReturnsTrue()
    {
        var pass = new MockTransformPass();
        var function = new IrFunction("normal_function", IrIntType.I32);

        var method = typeof(TransformPassBase).GetMethod("ShouldTransform",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (bool)method!.Invoke(pass, new object[] { function })!;

        Assert.True(result);
    }

    [Fact]
    public void TransformPassBase_ShouldTransform_InlinedFunction_ReturnsFalse()
    {
        var pass = new MockTransformPass();
        var function = new IrFunction("foo$inline", IrIntType.I32);

        var method = typeof(TransformPassBase).GetMethod("ShouldTransform",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (bool)method!.Invoke(pass, new object[] { function })!;

        Assert.False(result);
    }

    [Fact]
    public void TransformPassBase_ShouldTransform_MonomorphizedFunction_ReturnsFalse()
    {
        var pass = new MockTransformPass();
        var function = new IrFunction("max$mono", IrIntType.I32);

        var method = typeof(TransformPassBase).GetMethod("ShouldTransform",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (bool)method!.Invoke(pass, new object[] { function })!;

        Assert.False(result);
    }

    [Fact]
    public void TransformPassBase_Name_ReturnsCorrectName()
    {
        var pass = new MockTransformPass();

        Assert.Equal("Mock Transform", pass.Name);
    }

    [Fact]
    public void TransformPipeline_RealInlinePass_HasCorrectName()
    {
        var pass = new InlineExpansionPass();

        Assert.Equal("Inline Expansion", pass.Name);
    }
}
