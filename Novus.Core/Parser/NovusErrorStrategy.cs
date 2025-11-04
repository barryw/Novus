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
            if (expecting.Contains(NovusParser.IDENTIFIER))
            {
                return CreateMissingToken(recognizer, NovusParser.IDENTIFIER, "");
            }

            // Scenario: Unclosed delimiters at EOF
            // Insert the missing closing delimiter to help parser recover
            if (expecting.Contains(NovusParser.T__3)) // ')'
                return CreateMissingToken(recognizer, NovusParser.T__3, ")");
            if (expecting.Contains(NovusParser.T__5)) // '}'
                return CreateMissingToken(recognizer, NovusParser.T__5, "}");
            if (expecting.Contains(NovusParser.T__7)) // ']'
                return CreateMissingToken(recognizer, NovusParser.T__7, "]");
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
            NovusParser.NEWLINE,
            NovusParser.T__4,  // '{'
            NovusParser.T__5,  // '}'

            // Declaration keywords
            NovusParser.KW_FN,
            NovusParser.KW_STRUCT,
            NovusParser.KW_ENUM,
            NovusParser.KW_IMPL,
            NovusParser.KW_TRAIT,
            NovusParser.KW_CONST,
            NovusParser.KW_STATIC,
            NovusParser.KW_PUB,

            // Statement keywords
            NovusParser.KW_LET,
            NovusParser.KW_VAR,
            NovusParser.KW_RETURN,
            NovusParser.KW_IF,
            NovusParser.KW_WHILE,
            NovusParser.KW_FOR,
            NovusParser.KW_MATCH,
            NovusParser.KW_DEFER,
            NovusParser.KW_BREAK,

            // Import/module keywords
            NovusParser.KW_FROM,
            NovusParser.KW_IMPORT,
            NovusParser.KW_USE,

            // End of file
            NovusParser.Eof
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
