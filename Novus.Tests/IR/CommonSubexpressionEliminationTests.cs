using Xunit;
using Novus.IR;

namespace Novus.Tests.IR;

public class CommonSubexpressionEliminationTests
{
    [Fact]
    public void Eliminate_IdenticalAdditions_ReusesFirst()
    {
        // a = x + y
        // b = x + y  // Same expression
        // Expected: b = a
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        Assert.Equal(1, count);

        // Second instruction should now be a copy: b = a
        var store = block.Instructions[2] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("b", store!.VariableName);
        var value = store.Value as IrVariable;
        Assert.Equal("a", value!.Name);
    }

    [Fact]
    public void Eliminate_CommutativeOperation_RecognizesBothOrders()
    {
        // a = x + y
        // b = y + x  // Commutative: should recognize as same expression
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Add,
            new IrVariable("y", IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        Assert.Equal(1, count);

        // Should eliminate the second addition
        var store = block.Instructions[2] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("b", store!.VariableName);
    }

    [Fact]
    public void Eliminate_NonCommutativeOperation_DoesNotRecognizeDifferentOrders()
    {
        // a = x - y
        // b = y - x  // NOT the same! Subtraction is not commutative
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Sub,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Sub,
            new IrVariable("y", IrIntType.I32),
            new IrVariable("x", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        // Should NOT eliminate - these are different expressions
        Assert.Equal(0, count);
    }

    [Fact]
    public void Eliminate_WithConstants_WorksCorrectly()
    {
        // a = x + 5
        // b = x + 5  // Same constant
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        Assert.Equal(1, count);
    }

    [Fact]
    public void Eliminate_DifferentConstants_DoesNotEliminate()
    {
        // a = x + 5
        // b = x + 10  // Different constant
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        Assert.Equal(0, count);
    }

    [Fact]
    public void Eliminate_TripleOccurrence_EliminatesSecondAndThird()
    {
        // a = x + y
        // b = x + y  // Should reuse a
        // c = x + y  // Should also reuse a
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("c", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("c", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        Assert.Equal(2, count);

        // Both b and c should be copies of a
        var bStore = block.Instructions[2] as IrStore;
        Assert.NotNull(bStore);
        var bValue = bStore!.Value as IrVariable;
        Assert.Equal("a", bValue!.Name);

        var cStore = block.Instructions[3] as IrStore;
        Assert.NotNull(cStore);
        var cValue = cStore!.Value as IrVariable;
        Assert.Equal("a", cValue!.Name);
    }

    [Fact]
    public void Eliminate_FunctionCallInvalidates_ClearsAvailableExpressions()
    {
        // a = x + y
        // call foo()  // Invalidates available expressions
        // b = x + y   // Should NOT reuse a (x or y might have changed)
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrCall("foo", IrVoidType.Instance));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        // Should NOT eliminate because function call invalidates
        Assert.Equal(0, count);
    }

    [Fact]
    public void Eliminate_BitwiseAnd_Eliminates()
    {
        // a = x & y
        // b = x & y
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.And,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.And,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        Assert.Equal(1, count);
    }

    [Fact]
    public void Eliminate_Multiplication_Eliminates()
    {
        // a = x * y
        // b = x * y
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        Assert.Equal(1, count);
    }

    [Fact]
    public void Eliminate_DivisionNotEliminated_MayTrap()
    {
        // Division is not considered pure because it can trap on divide by zero
        // a = x / y
        // b = x / y  // Should NOT eliminate (conservative)
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Div,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Div,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        // Division is not eliminated because it's not considered pure
        Assert.Equal(0, count);
    }

    [Fact]
    public void Eliminate_Comparisons_Eliminates()
    {
        // a = x < y
        // b = x < y
        var function = new IrFunction("test", IrBoolType.Instance);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.Lt,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrBoolType.Instance));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.Lt,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrBoolType.Instance));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrBoolType.Instance)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        Assert.Equal(1, count);
    }

    [Fact]
    public void Eliminate_ComplexExpression_Eliminates()
    {
        // a = (x + y) & z
        // b = (x + y) & z
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrBinaryOp("temp1", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("a", IrBinaryOp.OpKind.And,
            new IrVariable("temp1", IrIntType.I32),
            new IrVariable("z", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("temp2", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("b", IrBinaryOp.OpKind.And,
            new IrVariable("temp2", IrIntType.I32),
            new IrVariable("z", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("b", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var cse = new CommonSubexpressionElimination(function);
        int count = cse.Eliminate();

        // Should eliminate both temp2 = x+y and b = temp&z
        Assert.True(count >= 1);
    }
}
