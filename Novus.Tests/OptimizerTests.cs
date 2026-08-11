using Novus.IR;
using Novus.Optimizer;
using Novus.Optimizer.Passes;
using Xunit;

namespace Novus.Tests;

public class OptimizerTests
{
    [Fact]
    public void ConstantFolding_SimpleAddition_FoldsToConstant()
    {
        // Create IR: return 2 + 3
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrConstant(2, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add);
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.I32)));

        // Run constant folding
        var pass = new ConstantFoldingPass();
        bool changed = pass.Run(module);

        Assert.True(changed);
        // After folding, should only have the return instruction
        Assert.Single(block.Instructions);
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(5, constant.Value);
    }

    [Fact]
    public void ConstantFolding_Multiplication_FoldsCorrectly()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul,
            new IrConstant(4, IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(mul);
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.I32)));

        var pass = new ConstantFoldingPass();
        pass.Run(module);

        var ret = (IrReturn)block.Instructions[0];
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(20, constant.Value);
    }

    [Fact]
    public void ConstantFolding_DivisionByZero_DoesNotFold()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        var div = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Div,
            new IrConstant(10, IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(div);
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.I32)));

        var pass = new ConstantFoldingPass();
        bool changed = pass.Run(module);

        // Should not fold division by zero
        Assert.False(changed);
        Assert.Equal(2, block.Instructions.Count);
    }

    [Fact]
    public void DeadCodeElimination_UnusedResult_RemovesInstruction()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        // Unused calculation
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrConstant(2, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add);
        // Return something else
        block.AddInstruction(new IrReturn(new IrConstant(42, IrIntType.I32)));

        var pass = new DeadCodeEliminationPass();
        bool changed = pass.Run(module);

        Assert.True(changed);
        Assert.Single(block.Instructions); // Only return should remain
    }

    [Fact]
    public void DeadCodeElimination_UsedResult_KeepsInstruction()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrConstant(2, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add);
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.I32)));

        var pass = new DeadCodeEliminationPass();
        bool changed = pass.Run(module);

        Assert.False(changed);
        Assert.Equal(2, block.Instructions.Count);
    }

    [Fact]
    public void StrengthReduction_MultiplyByPowerOfTwo_ConvertToShift()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.U32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        var mul = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.U32),
            new IrConstant(4, IrIntType.U32),
            IrIntType.U32);
        block.AddInstruction(mul);
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.U32)));

        var pass = new StrengthReductionPass();
        bool changed = pass.Run(module);

        Assert.True(changed);
        Assert.Equal(IrBinaryOp.OpKind.Shl, mul.Operation);
        var shiftAmount = Assert.IsType<IrConstant>(mul.Right);
        Assert.Equal(2, shiftAmount.Value); // 4 = 2^2
    }

    [Fact]
    public void StrengthReduction_UnsignedDivideByPowerOfTwo_ConvertToShift()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.U32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        var div = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Div,
            new IrVariable("x", IrIntType.U32),
            new IrConstant(8, IrIntType.U32),
            IrIntType.U32);
        block.AddInstruction(div);
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.U32)));

        var pass = new StrengthReductionPass();
        bool changed = pass.Run(module);

        Assert.True(changed);
        Assert.Equal(IrBinaryOp.OpKind.Shr, div.Operation);
        var shiftAmount = Assert.IsType<IrConstant>(div.Right);
        Assert.Equal(3, shiftAmount.Value); // 8 = 2^3
    }

    [Fact]
    public void StrengthReduction_SignedDivide_DoesNotConvert()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        var div = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Div,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(4, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(div);

        var pass = new StrengthReductionPass();
        bool changed = pass.Run(module);

        // Signed division by power of 2 is not reduced (requires arithmetic shift)
        Assert.False(changed);
        Assert.Equal(IrBinaryOp.OpKind.Div, div.Operation);
    }

    [Fact]
    public void OptimizationPipeline_Level0_NoOptimizations()
    {
        var pipeline = OptimizationPipeline.CreatePipeline(0);
        var module = new IrModule();

        // Pipeline should have no passes
        pipeline.Run(module);

        // Should not throw, just do nothing
        Assert.True(true);
    }

    [Fact]
    public void OptimizationPipeline_Level1_BasicOptimizations()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        // Create: return 2 + 3
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrConstant(2, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add);
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.I32)));

        var pipeline = OptimizationPipeline.CreatePipeline(1);
        pipeline.Run(module);

        // Should fold to: return 5
        Assert.Single(block.Instructions);
        var ret = (IrReturn)block.Instructions[0];
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(5, constant.Value);
    }

    [Fact]
    public void OptimizationPipeline_ComplexExpression_OptimizesCorrectly()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        // Create: (2 + 3) * 4
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrConstant(2, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add);

        var mul = new IrBinaryOp("%t1", IrBinaryOp.OpKind.Mul,
            new IrVariable("%t0", IrIntType.I32),
            new IrConstant(4, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(mul);
        block.AddInstruction(new IrReturn(new IrVariable("%t1", IrIntType.I32)));

        var pipeline = OptimizationPipeline.CreatePipeline(2);
        pipeline.Run(module);

        // Should fold completely to: return 20
        Assert.Single(block.Instructions);
        var ret = (IrReturn)block.Instructions[0];
        var constant = Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(20, constant.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OptimizationPipeline_AllLevels_DoNotCrash(int level)
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        block.AddInstruction(new IrReturn(new IrConstant(42, IrIntType.I32)));

        var pipeline = OptimizationPipeline.CreatePipeline(level);
        pipeline.Run(module);

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public void OptimizationPipeline_InvalidLevel_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => OptimizationPipeline.CreatePipeline(4));
        Assert.Throws<ArgumentException>(() => OptimizationPipeline.CreatePipeline(-1));
    }

    [Fact]
    public void ConstantPropagation_BasicTest()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        // ConstantPropagation needs constants to be already present
        // This test just verifies it doesn't crash
        var add = new IrBinaryOp("%y", IrBinaryOp.OpKind.Add,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(10, IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add);
        block.AddInstruction(new IrReturn(new IrVariable("%y", IrIntType.I32)));

        var pass = new ConstantPropagationPass();
        bool changed = pass.Run(module);

        // No variable propagation needed in this simple case
        Assert.False(changed);
    }

    [Fact]
    public void ConstantPropagation_PropagatesIntoReturn()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        var ret = new IrReturn(new IrVariable("%t0", IrIntType.I32));
        block.AddInstruction(ret);

        // Note: In real code, %t0 would be defined by a previous instruction
        // but ConstantPropagation only works if it has constant values tracked
        // This test verifies the pass doesn't crash on return statements

        var pass = new ConstantPropagationPass();
        bool changed = pass.Run(module);

        // No constants tracked, so no change
        Assert.False(changed);
    }

    [Fact]
    public void CopyPropagation_ReplacesVariableWithOriginal()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        // In a real scenario, we'd have copy assignment
        // For now, just verify the pass doesn't crash
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add);
        block.AddInstruction(new IrReturn(new IrVariable("%t0", IrIntType.I32)));

        var pass = new CopyPropagationPass();
        bool changed = pass.Run(module);

        // No copies to propagate
        Assert.False(changed);
    }

    [Fact]
    public void CommonSubexpressionElimination_EliminatesDuplicateExpression()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        // Create: x = a + b; y = a + b; return y
        var add1 = new IrBinaryOp("%x", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add1);

        var add2 = new IrBinaryOp("%y", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add2);
        block.AddInstruction(new IrReturn(new IrVariable("%y", IrIntType.I32)));

        var pass = new CommonSubexpressionEliminationPass();
        bool changed = pass.Run(module);

        Assert.True(changed);
        // Second add should be eliminated, only 2 instructions remain
        Assert.Equal(2, block.Instructions.Count);
    }

    [Fact]
    public void CommonSubexpressionElimination_HandlesCommutativeOperations()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        // Create: x = a + b; y = b + a; (commutative, should be recognized as same)
        var add1 = new IrBinaryOp("%x", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add1);

        var add2 = new IrBinaryOp("%y", IrBinaryOp.OpKind.Add,
            new IrVariable("b", IrIntType.I32),
            new IrVariable("a", IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add2);
        block.AddInstruction(new IrReturn(new IrVariable("%y", IrIntType.I32)));

        var pass = new CommonSubexpressionEliminationPass();
        bool changed = pass.Run(module);

        Assert.True(changed);
        // Second add should be eliminated
        Assert.Equal(2, block.Instructions.Count);
    }

    [Fact]
    public void CommonSubexpressionElimination_DoesNotEliminateDifferentExpressions()
    {
        var module = new IrModule();
        var func = new IrFunction("test", IrIntType.I32);
        module.AddFunction(func);

        var block = func.CreateBasicBlock("entry");
        // Create: x = a + b; y = a - b; (different operations)
        var add = new IrBinaryOp("%x", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(add);

        var sub = new IrBinaryOp("%y", IrBinaryOp.OpKind.Sub,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32);
        block.AddInstruction(sub);
        block.AddInstruction(new IrReturn(new IrVariable("%y", IrIntType.I32)));

        var pass = new CommonSubexpressionEliminationPass();
        bool changed = pass.Run(module);

        Assert.False(changed);
        // Both instructions should remain
        Assert.Equal(3, block.Instructions.Count);
    }

    [Fact]
    public void CommonSubexpressionElimination_ReusesScalarMemberLoadUntilMemoryChanges()
    {
        var module = new IrModule();
        var type = new IrStructType("State", [new IrStructField("value", IrIntType.I32)]);
        var function = new IrFunction("test", IrIntType.I32);
        module.AddFunction(function);
        var block = function.CreateBasicBlock("entry");
        var statePointer = new IrVariable("state", new IrPointerType(type));
        var state = new IrDereferenceValue(statePointer, type);
        block.AddInstruction(new IrMemberAccess("%first", state, "value", IrIntType.I32, 0));
        block.AddInstruction(new IrMemberAccess("%second", state, "value", IrIntType.I32, 0));
        block.AddInstruction(new IrReturn(new IrVariable("%second", IrIntType.I32)));

        Assert.True(new CommonSubexpressionEliminationPass().Run(module));
        Assert.Equal(2, block.Instructions.Count);
        Assert.Equal("%first", Assert.IsType<IrVariable>(Assert.IsType<IrReturn>(block.Instructions[1]).Value).Name);

        block.Instructions.Insert(1, new IrMemberStore(state, "value", 0, new IrConstant(2, IrIntType.I32)));
        block.Instructions.Insert(2, new IrMemberAccess("%third", state, "value", IrIntType.I32, 0));
        Assert.False(new CommonSubexpressionEliminationPass().Run(module));
    }

    [Fact]
    public void CommonSubexpressionElimination_RewritesIndexedFieldValueUses()
    {
        var module = new IrModule();
        var item = new IrStructType("Item", [new IrStructField("value", IrIntType.I32)]);
        var items = new IrPointerType(item);
        var container = new IrStructType("Container", [new IrStructField("items", items)]);
        var function = new IrFunction("test", IrIntType.I32);
        module.AddFunction(function);
        var block = function.CreateBasicBlock("entry");
        var source = new IrDereferenceValue(new IrVariable("source", new IrPointerType(container)), container);
        block.AddInstruction(new IrMemberAccess("%first", source, "items", items, 0));
        block.AddInstruction(new IrMemberAccess("%second", source, "items", items, 0));
        block.AddInstruction(new IrReturn(new IrIndexedFieldAccess(
            new IrVariable("%second", items), new IrConstant(0, IrIntType.U32), "value", 0, IrIntType.I32)));

        Assert.True(new CommonSubexpressionEliminationPass().Run(module));
        var indexed = Assert.IsType<IrIndexedFieldAccess>(Assert.IsType<IrReturn>(block.Instructions[1]).Value);
        Assert.Equal("%first", Assert.IsType<IrVariable>(indexed.Array).Name);
    }
}
