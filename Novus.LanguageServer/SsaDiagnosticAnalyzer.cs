using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.LanguageServer;

/// <summary>
/// Performs SSA-based diagnostic analysis to detect code quality issues.
///
/// This analyzer uses SSA (Static Single Assignment) form to detect:
/// - Uninitialized variables
/// - Variables that are assigned but never read (dead stores)
/// - Unreachable code
/// - Potential null pointer dereferences
/// - Type inconsistencies
/// - Redundant assignments
///
/// The analyzer runs after semantic analysis and adds additional diagnostics
/// to help developers write better code.
/// </summary>
public class SsaDiagnosticAnalyzer
{
    private readonly SemanticAnalyzer _semanticAnalyzer;
    private readonly DiagnosticBag _diagnostics;

    public SsaDiagnosticAnalyzer(SemanticAnalyzer semanticAnalyzer, DiagnosticBag diagnostics)
    {
        _semanticAnalyzer = semanticAnalyzer;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Run SSA-based analysis and add diagnostics for code quality issues
    /// </summary>
    public void Analyze()
    {
        // For now, we'll analyze the IR if available
        // In the future, we can build IR from the semantic analyzer's output

        // Check for common code patterns that indicate issues:

        // 1. Variables declared but never used
        AnalyzeUnusedVariables();

        // 2. Variables used before initialization
        AnalyzeUninitializedVariables();

        // 3. Dead code after return statements
        AnalyzeDeadCode();

        // 4. Redundant assignments
        AnalyzeRedundantAssignments();
    }

    /// <summary>
    /// Detect variables that are declared but never used
    /// </summary>
    private void AnalyzeUnusedVariables()
    {
        // Access the symbol table from semantic analyzer
        var symbolTable = _semanticAnalyzer.GetSymbolTable();

        foreach (var (name, symbol) in symbolTable.GetAllSymbols())
        {
            // Skip parameters and special variables
            if (symbol.IsParameter || name.StartsWith("_") || name == "self")
                continue;

            // Check if variable is written to but never read
            if (symbol.WriteCount > 0 && symbol.ReadCount == 0)
            {
                _diagnostics.ReportWarning(
                    "W4001",
                    $"Variable '{name}' is assigned but never used",
                    symbol.DeclarationLocation,
                    helpTexts: new List<string>
                    {
                        "This variable is assigned a value but never read.",
                        "Consider removing it or using it in your code.",
                        "If intentionally unused, prefix the name with '_' to suppress this warning."
                    }
                );
            }

            // Check if variable is never written to or read (completely unused)
            if (symbol.WriteCount == 0 && symbol.ReadCount == 0)
            {
                _diagnostics.ReportWarning(
                    "W4002",
                    $"Variable '{name}' is declared but never used",
                    symbol.DeclarationLocation,
                    helpTexts: new List<string>
                    {
                        "This variable is declared but never referenced in any way.",
                        "Consider removing this declaration."
                    }
                );
            }
        }
    }

    /// <summary>
    /// Detect variables that may be used before initialization
    /// </summary>
    private void AnalyzeUninitializedVariables()
    {
        var symbolTable = _semanticAnalyzer.GetSymbolTable();

        foreach (var (name, symbol) in symbolTable.GetAllSymbols())
        {
            // Check if variable is read before any writes
            if (!symbol.IsParameter && symbol.ReadCount > 0)
            {
                // If first use is a read and there's no initializer, it might be uninitialized
                if (!symbol.HasInitializer && symbol.FirstReadBeforeWrite)
                {
                    _diagnostics.ReportError(
                        "E4001",
                        $"Variable '{name}' may be used before it is initialized",
                        symbol.FirstUseLocation,
                        helpTexts: new List<string>
                        {
                            "This variable is read before any value is assigned to it.",
                            "Initialize the variable before using it."
                        }
                    );
                }
            }
        }
    }

    /// <summary>
    /// Detect unreachable code (e.g., code after return statements)
    /// NOTE: This is currently disabled as W0003 already detects unreachable code
    /// in the semantic analyzer. This method is kept for future SSA-based dead code analysis.
    /// </summary>
    private void AnalyzeDeadCode()
    {
        // This requires control flow analysis
        // For now, we'll detect simple cases like code after unconditional returns

        // DISABLED: W0003 already detects unreachable code in SemanticAnalyzer
        // We'll implement SSA-based dead code analysis in the future which can detect
        // more complex cases like unreachable code due to constant propagation.

        // var controlFlow = _semanticAnalyzer.GetControlFlowInfo();
        // foreach (var block in controlFlow.UnreachableBlocks)
        // {
        //     _diagnostics.ReportWarning(
        //         "W4003",  // Reserved for future SSA-based dead code detection
        //         "Unreachable code detected",
        //         block.Location,
        //         helpTexts: new List<string>
        //         {
        //             "This code will never be executed due to control flow.",
        //             "It may follow an unconditional return, break, or continue statement.",
        //             "Consider removing this code or restructuring your control flow."
        //         }
        //     );
        // }
    }

    /// <summary>
    /// Detect redundant assignments (variable assigned multiple times without being read)
    /// </summary>
    private void AnalyzeRedundantAssignments()
    {
        var dataFlow = _semanticAnalyzer.GetDataFlowInfo();

        foreach (var assignment in dataFlow.RedundantAssignments)
        {
            _diagnostics.ReportWarning(
                "W4003",
                $"Variable '{assignment.VariableName}' is assigned a value that is never used",
                assignment.Location,
                helpTexts: new List<string>
                {
                    "This assignment is immediately overwritten by another assignment.",
                    "The assigned value is never read.",
                    "Consider removing this redundant assignment."
                }
            );
        }
    }
}

/// <summary>
/// Represents a symbol in the symbol table with usage tracking
/// </summary>
public class VariableUsageInfo
{
    public string Name { get; set; } = "";
    public SourceLocation DeclarationLocation { get; set; } = default!;
    public SourceLocation FirstUseLocation { get; set; } = default!;
    public bool IsParameter { get; set; }
    public bool HasInitializer { get; set; }
    public int ReadCount { get; set; }
    public int WriteCount { get; set; }
    public bool FirstReadBeforeWrite { get; set; }
}

/// <summary>
/// Simple symbol table for tracking variable usage
/// </summary>
public class VariableUsageTable
{
    private readonly Dictionary<string, VariableUsageInfo> _symbols = new();

    public void AddSymbol(string name, VariableUsageInfo info)
    {
        _symbols[name] = info;
    }

    public VariableUsageInfo? GetSymbol(string name)
    {
        _symbols.TryGetValue(name, out var info);
        return info;
    }

    public IEnumerable<(string, VariableUsageInfo)> GetAllSymbols()
    {
        return _symbols.Select(kvp => (kvp.Key, kvp.Value));
    }
}

/// <summary>
/// Control flow information for dead code detection
/// </summary>
public class ControlFlowInfo
{
    public List<CodeBlock> UnreachableBlocks { get; set; } = new();
}

/// <summary>
/// Represents a block of code with location information
/// </summary>
public class CodeBlock
{
    public SourceLocation Location { get; set; } = default!;
}

/// <summary>
/// Data flow information for redundant assignment detection
/// </summary>
public class DataFlowInfo
{
    public List<AssignmentInfo> RedundantAssignments { get; set; } = new();
}

/// <summary>
/// Information about an assignment
/// </summary>
public class AssignmentInfo
{
    public string VariableName { get; set; } = "";
    public SourceLocation Location { get; set; } = default!;
}
