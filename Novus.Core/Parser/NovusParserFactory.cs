using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Novus.Diagnostics;
using Novus.Frontend;

namespace Novus.Parser;

/// <summary>
/// Factory for creating configured NovusParser instances with error recovery.
/// Supports two modes: Compilation (strict) and LanguageServer (error-tolerant).
/// </summary>
public static class NovusParserFactory
{
    public enum ParseMode
    {
        /// <summary>
        /// Standard compilation mode - strict parsing, exit on errors.
        /// Used by the CLI compiler where correctness is critical.
        /// </summary>
        Compilation,

        /// <summary>
        /// LSP mode - continue parsing despite errors, build partial AST.
        /// Used by the language server to provide completions/hover even in broken code.
        /// </summary>
        LanguageServer
    }

    /// <summary>
    /// Create a configured parser for the specified mode.
    /// </summary>
    /// <param name="source">The source code to parse</param>
    /// <param name="diagnostics">DiagnosticBag to collect errors</param>
    /// <param name="filePath">Path to the source file (for error reporting)</param>
    /// <param name="mode">Parsing mode (Compilation or LanguageServer)</param>
    /// <returns>Configured NovusParser instance</returns>
    public static NovusParser CreateParser(
        string source,
        DiagnosticBag diagnostics,
        string filePath,
        ParseMode mode = ParseMode.Compilation)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);

        // Remove default console error listener
        parser.RemoveErrorListeners();

        if (mode == ParseMode.LanguageServer)
        {
            // LSP mode: Continue parsing, build partial AST
            ConfigureForLsp(parser, diagnostics, filePath, source);
        }
        else
        {
            // Compilation mode: Standard error handling
            ConfigureForCompilation(parser, diagnostics, filePath, source);
        }

        return parser;
    }

    /// <summary>
    /// Configure parser for LSP mode - error tolerant, continues on errors.
    /// </summary>
    private static void ConfigureForLsp(
        NovusParser parser,
        DiagnosticBag diagnostics,
        string filePath,
        string source)
    {
        // Use custom error listener that doesn't stop parsing
        parser.AddErrorListener(new NovusLspErrorListener(diagnostics, filePath, source));

        // Use custom error strategy for smart recovery
        parser.ErrorHandler = new NovusErrorStrategy();

        // Enable LL(*) prediction mode for better error recovery (slower but more resilient)
        // This helps ANTLR make better decisions when the input is ambiguous or erroneous
        parser.Interpreter.PredictionMode = PredictionMode.LL;
    }

    /// <summary>
    /// Configure parser for compilation mode - standard strict parsing.
    /// </summary>
    private static void ConfigureForCompilation(
        NovusParser parser,
        DiagnosticBag diagnostics,
        string filePath,
        string source)
    {
        // Standard error listener (existing behavior)
        parser.AddErrorListener(new NovusErrorListener(diagnostics, filePath, source));

        // Use default error strategy with faster SLL prediction
        parser.ErrorHandler = new DefaultErrorStrategy();
        parser.Interpreter.PredictionMode = PredictionMode.SLL;
    }
}
