using Novus.Diagnostics;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for AttributeInfo and AttributeCollection classes
/// </summary>
public class AttributeInfoTests
{
    private static SourceLocation DummyLocation => new("test.novus", 1, 1, 0, "");

    [Fact]
    public void HasFlag_WithNamedArg_ReturnsTrue()
    {
        var attr = new AttributeInfo("test", DummyLocation);
        attr.NamedArgs["should_panic"] = true;

        Assert.True(attr.HasFlag("should_panic"));
    }

    [Fact]
    public void HasFlag_WithPositionalIdentifier_ReturnsTrue()
    {
        var attr = new AttributeInfo("test", DummyLocation);
        attr.PositionalArgs.Add("should_panic");

        Assert.True(attr.HasFlag("should_panic"));
    }

    [Fact]
    public void HasFlag_NotPresent_ReturnsFalse()
    {
        var attr = new AttributeInfo("test", DummyLocation);
        attr.PositionalArgs.Add("some_other_flag");

        Assert.False(attr.HasFlag("should_panic"));
    }

    [Fact]
    public void HasFlag_DistinguishesFlagsFromDescriptions()
    {
        // Flags are bare identifiers like "should_panic", "skip"
        // Descriptions are typically quoted strings or longer text
        var attr = new AttributeInfo("test", DummyLocation);
        attr.PositionalArgs.Add("This is a description");

        // HasFlag checks for exact string match - descriptions won't match flag names
        Assert.False(attr.HasFlag("should_panic"));
        Assert.False(attr.HasFlag("skip"));
    }

    [Fact]
    public void HasArg_OnlyChecksNamedArgs()
    {
        var attr = new AttributeInfo("test", DummyLocation);
        attr.PositionalArgs.Add("should_panic");

        // HasArg only checks NamedArgs, not PositionalArgs
        Assert.False(attr.HasArg("should_panic"));
    }

    [Fact]
    public void HasArg_WithNamedArg_ReturnsTrue()
    {
        var attr = new AttributeInfo("test", DummyLocation);
        attr.NamedArgs["skip"] = "reason";

        Assert.True(attr.HasArg("skip"));
    }

    [Fact]
    public void GetString_ReturnsNamedArgValue()
    {
        var attr = new AttributeInfo("test", DummyLocation);
        attr.NamedArgs["skip"] = "Not implemented yet";

        Assert.Equal("Not implemented yet", attr.GetString("skip"));
    }

    [Fact]
    public void GetString_ReturnsNull_WhenNotPresent()
    {
        var attr = new AttributeInfo("test", DummyLocation);

        Assert.Null(attr.GetString("skip"));
    }

    [Fact]
    public void GetPositionalArg_ReturnsCorrectValue()
    {
        var attr = new AttributeInfo("test", DummyLocation);
        attr.PositionalArgs.Add("First description");
        attr.PositionalArgs.Add("Second value");

        Assert.Equal("First description", attr.GetPositionalArg<string>(0));
        Assert.Equal("Second value", attr.GetPositionalArg<string>(1));
    }

    [Fact]
    public void GetPositionalArg_ReturnsDefault_WhenOutOfRange()
    {
        var attr = new AttributeInfo("test", DummyLocation);

        Assert.Null(attr.GetPositionalArg<string>(0));
        Assert.Null(attr.GetPositionalArg<string>(5));
    }

    [Fact]
    public void Collection_Has_ReturnsTrue_WhenAttributeExists()
    {
        var collection = new AttributeCollection();
        collection.Add(new AttributeInfo("test", DummyLocation));
        collection.Add(new AttributeInfo("export", DummyLocation));

        Assert.True(collection.Has("test"));
        Assert.True(collection.Has("export"));
        Assert.False(collection.Has("inline"));
    }

    [Fact]
    public void Collection_Get_ReturnsAttribute()
    {
        var collection = new AttributeCollection();
        var testAttr = new AttributeInfo("test", DummyLocation);
        testAttr.NamedArgs["should_panic"] = true;
        collection.Add(testAttr);

        var retrieved = collection.Get("test");
        Assert.NotNull(retrieved);
        Assert.True(retrieved.HasFlag("should_panic"));
    }

    [Fact]
    public void KnownAttributes_ContainsTest()
    {
        Assert.True(KnownAttributes.IsKnown("test"));
        Assert.True(KnownAttributes.IsKnown("benchmark"));
        Assert.True(KnownAttributes.IsKnown("export"));
        Assert.False(KnownAttributes.IsKnown("unknown_attr"));
    }

    [Fact]
    public void TestAttribute_WithBothFlagsAndDescription()
    {
        // Simulate: @test("My test description", should_panic)
        var attr = new AttributeInfo("test", DummyLocation);
        attr.PositionalArgs.Add("My test description");
        attr.PositionalArgs.Add("should_panic");

        // Should be able to get description from first positional arg
        Assert.Equal("My test description", attr.GetPositionalArg<string>(0));

        // should_panic flag should be detected
        Assert.True(attr.HasFlag("should_panic"));
    }
}
