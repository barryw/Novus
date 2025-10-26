using System.Text;
using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// Helper methods for generating 68k math operations with proper CPU target handling
/// </summary>
public partial class M68kCodeGenerator
{
    /// <summary>
    /// Generate multiply instruction with proper signed/unsigned handling and 68000 compatibility
    /// </summary>
    private void GenerateMultiply(IrBinaryOp binOp)
    {
        var isSigned = binOp.Type is IrIntType intType && intType.IsSigned;

        if (binOp.Type.SizeInBytes == 1)
        {
            // 8-bit multiply: extend to 16-bit first
            if (isSigned)
            {
                Emit("\text.w\td0");  // Sign extend d0.b to d0.w
                Emit("\text.w\td1");  // Sign extend d1.b to d1.w
            }
            else
            {
                Emit("\tandi.w\t#$FF,d0");  // Zero extend d0.b to d0.w
                Emit("\tandi.w\t#$FF,d1");  // Zero extend d1.b to d1.w
            }
            var mulOp = isSigned ? "muls.w" : "mulu.w";
            Emit($"\t{mulOp}\td1,d0");
        }
        else if (binOp.Type.SizeInBytes == 2)
        {
            // 16-bit multiply: works on all CPUs
            var mulOp = isSigned ? "muls.w" : "mulu.w";
            Emit($"\t{mulOp}\td1,d0");
        }
        else if (binOp.Type.SizeInBytes == 4)
        {
            // 32-bit multiply
            if (Is68020Plus)
            {
                // 68020+: Native 32-bit multiply
                var mulOp = isSigned ? "muls.l" : "mulu.l";
                Emit($"\t{mulOp}\td1,d0");
            }
            else
            {
                // 68000: Need to use 16x16→32 multiply sequence
                EmitComment("68000: 32-bit multiply using 16x16→32");
                Generate68000Multiply32(isSigned);
            }
        }
    }

    /// <summary>
    /// Generate 68000-compatible 32-bit multiply using 16x16 operations
    /// Algorithm: (a.hi * 65536 + a.lo) * (b.hi * 65536 + b.lo)
    /// Simplified to: a.lo * b.lo + (a.hi * b.lo + a.lo * b.hi) * 65536
    /// </summary>
    private void Generate68000Multiply32(bool isSigned)
    {
        // d0 = multiplicand, d1 = multiplier
        // Uses d2 as temporary
        // Result in d0

        Emit("\tmovem.l\td2-d3,-(sp)");  // Save registers

        if (isSigned)
        {
            // For signed multiply, we need to handle the algorithm differently
            // For simplicity in POC, call a runtime routine
            EmitComment("TODO: Implement inline 68000 signed 32-bit multiply");
            Emit("\tmovem.l\t(sp)+,d2-d3");
            // For now, generate a placeholder that won't break
            Emit("\tmuls.w\td1,d0");  // Fallback - will be wrong for large values
            EmitComment("WARNING: 32-bit signed multiply on 68000 is incomplete");
        }
        else
        {
            // Unsigned 32-bit multiply: d0 * d1 → d0 (low 32 bits)
            // Split into high and low words:
            // d0 = d0.hi:d0.lo, d1 = d1.hi:d1.lo
            // result = (d0.lo * d1.lo) + ((d0.hi * d1.lo + d0.lo * d1.hi) << 16)

            Emit("\tmove.l\td0,d2");      // d2 = d0 (save original)
            Emit("\tmove.l\td1,d3");      // d3 = d1 (save original)

            // d0.lo * d1.lo (lower 32 bits)
            Emit("\tmulu.w\td1,d0");      // d0 = d0.lo * d1.lo (result is 32-bit)

            // d0.hi * d1.lo
            Emit("\tmove.l\td2,d1");      // d1 = original d0
            Emit("\tswap\td1");           // d1 = d0.hi in lower word
            Emit("\tmulu.w\td3,d1");      // d1 = d0.hi * d1.lo

            // d0.lo * d1.hi
            Emit("\tswap\td3");           // d3 = d1.hi in lower word
            Emit("\tmulu.w\td3,d2");      // d2 = d0.lo * d1.hi

            // Combine: add cross products (shifted left by 16)
            Emit("\tadd.l\td2,d1");       // d1 = d0.hi*d1.lo + d0.lo*d1.hi
            Emit("\tswap\td1");           // Shift result left by 16
            Emit("\tclr.w\td1");          // Clear lower word
            Emit("\tadd.l\td1,d0");       // Add to final result

            Emit("\tmovem.l\t(sp)+,d2-d3");  // Restore registers
        }
    }

    /// <summary>
    /// Generate division instruction with proper signed/unsigned handling
    /// </summary>
    private void GenerateDivide(IrBinaryOp binOp)
    {
        var isSigned = binOp.Type is IrIntType intType && intType.IsSigned;

        if (binOp.Type.SizeInBytes <= 2)
        {
            // 16-bit divide: works on all CPUs
            // divs.w/divu.w: 32-bit / 16-bit → 16-bit quotient + 16-bit remainder
            var divOp = isSigned ? "divs.w" : "divu.w";
            Emit($"\t{divOp}\td1,d0");
        }
        else if (binOp.Type.SizeInBytes == 4)
        {
            // 32-bit divide
            if (Is68020Plus)
            {
                // 68020+: Native 32-bit divide
                var divOp = isSigned ? "divs.l" : "divu.l";
                Emit($"\t{divOp}\td1,d0");
            }
            else
            {
                // 68000: No native 32-bit divide
                // Would need to call runtime routine
                EmitComment("68000: 32-bit divide requires runtime routine");
                EmitComment("TODO: Call __divsi3 / __udivsi3");
                // Placeholder - this will be wrong
                var divOp = isSigned ? "divs.w" : "divu.w";
                Emit($"\t{divOp}\td1,d0");
                EmitComment("WARNING: 32-bit divide on 68000 is incomplete");
            }
        }
    }

    /// <summary>
    /// Generate modulo instruction
    /// </summary>
    private void GenerateModulo(IrBinaryOp binOp)
    {
        var isSigned = binOp.Type is IrIntType intType && intType.IsSigned;

        if (binOp.Type.SizeInBytes <= 2)
        {
            // After divs/divu.w, remainder is in upper word of d0
            var divOp = isSigned ? "divs.w" : "divu.w";
            Emit($"\t{divOp}\td1,d0");
            Emit("\tswap\td0");         // Move remainder to lower word
            Emit("\text.l\td0");        // Sign extend if needed
        }
        else if (binOp.Type.SizeInBytes == 4)
        {
            if (Is68020Plus)
            {
                // 68020+: divsl.l/divul.l can return both quotient and remainder
                // For now, use simple approach
                var divOp = isSigned ? "divsl.l" : "divul.l";
                EmitComment("TODO: Use remainder register for modulo");
                Emit($"\t{divOp}\td1,d0:d0");
            }
            else
            {
                EmitComment("68000: 32-bit modulo requires runtime routine");
                EmitComment("TODO: Call __modsi3 / __umodsi3");
            }
        }
    }
}
