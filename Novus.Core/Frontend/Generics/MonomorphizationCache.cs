using Novus.IR;

namespace Novus.Frontend.Generics;

/// <summary>
/// Cache for tracking instantiated generic types and methods to avoid duplicate code generation
/// Thread-safe for future concurrent compilation support
/// </summary>
public class MonomorphizationCache
{
    // Method instantiation tracking
    private readonly HashSet<string> _instantiatedMethods = new();
    private readonly HashSet<string> _instantiatedFunctions = new();

    // Template storage
    private readonly Dictionary<string, GenericTemplate> _methodTemplates = new();
    private readonly Dictionary<string, GenericTemplate> _functionTemplates = new();

    // Lock for thread-safe operations (future-proofing for parallel compilation)
    private readonly object _lock = new();

    #region Method Instantiation Tracking

    /// <summary>
    /// Mark a method as instantiated
    /// </summary>
    public void MarkMethodInstantiated(string cacheKey)
    {
        lock (_lock)
        {
            _instantiatedMethods.Add(cacheKey);
        }
    }

    /// <summary>
    /// Check if a method has been instantiated
    /// </summary>
    public bool IsMethodInstantiated(string cacheKey)
    {
        lock (_lock)
        {
            return _instantiatedMethods.Contains(cacheKey);
        }
    }

    /// <summary>
    /// Get all instantiated method keys (for debugging/diagnostics)
    /// </summary>
    public IReadOnlyCollection<string> GetInstantiatedMethods()
    {
        lock (_lock)
        {
            return _instantiatedMethods.ToList();
        }
    }

    #endregion

    #region Function Instantiation Tracking

    /// <summary>
    /// Mark a function as instantiated
    /// </summary>
    public void MarkFunctionInstantiated(string cacheKey)
    {
        lock (_lock)
        {
            _instantiatedFunctions.Add(cacheKey);
        }
    }

    /// <summary>
    /// Check if a function has been instantiated
    /// </summary>
    public bool IsFunctionInstantiated(string cacheKey)
    {
        lock (_lock)
        {
            return _instantiatedFunctions.Contains(cacheKey);
        }
    }

    /// <summary>
    /// Get all instantiated function keys (for debugging/diagnostics)
    /// </summary>
    public IReadOnlyCollection<string> GetInstantiatedFunctions()
    {
        lock (_lock)
        {
            return _instantiatedFunctions.ToList();
        }
    }

    #endregion

    #region Template Management

    /// <summary>
    /// Register a generic method template
    /// </summary>
    public void RegisterMethodTemplate(string templateKey, GenericTemplate template)
    {
        lock (_lock)
        {
            _methodTemplates[templateKey] = template;
        }
    }

    /// <summary>
    /// Try to get a method template
    /// </summary>
    public bool TryGetMethodTemplate(string templateKey, out GenericTemplate? template)
    {
        lock (_lock)
        {
            return _methodTemplates.TryGetValue(templateKey, out template);
        }
    }

    /// <summary>
    /// Check if a method template exists
    /// </summary>
    public bool HasMethodTemplate(string templateKey)
    {
        lock (_lock)
        {
            return _methodTemplates.ContainsKey(templateKey);
        }
    }

    /// <summary>
    /// Register a generic function template
    /// </summary>
    public void RegisterFunctionTemplate(string functionName, GenericTemplate template)
    {
        lock (_lock)
        {
            _functionTemplates[functionName] = template;
        }
    }

    /// <summary>
    /// Try to get a function template
    /// </summary>
    public bool TryGetFunctionTemplate(string functionName, out GenericTemplate? template)
    {
        lock (_lock)
        {
            return _functionTemplates.TryGetValue(functionName, out template);
        }
    }

    /// <summary>
    /// Check if a function template exists
    /// </summary>
    public bool HasFunctionTemplate(string functionName)
    {
        lock (_lock)
        {
            return _functionTemplates.ContainsKey(functionName);
        }
    }

    /// <summary>
    /// Get all method template keys (for debugging/diagnostics)
    /// </summary>
    public IReadOnlyCollection<string> GetMethodTemplateKeys()
    {
        lock (_lock)
        {
            return _methodTemplates.Keys.ToList();
        }
    }

    /// <summary>
    /// Get all function template keys (for debugging/diagnostics)
    /// </summary>
    public IReadOnlyCollection<string> GetFunctionTemplateKeys()
    {
        lock (_lock)
        {
            return _functionTemplates.Keys.ToList();
        }
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// Clear all cached instantiations and templates (used for testing)
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _instantiatedMethods.Clear();
            _instantiatedFunctions.Clear();
            _methodTemplates.Clear();
            _functionTemplates.Clear();
        }
    }

    /// <summary>
    /// Get cache statistics (for debugging/diagnostics)
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new CacheStatistics(
                MethodTemplateCount: _methodTemplates.Count,
                FunctionTemplateCount: _functionTemplates.Count,
                InstantiatedMethodCount: _instantiatedMethods.Count,
                InstantiatedFunctionCount: _instantiatedFunctions.Count
            );
        }
    }

    #endregion
}

/// <summary>
/// Statistics about the monomorphization cache (for debugging/diagnostics)
/// </summary>
public record CacheStatistics(
    int MethodTemplateCount,
    int FunctionTemplateCount,
    int InstantiatedMethodCount,
    int InstantiatedFunctionCount
);
