using Novus.Infrastructure;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for ObjectPool infrastructure.
/// </summary>
public class ObjectPoolTests
{
    private class TestObject
    {
        public int Value { get; set; }
        public string? Name { get; set; }
    }

    #region Basic Rent/Return

    [Fact]
    public void Rent_CreatesNewObject_WhenPoolEmpty()
    {
        var pool = new ObjectPool<TestObject>();

        var obj = pool.Rent();

        Assert.NotNull(obj);
        Assert.Equal(1, pool.AllocatedCount);
        Assert.Equal(0, pool.ReuseCount);
    }

    [Fact]
    public void Rent_ReusesObject_WhenPoolHasItems()
    {
        var pool = new ObjectPool<TestObject>();
        var obj1 = pool.Rent();
        pool.Return(obj1);

        var obj2 = pool.Rent();

        Assert.Same(obj1, obj2);
        Assert.Equal(1, pool.AllocatedCount);
        Assert.Equal(1, pool.ReuseCount);
    }

    [Fact]
    public void Return_AddsObjectToPool()
    {
        var pool = new ObjectPool<TestObject>();
        var obj = pool.Rent();
        Assert.Equal(0, pool.AvailableCount);

        pool.Return(obj);

        Assert.Equal(1, pool.AvailableCount);
    }

    [Fact]
    public void Return_NullObject_DoesNothing()
    {
        var pool = new ObjectPool<TestObject>();

        pool.Return(null!);

        Assert.Equal(0, pool.AvailableCount);
    }

    #endregion

    #region Reset Action

    [Fact]
    public void Return_CallsResetAction()
    {
        var resetCalled = false;
        var pool = new ObjectPool<TestObject>(resetAction: obj =>
        {
            resetCalled = true;
            obj.Value = 0;
            obj.Name = null;
        });

        var obj = pool.Rent();
        obj.Value = 42;
        obj.Name = "test";
        pool.Return(obj);

        Assert.True(resetCalled);
        Assert.Equal(0, obj.Value);
        Assert.Null(obj.Name);
    }

    [Fact]
    public void Rent_ReturnsResetObject()
    {
        var pool = new ObjectPool<TestObject>(resetAction: obj =>
        {
            obj.Value = 0;
            obj.Name = null;
        });

        var obj1 = pool.Rent();
        obj1.Value = 99;
        obj1.Name = "modified";
        pool.Return(obj1);

        var obj2 = pool.Rent();

        Assert.Same(obj1, obj2);
        Assert.Equal(0, obj2.Value);
        Assert.Null(obj2.Name);
    }

    #endregion

    #region Max Pool Size

    [Fact]
    public void Return_RespectsMaxPoolSize()
    {
        var pool = new ObjectPool<TestObject>(maxPoolSize: 2);
        var obj1 = pool.Rent();
        var obj2 = pool.Rent();
        var obj3 = pool.Rent();

        pool.Return(obj1);
        pool.Return(obj2);
        pool.Return(obj3); // Should exceed max size

        Assert.Equal(2, pool.AvailableCount);
    }

    [Fact]
    public void Rent_CreatesNewObjects_AfterMaxPoolSizeExceeded()
    {
        var pool = new ObjectPool<TestObject>(maxPoolSize: 1);
        var obj1 = pool.Rent();
        var obj2 = pool.Rent();
        pool.Return(obj1);
        pool.Return(obj2); // obj2 will be discarded

        var obj3 = pool.Rent();
        var obj4 = pool.Rent(); // This should be a new allocation

        Assert.Same(obj1, obj3);
        Assert.NotSame(obj2, obj4);
        Assert.Equal(3, pool.AllocatedCount);
    }

    #endregion

    #region Statistics

    [Fact]
    public void GetStats_ReturnsCorrectValues()
    {
        var pool = new ObjectPool<TestObject>();

        // Rent 3 objects (all new allocations)
        var obj1 = pool.Rent();
        var obj2 = pool.Rent();
        var obj3 = pool.Rent();

        // Return all 3
        pool.Return(obj1);
        pool.Return(obj2);
        pool.Return(obj3);

        // Rent 2 back (reuses)
        _ = pool.Rent();
        _ = pool.Rent();

        var (allocated, reused, available, reuseRate) = pool.GetStats();

        Assert.Equal(3, allocated);
        Assert.Equal(2, reused);
        Assert.Equal(1, available);
        Assert.True(reuseRate > 35); // 2/(3+2) = 40%
    }

    [Fact]
    public void ResetStats_ClearsCounters()
    {
        var pool = new ObjectPool<TestObject>();
        _ = pool.Rent();
        pool.Return(pool.Rent());
        _ = pool.Rent();

        pool.ResetStats();

        Assert.Equal(0, pool.AllocatedCount);
        Assert.Equal(0, pool.ReuseCount);
    }

    #endregion

    #region Clear

    [Fact]
    public void Clear_RemovesAllPooledObjects()
    {
        var pool = new ObjectPool<TestObject>();

        // Rent 3 distinct objects
        var obj1 = pool.Rent();
        var obj2 = pool.Rent();
        var obj3 = pool.Rent();

        // Return all 3 to pool
        pool.Return(obj1);
        pool.Return(obj2);
        pool.Return(obj3);
        Assert.Equal(3, pool.AvailableCount);

        pool.Clear();

        Assert.Equal(0, pool.AvailableCount);
    }

    [Fact]
    public void Rent_CreatesNewObject_AfterClear()
    {
        var pool = new ObjectPool<TestObject>();
        var obj1 = pool.Rent();
        pool.Return(obj1);
        pool.Clear();

        var obj2 = pool.Rent();

        Assert.NotSame(obj1, obj2);
    }

    #endregion

    #region Thread Safety

    [Fact]
    public async Task Rent_IsThreadSafe()
    {
        var pool = new ObjectPool<TestObject>(maxPoolSize: 100);
        var objects = new System.Collections.Concurrent.ConcurrentBag<TestObject>();
        var tasks = new List<Task>();

        // Rent from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    objects.Add(pool.Rent());
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(1000, objects.Count);
        Assert.Equal(1000, pool.AllocatedCount);
    }

    [Fact]
    public async Task Return_IsThreadSafe()
    {
        var pool = new ObjectPool<TestObject>(maxPoolSize: 500);
        var objects = new List<TestObject>();

        // Pre-allocate objects
        for (int i = 0; i < 500; i++)
        {
            objects.Add(pool.Rent());
        }

        var tasks = new List<Task>();

        // Return from multiple threads
        for (int i = 0; i < 10; i++)
        {
            var start = i * 50;
            tasks.Add(Task.Run(() =>
            {
                for (int j = start; j < start + 50; j++)
                {
                    pool.Return(objects[j]);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(500, pool.AvailableCount);
    }

    [Fact]
    public async Task RentAndReturn_ConcurrentlyIsThreadSafe()
    {
        var pool = new ObjectPool<TestObject>(maxPoolSize: 100);
        var tasks = new List<Task>();
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var obj = pool.Rent();
                        obj.Value = j;
                        pool.Return(obj);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
    }

    #endregion

    #region PooledObject Scoped

    [Fact]
    public void RentScoped_ReturnsToPoolOnDispose()
    {
        var pool = new ObjectPool<TestObject>();

        using (var scoped = pool.RentScoped())
        {
            scoped.Value.Value = 42;
        }

        Assert.Equal(1, pool.AvailableCount);
    }

    [Fact]
    public void RentScoped_ObjectIsReusedAfterDispose()
    {
        var pool = new ObjectPool<TestObject>(resetAction: obj => obj.Value = 0);
        TestObject? capturedObj;

        using (var scoped = pool.RentScoped())
        {
            scoped.Value.Value = 99;
            capturedObj = scoped.Value;
        }

        using (var scoped2 = pool.RentScoped())
        {
            Assert.Same(capturedObj, scoped2.Value);
            Assert.Equal(0, scoped2.Value.Value);
        }
    }

    #endregion
}

/// <summary>
/// Tests for ObjectPoolWithFactory.
/// </summary>
public class ObjectPoolWithFactoryTests
{
    private class ComplexObject
    {
        public int Id { get; }
        public string Name { get; set; }

        public ComplexObject(int id)
        {
            Id = id;
            Name = "";
        }
    }

    [Fact]
    public void Rent_UsesFactoryToCreateObjects()
    {
        var nextId = 0;
        var pool = new ObjectPoolWithFactory<ComplexObject>(
            factory: () => new ComplexObject(++nextId)
        );

        var obj1 = pool.Rent();
        var obj2 = pool.Rent();

        Assert.Equal(1, obj1.Id);
        Assert.Equal(2, obj2.Id);
    }

    [Fact]
    public void Rent_ReusesExistingObjects()
    {
        var createCount = 0;
        var pool = new ObjectPoolWithFactory<ComplexObject>(
            factory: () => new ComplexObject(++createCount)
        );

        var obj1 = pool.Rent();
        pool.Return(obj1);
        var obj2 = pool.Rent();

        Assert.Same(obj1, obj2);
        Assert.Equal(1, createCount); // Factory called only once
    }

    [Fact]
    public void Return_CallsResetAction()
    {
        var pool = new ObjectPoolWithFactory<ComplexObject>(
            factory: () => new ComplexObject(1),
            resetAction: obj => obj.Name = "reset"
        );

        var obj = pool.Rent();
        obj.Name = "modified";
        pool.Return(obj);

        var obj2 = pool.Rent();
        Assert.Equal("reset", obj2.Name);
    }

    [Fact]
    public void Constructor_ThrowsIfFactoryNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ObjectPoolWithFactory<ComplexObject>(factory: null!)
        );
    }
}
