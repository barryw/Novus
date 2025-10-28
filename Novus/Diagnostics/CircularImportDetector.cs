using System.Text;

namespace Novus.Diagnostics;

/// <summary>
/// Detects and reports circular module dependencies during compilation
/// Tracks both the active import chain and the full dependency graph
/// </summary>
public class CircularImportDetector
{
    private readonly Stack<string> _importChain = new();
    private readonly Dictionary<string, List<string>> _dependencyGraph = new();
    private readonly DiagnosticBag _diagnostics;

    public CircularImportDetector(DiagnosticBag diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Enter a module for processing. Returns false if a cycle is detected.
    /// </summary>
    /// <param name="modulePath">Absolute path to the module being entered</param>
    /// <returns>True if no cycle detected, false if cycle found</returns>
    public bool EnterModule(string modulePath)
    {
        // Check if this module is already in the current import chain
        if (_importChain.Contains(modulePath))
        {
            ReportCircularDependency(modulePath);
            return false;
        }

        _importChain.Push(modulePath);

        // Initialize dependency list for this module if needed
        if (!_dependencyGraph.ContainsKey(modulePath))
        {
            _dependencyGraph[modulePath] = new List<string>();
        }

        return true;
    }

    /// <summary>
    /// Record that currentModule depends on importedModule
    /// Checks for cycles in the dependency graph
    /// </summary>
    public bool RecordDependency(string currentModule, string importedModule)
    {
        // Add the dependency
        if (!_dependencyGraph.ContainsKey(currentModule))
        {
            _dependencyGraph[currentModule] = new List<string>();
        }

        if (!_dependencyGraph[currentModule].Contains(importedModule))
        {
            _dependencyGraph[currentModule].Add(importedModule);
        }

        // Check if this creates a cycle
        var visited = new HashSet<string>();
        var recStack = new HashSet<string>();
        List<string>? cycle = null;

        if (HasCycleDFS(importedModule, visited, recStack, new List<string> { currentModule, importedModule }, out cycle))
        {
            ReportCircularDependencyFromPath(cycle!);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Depth-first search to detect cycles in the dependency graph
    /// </summary>
    private bool HasCycleDFS(string module, HashSet<string> visited, HashSet<string> recStack,
        List<string> path, out List<string>? cycle)
    {
        cycle = null;

        if (!_dependencyGraph.ContainsKey(module))
        {
            return false;
        }

        visited.Add(module);
        recStack.Add(module);

        foreach (var dependency in _dependencyGraph[module])
        {
            if (!visited.Contains(dependency))
            {
                var newPath = new List<string>(path) { dependency };
                if (HasCycleDFS(dependency, visited, recStack, newPath, out cycle))
                {
                    return true;
                }
            }
            else if (recStack.Contains(dependency))
            {
                // Found a cycle - build the cycle path
                cycle = new List<string>(path);

                // Find where the cycle starts in the path
                var cycleStart = cycle.IndexOf(dependency);
                if (cycleStart >= 0)
                {
                    // Extract only the cycle portion
                    cycle = cycle.Skip(cycleStart).ToList();
                }

                return true;
            }
        }

        recStack.Remove(module);
        return false;
    }

    /// <summary>
    /// Exit the current module after processing
    /// </summary>
    public void ExitModule()
    {
        if (_importChain.Count > 0)
        {
            _importChain.Pop();
        }
    }

    /// <summary>
    /// Reports a circular dependency error with the full import chain
    /// </summary>
    private void ReportCircularDependency(string cyclicModule)
    {
        // Build the import chain for display
        var chain = new StringBuilder();
        var chainList = _importChain.Reverse().ToList();

        for (int i = 0; i < chainList.Count; i++)
        {
            var moduleName = Path.GetFileName(chainList[i]);
            chain.Append("            ");
            chain.AppendLine(moduleName);
            if (i < chainList.Count - 1)
            {
                chain.Append("         -> ");
            }
        }

        // Add the cyclic module that closes the loop
        chain.Append("         -> ");
        chain.AppendLine($"{Path.GetFileName(cyclicModule)} (cycle)");

        // Create a simple source location for the error
        var location = new SourceLocation(
            cyclicModule,
            1,
            1,
            1,
            "from ... import ..."
        );

        var message = "circular module dependency detected";
        var helpTexts = new List<string>
        {
            "import chain:",
            chain.ToString().TrimEnd()
        };

        _diagnostics.ReportError("E0050", message, location, helpTexts);
    }

    /// <summary>
    /// Reports a circular dependency using a detected cycle path
    /// </summary>
    private void ReportCircularDependencyFromPath(List<string> cyclePath)
    {
        var chain = new StringBuilder();

        for (int i = 0; i < cyclePath.Count; i++)
        {
            var moduleName = Path.GetFileName(cyclePath[i]);
            chain.Append("            ");
            chain.AppendLine(moduleName);
            if (i < cyclePath.Count - 1)
            {
                chain.Append("         -> ");
            }
        }

        // Close the loop by showing the first module again
        chain.Append("         -> ");
        chain.AppendLine($"{Path.GetFileName(cyclePath[0])} (cycle)");

        var location = new SourceLocation(
            cyclePath[0],
            1,
            1,
            1,
            "from ... import ..."
        );

        var message = "circular module dependency detected";
        var helpTexts = new List<string>
        {
            "import chain:",
            chain.ToString().TrimEnd()
        };

        _diagnostics.ReportError("E0050", message, location, helpTexts);
    }

    /// <summary>
    /// Get the current import chain for debugging
    /// </summary>
    public IEnumerable<string> GetCurrentChain()
    {
        return _importChain.Reverse();
    }

    /// <summary>
    /// Check if we're currently processing any modules
    /// </summary>
    public bool IsProcessing => _importChain.Count > 0;

    /// <summary>
    /// Get the depth of the current import chain
    /// </summary>
    public int Depth => _importChain.Count;
}
