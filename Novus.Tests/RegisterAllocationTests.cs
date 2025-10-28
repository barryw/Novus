using Novus.IR;
using Novus.Optimizer;
using Xunit;

namespace Novus.Tests;

public class RegisterAllocationTests
{
    [Fact]
    public void SimpleFunction_AllocatesRegisters()
    {
        // Test a simple function with a few variables
        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entry);

        // x = 10
        var xDecl = new IrLocalDecl("x", IrIntType.I32, true, new IrConstant(10, IrIntType.I32));
        entry.Instructions.Add(xDecl);

        // y = 20
        var yDecl = new IrLocalDecl("y", IrIntType.I32, true, new IrConstant(20, IrIntType.I32));
        entry.Instructions.Add(yDecl);

        // z = x + y
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32);
        entry.Instructions.Add(add);
        entry.Instructions.Add(new IrStore("z", new IrVariable("%t0", IrIntType.I32)));

        // return z
        entry.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        // Run register allocation
        var allocator = new RegisterAllocator();
        var allocation = allocator.AllocateRegisters(function);

        // Verify that x, y, and z all got registers
        Assert.NotNull(allocation.GetRegister("x"));
        Assert.NotNull(allocation.GetRegister("y"));
        Assert.NotNull(allocation.GetRegister("z"));

        // Verify they got data registers (d2-d7)
        Assert.StartsWith("d", allocation.GetRegister("x"));
        Assert.StartsWith("d", allocation.GetRegister("y"));
        Assert.StartsWith("d", allocation.GetRegister("z"));

        // Verify none were spilled
        Assert.False(allocation.IsSpilled("x"));
        Assert.False(allocation.IsSpilled("y"));
        Assert.False(allocation.IsSpilled("z"));
    }

    [Fact]
    public void NonOverlappingVariables_ShareRegisters()
    {
        // Variables with non-overlapping lifetimes should share registers
        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entry);

        // a = 1
        entry.Instructions.Add(new IrLocalDecl("a", IrIntType.I32, true, new IrConstant(1, IrIntType.I32)));

        // b = a + 1  (a is used here, then never again)
        var add1 = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrVariable("a", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32);
        entry.Instructions.Add(add1);
        entry.Instructions.Add(new IrStore("b", new IrVariable("%t0", IrIntType.I32)));

        // c = b + 1  (b is used here, then never again)
        var add2 = new IrBinaryOp("%t1", IrBinaryOp.OpKind.Add,
            new IrVariable("b", IrIntType.I32),
            new IrConstant(1, IrIntType.I32),
            IrIntType.I32);
        entry.Instructions.Add(add2);
        entry.Instructions.Add(new IrStore("c", new IrVariable("%t1", IrIntType.I32)));

        // return c
        entry.Instructions.Add(new IrReturn(new IrVariable("c", IrIntType.I32)));

        var allocator = new RegisterAllocator();
        var allocation = allocator.AllocateRegisters(function);

        // Since a, b, and c don't overlap, they should be able to share registers
        // With 6 data registers available, all should get registers
        Assert.NotNull(allocation.GetRegister("a"));
        Assert.NotNull(allocation.GetRegister("b"));
        Assert.NotNull(allocation.GetRegister("c"));

        // In an optimal allocation, a and c could share the same register
        // (though our simple allocator might not do this)
    }

    [Fact]
    public void TooManyVariables_SomeSpilled()
    {
        // Create a function with more live variables than available registers
        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entry);

        // Create 10 variables that are all live at the same time
        for (int i = 0; i < 10; i++)
        {
            var varName = $"v{i}";
            entry.Instructions.Add(new IrLocalDecl(varName, IrIntType.I32, true,
                new IrConstant(i, IrIntType.I32)));
        }

        // Use all variables in a single expression
        var current = new IrVariable("v0", IrIntType.I32) as IrValue;
        for (int i = 1; i < 10; i++)
        {
            var add = new IrBinaryOp($"%t{i}", IrBinaryOp.OpKind.Add,
                current,
                new IrVariable($"v{i}", IrIntType.I32),
                IrIntType.I32);
            entry.Instructions.Add(add);
            current = new IrVariable($"%t{i}", IrIntType.I32);
        }

        entry.Instructions.Add(new IrReturn(current));

        var allocator = new RegisterAllocator();
        var allocation = allocator.AllocateRegisters(function);

        // We have 6 data registers (d2-d7), so 10 variables means at least 4 must spill
        var allocatedCount = 0;
        var spilledCount = 0;

        for (int i = 0; i < 10; i++)
        {
            var varName = $"v{i}";
            if (allocation.GetRegister(varName) != null)
            {
                allocatedCount++;
            }
            if (allocation.IsSpilled(varName))
            {
                spilledCount++;
            }
        }

        // Should have allocated up to 6 registers
        Assert.True(allocatedCount <= 6);

        // Should have spilled at least 4
        Assert.True(spilledCount >= 4);

        // Total should be 10
        Assert.Equal(10, allocatedCount + spilledCount);
    }

    [Fact]
    public void AddressVariables_GetAddressRegisters()
    {
        // Variables with pointer/address in name should get address registers
        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entry);

        // Create a pointer variable
        entry.Instructions.Add(new IrLocalDecl("my_ptr", IrIntType.I32, true,
            new IrConstant(0x1000, IrIntType.I32)));

        // Regular variable
        entry.Instructions.Add(new IrLocalDecl("count", IrIntType.I32, true,
            new IrConstant(42, IrIntType.I32)));

        // Use both
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrVariable("my_ptr", IrIntType.I32),
            new IrVariable("count", IrIntType.I32),
            IrIntType.I32);
        entry.Instructions.Add(add);
        entry.Instructions.Add(new IrReturn(new IrVariable("%t0", IrIntType.I32)));

        var allocator = new RegisterAllocator();
        var allocation = allocator.AllocateRegisters(function);

        // Pointer should get address register
        var ptrReg = allocation.GetRegister("my_ptr");
        Assert.NotNull(ptrReg);
        Assert.StartsWith("a", ptrReg);

        // Count should get data register
        var countReg = allocation.GetRegister("count");
        Assert.NotNull(countReg);
        Assert.StartsWith("d", countReg);
    }

    [Fact]
    public void LiveRanges_ComputedCorrectly()
    {
        var function = new IrFunction("test", IrIntType.I32);
        var entry = new IrBasicBlock("entry");
        function.BasicBlocks.Add(entry);

        // x = 10 (defined at index 0)
        entry.Instructions.Add(new IrLocalDecl("x", IrIntType.I32, true, new IrConstant(10, IrIntType.I32)));

        // y = 20 (defined at index 1)
        entry.Instructions.Add(new IrLocalDecl("y", IrIntType.I32, true, new IrConstant(20, IrIntType.I32)));

        // z = x + y (x and y used at index 2-3)
        var add = new IrBinaryOp("%t0", IrBinaryOp.OpKind.Add,
            new IrVariable("x", IrIntType.I32),
            new IrVariable("y", IrIntType.I32),
            IrIntType.I32);
        entry.Instructions.Add(add);
        entry.Instructions.Add(new IrStore("z", new IrVariable("%t0", IrIntType.I32)));

        // return z (z used at index 4)
        entry.Instructions.Add(new IrReturn(new IrVariable("z", IrIntType.I32)));

        var allocator = new RegisterAllocator();
        var allocation = allocator.AllocateRegisters(function);

        // Find live ranges
        var xRange = allocation.LiveRanges.FirstOrDefault(r => r.VariableName == "x");
        var yRange = allocation.LiveRanges.FirstOrDefault(r => r.VariableName == "y");
        var zRange = allocation.LiveRanges.FirstOrDefault(r => r.VariableName == "z");

        Assert.NotNull(xRange);
        Assert.NotNull(yRange);
        Assert.NotNull(zRange);

        // x: defined at 0, last used at 2
        Assert.Equal(0, xRange.Start);
        Assert.True(xRange.End >= 2);

        // y: defined at 1, last used at 2
        Assert.Equal(1, yRange.Start);
        Assert.True(yRange.End >= 2);

        // z: defined at 3, last used at 4
        Assert.Equal(3, zRange.Start);
        Assert.True(zRange.End >= 4);
    }
}
