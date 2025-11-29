namespace Novus.IR;

/// <summary>
/// Loop-Invariant Code Motion (LICM) optimization pass.
///
/// LICM identifies computations inside loops that don't change between iterations
/// and "hoists" them out of the loop to execute only once. This reduces the number
/// of redundant computations and can significantly improve performance.
///
/// A computation is loop-invariant if:
/// 1. All its operands are defined outside the loop, OR
/// 2. All its operands are themselves loop-invariant
///
/// For correctness, we can only hoist an instruction if:
/// 1. It's loop-invariant
/// 2. It dominates all its uses within the loop (in SSA, this is automatic)
/// 3. It has no side effects
/// 4. It doesn't depend on control flow within the loop
///
/// Example:
///   loop:
///     x = a + b      // a and b defined outside loop - invariant!
///     y = x * i      // x is loop-invariant, but y depends on i - not invariant
///     i = i + 1
///     if i < 10 goto loop
///
/// After LICM:
///   x = a + b        // Hoisted out!
///   loop:
///     y = x * i
///     i = i + 1
///     if i < 10 goto loop
///
/// Algorithm:
/// 1. Identify loops (natural loops from back edges)
/// 2. Find loop-invariant instructions
/// 3. Hoist safe instructions to loop preheader
/// 4. Create preheader if it doesn't exist
///
/// Benefits for 68k:
/// - Fewer instructions executed per iteration
/// - Better register allocation (values computed once, used many times)
/// - Reduces memory traffic
/// - Critical for inner loops in graphics/audio code
/// </summary>
public class LoopInvariantCodeMotion
{
    private readonly IrFunction _function;

    /// <summary>
    /// Represents a loop in the control flow graph
    /// </summary>
    private class Loop
    {
        public IrBasicBlock Header { get; set; }
        public HashSet<IrBasicBlock> Body { get; set; } = new();
        public IrBasicBlock? Preheader { get; set; }

        public Loop(IrBasicBlock header)
        {
            Header = header;
            Body.Add(header);
        }
    }

    public LoopInvariantCodeMotion(IrFunction function)
    {
        _function = function;
    }

    /// <summary>
    /// Run LICM optimization.
    /// Returns number of instructions hoisted.
    /// </summary>
    public int Optimize()
    {
        int hoistedCount = 0;

        // Find all loops in the function
        var loops = FindLoops();

        // For each loop, hoist invariant code
        foreach (var loop in loops)
        {
            hoistedCount += OptimizeLoop(loop);
        }

        return hoistedCount;
    }

    /// <summary>
    /// Find all natural loops in the function's control flow graph
    /// </summary>
    private List<Loop> FindLoops()
    {
        var loops = new List<Loop>();

        // For now, use a simple heuristic: any block with a backward branch
        // In a full implementation, we'd compute dominators and find back edges
        foreach (var block in _function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                // Look for branches that go backward (potential loop)
                if (instruction is IrBranch branch)
                {
                    var targetBlock = FindBlockByLabel(branch.Target);
                    if (targetBlock != null)
                    {
                        int targetIndex = _function.BasicBlocks.IndexOf(targetBlock);
                        int currentIndex = _function.BasicBlocks.IndexOf(block);

                        // Back edge (loop!)
                        if (targetIndex <= currentIndex)
                        {
                            var loop = new Loop(targetBlock);
                            // Add all blocks from target to current as loop body
                            for (int i = targetIndex; i <= currentIndex; i++)
                            {
                                loop.Body.Add(_function.BasicBlocks[i]);
                            }
                            loops.Add(loop);
                        }
                    }
                }
                else if (instruction is IrConditionalBranch condBranch)
                {
                    // Check both targets
                    CheckBackEdge(condBranch.TrueTarget, block, loops);
                    CheckBackEdge(condBranch.FalseTarget, block, loops);
                }
            }
        }

        return loops;
    }

    /// <summary>
    /// Check if a branch target forms a back edge (loop)
    /// </summary>
    private void CheckBackEdge(string targetLabel, IrBasicBlock currentBlock, List<Loop> loops)
    {
        var targetBlock = FindBlockByLabel(targetLabel);
        if (targetBlock != null)
        {
            int targetIndex = _function.BasicBlocks.IndexOf(targetBlock);
            int currentIndex = _function.BasicBlocks.IndexOf(currentBlock);

            if (targetIndex <= currentIndex)
            {
                // Don't create duplicate loops
                if (!loops.Any(l => l.Header == targetBlock))
                {
                    var loop = new Loop(targetBlock);
                    for (int i = targetIndex; i <= currentIndex; i++)
                    {
                        loop.Body.Add(_function.BasicBlocks[i]);
                    }
                    loops.Add(loop);
                }
            }
        }
    }

    /// <summary>
    /// Optimize a single loop by hoisting invariant code
    /// </summary>
    private int OptimizeLoop(Loop loop)
    {
        int hoistedCount = 0;

        // Ensure loop has a preheader
        EnsurePreheader(loop);

        // Find all loop-invariant instructions
        var invariantInstructions = FindLoopInvariants(loop);

        // Hoist them to the preheader
        foreach (var (block, instruction) in invariantInstructions)
        {
            if (IsSafeToHoist(instruction, loop))
            {
                HoistInstruction(instruction, block, loop);
                hoistedCount++;
            }
        }

        return hoistedCount;
    }

    /// <summary>
    /// Ensure loop has a preheader block (create if needed)
    /// </summary>
    private void EnsurePreheader(Loop loop)
    {
        // For simplicity, we'll just mark the block before the header as the preheader
        // In a full implementation, we'd insert a new block if needed
        int headerIndex = _function.BasicBlocks.IndexOf(loop.Header);
        if (headerIndex > 0)
        {
            loop.Preheader = _function.BasicBlocks[headerIndex - 1];
        }
    }

    /// <summary>
    /// Find all loop-invariant instructions in the loop
    /// </summary>
    private List<(IrBasicBlock block, IrInstruction instruction)> FindLoopInvariants(Loop loop)
    {
        var invariants = new List<(IrBasicBlock, IrInstruction)>();
        var invariantNames = new HashSet<string>();

        // Keep iterating until no new invariants found (fixpoint)
        bool changed;
        do
        {
            changed = false;

            foreach (var block in loop.Body)
            {
                foreach (var instruction in block.Instructions)
                {
                    if (IsLoopInvariant(instruction, loop, invariantNames))
                    {
                        string? defName = GetDefinedVariable(instruction);
                        if (defName != null && !invariantNames.Contains(defName))
                        {
                            invariants.Add((block, instruction));
                            invariantNames.Add(defName);
                            changed = true;
                        }
                    }
                }
            }
        } while (changed);

        return invariants;
    }

    /// <summary>
    /// Check if an instruction is loop-invariant
    /// </summary>
    private bool IsLoopInvariant(IrInstruction instruction, Loop loop, HashSet<string> knownInvariants)
    {
        // Get all operands used by this instruction
        var usedVars = GetUsedVariables(instruction);

        // All operands must be either:
        // 1. Defined outside the loop, OR
        // 2. Already known to be loop-invariant
        foreach (var varName in usedVars)
        {
            bool definedInLoop = IsDefinedInLoop(varName, loop);
            bool isInvariant = knownInvariants.Contains(varName);

            if (definedInLoop && !isInvariant)
            {
                return false; // This operand changes in the loop
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a variable is defined within the loop
    /// </summary>
    private bool IsDefinedInLoop(string varName, Loop loop)
    {
        foreach (var block in loop.Body)
        {
            foreach (var instruction in block.Instructions)
            {
                string? defName = GetDefinedVariable(instruction);
                if (defName == varName)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Get the variable defined by an instruction (if any)
    /// </summary>
    private string? GetDefinedVariable(IrInstruction instruction)
    {
        return instruction switch
        {
            IrLocalDecl decl => decl.Name,
            IrStore store => store.VariableName,
            IrBinaryOp binOp => binOp.ResultName,
            IrCall call when call.ResultName != null => call.ResultName,
            _ => null
        };
    }

    /// <summary>
    /// Get all variables used by an instruction
    /// </summary>
    private HashSet<string> GetUsedVariables(IrInstruction instruction)
    {
        var used = new HashSet<string>();

        switch (instruction)
        {
            case IrBinaryOp binOp:
                AddIfVariable(binOp.Left, used);
                AddIfVariable(binOp.Right, used);
                break;

            case IrStore store:
                AddIfVariable(store.Value, used);
                break;

            case IrReturn ret when ret.Value != null:
                AddIfVariable(ret.Value, used);
                break;

            case IrCall call:
                foreach (var arg in call.Arguments)
                {
                    AddIfVariable(arg, used);
                }
                break;

            case IrConditionalBranch condBranch:
                AddIfVariable(condBranch.Condition, used);
                break;
        }

        return used;
    }

    /// <summary>
    /// Add variable name to set if value is a variable
    /// </summary>
    private void AddIfVariable(IrValue value, HashSet<string> set)
    {
        if (value is IrVariable variable)
        {
            set.Add(variable.Name);
        }
    }

    /// <summary>
    /// Check if it's safe to hoist an instruction
    /// </summary>
    private bool IsSafeToHoist(IrInstruction instruction, Loop loop)
    {
        // Don't hoist instructions with side effects
        if (instruction is IrCall)
        {
            return false; // Function calls might have side effects
        }

        // Don't hoist stores (they modify state)
        if (instruction is IrStore)
        {
            return false;
        }

        // Don't hoist declarations with initializers
        if (instruction is IrLocalDecl decl && decl.InitialValue != null)
        {
            return false;
        }

        // Don't hoist control flow
        if (instruction is IrBranch || instruction is IrConditionalBranch ||
            instruction is IrReturn || instruction is IrLabel)
        {
            return false;
        }

        // Pure computations (binary ops) are safe to hoist
        if (instruction is IrBinaryOp)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Hoist an instruction to the loop preheader
    /// </summary>
    private void HoistInstruction(IrInstruction instruction, IrBasicBlock fromBlock, Loop loop)
    {
        if (loop.Preheader == null)
        {
            return; // Can't hoist without a preheader
        }

        // Remove from current block
        fromBlock.Instructions.Remove(instruction);

        // Insert before the last instruction in preheader (typically a branch)
        int insertIndex = loop.Preheader.Instructions.Count;

        // Find a good insertion point (before control flow)
        for (int i = loop.Preheader.Instructions.Count - 1; i >= 0; i--)
        {
            var inst = loop.Preheader.Instructions[i];
            if (inst is IrBranch || inst is IrConditionalBranch || inst is IrReturn)
            {
                insertIndex = i;
            }
            else
            {
                break;
            }
        }

        loop.Preheader.Instructions.Insert(insertIndex, instruction);
    }

    /// <summary>
    /// Find a basic block by its label
    /// </summary>
    private IrBasicBlock? FindBlockByLabel(string label)
    {
        foreach (var block in _function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLabel labelInst && labelInst.Name == label)
                {
                    return block;
                }
            }
        }
        return null;
    }
}
