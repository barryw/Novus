using Novus.IR;
using Novus.HIR;

namespace Novus.Transforms.Passes;

/// <summary>
/// Lowers HIR Blitter jobs to LIR (68k assembly with hardware register access)
/// Blitter is Amiga-specific co-processor for fast memory operations
/// </summary>
public class BlitterLoweringPass : TransformPassBase
{
    public override string Name => "Blitter Lowering";

    // Custom chip base address
    private const uint CUSTOM_BASE = 0xDFF000;

    // Custom chip register addresses (offset from $DFF000)
    private const ushort DMACONR = 0x002;   // DMA control read
    private const ushort BLTCON0 = 0x040;   // Blitter control register 0
    private const ushort BLTCON1 = 0x042;   // Blitter control register 1
    private const ushort BLTAFWM = 0x044;   // Blitter first word mask for source A
    private const ushort BLTALWM = 0x046;   // Blitter last word mask for source A
    private const ushort BLTCPTH = 0x048;   // Blitter pointer to source C (high)
    private const ushort BLTCPTL = 0x04A;   // Blitter pointer to source C (low)
    private const ushort BLTBPTH = 0x04C;   // Blitter pointer to source B (high)
    private const ushort BLTBPTL = 0x04E;   // Blitter pointer to source B (low)
    private const ushort BLTAPTH = 0x050;   // Blitter pointer to source A (high)
    private const ushort BLTAPTL = 0x052;   // Blitter pointer to source A (low)
    private const ushort BLTDPTH = 0x054;   // Blitter pointer to destination D (high)
    private const ushort BLTDPTL = 0x056;   // Blitter pointer to destination D (low)
    private const ushort BLTSIZE = 0x058;   // Blitter start and size (starts blit!)
    private const ushort BLTCMOD = 0x060;   // Blitter modulo for source C
    private const ushort BLTBMOD = 0x062;   // Blitter modulo for source B
    private const ushort BLTAMOD = 0x064;   // Blitter modulo for source A
    private const ushort BLTDMOD = 0x066;   // Blitter modulo for destination D
    private const ushort BLTAPT = 0x050;    // Blitter A pointer (long write)
    private const ushort BLTBPT = 0x04C;    // Blitter B pointer (long write)
    private const ushort BLTCPT = 0x048;    // Blitter C pointer (long write)
    private const ushort BLTDPT = 0x054;    // Blitter D pointer (long write)

    // Common minterms
    private const byte MINTERM_COPY = 0xF0;     // Copy A to D (A)
    private const byte MINTERM_CLEAR = 0x00;   // Clear D (0)
    private const byte MINTERM_OR = 0xFC;      // D = A | D
    private const byte MINTERM_AND = 0xC0;     // D = A & D
    private const byte MINTERM_XOR = 0x5A;     // D = A ^ D
    private const byte MINTERM_COOKIE = 0xCA;  // Cookie-cut: D = (A & B) | (C & ~B)

    private int _blitterOpCounter = 0;

    public override bool Transform(IrModule module)
    {
        bool changed = false;

        // Find HIR instructions that need lowering
        foreach (var hirInstruction in module.HirInstructions.ToList())
        {
            if (hirInstruction is HirBlitterJob blitterJob)
            {
                // Lower this blitter job
                LowerBlitterJob(module, blitterJob);

                module.HirInstructions.Remove(hirInstruction);
                changed = true;
            }
        }

        return changed;
    }

    private void LowerBlitterJob(IrModule module, HirBlitterJob job)
    {
        // Validate the job
        ValidateBlitterJob(job);

        // Extract blitter parameters from the DSL fields
        var blitData = ExtractBlitterData(job);

        // Find the function containing this blitter job and generate lowered instructions
        // For now, we generate a call to a runtime helper function
        // This is safer than inline hardware writes because it handles ownership correctly

        if (job.ResultName != null)
        {
            // Update the result variable with the blitter operation data
            // The C code generator will emit calls to OwnBlitter/WaitBlit/DisownBlitter
            // and direct register writes based on this data
            UpdateBlitterVariableInit(module, job.ResultName, blitData);
        }
    }

    /// <summary>
    /// Extract blitter parameters from DSL fields into IrBlitterOpData
    /// </summary>
    private IrBlitterOpData ExtractBlitterData(HirBlitterJob job)
    {
        var blitData = new IrBlitterOpData();

        // Extract width and height
        int widthPixels = 16;  // Default
        int heightLines = 16;  // Default

        if (job.Fields.TryGetValue("width", out var widthVal) && TryEvaluateConstant(widthVal, out int w))
            widthPixels = w;
        if (job.Fields.TryGetValue("height", out var heightVal) && TryEvaluateConstant(heightVal, out int h))
            heightLines = h;

        // Width in words (16 pixels per word for 1bpp)
        int widthWords = (widthPixels + 15) / 16;

        // BLTSIZE format: height (10 bits) << 6 | width_words (6 bits)
        blitData.BltSize = (ushort)((heightLines << 6) | widthWords);

        // Extract minterm
        byte minterm = MINTERM_COPY;  // Default to copy
        if (job.Fields.TryGetValue("minterm", out var mintermVal) && TryEvaluateConstant(mintermVal, out int mt))
            minterm = (byte)(mt & 0xFF);

        // Determine which channels are used
        bool useA = job.SourceA != null || job.Fields.ContainsKey("source") || job.Fields.ContainsKey("sourcea");
        bool useB = job.SourceB != null || job.Fields.ContainsKey("sourceb");
        bool useC = job.SourceC != null || job.Fields.ContainsKey("sourcec");
        bool useD = job.Destination != null || job.Fields.ContainsKey("dest") || job.Fields.ContainsKey("destination");

        // BLTCON0: ASH3-0 | USEA | USEB | USEC | USED | LF7-0
        // Bits 15-12: A shift value
        // Bit 11: USEA
        // Bit 10: USEB
        // Bit 9: USEC
        // Bit 8: USED
        // Bits 7-0: Logic function (minterm)
        ushort bltcon0 = minterm;
        if (useA) bltcon0 |= 0x0800;  // USEA
        if (useB) bltcon0 |= 0x0400;  // USEB
        if (useC) bltcon0 |= 0x0200;  // USEC
        if (useD) bltcon0 |= 0x0100;  // USED

        // Extract shift value for source A
        if (job.Fields.TryGetValue("shift", out var shiftVal) && TryEvaluateConstant(shiftVal, out int shift))
            bltcon0 |= (ushort)((shift & 0x0F) << 12);

        blitData.BltCon0 = bltcon0;

        // BLTCON1: BSH3-0 | 0 | EFE | IFE | FCI | DESC | LINE | 0
        // Bits 15-12: B shift value
        // Bit 4: EFE (Exclusive fill enable)
        // Bit 3: IFE (Inclusive fill enable)
        // Bit 2: FCI (Fill carry in)
        // Bit 1: DESC (Descending mode)
        // Bit 0: LINE (Line draw mode)
        ushort bltcon1 = 0;

        // Check for descending mode
        if (job.Fields.TryGetValue("descending", out var descVal) &&
            descVal is IrBoolConstant descBool && descBool.Value)
            bltcon1 |= 0x0002;  // DESC

        // Check for fill mode
        if (job.Fields.TryGetValue("fill", out var fillVal) &&
            fillVal is IrBoolConstant fillBool && fillBool.Value)
            bltcon1 |= 0x0008;  // IFE (inclusive fill)

        blitData.BltCon1 = bltcon1;

        // Extract source/dest pointers
        blitData.SourceA = job.SourceA ?? job.Fields.GetValueOrDefault("source") ?? job.Fields.GetValueOrDefault("sourcea");
        blitData.SourceB = job.SourceB ?? job.Fields.GetValueOrDefault("sourceb");
        blitData.SourceC = job.SourceC ?? job.Fields.GetValueOrDefault("sourcec");
        blitData.Destination = job.Destination ?? job.Fields.GetValueOrDefault("dest") ?? job.Fields.GetValueOrDefault("destination");

        // Extract modulos (default 0 for contiguous memory)
        if (job.Fields.TryGetValue("moda", out var modaVal) && TryEvaluateConstant(modaVal, out int modA))
            blitData.ModuloA = (short)modA;
        if (job.Fields.TryGetValue("modb", out var modbVal) && TryEvaluateConstant(modbVal, out int modB))
            blitData.ModuloB = (short)modB;
        if (job.Fields.TryGetValue("modc", out var modcVal) && TryEvaluateConstant(modcVal, out int modC))
            blitData.ModuloC = (short)modC;
        if (job.Fields.TryGetValue("modd", out var moddVal) && TryEvaluateConstant(moddVal, out int modD))
            blitData.ModuloD = (short)modD;

        // Check wait flag
        if (job.Fields.TryGetValue("wait", out var waitVal))
        {
            if (waitVal is IrBoolConstant waitBool)
                blitData.WaitForCompletion = waitBool.Value;
        }

        return blitData;
    }

    /// <summary>
    /// Generate lowered IR instructions for a blitter operation.
    /// Returns a list of instructions to insert into the function.
    /// </summary>
    public List<IrInstruction> GenerateBlitterInstructions(IrBlitterOpData blitData)
    {
        var instructions = new List<IrInstruction>();
        var tempCounter = _blitterOpCounter++;

        // Generate wait-for-idle loop label
        var waitLabel = $"__blit_wait_{tempCounter}";

        // 1. Wait for blitter idle (poll DMACONR bit 14)
        // This generates a spin loop checking BLTBUSY bit
        instructions.Add(new IrLabel(waitLabel));

        // Read DMACONR
        var dmaconrVar = $"__dmaconr_{tempCounter}";
        instructions.Add(new IrHardwareRead(dmaconrVar, CUSTOM_BASE + DMACONR, 2));

        // Check bit 14 (0x4000)
        var busyCheckVar = $"__busy_{tempCounter}";
        instructions.Add(new IrBinaryOp(
            busyCheckVar,
            IrBinaryOp.OpKind.And,
            new IrVariable(dmaconrVar, IrIntType.U16),
            new IrConstant(0x4000, IrIntType.U16),
            IrIntType.U16
        ));

        // Branch back if still busy
        instructions.Add(new IrConditionalBranch(
            new IrVariable(busyCheckVar, IrIntType.U16),
            waitLabel,
            $"__blit_ready_{tempCounter}"
        ));

        instructions.Add(new IrLabel($"__blit_ready_{tempCounter}"));

        // 2. Write control registers (BLTCON0, BLTCON1)
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTCON1, new IrConstant(blitData.BltCon1, IrIntType.U16)));
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTCON0, new IrConstant(blitData.BltCon0, IrIntType.U16)));

        // 3. Write word masks (full words by default)
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTAFWM, new IrConstant(0xFFFF, IrIntType.U16)));
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTALWM, new IrConstant(0xFFFF, IrIntType.U16)));

        // 4. Write modulos
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTAMOD, new IrConstant(blitData.ModuloA, IrIntType.I16)));
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTBMOD, new IrConstant(blitData.ModuloB, IrIntType.I16)));
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTCMOD, new IrConstant(blitData.ModuloC, IrIntType.I16)));
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTDMOD, new IrConstant(blitData.ModuloD, IrIntType.I16)));

        // 5. Write source/dest pointers (long writes)
        if (blitData.SourceA != null)
        {
            instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTAPT, blitData.SourceA, 4));
        }
        if (blitData.SourceB != null)
        {
            instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTBPT, blitData.SourceB, 4));
        }
        if (blitData.SourceC != null)
        {
            instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTCPT, blitData.SourceC, 4));
        }
        if (blitData.Destination != null)
        {
            instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTDPT, blitData.Destination, 4));
        }

        // 6. Write BLTSIZE (triggers the blit!)
        instructions.Add(new IrHardwareWrite(CUSTOM_BASE + BLTSIZE, new IrConstant(blitData.BltSize, IrIntType.U16)));

        // 7. Optionally wait for completion
        if (blitData.WaitForCompletion)
        {
            var waitDoneLabel = $"__blit_done_wait_{tempCounter}";
            instructions.Add(new IrLabel(waitDoneLabel));

            var doneVar = $"__dmaconr_done_{tempCounter}";
            instructions.Add(new IrHardwareRead(doneVar, CUSTOM_BASE + DMACONR, 2));

            var doneBusyVar = $"__done_busy_{tempCounter}";
            instructions.Add(new IrBinaryOp(
                doneBusyVar,
                IrBinaryOp.OpKind.And,
                new IrVariable(doneVar, IrIntType.U16),
                new IrConstant(0x4000, IrIntType.U16),
                IrIntType.U16
            ));

            instructions.Add(new IrConditionalBranch(
                new IrVariable(doneBusyVar, IrIntType.U16),
                waitDoneLabel,
                $"__blit_complete_{tempCounter}"
            ));

            instructions.Add(new IrLabel($"__blit_complete_{tempCounter}"));
        }

        return instructions;
    }

    private void ValidateBlitterJob(HirBlitterJob job)
    {
        // Ensure destination is specified
        bool hasDest = job.Destination != null ||
                       job.Fields.ContainsKey("dest") ||
                       job.Fields.ContainsKey("destination");

        if (!hasDest)
        {
            throw new InvalidOperationException("Blitter job must have a destination pointer");
        }

        // Validate size constraints
        if (job.Fields.TryGetValue("width", out var widthVal) && TryEvaluateConstant(widthVal, out int width))
        {
            if (width < 1 || width > 1024)
            {
                throw new InvalidOperationException($"Blitter width {width} out of range (1-1024 pixels)");
            }
        }

        if (job.Fields.TryGetValue("height", out var heightVal) && TryEvaluateConstant(heightVal, out int height))
        {
            if (height < 1 || height > 1024)
            {
                throw new InvalidOperationException($"Blitter height {height} out of range (1-1024 lines)");
            }
        }
    }

    /// <summary>
    /// Try to evaluate an IrValue to a compile-time constant integer
    /// </summary>
    private bool TryEvaluateConstant(IrValue value, out int result)
    {
        result = 0;

        switch (value)
        {
            case IrConstant constant:
                result = (int)constant.Value;
                return true;

            case IrVariable variable:
                // Could look up constant values from module.Constants
                // For now, treat variables as non-constant
                return false;

            case IrCastValue cast:
                // Recursively evaluate the cast source
                return TryEvaluateConstant(cast.Value, out result);

            default:
                return false;
        }
    }

    /// <summary>
    /// Update a blitter variable's initialization to use the extracted data
    /// </summary>
    private void UpdateBlitterVariableInit(IrModule module, string varName, IrBlitterOpData blitData)
    {
        foreach (var function in module.Functions)
        {
            foreach (var block in function.BasicBlocks)
            {
                for (int i = 0; i < block.Instructions.Count; i++)
                {
                    if (block.Instructions[i] is IrLocalDecl localDecl && localDecl.Name == varName)
                    {
                        // Replace the placeholder initialization with the actual blitter operation
                        // Generate and insert the lowered instructions before the declaration
                        var blitInstructions = GenerateBlitterInstructions(blitData);

                        // Insert all blitter instructions before the current position
                        for (int j = 0; j < blitInstructions.Count; j++)
                        {
                            block.Instructions.Insert(i + j, blitInstructions[j]);
                        }

                        // Update the local decl to have unit type (blitter doesn't return a value)
                        localDecl.Type = IrTupleType.Unit;
                        localDecl.InitialValue = null!;

                        return;
                    }
                }
            }
        }
    }
}
