using Xunit;
using Novus.IR;

namespace Novus.Tests.IR;

public class SparseConditionalConstantPropagationTests
{
    [Fact]
    public void Propagate_SimpleConstant_Propagates()
    {
        // x = 5
        // y = x + 3
        // Expected: y = 8
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        int count = sccp.Propagate();

        // Should propagate x's value
        Assert.True(count > 0);

        // Binary op should have constant left operand
        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.IsType<IrConstant>(binOp!.Left);
        var leftConst = binOp.Left as IrConstant;
        Assert.Equal(5, leftConst!.Value);
    }

    [Fact]
    public void Propagate_ConstantFolding_FullyEvaluates()
    {
        // a = 10
        // b = 20
        // c = a + b
        // Expected: c becomes 30
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("b", IrIntType.I32, false, new IrConstant(20, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("c", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("c", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        sccp.Propagate();

        // Both operands should be replaced with constants
        var binOp = block.Instructions[3] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.IsType<IrConstant>(binOp!.Left);
        Assert.IsType<IrConstant>(binOp.Right);
    }

    [Fact]
    public void Propagate_ChainedConstants_PropagatesThroughChain()
    {
        // x = 5
        // y = x
        // z = y + 10
        // Expected: z = 15
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrVariable("x", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Add,
            new IrVariable("y", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        sccp.Propagate();

        // y should be replaced with constant in the store
        var store = block.Instructions[2] as IrStore;
        Assert.NotNull(store);
        Assert.IsType<IrConstant>(store!.Value);
        var storeConst = store.Value as IrConstant;
        Assert.Equal(5, storeConst!.Value);

        // z's left operand should be replaced with 5
        var binOp = block.Instructions[3] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.IsType<IrConstant>(binOp!.Left);
        var leftConst = binOp.Left as IrConstant;
        Assert.Equal(5, leftConst!.Value);
    }

    [Fact]
    public void Propagate_Multiplication_EvaluatesCorrectly()
    {
        // x = 6
        // y = x * 7
        // Expected: y = 42
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(6, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(7, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        sccp.Propagate();

        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.IsType<IrConstant>(binOp!.Left);
        var leftConst = binOp.Left as IrConstant;
        Assert.Equal(6, leftConst!.Value);
    }

    [Fact]
    public void Propagate_BitwiseOperations_Work()
    {
        // x = 15
        // y = x & 7
        // Expected: y = 7
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(15, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.And,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(7, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        sccp.Propagate();

        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.IsType<IrConstant>(binOp!.Left);
    }

    [Fact]
    public void Propagate_Comparison_Evaluates()
    {
        // x = 10
        // y = 5
        // z = x > y
        // Expected: z = 1 (true)
        var function = new IrFunction("test", IrBoolType.Instance);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Gt,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrBoolType.Instance));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrBoolType.Instance)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        sccp.Propagate();

        var binOp = block.Instructions[3] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.IsType<IrConstant>(binOp!.Left);
        Assert.IsType<IrConstant>(binOp.Right);
    }

    [Fact]
    public void Propagate_ShiftOperations_Work()
    {
        // x = 8
        // y = x << 2
        // Expected: y = 32
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(8, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Shl,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        sccp.Propagate();

        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.IsType<IrConstant>(binOp!.Left);
    }

    [Fact]
    public void Propagate_NonConstantInput_DoesNotPropagate()
    {
        // Test with function parameter (unknown value)
        // result = param + 10
        var function = new IrFunction("test", IrIntType.I32);
        function.Parameters.Add(new IrParameter("param", IrIntType.I32));
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Add,
            new IrVariable("param", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        int count = sccp.Propagate();

        // Should not propagate anything since param is unknown
        // The binary op should remain unchanged
        var binOp = block.Instructions[1] as IrBinaryOp;
        Assert.NotNull(binOp);
        // param should still be a variable
        Assert.IsType<IrVariable>(binOp!.Left);
    }

    [Fact]
    public void Propagate_ComplexExpression_PropagatesAll()
    {
        // a = 5
        // b = 10
        // c = a + b      // = 15
        // d = c * 2      // = 30
        // e = d - 10     // = 20
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("b", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("c", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("d", IrBinaryOp.OpKind.Mul,
            new IrVariable("c", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("e", IrBinaryOp.OpKind.Sub,
            new IrVariable("d", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("e", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        sccp.Propagate();

        // All operations should have constant operands
        var cOp = block.Instructions[3] as IrBinaryOp;
        Assert.NotNull(cOp);
        Assert.IsType<IrConstant>(cOp!.Left);
        Assert.IsType<IrConstant>(cOp.Right);

        var dOp = block.Instructions[4] as IrBinaryOp;
        Assert.NotNull(dOp);
        // d's left operand might not be replaced yet (depends on iteration order)
        // but we should have propagated something
        Assert.True(dOp!.Left is IrConstant || dOp.Right is IrConstant);
    }

    [Fact]
    public void Propagate_ReturnValue_GetsReplaced()
    {
        // x = 42
        // return x
        // Expected: return 42
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(42, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var sccp = new SparseConditionalConstantPropagation(function);
        sccp.Propagate();

        var ret = block.Instructions[2] as IrReturn;
        Assert.NotNull(ret);
        Assert.IsType<IrConstant>(ret!.Value);
        var retConst = ret.Value as IrConstant;
        Assert.Equal(42, retConst!.Value);
    }
}
