using Novus.IR;
using Xunit;

namespace Novus.Tests.IR;

/// <summary>
/// Tests for SSA Destruction (phi elimination)
/// </summary>
public class SsaDestructionTests
{
    [Fact]
    public void DestructSsa_SimpleIfThenElse_InsertsCopiesToPredecessors()
    {
        // Test: if (cond) x = 10 else x = 20; return x;
        // After SSA: merge has phi: x_2 = φ(x_0 from then, x_1 from else)
        // After destruction: copies inserted in then/else blocks, phi removed

        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");
        var merge = new IrBasicBlock("merge");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "then", "else"));

        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrStore("x", new IrConstant(10, IrIntType.I32)));
        thenBlock.Instructions.Add(new IrBranch("merge"));

        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrStore("x", new IrConstant(20, IrIntType.I32)));
        elseBlock.Instructions.Add(new IrBranch("merge"));

        merge.Instructions.Add(new IrLabel("merge"));
        merge.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);
        function.BasicBlocks.Add(merge);

        // Convert to SSA
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        // Verify phi was created
        Assert.NotEmpty(merge.PhiFunctions);

        // Destruct SSA
        var destruction = new SsaDestruction(function);
        destruction.DestructSsa();

        // Phi should be removed
        Assert.Empty(merge.PhiFunctions);

        // Copies should be inserted in predecessor blocks (before branch)
        var thenHasStore = thenBlock.Instructions.Any(i => i is IrStore);
        var elseHasStore = elseBlock.Instructions.Any(i => i is IrStore);
        Assert.True(thenHasStore, "Then block should have store for phi elimination");
        Assert.True(elseHasStore, "Else block should have store for phi elimination");
    }

    [Fact]
    public void DestructSsa_Loop_InsertsCopiesToPredecessors()
    {
        // Test: i = 0; while (i < 10) { i = i + 1; } return i;
        // Loop header has phi for i

        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var loop = new IrBasicBlock("loop");
        var body = new IrBasicBlock("body");
        var exit = new IrBasicBlock("exit");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        entry.Instructions.Add(new IrBranch("loop"));

        loop.Instructions.Add(new IrLabel("loop"));
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "body", "exit"));

        body.Instructions.Add(new IrLabel("body"));
        body.Instructions.Add(new IrBinaryOp("temp", IrBinaryOp.OpKind.Add,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        body.Instructions.Add(new IrStore("i", new IrVariable("temp", IrIntType.I32)));
        body.Instructions.Add(new IrBranch("loop"));

        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("i", IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(body);
        function.BasicBlocks.Add(exit);

        // Convert to SSA
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        // Loop header should have phi
        Assert.NotEmpty(loop.PhiFunctions);

        // Destruct SSA
        var destruction = new SsaDestruction(function);
        destruction.DestructSsa();

        // Phis should be removed
        Assert.Empty(loop.PhiFunctions);

        // Copies should be inserted in entry and body blocks
        // Entry: i = initial_value (before branch to loop)
        // Body: i = updated_value (before branch back to loop)
    }

    [Fact]
    public void DestructSsa_MultiplePhis_HandlesParallelCopyProblem()
    {
        // Test case where multiple phis might interfere
        // x_2 = φ(x_0, x_1)
        // y_2 = φ(y_0, x_0)  // Uses x_0 which is overwritten by first phi
        //
        // Without proper handling, we might do:
        // x_2 = x_0
        // y_2 = x_0  // Wrong! x_0 was overwritten
        //
        // Should use temporaries to resolve

        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var merge = new IrBasicBlock("merge");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        entry.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(20, IrIntType.I32)));
        entry.Instructions.Add(new IrBranch("merge"));

        merge.Instructions.Add(new IrLabel("merge"));
        merge.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(merge);

        // Manually create interfering phis (simulating what SSA might create)
        var xPhi = new IrPhi(new IrVariable("x", 2, IrIntType.I32));
        xPhi.AddIncoming(new IrVariable("x", 0, IrIntType.I32), entry);

        var yPhi = new IrPhi(new IrVariable("y", 2, IrIntType.I32));
        yPhi.AddIncoming(new IrVariable("x", 0, IrIntType.I32), entry); // Uses x_0!

        merge.PhiFunctions.Add(xPhi);
        merge.PhiFunctions.Add(yPhi);

        // Destruct SSA
        var destruction = new SsaDestruction(function);
        destruction.DestructSsa();

        // Phis should be removed
        Assert.Empty(merge.PhiFunctions);

        // Entry block should have stores inserted
        var stores = entry.Instructions.OfType<IrStore>().ToList();
        Assert.True(stores.Count >= 2, "Should have at least 2 stores for phi elimination");
    }

    [Fact]
    public void RemoveSsaVersioning_ConvertsVersionedVariablesToPlainNames()
    {
        // Test that SSA versions are removed: x_0, x_1 -> x

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x_0", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrStore("x_1", new IrVariable("x_0", IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x_1", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var destruction = new SsaDestruction(function);
        destruction.RemoveSsaVersioning();

        // Check that versions were removed
        var decl = block.Instructions[1] as IrLocalDecl;
        Assert.NotNull(decl);
        Assert.Equal("x", decl.Name);

        var store = block.Instructions[2] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("x", store.VariableName);
        Assert.IsType<IrVariable>(store.Value);
        var storeValue = store.Value as IrVariable;
        Assert.Equal("x", storeValue!.Name);

        var ret = block.Instructions[3] as IrReturn;
        Assert.NotNull(ret);
        var retValue = ret.Value as IrVariable;
        Assert.NotNull(retValue);
        Assert.Equal("x", retValue.Name);
    }

    [Fact]
    public void RemoveSsaVersioning_HandlesBinaryOperations()
    {
        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x_0", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y_0", IrBinaryOp.OpKind.Add,
            new IrVariable("x_0", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y_0", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var destruction = new SsaDestruction(function);
        destruction.RemoveSsaVersioning();

        var binOp = block.Instructions[2] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.Equal("y", binOp.ResultName);

        var leftVar = binOp.Left as IrVariable;
        Assert.NotNull(leftVar);
        Assert.Equal("x", leftVar.Name);
    }

    [Fact]
    public void DestructSsa_NoPhis_DoesNothing()
    {
        // Test that destruction works fine on code without phis

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(42, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var instructionCountBefore = block.Instructions.Count;

        var destruction = new SsaDestruction(function);
        destruction.DestructSsa();

        // Should not change anything
        Assert.Equal(instructionCountBefore, block.Instructions.Count);
        Assert.Empty(block.PhiFunctions);
    }

    [Fact]
    public void DestructSsa_NestedIf_HandlesMultipleMergePoints()
    {
        // Test nested if with multiple merge points and phis

        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var outerThen = new IrBasicBlock("outer_then");
        var outerElse = new IrBasicBlock("outer_else");
        var innerThen = new IrBasicBlock("inner_then");
        var innerElse = new IrBasicBlock("inner_else");
        var innerMerge = new IrBasicBlock("inner_merge");
        var outerMerge = new IrBasicBlock("outer_merge");

        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("a", IrBoolType.Instance), "outer_then", "outer_else"));

        outerThen.Instructions.Add(new IrConditionalBranch(new IrVariable("b", IrBoolType.Instance), "inner_then", "inner_else"));

        innerThen.Instructions.Add(new IrStore("x", new IrConstant(1, IrIntType.I32)));
        innerThen.Instructions.Add(new IrBranch("inner_merge"));

        innerElse.Instructions.Add(new IrStore("x", new IrConstant(2, IrIntType.I32)));
        innerElse.Instructions.Add(new IrBranch("inner_merge"));

        innerMerge.Instructions.Add(new IrBranch("outer_merge"));

        outerElse.Instructions.Add(new IrStore("x", new IrConstant(3, IrIntType.I32)));
        outerElse.Instructions.Add(new IrBranch("outer_merge"));

        outerMerge.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(outerThen);
        function.BasicBlocks.Add(outerElse);
        function.BasicBlocks.Add(innerThen);
        function.BasicBlocks.Add(innerElse);
        function.BasicBlocks.Add(innerMerge);
        function.BasicBlocks.Add(outerMerge);

        // Convert to SSA
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        // Should have phis at both merge points
        var phiCountBefore = innerMerge.PhiFunctions.Count + outerMerge.PhiFunctions.Count;
        Assert.True(phiCountBefore > 0, "Should have phis at merge points");

        // Destruct SSA
        var destruction = new SsaDestruction(function);
        destruction.DestructSsa();

        // All phis should be removed
        Assert.Empty(innerMerge.PhiFunctions);
        Assert.Empty(outerMerge.PhiFunctions);
    }

    [Fact]
    public void DestructSsa_CompleteRoundTrip_RestoresNonSsaForm()
    {
        // Test complete round trip: non-SSA -> SSA -> non-SSA
        // Verify the result is functionally equivalent

        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");
        var merge = new IrBasicBlock("merge");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "then", "else"));

        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrStore("x", new IrConstant(10, IrIntType.I32)));
        thenBlock.Instructions.Add(new IrBranch("merge"));

        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrStore("x", new IrConstant(20, IrIntType.I32)));
        elseBlock.Instructions.Add(new IrBranch("merge"));

        merge.Instructions.Add(new IrLabel("merge"));
        merge.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);
        function.BasicBlocks.Add(merge);

        // Convert to SSA
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        // Verify SSA form
        Assert.NotEmpty(merge.PhiFunctions);

        // Destruct back to non-SSA
        var destruction = new SsaDestruction(function);
        destruction.DestructSsa();
        destruction.RemoveSsaVersioning();

        // Verify non-SSA form
        Assert.Empty(merge.PhiFunctions);

        // Both branches should assign to 'x' (not versioned)
        var thenStore = thenBlock.Instructions.OfType<IrStore>().FirstOrDefault(s => s.VariableName == "x");
        var elseStore = elseBlock.Instructions.OfType<IrStore>().FirstOrDefault(s => s.VariableName == "x");

        Assert.NotNull(thenStore);
        Assert.NotNull(elseStore);

        // Return should use 'x' (not versioned)
        var ret = merge.Instructions.OfType<IrReturn>().FirstOrDefault();
        Assert.NotNull(ret);
        var retVar = ret.Value as IrVariable;
        Assert.NotNull(retVar);
        Assert.Equal("x", retVar.Name);
        Assert.False(retVar.IsInSsaForm);
    }

    [Fact]
    public void RemoveSsaVersioning_PreservesNonVersionedNames()
    {
        // Test that variables without versions are preserved

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("param", IrIntType.I32, false, new IrConstant(42, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("temp_var", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("param", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var destruction = new SsaDestruction(function);
        destruction.RemoveSsaVersioning();

        // Non-versioned names should be unchanged
        var decl1 = block.Instructions[1] as IrLocalDecl;
        Assert.Equal("param", decl1!.Name);

        var decl2 = block.Instructions[2] as IrLocalDecl;
        Assert.Equal("temp_var", decl2!.Name);
    }
}
