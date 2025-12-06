using System.Collections.Concurrent;

namespace Novus.Infrastructure;

/// <summary>
/// Thread-safe object pool for reusing frequently-allocated objects.
/// Reduces GC pressure by recycling objects instead of creating new ones.
/// </summary>
/// <typeparam name="T">The type of object to pool. Must have parameterless constructor.</typeparam>
public class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _pool;
    private readonly int _maxPoolSize;
    private readonly Action<T>? _resetAction;
    private int _allocatedCount;
    private int _reuseCount;

    /// <summary>
    /// Number of objects allocated (created new).
    /// </summary>
    public int AllocatedCount => _allocatedCount;

    /// <summary>
    /// Number of objects reused from pool.
    /// </summary>
    public int ReuseCount => _reuseCount;

    /// <summary>
    /// Current number of objects available in pool.
    /// </summary>
    public int AvailableCount => _pool.Count;

    /// <summary>
    /// Create a new object pool.
    /// </summary>
    /// <param name="maxPoolSize">Maximum number of objects to keep in pool.</param>
    /// <param name="resetAction">Optional action to reset object state before reuse.</param>
    public ObjectPool(int maxPoolSize = 256, Action<T>? resetAction = null)
    {
        _pool = new ConcurrentBag<T>();
        _maxPoolSize = maxPoolSize;
        _resetAction = resetAction;
    }

    /// <summary>
    /// Get an object from the pool or create a new one if pool is empty.
    /// </summary>
    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            Interlocked.Increment(ref _reuseCount);
            return item;
        }

        Interlocked.Increment(ref _allocatedCount);
        return new T();
    }

    /// <summary>
    /// Return an object to the pool for reuse.
    /// </summary>
    /// <param name="item">The object to return.</param>
    public void Return(T item)
    {
        if (item == null) return;

        // Reset the object before returning to pool
        _resetAction?.Invoke(item);

        // Only add to pool if we haven't exceeded max size
        if (_pool.Count < _maxPoolSize)
        {
            _pool.Add(item);
        }
        // Otherwise let GC handle it
    }

    /// <summary>
    /// Clear all objects from the pool.
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out _)) { }
    }

    /// <summary>
    /// Get pool statistics.
    /// </summary>
    public (int allocated, int reused, int available, double reuseRate) GetStats()
    {
        var total = _allocatedCount + _reuseCount;
        var reuseRate = total > 0 ? (double)_reuseCount / total * 100 : 0;
        return (_allocatedCount, _reuseCount, _pool.Count, reuseRate);
    }

    /// <summary>
    /// Reset statistics counters.
    /// </summary>
    public void ResetStats()
    {
        Interlocked.Exchange(ref _allocatedCount, 0);
        Interlocked.Exchange(ref _reuseCount, 0);
    }
}

/// <summary>
/// Object pool with factory method for objects without parameterless constructors.
/// </summary>
/// <typeparam name="T">The type of object to pool.</typeparam>
public class ObjectPoolWithFactory<T> where T : class
{
    private readonly ConcurrentBag<T> _pool;
    private readonly int _maxPoolSize;
    private readonly Func<T> _factory;
    private readonly Action<T>? _resetAction;
    private int _allocatedCount;
    private int _reuseCount;

    public int AllocatedCount => _allocatedCount;
    public int ReuseCount => _reuseCount;
    public int AvailableCount => _pool.Count;

    public ObjectPoolWithFactory(Func<T> factory, int maxPoolSize = 256, Action<T>? resetAction = null)
    {
        _pool = new ConcurrentBag<T>();
        _maxPoolSize = maxPoolSize;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _resetAction = resetAction;
    }

    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            Interlocked.Increment(ref _reuseCount);
            return item;
        }

        Interlocked.Increment(ref _allocatedCount);
        return _factory();
    }

    public void Return(T item)
    {
        if (item == null) return;
        _resetAction?.Invoke(item);

        if (_pool.Count < _maxPoolSize)
        {
            _pool.Add(item);
        }
    }

    public void Clear()
    {
        while (_pool.TryTake(out _)) { }
    }
}

/// <summary>
/// Pooled wrapper that automatically returns the object when disposed.
/// Use with 'using' statement for automatic return.
/// </summary>
/// <typeparam name="T">The type of pooled object.</typeparam>
public readonly struct PooledObject<T> : IDisposable where T : class, new()
{
    private readonly ObjectPool<T> _pool;
    public readonly T Value;

    internal PooledObject(ObjectPool<T> pool, T value)
    {
        _pool = pool;
        Value = value;
    }

    public void Dispose()
    {
        _pool.Return(Value);
    }
}

/// <summary>
/// Extension methods for ObjectPool.
/// </summary>
public static class ObjectPoolExtensions
{
    /// <summary>
    /// Rent an object wrapped in a disposable container for automatic return.
    /// </summary>
    public static PooledObject<T> RentScoped<T>(this ObjectPool<T> pool) where T : class, new()
    {
        return new PooledObject<T>(pool, pool.Rent());
    }
}
