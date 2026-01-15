using Novus.Diagnostics;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class BorrowGraphTests
{
    [Fact]
    public void BorrowGraph_AddBorrow_TracksRelationship()
    {
        var graph = new BorrowGraph();
        var loc = new SourceLocation("test.novus", 1, 1, 0, "");

        graph.RegisterVariable(1, "screen", scopeDepth: 1, loc);
        graph.RegisterVariable(2, "rp", scopeDepth: 1, loc);
        graph.AddBorrow(borrowerId: 2, sourceId: 1, loc, mutable: false);

        var chain = graph.GetBorrowChain(2);
        Assert.Contains(1, chain);
    }

    [Fact]
    public void BorrowGraph_TransitiveBorrow_TracksFullChain()
    {
        var graph = new BorrowGraph();
        var loc = new SourceLocation("test.novus", 1, 1, 0, "");

        graph.RegisterVariable(1, "screen", scopeDepth: 1, loc);
        graph.RegisterVariable(2, "rp", scopeDepth: 1, loc);
        graph.RegisterVariable(3, "pen", scopeDepth: 1, loc);
        graph.AddBorrow(borrowerId: 2, sourceId: 1, loc, mutable: false);
        graph.AddBorrow(borrowerId: 3, sourceId: 2, loc, mutable: false);

        var chain = graph.GetBorrowChain(3);
        Assert.Equal(3, chain.Count); // pen -> rp -> screen
        Assert.Equal(3, chain[0]);
        Assert.Equal(2, chain[1]);
        Assert.Equal(1, chain[2]);
    }

    [Fact]
    public void BorrowGraph_GetDanglingBorrows_FindsOutlivingReferences()
    {
        var graph = new BorrowGraph();
        var loc = new SourceLocation("test.novus", 1, 1, 0, "");

        graph.RegisterVariable(1, "screen", scopeDepth: 2, loc);  // Inner scope
        graph.RegisterVariable(2, "rp", scopeDepth: 1, loc);       // Outer scope
        graph.AddBorrow(borrowerId: 2, sourceId: 1, loc, mutable: false);

        var dangling = graph.GetDanglingBorrowsAtScopeExit(scopeDepth: 2);
        Assert.Single(dangling);
        Assert.Equal(2, dangling[0].BorrowerId);
        Assert.Equal(1, dangling[0].SourceId);
    }
}
