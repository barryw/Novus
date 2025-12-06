using Novus.IR;

namespace Novus.Frontend;

/// <summary>
/// Manages lexical scopes during IR building.
/// Tracks defer blocks, drop/cleanup requirements, and loop context for each scope.
///
/// This class centralizes scope management that was previously scattered across IrBuilder:
/// - Defer block registration and emission
/// - Drop tracking for RAII cleanup
/// - Move tracking for ownership semantics
/// - Loop exit/continue labels
/// - Labeled loop management
///
/// Usage:
/// <code>
/// var scopeManager = new ScopeManager();
///
/// // Enter a new scope (function, block, loop, etc.)
/// scopeManager.PushScope();
///
/// // Register defers and drops within the scope
/// scopeManager.RegisterDefer(deferBlock);
/// scopeManager.RegisterDrop(variableName);
///
/// // When exiting the scope
/// var defers = scopeManager.PopScope();
/// foreach (var defer in defers) { /* emit cleanup */ }
/// </code>
/// </summary>
public class ScopeManager
{
    // ==========================================================================
    // DEFER BLOCK TRACKING
    // ==========================================================================

    /// <summary>
    /// Stack of defer blocks per scope.
    /// Each element represents a scope; the list contains defers registered in that scope.
    /// Defers are executed in reverse order (LIFO) when the scope exits.
    /// </summary>
    private readonly Stack<List<IrBasicBlock>> _scopeDeferStack = new();

    /// <summary>
    /// Track emitted defer blocks to prevent double-free bugs.
    /// A defer block can be registered at both function and scope level,
    /// but should only be emitted once.
    /// </summary>
    private readonly HashSet<IrBasicBlock> _emittedDeferBlocks = new();

    // ==========================================================================
    // DROP/RAII TRACKING
    // ==========================================================================

    /// <summary>
    /// Stack of variable names that need drop calls per scope.
    /// When a scope exits, variables in the top list need their Drop::drop() called.
    /// </summary>
    private readonly Stack<List<string>> _scopeDropStack = new();

    /// <summary>
    /// Track which variables have been moved (ownership transferred).
    /// Moved variables should NOT have their drop() called.
    /// </summary>
    private readonly Dictionary<string, bool> _movedVariables = new();

    // ==========================================================================
    // LOOP CONTEXT TRACKING
    // ==========================================================================

    /// <summary>
    /// Stack of loop exit labels for break statements.
    /// Push when entering a loop, pop when exiting.
    /// </summary>
    private readonly Stack<string> _loopExitLabels = new();

    /// <summary>
    /// Stack of loop continue labels for continue statements.
    /// Push when entering a loop, pop when exiting.
    /// </summary>
    private readonly Stack<string> _loopContinueLabels = new();

    /// <summary>
    /// Maps labeled loop names to their (exitLabel, continueLabel) pairs.
    /// Enables: `break 'outer` or `continue 'inner`
    /// </summary>
    private readonly Dictionary<string, (string ExitLabel, string ContinueLabel)> _labeledLoops = new();

    // ==========================================================================
    // SCOPE MANAGEMENT
    // ==========================================================================

    /// <summary>
    /// Current scope depth (0 = no scopes pushed).
    /// </summary>
    public int Depth => _scopeDeferStack.Count;

    /// <summary>
    /// Whether we're currently inside a loop.
    /// </summary>
    public bool InLoop => _loopExitLabels.Count > 0;

    /// <summary>
    /// Push a new scope onto the stack.
    /// Call this when entering a new lexical scope (function, block, if/else, loop, etc.)
    /// </summary>
    public void PushScope()
    {
        _scopeDeferStack.Push(new List<IrBasicBlock>());
        _scopeDropStack.Push(new List<string>());
    }

    /// <summary>
    /// Pop the current scope and return the defer blocks registered in it.
    /// Returns defers in reverse order (LIFO) for proper cleanup.
    /// </summary>
    /// <returns>List of defer blocks that should be emitted (in reverse order)</returns>
    public List<IrBasicBlock> PopScope()
    {
        if (_scopeDeferStack.Count == 0)
            return new List<IrBasicBlock>();

        var defers = _scopeDeferStack.Pop();
        _scopeDropStack.Pop();

        // Return in reverse order for LIFO execution
        defers.Reverse();
        return defers;
    }

    /// <summary>
    /// Get variables needing drop for the current scope (without popping).
    /// </summary>
    public IReadOnlyList<string> GetCurrentScopeDrops()
    {
        if (_scopeDropStack.Count == 0)
            return Array.Empty<string>();
        return _scopeDropStack.Peek();
    }

    /// <summary>
    /// Get all defer blocks from all scopes (for early returns).
    /// Returns in order from innermost to outermost scope.
    /// </summary>
    public IEnumerable<IrBasicBlock> GetAllDeferBlocks()
    {
        foreach (var scopeDefers in _scopeDeferStack)
        {
            for (int i = scopeDefers.Count - 1; i >= 0; i--)
            {
                yield return scopeDefers[i];
            }
        }
    }

    // ==========================================================================
    // DEFER REGISTRATION
    // ==========================================================================

    /// <summary>
    /// Register a defer block in the current scope.
    /// </summary>
    public void RegisterDefer(IrBasicBlock deferBlock)
    {
        if (_scopeDeferStack.Count == 0)
            throw new InvalidOperationException("Cannot register defer outside of a scope");

        _scopeDeferStack.Peek().Add(deferBlock);
    }

    /// <summary>
    /// Check if a defer block has already been emitted.
    /// </summary>
    public bool IsDeferEmitted(IrBasicBlock deferBlock)
    {
        return _emittedDeferBlocks.Contains(deferBlock);
    }

    /// <summary>
    /// Mark a defer block as emitted.
    /// </summary>
    public void MarkDeferEmitted(IrBasicBlock deferBlock)
    {
        _emittedDeferBlocks.Add(deferBlock);
    }

    /// <summary>
    /// Reset emitted defer tracking (call at start of each function).
    /// </summary>
    public void ResetEmittedDefers()
    {
        _emittedDeferBlocks.Clear();
    }

    // ==========================================================================
    // DROP REGISTRATION
    // ==========================================================================

    /// <summary>
    /// Register a variable for drop cleanup when the current scope exits.
    /// </summary>
    public void RegisterDrop(string variableName)
    {
        if (_scopeDropStack.Count == 0)
            throw new InvalidOperationException("Cannot register drop outside of a scope");

        _scopeDropStack.Peek().Add(variableName);
    }

    /// <summary>
    /// Mark a variable as moved (ownership transferred).
    /// Moved variables should not have drop() called.
    /// </summary>
    public void MarkMoved(string variableName)
    {
        _movedVariables[variableName] = true;
    }

    /// <summary>
    /// Check if a variable has been moved.
    /// </summary>
    public bool IsMoved(string variableName)
    {
        return _movedVariables.TryGetValue(variableName, out var moved) && moved;
    }

    /// <summary>
    /// Reset move tracking (call at start of each function).
    /// </summary>
    public void ResetMoves()
    {
        _movedVariables.Clear();
    }

    // ==========================================================================
    // LOOP CONTEXT
    // ==========================================================================

    /// <summary>
    /// Enter a loop, pushing exit and continue labels.
    /// </summary>
    /// <param name="exitLabel">Label for break statements</param>
    /// <param name="continueLabel">Label for continue statements</param>
    /// <param name="loopLabel">Optional named label for the loop (e.g., 'outer)</param>
    public void EnterLoop(string exitLabel, string continueLabel, string? loopLabel = null)
    {
        _loopExitLabels.Push(exitLabel);
        _loopContinueLabels.Push(continueLabel);

        if (loopLabel != null)
        {
            _labeledLoops[loopLabel] = (exitLabel, continueLabel);
        }
    }

    /// <summary>
    /// Exit the current loop.
    /// </summary>
    /// <param name="loopLabel">Optional named label to remove</param>
    public void ExitLoop(string? loopLabel = null)
    {
        if (_loopExitLabels.Count > 0)
            _loopExitLabels.Pop();
        if (_loopContinueLabels.Count > 0)
            _loopContinueLabels.Pop();

        if (loopLabel != null)
        {
            _labeledLoops.Remove(loopLabel);
        }
    }

    /// <summary>
    /// Get the exit label for the current (innermost) loop.
    /// </summary>
    public string? GetCurrentExitLabel()
    {
        return _loopExitLabels.Count > 0 ? _loopExitLabels.Peek() : null;
    }

    /// <summary>
    /// Get the continue label for the current (innermost) loop.
    /// </summary>
    public string? GetCurrentContinueLabel()
    {
        return _loopContinueLabels.Count > 0 ? _loopContinueLabels.Peek() : null;
    }

    /// <summary>
    /// Get the exit label for a named loop.
    /// </summary>
    public string? GetLabeledExitLabel(string loopLabel)
    {
        return _labeledLoops.TryGetValue(loopLabel, out var labels) ? labels.ExitLabel : null;
    }

    /// <summary>
    /// Get the continue label for a named loop.
    /// </summary>
    public string? GetLabeledContinueLabel(string loopLabel)
    {
        return _labeledLoops.TryGetValue(loopLabel, out var labels) ? labels.ContinueLabel : null;
    }

    // ==========================================================================
    // FUNCTION BOUNDARY
    // ==========================================================================

    /// <summary>
    /// Reset all scope state when entering a new function.
    /// Call this at the start of each function body.
    /// </summary>
    public void ResetForNewFunction()
    {
        _scopeDeferStack.Clear();
        _scopeDropStack.Clear();
        _emittedDeferBlocks.Clear();
        _movedVariables.Clear();
        _loopExitLabels.Clear();
        _loopContinueLabels.Clear();
        _labeledLoops.Clear();
    }
}
