using Novus.IR;
using Xunit;

namespace Novus.Tests.IR;

/// <summary>
/// Tests for Constant Propagation optimization
/// </summary>
public class ConstantPropagationTests
{
    [Fact]
    public void Propagate_MutableVariable_DoesNotReuseInitialConstant()
    {
        var function = new IrFunction("test", IrIntType.I32);
        var block = function.CreateBasicBlock("entry");
        block.AddInstruction(new IrLocalDecl("count", IrIntType.I32, true,
            new IrConstant(0, IrIntType.I32)));
        block.AddInstruction(new IrStore("count", new IrConstant(1, IrIntType.I32)));
        block.AddInstruction(new IrBinaryOp("next", IrBinaryOp.OpKind.Add,
            new IrVariable("count", IrIntType.I32), new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.AddInstruction(new IrReturn(new IrVariable("next", IrIntType.I32)));

        new ConstantPropagation(function).Propagate();

        var add = Assert.IsType<IrBinaryOp>(block.Instructions[2]);
        Assert.IsType<IrVariable>(add.Left);
    }

    [Fact]
    public void Propagate_SimpleConstant_ReplacesUses()
    {
        // Test: x = 5; return x;
        // Expected: x = 5; return 5;

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        var count = propagation.Propagate();

        Assert.True(count > 0, "Should have propagated at least one constant");

        // Return should now use constant
        var ret = block.Instructions[2] as IrReturn;
        Assert.NotNull(ret);
        Assert.IsType<IrConstant>(ret.Value);
        var constant = ret.Value as IrConstant;
        Assert.Equal(5, constant!.Value);
    }

    [Fact]
    public void Propagate_BinaryOpWithConstants_FoldsOperation()
    {
        // Test: x = 5; y = x + 3; return y;
        // Expected: x = 5; y = 8; return 8;

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

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // Binary operation should be replaced with constant store: y = 8
        var store = block.Instructions[2] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("y", store.VariableName);
        Assert.IsType<IrConstant>(store.Value);
        var constant = store.Value as IrConstant;
        Assert.Equal(8, constant!.Value);
    }

    [Fact]
    public void Propagate_ChainedOperations_FoldsAll()
    {
        // Test: x = 5; y = x + 3; z = y * 2; return z;
        // Expected: All operations fold to constants

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Mul,
            new IrVariable("y", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // Final result should be constant: 16
        var ret = block.Instructions[^1] as IrReturn;
        Assert.NotNull(ret);
        Assert.IsType<IrConstant>(ret.Value);
        var constant = ret.Value as IrConstant;
        Assert.Equal(16, constant!.Value);
    }

    [Fact]
    public void Propagate_Subtraction_FoldsCorrectly()
    {
        // Test: x = 10; y = x - 3; return y;
        // Expected: y = 7

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Sub,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        var ret = block.Instructions[^1] as IrReturn;
        var constant = ret!.Value as IrConstant;
        Assert.Equal(7, constant!.Value);
    }

    [Fact]
    public void Propagate_Multiplication_FoldsCorrectly()
    {
        // Test: x = 6; y = x * 7; return y;
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

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        var ret = block.Instructions[^1] as IrReturn;
        var constant = ret!.Value as IrConstant;
        Assert.Equal(42, constant!.Value);
    }

    [Fact]
    public void Propagate_Division_FoldsCorrectly()
    {
        // Test: x = 20; y = x / 4; return y;
        // Expected: y = 5

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

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        var ret = block.Instructions[^1] as IrReturn;
        var constant = ret!.Value as IrConstant;
        Assert.Equal(5, constant!.Value);
    }

    [Fact]
    public void Propagate_BitwiseOperations_FoldsCorrectly()
    {
        // Test: x = 12; y = x & 7; return y;
        // Expected: y = 4 (12 & 7 = 0b1100 & 0b0111 = 0b0100)

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(12, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.And,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(7, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        var ret = block.Instructions[^1] as IrReturn;
        var constant = ret!.Value as IrConstant;
        Assert.Equal(4, constant!.Value);
    }

    [Fact]
    public void Propagate_Comparison_FoldsCorrectly()
    {
        // Test: x = 10; y = (x < 20); return y;
        // Expected: y = true

        var function = new IrFunction("test", IrBoolType.Instance);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Lt,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(20, IrIntType.I32),
            IrBoolType.Instance));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrBoolType.Instance)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        var ret = block.Instructions[^1] as IrReturn;
        var constant = ret!.Value as IrConstant;
        Assert.Equal(1L, constant!.Value);
    }

    [Fact]
    public void Propagate_ConditionalBranch_SimplifiesWithConstantTrue()
    {
        // Test: x = true; if (x) then ... else ...
        // Expected: conditional branch is simplified to unconditional branch to "then"
        // because constant propagation now replaces constant-condition branches

        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrLocalDecl("x", IrBoolType.Instance, false, new IrConstant(1L, IrBoolType.Instance)));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("x", IrBoolType.Instance), "then", "else"));

        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrReturn(new IrConstant(1, IrIntType.I32)));

        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // Conditional branch should be replaced with unconditional branch to "then"
        var branch = entry.Instructions[2] as IrBranch;
        Assert.NotNull(branch);
        Assert.Equal("then", branch.Target);
    }

    [Fact]
    public void Propagate_ConditionalBranch_SimplifiesWithConstantFalse()
    {
        // Test: x = false; if (x) then ... else ...
        // Expected: conditional branch is simplified to unconditional branch to "else"

        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrLocalDecl("x", IrBoolType.Instance, false, new IrConstant(0L, IrBoolType.Instance)));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("x", IrBoolType.Instance), "then", "else"));

        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrReturn(new IrConstant(1, IrIntType.I32)));

        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // Conditional branch should be replaced with unconditional branch to "else"
        var branch = entry.Instructions[2] as IrBranch;
        Assert.NotNull(branch);
        Assert.Equal("else", branch.Target);
    }

    [Fact]
    public void Propagate_ConditionalBranch_NonConstant_RemainsBranch()
    {
        // Test: if (y) then ... else ... (where y is a parameter, not constant)
        // Expected: conditional branch remains unchanged

        var function = new IrFunction("test", IrIntType.I32);
        function.Parameters.Add(new IrParameter("y", IrBoolType.Instance));
        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("y", IrBoolType.Instance), "then", "else"));

        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrReturn(new IrConstant(1, IrIntType.I32)));

        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // Conditional branch should remain (parameter is not constant)
        var branch = entry.Instructions[1] as IrConditionalBranch;
        Assert.NotNull(branch);
    }

    [Fact]
    public void Propagate_CallArguments_PropagatesConstants()
    {
        // Test: x = 5; call(x, 10);
        // Expected: call(5, 10);

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));

        var call = new IrCall("foo", IrIntType.I32, "result");
        call.Arguments.Add(new IrVariable("x", IrIntType.I32));
        call.Arguments.Add(new IrConstant(10, IrIntType.I32));
        block.Instructions.Add(call);
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // Call arguments should be propagated
        var callInstr = block.Instructions[2] as IrCall;
        Assert.NotNull(callInstr);
        Assert.Equal(2, callInstr.Arguments.Count);
        Assert.IsType<IrConstant>(callInstr.Arguments[0]);
        var arg0 = callInstr.Arguments[0] as IrConstant;
        Assert.Equal(5, arg0!.Value);
    }

    [Fact]
    public void Propagate_NonConstant_DoesNotPropagate()
    {
        // Test: x = param; y = x + 3; return y;
        // Expected: y still uses x (x is not constant)

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        // x is not a constant (would come from parameter or something)
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // Binary operation should still use variable x
        var binOp = block.Instructions[1] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.IsType<IrVariable>(binOp.Left);
        var leftVar = binOp.Left as IrVariable;
        Assert.Equal("x", leftVar!.Name);
    }

    [Fact]
    public void Propagate_NoConstants_DoesNothing()
    {
        // Test: x = y; z = x + 1; return z;
        // Expected: No changes (no constants to propagate)

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrStore("x", new IrVariable("y", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        var count = propagation.Propagate();

        // Should not propagate anything
        Assert.Equal(0, count);
    }

    [Fact]
    public void Propagate_WithSSA_WorksCorrectly()
    {
        // Test constant propagation after SSA construction
        // if (cond) x = 5 else x = 10; y = x + 1; return y;

        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");
        var merge = new IrBasicBlock("merge");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "then", "else"));

        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrStore("x", new IrConstant(5, IrIntType.I32)));
        thenBlock.Instructions.Add(new IrBranch("merge"));

        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrStore("x", new IrConstant(10, IrIntType.I32)));
        elseBlock.Instructions.Add(new IrBranch("merge"));

        merge.Instructions.Add(new IrLabel("merge"));
        merge.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        merge.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);
        function.BasicBlocks.Add(merge);

        // Convert to SSA
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        // Propagate constants
        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // The constants 5 and 10 should be visible in their respective branches
        // But x in merge comes from phi, so can't be constant-folded
        // This test just verifies no crashes with SSA form
    }

    [Fact]
    public void Propagate_IteratesUntilFixpoint()
    {
        // Test: a = 2; b = a; c = b; d = c + 1; return d;
        // Should require multiple iterations to fully propagate

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(2, IrIntType.I32)));
        block.Instructions.Add(new IrStore("b", new IrVariable("a", IrIntType.I32)));
        block.Instructions.Add(new IrStore("c", new IrVariable("b", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("d", IrBinaryOp.OpKind.Add,
            new IrVariable("c", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("d", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new ConstantPropagation(function);
        propagation.Propagate();

        // Final result should be 3 (2 + 1)
        var ret = block.Instructions[^1] as IrReturn;
        Assert.NotNull(ret);
        Assert.IsType<IrConstant>(ret.Value);
        var constant = ret.Value as IrConstant;
        Assert.Equal(3, constant!.Value);
    }
}
