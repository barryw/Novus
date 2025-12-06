using Novus.Diagnostics;
using Novus.Parser;

namespace Novus.SemanticAnalysis.Validators;

/// <summary>
/// Validates Copper DSL usage for Amiga hardware.
/// The Copper is a display list co-processor that can modify chip registers
/// during the display beam scan.
///
/// Validates:
/// - WAIT positions within chipset capabilities
/// - MOVE operations to valid registers
/// - Dangerous register access prevention
/// - Chipset compatibility (OCS/ECS/AGA)
/// </summary>
public class CopperDslValidator : ValidatorBase
{
    /// <summary>
    /// Target chipset for validation.
    /// </summary>
    public ChipsetProfile Chipset { get; set; } = ChipsetProfile.Auto;

    public override string Name => "Copper DSL Validator";

    /// <summary>
    /// Dangerous registers that should not be written from Copper.
    /// Writing to these can cause infinite loops or system crashes.
    /// </summary>
    private static readonly HashSet<uint> DangerousRegisters = new()
    {
        0xDFF080,  // COP1LCH - Copper 1 location high (infinite loop risk)
        0xDFF082,  // COP1LCL - Copper 1 location low
        0xDFF084,  // COP2LCH - Copper 2 location high
        0xDFF086,  // COP2LCL - Copper 2 location low
        0xDFF088,  // COPJMP1 - Copper jump strobe 1
        0xDFF08A,  // COPJMP2 - Copper jump strobe 2
    };

    /// <summary>
    /// Read-only registers that cannot be written.
    /// </summary>
    private static readonly HashSet<uint> ReadOnlyRegisters = new()
    {
        0xDFF000,  // BLTDDAT - Blitter destination data (read only)
        0xDFF002,  // DMACONR - DMA control read
        0xDFF004,  // VPOSR - Vertical beam position read
        0xDFF006,  // VHPOSR - Vertical/horizontal beam position read
        0xDFF008,  // DSKDATR - Disk data read
        0xDFF00A,  // JOY0DAT - Joystick 0 data
        0xDFF00C,  // JOY1DAT - Joystick 1 data
        0xDFF00E,  // CLXDAT - Collision data
        0xDFF010,  // ADKCONR - Audio/disk control read
        0xDFF012,  // POT0DAT - Pot counter 0
        0xDFF014,  // POT1DAT - Pot counter 1
        0xDFF016,  // POTINP - Pot pin data read
        0xDFF018,  // SERDATR - Serial port data read
        0xDFF01A,  // DSKBYTR - Disk data byte read
        0xDFF01C,  // INTENAR - Interrupt enable read
        0xDFF01E,  // INTREQR - Interrupt request read
    };

    public override bool Validate(NovusParser.CompilationUnitContext context, DiagnosticBag diagnostics)
    {
        // Find all copper blocks in the compilation unit and validate them
        // For now, this is a structural validation - actual runtime values
        // cannot be validated at compile time unless they are constants
        //
        // Future: Integrate with HIR copper list generation for compile-time
        // constant copper lists

        return true; // No errors found at AST level
    }

    /// <summary>
    /// Validate a constant copper WAIT operation.
    /// </summary>
    public bool ValidateWait(int vertical, int horizontal, DiagnosticBag diagnostics, SourceLocation location)
    {
        if (!ChipsetCapabilities.IsWaitPositionValid(vertical, horizontal, Chipset, out var error))
        {
            diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                $"Invalid Copper WAIT position: {error}",
                location,
                helpTexts: new List<string>
                {
                    $"Current chipset target: {Chipset}",
                    "Use #[target(chipset = \"ECS\")] or #[target(chipset = \"AGA\")] for extended range"
                }
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate a copper MOVE operation.
    /// </summary>
    public bool ValidateMove(uint registerAddress, ushort value, DiagnosticBag diagnostics, SourceLocation location)
    {
        // Check if register is in custom chip range
        if (!ChipsetCapabilities.IsRegisterAccessible(registerAddress, Chipset))
        {
            diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                $"Copper MOVE to register ${registerAddress:X6} - not in custom chip range ($DFF000-$DFF1FF)",
                location
            );
            return false;
        }

        // Check for dangerous registers
        if (DangerousRegisters.Contains(registerAddress))
        {
            diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                $"Copper MOVE to register ${registerAddress:X4} is dangerous - can cause system hang or infinite loop",
                location,
                helpTexts: new List<string>
                {
                    "COP1LC/COP2LC and COPJMP registers should only be set from CPU code",
                    "Setting these in a copper list can create unpredictable behavior"
                }
            );
            return false;
        }

        // Check for read-only registers
        if (ReadOnlyRegisters.Contains(registerAddress))
        {
            diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                $"Copper MOVE to register ${registerAddress:X4} - register is read-only",
                location
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate that WAIT positions are monotonically increasing.
    /// The Copper cannot wait for a beam position earlier than the current one.
    /// </summary>
    public bool ValidateWaitSequence(
        List<(int vertical, int horizontal)> waitPositions,
        DiagnosticBag diagnostics,
        SourceLocation location)
    {
        for (int i = 1; i < waitPositions.Count; i++)
        {
            var prev = waitPositions[i - 1];
            var curr = waitPositions[i];

            // Check if current position is before previous
            if (curr.vertical < prev.vertical ||
                (curr.vertical == prev.vertical && curr.horizontal < prev.horizontal))
            {
                diagnostics.ReportError(
                    ErrorCodes.InvalidHardwareOperation,
                    $"Copper WAIT at ({curr.horizontal}, {curr.vertical}) is before previous WAIT at ({prev.horizontal}, {prev.vertical})",
                    location,
                    helpTexts: new List<string>
                    {
                        "Copper cannot wait for a beam position earlier than current",
                        "WAIT positions must be monotonically increasing within a frame"
                    }
                );
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validate a color register MOVE for chipset compatibility.
    /// </summary>
    public bool ValidateColorMove(int colorIndex, uint colorValue, DiagnosticBag diagnostics, SourceLocation location)
    {
        if (!ChipsetCapabilities.IsColorIndexValid(colorIndex, Chipset, out var indexError))
        {
            diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                $"Invalid color index: {indexError}",
                location
            );
            return false;
        }

        if (!ChipsetCapabilities.IsColorValueValid(colorValue, Chipset, out var valueError))
        {
            diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                $"Invalid color value: {valueError}",
                location
            );
            return false;
        }

        return true;
    }
}
