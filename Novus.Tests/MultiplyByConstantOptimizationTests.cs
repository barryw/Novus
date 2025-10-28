using Novus.IR;
using Novus.Optimizer.Passes;
using Xunit;

namespace Novus.Tests;

public class MultiplyByConstantOptimizationTests
{
    private readonly StrengthReductionPass _pass = new();

    [Fact]
    public void MultiplyByPowerOfTwo_ConvertedToShift()
    {
        // x * 4 => x << 2
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var four = new IrConstant(4, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, four, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Equal(IrBinaryOp.OpKind.Shl, mul.Operation);
        Assert.Same(varX, mul.Left);
        var shiftAmount = Assert.IsType<IrConstant>(mul.Right);
        Assert.Equal(2, shiftAmount.Value); // 4 = 2^2
    }

    [Fact]
    public void MultiplyBy3_ConvertedToShiftPlusAdd()
    {
        // x * 3 => (x << 1) + x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var three = new IrConstant(3, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, three, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Equal(2, block.Instructions.Count); // shift + add

        // First instruction should be the shift
        var shift = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Shl, shift.Operation);
        Assert.Same(varX, shift.Left);
        var shiftAmount = Assert.IsType<IrConstant>(shift.Right);
        Assert.Equal(1, shiftAmount.Value); // shift by 1

        // Second instruction should be the add
        var add = Assert.IsType<IrBinaryOp>(block.Instructions[1]);
        Assert.Equal(IrBinaryOp.OpKind.Add, add.Operation);
        Assert.Same(varX, add.Right);
        var shiftResult = Assert.IsType<IrVariable>(add.Left);
        Assert.Equal(shift.ResultName, shiftResult.Name);
    }

    [Fact]
    public void MultiplyBy5_ConvertedToShiftPlusAdd()
    {
        // x * 5 => (x << 2) + x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var five = new IrConstant(5, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, five, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Equal(2, block.Instructions.Count);

        var shift = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Shl, shift.Operation);
        var shiftAmount = Assert.IsType<IrConstant>(shift.Right);
        Assert.Equal(2, shiftAmount.Value); // shift by 2

        var add = Assert.IsType<IrBinaryOp>(block.Instructions[1]);
        Assert.Equal(IrBinaryOp.OpKind.Add, add.Operation);
    }

    [Fact]
    public void MultiplyBy7_ConvertedToShiftMinusSubtract()
    {
        // x * 7 => (x << 3) - x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var seven = new IrConstant(7, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, seven, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Equal(2, block.Instructions.Count);

        var shift = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Shl, shift.Operation);
        var shiftAmount = Assert.IsType<IrConstant>(shift.Right);
        Assert.Equal(3, shiftAmount.Value); // shift by 3 (2^3 = 8)

        var sub = Assert.IsType<IrBinaryOp>(block.Instructions[1]);
        Assert.Equal(IrBinaryOp.OpKind.Sub, sub.Operation);
        Assert.Same(varX, sub.Right); // Subtract x from (x << 3)
    }

    [Fact]
    public void MultiplyBy9_ConvertedToShiftPlusAdd()
    {
        // x * 9 => (x << 3) + x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var nine = new IrConstant(9, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, nine, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Equal(2, block.Instructions.Count);

        var shift = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        Assert.Equal(IrBinaryOp.OpKind.Shl, shift.Operation);
        var shiftAmount = Assert.IsType<IrConstant>(shift.Right);
        Assert.Equal(3, shiftAmount.Value); // shift by 3

        var add = Assert.IsType<IrBinaryOp>(block.Instructions[1]);
        Assert.Equal(IrBinaryOp.OpKind.Add, add.Operation);
    }

    [Fact]
    public void MultiplyBy15_ConvertedToShiftMinusSubtract()
    {
        // x * 15 => (x << 4) - x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var fifteen = new IrConstant(15, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, fifteen, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Equal(2, block.Instructions.Count);

        var shift = Assert.IsType<IrBinaryOp>(block.Instructions[0]);
        var shiftAmount = Assert.IsType<IrConstant>(shift.Right);
        Assert.Equal(4, shiftAmount.Value); // shift by 4 (2^4 = 16)

        var sub = Assert.IsType<IrBinaryOp>(block.Instructions[1]);
        Assert.Equal(IrBinaryOp.OpKind.Sub, sub.Operation);
    }

    [Fact]
    public void MultiplyBy6_NotOptimized()
    {
        // x * 6 - not yet optimized (would need 2 shifts + 1 add)
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var six = new IrConstant(6, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, six, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.False(changed); // Not optimized yet
        Assert.Single(block.Instructions); // Original multiply remains
    }

    [Fact]
    public void MultiplyBy10_NotOptimized()
    {
        // x * 10 - not yet optimized (would need 2 shifts + 1 add)
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var ten = new IrConstant(10, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, ten, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.False(changed); // Not optimized yet
        Assert.Single(block.Instructions);
    }

    [Fact]
    public void MultiplyByNegative_NotOptimized()
    {
        // x * -5 - not optimized (would need negation)
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var negFive = new IrConstant(-5, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, negFive, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.False(changed); // Not optimized
        Assert.Single(block.Instructions);
    }

    [Fact]
    public void MultiplyWithConstantOnLeft_StillOptimized()
    {
        // 5 * x => (x << 2) + x (constant on left)
        var block = new IrBasicBlock("test");
        var five = new IrConstant(5, IrIntType.I32);
        var varX = new IrVariable("x", IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, five, varX, IrIntType.I32);

        block.Instructions.Add(mul);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Equal(2, block.Instructions.Count);

        var add = Assert.IsType<IrBinaryOp>(block.Instructions[1]);
        Assert.Equal(IrBinaryOp.OpKind.Add, add.Operation);
    }
}
