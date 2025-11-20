using Novus.IR;
using Xunit;

namespace Novus.Tests.IR;

/// <summary>
/// Tests for the IrRewriter base class
/// Tests transformation passes that modify IR in-place or create modified copies
/// </summary>
public class IrRewriterTests
{
    /// <summary>
    /// Concrete rewriter that removes all label instructions
    /// </summary>
    private class LabelRemovingRewriter : IrRewriter
    {
        public int LabelsRemoved { get; private set; }

        public override IrInstruction? RewriteLabel(IrLabel label)
        {
            LabelsRemoved++;
            return null; // Delete the label
        }
    }

    /// <summary>
    /// Concrete rewriter that replaces all constants with zero
    /// </summary>
    private class ConstantZeroRewriter : IrRewriter
    {
        public int ConstantsRewritten { get; private set; }

        public override IrValue RewriteConstant(IrConstant constant)
        {
            ConstantsRewritten++;
            return new IrConstant(0, constant.Type);
        }
    }

    /// <summary>
    /// Concrete rewriter that folds constant binary operations
    /// </summary>
    private class ConstantFoldingRewriter : IrRewriter
    {
        public int OperationsFolded { get; private set; }

        public override IrInstruction? RewriteBinaryOp(IrBinaryOp binaryOp)
        {
            // First rewrite operands
            binaryOp = (IrBinaryOp)base.RewriteBinaryOp(binaryOp)!;

            // Check if both operands are constants
            if (binaryOp.Left is IrConstant leftConst && binaryOp.Right is IrConstant rightConst)
            {
                long result = binaryOp.Operation switch
                {
                    IrBinaryOp.OpKind.Add => leftConst.Value + rightConst.Value,
                    IrBinaryOp.OpKind.Sub => leftConst.Value - rightConst.Value,
                    IrBinaryOp.OpKind.Mul => leftConst.Value * rightConst.Value,
                    _ => 0
                };

                OperationsFolded++;
                // Replace with a store of the constant result
                return new IrStore(binaryOp.ResultName, new IrConstant(result, binaryOp.Type));
            }

            return binaryOp;
        }
    }

    /// <summary>
    /// Concrete rewriter that tracks which instruction types are visited
    /// </summary>
    private class InstructionTrackingRewriter : IrRewriter
    {
        public int BinaryOpCount { get; private set; }
        public int CallCount { get; private set; }
        public int ReturnCount { get; private set; }
        public int StoreCount { get; private set; }
        public int BranchCount { get; private set; }
        public int LocalDeclCount { get; private set; }

        public override IrInstruction? RewriteBinaryOp(IrBinaryOp binaryOp)
        {
            BinaryOpCount++;
            return base.RewriteBinaryOp(binaryOp);
        }

        public override IrInstruction? RewriteCall(IrCall call)
        {
            CallCount++;
            return base.RewriteCall(call);
        }

        public override IrInstruction? RewriteReturn(IrReturn ret)
        {
            ReturnCount++;
            return base.RewriteReturn(ret);
        }

        public override IrInstruction? RewriteStore(IrStore store)
        {
            StoreCount++;
            return base.RewriteStore(store);
        }

        public override IrInstruction? RewriteBranch(IrBranch branch)
        {
            BranchCount++;
            return base.RewriteBranch(branch);
        }

        public override IrInstruction? RewriteLocalDecl(IrLocalDecl localDecl)
        {
            LocalDeclCount++;
            return base.RewriteLocalDecl(localDecl);
        }
    }

    /// <summary>
    /// Concrete rewriter that replaces variable names
    /// </summary>
    private class VariableRenamingRewriter : IrRewriter
    {
        private readonly string _oldName;
        private readonly string _newName;
        public int VariablesRenamed { get; private set; }

        public VariableRenamingRewriter(string oldName, string newName)
        {
            _oldName = oldName;
            _newName = newName;
        }

        public override IrValue RewriteVariable(IrVariable variable)
        {
            if (variable.Name == _oldName)
            {
                VariablesRenamed++;
                return new IrVariable(_newName, variable.Type);
            }
            return variable;
        }
    }

    [Fact]
    public void RewriteModule_EmptyModule_ReturnsModule()
    {
        var module = new IrModule();
        var rewriter = new InstructionTrackingRewriter();

        var result = rewriter.RewriteModule(module);

        Assert.NotNull(result);
        Assert.Same(module, result);
    }

    [Fact]
    public void RewriteModule_WithFunctions_RewritesAllFunctions()
    {
        var module = new IrModule();
        var func1 = new IrFunction("func1", IrVoidType.Instance);
        func1.BasicBlocks.Add(new IrBasicBlock("entry"));
        var func2 = new IrFunction("func2", IrVoidType.Instance);
        func2.BasicBlocks.Add(new IrBasicBlock("entry"));

        module.Functions.Add(func1);
        module.Functions.Add(func2);

        var rewriter = new InstructionTrackingRewriter();
        rewriter.RewriteModule(module);

        Assert.Equal(2, module.Functions.Count);
    }

    [Fact]
    public void RewriteBasicBlock_WithLabels_RemovesLabels()
    {
        var block = new IrBasicBlock("test");
        block.Instructions.Add(new IrLabel("start"));
        block.Instructions.Add(new IrLabel("middle"));
        block.Instructions.Add(new IrReturn(null));
        block.Instructions.Add(new IrLabel("end"));

        var rewriter = new LabelRemovingRewriter();
        rewriter.RewriteBasicBlock(block);

        Assert.Equal(1, block.Instructions.Count); // Only return remains
        Assert.Equal(3, rewriter.LabelsRemoved);
        Assert.IsType<IrReturn>(block.Instructions[0]);
    }

    [Fact]
    public void RewriteInstruction_BinaryOp_RewritesOperands()
    {
        var binop = new IrBinaryOp("result", IrBinaryOp.OpKind.Add,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32);

        var rewriter = new ConstantZeroRewriter();
        var result = rewriter.RewriteInstruction(binop);

        Assert.NotNull(result);
        Assert.IsType<IrBinaryOp>(result);
        var rewritten = (IrBinaryOp)result;
        Assert.IsType<IrConstant>(rewritten.Left);
        Assert.IsType<IrConstant>(rewritten.Right);
        Assert.Equal(0, ((IrConstant)rewritten.Left).Value);
        Assert.Equal(0, ((IrConstant)rewritten.Right).Value);
        Assert.Equal(2, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteInstruction_Call_RewritesArguments()
    {
        var call = new IrCall("test", IrVoidType.Instance, "result");
        call.Arguments.Add(new IrConstant(1, IrIntType.I32));
        call.Arguments.Add(new IrConstant(2, IrIntType.I32));
        call.Arguments.Add(new IrConstant(3, IrIntType.I32));

        var rewriter = new ConstantZeroRewriter();
        var result = rewriter.RewriteInstruction(call);

        Assert.NotNull(result);
        Assert.IsType<IrCall>(result);
        var rewritten = (IrCall)result;
        Assert.Equal(3, rewritten.Arguments.Count);
        Assert.All(rewritten.Arguments, arg =>
        {
            var constant = Assert.IsType<IrConstant>(arg);
            Assert.Equal(0, constant.Value);
        });
        Assert.Equal(3, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteInstruction_Store_RewritesValue()
    {
        var store = new IrStore("x", new IrConstant(42, IrIntType.I32));

        var rewriter = new ConstantZeroRewriter();
        var result = rewriter.RewriteInstruction(store);

        Assert.NotNull(result);
        Assert.IsType<IrStore>(result);
        var rewritten = (IrStore)result;
        Assert.IsType<IrConstant>(rewritten.Value);
        Assert.Equal(0, ((IrConstant)rewritten.Value).Value);
        Assert.Equal(1, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteInstruction_Return_RewritesValue()
    {
        var ret = new IrReturn(new IrConstant(100, IrIntType.I32));

        var rewriter = new ConstantZeroRewriter();
        var result = rewriter.RewriteInstruction(ret);

        Assert.NotNull(result);
        Assert.IsType<IrReturn>(result);
        var rewritten = (IrReturn)result;
        Assert.NotNull(rewritten.Value);
        Assert.IsType<IrConstant>(rewritten.Value);
        Assert.Equal(0, ((IrConstant)rewritten.Value).Value);
        Assert.Equal(1, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteInstruction_ReturnVoid_LeavesUnchanged()
    {
        var ret = new IrReturn(null);

        var rewriter = new ConstantZeroRewriter();
        var result = rewriter.RewriteInstruction(ret);

        Assert.NotNull(result);
        Assert.IsType<IrReturn>(result);
        Assert.Null(((IrReturn)result).Value);
        Assert.Equal(0, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteInstruction_LocalDecl_RewritesInitialValue()
    {
        var decl = new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(50, IrIntType.I32));

        var rewriter = new ConstantZeroRewriter();
        var result = rewriter.RewriteInstruction(decl);

        Assert.NotNull(result);
        Assert.IsType<IrLocalDecl>(result);
        var rewritten = (IrLocalDecl)result;
        Assert.NotNull(rewritten.InitialValue);
        Assert.IsType<IrConstant>(rewritten.InitialValue);
        Assert.Equal(0, ((IrConstant)rewritten.InitialValue).Value);
        Assert.Equal(1, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteInstruction_ConditionalBranch_RewritesCondition()
    {
        var condBranch = new IrConditionalBranch(
            new IrVariable("cond", IrBoolType.Instance),
            "true_label",
            "false_label");

        var rewriter = new VariableRenamingRewriter("cond", "condition");
        var result = rewriter.RewriteInstruction(condBranch);

        Assert.NotNull(result);
        Assert.IsType<IrConditionalBranch>(result);
        var rewritten = (IrConditionalBranch)result;
        Assert.IsType<IrVariable>(rewritten.Condition);
        Assert.Equal("condition", ((IrVariable)rewritten.Condition).Name);
        Assert.Equal(1, rewriter.VariablesRenamed);
    }

    [Fact]
    public void RewriteFunction_WithBasicBlocks_RewritesAllBlocks()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var block1 = new IrBasicBlock("entry");
        block1.Instructions.Add(new IrLabel("entry"));
        var block2 = new IrBasicBlock("exit");
        block2.Instructions.Add(new IrLabel("exit"));

        function.BasicBlocks.Add(block1);
        function.BasicBlocks.Add(block2);

        var rewriter = new LabelRemovingRewriter();
        rewriter.RewriteFunction(function);

        Assert.Equal(0, function.BasicBlocks[0].Instructions.Count);
        Assert.Equal(0, function.BasicBlocks[1].Instructions.Count);
        Assert.Equal(2, rewriter.LabelsRemoved);
    }

    [Fact]
    public void RewriteFunction_WithDeferredBlocks_RewritesDeferredBlocks()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var regularBlock = new IrBasicBlock("entry");
        regularBlock.Instructions.Add(new IrLabel("entry"));

        var deferredBlock = new IrBasicBlock("defer1");
        deferredBlock.Instructions.Add(new IrLabel("defer1"));

        function.BasicBlocks.Add(regularBlock);
        function.DeferredBlocks.Add(deferredBlock);

        var rewriter = new LabelRemovingRewriter();
        rewriter.RewriteFunction(function);

        Assert.Equal(0, function.BasicBlocks[0].Instructions.Count);
        Assert.Equal(0, function.DeferredBlocks[0].Instructions.Count);
        Assert.Equal(2, rewriter.LabelsRemoved);
    }

    [Fact]
    public void ConstantFolding_AddsConstants_FoldsToStore()
    {
        var binop = new IrBinaryOp("sum", IrBinaryOp.OpKind.Add,
            new IrConstant(10, IrIntType.I32),
            new IrConstant(20, IrIntType.I32),
            IrIntType.I32);

        var rewriter = new ConstantFoldingRewriter();
        var result = rewriter.RewriteInstruction(binop);

        Assert.NotNull(result);
        Assert.IsType<IrStore>(result);
        var store = (IrStore)result;
        Assert.Equal("sum", store.VariableName);
        Assert.IsType<IrConstant>(store.Value);
        Assert.Equal(30, ((IrConstant)store.Value).Value);
        Assert.Equal(1, rewriter.OperationsFolded);
    }

    [Fact]
    public void ConstantFolding_SubtractsConstants_FoldsToStore()
    {
        var binop = new IrBinaryOp("diff", IrBinaryOp.OpKind.Sub,
            new IrConstant(50, IrIntType.I32),
            new IrConstant(20, IrIntType.I32),
            IrIntType.I32);

        var rewriter = new ConstantFoldingRewriter();
        var result = rewriter.RewriteInstruction(binop);

        Assert.NotNull(result);
        var store = Assert.IsType<IrStore>(result);
        Assert.Equal(30, ((IrConstant)store.Value).Value);
        Assert.Equal(1, rewriter.OperationsFolded);
    }

    [Fact]
    public void ConstantFolding_MultipliesConstants_FoldsToStore()
    {
        var binop = new IrBinaryOp("product", IrBinaryOp.OpKind.Mul,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(7, IrIntType.I32),
            IrIntType.I32);

        var rewriter = new ConstantFoldingRewriter();
        var result = rewriter.RewriteInstruction(binop);

        Assert.NotNull(result);
        var store = Assert.IsType<IrStore>(result);
        Assert.Equal(35, ((IrConstant)store.Value).Value);
        Assert.Equal(1, rewriter.OperationsFolded);
    }

    [Fact]
    public void ConstantFolding_NonConstantOperands_LeavesUnchanged()
    {
        var binop = new IrBinaryOp("result", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32);

        var rewriter = new ConstantFoldingRewriter();
        var result = rewriter.RewriteInstruction(binop);

        Assert.NotNull(result);
        Assert.IsType<IrBinaryOp>(result);
        Assert.Equal(0, rewriter.OperationsFolded);
    }

    [Fact]
    public void RewriteValue_Variable_ReplacesVariable()
    {
        var variable = new IrVariable("old_name", IrIntType.I32);

        var rewriter = new VariableRenamingRewriter("old_name", "new_name");
        var result = rewriter.RewriteValue(variable);

        Assert.IsType<IrVariable>(result);
        Assert.Equal("new_name", ((IrVariable)result).Name);
        Assert.Equal(1, rewriter.VariablesRenamed);
    }

    [Fact]
    public void RewriteValue_DifferentVariable_LeavesUnchanged()
    {
        var variable = new IrVariable("other_name", IrIntType.I32);

        var rewriter = new VariableRenamingRewriter("old_name", "new_name");
        var result = rewriter.RewriteValue(variable);

        Assert.Same(variable, result);
        Assert.Equal(0, rewriter.VariablesRenamed);
    }

    [Fact]
    public void RewriteModule_WithStaticVariables_RewritesStatics()
    {
        var module = new IrModule();
        module.StaticVariables.Add(new IrStaticVariable("global1", IrIntType.I32, Visibility.Public, false, new IrConstant(100, IrIntType.I32)));
        module.StaticVariables.Add(new IrStaticVariable("global2", IrIntType.I32, Visibility.Private, false, new IrConstant(200, IrIntType.I32)));

        var rewriter = new ConstantZeroRewriter();
        rewriter.RewriteModule(module);

        Assert.Equal(2, module.StaticVariables.Count);
        Assert.All(module.StaticVariables, staticVar =>
        {
            var constant = Assert.IsType<IrConstant>(staticVar.InitialValue);
            Assert.Equal(0, constant.Value);
        });
        Assert.Equal(2, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteBasicBlock_ComplexBlock_RewritesAllInstructions()
    {
        var block = new IrBasicBlock("test");
        block.Instructions.Add(new IrLabel("start"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrStore("x", new IrConstant(20, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add, new IrVariable("x", IrIntType.I32), new IrConstant(5, IrIntType.I32), IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        var rewriter = new InstructionTrackingRewriter();
        rewriter.RewriteBasicBlock(block);

        Assert.Equal(5, block.Instructions.Count);
        Assert.Equal(1, rewriter.BinaryOpCount);
        Assert.Equal(1, rewriter.StoreCount);
        Assert.Equal(1, rewriter.ReturnCount);
        Assert.Equal(1, rewriter.LocalDeclCount);
    }

    [Fact]
    public void RewriteInstruction_UnknownType_ReturnsUnchanged()
    {
        var instruction = new IrLabel("test"); // Use label as a simple instruction type

        var rewriter = new InstructionTrackingRewriter();
        var result = rewriter.RewriteInstruction(instruction);

        Assert.NotNull(result);
        Assert.Same(instruction, result);
    }

    [Fact]
    public void RewriteValue_StructLiteral_RewritesFieldValues()
    {
        var structType = new IrStructType("Point", new List<IrStructField>());
        var fieldValues = new Dictionary<string, IrValue>
        {
            ["x"] = new IrConstant(10, IrIntType.I32),
            ["y"] = new IrConstant(20, IrIntType.I32)
        };
        var structLiteral = new IrStructLiteral(structType, fieldValues);

        var rewriter = new ConstantZeroRewriter();
        var result = rewriter.RewriteValue(structLiteral);

        Assert.IsType<IrStructLiteral>(result);
        var rewritten = (IrStructLiteral)result;
        Assert.Equal(2, rewritten.FieldValues.Count);
        Assert.Equal(0, ((IrConstant)rewritten.FieldValues["x"]).Value);
        Assert.Equal(0, ((IrConstant)rewritten.FieldValues["y"]).Value);
        Assert.Equal(2, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteValue_ArrayLiteral_RewritesElements()
    {
        var arrayType = new IrArrayType(IrIntType.I32, 3);
        var arrayLiteral = new IrArrayLiteral(arrayType);
        arrayLiteral.Elements.Add(new IrConstant(1, IrIntType.I32));
        arrayLiteral.Elements.Add(new IrConstant(2, IrIntType.I32));
        arrayLiteral.Elements.Add(new IrConstant(3, IrIntType.I32));

        var rewriter = new ConstantZeroRewriter();
        var result = rewriter.RewriteValue(arrayLiteral);

        Assert.IsType<IrArrayLiteral>(result);
        var rewritten = (IrArrayLiteral)result;
        Assert.Equal(3, rewritten.Elements.Count);
        Assert.All(rewritten.Elements, element =>
        {
            var constant = Assert.IsType<IrConstant>(element);
            Assert.Equal(0, constant.Value);
        });
        Assert.Equal(3, rewriter.ConstantsRewritten);
    }

    [Fact]
    public void RewriteBasicBlock_RemovesMultipleInstructions_AdjustsIndices()
    {
        var block = new IrBasicBlock("test");
        block.Instructions.Add(new IrLabel("L1"));
        block.Instructions.Add(new IrReturn(null));
        block.Instructions.Add(new IrLabel("L2"));
        block.Instructions.Add(new IrReturn(null));
        block.Instructions.Add(new IrLabel("L3"));

        var rewriter = new LabelRemovingRewriter();
        rewriter.RewriteBasicBlock(block);

        Assert.Equal(2, block.Instructions.Count);
        Assert.All(block.Instructions, instr => Assert.IsType<IrReturn>(instr));
        Assert.Equal(3, rewriter.LabelsRemoved);
    }
}
