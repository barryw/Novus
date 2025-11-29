using Novus.IR;
using Novus.Transforms.Passes;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Function inlining optimization pass for O3 level
/// Wraps the InlineExpansionPass transform to run during optimization
/// </summary>
public class FunctionInliningPass : IOptimizationPass
{
    private readonly InlineExpansionPass _inliner = new();

    public string Name => "Function Inlining";

    public bool Run(IrModule module)
    {
        return _inliner.Transform(module);
    }
}
