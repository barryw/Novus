using Novus.IR;

namespace Novus.Transforms.Passes;

/// <summary>
/// Inline expansion transformation pass
/// Replaces calls to small functions with the function body
/// </summary>
public class InlineExpansionPass : TransformPassBase
{
    private const int MaxInlineInstructions = 20; // Don't inline functions larger than this
    private const int MaxInlineDepth = 3; // Prevent excessive inlining

    public override string Name => "Inline Expansion";

    public override bool Transform(IrModule module)
    {
        bool changed = false;
        int inlineCounter = 0;

        // Step 1: Identify functions that can be inlined
        var inlinableFunctions = module.Functions
            .Where(f => IsInlinable(f))
            .ToDictionary(f => f.Name);

        if (inlinableFunctions.Count == 0)
        {
            return false; // Nothing to inline
        }

        // Step 2: Inline calls in each function
        foreach (var function in module.Functions.ToList())
        {
            foreach (var block in function.BasicBlocks.ToList())
            {
                for (int i = block.Instructions.Count - 1; i >= 0; i--)
                {
                    if (block.Instructions[i] is IrCall call &&
                        inlinableFunctions.TryGetValue(call.FunctionName, out var targetFunction))
                    {
                        // Inline this call
                        var inlinedInstructions = InlineFunction(targetFunction, call, ref inlineCounter);

                        // Replace the call with inlined instructions
                        block.Instructions.RemoveAt(i);
                        block.Instructions.InsertRange(i, inlinedInstructions);

                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Inline a function call by copying its body with renamed variables
    /// </summary>
    private List<IrInstruction> InlineFunction(IrFunction targetFunction, IrCall call, ref int inlineCounter)
    {
        var result = new List<IrInstruction>();

        // For now, only handle simple single-block functions
        if (targetFunction.BasicBlocks.Count != 1)
        {
            return result; // Can't inline multi-block functions yet
        }

        var sourceBlock = targetFunction.BasicBlocks[0];
        var renameMap = new Dictionary<string, string>();

        // Map parameters to call arguments
        for (int i = 0; i < targetFunction.Parameters.Count && i < call.Arguments.Count; i++)
        {
            var param = targetFunction.Parameters[i];
            var arg = call.Arguments[i];

            // Create a temporary variable for the argument
            var tempName = $"%inline_{inlineCounter++}_{param.Name}";
            renameMap[param.Name] = tempName;

            // Add assignment: temp = argument
            if (arg is IrVariable || arg is IrConstant)
            {
                // Direct assignment
                result.Add(new IrLocalDecl(tempName, param.Type, false, arg));
            }
            else
            {
                // Complex expression - just use it directly in the rename map
                renameMap[param.Name] = ((IrVariable)arg).Name;
            }
        }

        // Copy and rename instructions from the inlined function
        foreach (var instruction in sourceBlock.Instructions)
        {
            if (instruction is IrReturn returnInstr)
            {
                // Handle return - assign to call result if there is one
                if (!string.IsNullOrEmpty(call.ResultName) && returnInstr.Value != null)
                {
                    var renamedValue = RenameValue(returnInstr.Value, renameMap);
                    result.Add(new IrLocalDecl(call.ResultName, call.ReturnType, false, renamedValue));
                }
                // Skip the return instruction itself (we're inlining)
            }
            else
            {
                // Clone and rename other instructions
                var cloned = CloneAndRenameInstruction(instruction, renameMap, ref inlineCounter);
                if (cloned != null)
                {
                    result.Add(cloned);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Clone an instruction and rename all variable references
    /// </summary>
    private IrInstruction? CloneAndRenameInstruction(IrInstruction instruction, Dictionary<string, string> renameMap, ref int inlineCounter)
    {
        if (instruction is IrLocalDecl localDecl)
        {
            var newName = $"%inline_{inlineCounter++}_{localDecl.Name}";
            renameMap[localDecl.Name] = newName;

            var renamedValue = RenameValue(localDecl.InitialValue, renameMap);
            return new IrLocalDecl(newName, localDecl.Type, localDecl.IsMutable, renamedValue);
        }
        else if (instruction is IrBinaryOp binOp)
        {
            var newResultName = string.IsNullOrEmpty(binOp.ResultName)
                ? binOp.ResultName
                : $"%inline_{inlineCounter++}_{binOp.ResultName}";

            if (!string.IsNullOrEmpty(binOp.ResultName))
            {
                renameMap[binOp.ResultName] = newResultName!;
            }

            return new IrBinaryOp(
                newResultName,
                binOp.Operation,
                RenameValue(binOp.Left, renameMap),
                RenameValue(binOp.Right, renameMap),
                binOp.Type
            );
        }
        // Add more instruction types as needed

        return null; // Can't clone this instruction type
    }

    /// <summary>
    /// Rename variable references in a value
    /// </summary>
    private IrValue RenameValue(IrValue value, Dictionary<string, string> renameMap)
    {
        if (value is IrVariable variable && renameMap.TryGetValue(variable.Name, out var newName))
        {
            return new IrVariable(newName, variable.Type);
        }
        return value; // Constants and other values don't need renaming
    }

    /// <summary>
    /// Check if a function is a candidate for inlining
    /// </summary>
    internal bool IsInlinable(IrFunction function)
    {
        // Don't inline entry points
        if (function.Name == "main")
        {
            return false;
        }

        // Count total instructions
        int instructionCount = function.BasicBlocks.Sum(bb => bb.Instructions.Count);
        if (instructionCount > MaxInlineInstructions)
        {
            return false;
        }

        // Don't inline functions with complex control flow (for now)
        if (function.BasicBlocks.Count > 3)
        {
            return false;
        }

        // Don't inline recursive functions
        if (IsRecursive(function))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if a function is recursive (calls itself)
    /// </summary>
    internal bool IsRecursive(IrFunction function)
    {
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCall call && call.FunctionName == function.Name)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
