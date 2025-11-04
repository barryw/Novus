using System.Text;

namespace Novus.LanguageServer;

/// <summary>
/// Extracts /// documentation comments from Novus source code.
/// Handles both single-line and multi-line doc comments.
/// </summary>
public static class DocCommentExtractor
{
    /// <summary>
    /// Extracts documentation comments that appear before a given line number.
    /// Returns the combined documentation as Markdown-formatted text.
    /// </summary>
    /// <param name="sourceText">The full source code text</param>
    /// <param name="targetLine">The 1-based line number of the declaration (e.g., function, struct)</param>
    /// <returns>Documentation string or null if no doc comments found</returns>
    public static string? ExtractDocComment(string sourceText, int targetLine)
    {
        var lines = sourceText.Split('\n');

        if (targetLine <= 0 || targetLine > lines.Length)
        {
            return null;
        }

        var docLines = new List<string>();
        int currentLine = targetLine - 2;  // Start checking line before the declaration (0-based index)

        // Scan backwards to collect all consecutive /// comments
        while (currentLine >= 0)
        {
            var line = lines[currentLine].Trim();

            if (line.StartsWith("///"))
            {
                // Extract the comment content (everything after ///)
                var content = line.Substring(3).TrimStart();
                docLines.Insert(0, content);  // Insert at beginning to maintain order
                currentLine--;
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                // Allow blank lines within doc comments
                currentLine--;
            }
            else
            {
                // Hit a non-doc-comment line, stop scanning
                break;
            }
        }

        if (docLines.Count == 0)
        {
            return null;
        }

        // Combine doc comment lines into Markdown
        return string.Join("\n", docLines);
    }

    /// <summary>
    /// Extracts a summary (first paragraph) from doc comments.
    /// Useful for completion item documentation where full docs would be too verbose.
    /// </summary>
    public static string? ExtractSummary(string? docComment)
    {
        if (string.IsNullOrWhiteSpace(docComment))
        {
            return null;
        }

        // Take everything up to the first blank line or "Examples:" section
        var lines = docComment.Split('\n');
        var summaryLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Stop at blank line (paragraph break)
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }

            // Stop at section headers
            if (trimmed.StartsWith("#") ||
                trimmed.StartsWith("Example") ||
                trimmed.StartsWith("Note:") ||
                trimmed.StartsWith("Returns:") ||
                trimmed.StartsWith("Parameters:"))
            {
                break;
            }

            summaryLines.Add(trimmed);
        }

        return summaryLines.Count > 0
            ? string.Join(" ", summaryLines)
            : null;
    }

    /// <summary>
    /// Extracts documentation for a specific function parameter from doc comments.
    /// Looks for lines like "param_name - description" or "@param param_name description"
    /// </summary>
    public static string? ExtractParameterDoc(string? docComment, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(docComment))
        {
            return null;
        }

        var lines = docComment.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Pattern 1: "- param_name: description" or "- param_name - description"
            if (trimmed.StartsWith($"- {parameterName}:") ||
                trimmed.StartsWith($"- {parameterName} -"))
            {
                var colonIndex = trimmed.IndexOf(':');
                var dashIndex = trimmed.IndexOf('-', parameterName.Length);

                if (colonIndex > 0)
                {
                    return trimmed.Substring(colonIndex + 1).Trim();
                }
                else if (dashIndex > 0)
                {
                    return trimmed.Substring(dashIndex + 1).Trim();
                }
            }

            // Pattern 2: "@param parameterName description"
            if (trimmed.StartsWith($"@param {parameterName}"))
            {
                return trimmed.Substring($"@param {parameterName}".Length).Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if source code contains doc comments for potential caching optimization
    /// </summary>
    public static bool HasDocComments(string sourceText)
    {
        return sourceText.Contains("///");
    }
}
