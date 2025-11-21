using Xunit;
using Novus.IR;

namespace Novus.Tests.IR;

public class LoopInvariantCodeMotionTests
{
    [Fact]
    public void Optimize_SimpleLoopInvariant_Hoists()
    {
        // loop:
        //   x = a + b    // a, b defined outside - invariant!
        //   i = i + 1
        //   if i < 10 goto loop
        var function = new IrFunction("test", IrIntType.I32);

        // Preheader block
        var preheader = new IrBasicBlock("entry");
        preheader.Instructions.Add(new IrLabel("entry"));
        preheader.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("b", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        preheader.Instructions.Add(new IrBranch("loop"));

        // Loop block
        var loop = new IrBasicBlock("loop");
        loop.Instructions.Add(new IrLabel("loop"));
        loop.Instructions.Add(new IrBinaryOp("x", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("i", IrBinaryOp.OpKind.Add,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(
            new IrVariable("cond", IrBoolType.Instance),
            "loop",
            "exit"));

        // Exit block
        var exit = new IrBasicBlock("exit");
        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(preheader);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(exit);

        var licm = new LoopInvariantCodeMotion(function);
        int count = licm.Optimize();

        // Should hoist the invariant computation
        Assert.True(count > 0);

        // The x = a + b computation should now be in the preheader
        bool foundInPreheader = preheader.Instructions.Any(i =>
            i is IrBinaryOp binOp &&
            binOp.ResultName == "x" &&
            binOp.Operation == IrBinaryOp.OpKind.Add);

        Assert.True(foundInPreheader);
    }

    [Fact]
    public void Optimize_NonInvariantComputation_DoesNotHoist()
    {
        // loop:
        //   x = i * 2    // Depends on i, which changes in loop - NOT invariant
        //   i = i + 1
        //   if i < 10 goto loop
        var function = new IrFunction("test", IrIntType.I32);

        var preheader = new IrBasicBlock("entry");
        preheader.Instructions.Add(new IrLabel("entry"));
        preheader.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        preheader.Instructions.Add(new IrBranch("loop"));

        var loop = new IrBasicBlock("loop");
        loop.Instructions.Add(new IrLabel("loop"));
        loop.Instructions.Add(new IrBinaryOp("x", IrBinaryOp.OpKind.Mul,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("i", IrBinaryOp.OpKind.Add,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(
            new IrVariable("cond", IrBoolType.Instance),
            "loop",
            "exit"));

        var exit = new IrBasicBlock("exit");
        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(preheader);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(exit);

        var licm = new LoopInvariantCodeMotion(function);
        int count = licm.Optimize();

        // Should NOT hoist because x depends on i
        Assert.Equal(0, count);
    }

    [Fact]
    public void Optimize_FunctionCall_DoesNotHoist()
    {
        // Function calls have side effects and should not be hoisted
        // loop:
        //   x = foo()    // Even if foo() is pure, we conservatively don't hoist
        //   i = i + 1
        //   if i < 10 goto loop
        var function = new IrFunction("test", IrIntType.I32);

        var preheader = new IrBasicBlock("entry");
        preheader.Instructions.Add(new IrLabel("entry"));
        preheader.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        preheader.Instructions.Add(new IrBranch("loop"));

        var loop = new IrBasicBlock("loop");
        loop.Instructions.Add(new IrLabel("loop"));
        var call = new IrCall("foo", IrIntType.I32, "x");
        loop.Instructions.Add(call);
        loop.Instructions.Add(new IrBinaryOp("i", IrBinaryOp.OpKind.Add,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(
            new IrVariable("cond", IrBoolType.Instance),
            "loop",
            "exit"));

        var exit = new IrBasicBlock("exit");
        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(preheader);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(exit);

        var licm = new LoopInvariantCodeMotion(function);
        int count = licm.Optimize();

        // Should NOT hoist function calls
        Assert.Equal(0, count);
    }

    [Fact]
    public void Optimize_MultipleInvariants_HoistsAll()
    {
        // loop:
        //   x = a + b    // Invariant
        //   y = c * d    // Invariant
        //   i = i + 1
        //   if i < 10 goto loop
        var function = new IrFunction("test", IrIntType.I32);

        var preheader = new IrBasicBlock("entry");
        preheader.Instructions.Add(new IrLabel("entry"));
        preheader.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("b", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("c", IrIntType.I32, false, new IrConstant(3, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("d", IrIntType.I32, false, new IrConstant(7, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        preheader.Instructions.Add(new IrBranch("loop"));

        var loop = new IrBasicBlock("loop");
        loop.Instructions.Add(new IrLabel("loop"));
        loop.Instructions.Add(new IrBinaryOp("x", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("c", IrIntType.I32),
            new IrVariable("d", IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("i", IrBinaryOp.OpKind.Add,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(
            new IrVariable("cond", IrBoolType.Instance),
            "loop",
            "exit"));

        var exit = new IrBasicBlock("exit");
        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(preheader);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(exit);

        var licm = new LoopInvariantCodeMotion(function);
        int count = licm.Optimize();

        // Should hoist both invariant computations
        Assert.True(count >= 2);
    }

    [Fact]
    public void Optimize_ChainedInvariants_HoistsInOrder()
    {
        // loop:
        //   x = a + b    // Invariant (depends on a, b from outside)
        //   y = x * 2    // Invariant (depends on x, which is invariant)
        //   i = i + 1
        //   if i < 10 goto loop
        var function = new IrFunction("test", IrIntType.I32);

        var preheader = new IrBasicBlock("entry");
        preheader.Instructions.Add(new IrLabel("entry"));
        preheader.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("b", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        preheader.Instructions.Add(new IrBranch("loop"));

        var loop = new IrBasicBlock("loop");
        loop.Instructions.Add(new IrLabel("loop"));
        loop.Instructions.Add(new IrBinaryOp("x", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("i", IrBinaryOp.OpKind.Add,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(
            new IrVariable("cond", IrBoolType.Instance),
            "loop",
            "exit"));

        var exit = new IrBasicBlock("exit");
        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(preheader);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(exit);

        var licm = new LoopInvariantCodeMotion(function);
        int count = licm.Optimize();

        // Should hoist both x and y (y is invariant because it depends on invariant x)
        Assert.True(count >= 2);
    }

    [Fact]
    public void Optimize_NoLoop_DoesNothing()
    {
        // Simple straight-line code, no loop
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("x", IrBinaryOp.OpKind.Add,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var licm = new LoopInvariantCodeMotion(function);
        int count = licm.Optimize();

        // No loops, nothing to optimize
        Assert.Equal(0, count);
    }

    [Fact]
    public void Optimize_StoreInstruction_DoesNotHoist()
    {
        // Stores have side effects and should never be hoisted
        // loop:
        //   x = a    // Store operation
        //   i = i + 1
        //   if i < 10 goto loop
        var function = new IrFunction("test", IrIntType.I32);

        var preheader = new IrBasicBlock("entry");
        preheader.Instructions.Add(new IrLabel("entry"));
        preheader.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        preheader.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        preheader.Instructions.Add(new IrBranch("loop"));

        var loop = new IrBasicBlock("loop");
        loop.Instructions.Add(new IrLabel("loop"));
        loop.Instructions.Add(new IrStore("x", new IrVariable("a", IrIntType.I32)));
        loop.Instructions.Add(new IrBinaryOp("i", IrBinaryOp.OpKind.Add,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(
            new IrVariable("cond", IrBoolType.Instance),
            "loop",
            "exit"));

        var exit = new IrBasicBlock("exit");
        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(preheader);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(exit);

        var licm = new LoopInvariantCodeMotion(function);
        int count = licm.Optimize();

        // Should NOT hoist stores
        Assert.Equal(0, count);
    }

    [Fact]
    public void Optimize_ConstantOperation_Hoists()
    {
        // loop:
        //   x = 5 + 10   // Pure constant operation - invariant
        //   i = i + 1
        //   if i < 10 goto loop
        var function = new IrFunction("test", IrIntType.I32);

        var preheader = new IrBasicBlock("entry");
        preheader.Instructions.Add(new IrLabel("entry"));
        preheader.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        preheader.Instructions.Add(new IrBranch("loop"));

        var loop = new IrBasicBlock("loop");
        loop.Instructions.Add(new IrLabel("loop"));
        loop.Instructions.Add(new IrBinaryOp("x", IrBinaryOp.OpKind.Add,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("i", IrBinaryOp.OpKind.Add,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(
            new IrVariable("cond", IrBoolType.Instance),
            "loop",
            "exit"));

        var exit = new IrBasicBlock("exit");
        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(preheader);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(exit);

        var licm = new LoopInvariantCodeMotion(function);
        int count = licm.Optimize();

        // Should hoist the constant computation
        Assert.True(count > 0);
    }
}
