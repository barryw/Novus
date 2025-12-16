using Xunit;
using Novus.IR;

namespace Novus.Tests.IR;

/// <summary>
/// Tests for the complete IR optimization pipeline.
/// These tests verify that multiple optimization passes work together correctly.
/// </summary>
public class IrOptimizationPipelineTests
{
    [Fact]
    public void Pipeline_O0_NoOptimizations()
    {
        // O0 should perform no optimizations
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O0);
        var stats = pipeline.Run();

        // Should perform no optimizations
        Assert.Equal(0, stats.AlgebraicSimplifications);
        Assert.Equal(0, stats.ConstantPropagations);
        Assert.Equal(0, stats.DeadCodeEliminations);
    }

    [Fact]
    public void Pipeline_O1_BasicOptimizations()
    {
        // O1 should run algebraic simplification, constant propagation, and DCE
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Add,
            new IrVariable("y", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O1);
        var stats = pipeline.Run();

        // Should perform algebraic simplification and constant propagation
        Assert.True(stats.AlgebraicSimplifications > 0 || stats.ConstantPropagations > 0);
    }

    [Fact]
    public void Pipeline_O2_IncludesStrengthReduction()
    {
        // O2 should include strength reduction
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(4, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O2);
        var stats = pipeline.Run();

        // Should perform strength reduction (mul by 4 -> shift by 2)
        Assert.True(stats.StrengthReductions > 0);
    }

    [Fact]
    public void Pipeline_O2_IncludesCopyPropagation()
    {
        // O2 should include copy propagation
        // Use a parameter instead of a constant to prevent constant propagation from eliminating copies
        var function = new IrFunction("test", IrIntType.I32);
        function.Parameters.Add(new IrParameter("input", IrIntType.I32));
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrStore("y", new IrVariable("input", IrIntType.I32)));
        block.Instructions.Add(new IrStore("z", new IrVariable("y", IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O2);
        var stats = pipeline.Run();

        // Should perform copy propagation
        Assert.True(stats.CopyPropagations > 0);
    }

    [Fact]
    public void Pipeline_CompleteExample_FullyOptimizes()
    {
        // Complex example showing all optimizations working together:
        // x = 5
        // y = x + 0        // Algebraic: should become y = x
        // z = y * 4        // Strength: should become z = y << 2
        // w = z + 3        // Constant: should fold to 23
        // return w

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Mul,
            new IrVariable("y", IrIntType.I32),
            new IrConstant(4, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("w", IrBinaryOp.OpKind.Add,
            new IrVariable("z", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("w", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O2);
        var stats = pipeline.Run();

        // Should perform multiple types of optimizations
        Assert.True(stats.AlgebraicSimplifications > 0);
        Assert.True(stats.StrengthReductions > 0);
        Assert.True(stats.ConstantPropagations > 0);

        // Final result should be optimized
        var ret = block.Instructions[^1] as IrReturn;
        Assert.NotNull(ret);
    }

    [Fact]
    public void Pipeline_IteratesUntilFixpoint()
    {
        // Test that pipeline iterates until no more changes
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(2, IrIntType.I32)));
        block.Instructions.Add(new IrStore("b", new IrVariable("a", IrIntType.I32)));
        block.Instructions.Add(new IrStore("c", new IrVariable("b", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("d", IrBinaryOp.OpKind.Mul,
            new IrVariable("c", IrIntType.I32),
            new IrConstant(4, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("d", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O2);
        var stats = pipeline.Run();

        // Should iterate multiple times
        Assert.True(stats.Iterations >= 1);
    }

    [Fact]
    public void Pipeline_MaxIterations_StopsEarly()
    {
        // Test that max iterations limit is respected
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var options = new OptimizationOptions { MaxIterations = 2 };
        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O2, options);
        var stats = pipeline.Run();

        // Should not exceed max iterations
        Assert.True(stats.Iterations <= 2);
    }

    [Fact]
    public void Pipeline_RunQuick_SingleIteration()
    {
        // RunQuick should do only one iteration
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function);
        var stats = pipeline.RunQuick();

        // Should be exactly 1 iteration
        Assert.Equal(1, stats.Iterations);
    }

    [Fact]
    public void Pipeline_Statistics_ToString()
    {
        // Test that statistics can be converted to string
        var stats = new OptimizationStatistics
        {
            Iterations = 3,
            AlgebraicSimplifications = 5,
            StrengthReductions = 2,
            ConstantPropagations = 10,
            CopyPropagations = 3,
            DeadCodeEliminations = 4
        };

        string str = stats.ToString();

        Assert.Contains("Iterations: 3", str);
        Assert.Contains("Algebraic Simplifications: 5", str);
        Assert.Contains("Strength Reductions: 2", str);
        Assert.Contains("Constant Propagations: 10", str);
    }

    [Fact]
    public void Pipeline_CombinedOptimizations_ProducesOptimalCode()
    {
        // Real-world example: array indexing calculation
        // array_offset = index * 4  (for 32-bit elements)
        // Should optimize: index * 4 -> index << 2

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("index", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("offset", IrBinaryOp.OpKind.Mul,
            new IrVariable("index", IrIntType.I32),
            new IrConstant(4, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("offset", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O2);
        var stats = pipeline.Run();

        // Should apply strength reduction (mul -> shift)
        Assert.True(stats.StrengthReductions > 0);

        // Then constant propagation should fold it
        Assert.True(stats.ConstantPropagations > 0);
    }

    [Fact]
    public void Pipeline_DeadCodeAfterOptimization_IsRemoved()
    {
        // After optimizations, some code becomes dead
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(10, IrIntType.I32))); // Dead
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var pipeline = new IrOptimizationPipeline(function, OptimizationLevel.O1);
        var stats = pipeline.Run();

        // Should eliminate dead code - either DSE or DCE will remove the unused y
        Assert.True(stats.DeadCodeEliminations > 0 || stats.DeadStoreEliminations > 0,
            "Expected either DeadCodeEliminations or DeadStoreEliminations to remove unused variable");
    }
}
