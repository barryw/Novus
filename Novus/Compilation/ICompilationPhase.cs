namespace Novus.Compilation;

/// <summary>
/// Base interface for compilation phases.
/// Each phase transforms or extends the compilation context.
/// </summary>
public interface ICompilationPhase
{
    /// <summary>
    /// Name of the phase for logging purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Execute this phase of compilation.
    /// </summary>
    /// <param name="context">The shared compilation context.</param>
    /// <returns>True if phase succeeded, false if compilation should stop.</returns>
    Task<bool> ExecuteAsync(CompilationContext context);
}
