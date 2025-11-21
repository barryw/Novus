using Novus.IR;
using Xunit;

namespace Novus.Tests.IR;

/// <summary>
/// Tests for SSA (Static Single Assignment) construction
/// </summary>
public class SsaConstructorTests
{
    [Fact]
    public void ConstructSsa_SimpleAssignment_CreatesVersionedVariables()
    {
        // Test: x = 10; return x;
        // Expected SSA: x_0 = 10; return x_0;

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // Check that x was renamed to x_0
        var decl = block.Instructions[1] as IrLocalDecl;
        Assert.NotNull(decl);
        Assert.Equal("x_0", decl.Name);

        var ret = block.Instructions[2] as IrReturn;
        Assert.NotNull(ret);
        var retVar = ret.Value as IrVariable;
        Assert.NotNull(retVar);
        Assert.Equal("x", retVar.Name);
        Assert.Equal(0, retVar.Version);
        Assert.True(retVar.IsInSsaForm);
    }

    [Fact]
    public void ConstructSsa_MultipleAssignments_CreatesDifferentVersions()
    {
        // Test: x = 10; x = 20; return x;
        // Expected SSA: x_0 = 10; x_1 = 20; return x_1;

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrStore("x", new IrConstant(20, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // Check that first assignment is x_0
        var decl = block.Instructions[1] as IrLocalDecl;
        Assert.NotNull(decl);
        Assert.Equal("x_0", decl.Name);

        // Check that second assignment is x_1
        var store = block.Instructions[2] as IrStore;
        Assert.NotNull(store);
        Assert.Equal("x_1", store.VariableName);

        // Check that return uses x_1
        var ret = block.Instructions[3] as IrReturn;
        Assert.NotNull(ret);
        var retVar = ret.Value as IrVariable;
        Assert.NotNull(retVar);
        Assert.Equal("x", retVar.Name);
        Assert.Equal(1, retVar.Version);
    }

    [Fact]
    public void ConstructSsa_IfThenElse_InsertsPhi()
    {
        // Test: if (cond) x = 10 else x = 20; return x;
        // Expected: phi at merge point

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

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // Check that merge block has a phi function
        Assert.NotEmpty(merge.PhiFunctions);
        var phi = merge.PhiFunctions[0];

        // Phi should merge values from then and else blocks
        Assert.Equal(2, phi.IncomingValues.Count);
        Assert.Equal(2, phi.IncomingBlocks.Count);

        // Check that return uses the phi result
        var ret = merge.Instructions[1] as IrReturn;
        Assert.NotNull(ret);
        var retVar = ret.Value as IrVariable;
        Assert.NotNull(retVar);
        Assert.True(retVar.IsInSsaForm);
    }

    [Fact]
    public void ConstructSsa_Loop_InsertsPhiAtLoopHeader()
    {
        // Test: i = 0; while (i < 10) { i = i + 1; } return i;
        // Expected: phi at loop header merging initial value and loop value

        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var loop = new IrBasicBlock("loop");
        var exit = new IrBasicBlock("exit");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrLocalDecl("i", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        entry.Instructions.Add(new IrBranch("loop"));

        loop.Instructions.Add(new IrLabel("loop"));
        // i < 10
        loop.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Lt,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrBoolType.Instance));
        loop.Instructions.Add(new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "body", "exit"));

        var body = new IrBasicBlock("body");
        body.Instructions.Add(new IrLabel("body"));
        // i = i + 1
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

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // Loop header should have phi functions for variables modified in the loop
        Assert.NotEmpty(loop.PhiFunctions);

        // There should be a phi for 'i' since it's modified in the loop
        var iPhi = loop.PhiFunctions.FirstOrDefault(p => p.Destination.Name == "i");
        Assert.NotNull(iPhi);

        // The phi should have two incoming values: from entry and from body
        Assert.Equal(2, iPhi.IncomingValues.Count);
    }

    [Fact]
    public void ConstructSsa_BinaryOperation_RenamesOperands()
    {
        // Test: x = 5; y = 10; z = x + y; return z;
        // Expected: all variables get version 0

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("z", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // Check binary operation has versioned operands
        var binOp = block.Instructions[3] as IrBinaryOp;
        Assert.NotNull(binOp);

        var leftVar = binOp.Left as IrVariable;
        Assert.NotNull(leftVar);
        Assert.Equal("x", leftVar.Name);
        Assert.Equal(0, leftVar.Version);

        var rightVar = binOp.Right as IrVariable;
        Assert.NotNull(rightVar);
        Assert.Equal("y", rightVar.Name);
        Assert.Equal(0, rightVar.Version);

        // Result should also be versioned
        Assert.Equal("z_0", binOp.ResultName);
    }

    [Fact]
    public void ConstructSsa_MultipleVariables_VersionsIndependently()
    {
        // Test: x = 1; y = 2; x = 3; y = 4; return x + y;
        // Expected: x_0, y_0, x_1, y_1, return x_1 + y_1

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(1, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(2, IrIntType.I32)));
        block.Instructions.Add(new IrStore("x", new IrConstant(3, IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrConstant(4, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("result", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("result", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // First declarations
        Assert.Equal("x_0", (block.Instructions[1] as IrLocalDecl)!.Name);
        Assert.Equal("y_0", (block.Instructions[2] as IrLocalDecl)!.Name);

        // Second assignments
        Assert.Equal("x_1", (block.Instructions[3] as IrStore)!.VariableName);
        Assert.Equal("y_1", (block.Instructions[4] as IrStore)!.VariableName);

        // Binary operation uses latest versions
        var binOp = block.Instructions[5] as IrBinaryOp;
        Assert.NotNull(binOp);
        Assert.Equal(1, (binOp.Left as IrVariable)!.Version);
        Assert.Equal(1, (binOp.Right as IrVariable)!.Version);
    }

    [Fact]
    public void ConstructSsa_NestedIf_InsertsPhisCorrectly()
    {
        // Test: if (a) { if (b) x = 1 else x = 2 } else x = 3; return x;
        // Expected: phis at inner merge and outer merge

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

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // Inner merge should have a phi
        Assert.NotEmpty(innerMerge.PhiFunctions);

        // Outer merge should have a phi
        Assert.NotEmpty(outerMerge.PhiFunctions);
    }

    [Fact]
    public void ConstructSsa_NoAssignments_NoPhis()
    {
        // Test: return 42;
        // Expected: no phis, no versioned variables

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrReturn(new IrConstant(42, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // No phi functions should be created
        Assert.Empty(block.PhiFunctions);
    }

    [Fact]
    public void ConstructSsa_SingleBlock_NoPhis()
    {
        // Test: x = 1; y = x + 1; return y;
        // Expected: no phis (single block, no control flow merge)

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(1, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var constructor = new SsaConstructor(function);
        constructor.ConstructSsa();

        // No phi functions in a single basic block
        Assert.Empty(block.PhiFunctions);

        // But variables should still be versioned
        Assert.Equal("x_0", (block.Instructions[1] as IrLocalDecl)!.Name);
        Assert.Equal("y_0", (block.Instructions[2] as IrBinaryOp)!.ResultName);
    }
}
