using Novus.Diagnostics;
using Novus.Parser;

namespace Novus.SemanticAnalysis.Validators;

/// <summary>
/// Validates Blitter DSL usage for Amiga hardware.
/// The Blitter is a DMA co-processor that can perform:
/// - Rectangle copy/fill operations
/// - Line drawing
/// - Boolean logic operations (via minterm)
///
/// Validates:
/// - Operation sizes within hardware limits
/// - Minterm/channel consistency
/// - Alignment requirements for chipset
/// - Chipset compatibility (OCS/ECS/AGA)
/// </summary>
public class BlitterDslValidator : ValidatorBase
{
    /// <summary>
    /// Target chipset for validation.
    /// </summary>
    public ChipsetProfile Chipset { get; set; } = ChipsetProfile.Auto;

    public override string Name => "Blitter DSL Validator";

    public override bool Validate(NovusParser.CompilationUnitContext context, DiagnosticBag diagnostics)
    {
        // Find all blitter blocks in the compilation unit and validate them
        // For now, this is a structural validation - actual runtime values
        // cannot be validated at compile time unless they are constants
        //
        // Future: Integrate with HIR blitter job generation for compile-time
        // constant blitter operations

        return true; // No errors found at AST level
    }

    /// <summary>
    /// Validate blitter operation size.
    /// </summary>
    public bool ValidateSize(int width, int height, DiagnosticBag diagnostics, SourceLocation location)
    {
        if (!ChipsetCapabilities.IsBlitterSizeValid(width, height, Chipset, out var error))
        {
            diagnostics.ReportError(
                ErrorCodes.BlitterSizeOutOfRange,
                $"Invalid Blitter size: {error}",
                location,
                helpTexts: new List<string>
                {
                    $"Current chipset target: {Chipset}",
                    "Blitter width must be 1-1024 pixels, height 1-1024 lines",
                    Chipset != ChipsetProfile.AGA
                        ? "OCS/ECS requires width to be multiple of 16 (word-aligned)"
                        : "AGA allows byte-aligned width"
                }
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate minterm value and required channels.
    /// </summary>
    public bool ValidateMinterm(
        byte minterm,
        bool hasSourceA,
        bool hasSourceB,
        bool hasSourceC,
        DiagnosticBag diagnostics,
        SourceLocation location)
    {
        var errors = new List<string>();

        if (RequiresChannelA(minterm) && !hasSourceA)
        {
            errors.Add("Source A required but not provided");
        }

        if (RequiresChannelB(minterm) && !hasSourceB)
        {
            errors.Add("Source B required but not provided");
        }

        if (RequiresChannelC(minterm) && !hasSourceC)
        {
            errors.Add("Source C required but not provided");
        }

        if (errors.Count > 0)
        {
            var mintermDesc = CommonMinterms.TryGetValue(minterm, out var desc)
                ? $" ({desc})"
                : "";

            diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                $"Blitter minterm 0x{minterm:X2}{mintermDesc} requires missing channels: {string.Join(", ", errors)}",
                location,
                helpTexts: new List<string>
                {
                    "Minterm is an 8-bit truth table that determines how A, B, C inputs combine",
                    "Each channel that affects the output must have valid source data",
                    GetMintermHelp(minterm)
                }
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate that a pointer is word-aligned for OCS/ECS.
    /// </summary>
    public bool ValidateAlignment(uint address, string pointerName, DiagnosticBag diagnostics, SourceLocation location)
    {
        if (Chipset != ChipsetProfile.AGA && (address & 1) != 0)
        {
            diagnostics.ReportError(
                ErrorCodes.BlitterWidthNotAligned,
                $"Blitter {pointerName} address 0x{address:X8} is not word-aligned",
                location,
                helpTexts: new List<string>
                {
                    "OCS/ECS Blitter requires word-aligned (even) addresses",
                    "Use #[target(chipset = \"AGA\")] for byte-aligned access"
                }
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate modulo value is within range.
    /// </summary>
    public bool ValidateModulo(int modulo, string channelName, DiagnosticBag diagnostics, SourceLocation location)
    {
        // Modulo is a 16-bit signed value (-32768 to 32767)
        if (modulo < -32768 || modulo > 32767)
        {
            diagnostics.ReportError(
                ErrorCodes.BlitterSizeOutOfRange,
                $"Blitter {channelName} modulo {modulo} out of range (-32768 to 32767)",
                location
            );
            return false;
        }

        // OCS/ECS require word-aligned modulo
        if (Chipset != ChipsetProfile.AGA && (modulo & 1) != 0)
        {
            diagnostics.ReportError(
                ErrorCodes.BlitterWidthNotAligned,
                $"Blitter {channelName} modulo {modulo} must be word-aligned for {Chipset}",
                location,
                helpTexts: new List<string>
                {
                    "OCS/ECS Blitter requires word-aligned modulo values",
                    "Modulo is added after each line to handle interleaved bitmaps"
                }
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate line mode parameters.
    /// </summary>
    public bool ValidateLineMode(
        int x1, int y1, int x2, int y2,
        DiagnosticBag diagnostics,
        SourceLocation location)
    {
        // Line mode uses different constraints
        // Length must fit in 11 bits (0-2047)
        var dx = Math.Abs(x2 - x1);
        var dy = Math.Abs(y2 - y1);
        var length = Math.Max(dx, dy);

        if (length > 2047)
        {
            diagnostics.ReportError(
                ErrorCodes.BlitterSizeOutOfRange,
                $"Blitter line length {length} exceeds maximum (2047 pixels)",
                location,
                helpTexts: new List<string>
                {
                    "Blitter line mode is limited to 2047 pixels per operation",
                    "For longer lines, split into multiple blitter operations"
                }
            );
            return false;
        }

        return true;
    }

    private string GetMintermHelp(byte minterm)
    {
        if (CommonMinterms.TryGetValue(minterm, out var desc))
        {
            return $"Minterm 0x{minterm:X2} = {desc}";
        }

        var parts = new List<string>();
        if (RequiresChannelA(minterm)) parts.Add("A");
        if (RequiresChannelB(minterm)) parts.Add("B");
        if (RequiresChannelC(minterm)) parts.Add("C");

        return parts.Count > 0
            ? $"Minterm 0x{minterm:X2} uses channels: {string.Join(", ", parts)}"
            : $"Minterm 0x{minterm:X2} produces constant output";
    }

    /// <summary>
    /// Check if a minterm uses a specific channel
    /// Minterm is an 8-bit truth table for boolean operations
    /// </summary>
    private bool MintermUsesChannel(byte minterm, char channel)
    {
        // Minterm bit positions:
        // Bit 7: A=1, B=1, C=1
        // Bit 6: A=1, B=1, C=0
        // Bit 5: A=1, B=0, C=1
        // Bit 4: A=1, B=0, C=0
        // Bit 3: A=0, B=1, C=1
        // Bit 2: A=0, B=1, C=0
        // Bit 1: A=0, B=0, C=1
        // Bit 0: A=0, B=0, C=0

        return channel switch
        {
            'A' => RequiresChannelA(minterm),
            'B' => RequiresChannelB(minterm),
            'C' => RequiresChannelC(minterm),
            _ => false
        };
    }

    private bool RequiresChannelA(byte minterm)
    {
        // Channel A is required if output differs based on A input
        // Check if upper nibble differs from lower nibble
        byte upperNibble = (byte)((minterm >> 4) & 0x0F);
        byte lowerNibble = (byte)(minterm & 0x0F);
        return upperNibble != lowerNibble;
    }

    private bool RequiresChannelB(byte minterm)
    {
        // Channel B is required if output differs based on B input
        // Check if bits differ between pairs: (7,5), (6,4), (3,1), (2,0)
        return ((minterm & 0xCC) >> 2) != (minterm & 0x33);
    }

    private bool RequiresChannelC(byte minterm)
    {
        // Channel C is required if output differs based on C input
        // Check if odd bits differ from even bits
        return ((minterm & 0xAA) >> 1) != (minterm & 0x55);
    }

    /// <summary>
    /// Common minterms and their meanings
    /// </summary>
    private static readonly Dictionary<byte, string> CommonMinterms = new()
    {
        { 0x00, "Clear (all zeros)" },
        { 0xF0, "Copy A" },
        { 0xCC, "Copy B" },
        { 0xAA, "Copy C" },
        { 0xFC, "A OR D" },
        { 0xC0, "A AND D" },
        { 0x5A, "A XOR D" },
        { 0x4A, "A AND (NOT B) AND D" },
        { 0xCA, "Pattern fill (A OR (B AND C))" },
        { 0xFF, "Set (all ones)" },
    };
}
