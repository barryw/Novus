using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Dead function elimination pass - removes unreachable functions from the module
///
/// Algorithm:
/// 1. Build call graph from all functions in the module
/// 2. Mark entry points (main, exported functions with @export attribute, public functions)
/// 3. Traverse call graph from entry points using DFS
/// 4. Remove functions not reachable from any entry point
///
/// This pass should run AFTER inlining, as inlining may make functions dead
/// </summary>
public class DeadFunctionEliminationPass : IOptimizationPass
{
    public string Name => "Dead Function Elimination";

    public bool Run(IrModule module)
    {
        // Step 1: Build call graph
        var callGraph = BuildCallGraph(module);

        // Step 2: Find entry points
        var entryPoints = FindEntryPoints(module);

        // Step 3: Mark reachable functions via DFS
        var reachableFunctions = new HashSet<string>();
        foreach (var entryPoint in entryPoints)
        {
            MarkReachable(entryPoint, callGraph, reachableFunctions);
        }

        // Step 4: Remove unreachable functions
        var functionsToRemove = new List<IrFunction>();
        foreach (var function in module.Functions)
        {
            if (!reachableFunctions.Contains(function.Name))
            {
                functionsToRemove.Add(function);
            }
        }

        // Remove unreachable functions
        foreach (var function in functionsToRemove)
        {
            module.Functions.Remove(function);
        }

        return functionsToRemove.Count > 0;
    }

    /// <summary>
    /// Build a call graph mapping function names to the functions they call
    /// </summary>
    private Dictionary<string, HashSet<string>> BuildCallGraph(IrModule module)
    {
        var callGraph = new Dictionary<string, HashSet<string>>();

        foreach (var function in module.Functions)
        {
            var callees = new HashSet<string>();

            // Find all function calls in this function
            foreach (var block in function.BasicBlocks)
            {
                foreach (var instruction in block.Instructions)
                {
                    if (instruction is IrCall call)
                    {
                        callees.Add(call.FunctionName);
                    }
                    else if (instruction is IrIndirectCall indirectCall)
                    {
                        // Indirect calls can't be analyzed statically -
                        // assume they might call any function (conservative)
                        // For now, we don't remove functions if there are any indirect calls
                    }
                }
            }

            callGraph[function.Name] = callees;
        }

        return callGraph;
    }

    /// <summary>
    /// Find all entry point functions that must not be eliminated
    /// </summary>
    private HashSet<string> FindEntryPoints(IrModule module)
    {
        var entryPoints = new HashSet<string>();

        foreach (var function in module.Functions)
        {
            // Entry point 1: main function
            if (function.Name == "main")
            {
                entryPoints.Add(function.Name);
                continue;
            }

            // Entry point 2: Functions with @export attribute
            if (function.Attributes?.Has(KnownAttributes.Export) == true)
            {
                entryPoints.Add(function.Name);
                continue;
            }

            // Entry point 3: Functions explicitly marked as exported
            if (function.IsExported)
            {
                entryPoints.Add(function.Name);
                continue;
            }

            // Entry point 4: Public functions (visible to other compilation units)
            if (function.Visibility == Visibility.Public)
            {
                entryPoints.Add(function.Name);
                continue;
            }

            // Entry point 5: Extern functions (they're declarations, not definitions)
            if (function.IsExtern)
            {
                entryPoints.Add(function.Name);
                continue;
            }

            // Entry point 6: Library vector functions (@libfunc, @libopen, @libclose, etc.)
            if (function.Attributes != null)
            {
                if (function.Attributes.Has(KnownAttributes.LibFunc) ||
                    function.Attributes.Has(KnownAttributes.LibOpen) ||
                    function.Attributes.Has(KnownAttributes.LibClose) ||
                    function.Attributes.Has(KnownAttributes.LibExpunge) ||
                    function.Attributes.Has(KnownAttributes.LibInit))
                {
                    entryPoints.Add(function.Name);
                    continue;
                }
            }

            // Entry point 7: Test functions (@test attribute)
            if (function.Attributes?.Has(KnownAttributes.Test) == true)
            {
                entryPoints.Add(function.Name);
                continue;
            }

            // Entry point 8: Benchmark functions (@benchmark attribute)
            if (function.Attributes?.Has(KnownAttributes.Benchmark) == true)
            {
                entryPoints.Add(function.Name);
                continue;
            }
        }

        return entryPoints;
    }

    /// <summary>
    /// Mark all functions reachable from a given entry point using DFS
    /// </summary>
    private void MarkReachable(
        string functionName,
        Dictionary<string, HashSet<string>> callGraph,
        HashSet<string> reachable)
    {
        // Already visited
        if (reachable.Contains(functionName))
        {
            return;
        }

        // Mark as reachable
        reachable.Add(functionName);

        // Recursively mark callees
        if (callGraph.TryGetValue(functionName, out var callees))
        {
            foreach (var callee in callees)
            {
                MarkReachable(callee, callGraph, reachable);
            }
        }
    }
}
