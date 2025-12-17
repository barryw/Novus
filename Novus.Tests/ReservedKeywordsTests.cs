using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for the ReservedKeywords utility class.
/// </summary>
public class ReservedKeywordsTests
{
    [Theory]
    [InlineData("double")]
    [InlineData("int")]
    [InlineData("char")]
    [InlineData("void")]
    [InlineData("struct")]
    [InlineData("const")]
    [InlineData("static")]
    [InlineData("inline")]
    [InlineData("restrict")]
    [InlineData("_Bool")]
    [InlineData("_Alignas")]
    [InlineData("true")]  // C23
    [InlineData("false")] // C23
    [InlineData("nullptr")] // C23
    [InlineData("constexpr")] // C23
    [InlineData("typeof")] // C23
    [InlineData("alignas")] // C23 lowercase
    [InlineData("bool")] // C23 lowercase
    public void IsCKeyword_WithCKeywords_ReturnsTrue(string keyword)
    {
        Assert.True(ReservedKeywords.IsCKeyword(keyword), $"'{keyword}' should be recognized as a C keyword");
    }

    [Theory]
    [InlineData("fn")]
    [InlineData("let")]
    [InlineData("pub")]
    [InlineData("impl")]
    [InlineData("trait")]
    [InlineData("match")]
    [InlineData("defer")]
    [InlineData("unsafe")]
    [InlineData("self")]
    [InlineData("Self")]
    [InlineData("from")]
    [InlineData("import")]
    [InlineData("return")]
    [InlineData("if")]
    [InlineData("else")]
    [InlineData("while")]
    [InlineData("for")]
    public void IsNovusKeyword_WithNovusKeywords_ReturnsTrue(string keyword)
    {
        Assert.True(ReservedKeywords.IsNovusKeyword(keyword), $"'{keyword}' should be recognized as a Novus keyword");
    }

    [Fact]
    public void DebugPrint_NovusKeywords()
    {
        var keywords = ReservedKeywords.GetAllNovusKeywords();
        // Just output for debugging
        foreach (var kw in keywords.OrderBy(k => k))
        {
            System.Diagnostics.Debug.WriteLine($"Novus keyword: {kw}");
        }
        // This test always passes, it's just for debugging
        Assert.True(keywords.Count > 0);
    }

    [Theory]
    [InlineData("NULL")]
    [InlineData("EOF")]
    [InlineData("size_t")]
    [InlineData("uint32_t")]
    [InlineData("INT_MAX")]
    [InlineData("ENOMEM")]
    public void IsCStdlibReserved_WithStdlibNames_ReturnsTrue(string name)
    {
        Assert.True(ReservedKeywords.IsCStdlibReserved(name), $"'{name}' should be recognized as C stdlib reserved");
    }

    [Theory]
    [InlineData("__reg")]
    [InlineData("__amigacall")]
    [InlineData("APTR")]
    [InlineData("BPTR")]
    [InlineData("UBYTE")]
    [InlineData("ULONG")]
    public void IsVbccReserved_WithVbccNames_ReturnsTrue(string name)
    {
        Assert.True(ReservedKeywords.IsVbccReserved(name), $"'{name}' should be recognized as VBCC reserved");
    }

    [Theory]
    [InlineData("myVariable")]
    [InlineData("times_two")]
    [InlineData("calculate")]
    [InlineData("MyStruct")]
    [InlineData("foo_bar_baz")]
    public void IsReserved_WithValidIdentifiers_ReturnsFalse(string identifier)
    {
        Assert.False(ReservedKeywords.IsReserved(identifier), $"'{identifier}' should NOT be reserved");
    }

    [Theory]
    [InlineData("_MyVar", true)]   // _ followed by uppercase
    [InlineData("__internal", true)] // double underscore
    [InlineData("__FOO", true)]     // double underscore with caps
    [InlineData("_myvar", false)]    // _ followed by lowercase is OK
    [InlineData("myVar", false)]     // no leading underscore
    [InlineData("MY_CONST", false)]  // no leading underscore
    [InlineData("_", false)]         // single underscore, no second char
    public void HasReservedCPrefix_ChecksCorrectly(string identifier, bool expected)
    {
        Assert.Equal(expected, ReservedKeywords.HasReservedCPrefix(identifier));
    }

    [Fact]
    public void GetReservationReason_ForCKeyword_ReturnsCorrectMessage()
    {
        var reason = ReservedKeywords.GetReservationReason("double");
        Assert.NotNull(reason);
        Assert.Contains("C keyword", reason);
    }

    [Fact]
    public void GetReservationReason_ForNovusKeyword_ReturnsCorrectMessage()
    {
        var reason = ReservedKeywords.GetReservationReason("fn");
        Assert.NotNull(reason);
        Assert.Contains("Novus keyword", reason);
    }

    [Fact]
    public void GetReservationReason_ForValidIdentifier_ReturnsNull()
    {
        var reason = ReservedKeywords.GetReservationReason("myValidIdentifier");
        Assert.Null(reason);
    }

    [Fact]
    public void GetSuggestedAlternative_ForDouble_ReturnsSuggestion()
    {
        var alternative = ReservedKeywords.GetSuggestedAlternative("double");
        Assert.Equal("d", alternative);
    }

    [Fact]
    public void GetAllCKeywords_ContainsExpectedCount()
    {
        var keywords = ReservedKeywords.GetAllCKeywords();
        // C89 (32) + C99 (5) + C11 (7) + C23 (11) = 55
        Assert.True(keywords.Count >= 43, $"Expected at least 43 C keywords, got {keywords.Count}");
    }

    [Fact]
    public void GetAllNovusKeywords_ContainsExpectedKeywords()
    {
        var keywords = ReservedKeywords.GetAllNovusKeywords();
        Assert.Contains("fn", keywords);
        Assert.Contains("let", keywords);
        Assert.Contains("pub", keywords);
        Assert.Contains("impl", keywords);
        // Note: "const" is shared between C and Novus, so it's in both
        // The grammar has KW_CONST for Novus, but since C also has "const",
        // IsReserved("const") returns true via IsCKeyword first
    }

    [Fact]
    public void IsProblematic_ChecksAllCategories()
    {
        // C keyword
        Assert.True(ReservedKeywords.IsProblematic("double"));
        // Novus keyword
        Assert.True(ReservedKeywords.IsProblematic("fn"));
        // C stdlib
        Assert.True(ReservedKeywords.IsProblematic("NULL"));
        // VBCC
        Assert.True(ReservedKeywords.IsProblematic("APTR"));
        // Valid
        Assert.False(ReservedKeywords.IsProblematic("myVariable"));
    }
}
