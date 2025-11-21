using Xunit;
using Novus.IR;

namespace Novus.Tests.IR;

public class AlgebraicSimplificationTests
{
    [Fact]
    public void Simplify_AddZero_EliminatesOperation()
    {
        // x + 0 = x
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

        var simplifier = new AlgebraicSimplification(function);
        int count = simplifier.Simplify();

        Assert.True(count > 0);

        // y = x + 0 should become y = x
        var store = block.Instructions[2] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("y", store.VariableName);
        var value = store.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_ZeroAdd_EliminatesOperation()
    {
        // 0 + x = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrConstant(0, IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_SubZero_EliminatesOperation()
    {
        // x - 0 = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Sub,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_SubSelf_BecomesZero()
    {
        // x - x = 0
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Sub,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrConstant;
        Assert.Equal(0, value!.Value);
    }

    [Fact]
    public void Simplify_MulOne_EliminatesOperation()
    {
        // x * 1 = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_MulZero_BecomesZero()
    {
        // x * 0 = 0
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrConstant;
        Assert.Equal(0, value!.Value);
    }

    [Fact]
    public void Simplify_DivOne_EliminatesOperation()
    {
        // x / 1 = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Div,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_AndSelf_EliminatesOperation()
    {
        // x & x = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.And,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_AndZero_BecomesZero()
    {
        // x & 0 = 0
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.And,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrConstant;
        Assert.Equal(0, value!.Value);
    }

    [Fact]
    public void Simplify_OrSelf_EliminatesOperation()
    {
        // x | x = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Or,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_OrZero_EliminatesOperation()
    {
        // x | 0 = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Or,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_XorZero_EliminatesOperation()
    {
        // x ^ 0 = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Xor,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_XorSelf_BecomesZero()
    {
        // x ^ x = 0
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Xor,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrConstant;
        Assert.Equal(0, value!.Value);
    }

    [Fact]
    public void Simplify_ShiftLeftZero_EliminatesOperation()
    {
        // x << 0 = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Shl,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_ShiftRightZero_EliminatesOperation()
    {
        // x >> 0 = x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Shr,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        simplifier.Simplify();

        var store = block.Instructions[2] as IrStore;
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Simplify_MultipleIdentities_SimplifiesAll()
    {
        // Test multiple simplifications in one function
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));

        // a = x + 0
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));

        // b = a * 1
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Mul,
            new IrVariable("a", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));

        // c = b | 0
        block.Instructions.Add(new IrBinaryOp("c", IrBinaryOp.OpKind.Or,
            new IrVariable("b", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));

        block.Instructions.Add(new IrReturn(new IrVariable("c", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var simplifier = new AlgebraicSimplification(function);
        int count = simplifier.Simplify();

        // Should simplify all three operations
        Assert.True(count >= 3);
    }
}
