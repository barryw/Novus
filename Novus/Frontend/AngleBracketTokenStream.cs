using Antlr4.Runtime;

namespace Novus.Frontend;

/// <summary>
/// Custom token stream that handles the classic lexer ambiguity between
/// shift operators (<<, >>) and nested generic type parameters.
///
/// When parsing nested generics like Vec<Option<T>>, the lexer tokenizes
/// the closing >> as a single SHIFT_RIGHT token. This stream splits such
/// tokens into two separate '>' tokens when they appear in generic contexts.
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

    private int _angleBracketDepth = 0;
    private readonly Queue<IToken> _splitTokenBuffer = new();
    private IToken? _lastSplitToken = null;

    public AngleBracketTokenStream(ITokenSource tokenSource) : base(tokenSource)
    {
        // Dynamically find token types from the lexer's vocabulary
        // This ensures we always have the correct token types even when the grammar changes
        // ITokenSource doesn't expose Vocabulary, but Lexer does
        var vocabulary = (tokenSource as Lexer)?.Vocabulary;

        if (vocabulary == null)
        {
            throw new InvalidOperationException("TokenSource must be a Lexer with a Vocabulary");
        }

        TOKEN_LESS = FindTokenType(vocabulary, "<");
        TOKEN_GREATER = FindTokenType(vocabulary, ">");
        TOKEN_LSHIFT = FindTokenType(vocabulary, "<<");
        TOKEN_RSHIFT = FindTokenType(vocabulary, ">>");

        if (TOKEN_LESS == -1 || TOKEN_GREATER == -1 || TOKEN_LSHIFT == -1 || TOKEN_RSHIFT == -1)
        {
            throw new InvalidOperationException(
                $"Failed to find required token types in lexer vocabulary: " +
                $"< = {TOKEN_LESS}, > = {TOKEN_GREATER}, << = {TOKEN_LSHIFT}, >> = {TOKEN_RSHIFT}");
        }
    }

    private static int FindTokenType(IVocabulary vocabulary, string literalName)
    {
        // Search through all token types to find the one matching our literal
        // We need to iterate a reasonable range since IVocabulary doesn't expose MaxTokenType
        for (int i = 0; i < 200; i++)  // Should be enough for any reasonable grammar
        {
            var literal = vocabulary.GetLiteralName(i);
            if (literal != null && literal == $"'{literalName}'")
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
        // Only split if we haven't already split this token
        if (ShouldSplitToken(token) && _lastSplitToken != token)
        {
            _lastSplitToken = token;
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
            var token = _splitTokenBuffer.Dequeue();

            // Track angle brackets for split tokens
            UpdateAngleBracketDepth(token);

            // Don't call base.Consume() - we're consuming from our buffer
            // The underlying token stream position stays the same
            if (_splitTokenBuffer.Count == 0)
            {
                // All split tokens consumed, now consume the underlying token
                _lastSplitToken = null;  // Reset so we can split future tokens
                base.Consume();
            }
            return;
        }

        // Get current token before consuming
        var currentToken = base.LT(1);

        // Consume from base stream
        base.Consume();

        // Track angle bracket depth
        UpdateAngleBracketDepth(currentToken);
    }

    private void UpdateAngleBracketDepth(IToken token)
    {
        if (token.Type == TokenConstants.EOF)
        {
            return;
        }

        var tokenType = token.Type;
        if (tokenType == TOKEN_LESS)
        {
            _angleBracketDepth++;
        }
        else if (tokenType == TOKEN_GREATER && _angleBracketDepth > 0)
        {
            _angleBracketDepth--;
        }
    }

    private bool ShouldSplitToken(IToken token)
    {
        var tokenType = token.Type;

        // Only split >> and << when we're potentially in a generic context
        // We're in a generic context if we have open angle brackets
        if (_angleBracketDepth > 0)
        {
            return tokenType == TOKEN_RSHIFT || tokenType == TOKEN_LSHIFT;
        }

        return false;
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
