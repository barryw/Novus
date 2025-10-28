using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// DBcc Loop Optimization Pass
///
/// Detects counted loops that can use the 68000 DBcc instruction.
/// DBcc (Decrement and Branch conditionally) combines:
/// - Decrementing a counter by 1
/// - Testing if counter == -1
/// - Branching if not -1
///
/// Pattern detected:
/// - Loop with a 16-bit counter variable
/// - Counter is decremented by 1 each iteration
/// - Loop continues while counter >= 0 (or != -1)
///
/// Transformation:
/// The pass marks eligible loops by reordering instructions to make
/// the pattern obvious to the code generator.
/// </summary>
public class DBccLoopOptimizationPass : FunctionPassBase
{
    public override string Name => "DBcc Loop Optimization";

    public override bool RunOnFunction(IrFunction function)
    {
        if (function.BasicBlocks.Count == 0) return false;

        var cfg = new ControlFlowGraph(function);
        var detector = new LoopDetector(cfg);
        var loops = detector.DetectLoops();

        if (loops.Count == 0) return false;

        bool changed = false;

        // Process loops from innermost to outermost
        foreach (var loop in loops)
        {
            if (TryOptimizeWithDBcc(loop, function))
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Try to optimize a loop to use DBcc instruction
    /// </summary>
    private bool TryOptimizeWithDBcc(Loop loop, IrFunction function)
    {
        // Find the loop's back edge(s)
        if (loop.BackEdges.Count != 1) return false; // DBcc works best with single back edge

        var backEdge = loop.BackEdges[0];
        var loopBody = backEdge.Source.Block;
        var loopHeader = loop.Header.Block;

        if (loopBody == null || loopHeader == null) return false;

        // Look for the pattern:
        // 1. Header has a conditional branch checking counter >= 0
        // 2. Body decrements the counter by 1
        // 3. Body branches back to header

        // Find the decrement operation in loop body
        var counterInfo = FindCounterDecrement(loopBody);
        if (counterInfo == null) return false;

        var (counterVar, decrementInst, decrementIndex) = counterInfo.Value;

        // Check if the header tests this counter
        if (!IsCounterTest(loopHeader, counterVar)) return false;

        // Check that counter is 16-bit (requirement for DBcc)
        if (!Is16BitInteger(counterVar)) return false;

        // We found a DBcc-eligible loop!
        // Apply loop rotation: move the counter test into the loop body
        // This reduces one branch and makes the pattern clearer for code generation
        return RotateLoop(loop, loopHeader, loopBody, counterVar);
    }

    /// <summary>
    /// Find a counter decrement operation (counter = counter - 1)
    /// </summary>
    private (string counterVar, IrInstruction decrementInst, int index)? FindCounterDecrement(IrBasicBlock block)
    {
        for (int i = 0; i < block.Instructions.Count; i++)
        {
            var inst = block.Instructions[i];

            // Look for: counter = counter - 1
            if (inst is IrStore store &&
                store.Value is IrVariable sourceVar)
            {
                // Check if the source is a subtraction by 1
                var defInst = FindDefinition(block, sourceVar.Name, i);
                if (defInst is IrBinaryOp binOp &&
                    binOp.ResultName == sourceVar.Name &&
                    binOp.Operation == IrBinaryOp.OpKind.Sub &&
                    binOp.Left is IrVariable leftVar &&
                    leftVar.Name == store.VariableName &&
                    binOp.Right is IrConstant constant &&
                    constant.Value == 1)
                {
                    return (store.VariableName, inst, i);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Find the instruction that defines a variable
    /// </summary>
    private IrInstruction? FindDefinition(IrBasicBlock block, string varName, int beforeIndex)
    {
        // Look backwards from beforeIndex
        for (int i = beforeIndex - 1; i >= 0; i--)
        {
            var inst = block.Instructions[i];
            if (inst is IrBinaryOp binOp && binOp.ResultName == varName)
            {
                return inst;
            }
            if (inst is IrCall call && call.ResultName == varName)
            {
                return inst;
            }
        }
        return null;
    }

    /// <summary>
    /// Check if the header tests the counter variable
    /// </summary>
    private bool IsCounterTest(IrBasicBlock header, string counterVar)
    {
        // Look for conditional branch that tests the counter
        foreach (var inst in header.Instructions)
        {
            if (inst is IrConditionalBranch condBr &&
                condBr.Condition is IrVariable condVar)
            {
                // Find the condition definition
                var condDef = FindConditionDefinition(header, condVar.Name);
                if (condDef != null && UsesVariable(condDef, counterVar))
                {
                    // Check if it's comparing to 0 or -1
                    if (condDef is IrBinaryOp binOp &&
                        (binOp.Operation == IrBinaryOp.OpKind.Ge ||
                         binOp.Operation == IrBinaryOp.OpKind.Gt ||
                         binOp.Operation == IrBinaryOp.OpKind.Ne))
                    {
                        // Check if comparing to 0 or -1
                        if (binOp.Right is IrConstant c && (c.Value == 0 || c.Value == -1))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Find the instruction that defines the condition variable
    /// </summary>
    private IrInstruction? FindConditionDefinition(IrBasicBlock block, string condVar)
    {
        foreach (var inst in block.Instructions)
        {
            if (inst is IrBinaryOp binOp && binOp.ResultName == condVar)
            {
                return inst;
            }
        }
        return null;
    }

    /// <summary>
    /// Check if an instruction uses a variable
    /// </summary>
    private bool UsesVariable(IrInstruction instruction, string varName)
    {
        if (instruction is IrBinaryOp binOp)
        {
            return (binOp.Left is IrVariable leftVar && leftVar.Name == varName) ||
                   (binOp.Right is IrVariable rightVar && rightVar.Name == varName);
        }
        return false;
    }

    /// <summary>
    /// Check if a variable is a 16-bit integer
    /// </summary>
    private bool Is16BitInteger(string varName)
    {
        // For now, assume we can use DBcc on any integer
        // TODO: Add type checking when type information is available in IR
        return true;
    }

    /// <summary>
    /// Rotate the loop: move the condition test from header to end of body
    /// This transformation changes:
    ///   header: if (cond) goto body else exit
    ///   body: ... decrement ... goto header
    /// to:
    ///   body: ... decrement ... if (cond) goto body else exit
    /// </summary>
    private bool RotateLoop(Loop loop, IrBasicBlock header, IrBasicBlock body, string counterVar)
    {
        // Find the conditional branch in the header
        IrConditionalBranch? headerBranch = null;
        IrInstruction? conditionDef = null;

        for (int i = header.Instructions.Count - 1; i >= 0; i--)
        {
            var inst = header.Instructions[i];
            if (inst is IrConditionalBranch condBr)
            {
                headerBranch = condBr;

                // Find the condition definition
                if (condBr.Condition is IrVariable condVar)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (header.Instructions[j] is IrBinaryOp binOp &&
                            binOp.ResultName == condVar.Name)
                        {
                            conditionDef = binOp;
                            header.Instructions.RemoveAt(j);
                            i--; // Adjust index after removal
                            break;
                        }
                    }
                }
                break;
            }
        }

        if (headerBranch == null) return false;

        // Remove the conditional branch from header
        header.Instructions.Remove(headerBranch);

        // Replace with unconditional branch to body
        header.Instructions.Add(new IrBranch(body.Label));

        // Remove the unconditional branch from body (the back edge)
        var lastInst = body.Instructions.LastOrDefault();
        if (lastInst is IrBranch)
        {
            body.Instructions.RemoveAt(body.Instructions.Count - 1);
        }

        // Add condition definition to end of body (if it exists)
        if (conditionDef != null)
        {
            body.Instructions.Add(conditionDef);
        }

        // Add conditional branch to end of body
        body.Instructions.Add(headerBranch);

        return true;
    }
}
