namespace Novus.Infrastructure;

/// <summary>
/// Base class for scoped state objects that automatically restore state on dispose.
/// Use with 'using' statement for RAII-style state management.
/// </summary>
/// <typeparam name="T">The type of state being managed.</typeparam>
public abstract class ScopedState<T> : IDisposable
{
    protected T SavedState { get; }
    protected bool IsDisposed { get; private set; }

    protected ScopedState(T savedState)
    {
        SavedState = savedState;
    }

    /// <summary>
    /// Restore the saved state. Called automatically on dispose.
    /// </summary>
    protected abstract void RestoreState();

    public void Dispose()
    {
        if (!IsDisposed)
        {
            RestoreState();
            IsDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped state for a single field value.
/// </summary>
/// <typeparam name="T">The type of the field.</typeparam>
public class FieldScope<T> : IDisposable
{
    private readonly Action<T> _setter;
    private readonly T _savedValue;
    private bool _isDisposed;

    public FieldScope(Func<T> getter, Action<T> setter, T newValue)
    {
        _setter = setter;
        _savedValue = getter();
        setter(newValue);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _setter(_savedValue);
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped state for a nullable field that should be cleared on exit.
/// </summary>
/// <typeparam name="T">The type of the field.</typeparam>
public class NullableFieldScope<T> : IDisposable where T : class
{
    private readonly Action<T?> _setter;
    private readonly T? _savedValue;
    private bool _isDisposed;

    public NullableFieldScope(Func<T?> getter, Action<T?> setter, T? newValue)
    {
        _setter = setter;
        _savedValue = getter();
        setter(newValue);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _setter(_savedValue);
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped state for an integer counter (depth tracking).
/// </summary>
public class DepthScope : IDisposable
{
    private readonly Action _decrement;
    private bool _isDisposed;

    public DepthScope(Action increment, Action decrement)
    {
        _decrement = decrement;
        increment();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _decrement();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped state for stack-based context (push on enter, pop on exit).
/// </summary>
/// <typeparam name="T">The type of items in the stack.</typeparam>
public class StackScope<T> : IDisposable
{
    private readonly Stack<T> _stack;
    private bool _isDisposed;

    public T Value { get; }

    public StackScope(Stack<T> stack, T value)
    {
        _stack = stack;
        Value = value;
        stack.Push(value);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_stack.Count > 0)
            {
                _stack.Pop();
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped state for dictionary entries (add on enter, remove on exit).
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
public class DictionaryEntryScope<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dictionary;
    private readonly TKey _key;
    private readonly TValue? _previousValue;
    private readonly bool _hadPreviousValue;
    private bool _isDisposed;

    public DictionaryEntryScope(Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        _dictionary = dictionary;
        _key = key;
        _hadPreviousValue = dictionary.TryGetValue(key, out _previousValue);
        dictionary[key] = value;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_hadPreviousValue)
            {
                _dictionary[_key] = _previousValue!;
            }
            else
            {
                _dictionary.Remove(_key);
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Scoped state that clears a collection on exit.
/// </summary>
/// <typeparam name="T">The collection type.</typeparam>
public class CollectionClearScope<T> : IDisposable where T : System.Collections.ICollection
{
    private readonly Action _clearAction;
    private bool _isDisposed;

    public CollectionClearScope(Action clearAction)
    {
        _clearAction = clearAction;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _clearAction();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Combines multiple scoped states into a single disposable.
/// </summary>
public class CompositeScope : IDisposable
{
    private readonly List<IDisposable> _scopes;
    private bool _isDisposed;

    public CompositeScope(params IDisposable[] scopes)
    {
        _scopes = new List<IDisposable>(scopes);
    }

    public CompositeScope Add(IDisposable scope)
    {
        _scopes.Add(scope);
        return this;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // Dispose in reverse order (LIFO)
            for (int i = _scopes.Count - 1; i >= 0; i--)
            {
                _scopes[i].Dispose();
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
