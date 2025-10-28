using Novus.IR;
using Novus.Optimizer.Passes;
using Xunit;

namespace Novus.Tests;

public class AlgebraicSimplificationPassTests
{
    private readonly AlgebraicSimplificationPass _pass = new();

    [Fact]
    public void SimplifyAddZero_RightOperand_ReturnsLeftOperand()
    {
        // x + 0 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add, varX, zero, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(add);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions); // Binary op removed
        Assert.IsType<IrStore>(block.Instructions[0]);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifyAddZero_LeftOperand_ReturnsRightOperand()
    {
        // 0 + x => x
        var block = new IrBasicBlock("test");
        var zero = new IrConstant(0, IrIntType.I32);
        var varX = new IrVariable("x", IrIntType.I32);
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add, zero, varX, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(add);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifySubtractZero_ReturnsOperand()
    {
        // x - 0 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var sub = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Sub, varX, zero, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(sub);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifySubtractSelf_ReturnsZero()
    {
        // x - x => 0
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var varX2 = new IrVariable("x", IrIntType.I32); // Same variable name
        var sub = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Sub, varX, varX2, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(sub);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.IsType<IrConstant>(resultStore.Value);
        var constant = (IrConstant)resultStore.Value;
        Assert.Equal(0, constant.Value);
    }

    [Fact]
    public void SimplifyMultiplyByZero_ReturnsZero()
    {
        // x * 0 => 0
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, zero, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(mul);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.IsType<IrConstant>(resultStore.Value);
        var constant = (IrConstant)resultStore.Value;
        Assert.Equal(0, constant.Value);
    }

    [Fact]
    public void SimplifyMultiplyByOne_ReturnsOperand()
    {
        // x * 1 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var one = new IrConstant(1, IrIntType.I32);
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul, varX, one, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(mul);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifyDivideByOne_ReturnsOperand()
    {
        // x / 1 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var one = new IrConstant(1, IrIntType.I32);
        var div = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Div, varX, one, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(div);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifyZeroDivide_ReturnsZero()
    {
        // 0 / x => 0
        var block = new IrBasicBlock("test");
        var zero = new IrConstant(0, IrIntType.I32);
        var varX = new IrVariable("x", IrIntType.I32);
        var div = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Div, zero, varX, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(div);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.IsType<IrConstant>(resultStore.Value);
        var constant = (IrConstant)resultStore.Value;
        Assert.Equal(0, constant.Value);
    }

    [Fact]
    public void SimplifyModuloByOne_ReturnsZero()
    {
        // x % 1 => 0
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var one = new IrConstant(1, IrIntType.I32);
        var mod = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mod, varX, one, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(mod);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.IsType<IrConstant>(resultStore.Value);
        var constant = (IrConstant)resultStore.Value;
        Assert.Equal(0, constant.Value);
    }

    [Fact]
    public void SimplifyShiftByZero_ReturnsOperand()
    {
        // x << 0 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var shl = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Shl, varX, zero, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(shl);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifyShiftZero_ReturnsZero()
    {
        // 0 << x => 0
        var block = new IrBasicBlock("test");
        var zero = new IrConstant(0, IrIntType.I32);
        var varX = new IrVariable("x", IrIntType.I32);
        var shl = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Shl, zero, varX, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(shl);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.IsType<IrConstant>(resultStore.Value);
        var constant = (IrConstant)resultStore.Value;
        Assert.Equal(0, constant.Value);
    }

    [Fact]
    public void SimplifyOrWithZero_ReturnsOperand()
    {
        // x | 0 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var or = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Or, varX, zero, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(or);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifyOrWithAllOnes_ReturnsAllOnes()
    {
        // x | -1 => -1
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var allOnes = new IrConstant(-1, IrIntType.I32);
        var or = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Or, varX, allOnes, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(or);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(allOnes, resultStore.Value);
    }

    [Fact]
    public void SimplifyAndWithZero_ReturnsZero()
    {
        // x & 0 => 0
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var and = new IrBinaryOp("%t0", IrBinaryOp.OpKind.And, varX, zero, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(and);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.IsType<IrConstant>(resultStore.Value);
        var constant = (IrConstant)resultStore.Value;
        Assert.Equal(0, constant.Value);
    }

    [Fact]
    public void SimplifyAndWithAllOnes_ReturnsOperand()
    {
        // x & -1 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var allOnes = new IrConstant(-1, IrIntType.I32);
        var and = new IrBinaryOp("%t0", IrBinaryOp.OpKind.And, varX, allOnes, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(and);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifyXorWithZero_ReturnsOperand()
    {
        // x ^ 0 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var xor = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Xor, varX, zero, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(xor);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }

    [Fact]
    public void SimplifyXorWithSelf_ReturnsZero()
    {
        // x ^ x => 0
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var varX2 = new IrVariable("x", IrIntType.I32); // Same variable name
        var xor = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Xor, varX, varX2, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(xor);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.IsType<IrConstant>(resultStore.Value);
        var constant = (IrConstant)resultStore.Value;
        Assert.Equal(0, constant.Value);
    }

    [Fact]
    public void NoSimplification_WhenNotApplicable()
    {
        // x + 5 (no simplification)
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var five = new IrConstant(5, IrIntType.I32);
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add, varX, five, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(add);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.False(changed);
        Assert.Equal(2, block.Instructions.Count); // Both instructions remain
    }

    [Fact]
    public void SimplifyInReturn_Statement()
    {
        // return x + 0; => return x;
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add, varX, zero, IrIntType.I32);
        var resultVar = new IrVariable("%t0", IrIntType.I32);
        var ret = new IrReturn(resultVar);

        block.Instructions.Add(add);
        block.Instructions.Add(ret);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        Assert.IsType<IrReturn>(block.Instructions[0]);
        var resultRet = (IrReturn)block.Instructions[0];
        Assert.Same(varX, resultRet.Value);
    }

    [Fact]
    public void SimplifyInNestedExpression()
    {
        // y = (x + 0) * 2; => y = x * 2;
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.I32);
        var zero = new IrConstant(0, IrIntType.I32);
        var two = new IrConstant(2, IrIntType.I32);
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add, varX, zero, IrIntType.I32);
        var addResultVar = new IrVariable("%t0", IrIntType.I32);
        var mul = new IrBinaryOp("%t1", IrBinaryOp.OpKind.Mul, addResultVar, two, IrIntType.I32);
        var mulResultVar = new IrVariable("%t1", IrIntType.I32);
        var store = new IrStore("result", mulResultVar);

        block.Instructions.Add(add);
        block.Instructions.Add(mul);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Equal(2, block.Instructions.Count); // add removed, mul and store remain
        var mulOp = (IrBinaryOp)block.Instructions[0];
        Assert.Same(varX, mulOp.Left); // add replaced with x
    }

    [Fact]
    public void SimplifyByte_Operations()
    {
        // byte: x + 0 => x
        var block = new IrBasicBlock("test");
        var varX = new IrVariable("x", IrIntType.U8);
        var zero = new IrConstant(0, IrIntType.U8);
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add, varX, zero, IrIntType.U8);
        var resultVar = new IrVariable("%t0", IrIntType.U8);
        var store = new IrStore("result", resultVar);

        block.Instructions.Add(add);
        block.Instructions.Add(store);

        var changed = _pass.RunOnBasicBlock(block);

        Assert.True(changed);
        Assert.Single(block.Instructions);
        var resultStore = (IrStore)block.Instructions[0];
        Assert.Same(varX, resultStore.Value);
    }
}
