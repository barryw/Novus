using Novus.SemanticAnalysis;

namespace Novus.LanguageServer;

/// <summary>
/// Extension methods for SemanticAnalyzer to provide language server features
/// </summary>
public static class SemanticAnalyzerExtensions
{
    /// <summary>
    /// Get symbol table with usage tracking for SSA diagnostics
    /// </summary>
    public static VariableUsageTable GetSymbolTable(this SemanticAnalyzer analyzer)
    {
        // For now, return an empty symbol table
        // In the future, we'll extract this from the semantic analyzer's internal state
        return new VariableUsageTable();
    }

    /// <summary>
    /// Get control flow information for dead code detection
    /// </summary>
    public static ControlFlowInfo GetControlFlowInfo(this SemanticAnalyzer analyzer)
    {
        // For now, return empty control flow info
        // In the future, we'll perform actual control flow analysis
        return new ControlFlowInfo();
    }

    /// <summary>
    /// Get data flow information for redundant assignment detection
    /// </summary>
    public static DataFlowInfo GetDataFlowInfo(this SemanticAnalyzer analyzer)
    {
        // For now, return empty data flow info
        // In the future, we'll perform actual data flow analysis
        return new DataFlowInfo();
    }
}
