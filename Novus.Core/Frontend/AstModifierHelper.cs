using Antlr4.Runtime.Tree;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// Helper class for parsing modifiers and visibility from AST nodes.
/// Centralizes the logic that was previously duplicated across IrBuilder and ModuleImportHelper.
/// </summary>
public static class AstModifierHelper
{
    /// <summary>
    /// Parse visibility and modifiers from a parse tree context by examining its children.
    /// </summary>
    /// <param name="context">The AST context to examine</param>
    /// <param name="maxChildren">Maximum number of children to examine (default 5)</param>
    /// <returns>Tuple containing visibility, extern flag, mutable flag, and const flag</returns>
    public static (Visibility visibility, bool isExtern, bool isMutable, bool isConst) ParseModifiers(
        IParseTree context,
        int maxChildren = 5)
    {
        var visibility = Visibility.Private;
        bool isExtern = false;
        bool isMutable = false;
        bool isConst = false;

        for (int i = 0; i < Math.Min(maxChildren, context.ChildCount); i++)
        {
            var childText = context.GetChild(i)?.GetText();
            switch (childText)
            {
                case "extern":
                    isExtern = true;
                    break;
                case "pub":
                    visibility = Visibility.Public;
                    break;
                case "internal":
                    visibility = Visibility.Internal;
                    break;
                case "var":
                    isMutable = true;
                    break;
                case "const":
                    isConst = true;
                    break;
            }
        }

        return (visibility, isExtern, isMutable, isConst);
    }

    /// <summary>
    /// Check if a context has a specific modifier
    /// </summary>
    public static bool HasModifier(IParseTree context, string modifier, int maxChildren = 5)
    {
        for (int i = 0; i < Math.Min(maxChildren, context.ChildCount); i++)
        {
            if (context.GetChild(i)?.GetText() == modifier)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Check if a function declaration is public
    /// </summary>
    public static bool IsPublic(NovusParser.FunctionDeclarationContext context)
    {
        return HasModifier(context, "pub", 3);
    }

    /// <summary>
    /// Check if a function declaration has the 'extern' modifier
    /// </summary>
    public static bool IsExtern(NovusParser.FunctionDeclarationContext context)
    {
        return HasModifier(context, "extern", 3);
    }

    /// <summary>
    /// Get function visibility (pub and extern flags)
    /// </summary>
    public static (bool IsPub, bool IsExtern) GetFunctionVisibility(NovusParser.FunctionDeclarationContext context)
    {
        var (visibility, isExtern, _, _) = ParseModifiers(context, 5);
        return (visibility == Visibility.Public, isExtern);
    }

    /// <summary>
    /// Check if a function declaration has the 'const' modifier (const fn)
    /// </summary>
    public static bool IsConstFn(NovusParser.FunctionDeclarationContext context)
    {
        return HasModifier(context, "const", 5);
    }
}
