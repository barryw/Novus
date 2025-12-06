using System.Text;
using Novus.Diagnostics;

namespace Novus.Preprocessing;

/// <summary>
/// Simple preprocessor for conditional compilation.
/// Handles #if, #elif/#elsif, #else, #endif directives.
/// </summary>
public class Preprocessor
{
    private readonly Dictionary<string, bool> _constants;
    private readonly DiagnosticBag _diagnostics;
    private readonly string _filePath;

    public Preprocessor(Dictionary<string, bool> constants, DiagnosticBag diagnostics, string filePath)
    {
        _constants = constants;
        _diagnostics = diagnostics;
        _filePath = filePath;
    }

    /// <summary>
    /// Process preprocessor directives in source code.
    /// Removes lines inside false #if blocks.
    /// Preserves line numbers by replacing removed lines with blank lines.
    /// </summary>
    public string Process(string source)
    {
        var lines = source.Split('\n');
        var output = new StringBuilder();
        var stack = new Stack<IfState>();
        var currentState = new IfState { Active = true, AnyBranchTaken = false };

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;
            var trimmed = line.TrimStart();

            // Check if this is a preprocessor directive
            // Note: #[ is attribute syntax, not a preprocessor directive
            if (trimmed.StartsWith("#") && !trimmed.StartsWith("#["))
            {
                var directive = ParseDirective(trimmed, lineNumber);
                if (directive == null)
                {
                    // Error already reported, skip this line
                    output.AppendLine();
                    continue;
                }

                switch (directive.Type)
                {
                    case DirectiveType.If:
                        // Push current state onto stack
                        stack.Push(currentState);

                        // Evaluate the condition
                        bool conditionValue = EvaluateConstant(directive.Constant!, lineNumber);

                        // New state: active if parent was active AND condition is true
                        currentState = new IfState
                        {
                            Active = currentState.Active && conditionValue,
                            AnyBranchTaken = conditionValue
                        };
                        break;

                    case DirectiveType.Elif:
                        if (stack.Count == 0)
                        {
                            ReportError("E9001", $"#elif without matching #if", lineNumber);
                            output.AppendLine();
                            continue;
                        }

                        // Evaluate the condition
                        bool elifCondition = EvaluateConstant(directive.Constant!, lineNumber);

                        // Parent state
                        var parentState = stack.Peek();

                        // Active if: parent is active AND no previous branch taken AND this condition is true
                        bool elifActive = parentState.Active && !currentState.AnyBranchTaken && elifCondition;

                        currentState = new IfState
                        {
                            Active = elifActive,
                            AnyBranchTaken = currentState.AnyBranchTaken || elifActive
                        };
                        break;

                    case DirectiveType.Else:
                        if (stack.Count == 0)
                        {
                            ReportError("E9002", $"#else without matching #if", lineNumber);
                            output.AppendLine();
                            continue;
                        }

                        // Parent state
                        var parentStateElse = stack.Peek();

                        // Active if: parent is active AND no previous branch was taken
                        currentState = new IfState
                        {
                            Active = parentStateElse.Active && !currentState.AnyBranchTaken,
                            AnyBranchTaken = true  // Else is always the last branch
                        };
                        break;

                    case DirectiveType.Endif:
                        if (stack.Count == 0)
                        {
                            ReportError("E9003", $"#endif without matching #if", lineNumber);
                            output.AppendLine();
                            continue;
                        }

                        // Pop state
                        currentState = stack.Pop();
                        break;
                }

                // Replace directive line with blank line (preserve line numbers)
                output.AppendLine();
            }
            else
            {
                // Regular code line
                if (currentState.Active)
                {
                    // Keep this line
                    output.AppendLine(line);
                }
                else
                {
                    // Skip this line (replace with blank line to preserve line numbers)
                    output.AppendLine();
                }
            }
        }

        // Check for unmatched #if directives
        if (stack.Count > 0)
        {
            ReportError("E9004",
                $"unmatched #if directive ({stack.Count} unclosed block{(stack.Count > 1 ? "s" : "")})",
                lines.Length);
        }

        return output.ToString();
    }

    private Directive? ParseDirective(string line, int lineNumber)
    {
        var parts = line.TrimStart().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return null;

        var directiveName = parts[0].ToLowerInvariant();

        switch (directiveName)
        {
            case "#if":
                if (parts.Length != 2)
                {
                    ReportError("E9005", $"#if requires exactly one constant name", lineNumber);
                    return null;
                }
                return new Directive { Type = DirectiveType.If, Constant = parts[1] };

            case "#elif":
            case "#elsif": // alias for #elif (Ruby-style)
                if (parts.Length != 2)
                {
                    ReportError("E9006", $"#elif/#elsif requires exactly one constant name", lineNumber);
                    return null;
                }
                return new Directive { Type = DirectiveType.Elif, Constant = parts[1] };

            case "#else":
                if (parts.Length != 1)
                {
                    ReportError("E9007", $"#else takes no arguments", lineNumber);
                    return null;
                }
                return new Directive { Type = DirectiveType.Else };

            case "#endif":
                if (parts.Length != 1)
                {
                    ReportError("E9008", $"#endif takes no arguments", lineNumber);
                    return null;
                }
                return new Directive { Type = DirectiveType.Endif };

            default:
                ReportError("E9009", $"unknown preprocessor directive '{parts[0]}'", lineNumber);
                return null;
        }
    }

    private bool EvaluateConstant(string constantName, int lineNumber)
    {
        if (_constants.TryGetValue(constantName, out bool value))
        {
            return value;
        }

        // Unknown constant - report error and return false
        var availableConstants = string.Join(", ", _constants.Keys.OrderBy(k => k));
        ReportError("E9010",
            $"undefined preprocessor constant '{constantName}'",
            lineNumber,
            helpText: $"Available constants: {availableConstants}");

        return false;
    }

    private void ReportError(string code, string message, int lineNumber, string? helpText = null)
    {
        // SourceLocation(filePath, line, column, length, sourceLine)
        var location = new SourceLocation(_filePath, lineNumber, 1, 1, "");
        var helpTexts = helpText != null ? new List<string> { helpText } : null;
        _diagnostics.ReportError(code, message, location, helpTexts);
    }

    private class Directive
    {
        public DirectiveType Type { get; set; }
        public string? Constant { get; set; }
    }

    private enum DirectiveType
    {
        If,
        Elif,
        Else,
        Endif
    }

    private class IfState
    {
        /// <summary>
        /// Whether code in this block should be included
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Whether any branch (#if or #elif) has been taken
        /// Used to determine if #else should be active
        /// </summary>
        public bool AnyBranchTaken { get; set; }
    }
}
