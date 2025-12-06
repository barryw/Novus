namespace Novus.IR;

/// <summary>
/// Interface for compiler analysis passes that use scratch space.
/// Provides lifecycle methods for proper state management.
/// </summary>
public interface IAnalysisPass
{
    /// <summary>
    /// Name of the pass for logging/debugging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initialize scratch space before analysis.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Execute the analysis pass.
    /// </summary>
    /// <returns>Number of changes made (0 if no changes).</returns>
    int Execute(IrFunction function);

    /// <summary>
    /// Clean up scratch space after analysis.
    /// </summary>
    void Cleanup();
}

/// <summary>
/// Base class for analysis passes with automatic state management.
/// </summary>
public abstract class AnalysisPassBase : IAnalysisPass
{
    public abstract string Name { get; }

    /// <summary>
    /// Initialize scratch space. Override to add custom initialization.
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// Clean up scratch space. Override to add custom cleanup.
    /// </summary>
    public virtual void Cleanup() { }

    /// <summary>
    /// Execute the pass with automatic Initialize/Cleanup.
    /// </summary>
    public int Run(IrFunction function)
    {
        Initialize();
        try
        {
            return Execute(function);
        }
        finally
        {
            Cleanup();
        }
    }

    public abstract int Execute(IrFunction function);
}

/// <summary>
/// Scoped wrapper for running an analysis pass with automatic cleanup.
/// </summary>
public class AnalysisPassScope : IDisposable
{
    private readonly IAnalysisPass _pass;
    private bool _isDisposed;

    public AnalysisPassScope(IAnalysisPass pass)
    {
        _pass = pass;
        pass.Initialize();
    }

    public int Execute(IrFunction function)
    {
        return _pass.Execute(function);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _pass.Cleanup();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
