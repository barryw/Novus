using Novus.IR;
using Novus.Optimizer.Passes;
using Novus.Transforms.Passes;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Comprehensive tests for optimizer passes to boost coverage.
/// Covers ConstantFoldingPass, ConstantPropagationPass, CopyPropagationPass, and CFGDeadCodeEliminationPass.
/// </summary>
public class OptimizerPassComprehensiveTests
{
    private IrBasicBlock CreateBlock(string name = "entry")
    {
        return new IrBasicBlock(name);
    }

    private IrFunction CreateFunction(string name = "test")
    {
        var function = new IrFunction(name, IrIntType.I32);
        function.BasicBlocks.Add(CreateBlock("entry"));
        return function;
    }

    #region ConstantFoldingPass Tests

    [Fact]
    public void ConstantFolding_Addition_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        var left = new IrConstant(5, IrIntType.I32);
        var right = new IrConstant(3, IrIntType.I32);
        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Add, left, right, IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        Assert.True(changed);
    }

    [Fact]
    public void ConstantFolding_Subtraction_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Sub,
            new IrConstant(10, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_Multiplication_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Mul,
            new IrConstant(4, IrIntType.I32),
            new IrConstant(7, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_Division_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Div,
            new IrConstant(20, IrIntType.I32),
            new IrConstant(4, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_DivisionByZero_DoesNotFold()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Div,
            new IrConstant(10, IrIntType.I32),
            new IrConstant(0, IrIntType.I32),
            IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        // Should NOT fold division by zero
        Assert.False(changed);
        Assert.Single(block.Instructions);
    }

    [Fact]
    public void ConstantFolding_Modulo_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Mod,
            new IrConstant(17, IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_BitwiseAnd_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.And,
            new IrConstant(0xFF, IrIntType.I32),
            new IrConstant(0x0F, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_BitwiseOr_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Or,
            new IrConstant(0xF0, IrIntType.I32),
            new IrConstant(0x0F, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_BitwiseXor_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Xor,
            new IrConstant(0xFF, IrIntType.I32),
            new IrConstant(0xFF, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_LeftShift_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Shl,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_RightShift_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Shr,
            new IrConstant(20, IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_Comparison_Eq_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Eq,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrBoolType.Instance));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_Comparison_Ne_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Ne,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrBoolType.Instance));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_Comparison_Lt_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Lt,
            new IrConstant(3, IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrBoolType.Instance));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_Comparison_Le_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Le,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrBoolType.Instance));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_Comparison_Gt_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Gt,
            new IrConstant(7, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrBoolType.Instance));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_Comparison_Ge_FoldsCorrectly()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Ge,
            new IrConstant(5, IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrBoolType.Instance));

        Assert.True(pass.RunOnBasicBlock(block));
    }

    [Fact]
    public void ConstantFolding_NonConstantOperands_DoesNotFold()
    {
        var pass = new ConstantFoldingPass();
        var block = CreateBlock();

        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32));

        Assert.False(pass.RunOnBasicBlock(block));
    }

    #endregion

    #region ConstantPropagationPass Tests

    [Fact]
    public void ConstantPropagation_SimpleConstant_Propagates()
    {
        var pass = new ConstantPropagationPass();
        var block = CreateBlock();

        // x = 5; y = x + 3 => x = 5; y = 5 + 3
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        Assert.True(changed);
        var binOp = (IrBinaryOp)block.Instructions[1];
        Assert.IsType<IrConstant>(binOp.Left);
        Assert.Equal(5L, ((IrConstant)binOp.Left).Value);
    }

    [Fact]
    public void ConstantPropagation_MultipleUses_PropagatesAll()
    {
        var pass = new ConstantPropagationPass();
        var block = CreateBlock();

        // x = 10; a = x + 1; b = x * 2
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("%a", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("%b", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        Assert.True(changed);
        var binOp1 = (IrBinaryOp)block.Instructions[1];
        var binOp2 = (IrBinaryOp)block.Instructions[2];
        Assert.IsType<IrConstant>(binOp1.Left);
        Assert.IsType<IrConstant>(binOp2.Left);
    }

    [Fact]
    public void ConstantPropagation_ChainedConstants_Propagates()
    {
        var pass = new ConstantPropagationPass();
        var block = CreateBlock();

        // x = 5; y = x; z = y + 1 => x = 5; y = 5; z = 5 + 1
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrVariable("x", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("%z", IrBinaryOp.OpKind.Add,
            new IrVariable("y", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        Assert.True(changed);
        var yDecl = (IrLocalDecl)block.Instructions[1];
        var binOp = (IrBinaryOp)block.Instructions[2];
        Assert.IsType<IrConstant>(yDecl.InitialValue);
        Assert.IsType<IrConstant>(binOp.Left);
    }

    [Fact]
    public void ConstantPropagation_ReturnValue_Propagates()
    {
        var pass = new ConstantPropagationPass();
        var block = CreateBlock();

        // x = 42; return x => x = 42; return 42
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(42, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        var changed = pass.RunOnBasicBlock(block);

        Assert.True(changed);
        var ret = (IrReturn)block.Instructions[1];
        Assert.IsType<IrConstant>(ret.Value);
        Assert.Equal(42L, ((IrConstant)ret.Value).Value);
    }

    [Fact]
    public void ConstantPropagation_StoreValue_Propagates()
    {
        var pass = new ConstantPropagationPass();
        var block = CreateBlock();

        // x = 7; y = x (via store) => propagate
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(7, IrIntType.I32)));
        block.Instructions.Add(new IrStore("y", new IrVariable("x", IrIntType.I32)));

        var changed = pass.RunOnBasicBlock(block);

        Assert.True(changed);
        var store = (IrStore)block.Instructions[1];
        Assert.IsType<IrConstant>(store.Value);
        Assert.Equal(7L, ((IrConstant)store.Value).Value);
    }

    [Fact]
    public void ConstantPropagation_Reassignment_InvalidatesConstant()
    {
        var pass = new ConstantPropagationPass();
        var block = CreateBlock();

        // x = 5; x = something_else; y = x => should NOT propagate 5 to y
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrStore("x", new IrVariable("other", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("%y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        // No propagation should happen after reassignment
        var binOp = (IrBinaryOp)block.Instructions[2];
        Assert.IsType<IrVariable>(binOp.Left);
    }

    [Fact]
    public void ConstantPropagation_FunctionCall_ClearsConstants()
    {
        var pass = new ConstantPropagationPass();
        var block = CreateBlock();

        // x = 5; call foo(); y = x => should NOT propagate (foo might modify x)
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrCall("foo", IrVoidType.Instance));
        block.Instructions.Add(new IrBinaryOp("%y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        // Should not propagate after function call
        var binOp = (IrBinaryOp)block.Instructions[2];
        Assert.IsType<IrVariable>(binOp.Left);
    }

    [Fact]
    public void ConstantPropagation_NonConstant_DoesNotPropagate()
    {
        var pass = new ConstantPropagationPass();
        var block = CreateBlock();

        // x = param; y = x => should NOT propagate
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrVariable("param", IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("%y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        Assert.False(changed);
    }

    #endregion

    #region InlineExpansionPass Tests

    [Fact]
    public void InlineExpansion_SimpleFunction_Inlines()
    {
        // Create a simple function: fn add(a: i32, b: i32) -> i32 { return a + b }
        var addFunc = new IrFunction("add", IrIntType.I32);
        addFunc.Parameters.Add(new IrParameter("a", IrIntType.I32));
        addFunc.Parameters.Add(new IrParameter("b", IrIntType.I32));

        var addBlock = new IrBasicBlock("entry");
        addBlock.Instructions.Add(new IrBinaryOp("%sum", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        addBlock.Instructions.Add(new IrReturn(new IrVariable("%sum", IrIntType.I32)));
        addFunc.BasicBlocks.Add(addBlock);

        // Create main function that calls add
        var mainFunc = new IrFunction("main", IrIntType.I32);
        var mainBlock = new IrBasicBlock("entry");

        var call = new IrCall("add", IrIntType.I32, "%result");
        call.Arguments.Add(new IrConstant(5, IrIntType.I32));
        call.Arguments.Add(new IrConstant(3, IrIntType.I32));
        mainBlock.Instructions.Add(call);
        mainBlock.Instructions.Add(new IrReturn(new IrVariable("%result", IrIntType.I32)));
        mainFunc.BasicBlocks.Add(mainBlock);

        var module = new IrModule();
        module.Functions.Add(addFunc);
        module.Functions.Add(mainFunc);

        var pass = new InlineExpansionPass();
        var changed = pass.Transform(module);

        Assert.True(changed);
        // Call should be replaced with inlined instructions
        Assert.DoesNotContain(mainBlock.Instructions, i => i is IrCall);
        // Should have parameter assignments + binary op + result assignment + return
        Assert.True(mainBlock.Instructions.Count >= 3);
    }

    [Fact]
    public void InlineExpansion_RecursiveFunction_NotInlined()
    {
        // Create recursive function: fn factorial(n: i32) -> i32 { if n <= 1 return 1 else return n * factorial(n-1) }
        var factFunc = new IrFunction("factorial", IrIntType.I32);
        factFunc.Parameters.Add(new IrParameter("n", IrIntType.I32));

        var block = new IrBasicBlock("entry");
        var call = new IrCall("factorial", IrIntType.I32, "%rec");
        call.Arguments.Add(new IrVariable("n", IrIntType.I32));
        block.Instructions.Add(call);
        block.Instructions.Add(new IrReturn(new IrVariable("%rec", IrIntType.I32)));
        factFunc.BasicBlocks.Add(block);

        var pass = new InlineExpansionPass();
        Assert.False(pass.IsInlinable(factFunc));
    }

    [Fact]
    public void InlineExpansion_TooLargeFunction_NotInlined()
    {
        // Create a function with too many instructions
        var largeFunc = new IrFunction("large", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        // Add 25 instructions (exceeds MaxInlineInstructions = 20)
        for (int i = 0; i < 25; i++)
        {
            block.Instructions.Add(new IrBinaryOp($"%temp{i}", IrBinaryOp.OpKind.Add,
                new IrConstant(i, IrIntType.I32),
                new IrConstant(1, IrIntType.I32),
                IrIntType.I32));
        }
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));
        largeFunc.BasicBlocks.Add(block);

        var pass = new InlineExpansionPass();
        Assert.False(pass.IsInlinable(largeFunc));
    }

    [Fact]
    public void InlineExpansion_MultiBlockFunction_NotInlined()
    {
        // Create a function with 4 basic blocks (exceeds limit of 3)
        var multiBlockFunc = new IrFunction("multi", IrIntType.I32);
        multiBlockFunc.BasicBlocks.Add(new IrBasicBlock("entry"));
        multiBlockFunc.BasicBlocks.Add(new IrBasicBlock("block1"));
        multiBlockFunc.BasicBlocks.Add(new IrBasicBlock("block2"));
        multiBlockFunc.BasicBlocks.Add(new IrBasicBlock("block3"));

        var pass = new InlineExpansionPass();
        Assert.False(pass.IsInlinable(multiBlockFunc));
    }

    [Fact]
    public void InlineExpansion_MainFunction_NotInlined()
    {
        var mainFunc = new IrFunction("main", IrIntType.I32);
        var block = new IrBasicBlock("entry");
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));
        mainFunc.BasicBlocks.Add(block);

        var pass = new InlineExpansionPass();
        Assert.False(pass.IsInlinable(mainFunc));
    }

    [Fact]
    public void InlineExpansion_MultipleCallsToSameFunction_AllInlined()
    {
        // Create simple add function
        var addFunc = new IrFunction("add", IrIntType.I32);
        addFunc.Parameters.Add(new IrParameter("a", IrIntType.I32));
        addFunc.Parameters.Add(new IrParameter("b", IrIntType.I32));

        var addBlock = new IrBasicBlock("entry");
        addBlock.Instructions.Add(new IrBinaryOp("%sum", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        addBlock.Instructions.Add(new IrReturn(new IrVariable("%sum", IrIntType.I32)));
        addFunc.BasicBlocks.Add(addBlock);

        // Create main with multiple calls
        var mainFunc = new IrFunction("main", IrIntType.I32);
        var mainBlock = new IrBasicBlock("entry");

        var call1 = new IrCall("add", IrIntType.I32, "%r1");
        call1.Arguments.Add(new IrConstant(1, IrIntType.I32));
        call1.Arguments.Add(new IrConstant(2, IrIntType.I32));
        mainBlock.Instructions.Add(call1);

        var call2 = new IrCall("add", IrIntType.I32, "%r2");
        call2.Arguments.Add(new IrConstant(3, IrIntType.I32));
        call2.Arguments.Add(new IrConstant(4, IrIntType.I32));
        mainBlock.Instructions.Add(call2);

        var call3 = new IrCall("add", IrIntType.I32, "%r3");
        call3.Arguments.Add(new IrVariable("%r1", IrIntType.I32));
        call3.Arguments.Add(new IrVariable("%r2", IrIntType.I32));
        mainBlock.Instructions.Add(call3);

        mainBlock.Instructions.Add(new IrReturn(new IrVariable("%r3", IrIntType.I32)));
        mainFunc.BasicBlocks.Add(mainBlock);

        var module = new IrModule();
        module.Functions.Add(addFunc);
        module.Functions.Add(mainFunc);

        var pass = new InlineExpansionPass();
        var changed = pass.Transform(module);

        Assert.True(changed);
        // All three calls should be inlined
        Assert.DoesNotContain(mainBlock.Instructions, i => i is IrCall);
    }

    [Fact]
    public void InlineExpansion_VariableRenaming_AvoidsConflicts()
    {
        // Create a function with local variables
        var testFunc = new IrFunction("test", IrIntType.I32);
        testFunc.Parameters.Add(new IrParameter("x", IrIntType.I32));

        var testBlock = new IrBasicBlock("entry");
        testBlock.Instructions.Add(new IrLocalDecl("temp", IrIntType.I32, false, new IrVariable("x", IrIntType.I32)));
        testBlock.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Mul,
            new IrVariable("temp", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        testBlock.Instructions.Add(new IrReturn(new IrVariable("%result", IrIntType.I32)));
        testFunc.BasicBlocks.Add(testBlock);

        // Create main that has its own "temp" variable
        var mainFunc = new IrFunction("main", IrIntType.I32);
        var mainBlock = new IrBasicBlock("entry");
        mainBlock.Instructions.Add(new IrLocalDecl("temp", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));

        var call = new IrCall("test", IrIntType.I32, "%r");
        call.Arguments.Add(new IrVariable("temp", IrIntType.I32));
        mainBlock.Instructions.Add(call);
        mainBlock.Instructions.Add(new IrReturn(new IrVariable("%r", IrIntType.I32)));
        mainFunc.BasicBlocks.Add(mainBlock);

        var module = new IrModule();
        module.Functions.Add(testFunc);
        module.Functions.Add(mainFunc);

        var pass = new InlineExpansionPass();
        var changed = pass.Transform(module);

        Assert.True(changed);
        // Call should be replaced
        Assert.DoesNotContain(mainBlock.Instructions, i => i is IrCall);

        // Check that inlined variables have unique names (prefixed with %inline_)
        var inlinedDecls = mainBlock.Instructions.OfType<IrLocalDecl>()
            .Where(d => d.Name.StartsWith("%inline_"));
        Assert.NotEmpty(inlinedDecls);
    }

    [Fact]
    public void InlineExpansion_VoidFunction_Inlines()
    {
        // Create void function (no return value)
        var voidFunc = new IrFunction("doSomething", IrVoidType.Instance);
        voidFunc.Parameters.Add(new IrParameter("x", IrIntType.I32));

        var voidBlock = new IrBasicBlock("entry");
        voidBlock.Instructions.Add(new IrBinaryOp("%temp", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        voidBlock.Instructions.Add(new IrReturn(null)); // void return
        voidFunc.BasicBlocks.Add(voidBlock);

        var mainFunc = new IrFunction("main", IrIntType.I32);
        var mainBlock = new IrBasicBlock("entry");

        var call = new IrCall("doSomething", IrVoidType.Instance);
        call.Arguments.Add(new IrConstant(5, IrIntType.I32));
        mainBlock.Instructions.Add(call);
        mainBlock.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));
        mainFunc.BasicBlocks.Add(mainBlock);

        var module = new IrModule();
        module.Functions.Add(voidFunc);
        module.Functions.Add(mainFunc);

        var pass = new InlineExpansionPass();
        var changed = pass.Transform(module);

        Assert.True(changed);
        Assert.DoesNotContain(mainBlock.Instructions, i => i is IrCall);
    }

    #endregion

    #region CopyPropagationPass Tests


    [Fact]
    public void CopyPropagation_SimpleCopy_Propagates()
    {
        var pass = new CopyPropagationPass();
        var block = CreateBlock();

        // x = y (parameter)
        // z = x  =>  should become z = y
        var y = new IrVariable("y", IrIntType.I32);
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, y));
        block.Instructions.Add(new IrLocalDecl("z", IrIntType.I32, false, new IrVariable("x", IrIntType.I32)));

        var changed = pass.RunOnBasicBlock(block);

        Assert.True(changed);
    }

    [Fact]
    public void CopyPropagation_CopyInvalidation_StopsPropagation()
    {
        var pass = new CopyPropagationPass();
        var block = CreateBlock();

        // x = y
        // y = 5  (invalidates copy)
        // z = x  (should NOT propagate to y)
        var y = new IrVariable("y", IrIntType.I32);
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, y));
        block.Instructions.Add(new IrStore("y", new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("z", IrIntType.I32, false, new IrVariable("x", IrIntType.I32)));

        pass.RunOnBasicBlock(block);

        // x should still be x (not propagated) because y was modified
        var zDecl = (IrLocalDecl)block.Instructions[2];
        Assert.IsType<IrVariable>(zDecl.InitialValue);
    }

    [Fact]
    public void CopyPropagation_InBinaryOp_Propagates()
    {
        var pass = new CopyPropagationPass();
        var block = CreateBlock();

        // a = param
        // result = a + 5  =>  result = param + 5
        var param = new IrVariable("param", IrIntType.I32);
        block.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, param));
        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrIntType.I32));

        var changed = pass.RunOnBasicBlock(block);

        Assert.True(changed);
    }

    [Fact]
    public void CopyPropagation_FunctionCall_ClearsAllCopies()
    {
        var pass = new CopyPropagationPass();
        var block = CreateBlock();

        // x = y
        // call foo()  (may modify anything)
        // z = x  (should NOT propagate)
        var y = new IrVariable("y", IrIntType.I32);
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, y));
        block.Instructions.Add(new IrCall("foo", IrVoidType.Instance));
        block.Instructions.Add(new IrLocalDecl("z", IrIntType.I32, false, new IrVariable("x", IrIntType.I32)));

        pass.RunOnBasicBlock(block);

        // Should clear copies after function call
        Assert.Equal(3, block.Instructions.Count);
    }

    #endregion

    #region CFGDeadCodeEliminationPass Tests

    [Fact]
    public void CFG_DCE_DeadAssignment_Removed()
    {
        var pass = new CFGDeadCodeEliminationPass();
        var function = CreateFunction();
        var block = function.BasicBlocks[0];

        // Dead assignment: x = 5 (never used)
        block.Instructions.Add(new IrBinaryOp("%x", IrBinaryOp.OpKind.Add,
            new IrConstant(2, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        var changed = pass.RunOnFunction(function);

        Assert.True(changed);
        // Dead instruction should be removed
        Assert.Single(block.Instructions); // Only return remains
    }

    [Fact]
    public void CFG_DCE_LiveReturn_NotRemoved()
    {
        var pass = new CFGDeadCodeEliminationPass();
        var function = CreateFunction();
        var block = function.BasicBlocks[0];

        // Live: x = 5, return x
        block.Instructions.Add(new IrBinaryOp("%x", IrBinaryOp.OpKind.Add,
            new IrConstant(2, IrIntType.I32),
            new IrConstant(3, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("%x", IrIntType.I32)));

        var changed = pass.RunOnFunction(function);

        Assert.False(changed);
        Assert.Equal(2, block.Instructions.Count); // Both kept
    }

    #endregion

    #region ConstantPropagation Const Fn Optimization Tests

    /// <summary>
    /// Helper to create a simple const fn for testing.
    /// const fn double(x: i32) -> i32 { x * 2 }
    /// </summary>
    private IrFunction CreateDoubleConstFn()
    {
        var function = new IrFunction("double", IrIntType.I32) { IsConstFn = true };
        function.Parameters.Add(new IrParameter("x", IrIntType.I32));

        var block = new IrBasicBlock("entry");
        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Mul,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("%result", IrIntType.I32)));

        function.BasicBlocks.Add(block);
        return function;
    }

    /// <summary>
    /// Helper to create a simple const fn: const fn add(a: i32, b: i32) -> i32 { a + b }
    /// </summary>
    private IrFunction CreateAddConstFn()
    {
        var function = new IrFunction("add", IrIntType.I32) { IsConstFn = true };
        function.Parameters.Add(new IrParameter("a", IrIntType.I32));
        function.Parameters.Add(new IrParameter("b", IrIntType.I32));

        var block = new IrBasicBlock("entry");
        block.Instructions.Add(new IrBinaryOp("%result", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrVariable("b", IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("%result", IrIntType.I32)));

        function.BasicBlocks.Add(block);
        return function;
    }

    [Fact]
    public void ConstantPropagation_ConstFnCall_WithConstantArgs_IsEvaluatedAtCompileTime()
    {
        // Create module with const fn double(x) = x * 2
        var module = new IrModule();
        module.Functions.Add(CreateDoubleConstFn());

        // Create main function: let y = double(5)
        var mainFn = new IrFunction("main", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        var call = new IrCall("double", IrIntType.I32, "%y");
        call.Arguments.Add(new IrConstant(5, IrIntType.I32));
        block.Instructions.Add(call);
        block.Instructions.Add(new IrReturn(new IrVariable("%y", IrIntType.I32)));

        mainFn.BasicBlocks.Add(block);
        module.Functions.Add(mainFn);

        // Run constant propagation
        var pass = new ConstantPropagationPass();
        var changed = pass.Run(module);

        // The call should be replaced with a store of constant 10
        Assert.True(changed);
        var firstInstr = mainFn.BasicBlocks[0].Instructions[0];
        Assert.IsType<IrStore>(firstInstr);
        var store = (IrStore)firstInstr;
        Assert.Equal("%y", store.VariableName);
        Assert.IsType<IrConstant>(store.Value);
        Assert.Equal(10L, ((IrConstant)store.Value).Value);
    }

    [Fact]
    public void ConstantPropagation_ConstFnCall_WithNonConstantArgs_IsNotEvaluated()
    {
        // Create module with const fn double(x) = x * 2
        var module = new IrModule();
        module.Functions.Add(CreateDoubleConstFn());

        // Create main function: let y = double(param)  -- param is not constant
        var mainFn = new IrFunction("main", IrIntType.I32);
        mainFn.Parameters.Add(new IrParameter("param", IrIntType.I32));

        var block = new IrBasicBlock("entry");
        var call = new IrCall("double", IrIntType.I32, "%y");
        call.Arguments.Add(new IrVariable("param", IrIntType.I32));
        block.Instructions.Add(call);
        block.Instructions.Add(new IrReturn(new IrVariable("%y", IrIntType.I32)));

        mainFn.BasicBlocks.Add(block);
        module.Functions.Add(mainFn);

        // Run constant propagation
        var pass = new ConstantPropagationPass();
        var changed = pass.Run(module);

        // The call should NOT be replaced (args aren't constant)
        Assert.False(changed);
        var firstInstr = mainFn.BasicBlocks[0].Instructions[0];
        Assert.IsType<IrCall>(firstInstr);
    }

    [Fact]
    public void ConstantPropagation_ConstFnCall_DoesNotClearConstantValues()
    {
        // Create module with const fn double(x) = x * 2
        var module = new IrModule();
        module.Functions.Add(CreateDoubleConstFn());

        // Create main function:
        // let x = 5;
        // let y = double(x);  -- const fn, should not clear x's constant tracking
        // let z = x + 1;      -- should still propagate x = 5
        var mainFn = new IrFunction("main", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));

        var call = new IrCall("double", IrIntType.I32, "%y");
        call.Arguments.Add(new IrVariable("x", IrIntType.I32));
        block.Instructions.Add(call);

        block.Instructions.Add(new IrBinaryOp("%z", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));

        block.Instructions.Add(new IrReturn(new IrVariable("%z", IrIntType.I32)));

        mainFn.BasicBlocks.Add(block);
        module.Functions.Add(mainFn);

        // Run constant propagation
        var pass = new ConstantPropagationPass();
        var changed = pass.Run(module);

        // x should still be propagated to the binary op
        Assert.True(changed);
        var binOp = (IrBinaryOp)mainFn.BasicBlocks[0].Instructions[2];
        Assert.IsType<IrConstant>(binOp.Left);
        Assert.Equal(5L, ((IrConstant)binOp.Left).Value);
    }

    [Fact]
    public void ConstantPropagation_NonConstFnCall_ClearsConstantValues()
    {
        // Create module with a regular (non-const) function
        var module = new IrModule();
        var nonConstFn = new IrFunction("side_effect_fn", IrIntType.I32);
        nonConstFn.BasicBlocks.Add(new IrBasicBlock("entry"));
        nonConstFn.BasicBlocks[0].Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));
        module.Functions.Add(nonConstFn);

        // Create main function:
        // let x = 5;
        // side_effect_fn();  -- non-const fn, should clear constant tracking
        // let z = x + 1;     -- should NOT propagate x = 5
        var mainFn = new IrFunction("main", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(5, IrIntType.I32)));
        block.Instructions.Add(new IrCall("side_effect_fn", IrIntType.I32));
        block.Instructions.Add(new IrBinaryOp("%z", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrReturn(new IrVariable("%z", IrIntType.I32)));

        mainFn.BasicBlocks.Add(block);
        module.Functions.Add(mainFn);

        // Run constant propagation
        var pass = new ConstantPropagationPass();
        var changed = pass.Run(module);

        // x should NOT be propagated (non-const fn might have modified it)
        var binOp = (IrBinaryOp)mainFn.BasicBlocks[0].Instructions[2];
        Assert.IsType<IrVariable>(binOp.Left);
    }

    [Fact]
    public void ConstantPropagation_ConstFnCall_Memoization_CachesResults()
    {
        // Create module with const fn add(a, b) = a + b
        var module = new IrModule();
        module.Functions.Add(CreateAddConstFn());

        // Create main function with two identical calls:
        // let x = add(3, 4);
        // let y = add(3, 4);  -- should use cached result
        var mainFn = new IrFunction("main", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        var call1 = new IrCall("add", IrIntType.I32, "%x");
        call1.Arguments.Add(new IrConstant(3, IrIntType.I32));
        call1.Arguments.Add(new IrConstant(4, IrIntType.I32));
        block.Instructions.Add(call1);

        var call2 = new IrCall("add", IrIntType.I32, "%y");
        call2.Arguments.Add(new IrConstant(3, IrIntType.I32));
        call2.Arguments.Add(new IrConstant(4, IrIntType.I32));
        block.Instructions.Add(call2);

        block.Instructions.Add(new IrReturn(new IrVariable("%y", IrIntType.I32)));

        mainFn.BasicBlocks.Add(block);
        module.Functions.Add(mainFn);

        // Run constant propagation
        var pass = new ConstantPropagationPass();
        var changed = pass.Run(module);

        Assert.True(changed);

        // Both calls should be replaced with stores of constant 7
        var store1 = (IrStore)mainFn.BasicBlocks[0].Instructions[0];
        var store2 = (IrStore)mainFn.BasicBlocks[0].Instructions[1];

        Assert.Equal(7L, ((IrConstant)store1.Value).Value);
        Assert.Equal(7L, ((IrConstant)store2.Value).Value);
    }

    [Fact]
    public void ConstantPropagation_ConstFnCall_PropagatesResultThroughLaterUses()
    {
        // Create module with const fn double(x) = x * 2
        var module = new IrModule();
        module.Functions.Add(CreateDoubleConstFn());

        // Create main function:
        // let x = double(5);  -- evaluates to 10
        // let y = x + 1;      -- should use 10
        // return y;           -- should use 11 (after constant folding would run)
        var mainFn = new IrFunction("main", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        var call = new IrCall("double", IrIntType.I32, "%x");
        call.Arguments.Add(new IrConstant(5, IrIntType.I32));
        block.Instructions.Add(call);

        block.Instructions.Add(new IrBinaryOp("%y", IrBinaryOp.OpKind.Add,
            new IrVariable("%x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));

        block.Instructions.Add(new IrReturn(new IrVariable("%y", IrIntType.I32)));

        mainFn.BasicBlocks.Add(block);
        module.Functions.Add(mainFn);

        // Run constant propagation
        var pass = new ConstantPropagationPass();
        var changed = pass.Run(module);

        Assert.True(changed);

        // The result of double(5) = 10 should be propagated to the binary op
        var binOp = (IrBinaryOp)mainFn.BasicBlocks[0].Instructions[1];
        Assert.IsType<IrConstant>(binOp.Left);
        Assert.Equal(10L, ((IrConstant)binOp.Left).Value);
    }

    #endregion
}
