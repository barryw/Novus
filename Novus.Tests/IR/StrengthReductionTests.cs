using Xunit;
using Novus.IR;

namespace Novus.Tests.IR;

public class StrengthReductionTests
{
    [Fact]
    public void Reduce_MulPowerOfTwo_BecomesShift()
    {
        // x * 2 → x << 1
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var reducer = new StrengthReduction(function);
        int count = reducer.Reduce();

        Assert.True(count > 0);

        // Should be replaced with shift left
        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.Equal(IrBinaryOp.OpKind.Shl, binOp.Operation);
        var shiftAmount = binOp.Right as IrConstant;
        Assert.Equal(1, shiftAmount!.Value);
    }

    [Fact]
    public void Reduce_MulByFour_BecomesShiftByTwo()
    {
        // x * 4 → x << 2
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

        var reducer = new StrengthReduction(function);
        reducer.Reduce();

        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Shl, binOp!.Operation);
        var shiftAmount = binOp.Right as IrConstant;
        Assert.Equal(2, shiftAmount!.Value);
    }

    [Fact]
    public void Reduce_MulByThree_BecomesShiftAndAdd()
    {
        // x * 3 → (x << 1) + x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var reducer = new StrengthReduction(function);
        reducer.Reduce();

        // Should have 2 instructions: shift and add
        // y_sr1 = x << 1
        // y = y_sr1 + x
        var shift = block.Instructions[2] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Shl, shift!.Operation);

        var add = block.Instructions[3] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Add, add!.Operation);
    }

    [Fact]
    public void Reduce_MulByFive_BecomesShiftAndAdd()
    {
        // x * 5 → (x << 2) + x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var reducer = new StrengthReduction(function);
        reducer.Reduce();

        var shift = block.Instructions[2] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Shl, shift!.Operation);
        var shiftAmount = shift.Right as IrConstant;
        Assert.Equal(2, shiftAmount!.Value);

        var add = block.Instructions[3] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Add, add!.Operation);
    }

    [Fact]
    public void Reduce_MulBySeven_BecomesShiftAndSub()
    {
        // x * 7 → (x << 3) - x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(7, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var reducer = new StrengthReduction(function);
        reducer.Reduce();

        var shift = block.Instructions[2] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Shl, shift!.Operation);

        var sub = block.Instructions[3] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Sub, sub!.Operation);
    }

    [Fact]
    public void Reduce_DivByPowerOfTwo_BecomesShift()
    {
        // x / 4 → x >> 2
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(20, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Div,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(4, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var reducer = new StrengthReduction(function);
        reducer.Reduce();

        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Shr, binOp!.Operation);
        var shiftAmount = binOp.Right as IrConstant;
        Assert.Equal(2, shiftAmount!.Value);
    }

    [Fact]
    public void Reduce_ModByPowerOfTwo_BecomesAnd()
    {
        // x % 8 → x & 7
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(20, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mod,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(8, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var reducer = new StrengthReduction(function);
        reducer.Reduce();

        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.And, binOp!.Operation);
        var mask = binOp.Right as IrConstant;
        Assert.Equal(7, mask!.Value); // 8 - 1 = 7
    }

    [Fact]
    public void Reduce_NonPowerOfTwo_NoChange()
    {
        // x * 11 → no change (11 is not power of 2 and not in our special cases)
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(11, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var reducer = new StrengthReduction(function);
        int count = reducer.Reduce();

        // No reduction should occur
        Assert.Equal(0, count);

        // Should still be multiplication
        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.Equal(IrBinaryOp.OpKind.Mul, binOp!.Operation);
    }

    [Fact]
    public void Reduce_MulBy10_BecomesShiftsAndAdd()
    {
        // x * 10 → (x << 3) + (x << 1)
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var reducer = new StrengthReduction(function);
        reducer.Reduce();

        // Should have 3 instructions: shift, shift, add
        Assert.True(block.Instructions.Count >= 5); // label, decl, shift, shift, add, return
    }
}
