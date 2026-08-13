using Antlr4.Runtime.Misc;
using Novus.HIR;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing hardware DSL visitor methods.
/// This file contains methods for processing Amiga-specific hardware DSLs:
/// - Copper DSL: copper { wait(0, 100); move(COLOR00, $F00); }
/// - Blitter DSL: blitter { source: ptr, dest: screen, ... }
/// - Inline assembly expressions (delegates to shared ProcessInlineAssembly)
///
/// These DSLs provide type-safe, compile-time validated access to Amiga custom chips.
/// </summary>
public partial class IrBuilder
{
    // ===========================
    // Copper DSL Expression
    // ===========================

    /// <summary>
    /// Handle Copper DSL expression: copper { wait(0, 100); move(COLOR00, $F00); }
    /// Generates HIR Copper instructions that will be lowered to actual copper list data.
    /// </summary>
    public override object? VisitCopperExpr([NotNull] NovusParser.CopperExprContext context)
    {
        var copperList = context.copperList();
        var operations = new List<HirCopperInstruction>();

        // Process each copper operation
        foreach (var opContext in copperList.copperOperation())
        {
            // Get the operation name from copperOpName (IDENTIFIER)
            var opName = opContext.copperOpName().IDENTIFIER().GetText().ToLower();
            var arg0 = (IrValue?)Visit(opContext.expression(0));
            var arg1 = (IrValue?)Visit(opContext.expression(1));

            if (arg0 != null && arg1 != null)
            {
                switch (opName)
                {
                    case "wait":
                        operations.Add(new HirCopperInstruction.Wait(arg0, arg1));
                        break;
                    case "move":
                        operations.Add(new HirCopperInstruction.Move(arg0, arg1));
                        break;
                    case "skip":
                        operations.Add(new HirCopperInstruction.Skip(arg0, arg1));
                        break;
                }
            }
        }

        // Generate a temporary to hold the copper list pointer
        var resultTemp = $"%copper_{_tempCounter++}";
        var copperPtrType = new IrPointerType(IrIntType.U16);

        // Create HIR copper list node with result variable info
        var copperListNode = new HirCopperList(operations)
        {
            ResultName = resultTemp,
            ResultType = copperPtrType
        };

        // Add HIR instruction to module - will be lowered by CopperLoweringPass
        _module.HirInstructions.Add(copperListNode);

        // Declare local variable to hold the copper list pointer
        var resultLocal = new IrLocalVariable(resultTemp, copperPtrType, false);
        _currentFunction?.LocalVariables.Add(resultLocal);
        _currentBlock?.AddInstruction(new IrLocalDecl(resultTemp, copperPtrType, false, new IrConstant(0, IrIntType.U32)));

        return new IrVariable(resultTemp, copperPtrType);
    }

    // ===========================
    // Blitter DSL Expression
    // ===========================

    // Custom chip base address
    private const uint CUSTOM_BASE = 0xDFF000;

    // Blitter register offsets
    private const ushort DMACONR = 0x002;   // DMA control read (bit 14 = BLTBUSY)
    private const ushort BLTCON0 = 0x040;   // Blitter control register 0
    private const ushort BLTCON1 = 0x042;   // Blitter control register 1
    private const ushort BLTAFWM = 0x044;   // Blitter first word mask for source A
    private const ushort BLTALWM = 0x046;   // Blitter last word mask for source A
    private const ushort BLTCPT = 0x048;    // Blitter pointer to source C (32-bit)
    private const ushort BLTBPT = 0x04C;    // Blitter pointer to source B (32-bit)
    private const ushort BLTAPT = 0x050;    // Blitter pointer to source A (32-bit)
    private const ushort BLTDPT = 0x054;    // Blitter pointer to destination D (32-bit)
    private const ushort BLTSIZE = 0x058;   // Blitter start and size (starts blit!)
    private const ushort BLTCMOD = 0x060;   // Blitter modulo for source C
    private const ushort BLTBMOD = 0x062;   // Blitter modulo for source B
    private const ushort BLTAMOD = 0x064;   // Blitter modulo for source A
    private const ushort BLTDMOD = 0x066;   // Blitter modulo for destination D
    private const ushort BLTADAT = 0x074;   // Blitter source A data (pattern)

    // Common minterms
    private const byte MINTERM_COPY = 0xF0;     // D = A

    /// <summary>
    /// Handle Blitter DSL expression: blitter { source: ptr, dest: screen, width: 16, height: 16, minterm: $F0 }
    /// Generates inline blitter register write code.
    /// </summary>
    public override object? VisitBlitterExpr([NotNull] NovusParser.BlitterExprContext context)
    {
        var blitterJob = context.blitterJob();
        var fields = new Dictionary<string, IrValue>();

        // Process each blitter field
        foreach (var fieldContext in blitterJob.blitterField())
        {
            var fieldName = fieldContext.IDENTIFIER().GetText();
            var fieldValue = (IrValue?)Visit(fieldContext.expression());

            if (fieldValue != null)
            {
                fields[fieldName.ToLower()] = fieldValue;
            }
        }

        // Extract and validate blitter parameters
        IrValue? sourceA = fields.GetValueOrDefault("source") ?? fields.GetValueOrDefault("sourcea");
        IrValue? sourceB = fields.GetValueOrDefault("sourceb");
        IrValue? sourceC = fields.GetValueOrDefault("sourcec");
        IrValue? destination = fields.GetValueOrDefault("dest") ?? fields.GetValueOrDefault("destination");

        if (destination == null)
        {
            // Error: No destination specified - this will be caught by semantic analysis
            // For now, just return unit and don't generate any code
            return new IrTupleLiteral(IrTupleType.Unit, new List<IrValue>());
        }

        // Extract dimensions (default 16x16)
        int widthPixels = 16;
        int heightLines = 16;

        if (fields.TryGetValue("width", out var widthVal) && TryEvaluateConstant(widthVal, out int w))
            widthPixels = w;
        if (fields.TryGetValue("height", out var heightVal) && TryEvaluateConstant(heightVal, out int h))
            heightLines = h;

        // Width in words (16 pixels per word for 1bpp)
        int widthWords = (widthPixels + 15) / 16;

        // BLTSIZE format: height (10 bits) << 6 | width_words (6 bits)
        ushort bltSize = (ushort)((heightLines << 6) | widthWords);

        // Extract minterm (default copy)
        byte minterm = MINTERM_COPY;
        if (fields.TryGetValue("minterm", out var mintermVal) && TryEvaluateConstant(mintermVal, out int mt))
            minterm = (byte)(mt & 0xFF);

        // Determine which channels are used
        bool useA = sourceA != null;
        bool useB = sourceB != null;
        bool useC = sourceC != null;
        bool useD = true; // Always use destination

        // BLTCON0: ASH3-0 | USEA | USEB | USEC | USED | LF7-0
        ushort bltcon0 = minterm;
        if (useA) bltcon0 |= 0x0800;  // USEA
        if (useB) bltcon0 |= 0x0400;  // USEB
        if (useC) bltcon0 |= 0x0200;  // USEC
        if (useD) bltcon0 |= 0x0100;  // USED

        // Extract shift value for source A
        if (fields.TryGetValue("shift", out var shiftVal) && TryEvaluateConstant(shiftVal, out int shift))
            bltcon0 |= (ushort)((shift & 0x0F) << 12);

        // BLTCON1: BSH3-0 | 0 | EFE | IFE | FCI | DESC | LINE | 0
        ushort bltcon1 = 0;
        if (fields.TryGetValue("descending", out var descVal) && descVal is IrBoolConstant descBool && descBool.Value)
            bltcon1 |= 0x0002;  // DESC
        if (fields.TryGetValue("fill", out var fillVal) && fillVal is IrBoolConstant fillBool && fillBool.Value)
            bltcon1 |= 0x0008;  // IFE

        // Extract modulos (default 0)
        short modA = 0, modB = 0, modC = 0, modD = 0;
        if (fields.TryGetValue("moda", out var modaVal) && TryEvaluateConstant(modaVal, out int ma))
            modA = (short)ma;
        if (fields.TryGetValue("modb", out var modbVal) && TryEvaluateConstant(modbVal, out int mb))
            modB = (short)mb;
        if (fields.TryGetValue("modc", out var modcVal) && TryEvaluateConstant(modcVal, out int mc))
            modC = (short)mc;
        if (fields.TryGetValue("modd", out var moddVal) && TryEvaluateConstant(moddVal, out int md))
            modD = (short)md;

        // Check if we should wait for completion (default true)
        bool waitForCompletion = true;
        if (fields.TryGetValue("wait", out var waitVal) && waitVal is IrBoolConstant waitBool)
            waitForCompletion = waitBool.Value;

        // Generate blitter register write code
        GenerateBlitterCode(bltcon0, bltcon1, bltSize, sourceA, sourceB, sourceC, destination,
                            modA, modB, modC, modD, waitForCompletion);

        // Blitter jobs return unit (execute synchronously)
        return new IrTupleLiteral(IrTupleType.Unit, new List<IrValue>());
    }

    /// <summary>
    /// Generate IR instructions for blitter register setup.
    /// This generates straight-line code that writes to hardware registers.
    /// Note: Does NOT generate wait loops - users should call WaitBlit() from amiga::raw::graphics.
    /// </summary>
    private void GenerateBlitterCode(ushort bltcon0, ushort bltcon1, ushort bltSize,
                                      IrValue? sourceA, IrValue? sourceB, IrValue? sourceC, IrValue destination,
                                      short modA, short modB, short modC, short modD, bool waitForCompletion)
    {
        if (_currentBlock == null) return;

        var u16PtrType = new IrPointerType(IrIntType.U16);
        var u32PtrType = new IrPointerType(IrIntType.U32);
        var i16PtrType = new IrPointerType(IrIntType.I16);

        // Helper to create a hardware register write (16-bit)
        void WriteReg16(ushort offset, ushort value)
        {
            var addr = new IrConstant(CUSTOM_BASE + offset, IrIntType.U32);
            var ptr = new IrCastValue(addr, IrIntType.U32, u16PtrType);
            var val = new IrConstant(value, IrIntType.U16);
            _currentBlock?.AddInstruction(new IrDereferenceStore(ptr, val));
        }

        // Helper to create a hardware register write (32-bit pointer)
        void WriteReg32Ptr(ushort offset, IrValue ptrValue)
        {
            var addr = new IrConstant(CUSTOM_BASE + offset, IrIntType.U32);
            var ptr = new IrCastValue(addr, IrIntType.U32, u32PtrType);
            // Cast the pointer to u32 for storage
            var ptrAsU32 = new IrCastValue(ptrValue, ptrValue.Type, IrIntType.U32);
            _currentBlock?.AddInstruction(new IrDereferenceStore(ptr, ptrAsU32));
        }

        // Helper to create a modulo register write (signed 16-bit)
        void WriteModulo(ushort offset, short value)
        {
            var addr = new IrConstant(CUSTOM_BASE + offset, IrIntType.U32);
            var ptr = new IrCastValue(addr, IrIntType.U32, i16PtrType);
            var val = new IrConstant(value, IrIntType.I16);
            _currentBlock?.AddInstruction(new IrDereferenceStore(ptr, val));
        }

        // Note: We don't generate wait loops inline - the user should call WaitBlit()
        // from amiga::raw::graphics before and after blitter operations for proper
        // synchronization. This keeps the DSL simple and predictable.

        // 1. Write control registers (order matters!)
        WriteReg16(BLTCON1, bltcon1);
        WriteReg16(BLTCON0, bltcon0);

        // 2. Write word masks (full words by default)
        WriteReg16(BLTAFWM, 0xFFFF);
        WriteReg16(BLTALWM, 0xFFFF);

        // 3. Write modulos
        if ((bltcon0 & 0x0800) != 0) WriteModulo(BLTAMOD, modA);  // Source A
        if ((bltcon0 & 0x0400) != 0) WriteModulo(BLTBMOD, modB);  // Source B
        if ((bltcon0 & 0x0200) != 0) WriteModulo(BLTCMOD, modC);  // Source C
        WriteModulo(BLTDMOD, modD);  // Destination

        // 4. Write source/destination pointers
        if (sourceA != null) WriteReg32Ptr(BLTAPT, sourceA);
        if (sourceB != null) WriteReg32Ptr(BLTBPT, sourceB);
        if (sourceC != null) WriteReg32Ptr(BLTCPT, sourceC);
        WriteReg32Ptr(BLTDPT, destination);

        // 5. Write BLTSIZE to trigger the blit (MUST BE LAST!)
        WriteReg16(BLTSIZE, bltSize);

        // Note: If waitForCompletion was true, users should call WaitBlit() after this.
        // We emit a comment to document this but don't actually generate the wait code
        // since it requires proper basic block handling for loops.
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

            case IrCastValue cast:
                return TryEvaluateConstant(cast.Value, out result);

            default:
                return false;
        }
    }

    // ===========================
    // Inline Assembly Expression
    // ===========================

    /// <summary>
    /// Visit an inline assembly expression wrapper (from primaryExpression).
    /// Delegates to VisitAsmExpression.
    /// </summary>
    public override object? VisitAsmExpr([NotNull] NovusParser.AsmExprContext context)
    {
        return Visit(context.asmExpression());
    }

    /// <summary>
    /// Visit the actual inline assembly expression.
    /// Uses the shared ProcessInlineAssembly helper from IrBuilder.Statements.cs.
    /// </summary>
    public override object? VisitAsmExpression([NotNull] NovusParser.AsmExpressionContext context)
    {
        return ProcessInlineAssembly(
            context.asmInputList(),
            context.asmReturnSpec(),
            context.asmUseClause(),
            context.asmVolatile(),
            context.asmClobbers(),
            context.asmBlock(),
            GetLocation(context)
        );
    }
}
