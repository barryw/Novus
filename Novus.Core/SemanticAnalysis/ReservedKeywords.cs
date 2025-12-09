using System.Collections.Frozen;
using Novus.Parser;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Centralized registry of reserved keywords that cannot be used as identifiers.
/// C keywords are static (they don't change), but Novus keywords are extracted
/// automatically from the generated lexer to stay in sync with the grammar.
/// </summary>
public static class ReservedKeywords
{
    /// <summary>
    /// C89/C99/C11 reserved keywords that will cause VBCC compilation errors if used as identifiers.
    /// </summary>
    private static readonly string[] CKeywordsArray =
    [
        // C89 keywords
        "auto", "break", "case", "char", "const", "continue", "default", "do",
        "double", "else", "enum", "extern", "float", "for", "goto", "if",
        "int", "long", "register", "return", "short", "signed", "sizeof", "static",
        "struct", "switch", "typedef", "union", "unsigned", "void", "volatile", "while",

        // C99 additions
        "inline", "restrict", "_Bool", "_Complex", "_Imaginary",

        // C11 additions
        "_Alignas", "_Alignof", "_Atomic", "_Generic", "_Noreturn", "_Static_assert", "_Thread_local"
    ];

    /// <summary>
    /// Frozen set of C keywords for O(1) lookup.
    /// </summary>
    private static readonly FrozenSet<string> CKeywords = CKeywordsArray.ToFrozenSet();

    /// <summary>
    /// Novus keywords extracted from the lexer grammar.
    /// Lazily initialized to avoid issues during static initialization.
    /// </summary>
    private static FrozenSet<string>? _novusKeywords;

    /// <summary>
    /// Gets the set of Novus keywords, extracted from the lexer grammar.
    /// </summary>
    private static FrozenSet<string> NovusKeywords => _novusKeywords ??= ExtractNovusKeywords();

    /// <summary>
    /// Extracts Novus keywords from the generated lexer's vocabulary.
    /// This keeps the keyword list in sync with the grammar automatically.
    /// </summary>
    private static FrozenSet<string> ExtractNovusKeywords()
    {
        var keywords = new HashSet<string>();
        var vocabulary = NovusLexer.DefaultVocabulary;

        // Iterate through all token types and find those with KW_ prefix (keywords)
        // Use a reasonable upper bound for token types - NovusLexer has ~150 tokens
        for (int i = 0; i <= 200; i++)
        {
            var symbolicName = vocabulary.GetSymbolicName(i);
            if (symbolicName != null && symbolicName.StartsWith("KW_"))
            {
                // Get the literal name (the actual keyword text)
                var literalName = vocabulary.GetLiteralName(i);
                if (literalName != null)
                {
                    // Remove the quotes around the literal (e.g., "'fn'" -> "fn")
                    var keyword = literalName.Trim('\'');
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        keywords.Add(keyword);
                    }
                }
            }
        }

        return keywords.ToFrozenSet();
    }

    /// <summary>
    /// Checks if an identifier is a C reserved keyword.
    /// </summary>
    public static bool IsCKeyword(string identifier) => CKeywords.Contains(identifier);

    /// <summary>
    /// Checks if an identifier is a Novus reserved keyword.
    /// </summary>
    public static bool IsNovusKeyword(string identifier) => NovusKeywords.Contains(identifier);

    /// <summary>
    /// Checks if an identifier is any reserved keyword (C or Novus).
    /// </summary>
    public static bool IsReserved(string identifier) => IsCKeyword(identifier) || IsNovusKeyword(identifier);

    /// <summary>
    /// Gets a suggested alternative name for a C reserved keyword.
    /// Returns null if no suggestion is available.
    /// </summary>
    public static string? GetSuggestedAlternative(string keyword)
    {
        return keyword switch
        {
            // Common C type names that conflict
            "short" => "s",
            "long" => "l",
            "int" => "i",
            "char" => "c",
            "float" => "f",
            "double" => "d",
            "void" => "v",
            "signed" => "s",
            "unsigned" => "u",

            // Control flow - suggest prefixed versions
            "break" => "brk",
            "continue" => "cont",
            "return" => "ret",
            "goto" => "jump",
            "switch" => "sw",
            "case" => "cas",
            "default" => "def",

            // Storage class
            "auto" => "automatic",
            "register" => "reg",
            "static" => "stat",
            "extern" => "ext",
            "const" => "constant",
            "volatile" => "vol",

            // Other
            "sizeof" => "size_of",
            "typedef" => "type_def",
            "struct" => "st",
            "union" => "un",
            "enum" => "en",
            "inline" => "inln",

            _ => $"{keyword}_"  // Generic: append underscore
        };
    }

    /// <summary>
    /// Returns all C keywords for diagnostic purposes.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllCKeywords() => CKeywords;

    /// <summary>
    /// Returns all Novus keywords for diagnostic purposes.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllNovusKeywords() => NovusKeywords;
}
