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
    /// C89/C99/C11/C23 reserved keywords that will cause VBCC compilation errors if used as identifiers.
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
        "_Alignas", "_Alignof", "_Atomic", "_Generic", "_Noreturn", "_Static_assert", "_Thread_local",

        // C23 additions (new keywords that were previously macros or new features)
        "true", "false", "nullptr", "constexpr", "typeof", "typeof_unqual",
        // C23 lowercase forms of C11 keywords (deprecated forms still exist but these are now preferred)
        "alignas", "alignof", "bool", "static_assert", "thread_local"
    ];

    /// <summary>
    /// Common C standard library identifiers that could cause conflicts.
    /// These aren't keywords per se, but using them as variable names is problematic.
    /// </summary>
    private static readonly string[] CStdlibReservedArray =
    [
        // Common macros from standard headers
        "NULL", "EOF", "BUFSIZ", "FILENAME_MAX", "FOPEN_MAX", "TMP_MAX",
        "RAND_MAX", "MB_CUR_MAX", "CHAR_BIT", "CHAR_MAX", "CHAR_MIN",
        "INT_MAX", "INT_MIN", "LONG_MAX", "LONG_MIN", "SHRT_MAX", "SHRT_MIN",
        "UINT_MAX", "ULONG_MAX", "USHRT_MAX", "SIZE_MAX", "PTRDIFF_MAX",

        // Errno values (E[0-9A-Z]* pattern - just common ones)
        "EDOM", "ERANGE", "EILSEQ", "ENOMEM", "EINVAL", "EIO",

        // Standard type names that could conflict
        "size_t", "ptrdiff_t", "wchar_t", "intptr_t", "uintptr_t",
        "int8_t", "int16_t", "int32_t", "int64_t",
        "uint8_t", "uint16_t", "uint32_t", "uint64_t",
        "intmax_t", "uintmax_t"
    ];

    /// <summary>
    /// VBCC-specific reserved identifiers and AmigaOS common names.
    /// </summary>
    private static readonly string[] VbccReservedArray =
    [
        // VBCC-specific keywords/extensions
        "__reg", "__amigacall", "__saveds", "__aligned", "__interrupt",
        "__asm", "__inline", "__extension", "__attribute",

        // Common AmigaOS type names we generate
        "APTR", "BPTR", "BSTR", "STRPTR", "UBYTE", "BYTE", "UWORD", "WORD", "ULONG", "LONG",
        "BOOL", "TEXT", "FLOAT", "DOUBLE"
    ];

    /// <summary>
    /// Frozen set of C keywords for O(1) lookup.
    /// </summary>
    private static readonly FrozenSet<string> CKeywords = CKeywordsArray.ToFrozenSet();

    /// <summary>
    /// Frozen set of C stdlib reserved names for O(1) lookup.
    /// </summary>
    private static readonly FrozenSet<string> CStdlibReserved = CStdlibReservedArray.ToFrozenSet();

    /// <summary>
    /// Frozen set of VBCC/AmigaOS reserved names for O(1) lookup.
    /// </summary>
    private static readonly FrozenSet<string> VbccReserved = VbccReservedArray.ToFrozenSet();

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
    /// Checks if an identifier is a C standard library reserved name.
    /// </summary>
    public static bool IsCStdlibReserved(string identifier) => CStdlibReserved.Contains(identifier);

    /// <summary>
    /// Checks if an identifier is a VBCC/AmigaOS reserved name.
    /// </summary>
    public static bool IsVbccReserved(string identifier) => VbccReserved.Contains(identifier);

    /// <summary>
    /// Checks if an identifier is a Novus reserved keyword.
    /// </summary>
    public static bool IsNovusKeyword(string identifier) => NovusKeywords.Contains(identifier);

    /// <summary>
    /// Checks if an identifier is any reserved keyword (C or Novus).
    /// This is the primary check - use this for identifier validation.
    /// </summary>
    public static bool IsReserved(string identifier) =>
        IsCKeyword(identifier) || IsNovusKeyword(identifier);

    /// <summary>
    /// Checks if an identifier would cause problems with C codegen or VBCC.
    /// This is a broader check that includes stdlib names and VBCC-specific identifiers.
    /// </summary>
    public static bool IsProblematic(string identifier) =>
        IsReserved(identifier) || IsCStdlibReserved(identifier) || IsVbccReserved(identifier);

    /// <summary>
    /// Checks if an identifier starts with a reserved C prefix pattern.
    /// According to the C standard, identifiers starting with _ followed by uppercase
    /// or another _ are reserved for the implementation.
    /// </summary>
    public static bool HasReservedCPrefix(string identifier)
    {
        if (string.IsNullOrEmpty(identifier) || identifier.Length < 2)
            return false;

        // _X or __ patterns are reserved in C
        if (identifier[0] == '_')
        {
            return identifier.Length >= 2 && (char.IsUpper(identifier[1]) || identifier[1] == '_');
        }

        return false;
    }

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
    /// Returns all C stdlib reserved names for diagnostic purposes.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllCStdlibReserved() => CStdlibReserved;

    /// <summary>
    /// Returns all VBCC/AmigaOS reserved names for diagnostic purposes.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllVbccReserved() => VbccReserved;

    /// <summary>
    /// Returns all Novus keywords for diagnostic purposes.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllNovusKeywords() => NovusKeywords;

    /// <summary>
    /// Returns all reserved identifiers (combined) for diagnostic purposes.
    /// </summary>
    public static IEnumerable<string> GetAllReserved() =>
        CKeywords.Concat(NovusKeywords).Concat(CStdlibReserved).Concat(VbccReserved).Distinct();

    /// <summary>
    /// Gets detailed information about why an identifier is reserved.
    /// Returns null if the identifier is not reserved.
    /// </summary>
    public static string? GetReservationReason(string identifier)
    {
        if (IsCKeyword(identifier))
            return $"'{identifier}' is a C keyword and cannot be used as an identifier";

        if (IsNovusKeyword(identifier))
            return $"'{identifier}' is a Novus keyword and cannot be used as an identifier";

        if (IsCStdlibReserved(identifier))
            return $"'{identifier}' is a C standard library identifier and may cause conflicts";

        if (IsVbccReserved(identifier))
            return $"'{identifier}' is reserved by VBCC or AmigaOS and may cause conflicts";

        if (HasReservedCPrefix(identifier))
            return $"'{identifier}' starts with a reserved C prefix (_ followed by uppercase or _)";

        return null;
    }
}
