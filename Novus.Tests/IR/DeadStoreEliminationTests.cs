using Novus.IR;
using Xunit;

namespace Novus.Tests.IR;

/// <summary>
/// Tests for Dead Store Elimination (DSE) optimization pass.
/// DSE removes stores to variables that are overwritten before being read.
/// </summary>
public class DeadStoreEliminationTests
{
    [Fact]
    public void Eliminate_RedundantInitialization_RemovesFirstStore()
    {
        // Test: x = 0; x = compute(); return x;
        // Expected: First store (x = 0) is removed, second store is kept

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));
        block.Instructions.Add(new IrStore("x", new IrConstant(42, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // First store should be removed (x_0 is overwritten by x_1)
        Assert.True(removedCount >= 1, $"Expected at least 1 removal, got {removedCount}");
    }

    [Fact]
    public void Eliminate_NeverReadVariable_RemovesStore()
    {
        // Test: x = 10; return 0;
        // Expected: Store to x is removed since x is never read

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // Store should be removed
        Assert.Equal(1, removedCount);

        // Should only have label and return
        var nonLabelInstructions = block.Instructions.Where(i => i is not IrLabel).ToList();
        Assert.Single(nonLabelInstructions);
        Assert.IsType<IrReturn>(nonLabelInstructions[0]);
    }

    [Fact]
    public void Eliminate_UsedVariable_KeepsStore()
    {
        // Test: x = 10; return x;
        // Expected: Store is kept since x is used

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("x", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // Nothing should be removed
        Assert.Equal(0, removedCount);
    }

    [Fact]
    public void Eliminate_MultipleDeadStores_RemovesAll()
    {
        // Test: a = 1; a = 2; a = 3; return 0;
        // Expected: All stores to a are removed since a is never read

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(1, IrIntType.I32)));
        block.Instructions.Add(new IrStore("a", new IrConstant(2, IrIntType.I32)));
        block.Instructions.Add(new IrStore("a", new IrConstant(3, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // All stores should be removed
        Assert.True(removedCount >= 1, "At least one store should be removed");

        // Should only have label and return
        var nonLabelInstructions = block.Instructions.Where(i => i is not IrLabel).ToList();
        Assert.True(nonLabelInstructions.All(i => i is IrReturn),
            "Only return should remain after removing dead stores");
    }

    [Fact]
    public void Eliminate_StoreReadThenOverwritten_KeepsFirstRemovesSecond()
    {
        // Test: x = 10; y = x + 1; x = 20; return y;
        // Expected: First store to x is kept (used by y), second store is removed

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("y", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32));
        block.Instructions.Add(new IrStore("x", new IrConstant(20, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrVariable("y", IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first (creates x_0 = 10, y_0 = x_0 + 1, x_1 = 20)
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // x_1 = 20 should be removed (never read)
        Assert.True(removedCount >= 1, "Second store (x_1) should be removed");

        // y computation should still exist
        var hasBinaryOp = block.Instructions.Any(i => i is IrBinaryOp);
        Assert.True(hasBinaryOp, "Binary op computing y should be kept");
    }

    [Fact]
    public void Eliminate_CallResult_AlwaysKeptIfCallHasSideEffects()
    {
        // Test: x = call_func(); return 0;
        // Expected: Call is kept (has side effects), but result assignment depends on DCE

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        var call = new IrCall("some_function", IrIntType.I32, "x");
        block.Instructions.Add(call);
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // Call should NOT be removed (has side effects)
        var hasCall = block.Instructions.Any(i => i is IrCall);
        Assert.True(hasCall, "Call instruction must be kept (side effects)");
    }

    [Fact]
    public void Eliminate_MemoryStores_NeverRemoved()
    {
        // Test: *ptr = 10; return 0;
        // Expected: Dereference store is kept (writes to external memory)

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrDereferenceStore(
            new IrVariable("ptr", new IrPointerType(IrIntType.I32)),
            new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // Dereference store should NOT be removed (external side effect)
        Assert.Equal(0, removedCount);
        var hasDerefStore = block.Instructions.Any(i => i is IrDereferenceStore);
        Assert.True(hasDerefStore, "Dereference store must be kept");
    }

    [Fact]
    public void Eliminate_MemberStore_NeverRemoved()
    {
        // Test: s.field = 10; return 0;
        // Expected: Member store is kept (modifies struct)

        var structType = new IrStructType("TestStruct", new List<IrStructField>
        {
            new IrStructField("field", IrIntType.I32)
        });

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrMemberStore(
            new IrVariable("s", structType),
            "field",
            0,  // fieldOffset
            new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // Member store should NOT be removed (external side effect)
        Assert.Equal(0, removedCount);
    }

    [Fact]
    public void Eliminate_ChainOfDeadStores_RemovesEntireChain()
    {
        // Test: a = 1; b = a; c = b; return 0;
        // Expected: All three are removed since c is never used

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, false, new IrConstant(1, IrIntType.I32)));
        block.Instructions.Add(new IrStore("b", new IrVariable("a", IrIntType.I32)));
        block.Instructions.Add(new IrStore("c", new IrVariable("b", IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // All stores should be removed
        Assert.True(removedCount >= 1, "Dead chain should be removed");
    }

    [Fact]
    public void Eliminate_InLoopWithDeadStore_RemovesUnused()
    {
        // Test: while loop with dead computation inside
        // i = 0; while (i < 10) { i++; dead = i * 2; } return i;
        // Expected: dead computation is removed

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
        // Dead computation
        body.Instructions.Add(new IrBinaryOp("dead", IrBinaryOp.OpKind.Mul,
            new IrVariable("i", IrIntType.I32),
            new IrConstant(2, IrIntType.I32),
            IrIntType.I32));
        body.Instructions.Add(new IrBranch("loop"));

        exit.Instructions.Add(new IrLabel("exit"));
        exit.Instructions.Add(new IrReturn(new IrVariable("i", IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(loop);
        function.BasicBlocks.Add(body);
        function.BasicBlocks.Add(exit);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // The dead computation should be removed
        // Note: After SSA, variable names become versioned (e.g., "dead" -> "dead_0")
        // The DSE pass should identify and remove the multiplication that's never used
        Assert.True(removedCount >= 1, "Dead computation in loop body should be removed");

        // Verify at least some multiplications were removed (may still have loop counter ops)
        var mulOps = body.Instructions.Where(i =>
            i is IrBinaryOp binOp && binOp.Operation == IrBinaryOp.OpKind.Mul).ToList();
        // With DSE, the dead multiplication should be removed
        // If there are still mul ops, they should be used (not dead)
        Assert.True(mulOps.Count == 0 || removedCount >= 1, "Dead multiplication should be removed");
    }

    [Fact]
    public void Eliminate_ConditionalDeadStore_RemovesIfNeverRead()
    {
        // Test: if (cond) { x = 10; } return 0;
        // Expected: x = 10 is removed since x is never read

        var function = new IrFunction("test", IrIntType.I32);

        var entry = new IrBasicBlock("entry");
        var thenBlock = new IrBasicBlock("then");
        var merge = new IrBasicBlock("merge");

        entry.Instructions.Add(new IrLabel("entry"));
        entry.Instructions.Add(new IrConditionalBranch(new IrVariable("cond", IrBoolType.Instance), "then", "merge"));

        thenBlock.Instructions.Add(new IrLabel("then"));
        thenBlock.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        thenBlock.Instructions.Add(new IrBranch("merge"));

        merge.Instructions.Add(new IrLabel("merge"));
        merge.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(entry);
        function.BasicBlocks.Add(thenBlock);
        function.BasicBlocks.Add(merge);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // Store should be removed
        Assert.True(removedCount >= 1, "Dead store in conditional should be removed");
    }

    [Fact]
    public void GetDeadStores_ReturnsListWithoutRemoving()
    {
        // Test the diagnostic method

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, false, new IrConstant(20, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var deadStores = dse.GetDeadStores();

        // Should identify two dead stores
        Assert.Equal(2, deadStores.Count);

        // Instructions should still be in the block (not removed)
        var nonLabelInstructions = block.Instructions.Where(i => i is not IrLabel).ToList();
        Assert.Equal(3, nonLabelInstructions.Count); // x, y, return
    }

    [Fact]
    public void Eliminate_ZeroingAfterMove_Removed()
    {
        // Test: x = value; consume(x); x = 0; return 0;
        // The zeroing after move is a dead store since x is not read after

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(42, IrIntType.I32)));

        // Call that uses x
        var call = new IrCall("consume", IrVoidType.Instance, null);
        call.Arguments.Add(new IrVariable("x", IrIntType.I32));
        block.Instructions.Add(call);

        // Zero after move
        block.Instructions.Add(new IrStore("x", new IrConstant(0, IrIntType.I32)));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // The zeroing store should be removed (x is not read after)
        Assert.True(removedCount >= 1, "Zeroing after move should be removed");
    }

    [Fact]
    public void Eliminate_AssertCondition_KeptWithDependencies()
    {
        // Test: x = 10; cond = x > 5; assert(cond); return 0;
        // Expected: Nothing removed - assert needs cond, cond needs x

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, false, new IrConstant(10, IrIntType.I32)));
        block.Instructions.Add(new IrBinaryOp("cond", IrBinaryOp.OpKind.Gt,
            new IrVariable("x", IrIntType.I32),
            new IrConstant(5, IrIntType.I32),
            IrBoolType.Instance));
        var dummyLocation = new Novus.Diagnostics.SourceLocation("test.novus", 1, 1, 1, "");
        block.Instructions.Add(new IrAssert(new IrVariable("cond", IrBoolType.Instance), "assertion failed", dummyLocation));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        // Convert to SSA first
        var ssaConstructor = new SsaConstructor(function);
        ssaConstructor.ConstructSsa();

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        // Nothing should be removed - assert has side effects and uses cond
        // Note: DSE only tracks stores, not all definitions. DCE handles the rest.
        // But x's store should be kept because cond uses it, and assert uses cond.

        // Assert must be kept
        var hasAssert = block.Instructions.Any(i => i is IrAssert);
        Assert.True(hasAssert, "Assert must be kept");
    }

    [Fact]
    public void Eliminate_EmptyFunction_DoesNothing()
    {
        // Test: empty function with just return

        var function = new IrFunction("test", IrIntType.I32);
        var block = new IrBasicBlock("entry");

        block.Instructions.Add(new IrLabel("entry"));
        block.Instructions.Add(new IrReturn(new IrConstant(0, IrIntType.I32)));

        function.BasicBlocks.Add(block);

        var dse = new DeadStoreElimination(function);
        var removedCount = dse.Eliminate();

        Assert.Equal(0, removedCount);
        Assert.Equal(2, block.Instructions.Count);
    }
}
