using Antlr4.Runtime.Tree;

namespace Novus.Frontend;

/// <summary>
/// Extension methods for common AST operations.
/// Reduces boilerplate and improves null safety.
/// </summary>
public static class AstExtensions
{
    /// <summary>
    /// Safely get the text of a child node at the specified index
    /// </summary>
    /// <param name="context">The parse tree context</param>
    /// <param name="index">The child index</param>
    /// <returns>The child's text, or empty string if the child doesn't exist</returns>
    public static string GetChildText(this IParseTree context, int index)
    {
        return context.GetChild(index)?.GetText() ?? string.Empty;
    }

    /// <summary>
    /// Check if a context has a child with the specified text
    /// </summary>
    /// <param name="context">The parse tree context</param>
    /// <param name="text">The text to search for</param>
    /// <param name="searchDepth">How many children to examine</param>
    /// <returns>True if a child with the specified text exists</returns>
    public static bool HasChildWithText(this IParseTree context, string text, int searchDepth = 5)
    {
        for (int i = 0; i < Math.Min(searchDepth, context.ChildCount); i++)
        {
            if (context.GetChild(i)?.GetText() == text)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get the index of the first child with the specified text
    /// </summary>
    /// <param name="context">The parse tree context</param>
    /// <param name="text">The text to search for</param>
    /// <param name="searchDepth">How many children to examine</param>
    /// <returns>The index of the child, or -1 if not found</returns>
    public static int GetChildIndexWithText(this IParseTree context, string text, int searchDepth = 5)
    {
        for (int i = 0; i < Math.Min(searchDepth, context.ChildCount); i++)
        {
            if (context.GetChild(i)?.GetText() == text)
            {
                return i;
            }
        }
        return -1;
    }
}
