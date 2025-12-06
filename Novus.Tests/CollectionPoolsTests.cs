using Novus.Infrastructure;
using Novus.IR;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for CollectionPools and PooledCollections helpers.
/// </summary>
public class CollectionPoolsTests
{
    public CollectionPoolsTests()
    {
        // Reset pool stats before each test
        CollectionPools.ResetAllStats();
    }

    #region IrValueLists Pool

    [Fact]
    public void IrValueLists_RentReturnsEmptyList()
    {
        var list = CollectionPools.IrValueLists.Rent();

        Assert.NotNull(list);
        Assert.Empty(list);

        CollectionPools.IrValueLists.Return(list);
    }

    [Fact]
    public void IrValueLists_ReturnClearsList()
    {
        var list = CollectionPools.IrValueLists.Rent();
        list.Add(new IrConstant(42, IrIntType.I32));
        list.Add(new IrConstant(99, IrIntType.I32));
        CollectionPools.IrValueLists.Return(list);

        var list2 = CollectionPools.IrValueLists.Rent();

        Assert.Same(list, list2);
        Assert.Empty(list2);

        CollectionPools.IrValueLists.Return(list2);
    }

    [Fact]
    public void RentIrValueList_AutoReturnsOnDispose()
    {
        // Rent a list, then dispose to return it
        // After disposal, we should be able to rent it back
        List<IrValue>? capturedList;

        using (var pooled = PooledCollections.RentIrValueList())
        {
            pooled.Value.Add(new IrConstant(1, IrIntType.I32));
            capturedList = pooled.Value;
        }

        // The list should be returned to the pool and cleared
        // Next rent should get the same list back (reused)
        var nextList = CollectionPools.IrValueLists.Rent();
        Assert.Same(capturedList, nextList);
        Assert.Empty(nextList); // Should be cleared

        CollectionPools.IrValueLists.Return(nextList);
    }

    #endregion

    #region StringLists Pool

    [Fact]
    public void StringLists_RentReturnsEmptyList()
    {
        var list = CollectionPools.StringLists.Rent();

        Assert.NotNull(list);
        Assert.Empty(list);

        CollectionPools.StringLists.Return(list);
    }

    [Fact]
    public void StringLists_ReturnClearsList()
    {
        var list = CollectionPools.StringLists.Rent();
        list.Add("test1");
        list.Add("test2");
        CollectionPools.StringLists.Return(list);

        var list2 = CollectionPools.StringLists.Rent();

        Assert.Same(list, list2);
        Assert.Empty(list2);

        CollectionPools.StringLists.Return(list2);
    }

    [Fact]
    public void RentStringList_AutoReturnsOnDispose()
    {
        using (var pooled = PooledCollections.RentStringList())
        {
            pooled.Value.Add("item1");
            pooled.Value.Add("item2");
            Assert.Equal(2, pooled.Value.Count);
        }

        // List was returned and cleared
        var nextList = CollectionPools.StringLists.Rent();
        Assert.Empty(nextList);
        CollectionPools.StringLists.Return(nextList);
    }

    #endregion

    #region StringToIrValueDicts Pool

    [Fact]
    public void StringToIrValueDicts_RentReturnsEmptyDict()
    {
        var dict = CollectionPools.StringToIrValueDicts.Rent();

        Assert.NotNull(dict);
        Assert.Empty(dict);

        CollectionPools.StringToIrValueDicts.Return(dict);
    }

    [Fact]
    public void StringToIrValueDicts_ReturnClearsDict()
    {
        var dict = CollectionPools.StringToIrValueDicts.Rent();
        dict["x"] = new IrConstant(1, IrIntType.I32);
        dict["y"] = new IrConstant(2, IrIntType.I32);
        CollectionPools.StringToIrValueDicts.Return(dict);

        var dict2 = CollectionPools.StringToIrValueDicts.Rent();

        Assert.Same(dict, dict2);
        Assert.Empty(dict2);

        CollectionPools.StringToIrValueDicts.Return(dict2);
    }

    #endregion

    #region StringToIrTypeDicts Pool

    [Fact]
    public void StringToIrTypeDicts_RentReturnsEmptyDict()
    {
        var dict = CollectionPools.StringToIrTypeDicts.Rent();

        Assert.NotNull(dict);
        Assert.Empty(dict);

        CollectionPools.StringToIrTypeDicts.Return(dict);
    }

    [Fact]
    public void RentStringToIrTypeDict_AutoReturnsOnDispose()
    {
        using (var pooled = PooledCollections.RentStringToIrTypeDict())
        {
            pooled.Value["T"] = IrIntType.I32;
            Assert.Single(pooled.Value);
        }

        var nextDict = CollectionPools.StringToIrTypeDicts.Rent();
        Assert.Empty(nextDict);
        CollectionPools.StringToIrTypeDicts.Return(nextDict);
    }

    #endregion

    #region StringHashSets Pool

    [Fact]
    public void StringHashSets_RentReturnsEmptySet()
    {
        var set = CollectionPools.StringHashSets.Rent();

        Assert.NotNull(set);
        Assert.Empty(set);

        CollectionPools.StringHashSets.Return(set);
    }

    [Fact]
    public void StringHashSets_ReturnClearsSet()
    {
        var set = CollectionPools.StringHashSets.Rent();
        set.Add("item1");
        set.Add("item2");
        CollectionPools.StringHashSets.Return(set);

        var set2 = CollectionPools.StringHashSets.Rent();

        Assert.Same(set, set2);
        Assert.Empty(set2);

        CollectionPools.StringHashSets.Return(set2);
    }

    [Fact]
    public void RentStringHashSet_AutoReturnsOnDispose()
    {
        using (var pooled = PooledCollections.RentStringHashSet())
        {
            pooled.Value.Add("a");
            pooled.Value.Add("b");
            Assert.Equal(2, pooled.Value.Count);
        }

        var nextSet = CollectionPools.StringHashSets.Rent();
        Assert.Empty(nextSet);
        CollectionPools.StringHashSets.Return(nextSet);
    }

    #endregion

    #region StringBuilders Pool

    [Fact]
    public void StringBuilders_RentReturnsEmptyBuilder()
    {
        var sb = CollectionPools.StringBuilders.Rent();

        Assert.NotNull(sb);
        Assert.Equal(0, sb.Length);

        CollectionPools.StringBuilders.Return(sb);
    }

    [Fact]
    public void StringBuilders_ReturnClearsBuilder()
    {
        var sb = CollectionPools.StringBuilders.Rent();
        sb.Append("Hello World");
        CollectionPools.StringBuilders.Return(sb);

        var sb2 = CollectionPools.StringBuilders.Rent();

        Assert.Same(sb, sb2);
        Assert.Equal(0, sb2.Length);

        CollectionPools.StringBuilders.Return(sb2);
    }

    [Fact]
    public void RentStringBuilder_AutoReturnsOnDispose()
    {
        using (var pooled = PooledCollections.RentStringBuilder())
        {
            pooled.Value.Append("test");
            Assert.Equal(4, pooled.Value.Length);
        }

        var nextSb = CollectionPools.StringBuilders.Rent();
        Assert.Equal(0, nextSb.Length);
        CollectionPools.StringBuilders.Return(nextSb);
    }

    #endregion

    #region GetAllStats and ResetAllStats

    [Fact]
    public void GetAllStats_ReturnsAllPoolStats()
    {
        // Make some allocations to get non-zero stats
        CollectionPools.IrValueLists.Return(CollectionPools.IrValueLists.Rent());
        CollectionPools.StringLists.Return(CollectionPools.StringLists.Rent());

        var stats = CollectionPools.GetAllStats().ToList();

        Assert.Equal(8, stats.Count);
        Assert.Contains(stats, s => s.name == "IrValueLists");
        Assert.Contains(stats, s => s.name == "StringLists");
        Assert.Contains(stats, s => s.name == "StringToIrValueDicts");
        Assert.Contains(stats, s => s.name == "StringToIrTypeDicts");
        Assert.Contains(stats, s => s.name == "StringHashSets");
        Assert.Contains(stats, s => s.name == "BasicBlockHashSets");
        Assert.Contains(stats, s => s.name == "BasicBlockLists");
        Assert.Contains(stats, s => s.name == "StringBuilders");
    }

    [Fact]
    public void ResetAllStats_ClearsAllCounters()
    {
        // Make some allocations
        _ = CollectionPools.IrValueLists.Rent();
        _ = CollectionPools.StringLists.Rent();

        CollectionPools.ResetAllStats();

        var stats = CollectionPools.GetAllStats().ToList();
        Assert.All(stats, s =>
        {
            Assert.Equal(0, s.allocated);
            Assert.Equal(0, s.reused);
        });
    }

    #endregion

    #region ClearAll

    [Fact]
    public void ClearAll_RemovesAllPooledObjects()
    {
        // Add some objects to pools
        CollectionPools.IrValueLists.Return(CollectionPools.IrValueLists.Rent());
        CollectionPools.StringLists.Return(CollectionPools.StringLists.Rent());
        CollectionPools.StringToIrValueDicts.Return(CollectionPools.StringToIrValueDicts.Rent());

        CollectionPools.ClearAll();

        Assert.Equal(0, CollectionPools.IrValueLists.AvailableCount);
        Assert.Equal(0, CollectionPools.StringLists.AvailableCount);
        Assert.Equal(0, CollectionPools.StringToIrValueDicts.AvailableCount);
    }

    #endregion

    #region Reuse Verification

    [Fact]
    public void MultipleRentReturn_ReusesObjects()
    {
        CollectionPools.ResetAllStats();

        // Simulate typical usage pattern
        for (int i = 0; i < 10; i++)
        {
            var list = CollectionPools.IrValueLists.Rent();
            list.Add(new IrConstant(i, IrIntType.I32));
            CollectionPools.IrValueLists.Return(list);
        }

        var stats = CollectionPools.IrValueLists.GetStats();

        // Should only allocate once, reuse 9 times
        Assert.Equal(1, stats.allocated);
        Assert.Equal(9, stats.reused);
        Assert.True(stats.reuseRate > 80);
    }

    #endregion
}
