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
    /// Optimizes for 68060 by avoiding multiply instructions for small constants
    /// </summary>
    private void GenerateMultiply(IrBinaryOp binOp)
    {
        var isSigned = binOp.Type is IrIntType intType && intType.IsSigned;

        // Check if we're multiplying by a constant and if CPU prefers shift/add sequences
        if (binOp.Right is IrConstant constant && _cpuFeatures.AvoidMultiplyInstructions)
        {
            // 68060: Multiply is slow, prefer shift/add sequences for small constants
            if (TryGenerateConstantMultiplyOptimization(constant.Value, isSigned))
            {
                return;
            }
            // Fall through to regular multiply for large constants
        }

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
                // But on 68060, multiply is slow - use it only if we couldn't optimize above
                var mulOp = isSigned ? "muls.l" : "mulu.l";
                Emit($"\t{mulOp}\td1,d0");

                if (_cpuFeatures.AvoidMultiplyInstructions)
                {
                    EmitComment("Note: 68060 has slow multiply; consider optimizing constant multiplies");
                }
            }
            else
            {
                // 68000: Need to use 16x16→32 multiply sequence
                // In fat binary mode, call runtime primitive to avoid duplicate labels
                if (_isOriginallyFatBinary)
                {
                    EmitComment("68000: Call CPU-optimized runtime primitive");
                    var funcName = isSigned ? "__mul_i32" : "__mul_u32";
                    Emit($"\tjsr\t{funcName}");
                }
                else
                {
                    EmitComment("68000: 32-bit multiply using 16x16→32");
                    Generate68000Multiply32(isSigned);
                }
            }
        }
    }

    /// <summary>
    /// Try to optimize constant multiplication using shifts and adds (for 68060)
    /// Returns true if optimization was generated, false if multiply instruction should be used
    /// </summary>
    private bool TryGenerateConstantMultiplyOptimization(long constant, bool isSigned)
    {
        // For 68060, only optimize small constants where shift/add is faster than multiply
        if (!_cpuFeatures.PreferMultiplyForConstant((int)constant))
        {
            // Generate optimized shift/add sequence for common constants
            switch (constant)
            {
                case 2:
                    EmitComment("68060 optimization: x * 2 = x << 1");
                    Emit("\tadd.l\td0,d0");  // d0 = d0 + d0 (faster than shift on 060)
                    return true;

                case 3:
                    EmitComment("68060 optimization: x * 3 = x + (x << 1)");
                    Emit("\tmove.l\td0,d1");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td1,d0");
                    return true;

                case 4:
                    EmitComment("68060 optimization: x * 4 = x << 2");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td0,d0");
                    return true;

                case 5:
                    EmitComment("68060 optimization: x * 5 = x + (x << 2)");
                    Emit("\tmove.l\td0,d1");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td1,d0");
                    return true;

                case 8:
                    EmitComment("68060 optimization: x * 8 = x << 3");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td0,d0");
                    return true;

                case 10:
                    EmitComment("68060 optimization: x * 10 = (x << 3) + (x << 1)");
                    Emit("\tmove.l\td0,d1");
                    Emit("\tadd.l\td0,d0");   // x << 1
                    Emit("\tmove.l\td1,d2");
                    Emit("\tadd.l\td2,d2");   // x << 1
                    Emit("\tadd.l\td2,d2");   // x << 2
                    Emit("\tadd.l\td2,d2");   // x << 3
                    Emit("\tadd.l\td2,d0");   // (x << 1) + (x << 3)
                    return true;

                case 16:
                    EmitComment("68060 optimization: x * 16 = x << 4");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td0,d0");
                    Emit("\tadd.l\td0,d0");
                    return true;

                // Powers of 2 up to 256
                case 32:
                case 64:
                case 128:
                case 256:
                {
                    int shiftCount = 0;
                    long temp = constant;
                    while (temp > 1)
                    {
                        temp >>= 1;
                        shiftCount++;
                    }
                    EmitComment($"68060 optimization: x * {constant} = x << {shiftCount}");
                    for (int i = 0; i < shiftCount; i++)
                    {
                        Emit("\tadd.l\td0,d0");
                    }
                    return true;
                }
            }
        }

        return false;  // Use multiply instruction
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
            // Signed 32×32→32 multiply
            // Strategy: Convert to unsigned, multiply, adjust sign
            // Uses d2-d4 as temporaries
            Emit("\tmovem.l\t(sp)+,d2-d3");  // We'll need d4 too
            Emit("\tmovem.l\td2-d4,-(sp)");  // Save d2-d4

            Emit("\tmoveq\t#0,d4");          // d4 = sign tracker (0 = positive, -1 = negative)

            // Check if d0 is negative
            Emit("\ttst.l\td0");
            Emit("\tbpl.s\t.pos_d0");
            Emit("\tneg.l\td0");             // Make d0 positive
            Emit("\tnot.l\td4");             // Flip sign tracker
            Emit(".pos_d0:");

            // Check if d1 is negative
            Emit("\ttst.l\td1");
            Emit("\tbpl.s\t.pos_d1");
            Emit("\tneg.l\td1");             // Make d1 positive
            Emit("\tnot.l\td4");             // Flip sign tracker
            Emit(".pos_d1:");

            // Now do unsigned multiply (both operands are positive)
            Emit("\tmove.l\td0,d2");         // d2 = d0 (save original)
            Emit("\tmove.l\td1,d3");         // d3 = d1 (save original)

            // d0.lo * d1.lo (lower 32 bits)
            Emit("\tmulu.w\td1,d0");         // d0 = d0.lo * d1.lo (result is 32-bit)

            // d0.hi * d1.lo
            Emit("\tmove.l\td2,d1");         // d1 = original d0
            Emit("\tswap\td1");              // d1 = d0.hi in lower word
            Emit("\tmulu.w\td3,d1");         // d1 = d0.hi * d1.lo

            // d0.lo * d1.hi
            Emit("\tswap\td3");              // d3 = d1.hi in lower word
            Emit("\tmulu.w\td3,d2");         // d2 = d0.lo * d1.hi

            // Combine: add cross products (shifted left by 16)
            Emit("\tadd.l\td2,d1");          // d1 = d0.hi*d1.lo + d0.lo*d1.hi
            Emit("\tswap\td1");              // Shift result left by 16
            Emit("\tclr.w\td1");             // Clear lower word
            Emit("\tadd.l\td1,d0");          // Add to final result

            // Apply sign correction
            Emit("\ttst.l\td4");
            Emit("\tbeq.s\t.done");
            Emit("\tneg.l\td0");             // Negate result if needed
            Emit(".done:");

            Emit("\tmovem.l\t(sp)+,d2-d4");  // Restore registers
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
    /// Note: Division is very slow on 68060 (>60 cycles); consider alternatives when possible
    /// </summary>
    private void GenerateDivide(IrBinaryOp binOp)
    {
        var isSigned = binOp.Type is IrIntType intType && intType.IsSigned;

        // Check if dividing by a power of 2 constant - can optimize to shift
        if (binOp.Right is IrConstant constant && IsPowerOfTwo(constant.Value) && !isSigned)
        {
            int shiftCount = 0;
            long temp = constant.Value;
            while (temp > 1)
            {
                temp >>= 1;
                shiftCount++;
            }

            if (_cpuFeatures.AvoidDivideInstructions)
            {
                EmitComment($"68060 optimization: x / {constant.Value} = x >> {shiftCount} (unsigned)");
            }
            else
            {
                EmitComment($"Optimized: x / {constant.Value} = x >> {shiftCount} (unsigned)");
            }

            // Use optimal shift strategy based on CPU
            GenerateRightShift(shiftCount, false);
            return;
        }

        if (binOp.Type.SizeInBytes <= 2)
        {
            // 16-bit divide: works on all CPUs
            // divs.w/divu.w: 32-bit / 16-bit → 16-bit quotient + 16-bit remainder
            var divOp = isSigned ? "divs.w" : "divu.w";
            Emit($"\t{divOp}\td1,d0");

            if (_cpuFeatures.AvoidDivideInstructions)
            {
                EmitComment("Note: 68060 has very slow divide (>60 cycles)");
            }
        }
        else if (binOp.Type.SizeInBytes == 4)
        {
            // 32-bit divide
            if (Is68020Plus)
            {
                // 68020+: Native 32-bit divide
                var divOp = isSigned ? "divs.l" : "divu.l";
                Emit($"\t{divOp}\td1,d0");

                if (_cpuFeatures.AvoidDivideInstructions)
                {
                    EmitComment("WARNING: 68060 has very slow 32-bit divide (>70 cycles); avoid if possible");
                }
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
    /// Check if a value is a power of 2
    /// </summary>
    private bool IsPowerOfTwo(long value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    /// <summary>
    /// Generate optimized right shift based on CPU capabilities
    /// </summary>
    private void GenerateRightShift(int shiftCount, bool isSigned)
    {
        if (shiftCount == 0)
        {
            return;  // No shift needed
        }

        var shiftOp = isSigned ? "asr.l" : "lsr.l";

        // Immediate shifts are limited to 1-8 on ALL 68k CPUs
        // For larger shifts, use register even on 68020+ (barrel shifter works with register)
        if (shiftCount <= 8)
        {
            // Small shifts (1-8) use immediate operand
            Emit($"\t{shiftOp}\t#{shiftCount},d0");
        }
        else
        {
            // Large shifts: load count into register and use register shift
            // On 68020+ with barrel shifter, this is still just 1 cycle
            Emit($"\tmoveq\t#{shiftCount},d1");
            Emit($"\t{shiftOp}\td1,d0");
        }
    }

    /// <summary>
    /// Generate optimized left shift based on CPU capabilities
    /// </summary>
    private void GenerateLeftShift(int shiftCount, string size)
    {
        if (shiftCount == 0)
        {
            return;  // No shift needed
        }

        // Immediate shifts are limited to 1-8 on ALL 68k CPUs
        // For larger shifts, use register even on 68020+ (barrel shifter works with register)
        if (shiftCount <= 8)
        {
            // Small shifts (1-8) use immediate operand
            Emit($"\tlsl{size}\t#{shiftCount},d0");
        }
        else
        {
            // Large shifts: load count into register and use register shift
            // On 68020+ with barrel shifter, this is still just 1 cycle
            Emit($"\tmoveq\t#{shiftCount},d1");
            Emit($"\tlsl{size}\td1,d0");
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
