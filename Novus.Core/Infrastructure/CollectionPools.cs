using System.Collections.Concurrent;
using Novus.IR;

namespace Novus.Infrastructure;

/// <summary>
/// Pre-configured pools for frequently-allocated collection types in the compiler.
/// These pools help reduce GC pressure during IR building and optimization passes.
/// </summary>
public static class CollectionPools
{
    /// <summary>
    /// Pool for List&lt;IrValue&gt; used for function arguments and temporary value lists.
    /// </summary>
    public static readonly ObjectPool<List<IrValue>> IrValueLists =
        new(maxPoolSize: 200, resetAction: list => list.Clear());

    /// <summary>
    /// Pool for List&lt;string&gt; used for variable tracking and name lists.
    /// </summary>
    public static readonly ObjectPool<List<string>> StringLists =
        new(maxPoolSize: 100, resetAction: list => list.Clear());

    /// <summary>
    /// Pool for Dictionary&lt;string, IrValue&gt; used for struct fields.
    /// </summary>
    public static readonly ObjectPool<Dictionary<string, IrValue>> StringToIrValueDicts =
        new(maxPoolSize: 100, resetAction: dict => dict.Clear());

    /// <summary>
    /// Pool for Dictionary&lt;string, IrType&gt; used for generic type substitutions.
    /// </summary>
    public static readonly ObjectPool<Dictionary<string, IrType>> StringToIrTypeDicts =
        new(maxPoolSize: 100, resetAction: dict => dict.Clear());

    /// <summary>
    /// Pool for HashSet&lt;string&gt; used in optimization passes.
    /// </summary>
    public static readonly ObjectPool<HashSet<string>> StringHashSets =
        new(maxPoolSize: 50, resetAction: set => set.Clear());

    /// <summary>
    /// Pool for HashSet&lt;IrBasicBlock&gt; used in SSA construction and CFG analysis.
    /// </summary>
    public static readonly ObjectPool<HashSet<IrBasicBlock>> BasicBlockHashSets =
        new(maxPoolSize: 50, resetAction: set => set.Clear());

    /// <summary>
    /// Pool for List&lt;IrBasicBlock&gt; used for block worklists.
    /// </summary>
    public static readonly ObjectPool<List<IrBasicBlock>> BasicBlockLists =
        new(maxPoolSize: 50, resetAction: list => list.Clear());

    /// <summary>
    /// Pool for StringBuilder used for string construction.
    /// </summary>
    public static readonly ObjectPool<System.Text.StringBuilder> StringBuilders =
        new(maxPoolSize: 50, resetAction: sb => sb.Clear());

    /// <summary>
    /// Get statistics for all collection pools.
    /// </summary>
    public static IEnumerable<(string name, int allocated, int reused, int available, double reuseRate)> GetAllStats()
    {
        yield return ("IrValueLists", IrValueLists.AllocatedCount, IrValueLists.ReuseCount,
            IrValueLists.AvailableCount, GetReuseRate(IrValueLists));
        yield return ("StringLists", StringLists.AllocatedCount, StringLists.ReuseCount,
            StringLists.AvailableCount, GetReuseRate(StringLists));
        yield return ("StringToIrValueDicts", StringToIrValueDicts.AllocatedCount, StringToIrValueDicts.ReuseCount,
            StringToIrValueDicts.AvailableCount, GetReuseRate(StringToIrValueDicts));
        yield return ("StringToIrTypeDicts", StringToIrTypeDicts.AllocatedCount, StringToIrTypeDicts.ReuseCount,
            StringToIrTypeDicts.AvailableCount, GetReuseRate(StringToIrTypeDicts));
        yield return ("StringHashSets", StringHashSets.AllocatedCount, StringHashSets.ReuseCount,
            StringHashSets.AvailableCount, GetReuseRate(StringHashSets));
        yield return ("BasicBlockHashSets", BasicBlockHashSets.AllocatedCount, BasicBlockHashSets.ReuseCount,
            BasicBlockHashSets.AvailableCount, GetReuseRate(BasicBlockHashSets));
        yield return ("BasicBlockLists", BasicBlockLists.AllocatedCount, BasicBlockLists.ReuseCount,
            BasicBlockLists.AvailableCount, GetReuseRate(BasicBlockLists));
        yield return ("StringBuilders", StringBuilders.AllocatedCount, StringBuilders.ReuseCount,
            StringBuilders.AvailableCount, GetReuseRate(StringBuilders));
    }

    private static double GetReuseRate<T>(ObjectPool<T> pool) where T : class, new()
    {
        var total = pool.AllocatedCount + pool.ReuseCount;
        return total > 0 ? (double)pool.ReuseCount / total * 100 : 0;
    }

    /// <summary>
    /// Reset statistics for all pools.
    /// </summary>
    public static void ResetAllStats()
    {
        IrValueLists.ResetStats();
        StringLists.ResetStats();
        StringToIrValueDicts.ResetStats();
        StringToIrTypeDicts.ResetStats();
        StringHashSets.ResetStats();
        BasicBlockHashSets.ResetStats();
        BasicBlockLists.ResetStats();
        StringBuilders.ResetStats();
    }

    /// <summary>
    /// Clear all pools (release pooled objects to GC).
    /// </summary>
    public static void ClearAll()
    {
        IrValueLists.Clear();
        StringLists.Clear();
        StringToIrValueDicts.Clear();
        StringToIrTypeDicts.Clear();
        StringHashSets.Clear();
        BasicBlockHashSets.Clear();
        BasicBlockLists.Clear();
        StringBuilders.Clear();
    }
}

/// <summary>
/// Helper methods for working with pooled collections.
/// </summary>
public static class PooledCollections
{
    /// <summary>
    /// Rent a List&lt;IrValue&gt; and return it automatically when disposed.
    /// </summary>
    /// <example>
    /// using var args = PooledCollections.RentIrValueList();
    /// args.Value.Add(someValue);
    /// // List is automatically returned to pool when scope ends
    /// </example>
    public static PooledObject<List<IrValue>> RentIrValueList()
        => CollectionPools.IrValueLists.RentScoped();

    /// <summary>
    /// Rent a List&lt;string&gt; and return it automatically when disposed.
    /// </summary>
    public static PooledObject<List<string>> RentStringList()
        => CollectionPools.StringLists.RentScoped();

    /// <summary>
    /// Rent a Dictionary&lt;string, IrValue&gt; and return it automatically when disposed.
    /// </summary>
    public static PooledObject<Dictionary<string, IrValue>> RentStringToIrValueDict()
        => CollectionPools.StringToIrValueDicts.RentScoped();

    /// <summary>
    /// Rent a Dictionary&lt;string, IrType&gt; and return it automatically when disposed.
    /// </summary>
    public static PooledObject<Dictionary<string, IrType>> RentStringToIrTypeDict()
        => CollectionPools.StringToIrTypeDicts.RentScoped();

    /// <summary>
    /// Rent a HashSet&lt;string&gt; and return it automatically when disposed.
    /// </summary>
    public static PooledObject<HashSet<string>> RentStringHashSet()
        => CollectionPools.StringHashSets.RentScoped();

    /// <summary>
    /// Rent a StringBuilder and return it automatically when disposed.
    /// </summary>
    public static PooledObject<System.Text.StringBuilder> RentStringBuilder()
        => CollectionPools.StringBuilders.RentScoped();
}
