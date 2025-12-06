using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.HIR;

/// <summary>
/// Validates HIR instructions before lowering.
/// Catches errors early before code generation.
/// </summary>
public class HirValidator
{
    private readonly DiagnosticBag _diagnostics;
    private readonly ChipsetProfile _chipset;

    public HirValidator(DiagnosticBag diagnostics, ChipsetProfile chipset = ChipsetProfile.Auto)
    {
        _diagnostics = diagnostics;
        _chipset = chipset;
    }

    /// <summary>
    /// Validate all HIR instructions in a module.
    /// </summary>
    /// <returns>True if all validations pass.</returns>
    public bool ValidateModule(IrModule module)
    {
        var isValid = true;

        foreach (var hir in module.HirInstructions)
        {
            isValid &= ValidateInstruction(hir);
        }

        return isValid;
    }

    /// <summary>
    /// Validate a single HIR instruction.
    /// </summary>
    public bool ValidateInstruction(HirInstruction instruction)
    {
        return instruction switch
        {
            HirCopperList copper => ValidateCopperList(copper),
            HirBlitterJob blitter => ValidateBlitterJob(blitter),
            HirAsyncFunction asyncFn => ValidateAsyncFunction(asyncFn),
            _ => true // Unknown instruction types pass by default
        };
    }

    #region Copper Validation

    private bool ValidateCopperList(HirCopperList copper)
    {
        var isValid = true;
        var waitPositions = new List<(int vertical, int horizontal)>();

        for (int i = 0; i < copper.Instructions.Count; i++)
        {
            var instr = copper.Instructions[i];
            isValid &= ValidateCopperInstruction(instr, i, waitPositions);
        }

        // Validate that wait positions are monotonically increasing
        if (waitPositions.Count > 1)
        {
            isValid &= ValidateWaitSequence(waitPositions);
        }

        return isValid;
    }

    private bool ValidateCopperInstruction(HirCopperInstruction instruction, int index, List<(int, int)> waitPositions)
    {
        switch (instruction)
        {
            case HirCopperInstruction.Wait wait:
                return ValidateCopperWait(wait, index, waitPositions);

            case HirCopperInstruction.Move move:
                return ValidateCopperMove(move, index);

            case HirCopperInstruction.Skip skip:
                return ValidateCopperSkip(skip, index);

            default:
                return true;
        }
    }

    private bool ValidateCopperWait(HirCopperInstruction.Wait wait, int index, List<(int, int)> waitPositions)
    {
        // Try to evaluate constant values
        if (!TryEvaluateConstant(wait.X, out int horizontal))
        {
            // Can't validate runtime values at compile time
            return true;
        }

        if (!TryEvaluateConstant(wait.Y, out int vertical))
        {
            return true;
        }

        // Validate position using ChipsetCapabilities
        if (!ChipsetCapabilities.IsWaitPositionValid(vertical, horizontal, _chipset, out var error))
        {
            _diagnostics.ReportError(
                ErrorCodes.CopperWaitOutOfRange,
                $"Copper WAIT instruction {index}: {error}",
                CreateLocation()
            );
            return false;
        }

        waitPositions.Add((vertical, horizontal));
        return true;
    }

    private bool ValidateCopperMove(HirCopperInstruction.Move move, int index)
    {
        if (!TryEvaluateConstant(move.Register, out int register))
        {
            return true; // Can't validate runtime values
        }

        uint regAddr = (uint)register;

        // Check for read-only registers
        var readOnlyRegisters = new HashSet<uint>
        {
            0xDFF000, // BLTDDAT
            0xDFF002, // DMACONR
            0xDFF004, // VPOSR
            0xDFF006, // VHPOSR
            0xDFF00A, // JOY0DAT
            0xDFF00C, // JOY1DAT
        };

        if (readOnlyRegisters.Contains(regAddr))
        {
            _diagnostics.ReportError(
                ErrorCodes.CopperMoveToReadOnly,
                $"Copper MOVE instruction {index}: Register ${regAddr:X4} is read-only",
                CreateLocation()
            );
            return false;
        }

        // Check for dangerous registers
        var dangerousRegisters = new HashSet<uint>
        {
            0xDFF080, // COP1LCH
            0xDFF082, // COP1LCL
            0xDFF084, // COP2LCH
            0xDFF086, // COP2LCL
            0xDFF088, // COPJMP1
            0xDFF08A, // COPJMP2
        };

        if (dangerousRegisters.Contains(regAddr))
        {
            _diagnostics.ReportError(
                ErrorCodes.CopperMoveToDangerous,
                $"Copper MOVE instruction {index}: Writing to register ${regAddr:X4} from copper list is dangerous",
                CreateLocation(),
                helpTexts: new List<string>
                {
                    "COP1LC/COP2LC and COPJMP registers should only be modified from CPU code",
                    "Modifying these in a copper list can cause infinite loops or unpredictable behavior"
                }
            );
            return false;
        }

        return true;
    }

    private bool ValidateCopperSkip(HirCopperInstruction.Skip skip, int index)
    {
        // Similar validation to WAIT
        if (!TryEvaluateConstant(skip.X, out int horizontal) ||
            !TryEvaluateConstant(skip.Y, out int vertical))
        {
            return true;
        }

        if (!ChipsetCapabilities.IsWaitPositionValid(vertical, horizontal, _chipset, out var error))
        {
            _diagnostics.ReportError(
                ErrorCodes.CopperWaitOutOfRange,
                $"Copper SKIP instruction {index}: {error}",
                CreateLocation()
            );
            return false;
        }

        return true;
    }

    private bool ValidateWaitSequence(List<(int vertical, int horizontal)> positions)
    {
        for (int i = 1; i < positions.Count; i++)
        {
            var prev = positions[i - 1];
            var curr = positions[i];

            if (curr.vertical < prev.vertical ||
                (curr.vertical == prev.vertical && curr.horizontal < prev.horizontal))
            {
                _diagnostics.ReportError(
                    ErrorCodes.CopperWaitNotMonotonic,
                    $"Copper WAIT at ({curr.horizontal}, {curr.vertical}) is before previous WAIT at ({prev.horizontal}, {prev.vertical})",
                    CreateLocation(),
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

    #endregion

    #region Blitter Validation

    private bool ValidateBlitterJob(HirBlitterJob blitter)
    {
        var isValid = true;

        // Validate size
        if (blitter.Fields.TryGetValue("width", out var widthVal) &&
            TryEvaluateConstant(widthVal, out int width))
        {
            if (width < 1 || width > 1024)
            {
                _diagnostics.ReportError(
                    ErrorCodes.BlitterSizeOutOfRange,
                    $"Blitter width {width} out of range (1-1024 pixels)",
                    CreateLocation()
                );
                isValid = false;
            }
            else if (_chipset != ChipsetProfile.AGA && width % 16 != 0)
            {
                _diagnostics.ReportError(
                    ErrorCodes.BlitterWidthNotAligned,
                    $"Blitter width {width} must be multiple of 16 for {_chipset} chipset",
                    CreateLocation()
                );
                isValid = false;
            }
        }

        if (blitter.Fields.TryGetValue("height", out var heightVal) &&
            TryEvaluateConstant(heightVal, out int height))
        {
            if (height < 1 || height > 1024)
            {
                _diagnostics.ReportError(
                    ErrorCodes.BlitterSizeOutOfRange,
                    $"Blitter height {height} out of range (1-1024 lines)",
                    CreateLocation()
                );
                isValid = false;
            }
        }

        // Validate minterm and channel usage
        if (blitter.Fields.TryGetValue("minterm", out var mintermVal) &&
            TryEvaluateConstant(mintermVal, out int minterm))
        {
            isValid &= ValidateMintermChannels(blitter, (byte)minterm);
        }

        // Validate destination exists
        if (blitter.Destination == null &&
            !blitter.Fields.ContainsKey("dest") &&
            !blitter.Fields.ContainsKey("destination"))
        {
            _diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                "Blitter job must have a destination pointer",
                CreateLocation()
            );
            isValid = false;
        }

        return isValid;
    }

    private bool ValidateMintermChannels(HirBlitterJob blitter, byte minterm)
    {
        var isValid = true;
        var missingChannels = new List<string>();

        // Check if minterm requires channel A
        if (RequiresChannelA(minterm) && blitter.SourceA == null &&
            !blitter.Fields.ContainsKey("source") && !blitter.Fields.ContainsKey("sourcea"))
        {
            missingChannels.Add("A");
        }

        // Check if minterm requires channel B
        if (RequiresChannelB(minterm) && blitter.SourceB == null &&
            !blitter.Fields.ContainsKey("sourceb"))
        {
            missingChannels.Add("B");
        }

        // Check if minterm requires channel C
        if (RequiresChannelC(minterm) && blitter.SourceC == null &&
            !blitter.Fields.ContainsKey("sourcec"))
        {
            missingChannels.Add("C");
        }

        if (missingChannels.Count > 0)
        {
            _diagnostics.ReportError(
                ErrorCodes.InvalidHardwareOperation,
                $"Blitter minterm 0x{minterm:X2} requires missing channels: {string.Join(", ", missingChannels)}",
                CreateLocation()
            );
            isValid = false;
        }

        return isValid;
    }

    private bool RequiresChannelA(byte minterm)
    {
        byte upperNibble = (byte)((minterm >> 4) & 0x0F);
        byte lowerNibble = (byte)(minterm & 0x0F);
        return upperNibble != lowerNibble;
    }

    private bool RequiresChannelB(byte minterm)
    {
        return ((minterm & 0xCC) >> 2) != (minterm & 0x33);
    }

    private bool RequiresChannelC(byte minterm)
    {
        return ((minterm & 0xAA) >> 1) != (minterm & 0x55);
    }

    #endregion

    #region Async Validation

    private bool ValidateAsyncFunction(HirAsyncFunction asyncFn)
    {
        var isValid = true;

        // Validate function name
        if (string.IsNullOrEmpty(asyncFn.FunctionName))
        {
            _diagnostics.ReportError(
                ErrorCodes.InternalCompilerError,
                "Async function has empty name",
                CreateLocation()
            );
            isValid = false;
        }

        // Validate await points have sequential state numbers
        var stateNumbers = asyncFn.AwaitPoints.Select(ap => ap.StateNumber).ToList();
        for (int i = 0; i < stateNumbers.Count; i++)
        {
            if (stateNumbers[i] != i)
            {
                _diagnostics.ReportError(
                    ErrorCodes.InternalCompilerError,
                    $"Async function {asyncFn.FunctionName} has non-sequential state numbers",
                    CreateLocation()
                );
                isValid = false;
                break;
            }
        }

        return isValid;
    }

    #endregion

    #region Helpers

    private bool TryEvaluateConstant(IrValue value, out int result)
    {
        result = 0;

        switch (value)
        {
            case IrConstant constant:
                result = (int)constant.Value;
                return true;

            case IrCastValue cast:
                return TryEvaluateConstant(cast.Value, out result);

            default:
                return false;
        }
    }

    private SourceLocation CreateLocation()
    {
        // HIR instructions don't currently store source location
        // Return a placeholder
        return new SourceLocation("", 0, 0, 0, "");
    }

    #endregion
}
