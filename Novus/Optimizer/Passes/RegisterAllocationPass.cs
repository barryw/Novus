using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Register allocation pass.
/// Analyzes variable lifetimes and assigns physical registers to variables.
/// This is an analysis pass - it doesn't modify the IR, but annotates it with
/// register assignments that the code generator can use.
/// </summary>
public class RegisterAllocationPass : FunctionPassBase
{
    public override string Name => "Register Allocation";

    // Store allocation results per function
    private readonly Dictionary<string, RegisterAllocation> _allocations = new();

    public override bool RunOnFunction(IrFunction function)
    {
        if (function.BasicBlocks.Count == 0) return false;

        var allocator = new RegisterAllocator();
        var allocation = allocator.AllocateRegisters(function);

        // Store the allocation for this function
        _allocations[function.Name] = allocation;

        // For now, we don't modify the IR
        // The code generator will query the allocation results
        // TODO: Add metadata system to IR to store allocation info

        return false; // No IR modifications (yet)
    }

    /// <summary>
    /// Get the register allocation for a function
    /// </summary>
    public RegisterAllocation? GetAllocation(string functionName)
    {
        return _allocations.GetValueOrDefault(functionName);
    }

    /// <summary>
    /// Get all allocations
    /// </summary>
    public IReadOnlyDictionary<string, RegisterAllocation> GetAllAllocations()
    {
        return _allocations;
    }
}
