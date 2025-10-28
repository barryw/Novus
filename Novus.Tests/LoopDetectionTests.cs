using Novus.IR;
using Xunit;

namespace Novus.Tests;

public class LoopDetectionTests
{
    [Fact]
    public void SimpleWhileLoop_DetectedCorrectly()
    {
        // while (x < 10) { x = x + 1 }
        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var loopHeader = new IrBasicBlock("loop_header");
        var loopBody = new IrBasicBlock("loop_body");
        var exit = new IrBasicBlock("exit");

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(loopHeader);
        function.BasicBlocks.Add(loopBody);
        function.BasicBlocks.Add(exit);

        // entry: branch to loop_header
        entry.Instructions.Add(new IrBranch("loop_header"));

        // loop_header: if (x < 10) goto loop_body else goto exit
        var cond = new IrBinaryOp("%cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32);
        loopHeader.Instructions.Add(cond);
        loopHeader.Instructions.Add(new IrConditionalBranch(new IrVariable("%cond", IrIntType.I32), "loop_body", "exit"));

        // loop_body: x = x + 1; goto loop_header
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32);
        loopBody.Instructions.Add(add);
        loopBody.Instructions.Add(new IrStore("x", new IrVariable("%t0", IrIntType.I32)));
        loopBody.Instructions.Add(new IrBranch("loop_header"));

        // exit: return
        exit.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        // Detect loops
        var cfg = new ControlFlowGraph(function);
        var detector = new LoopDetector(cfg);
        var loops = detector.DetectLoops();

        // Should detect exactly one loop
        Assert.Single(loops);

        var loop = loops[0];
        Assert.Equal("loop_header", loop.Header.Label);

        // Loop should contain header and body
        Assert.Contains(loop.Body, n => n.Label == "loop_header");
        Assert.Contains(loop.Body, n => n.Label == "loop_body");

        // Should not contain entry or exit
        Assert.DoesNotContain(loop.Body, n => n.Label == "entry");
        Assert.DoesNotContain(loop.Body, n => n.Label == "exit");

        // Should have one back edge (loop_body → loop_header)
        Assert.Single(loop.BackEdges);
        Assert.Equal("loop_body", loop.BackEdges[0].Source.Label);
        Assert.Equal("loop_header", loop.BackEdges[0].Destination.Label);

        // Loop depth should be 0 (outermost)
        Assert.Equal(0, loop.GetDepth());
    }

    [Fact]
    public void NestedLoops_DetectedWithCorrectNesting()
    {
        // Outer loop with inner loop
        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var outerHeader = new IrBasicBlock("outer_header");
        var innerHeader = new IrBasicBlock("inner_header");
        var innerBody = new IrBasicBlock("inner_body");
        var outerBody = new IrBasicBlock("outer_body");
        var exit = new IrBasicBlock("exit");

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(outerHeader);
        function.BasicBlocks.Add(innerHeader);
        function.BasicBlocks.Add(innerBody);
        function.BasicBlocks.Add(outerBody);
        function.BasicBlocks.Add(exit);

        // entry → outer_header
        entry.Instructions.Add(new IrBranch("outer_header"));

        // outer_header: if condition goto inner_header else exit
        var outerCond = new IrBinaryOp("%outer_cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32);
        outerHeader.Instructions.Add(outerCond);
        outerHeader.Instructions.Add(new IrConditionalBranch(
            new IrVariable("%outer_cond", IrIntType.I32), "inner_header", "exit"));

        // inner_header: if condition goto inner_body else outer_body
        var innerCond = new IrBinaryOp("%inner_cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("j", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32);
        innerHeader.Instructions.Add(innerCond);
        innerHeader.Instructions.Add(new IrConditionalBranch(
            new IrVariable("%inner_cond", IrIntType.I32), "inner_body", "outer_body"));

        // inner_body → inner_header (inner loop back edge)
        innerBody.Instructions.Add(new IrBranch("inner_header"));

        // outer_body → outer_header (outer loop back edge)
        outerBody.Instructions.Add(new IrBranch("outer_header"));

        // exit
        exit.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        // Detect loops
        var cfg = new ControlFlowGraph(function);
        var detector = new LoopDetector(cfg);
        var loops = detector.DetectLoops();

        // Should detect two loops
        Assert.Equal(2, loops.Count);

        // Find outer and inner loops
        var outerLoop = loops.FirstOrDefault(l => l.Header.Label == "outer_header");
        var innerLoop = loops.FirstOrDefault(l => l.Header.Label == "inner_header");

        Assert.NotNull(outerLoop);
        Assert.NotNull(innerLoop);

        // Inner loop should be nested in outer loop
        Assert.Equal(outerLoop, innerLoop.ParentLoop);
        Assert.Contains(innerLoop, outerLoop.NestedLoops);

        // Depth should be correct
        Assert.Equal(0, outerLoop.GetDepth());
        Assert.Equal(1, innerLoop.GetDepth());

        // Outer loop should contain inner loop header
        Assert.Contains(outerLoop.Body, n => n.Label == "inner_header");

        // Inner loop should NOT contain outer loop header
        Assert.DoesNotContain(innerLoop.Body, n => n.Label == "outer_header");
    }

    [Fact]
    public void NoLoops_ReturnsEmptyList()
    {
        // Straight-line code with no loops
        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var middle = new IrBasicBlock("middle");
        var exit = new IrBasicBlock("exit");

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(middle);
        function.BasicBlocks.Add(exit);

        entry.Instructions.Add(new IrBranch("middle"));
        middle.Instructions.Add(new IrBranch("exit"));
        exit.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        var cfg = new ControlFlowGraph(function);
        var detector = new LoopDetector(cfg);
        var loops = detector.DetectLoops();

        Assert.Empty(loops);
    }

    [Fact]
    public void LoopWithMultipleBackEdges_DetectedCorrectly()
    {
        // Loop with two different paths back to header
        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var loopHeader = new IrBasicBlock("loop_header");
        var path1 = new IrBasicBlock("path1");
        var path2 = new IrBasicBlock("path2");
        var exit = new IrBasicBlock("exit");

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(loopHeader);
        function.BasicBlocks.Add(path1);
        function.BasicBlocks.Add(path2);
        function.BasicBlocks.Add(exit);

        entry.Instructions.Add(new IrBranch("loop_header"));

        // loop_header: branch to path1 or exit
        var cond1 = new IrBinaryOp("%cond1", IrBinaryOp.OpKind.Lt,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32);
        loopHeader.Instructions.Add(cond1);
        loopHeader.Instructions.Add(new IrConditionalBranch(
            new IrVariable("%cond1", IrIntType.I32), "path1", "exit"));

        // path1: branch to path2 or back to header
        var cond2 = new IrBinaryOp("%cond2", IrBinaryOp.OpKind.Eq,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32);
        path1.Instructions.Add(cond2);
        path1.Instructions.Add(new IrConditionalBranch(
            new IrVariable("%cond2", IrIntType.I32), "path2", "loop_header"));  // Back edge 1

        // path2: back to header
        path2.Instructions.Add(new IrBranch("loop_header"));  // Back edge 2

        exit.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        var cfg = new ControlFlowGraph(function);
        var detector = new LoopDetector(cfg);
        var loops = detector.DetectLoops();

        Assert.Single(loops);

        var loop = loops[0];

        // Should have two back edges
        Assert.Equal(2, loop.BackEdges.Count);

        // Both back edges should point to loop_header
        Assert.All(loop.BackEdges, edge => Assert.Equal("loop_header", edge.Destination.Label));

        // Loop body should contain all three blocks
        Assert.Contains(loop.Body, n => n.Label == "loop_header");
        Assert.Contains(loop.Body, n => n.Label == "path1");
        Assert.Contains(loop.Body, n => n.Label == "path2");
    }

    [Fact]
    public void LoopInvariantDetection_ConstantsAreInvariant()
    {
        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var loopHeader = new IrBasicBlock("loop_header");
        var loopBody = new IrBasicBlock("loop_body");
        var exit = new IrBasicBlock("exit");

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(loopHeader);
        function.BasicBlocks.Add(loopBody);
        function.BasicBlocks.Add(exit);

        entry.Instructions.Add(new IrBranch("loop_header"));

        var cond = new IrBinaryOp("%cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32);
        loopHeader.Instructions.Add(cond);
        loopHeader.Instructions.Add(new IrConditionalBranch(
            new IrVariable("%cond", IrIntType.I32), "loop_body", "exit"));

        // This operation uses only constants - should be loop-invariant
        var invariantOp = new IrBinaryOp("%inv", IrBinaryOp.OpKind.Add,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32);
        loopBody.Instructions.Add(invariantOp);
        loopBody.Instructions.Add(new IrBranch("loop_header"));

        exit.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        var cfg = new ControlFlowGraph(function);
        var detector = new LoopDetector(cfg);
        var loops = detector.DetectLoops();

        Assert.Single(loops);

        var loop = loops[0];

        // The invariant operation should be detected as loop-invariant
        Assert.True(detector.IsLoopInvariant(invariantOp, loop));
    }
}
