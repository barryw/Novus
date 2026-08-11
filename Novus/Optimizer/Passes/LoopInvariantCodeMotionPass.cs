using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Loop-Invariant Code Motion (LICM) optimization pass.
/// Moves loop-invariant computations out of loops to reduce redundant work.
/// Important for 68020+ performance where repeated memory work is expensive.
/// </summary>
public class LoopInvariantCodeMotionPass : FunctionPassBase
{
    public override string Name => "Loop-Invariant Code Motion (LICM)";

    public override bool RunOnFunction(IrFunction function)
    {
        if (function.BasicBlocks.Count == 0) return false;

        // Build CFG and detect loops
        var cfg = new ControlFlowGraph(function);
        var detector = new LoopDetector(cfg);
        var loops = detector.DetectLoops();

        if (loops.Count == 0) return false;

        bool changed = false;

        // Process loops innermost-first (already sorted by DetectLoops)
        foreach (var loop in loops)
        {
            if (MoveInvariantCode(loop, function, detector))
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Move loop-invariant code out of the loop to a preheader block
    /// </summary>
    private bool MoveInvariantCode(Loop loop, IrFunction function, LoopDetector detector)
    {
        // Find or create a preheader block for this loop
        var preheader = GetOrCreatePreheader(loop, function);
        if (preheader == null) return false;

        bool moved = false;
        var movedInstructions = new List<(IrBasicBlock sourceBlock, int index, IrInstruction instruction)>();

        // Scan all blocks in the loop for invariant instructions
        foreach (var node in loop.Body)
        {
            if (node.Block == null) continue;
            if (node == loop.Header) continue; // Don't process header yet

            var block = node.Block;

            // Scan instructions in this block
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var instruction = block.Instructions[i];

                // Only move safe, side-effect-free instructions
                if (!IsSafeToMove(instruction)) continue;

                // Check if it's loop-invariant
                if (detector.IsLoopInvariant(instruction, loop))
                {
                    // Check if all uses of this instruction dominate the loop
                    // (We can only move if the instruction result isn't used before it's moved)
                    if (IsSafeToHoist(instruction, loop, function))
                    {
                        movedInstructions.Add((block, i, instruction));
                        moved = true;
                    }
                }
            }
        }

        // Now actually move the instructions
        // Process in reverse order to maintain indices
        foreach (var (sourceBlock, index, instruction) in movedInstructions.OrderByDescending(x => x.index))
        {
            // Remove from original location
            sourceBlock.Instructions.RemoveAt(index);

            // Add to preheader (before the branch)
            // Preheader should end with a branch to loop header
            if (preheader.Instructions.Count > 0 &&
                preheader.Instructions[^1] is IrBranch)
            {
                preheader.Instructions.Insert(preheader.Instructions.Count - 1, instruction);
            }
            else
            {
                preheader.Instructions.Add(instruction);
            }
        }

        return moved;
    }

    /// <summary>
    /// Check if an instruction is safe to move (no side effects)
    /// </summary>
    private bool IsSafeToMove(IrInstruction instruction)
    {
        return instruction switch
        {
            IrBinaryOp => true,  // Pure computation
            // Don't move instructions with side effects
            IrCall => false,
            IrStore => false,
            IrDereferenceStore => false,
            IrIndexStore => false,
            IrReturn => false,
            IrBranch => false,
            IrConditionalBranch => false,
            _ => false
        };
    }

    /// <summary>
    /// Check if it's safe to hoist an instruction out of the loop.
    /// Safe if the instruction doesn't have dependencies on loop-variant values
    /// and its result is only used after the preheader.
    /// </summary>
    private bool IsSafeToHoist(IrInstruction instruction, Loop loop, IrFunction function)
    {
        // For now, use a conservative approach:
        // Only hoist if the instruction is a simple binary operation with no control flow dependencies

        if (instruction is IrBinaryOp binOp)
        {
            // Check that the instruction doesn't define a variable that's used in the loop header's condition
            // (This would change loop semantics)
            var defVar = binOp.ResultName;

            // Check loop header for uses of this variable
            if (loop.Header.Block != null)
            {
                foreach (var headerInst in loop.Header.Block.Instructions)
                {
                    if (UsesVariable(headerInst, defVar))
                    {
                        // Used in header - might affect loop condition, don't hoist
                        return false;
                    }
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if an instruction uses a specific variable
    /// </summary>
    private bool UsesVariable(IrInstruction instruction, string varName)
    {
        return instruction switch
        {
            IrBinaryOp binOp => UsesVar(binOp.Left, varName) || UsesVar(binOp.Right, varName),
            IrConditionalBranch condBr => UsesVar(condBr.Condition, varName),
            IrStore store => UsesVar(store.Value, varName),
            IrReturn ret when ret.Value != null => UsesVar(ret.Value, varName),
            _ => false
        };
    }

    private bool UsesVar(IrValue value, string varName)
    {
        return value is IrVariable var && var.Name == varName;
    }

    /// <summary>
    /// Get or create a preheader block for a loop.
    /// The preheader is a block that:
    /// - Has exactly one successor (the loop header)
    /// - Is the only predecessor of the loop header that's outside the loop
    /// </summary>
    private IrBasicBlock? GetOrCreatePreheader(Loop loop, IrFunction function)
    {
        var header = loop.Header;
        if (header.Block == null) return null;

        // Find predecessors of the header that are NOT in the loop
        var externalPreds = header.Predecessors
            .Where(pred => !loop.Contains(pred))
            .ToList();

        if (externalPreds.Count == 0)
        {
            // No external entry to loop - this shouldn't happen in well-formed IR
            return null;
        }

        if (externalPreds.Count == 1)
        {
            // Single external predecessor - check if it can serve as preheader
            var pred = externalPreds[0];
            if (pred.Block != null && pred.Successors.Count == 1)
            {
                // Perfect - it's already a preheader
                return pred.Block;
            }
        }

        // Need to create a new preheader block
        // Insert it between external predecessors and the loop header
        var preheader = new IrBasicBlock($"{header.Label}_preheader");

        // Add branch from preheader to loop header
        preheader.Instructions.Add(new IrBranch(header.Label));

        // Insert preheader into function's basic blocks
        // Find the index of the loop header
        var headerIndex = function.BasicBlocks.FindIndex(b => b.Label == header.Label);
        if (headerIndex >= 0)
        {
            function.BasicBlocks.Insert(headerIndex, preheader);
        }
        else
        {
            // Fallback: add at beginning
            function.BasicBlocks.Insert(0, preheader);
        }

        // Update external predecessors to branch to preheader instead of header
        foreach (var predNode in externalPreds)
        {
            if (predNode.Block == null) continue;

            // Update the last instruction (branch/conditional branch)
            var lastInst = predNode.Block.Instructions.LastOrDefault();
            if (lastInst is IrBranch branch && branch.Target == header.Label)
            {
                branch.Target = preheader.Label;
            }
            else if (lastInst is IrConditionalBranch condBranch)
            {
                if (condBranch.TrueTarget == header.Label)
                {
                    condBranch.TrueTarget = preheader.Label;
                }
                if (condBranch.FalseTarget == header.Label)
                {
                    condBranch.FalseTarget = preheader.Label;
                }
            }
        }

        return preheader;
    }
}
