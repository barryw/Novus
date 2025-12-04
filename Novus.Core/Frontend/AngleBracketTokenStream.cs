using Antlr4.Runtime;

namespace Novus.Frontend;

/// <summary>
/// Custom token stream that handles the classic lexer ambiguity between
/// shift operators (&lt;&lt;, &gt;&gt;) and nested generic type parameters.
///
/// When parsing nested generics like Vec&lt;Option&lt;T&gt;&gt;, the lexer tokenizes
/// the closing &gt;&gt; as a single SHIFT_RIGHT token. This stream splits such
/// tokens into two separate '&gt;' tokens when they appear in generic contexts.
///
/// This implementation uses a STATELESS approach: instead of tracking depth
/// during parsing (which fails due to parser backtracking), it looks backward
/// in the token stream to count unmatched angle brackets whenever it sees
/// a potential &gt;&gt; to split.
///
/// This is the same approach used by C++11, Rust, and modern Java compilers.
/// </summary>
public class AngleBracketTokenStream : CommonTokenStream
{
    // Token type constants - dynamically determined from lexer vocabulary
    private readonly int TOKEN_LESS;
    private readonly int TOKEN_GREATER;
    private readonly int TOKEN_LSHIFT;
    private readonly int TOKEN_RSHIFT;
    private readonly int TOKEN_COLON;
    private readonly int TOKEN_ARROW;
    private readonly int TOKEN_COMMA;
    private readonly int TOKEN_IDENTIFIER;
    private readonly int TOKEN_DOUBLE_COLON;
    private readonly int TOKEN_EQUALS;
    private readonly int TOKEN_LBRACE;
    private readonly int TOKEN_RBRACE;
    private readonly int TOKEN_LPAREN;
    private readonly int TOKEN_RPAREN;
    private readonly int TOKEN_SEMI;
    private readonly int TOKEN_NEWLINE;

    private readonly Queue<IToken> _splitTokenBuffer = new();
    private IToken? _lastSplitToken = null;
    private int _lastSplitPosition = -1;

    public AngleBracketTokenStream(ITokenSource tokenSource) : base(tokenSource)
    {
        // Dynamically find token types from the lexer's vocabulary
        var vocabulary = (tokenSource as Lexer)?.Vocabulary;

        if (vocabulary == null)
        {
            throw new InvalidOperationException("TokenSource must be a Lexer with a Vocabulary");
        }

        TOKEN_LESS = FindTokenType(vocabulary, "<");
        TOKEN_GREATER = FindTokenType(vocabulary, ">");
        TOKEN_LSHIFT = FindTokenType(vocabulary, "<<");
        TOKEN_RSHIFT = FindTokenType(vocabulary, ">>");
        TOKEN_COLON = FindTokenType(vocabulary, ":");
        TOKEN_ARROW = FindTokenType(vocabulary, "->");
        TOKEN_COMMA = FindTokenType(vocabulary, ",");
        TOKEN_DOUBLE_COLON = FindTokenType(vocabulary, "::");
        TOKEN_EQUALS = FindTokenType(vocabulary, "=");
        TOKEN_LBRACE = FindTokenType(vocabulary, "{");
        TOKEN_RBRACE = FindTokenType(vocabulary, "}");
        TOKEN_LPAREN = FindTokenType(vocabulary, "(");
        TOKEN_RPAREN = FindTokenType(vocabulary, ")");
        TOKEN_SEMI = FindTokenType(vocabulary, ";");
        TOKEN_IDENTIFIER = FindTokenTypeBySymbolicName(vocabulary, "IDENTIFIER");
        TOKEN_NEWLINE = FindTokenTypeBySymbolicName(vocabulary, "NEWLINE");

        if (TOKEN_LESS == -1 || TOKEN_GREATER == -1 || TOKEN_LSHIFT == -1 || TOKEN_RSHIFT == -1)
        {
            throw new InvalidOperationException(
                $"Failed to find required token types in lexer vocabulary: " +
                $"< = {TOKEN_LESS}, > = {TOKEN_GREATER}, << = {TOKEN_LSHIFT}, >> = {TOKEN_RSHIFT}");
        }
    }

    private static int FindTokenType(IVocabulary vocabulary, string literalName)
    {
        for (int i = 0; i < 200; i++)
        {
            var literal = vocabulary.GetLiteralName(i);
            if (literal != null && literal == $"'{literalName}'")
            {
                return i;
            }
        }
        return -1;
    }

    private static int FindTokenTypeBySymbolicName(IVocabulary vocabulary, string symbolicName)
    {
        for (int i = 0; i < 200; i++)
        {
            var name = vocabulary.GetSymbolicName(i);
            if (name == symbolicName)
            {
                return i;
            }
        }
        return -1;
    }

    public override IToken LT(int k)
    {
        // If we have buffered split tokens, handle them
        if (_splitTokenBuffer.Count > 0 && k == 1)
        {
            return _splitTokenBuffer.Peek();
        }

        // Get the underlying token
        var token = base.LT(k);

        // Only process for lookahead of 1
        if (k != 1)
        {
            return token;
        }

        // Check if we need to split >> or << tokens in generic context
        // Use position-based check to avoid re-splitting the same token during backtracking
        int currentPosition = Index;

        if (ShouldSplitToken(token) &&
            !(_lastSplitToken == token || _lastSplitPosition == currentPosition))
        {
            _lastSplitToken = token;
            _lastSplitPosition = currentPosition;
            SplitAndBufferToken(token);
            return _splitTokenBuffer.Peek();
        }

        return token;
    }

    public override void Consume()
    {
        // If we have split tokens buffered, consume from buffer
        if (_splitTokenBuffer.Count > 0)
        {
            _splitTokenBuffer.Dequeue();

            if (_splitTokenBuffer.Count == 0)
            {
                // All split tokens consumed, now consume the underlying token
                _lastSplitToken = null;
                _lastSplitPosition = -1;
                base.Consume();
            }
            return;
        }

        base.Consume();
    }

    public override void Seek(int index)
    {
        // When the parser seeks/backtracks, clear our split token buffer
        // The buffer is only valid for the current position
        if (_splitTokenBuffer.Count > 0 && index != Index)
        {
            _splitTokenBuffer.Clear();
            _lastSplitToken = null;
            _lastSplitPosition = -1;
        }
        base.Seek(index);
    }

    /// <summary>
    /// Determines if the token should be split by looking backward in the token stream
    /// to count unmatched angle brackets. This is a STATELESS approach that works
    /// correctly even when the parser backtracks.
    /// </summary>
    private bool ShouldSplitToken(IToken token)
    {
        var tokenType = token.Type;

        // Only consider >> for splitting (not <<)
        if (tokenType != TOKEN_RSHIFT)
        {
            return false;
        }

        // Look backward to count unmatched angle brackets
        // We need at least 2 unmatched < to split >> into two >
        int unmatchedLess = CountUnmatchedAngleBracketsLookingBack();

        return unmatchedLess >= 2;
    }

    /// <summary>
    /// Counts unmatched '&lt;' tokens by scanning backward from current position.
    /// Stops at statement/expression boundaries to avoid counting across statements.
    /// </summary>
    private int CountUnmatchedAngleBracketsLookingBack()
    {
        int depth = 0;
        bool inTypeContext = false;

        // Start from the token just before the current position
        // We need to look at already-consumed tokens
        int pos = Index - 1;

        // Scan backward looking for type context markers and angle brackets
        while (pos >= 0)
        {
            var tok = Get(pos);
            var tokType = tok.Type;

            // Stop at statement boundaries
            if (tokType == TOKEN_SEMI || tokType == TOKEN_LBRACE || tokType == TOKEN_RBRACE)
            {
                break;
            }

            // Stop at NEWLINE if we're not clearly in a type context
            if (tokType == TOKEN_NEWLINE && depth == 0)
            {
                break;
            }

            if (tokType == TOKEN_GREATER)
            {
                // A > means we matched one level
                depth--;
            }
            else if (tokType == TOKEN_LESS)
            {
                // A < opens one level - check if it's a type context
                // Look at what preceded this <
                int prevPos = pos - 1;
                while (prevPos >= 0)
                {
                    var prevTok = Get(prevPos);
                    if (prevTok.Type != TOKEN_NEWLINE)
                    {
                        break;
                    }
                    prevPos--;
                }

                if (prevPos >= 0)
                {
                    var prevTok = Get(prevPos);
                    // < after identifier or :: is likely a generic type
                    if (prevTok.Type == TOKEN_IDENTIFIER || prevTok.Type == TOKEN_DOUBLE_COLON)
                    {
                        depth++;
                        inTypeContext = true;
                    }
                    // Otherwise it might be a comparison operator - don't count it
                }
            }
            else if (tokType == TOKEN_COLON || tokType == TOKEN_ARROW)
            {
                // Type annotation or return type - definitely a type context
                inTypeContext = true;
            }
            else if (tokType == TOKEN_EQUALS && !inTypeContext)
            {
                // Assignment - if we haven't seen type markers, stop
                break;
            }

            pos--;
        }

        return depth;
    }

    private void SplitAndBufferToken(IToken token)
    {
        var tokenType = token.Type;

        if (tokenType == TOKEN_RSHIFT)
        {
            // Split >> into two > tokens
            var firstToken = new CommonToken(token)
            {
                Type = TOKEN_GREATER,
                Text = ">",
                StopIndex = token.StartIndex
            };

            var secondToken = new CommonToken(token)
            {
                Type = TOKEN_GREATER,
                Text = ">",
                StartIndex = token.StartIndex + 1,
                StopIndex = token.StopIndex
            };

            _splitTokenBuffer.Enqueue(firstToken);
            _splitTokenBuffer.Enqueue(secondToken);
        }
        else if (tokenType == TOKEN_LSHIFT)
        {
            // Split << into two < tokens
            var firstToken = new CommonToken(token)
            {
                Type = TOKEN_LESS,
                Text = "<",
                StopIndex = token.StartIndex
            };

            var secondToken = new CommonToken(token)
            {
                Type = TOKEN_LESS,
                Text = "<",
                StartIndex = token.StartIndex + 1,
                StopIndex = token.StopIndex
            };

            _splitTokenBuffer.Enqueue(firstToken);
            _splitTokenBuffer.Enqueue(secondToken);
        }
    }
}
