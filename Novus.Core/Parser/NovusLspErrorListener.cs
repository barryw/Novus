using System.IO;
using Antlr4.Runtime;
using Novus.Diagnostics;

namespace Novus.Parser;

/// <summary>
/// Error listener optimized for LSP scenarios - never stops parsing.
/// Unlike the standard NovusErrorListener, this continues parsing even after
/// encountering syntax errors, allowing us to build partial ASTs for code completion.
/// </summary>
public class NovusLspErrorListener : NovusErrorListener
{
    public NovusLspErrorListener(DiagnosticBag diagnostics, string filePath, string source)
        : base(diagnostics, filePath, source)
    {
    }

    // Note: We inherit SyntaxError from NovusErrorListener which already reports errors
    // The key difference is that we use this with NovusErrorStrategy which handles recovery
    // No need to override SyntaxError since the base implementation is already correct
    // The LSP mode is achieved through the error strategy, not the listener
}
