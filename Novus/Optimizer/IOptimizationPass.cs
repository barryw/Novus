using Novus.IR;

namespace Novus.Optimizer;

/// <summary>
/// Base interface for all optimization passes
/// </summary>
public interface IOptimizationPass
{
    /// <summary>
    /// Name of the optimization pass for logging and debugging
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Apply the optimization pass to a module
    /// </summary>
    /// <param name="module">The IR module to optimize</param>
    /// <returns>True if any changes were made, false otherwise</returns>
    bool Run(IrModule module);
}

/// <summary>
/// Base interface for function-level optimization passes
/// </summary>
public interface IFunctionPass : IOptimizationPass
{
    /// <summary>
    /// Apply the optimization pass to a single function
    /// </summary>
    /// <param name="function">The function to optimize</param>
    /// <returns>True if any changes were made, false otherwise</returns>
    bool RunOnFunction(IrFunction function);
}

/// <summary>
/// Base interface for basic-block level optimization passes
/// </summary>
public interface IBasicBlockPass : IOptimizationPass
{
    /// <summary>
    /// Apply the optimization pass to a single basic block
    /// </summary>
    /// <param name="block">The basic block to optimize</param>
    /// <returns>True if any changes were made, false otherwise</returns>
    bool RunOnBasicBlock(IrBasicBlock block);
}
