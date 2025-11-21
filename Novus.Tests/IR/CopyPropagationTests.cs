using Xunit;
using Novus.IR;

namespace Novus.Tests.IR;

public class CopyPropagationTests
{
    [Fact]
    public void Propagate_SimpleCopy_ReplacesUses()
    {
        // Test: x = 5; y = x; z = y;
        // Expected: z uses x instead of y

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrVariable("x", IrIntType.I32)));
        block.Instructions.Add(new IrStore("z", new IrVariable("y", IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        // z = y should now be z = x
        var zStore = block.Instructions[3] as IrStore;
        Assert.Equal("z", zStore!.VariableName);
        var zValue = zStore.Value as IrVariable;
        Assert.Equal("x", zValue!.Name);

        // Return should use x
        var ret = block.Instructions[4] as IrReturn;
        var retVar = ret!.Value as IrVariable;
        Assert.Equal("x", retVar!.Name);
    }

    [Fact]
    public void Propagate_ChainedCopies_FollowsChain()
    {
        // Test: x = 5; y = x; z = y; w = z;
        // Expected: all should use x

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrVariable("x", IrIntType.I32)));
        block.Instructions.Add(new IrStore("z", new IrVariable("y", IrIntType.I32)));
        block.Instructions.Add(new IrStore("w", new IrVariable("z", IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("w", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        // All copies should now reference x
        var wStore = block.Instructions[4] as IrStore;
        var wValue = wStore!.Value as IrVariable;
        Assert.Equal("x", wValue!.Name);

        var ret = block.Instructions[5] as IrReturn;
        var retVar = ret!.Value as IrVariable;
        Assert.Equal("x", retVar!.Name);
    }

    [Fact]
    public void Propagate_BinaryOpWithCopy_ReplacesOperands()
    {
        // Test: x = 5; y = x; z = y + 3;
        // Expected: z = x + 3

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrVariable("x", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Add,
            new IrVariable("y", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        var binOp = block.Instructions[3] as IrBinaryOp;
        var leftVar = binOp!.Left as IrVariable;
        Assert.Equal("x", leftVar!.Name);
    }

    [Fact]
    public void Propagate_BothOperandsCopied_ReplacesBoth()
    {
        // Test: x = 5; y = 10; a = x; b = y; c = a + b;
        // Expected: c = x + y

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrStore("a", new IrVariable("x", IrIntType.I32)));
        block.Instructions.Add(new IrStore("b", new IrVariable("y", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("c", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("c", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        var binOp = block.Instructions[5] as IrBinaryOp;
        var leftVar = binOp!.Left as IrVariable;
        var rightVar = binOp.Right as IrVariable;
        Assert.Equal("x", leftVar!.Name);
        Assert.Equal("y", rightVar!.Name);
    }

    [Fact]
    public void Propagate_CallArguments_PropagatesCopies()
    {
        // Test: x = 5; y = x; call(y, y);
        // Expected: call(x, x);

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrVariable("x", IrIntType.I32)));

        var call = new IrCall("foo", IrIntType.I32, "result");
        call.Arguments.Add(new IrVariable("y", IrIntType.I32));
        call.Arguments.Add(new IrVariable("y", IrIntType.I32));
        block.Instructions.Add(call);

        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        var callInstr = block.Instructions[3] as IrCall;
        var arg1 = callInstr!.Arguments[0] as IrVariable;
        var arg2 = callInstr.Arguments[1] as IrVariable;
        Assert.Equal("x", arg1!.Name);
        Assert.Equal("x", arg2!.Name);
    }

    [Fact]
    public void Propagate_ConditionalBranch_PropagatesCondition()
    {
        // Test: x = true; y = x; if (y) then ... else ...
        // Expected: if (x) then ... else ...

        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrLocalDecl("x", IrBoolType.Instance, false, new IrConstant(1L, IrBoolType.Instance)));
        entry.Instructions.Add(new IrStore("y", new IrVariable("x", IrBoolType.Instance)));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("y", IrBoolType.Instance), "then", "else"));

        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrReturn(new IrConstant(1, IrIntType.I32)));

        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        var branch = entry.Instructions[3] as IrConditionalBranch;
        var condVar = branch!.Condition as IrVariable;
        Assert.Equal("x", condVar!.Name);
    }

    [Fact]
    public void Propagate_NoCopies_DoesNothing()
    {
        // Test: x = 5; y = x + 3; return y;
        // Expected: No changes (y is not a copy)

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

        var propagation = new CopyPropagation(function);
        int substitutions = propagation.Propagate();

        Assert.Equal(0, substitutions);

        // Return should still use y (not propagated)
        var ret = block.Instructions[3] as IrReturn;
        var retVar = ret!.Value as IrVariable;
        Assert.Equal("y", retVar!.Name);
    }

    [Fact]
    public void Propagate_CopyOfConstant_DoesNotPropagate()
    {
        // Test: x = 5; y = x + 3;
        // Expected: No copy propagation (x is assigned a constant, not a variable)

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

        var propagation = new CopyPropagation(function);
        int substitutions = propagation.Propagate();

        Assert.Equal(0, substitutions);
    }

    [Fact]
    public void Propagate_WithSSA_WorksCorrectly()
    {
        // Test copy propagation with SSA versioning

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        var x0 = new IrVariable("x", 0, IrIntType.I32);
        var y0 = new IrVariable("y", 0, IrIntType.I32);
        var z0 = new IrVariable("z", 0, IrIntType.I32);

        block.Instructions.Add(new IrLocalDecl(x0.SsaName, IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrStore(y0.SsaName, x0));
        block.Instructions.Add(new IrStore(z0.SsaName, y0));
        block.Instructions.Add(new IrReturn(z0));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        // z_0 = y_0 should become z_0 = x_0
        var zStore = block.Instructions[3] as IrStore;
        var zValue = zStore!.Value as IrVariable;
        Assert.Equal("x_0", zValue!.SsaName);

        // Return should use x_0
        var ret = block.Instructions[4] as IrReturn;
        var retVar = ret!.Value as IrVariable;
        Assert.Equal("x_0", retVar!.SsaName);
    }

    [Fact]
    public void Propagate_IteratesUntilFixpoint()
    {
        // Test that propagation iterates until no more changes
        // x = 5; y = x; z = y; (first iteration: z = y becomes z = x)
        // Need second iteration to see that y is also a copy of x

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrVariable("x", IrIntType.I32)));
        block.Instructions.Add(new IrStore("z", new IrVariable("y", IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        // After fixpoint, all should reference x
        var zStore = block.Instructions[3] as IrStore;
        var zValue = zStore!.Value as IrVariable;
        Assert.Equal("x", zValue!.Name);

        var ret = block.Instructions[4] as IrReturn;
        var retVar = ret!.Value as IrVariable;
        Assert.Equal("x", retVar!.Name);
    }

    [Fact]
    public void Propagate_IndexAccess_PropagatesCopies()
    {
        // Test: Create array and index, then copy them: arr = myArray; idx = myIdx; x = arr[idx];
        // Expected: x = myArray[myIdx]
        // Note: myArray and myIdx must not be copies themselves

        var arrayType = new IrArrayType(IrIntType.I32, 10);
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        // Use non-copy declarations (initialized with default value, not a variable)
        block.Instructions.Add(new IrLocalDecl("myArray", arrayType, false, new IrConstant(0, arrayType)));
        block.Instructions.Add(new IrLocalDecl("myIdx", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        block.Instructions.Add(new IrStore("arr", new IrVariable("myArray", arrayType)));
        block.Instructions.Add(new IrStore("idx", new IrVariable("myIdx", IrIntType.I32)));
        block.Instructions.Add(new IrIndexAccess("x", new IrVariable("arr", arrayType), new IrVariable("idx", IrIntType.I32), IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        var indexAccess = block.Instructions[5] as IrIndexAccess;
        var arrayVar = indexAccess!.Array as IrVariable;
        var indexVar = indexAccess.Index as IrVariable;
        Assert.Equal("myArray", arrayVar!.Name);
        Assert.Equal("myIdx", indexVar!.Name);
    }

    [Fact]
    public void Propagate_MemberAccess_PropagatesCopies()
    {
        // Test: s = myStruct; x = s.field;
        // Expected: x = myStruct.field
        // Note: myStruct must not be a copy itself

        var structFields = new List<IrStructField>
        {
            new IrStructField("field", IrIntType.I32)
        };
        var structType = new IrStructType("MyStruct", structFields);
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        // Use non-copy declaration (initialized with default value, not a variable)
        block.Instructions.Add(new IrLocalDecl("myStruct", structType, false, new IrConstant(0, structType)));
        block.Instructions.Add(new IrStore("s", new IrVariable("myStruct", structType)));
        block.Instructions.Add(new IrMemberAccess("x", new IrVariable("s", structType), "field", IrIntType.I32, 0));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        var memberAccess = block.Instructions[3] as IrMemberAccess;
        var structVar = memberAccess!.Struct as IrVariable;
        Assert.Equal("myStruct", structVar!.Name);
    }

    [Fact]
    public void Propagate_WithPhi_PropagatesPhiInputs()
    {
        // Test: phi function inputs that are copies should be propagated
        // if (cond) { x = a } else { x = b }
        // x_2 = phi(x_0, x_1)
        // If x_0 = y_0, then phi should use y_0

        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");
        var merge = new IrBasicBlock("merge");

        // Entry: y = 5
        entry.Instructions.Add(new IrLabel("entry"));
        var y0 = new IrVariable("y", 0, IrIntType.I32);
        entry.Instructions.Add(new IrLocalDecl(y0.SsaName, IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        entry.Instructions.Add(new IrConditionalBranch(new IrConstant(1L, IrBoolType.Instance), "then", "else"));

        // Then: x_0 = y_0 (copy)
        thenBlock.Instructions.Add(new IrLabel("then"));
        var x0 = new IrVariable("x", 0, IrIntType.I32);
        thenBlock.Instructions.Add(new IrStore(x0.SsaName, y0));
        thenBlock.Instructions.Add(new IrBranch("merge"));

        // Else: x_1 = 10
        elseBlock.Instructions.Add(new IrLabel("else"));
        var x1 = new IrVariable("x", 1, IrIntType.I32);
        elseBlock.Instructions.Add(new IrStore(x1.SsaName, new IrConstant(10, IrIntType.I32)));
        elseBlock.Instructions.Add(new IrBranch("merge"));

        // Merge: x_2 = phi(x_0 from then, x_1 from else)
        merge.Instructions.Add(new IrLabel("merge"));
        var x2 = new IrVariable("x", 2, IrIntType.I32);
        var phi = new IrPhi(x2);
        phi.AddIncoming(x0, thenBlock);
        phi.AddIncoming(x1, elseBlock);
        merge.PhiFunctions.Add(phi);
        merge.Instructions.Add(new IrReturn(x2));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);
        function.BasicBlocks.Add(merge);

        var propagation = new CopyPropagation(function);
        propagation.Propagate();

        // Phi should now use y_0 instead of x_0 from the then block
        var phiFunc = merge.PhiFunctions[0];
        var firstValue = phiFunc.IncomingValues[0] as IrVariable;
        Assert.Equal("y_0", firstValue!.SsaName);
    }
}
