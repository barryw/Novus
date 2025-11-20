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
        module.Structs.Add(new IrStructType("Point", new List<IrStructField>()));

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
}
