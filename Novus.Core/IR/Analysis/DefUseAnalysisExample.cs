namespace Novus.IR.Analysis;

/// <summary>
/// Example usage of DefUseAnalysis for testing and documentation purposes.
/// Shows how to use the analysis to find dead code, perform copy propagation, etc.
/// </summary>
public static class DefUseAnalysisExample
{
    /// <summary>
    /// Example: Find all unused variables in a function
    /// </summary>
    public static List<string> FindUnusedVariables(IrFunction function)
    {
        var analysis = DefUseAnalysis.Analyze(function);
        var unusedVars = new List<string>();

        // Check each basic block for variable definitions
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                var definedVars = DefUseAnalysis.GetDefinedVariables(instruction);
                foreach (var varName in definedVars)
                {
                    if (!analysis.IsUsed(varName))
                    {
                        unusedVars.Add(varName);
                    }
                }
            }
        }

        return unusedVars;
    }

    /// <summary>
    /// Example: Find all instructions that use a specific variable
    /// </summary>
    public static List<IrInstruction> FindUsesOf(IrFunction function, string variableName)
    {
        var analysis = DefUseAnalysis.Analyze(function);
        return analysis.GetUses(variableName);
    }

    /// <summary>
    /// Example: Check if a variable is defined before use
    /// </summary>
    public static bool IsDefinedBeforeUse(IrFunction function, string variableName, IrInstruction useInstruction)
    {
        var analysis = DefUseAnalysis.Analyze(function);
        var definition = analysis.GetDefinition(variableName);

        if (definition == null)
        {
            return false; // Variable never defined
        }

        // In a more sophisticated implementation, you'd check if the definition
        // dominates the use in the control flow graph
        return true;
    }

    /// <summary>
    /// Example: Perform simple copy propagation
    /// Replace all uses of "x" with "y" if we have "x = y" and y is a variable
    /// </summary>
    public static void SimpleCopyPropagation(IrFunction function)
    {
        var analysis = DefUseAnalysis.Analyze(function);

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                // Look for pattern: x = y (where y is a variable)
                if (instruction is IrStore store && store.Value is IrVariable sourceVar)
                {
                    // Replace all uses of the destination with the source
                    analysis.ReplaceAllUses(store.VariableName, sourceVar);
                }
            }
        }
    }

    /// <summary>
    /// Example: Count how many times each variable is used
    /// </summary>
    public static Dictionary<string, int> CountVariableUses(IrFunction function)
    {
        var analysis = DefUseAnalysis.Analyze(function);
        var useCounts = new Dictionary<string, int>();

        // Get all defined variables
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                var definedVars = DefUseAnalysis.GetDefinedVariables(instruction);
                foreach (var varName in definedVars)
                {
                    var uses = analysis.GetUses(varName);
                    useCounts[varName] = uses.Count;
                }
            }
        }

        return useCounts;
    }

    /// <summary>
    /// Example: Find variables that are used exactly once (candidates for inlining)
    /// </summary>
    public static List<string> FindSingleUseVariables(IrFunction function)
    {
        var useCounts = CountVariableUses(function);
        return useCounts.Where(kvp => kvp.Value == 1).Select(kvp => kvp.Key).ToList();
    }
}
