using Novus.IR;

namespace Novus.Transforms;

/// <summary>
/// Interface for IR transformation passes
/// Transform passes run BEFORE optimization and can modify the structure of the IR
/// (e.g., add/remove functions, change call sites, expand generics)
/// </summary>
public interface IIrTransformPass
{
    /// <summary>
    /// Name of the transformation pass for logging/debugging
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Run the transformation pass on the IR module
    /// </summary>
    /// <param name="module">The IR module to transform</param>
    /// <returns>True if the IR was modified, false otherwise</returns>
    bool Transform(IrModule module);
}

/// <summary>
/// Base class for transformation passes that provides common utilities
/// </summary>
public abstract class TransformPassBase : IIrTransformPass
{
    public abstract string Name { get; }

    public abstract bool Transform(IrModule module);

    /// <summary>
    /// Helper to create a fresh temporary variable name
    /// </summary>
    protected string CreateTempName(int counter)
    {
        return $"%t{counter}";
    }

    /// <summary>
    /// Helper to check if a function should be transformed
    /// </summary>
    protected bool ShouldTransform(IrFunction function)
    {
        // Skip already-transformed functions
        if (function.Name.Contains("$inline") || function.Name.Contains("$mono"))
        {
            return false;
        }
        return true;
    }
}
