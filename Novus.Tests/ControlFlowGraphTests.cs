using Novus.IR;
using Xunit;

namespace Novus.Tests;

public class ControlFlowGraphTests
{
    [Fact]
    public void Build_SimpleLinearFunction_CreatesCorrectCFG()
    {
        // fn test() { return; }
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        entry.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);

        // Should have entry node, entry block, and exit node
        Assert.Equal(3, cfg.Nodes.Count);
        Assert.NotNull(cfg.EntryNode);
        Assert.NotNull(cfg.ExitNode);

        // Entry should have one successor (the entry block)
        Assert.Single(cfg.EntryNode.Successors);

        // Entry block should have one successor (exit)
        var entryBlock = cfg.GetNode("entry");
        Assert.NotNull(entryBlock);
        Assert.Single(entryBlock!.Successors);
        Assert.Equal(cfg.ExitNode, entryBlock.Successors[0].Destination);
    }

    [Fact]
    public void Build_UnconditionalBranch_CreatesCorrectEdges()
    {
        // fn test() {
        //   entry: goto target;
        //   target: return;
        // }
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var target = function.CreateBasicBlock("target");

        entry.AddInstruction(new IrBranch("target"));
        target.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);

        var entryNode = cfg.GetNode("entry");
        var targetNode = cfg.GetNode("target");

        Assert.NotNull(entryNode);
        Assert.NotNull(targetNode);

        // Entry should branch to target
        Assert.Single(entryNode!.Successors);
        Assert.Equal(targetNode, entryNode.Successors[0].Destination);

        // Target should have entry as predecessor
        Assert.Contains(entryNode, targetNode!.Predecessors);
    }

    [Fact]
    public void Build_ConditionalBranch_CreatesTwoEdges()
    {
        // fn test() {
        //   entry: if cond goto then else else;
        //   then: return;
        //   else: return;
        // }
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var thenBlock = function.CreateBasicBlock("then");
        var elseBlock = function.CreateBasicBlock("else");

        var cond = new IrBoolConstant(true);
        entry.AddInstruction(new IrConditionalBranch(cond, "then", "else"));
        thenBlock.AddInstruction(new IrReturn());
        elseBlock.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);

        var entryNode = cfg.GetNode("entry");
        var thenNode = cfg.GetNode("then");
        var elseNode = cfg.GetNode("else");

        Assert.NotNull(entryNode);
        Assert.NotNull(thenNode);
        Assert.NotNull(elseNode);

        // Entry should have two successors
        Assert.Equal(2, entryNode!.Successors.Count);

        // Both branches should be conditional
        Assert.All(entryNode.Successors, edge => Assert.True(edge.IsConditional));

        // Check that both then and else are successors
        var successorLabels = entryNode.Successors.Select(e => e.Destination.Label).ToHashSet();
        Assert.Contains("then", successorLabels);
        Assert.Contains("else", successorLabels);
    }

    [Fact]
    public void Build_Loop_CreatesBackEdge()
    {
        // fn test() {
        //   entry: goto loop;
        //   loop: if cond goto loop else exit;
        //   exit: return;
        // }
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var loop = function.CreateBasicBlock("loop");
        var exit = function.CreateBasicBlock("exit");

        entry.AddInstruction(new IrBranch("loop"));
        var cond = new IrBoolConstant(true);
        loop.AddInstruction(new IrConditionalBranch(cond, "loop", "exit"));
        exit.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);

        var loopNode = cfg.GetNode("loop");
        Assert.NotNull(loopNode);

        // Loop should have itself as a successor (back edge)
        var successors = loopNode!.Successors.Select(e => e.Destination).ToList();
        Assert.Contains(loopNode, successors);
    }

    [Fact]
    public void FindBackEdges_SimpleLoop_FindsBackEdge()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var loop = function.CreateBasicBlock("loop");
        var exit = function.CreateBasicBlock("exit");

        entry.AddInstruction(new IrBranch("loop"));
        var cond = new IrBoolConstant(true);
        loop.AddInstruction(new IrConditionalBranch(cond, "loop", "exit"));
        exit.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var backEdges = cfg.FindBackEdges();

        Assert.Single(backEdges);
        Assert.Equal("loop", backEdges[0].Source.Label);
        Assert.Equal("loop", backEdges[0].Destination.Label);
    }

    [Fact]
    public void FindBackEdges_NoLoop_FindsNoBackEdges()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        entry.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var backEdges = cfg.FindBackEdges();

        Assert.Empty(backEdges);
    }

    [Fact]
    public void DepthFirstTraversal_VisitsAllReachableNodes()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var block1 = function.CreateBasicBlock("block1");
        var block2 = function.CreateBasicBlock("block2");

        entry.AddInstruction(new IrBranch("block1"));
        block1.AddInstruction(new IrBranch("block2"));
        block2.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var visited = cfg.DepthFirstTraversal();

        // Should visit ENTRY, entry, block1, block2, EXIT
        Assert.Equal(5, visited.Count);
        Assert.Contains(visited, n => n.Label == "ENTRY");
        Assert.Contains(visited, n => n.Label == "entry");
        Assert.Contains(visited, n => n.Label == "block1");
        Assert.Contains(visited, n => n.Label == "block2");
        Assert.Contains(visited, n => n.Label == "EXIT");
    }

    [Fact]
    public void ReversePostOrder_ReturnsCorrectOrder()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var block1 = function.CreateBasicBlock("block1");
        var block2 = function.CreateBasicBlock("block2");

        entry.AddInstruction(new IrBranch("block1"));
        block1.AddInstruction(new IrBranch("block2"));
        block2.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var rpo = cfg.ReversePostOrder();

        // ENTRY should be first
        Assert.Equal("ENTRY", rpo[0].Label);

        // entry should come before block1
        var entryIdx = rpo.FindIndex(n => n.Label == "entry");
        var block1Idx = rpo.FindIndex(n => n.Label == "block1");
        Assert.True(entryIdx < block1Idx);

        // block1 should come before block2
        var block2Idx = rpo.FindIndex(n => n.Label == "block2");
        Assert.True(block1Idx < block2Idx);
    }

    [Fact]
    public void ComputeDominators_SimpleLinear_CorrectDominators()
    {
        // entry -> block1 -> exit
        // entry dominates everything
        // block1 dominates itself and exit
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var block1 = function.CreateBasicBlock("block1");

        entry.AddInstruction(new IrBranch("block1"));
        block1.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var dominators = cfg.ComputeDominators();

        var entryNode = cfg.GetNode("entry");
        var block1Node = cfg.GetNode("block1");

        // entry is dominated by ENTRY and itself
        Assert.Contains(cfg.EntryNode, dominators[entryNode!]!);
        Assert.Contains(entryNode, dominators[entryNode!]!);

        // block1 is dominated by ENTRY, entry, and itself
        Assert.Contains(cfg.EntryNode, dominators[block1Node!]!);
        Assert.Contains(entryNode, dominators[block1Node!]!);
        Assert.Contains(block1Node, dominators[block1Node!]!);
    }

    [Fact]
    public void ComputeDominators_Diamond_CorrectDominators()
    {
        //      entry
        //      /   \
        //   then   else
        //      \   /
        //       exit
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var thenBlock = function.CreateBasicBlock("then");
        var elseBlock = function.CreateBasicBlock("else");
        var exit = function.CreateBasicBlock("exit");

        var cond = new IrBoolConstant(true);
        entry.AddInstruction(new IrConditionalBranch(cond, "then", "else"));
        thenBlock.AddInstruction(new IrBranch("exit"));
        elseBlock.AddInstruction(new IrBranch("exit"));
        exit.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var dominators = cfg.ComputeDominators();

        var entryNode = cfg.GetNode("entry");
        var exitNode = cfg.GetNode("exit");
        var thenNode = cfg.GetNode("then");
        var elseNode = cfg.GetNode("else");

        // entry dominates exit (all paths go through entry)
        Assert.Contains(entryNode, dominators[exitNode!]!);

        // then does NOT dominate exit (can go through else)
        Assert.DoesNotContain(thenNode, dominators[exitNode!]!);

        // else does NOT dominate exit (can go through then)
        Assert.DoesNotContain(elseNode, dominators[exitNode!]!);
    }

    [Fact]
    public void ComputeImmediateDominators_SimpleLinear_CorrectIdoms()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var block1 = function.CreateBasicBlock("block1");

        entry.AddInstruction(new IrBranch("block1"));
        block1.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var idom = cfg.ComputeImmediateDominators();

        var entryNode = cfg.GetNode("entry");
        var block1Node = cfg.GetNode("block1");

        // entry's idom is ENTRY
        Assert.Equal(cfg.EntryNode, idom[entryNode!]);

        // block1's idom is entry
        Assert.Equal(entryNode, idom[block1Node!]);

        // ENTRY has no idom
        Assert.Null(idom[cfg.EntryNode]);
    }

    [Fact]
    public void BuildDominatorTree_CreatesCorrectTree()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var block1 = function.CreateBasicBlock("block1");
        var block2 = function.CreateBasicBlock("block2");

        entry.AddInstruction(new IrBranch("block1"));
        block1.AddInstruction(new IrBranch("block2"));
        block2.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var domTree = cfg.BuildDominatorTree();

        Assert.NotNull(domTree);
        Assert.Equal(cfg.EntryNode, domTree.Root);

        var entryNode = cfg.GetNode("entry");
        var block1Node = cfg.GetNode("block1");

        // ENTRY should dominate entry
        Assert.True(domTree.Dominates(cfg.EntryNode, entryNode!));

        // entry should dominate block1
        Assert.True(domTree.Dominates(entryNode!, block1Node!));

        // block1 should NOT dominate entry
        Assert.False(domTree.Dominates(block1Node!, entryNode!));
    }

    [Fact]
    public void GetReachableNodes_AllNodesReachable_ReturnsAll()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var block1 = function.CreateBasicBlock("block1");

        entry.AddInstruction(new IrBranch("block1"));
        block1.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var reachable = cfg.GetReachableNodes();

        // All nodes should be reachable
        Assert.Equal(cfg.Nodes.Count, reachable.Count);
    }

    [Fact]
    public void GetReachableNodes_UnreachableBlock_ExcludesIt()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var reachable = function.CreateBasicBlock("reachable");
        var unreachable = function.CreateBasicBlock("unreachable");

        entry.AddInstruction(new IrBranch("reachable"));
        reachable.AddInstruction(new IrReturn());
        // unreachable block has no incoming edges
        unreachable.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var reachableNodes = cfg.GetReachableNodes();

        var unreachableNode = cfg.GetNode("unreachable");

        // Unreachable block should not be in reachable set
        Assert.DoesNotContain(unreachableNode!, reachableNodes);
    }

    [Fact]
    public void CFGNode_ToString_ReturnsLabel()
    {
        var block = new IrBasicBlock("test_block");
        var node = new CFGNode(block, "test_block");

        Assert.Equal("test_block", node.ToString());
    }

    [Fact]
    public void CFGEdge_ToString_ReturnsSourceToDestination()
    {
        var source = new CFGNode(null, "source");
        var dest = new CFGNode(null, "dest");
        var edge = new CFGEdge(source, dest);

        Assert.Equal("source -> dest", edge.ToString());
    }

    [Fact]
    public void GetDominanceFrontier_Diamond_CorrectFrontier()
    {
        //      entry
        //      /   \
        //   then   else
        //      \   /
        //       exit
        var function = new IrFunction("test", IrVoidType.Instance);
        var entry = function.CreateBasicBlock("entry");
        var thenBlock = function.CreateBasicBlock("then");
        var elseBlock = function.CreateBasicBlock("else");
        var exit = function.CreateBasicBlock("exit");

        var cond = new IrBoolConstant(true);
        entry.AddInstruction(new IrConditionalBranch(cond, "then", "else"));
        thenBlock.AddInstruction(new IrBranch("exit"));
        elseBlock.AddInstruction(new IrBranch("exit"));
        exit.AddInstruction(new IrReturn());

        var cfg = new ControlFlowGraph(function);
        var domTree = cfg.BuildDominatorTree();

        var thenNode = cfg.GetNode("then");
        var elseNode = cfg.GetNode("else");
        var exitNode = cfg.GetNode("exit");

        // Dominance frontier of 'then' should include 'exit'
        // (then dominates a predecessor of exit but doesn't strictly dominate exit)
        var thenFrontier = domTree.GetDominanceFrontier(thenNode!, cfg);
        Assert.Contains(exitNode!, thenFrontier);

        // Dominance frontier of 'else' should also include 'exit'
        var elseFrontier = domTree.GetDominanceFrontier(elseNode!, cfg);
        Assert.Contains(exitNode!, elseFrontier);
    }
}
