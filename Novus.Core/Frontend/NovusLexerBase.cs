using Antlr4.Runtime;
using System.IO;

namespace Novus.Frontend;

/// <summary>
/// Base class for NovusLexer providing custom mode management for inline assembly.
/// This allows the parser to control when the lexer switches to assembly mode.
///
/// The key challenge: ANTLR lexer modes are triggered at the lexer level but we need
/// parser context to know we're in an asm block. Solution: parser actions call these
/// methods to control lexer state.
/// </summary>
public abstract class NovusLexerBase : Lexer
{
    // Flag to tell the lexer the next '{' should enter ASM_MODE
    private bool _enterAsmModeOnNextBrace = false;

    protected NovusLexerBase(ICharStream input) : base(input)
    {
    }

    protected NovusLexerBase(ICharStream input, TextWriter output, TextWriter errorOutput)
        : base(input, output, errorOutput)
    {
    }

    /// <summary>
    /// Called by parser when we've parsed "unsafe asm(...) -&gt; type clobbers(...)"
    /// and are about to see the opening brace. The next '{' should enter ASM_MODE.
    /// </summary>
    public void PrepareForAsmBlock()
    {
        _enterAsmModeOnNextBrace = true;
    }

    /// <summary>
    /// Called by lexer when it sees a '{'. Returns true if we should enter ASM_MODE.
    /// </summary>
    public bool ShouldEnterAsmMode()
    {
        if (_enterAsmModeOnNextBrace)
        {
            _enterAsmModeOnNextBrace = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Called by parser when entering an asm block (backup method).
    /// Directly pushes ASM_MODE onto the mode stack.
    /// </summary>
    public void EnterAsmMode()
    {
        // Mode constant ASM_MODE will be defined in the generated lexer
        // Default mode is 0, ASM_MODE will be 1
        PushMode(1); // ASM_MODE = 1
    }

    /// <summary>
    /// Called by parser when exiting an asm block.
    /// Pops back to the previous mode.
    /// </summary>
    public void ExitAsmMode()
    {
        if (ModeStack.Count > 0)
        {
            PopMode();
        }
    }

    /// <summary>
    /// Check if we're currently in assembly mode.
    /// </summary>
    public bool IsInAsmMode => CurrentMode == 1; // ASM_MODE = 1
}
