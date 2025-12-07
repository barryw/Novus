using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace Novus.Parser;

/// <summary>
/// Custom error recovery strategy optimized for Novus LSP scenarios.
/// Provides intelligent recovery for common incomplete code patterns like:
/// - "someFunc." (partial member access)
/// - "Result::" (partial enum variant access)
/// - Missing closing delimiters at EOF
/// - Incomplete type annotations
/// </summary>
public class NovusErrorStrategy : DefaultErrorStrategy
{
    /// <summary>
    /// Enhanced recovery for missing tokens - crucial for code completion.
    /// This method is called when the parser expects a specific token but doesn't find it.
    /// </summary>
    public override IToken RecoverInline(Antlr4.Runtime.Parser recognizer)
    {
        // Get expected tokens at current position
        var expecting = GetExpectedTokens(recognizer);
        var currentToken = recognizer.CurrentToken;

        // Special handling for EOF scenarios - critical for LSP completions
        if (currentToken.Type == TokenConstants.EOF)
        {
            // Scenario: "someFunc." <EOF>
            // We're at EOF and expecting an identifier - create synthetic empty identifier
            // This allows the parser to recognize the member access expression
            if (expecting.Contains(NovusLexer.IDENTIFIER))
            {
                return CreateMissingToken(recognizer, NovusLexer.IDENTIFIER, "");
            }

            // Scenario: Unclosed delimiters at EOF
            // Insert the missing closing delimiter to help parser recover
            if (expecting.Contains(NovusLexer.RPAREN))
                return CreateMissingToken(recognizer, NovusLexer.RPAREN, ")");
            if (expecting.Contains(NovusLexer.RBRACE))
                return CreateMissingToken(recognizer, NovusLexer.RBRACE, "}");
            if (expecting.Contains(NovusLexer.RBRACKET))
                return CreateMissingToken(recognizer, NovusLexer.RBRACKET, "]");
        }

        // For non-EOF scenarios, use default recovery
        return base.RecoverInline(recognizer);
    }

    /// <summary>
    /// Sync to recover from errors - uses strategic sync points in Novus grammar.
    /// Sync points are "safe" places where we can resume parsing after an error.
    /// </summary>
    public override void Sync(Antlr4.Runtime.Parser recognizer)
    {
        // If we're already in error recovery mode, don't sync again
        if (InErrorRecoveryMode(recognizer))
        {
            return;
        }

        var tokens = (ITokenStream)recognizer.InputStream;
        var la = tokens.LA(1);

        // Define sync tokens for Novus grammar
        // These are tokens that typically start new statements/declarations
        // We can safely resume parsing from these points
        var syncTokens = new HashSet<int>
        {
            // Statement/block boundaries
            NovusLexer.NEWLINE,
            NovusLexer.LBRACE,
            NovusLexer.RBRACE,

            // Declaration keywords
            NovusLexer.KW_FN,
            NovusLexer.KW_STRUCT,
            NovusLexer.KW_ENUM,
            NovusLexer.KW_IMPL,
            NovusLexer.KW_TRAIT,
            NovusLexer.KW_CONST,
            NovusLexer.KW_STATIC,
            NovusLexer.KW_PUB,

            // Statement keywords
            NovusLexer.KW_LET,
            NovusLexer.KW_VAR,
            NovusLexer.KW_RETURN,
            NovusLexer.KW_IF,
            NovusLexer.KW_WHILE,
            NovusLexer.KW_FOR,
            NovusLexer.KW_MATCH,
            NovusLexer.KW_DEFER,
            NovusLexer.KW_BREAK,

            // Import/module keywords
            NovusLexer.KW_FROM,
            NovusLexer.KW_IMPORT,
            NovusLexer.KW_USE,

            // End of file
            NovusLexer.Eof
        };

        // If we're at a sync token, don't consume it - we're already at a safe point
        if (syncTokens.Contains(la))
        {
            return;
        }

        // Otherwise, call base sync logic to skip tokens until we hit a sync point
        base.Sync(recognizer);
    }

    /// <summary>
    /// Create a synthetic missing token for error recovery.
    /// This allows the parser to "pretend" it found the expected token and continue.
    /// </summary>
    private IToken CreateMissingToken(Antlr4.Runtime.Parser recognizer, int expectedTokenType, string text)
    {
        var currentToken = recognizer.CurrentToken;
        var tokenSource = currentToken.TokenSource;
        var inputStream = tokenSource?.InputStream;

        var pair = Tuple.Create(tokenSource, inputStream);

        // Create a CommonToken with the expected type
        // The token has zero length (StartIndex > StopIndex) to indicate it's synthetic
        return new CommonToken(pair, expectedTokenType, TokenConstants.DefaultChannel,
            currentToken.StartIndex, currentToken.StartIndex - 1)
        {
            Text = text,
            Line = currentToken.Line,
            Column = currentToken.Column
        };
    }
}
