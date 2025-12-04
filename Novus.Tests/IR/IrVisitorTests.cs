using Novus.IR;
using Xunit;

namespace Novus.Tests.IR;

/// <summary>
/// Tests for the IrVisitor base class
/// Tests visitor pattern implementation for IR traversal
/// </summary>
public class IrVisitorTests
{
    /// <summary>
    /// Concrete visitor implementation that counts visited nodes
    /// </summary>
    private class CountingVisitor : IrVisitor<int, object?>
    {
        public int ModuleCount { get; private set; }
        public int FunctionCount { get; private set; }
        public int BasicBlockCount { get; private set; }
        public int InstructionCount { get; private set; }
        public int BinaryOpCount { get; private set; }
        public int CallCount { get; private set; }
        public int ReturnCount { get; private set; }
        public int StoreCount { get; private set; }
        public int LocalDeclCount { get; private set; }
        public int LabelCount { get; private set; }
        public int BranchCount { get; private set; }
        public int ConditionalBranchCount { get; private set; }
        public int PhiCount { get; private set; }

        public override int VisitModule(IrModule module, object? context)
        {
            ModuleCount++;
            return base.VisitModule(module, context);
        }

        public override int VisitFunction(IrFunction function, object? context)
        {
            FunctionCount++;
            return base.VisitFunction(function, context);
        }

        public override int VisitBasicBlock(IrBasicBlock block, object? context)
        {
            BasicBlockCount++;
            return base.VisitBasicBlock(block, context);
        }

        public override int VisitInstruction(IrInstruction instruction, object? context)
        {
            InstructionCount++;
            return base.VisitInstruction(instruction, context);
        }

        public override int VisitBinaryOp(IrBinaryOp binaryOp, object? context)
        {
            BinaryOpCount++;
            return base.VisitBinaryOp(binaryOp, context);
        }

        public override int VisitCall(IrCall call, object? context)
        {
            CallCount++;
            return base.VisitCall(call, context);
        }

        public override int VisitReturn(IrReturn ret, object? context)
        {
            ReturnCount++;
            return base.VisitReturn(ret, context);
        }

        public override int VisitStore(IrStore store, object? context)
        {
            StoreCount++;
            return base.VisitStore(store, context);
        }

        public override int VisitLocalDecl(IrLocalDecl localDecl, object? context)
        {
            LocalDeclCount++;
            return base.VisitLocalDecl(localDecl, context);
        }

        public override int VisitLabel(IrLabel label, object? context)
        {
            LabelCount++;
            return base.VisitLabel(label, context);
        }

        public override int VisitBranch(IrBranch branch, object? context)
        {
            BranchCount++;
            return base.VisitBranch(branch, context);
        }

        public override int VisitConditionalBranch(IrConditionalBranch condBranch, object? context)
        {
            ConditionalBranchCount++;
            return base.VisitConditionalBranch(condBranch, context);
        }

        public override int VisitPhi(IrPhi phi, object? context)
        {
            PhiCount++;
            return base.VisitPhi(phi, context);
        }
    }

    /// <summary>
    /// Visitor that collects variable names from local declarations
    /// </summary>
    private class VariableCollector : IrVisitor<List<string>, List<string>>
    {
        public override List<string> VisitLocalDecl(IrLocalDecl localDecl, List<string> context)
        {
            context.Add(localDecl.Name);
            return base.VisitLocalDecl(localDecl, context);
        }
    }

    [Fact]
    public void VisitModule_EmptyModule_CallsVisitModule()
    {
        var module = new IrModule();
        var visitor = new CountingVisitor();

        visitor.VisitModule(module, null);

        Assert.Equal(1, visitor.ModuleCount);
        Assert.Equal(0, visitor.FunctionCount);
    }

    [Fact]
    public void VisitModule_WithFunctions_VisitsAllFunctions()
    {
        var module = new IrModule();
        module.Functions.Add(new IrFunction("func1", IrVoidType.Instance));
        module.Functions.Add(new IrFunction("func2", IrVoidType.Instance));
        module.Functions.Add(new IrFunction("func3", IrVoidType.Instance));

        var visitor = new CountingVisitor();
        visitor.VisitModule(module, null);

        Assert.Equal(1, visitor.ModuleCount);
        Assert.Equal(3, visitor.FunctionCount);
    }

    [Fact]
    public void VisitModule_WithEnums_VisitsEnums()
    {
        var module = new IrModule();
        module.Enums.Add(new IrEnumType("Color", new List<IrEnumVariant>()));

        var visitor = new CountingVisitor();
        visitor.VisitModule(module, null);

        Assert.Equal(1, visitor.ModuleCount);
    }

    [Fact]
    public void VisitModule_WithStructs_VisitsStructs()
    {
        var module = new IrModule();
        module.AddStruct(new IrStructType("Point", new List<IrStructField>()));

        var visitor = new CountingVisitor();
        visitor.VisitModule(module, null);

        Assert.Equal(1, visitor.ModuleCount);
    }

    [Fact]
    public void VisitFunction_EmptyFunction_CallsVisitFunction()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var visitor = new CountingVisitor();

        visitor.VisitFunction(function, null);

        Assert.Equal(1, visitor.FunctionCount);
        Assert.Equal(0, visitor.BasicBlockCount);
    }

    [Fact]
    public void VisitFunction_WithBasicBlocks_VisitsAllBlocks()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        function.BasicBlocks.Add(new IrBasicBlock("entry"));
        function.BasicBlocks.Add(new IrBasicBlock("loop"));
        function.BasicBlocks.Add(new IrBasicBlock("exit"));

        var visitor = new CountingVisitor();
        visitor.VisitFunction(function, null);

        Assert.Equal(1, visitor.FunctionCount);
        Assert.Equal(3, visitor.BasicBlockCount);
    }

    [Fact]
    public void VisitFunction_WithDeferredBlocks_VisitsDeferredBlocks()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        function.BasicBlocks.Add(new IrBasicBlock("entry"));
        function.DeferredBlocks.Add(new IrBasicBlock("defer1"));
        function.DeferredBlocks.Add(new IrBasicBlock("defer2"));

        var visitor = new CountingVisitor();
        visitor.VisitFunction(function, null);

        Assert.Equal(1, visitor.FunctionCount);
        Assert.Equal(3, visitor.BasicBlockCount); // 1 regular + 2 deferred
    }

    [Fact]
    public void VisitBasicBlock_EmptyBlock_CallsVisitBasicBlock()
    {
        var block = new IrBasicBlock("test");
        var visitor = new CountingVisitor();

        visitor.VisitBasicBlock(block, null);

        Assert.Equal(1, visitor.BasicBlockCount);
        Assert.Equal(0, visitor.InstructionCount);
    }

    [Fact]
    public void VisitBasicBlock_WithInstructions_VisitsAllInstructions()
    {
        var block = new IrBasicBlock("test");
        block.Instructions.Add(new IrLabel("start"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(null));

        var visitor = new CountingVisitor();
        visitor.VisitBasicBlock(block, null);

        Assert.Equal(1, visitor.BasicBlockCount);
        Assert.Equal(3, visitor.InstructionCount);
    }

    [Fact]
    public void VisitInstruction_Label_CallsVisitLabel()
    {
        var label = new IrLabel("test");
        var visitor = new CountingVisitor();

        visitor.VisitInstruction(label, null);

        Assert.Equal(1, visitor.InstructionCount);
        Assert.Equal(1, visitor.LabelCount);
    }

    [Fact]
    public void VisitInstruction_LocalDecl_CallsVisitLocalDecl()
    {
        var decl = new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(0, IrIntType.I32));
        var visitor = new CountingVisitor();

        visitor.VisitInstruction(decl, null);

        Assert.Equal(1, visitor.InstructionCount);
        Assert.Equal(1, visitor.LocalDeclCount);
    }

    [Fact]
    public void VisitInstruction_Return_CallsVisitReturn()
    {
        var ret = new IrReturn(null);
        var visitor = new CountingVisitor();

        visitor.VisitInstruction(ret, null);

        Assert.Equal(1, visitor.InstructionCount);
        Assert.Equal(1, visitor.ReturnCount);
    }

    [Fact]
    public void VisitInstruction_BinaryOp_CallsVisitBinaryOp()
    {
        var binop = new IrBinaryOp("result", IrBinaryOp.OpKind.Add, new IrConstant(1, IrIntType.I32), new IrConstant(2, IrIntType.I32), IrIntType.I32);
        var visitor = new CountingVisitor();

        visitor.VisitInstruction(binop, null);

        Assert.Equal(1, visitor.InstructionCount);
        Assert.Equal(1, visitor.BinaryOpCount);
    }

    [Fact]
    public void VisitInstruction_Call_CallsVisitCall()
    {
        var call = new IrCall("test", IrVoidType.Instance, null);
        var visitor = new CountingVisitor();

        visitor.VisitInstruction(call, null);

        Assert.Equal(1, visitor.InstructionCount);
        Assert.Equal(1, visitor.CallCount);
    }

    [Fact]
    public void VisitInstruction_Store_CallsVisitStore()
    {
        var store = new IrStore("x", new IrConstant(42, IrIntType.I32));
        var visitor = new CountingVisitor();

        visitor.VisitInstruction(store, null);

        Assert.Equal(1, visitor.InstructionCount);
        Assert.Equal(1, visitor.StoreCount);
    }

    [Fact]
    public void VisitInstruction_Branch_CallsVisitBranch()
    {
        var branch = new IrBranch("target");
        var visitor = new CountingVisitor();

        visitor.VisitInstruction(branch, null);

        Assert.Equal(1, visitor.InstructionCount);
        Assert.Equal(1, visitor.BranchCount);
    }

    [Fact]
    public void VisitInstruction_ConditionalBranch_CallsVisitConditionalBranch()
    {
        var condBranch = new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "true_label", "false_label");
        var visitor = new CountingVisitor();

        visitor.VisitInstruction(condBranch, null);

        Assert.Equal(1, visitor.InstructionCount);
        Assert.Equal(1, visitor.ConditionalBranchCount);
    }

    [Fact]
    public void VisitModule_ComplexHierarchy_VisitsAllNodes()
    {
        var module = new IrModule();

        // Create function with multiple blocks and instructions
        var function = new IrFunction("main", IrIntType.I32);
        var entryBlock = new IrBasicBlock("entry");
        entryBlock.Instructions.Add(new IrLabel("entry"));
        entryBlock.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        entryBlock.Instructions.Add(new IrStore("x", new IrConstant(10, IrIntType.I32)));
        entryBlock.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(20, IrIntType.I32)));
        entryBlock.Instructions.Add(new IrStore("y", new IrConstant(20, IrIntType.I32)));
        entryBlock.Instructions.Add(new IrBinaryOp("sum", IrBinaryOp.OpKind.Add, new IrVariable("x", IrIntType.I32), new IrVariable("y", IrIntType.I32), IrIntType.I32));
        entryBlock.Instructions.Add(new IrReturn(new IrVariable("sum", IrIntType.I32)));

        function.BasicBlocks.Add(entryBlock);
        module.Functions.Add(function);

        var visitor = new CountingVisitor();
        visitor.VisitModule(module, null);

        Assert.Equal(1, visitor.ModuleCount);
        Assert.Equal(1, visitor.FunctionCount);
        Assert.Equal(1, visitor.BasicBlockCount);
        Assert.Equal(7, visitor.InstructionCount);
        Assert.Equal(2, visitor.LocalDeclCount);
        Assert.Equal(2, visitor.StoreCount);
        Assert.Equal(1, visitor.BinaryOpCount);
        Assert.Equal(1, visitor.ReturnCount);
        Assert.Equal(1, visitor.LabelCount);
    }

    [Fact]
    public void CustomVisitor_CollectsVariableNames()
    {
        var block = new IrBasicBlock("test");
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("sum", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));

        var collector = new VariableCollector();
        var variables = new List<string>();
        collector.VisitBasicBlock(block, variables);

        Assert.Equal(3, variables.Count);
        Assert.Contains("x", variables);
        Assert.Contains("y", variables);
        Assert.Contains("sum", variables);
    }

    [Fact]
    public void VisitModule_WithStaticVariables_VisitsStatics()
    {
        var module = new IrModule();
        module.StaticVariables.Add(new IrStaticVariable("global1", IrIntType.I32, Visibility.Public, false, new IrConstant(0, IrIntType.I32)));
        module.StaticVariables.Add(new IrStaticVariable("global2", IrIntType.I32, Visibility.Private, false, new IrConstant(0, IrIntType.I32)));

        var visitor = new CountingVisitor();
        visitor.VisitModule(module, null);

        Assert.Equal(1, visitor.ModuleCount);
    }

    [Fact]
    public void VisitModule_WithExternalVariables_VisitsExternals()
    {
        var module = new IrModule();
        module.ExternalVariables.Add(new IrExternalVariable("extern_var", IrIntType.I32));

        var visitor = new CountingVisitor();
        visitor.VisitModule(module, null);

        Assert.Equal(1, visitor.ModuleCount);
    }

    [Fact]
    public void VisitFunction_MultipleBlocksWithBranches_VisitsAll()
    {
        var function = new IrFunction("test", IrVoidType.Instance);

        var entry = new IrBasicBlock("entry");
        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "then", "else"));

        var thenBlock = new IrBasicBlock("then");
        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrBranch("end"));

        var elseBlock = new IrBasicBlock("else");
        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrBranch("end"));

        var endBlock = new IrBasicBlock("end");
        endBlock.Instructions.Add(new IrLabel("end"));
        endBlock.Instructions.Add(new IrReturn(null));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);
        function.BasicBlocks.Add(endBlock);

        var visitor = new CountingVisitor();
        visitor.VisitFunction(function, null);

        Assert.Equal(1, visitor.FunctionCount);
        Assert.Equal(4, visitor.BasicBlockCount);
        Assert.Equal(8, visitor.InstructionCount);
        Assert.Equal(1, visitor.ConditionalBranchCount);
        Assert.Equal(2, visitor.BranchCount);
        Assert.Equal(1, visitor.ReturnCount);
        Assert.Equal(4, visitor.LabelCount);
    }

    [Fact]
    public void VisitPhi_SinglePhi_CallsVisitPhi()
    {
        var x0 = new IrVariable("x", 0, IrIntType.I32);
        var x1 = new IrVariable("x", 1, IrIntType.I32);
        var x2 = new IrVariable("x", 2, IrIntType.I32);

        var phi = new IrPhi(x2);
        var block1 = new IrBasicBlock("block1");
        var block2 = new IrBasicBlock("block2");
        phi.AddIncoming(x0, block1);
        phi.AddIncoming(x1, block2);

        var visitor = new CountingVisitor();
        visitor.VisitPhi(phi, null);

        Assert.Equal(1, visitor.PhiCount);
        Assert.Equal(0, visitor.InstructionCount); // Phi is visited directly, not as instruction
    }

    [Fact]
    public void VisitBasicBlock_WithPhiFunctions_VisitsPhisBeforeInstructions()
    {
        var block = new IrBasicBlock("test");

        // Add phi functions
        var x0 = new IrVariable("x", 0, IrIntType.I32);
        var x1 = new IrVariable("x", 1, IrIntType.I32);
        var x2 = new IrVariable("x", 2, IrIntType.I32);
        var phi = new IrPhi(x2);
        var pred1 = new IrBasicBlock("pred1");
        var pred2 = new IrBasicBlock("pred2");
        phi.AddIncoming(x0, pred1);
        phi.AddIncoming(x1, pred2);
        block.AddPhi(phi);

        // Add regular instructions
        block.Instructions.Add(new IrLabel("test"));
        block.Instructions.Add(new IrReturn(null));

        var visitor = new CountingVisitor();
        visitor.VisitBasicBlock(block, null);

        Assert.Equal(1, visitor.BasicBlockCount);
        Assert.Equal(1, visitor.PhiCount);
        Assert.Equal(2, visitor.InstructionCount);
    }

    [Fact]
    public void VisitFunction_WithPhiFunctions_VisitsAllPhis()
    {
        var function = new IrFunction("test", IrIntType.I32);

        // Create a simple if-then-else-merge structure with phi
        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var elseBlock = new IrBasicBlock("else");
        var merge = new IrBasicBlock("merge");

        // Entry block
        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "then", "else"));

        // Then block: x_1 = 10
        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        thenBlock.Instructions.Add(new IrBranch("merge"));

        // Else block: x_2 = 20
        elseBlock.Instructions.Add(new IrLabel("else"));
        elseBlock.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(20, IrIntType.I32)));
        elseBlock.Instructions.Add(new IrBranch("merge"));

        // Merge block: x_3 = φ(x_1 from then, x_2 from else)
        var x1 = new IrVariable("x", 1, IrIntType.I32);
        var x2 = new IrVariable("x", 2, IrIntType.I32);
        var x3 = new IrVariable("x", 3, IrIntType.I32);
        var phi = new IrPhi(x3);
        phi.AddIncoming(x1, thenBlock);
        phi.AddIncoming(x2, elseBlock);
        merge.AddPhi(phi);
        merge.Instructions.Add(new IrLabel("merge"));
        merge.Instructions.Add(new IrReturn(x3));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(elseBlock);
        function.BasicBlocks.Add(merge);

        var visitor = new CountingVisitor();
        visitor.VisitFunction(function, null);

        Assert.Equal(1, visitor.FunctionCount);
        Assert.Equal(4, visitor.BasicBlockCount);
        Assert.Equal(1, visitor.PhiCount);
        Assert.Equal(10, visitor.InstructionCount); // 4 labels, 3 branches, 2 decls, 1 return
    }

    [Fact]
    public void IrPhi_AddIncoming_AddsValueAndBlock()
    {
        var dest = new IrVariable("x", 2, IrIntType.I32);
        var phi = new IrPhi(dest);

        var value1 = new IrVariable("x", 0, IrIntType.I32);
        var block1 = new IrBasicBlock("block1");
        phi.AddIncoming(value1, block1);

        Assert.Single(phi.IncomingValues);
        Assert.Single(phi.IncomingBlocks);
        Assert.Equal(value1, phi.IncomingValues[0]);
        Assert.Equal(block1, phi.IncomingBlocks[0]);
    }

    [Fact]
    public void IrPhi_GetValueForBlock_ReturnsCorrectValue()
    {
        var dest = new IrVariable("x", 2, IrIntType.I32);
        var phi = new IrPhi(dest);

        var value1 = new IrVariable("x", 0, IrIntType.I32);
        var value2 = new IrVariable("x", 1, IrIntType.I32);
        var block1 = new IrBasicBlock("block1");
        var block2 = new IrBasicBlock("block2");

        phi.AddIncoming(value1, block1);
        phi.AddIncoming(value2, block2);

        Assert.Equal(value1, phi.GetValueForBlock(block1));
        Assert.Equal(value2, phi.GetValueForBlock(block2));
    }

    [Fact]
    public void IrPhi_GetValueForBlock_ReturnsNullForUnknownBlock()
    {
        var dest = new IrVariable("x", 2, IrIntType.I32);
        var phi = new IrPhi(dest);

        var value1 = new IrVariable("x", 0, IrIntType.I32);
        var block1 = new IrBasicBlock("block1");
        phi.AddIncoming(value1, block1);

        var unknownBlock = new IrBasicBlock("unknown");
        Assert.Null(phi.GetValueForBlock(unknownBlock));
    }

    [Fact]
    public void IrPhi_ReplaceValue_ReplacesAllOccurrences()
    {
        var dest = new IrVariable("x", 3, IrIntType.I32);
        var phi = new IrPhi(dest);

        var oldValue = new IrVariable("x", 0, IrIntType.I32);
        var newValue = new IrVariable("x", 1, IrIntType.I32);
        var otherValue = new IrVariable("y", 0, IrIntType.I32);

        var block1 = new IrBasicBlock("block1");
        var block2 = new IrBasicBlock("block2");
        var block3 = new IrBasicBlock("block3");

        phi.AddIncoming(oldValue, block1);
        phi.AddIncoming(otherValue, block2);
        phi.AddIncoming(oldValue, block3);

        phi.ReplaceValue(oldValue, newValue);

        Assert.Equal(newValue, phi.IncomingValues[0]);
        Assert.Equal(otherValue, phi.IncomingValues[1]);
        Assert.Equal(newValue, phi.IncomingValues[2]);
    }

    [Fact]
    public void IrVariable_SsaName_FormatsCorrectly()
    {
        var nonSsaVar = new IrVariable("x", IrIntType.I32);
        Assert.Equal("x", nonSsaVar.SsaName);
        Assert.False(nonSsaVar.IsInSsaForm);
        Assert.Equal(-1, nonSsaVar.Version);

        var ssaVar = new IrVariable("x", 0, IrIntType.I32);
        Assert.Equal("x_0", ssaVar.SsaName);
        Assert.True(ssaVar.IsInSsaForm);
        Assert.Equal(0, ssaVar.Version);

        var ssaVar2 = new IrVariable("result", 42, IrIntType.I32);
        Assert.Equal("result_42", ssaVar2.SsaName);
        Assert.True(ssaVar2.IsInSsaForm);
        Assert.Equal(42, ssaVar2.Version);
    }

    [Fact]
    public void IrBasicBlock_AddPhi_AddsToPhiFunctions()
    {
        var block = new IrBasicBlock("test");
        Assert.Empty(block.PhiFunctions);

        var phi1 = new IrPhi(new IrVariable("x", 0, IrIntType.I32));
        var phi2 = new IrPhi(new IrVariable("y", 0, IrIntType.I32));

        block.AddPhi(phi1);
        block.AddPhi(phi2);

        Assert.Equal(2, block.PhiFunctions.Count);
        Assert.Contains(phi1, block.PhiFunctions);
        Assert.Contains(phi2, block.PhiFunctions);
    }

    [Fact]
    public void VisitModule_WithPhiFunctions_CountsCorrectly()
    {
        var module = new IrModule();
        var function = new IrFunction("test", IrIntType.I32);

        // Create a loop structure with phi functions
        var entry = new IrBasicBlock("entry");
        var loop = new IrBasicBlock("loop");
        var exit = new IrBasicBlock("exit");

        // Loop block has a phi: i_1 = φ(i_0 from entry, i_2 from loop)
        var i0 = new IrVariable("i", 0, IrIntType.I32);
        var i1 = new IrVariable("i", 1, IrIntType.I32);
        var i2 = new IrVariable("i", 2, IrIntType.I32);

        var phiI = new IrPhi(i1);
        phiI.AddIncoming(i0, entry);
        phiI.AddIncoming(i2, loop);
        loop.AddPhi(phiI);

        // Sum phi: sum_1 = φ(sum_0 from entry, sum_2 from loop)
        var sum0 = new IrVariable("sum", 0, IrIntType.I32);
        var sum1 = new IrVariable("sum", 1, IrIntType.I32);
        var sum2 = new IrVariable("sum", 2, IrIntType.I32);

        var phiSum = new IrPhi(sum1);
        phiSum.AddIncoming(sum0, entry);
        phiSum.AddIncoming(sum2, loop);
        loop.AddPhi(phiSum);

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrBranch("loop"));

        loop.Instructions.Add(new IrLabel("loop"));
        loop.Instructions.Add(new IrBranch("exit"));

        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(sum1));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(exit);
        module.Functions.Add(function);

        var visitor = new CountingVisitor();
        visitor.VisitModule(module, null);

        Assert.Equal(1, visitor.ModuleCount);
        Assert.Equal(1, visitor.FunctionCount);
        Assert.Equal(3, visitor.BasicBlockCount);
        Assert.Equal(2, visitor.PhiCount); // Two phi functions in loop block
        Assert.Equal(6, visitor.InstructionCount);
    }
}
