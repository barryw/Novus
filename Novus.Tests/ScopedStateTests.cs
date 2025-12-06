using Novus.Infrastructure;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for ScopedState infrastructure classes.
/// </summary>
public class ScopedStateTests
{
    #region FieldScope Tests

    [Fact]
    public void FieldScope_RestoresValueOnDispose()
    {
        var value = 42;
        var originalValue = value;

        using (new FieldScope<int>(() => value, v => value = v, 100))
        {
            Assert.Equal(100, value);
        }

        Assert.Equal(originalValue, value);
    }

    [Fact]
    public void FieldScope_SetsNewValueInScope()
    {
        var value = "original";

        using (var scope = new FieldScope<string>(() => value, v => value = v, "modified"))
        {
            Assert.Equal("modified", value);
        }
    }

    [Fact]
    public void FieldScope_NestedScopes_RestoreCorrectly()
    {
        var value = 1;

        using (new FieldScope<int>(() => value, v => value = v, 2))
        {
            Assert.Equal(2, value);

            using (new FieldScope<int>(() => value, v => value = v, 3))
            {
                Assert.Equal(3, value);
            }

            Assert.Equal(2, value);
        }

        Assert.Equal(1, value);
    }

    #endregion

    #region NullableFieldScope Tests

    [Fact]
    public void NullableFieldScope_RestoresNullValue()
    {
        string? value = null;

        using (new NullableFieldScope<string>(() => value, v => value = v, "set"))
        {
            Assert.Equal("set", value);
        }

        Assert.Null(value);
    }

    [Fact]
    public void NullableFieldScope_RestoresNonNullValue()
    {
        string? value = "original";

        using (new NullableFieldScope<string>(() => value, v => value = v, "modified"))
        {
            Assert.Equal("modified", value);
        }

        Assert.Equal("original", value);
    }

    [Fact]
    public void NullableFieldScope_SetsToNull()
    {
        string? value = "original";

        using (new NullableFieldScope<string>(() => value, v => value = v, null))
        {
            Assert.Null(value);
        }

        Assert.Equal("original", value);
    }

    #endregion

    #region DepthScope Tests

    [Fact]
    public void DepthScope_IncrementsOnCreate()
    {
        var depth = 0;

        using (new DepthScope(() => depth++, () => depth--))
        {
            Assert.Equal(1, depth);
        }
    }

    [Fact]
    public void DepthScope_DecrementsOnDispose()
    {
        var depth = 0;

        using (new DepthScope(() => depth++, () => depth--))
        {
            Assert.Equal(1, depth);
        }

        Assert.Equal(0, depth);
    }

    [Fact]
    public void DepthScope_NestedScopes_TrackDepthCorrectly()
    {
        var depth = 0;

        using (new DepthScope(() => depth++, () => depth--))
        {
            Assert.Equal(1, depth);

            using (new DepthScope(() => depth++, () => depth--))
            {
                Assert.Equal(2, depth);

                using (new DepthScope(() => depth++, () => depth--))
                {
                    Assert.Equal(3, depth);
                }

                Assert.Equal(2, depth);
            }

            Assert.Equal(1, depth);
        }

        Assert.Equal(0, depth);
    }

    #endregion

    #region StackScope Tests

    [Fact]
    public void StackScope_PushesValueOnCreate()
    {
        var stack = new Stack<string>();

        using (var scope = new StackScope<string>(stack, "value"))
        {
            Assert.Single(stack);
            Assert.Equal("value", stack.Peek());
            Assert.Equal("value", scope.Value);
        }
    }

    [Fact]
    public void StackScope_PopsOnDispose()
    {
        var stack = new Stack<string>();
        stack.Push("base");

        using (new StackScope<string>(stack, "temporary"))
        {
            Assert.Equal(2, stack.Count);
        }

        Assert.Single(stack);
        Assert.Equal("base", stack.Peek());
    }

    [Fact]
    public void StackScope_NestedScopes_MaintainOrder()
    {
        var stack = new Stack<int>();

        using (new StackScope<int>(stack, 1))
        {
            using (new StackScope<int>(stack, 2))
            {
                using (new StackScope<int>(stack, 3))
                {
                    Assert.Equal(3, stack.Count);
                    Assert.Equal(3, stack.Peek());
                }
                Assert.Equal(2, stack.Peek());
            }
            Assert.Equal(1, stack.Peek());
        }

        Assert.Empty(stack);
    }

    #endregion

    #region DictionaryEntryScope Tests

    [Fact]
    public void DictionaryEntryScope_AddsEntry()
    {
        var dict = new Dictionary<string, int>();

        using (new DictionaryEntryScope<string, int>(dict, "key", 42))
        {
            Assert.True(dict.ContainsKey("key"));
            Assert.Equal(42, dict["key"]);
        }
    }

    [Fact]
    public void DictionaryEntryScope_RemovesNewEntryOnDispose()
    {
        var dict = new Dictionary<string, int>();

        using (new DictionaryEntryScope<string, int>(dict, "key", 42))
        {
            Assert.Contains("key", dict.Keys);
        }

        Assert.DoesNotContain("key", dict.Keys);
    }

    [Fact]
    public void DictionaryEntryScope_RestoresPreviousValueOnDispose()
    {
        var dict = new Dictionary<string, int> { ["key"] = 100 };

        using (new DictionaryEntryScope<string, int>(dict, "key", 42))
        {
            Assert.Equal(42, dict["key"]);
        }

        Assert.Equal(100, dict["key"]);
    }

    [Fact]
    public void DictionaryEntryScope_PreservesOtherEntries()
    {
        var dict = new Dictionary<string, int>
        {
            ["other1"] = 1,
            ["other2"] = 2
        };

        using (new DictionaryEntryScope<string, int>(dict, "temp", 99))
        {
            Assert.Equal(3, dict.Count);
        }

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["other1"]);
        Assert.Equal(2, dict["other2"]);
    }

    #endregion

    #region CompositeScope Tests

    [Fact]
    public void CompositeScope_DisposesAllScopes()
    {
        var disposed1 = false;
        var disposed2 = false;
        var disposed3 = false;

        var scope1 = new TestDisposable(() => disposed1 = true);
        var scope2 = new TestDisposable(() => disposed2 = true);
        var scope3 = new TestDisposable(() => disposed3 = true);

        using (new CompositeScope(scope1, scope2, scope3))
        {
            Assert.False(disposed1);
            Assert.False(disposed2);
            Assert.False(disposed3);
        }

        Assert.True(disposed1);
        Assert.True(disposed2);
        Assert.True(disposed3);
    }

    [Fact]
    public void CompositeScope_DisposesInReverseOrder()
    {
        var order = new List<int>();

        var scope1 = new TestDisposable(() => order.Add(1));
        var scope2 = new TestDisposable(() => order.Add(2));
        var scope3 = new TestDisposable(() => order.Add(3));

        using (new CompositeScope(scope1, scope2, scope3)) { }

        Assert.Equal(new[] { 3, 2, 1 }, order);
    }

    [Fact]
    public void CompositeScope_AddMethod_Works()
    {
        var disposed = false;

        using (var composite = new CompositeScope())
        {
            composite.Add(new TestDisposable(() => disposed = true));
        }

        Assert.True(disposed);
    }

    private class TestDisposable : IDisposable
    {
        private readonly Action _onDispose;

        public TestDisposable(Action onDispose) => _onDispose = onDispose;

        public void Dispose() => _onDispose();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FieldScope_DisposeTwice_NoEffect()
    {
        var value = 0;
        var scope = new FieldScope<int>(() => value, v => value = v, 100);

        scope.Dispose();
        Assert.Equal(0, value);

        value = 999; // Change after first dispose
        scope.Dispose(); // Second dispose should be no-op
        Assert.Equal(999, value); // Value unchanged
    }

    [Fact]
    public void StackScope_EmptyStackOnDispose_Handles()
    {
        var stack = new Stack<int>();

        using (var scope = new StackScope<int>(stack, 1))
        {
            stack.Clear(); // Externally clear the stack
        }

        // Should not throw, just be empty
        Assert.Empty(stack);
    }

    #endregion
}
