using Novus.Infrastructure;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Scoped context for function analysis. Automatically restores state on dispose.
/// </summary>
public class FunctionAnalysisScope : IDisposable
{
    private readonly SemanticAnalyzer _analyzer;
    private readonly FunctionSymbol? _savedFunction;
    private readonly HashSet<string> _savedSuppressedWarnings;
    private bool _isDisposed;

    public FunctionAnalysisScope(SemanticAnalyzer analyzer, FunctionSymbol function)
    {
        _analyzer = analyzer;
        _savedFunction = analyzer.CurrentFunction;
        _savedSuppressedWarnings = new HashSet<string>(analyzer.CurrentFunctionSuppressedWarnings);

        analyzer.SetCurrentFunction(function);
        analyzer.ClearFunctionSuppressedWarnings();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _analyzer.SetCurrentFunction(_savedFunction);
            _analyzer.SetFunctionSuppressedWarnings(_savedSuppressedWarnings);
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped context for loop analysis. Tracks loop depth automatically.
/// </summary>
public class LoopAnalysisScope : IDisposable
{
    private readonly SemanticAnalyzer _analyzer;
    private bool _isDisposed;

    public LoopAnalysisScope(SemanticAnalyzer analyzer)
    {
        _analyzer = analyzer;
        analyzer.IncrementLoopDepth();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _analyzer.DecrementLoopDepth();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped context for unsafe block analysis.
/// </summary>
public class UnsafeAnalysisScope : IDisposable
{
    private readonly SemanticAnalyzer _analyzer;
    private bool _isDisposed;

    public UnsafeAnalysisScope(SemanticAnalyzer analyzer)
    {
        _analyzer = analyzer;
        analyzer.IncrementUnsafeDepth();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _analyzer.DecrementUnsafeDepth();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped context for expected type during type inference.
/// </summary>
public class ExpectedTypeScope : IDisposable
{
    private readonly SemanticAnalyzer _analyzer;
    private readonly IR.IrType? _savedExpectedType;
    private bool _isDisposed;

    public ExpectedTypeScope(SemanticAnalyzer analyzer, IR.IrType? expectedType)
    {
        _analyzer = analyzer;
        _savedExpectedType = analyzer.ExpectedType;
        analyzer.SetExpectedType(expectedType);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _analyzer.SetExpectedType(_savedExpectedType);
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped context for struct where clause during field analysis.
/// </summary>
public class StructWhereClauseScope : IDisposable
{
    private readonly SemanticAnalyzer _analyzer;
    private readonly IR.IrWhereClause? _savedWhereClause;
    private bool _isDisposed;

    public StructWhereClauseScope(SemanticAnalyzer analyzer, IR.IrWhereClause? whereClause)
    {
        _analyzer = analyzer;
        _savedWhereClause = analyzer.CurrentStructWhereClause;
        analyzer.SetCurrentStructWhereClause(whereClause);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _analyzer.SetCurrentStructWhereClause(_savedWhereClause);
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped context for drop scope management.
/// </summary>
public class DropAnalysisScope : IDisposable
{
    private readonly SemanticAnalyzer _analyzer;
    private bool _isDisposed;

    public DropAnalysisScope(SemanticAnalyzer analyzer)
    {
        _analyzer = analyzer;
        analyzer.PushDropScope();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _analyzer.PopDropScope();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
