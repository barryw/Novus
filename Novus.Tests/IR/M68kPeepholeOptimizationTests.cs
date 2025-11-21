using Xunit;
using Novus.IR;

namespace Novus.Tests.IR;

public class M68kPeepholeOptimizationTests
{
    [Fact]
    public void Optimize_MultiplyByMinusOne_ConvertedToNegation()
    {
        // x * -1 should become 0 - x (NEG instruction on 68k)
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(-1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        // Should be converted to subtraction: 0 - x
        var binOp = block.Instructions[1] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.Equal(IrBinaryOp.OpKind.Sub, binOp!.Operation);
        Assert.IsType<IrConstant>(binOp.Left);
        var leftConst = binOp.Left as IrConstant;
        Assert.Equal(0, leftConst!.Value);
    }

    [Fact]
    public void Optimize_MultiplyByMinusOne_LeftOperand_ConvertedToNegation()
    {
        // -1 * x should also become 0 - x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Mul,
            new IrConstant(-1, IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        var binOp = block.Instructions[1] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.Equal(IrBinaryOp.OpKind.Sub, binOp!.Operation);
    }

    [Fact]
    public void Optimize_ShiftLeftByOne_OnM68000_ConvertedToAdd()
    {
        // x << 1 on 68000 should become x + x (faster)
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Shl,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68000);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        // Should be converted to addition: x + x
        var binOp = block.Instructions[1] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.Equal(IrBinaryOp.OpKind.Add, binOp!.Operation);

        // Both operands should be the same variable
        var leftVar = binOp.Left as IrVariable;
        var rightVar = binOp.Right as IrVariable;
        Assert.NotNull(leftVar);
        Assert.NotNull(rightVar);
        Assert.Equal(leftVar!.Name, rightVar!.Name);
    }

    [Fact]
    public void Optimize_ShiftLeftByOne_OnM68020_NotConverted()
    {
        // x << 1 on 68020 should NOT be converted (shift is fine)
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Shl,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        // Should NOT optimize (shift is fine on 68020+)
        Assert.Equal(0, count);

        var binOp = block.Instructions[1] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.Equal(IrBinaryOp.OpKind.Shl, binOp!.Operation);
    }

    [Fact]
    public void Optimize_SubtractZero_EliminatedToCopy()
    {
        // x - 0 should become x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Sub,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        // Should be a store: result = x
        var store = block.Instructions[1] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("result", store!.VariableName);
        var value = store.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Optimize_OrWithZero_EliminatedToCopy()
    {
        // x | 0 should become x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Or,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        var store = block.Instructions[1] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("result", store!.VariableName);
    }

    [Fact]
    public void Optimize_OrWithZero_LeftOperand_EliminatedToCopy()
    {
        // 0 | x should also become x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Or,
            new IrConstant(0, IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        var store = block.Instructions[1] as IrStore;
        Assert.NotNull(store);
        var value = store!.Value as IrVariable;
        Assert.Equal("x", value!.Name);
    }

    [Fact]
    public void Optimize_AndWithMinusOne_EliminatedToCopy()
    {
        // x & -1 (all bits set) should become x
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.And,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(-1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        var store = block.Instructions[1] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("result", store!.VariableName);
    }

    [Fact]
    public void Optimize_CopyPropagation_SimplePair()
    {
        // x = y; z = x should become x = y; z = y
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrStore("x", new IrVariable("y", IrIntType.I32)));
        block.Instructions.Add(new IrStore("z", new IrVariable("x", IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        // Second store should now reference y directly
        var store2 = block.Instructions[2] as IrStore;
        Assert.NotNull(store2);
        Assert.Equal("z", store2!.VariableName);
        var value = store2.Value as IrVariable;
        Assert.Equal("y", value!.Name);
    }

    [Fact]
    public void Optimize_InlineOperands_TriplePattern()
    {
        // x = a; y = b; z = x + y should become x = a; y = b; z = a + b
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrStore("x", new IrVariable("a", IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrVariable("b", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.True(count > 0);

        // Binary op should now use a and b directly
        var binOp = block.Instructions[3] as IrBinaryOp;
        Assert.NotNull(binOp);
        var left = binOp!.Left as IrVariable;
        var right = binOp.Right as IrVariable;
        Assert.Equal("a", left!.Name);
        Assert.Equal("b", right!.Name);
    }

    [Fact]
    public void Optimize_MultipleIterations_ConvergesToFixpoint()
    {
        // x * -1 creates 0 - x, which might enable further optimizations
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(-1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Or,
            new IrVariable("y", IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        // Should optimize both operations
        Assert.True(count >= 2);
    }

    [Fact]
    public void Optimize_NoOptimizablePatterns_ReturnsZero()
    {
        // Regular operations that shouldn't be optimized
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var peephole = new M68kPeepholeOptimization(function, M68kCpuTarget.M68020);
        int count = peephole.Optimize();

        Assert.Equal(0, count);
    }
}
