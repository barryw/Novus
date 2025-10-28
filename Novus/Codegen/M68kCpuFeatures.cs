namespace Novus.Codegen;

/// <summary>
/// CPU-specific features and capabilities for 68k family processors
/// Used to generate optimal code for each processor variant
/// </summary>
public class M68kCpuFeatures
{
    public string CpuName { get; }
    public int CpuGeneration { get; }

    // Arithmetic capabilities
    public bool Has32BitMultiply { get; }
    public bool Has32BitDivide { get; }
    public bool Has64BitMultiply { get; }  // muls.l/mulu.l Dn,Dm (64-bit result)

    // Shift/rotate capabilities
    public bool HasBarrelShifter { get; }  // Fast multi-bit shifts (68020+)

    // Memory/data movement
    public bool HasMove16 { get; }         // MOVE16 for cache line transfers (68040+)
    public bool HasBitfield { get; }       // BFEXTU, BFINS, etc. (68020+)

    // Control/branch
    public bool HasBcc32 { get; }          // 32-bit branch displacement (68020+)
    public bool HasCallm { get; }          // Module call (68020-68030 only, removed in 040)
    public bool HasCas { get; }            // Compare and swap (68020+)
    public bool HasCas2 { get; }           // Double CAS (68020+)

    // Addressing modes
    public bool HasScaleIndex { get; }     // Scaled index registers (68020+)
    public bool HasFullExtension { get; }  // Full extension words (68020+)

    // Performance characteristics
    public bool IsSuperscalar { get; }     // Dual-issue pipeline (68060+)
    public bool HasBranchCache { get; }    // Branch prediction (68060+)
    public int PreferredAlignment { get; }  // 2 for 68000-030, 4 for 040+, 8 for 060+

    // Instructions to avoid
    public bool AvoidMultiplyInstructions { get; } // 68060 has slow multiply
    public bool AvoidDivideInstructions { get; }   // 68060 has slow divide
    public bool AvoidBitfields { get; }            // 68060 has slow bitfields

    // FPU
    public bool HasIntegratedFpu { get; }  // 68040+
    public bool HasFullFpu { get; }        // 68881/68882 full transcendental functions

    public M68kCpuFeatures(string cpuTarget)
    {
        CpuName = cpuTarget.ToLower();

        switch (CpuName)
        {
            case "auto":
                // For auto mode (fat binary), default to 68000 compatibility
                // The actual CPU-specific optimizations are generated separately
                goto case "68000";

            case "68000":
            case "68008":
                CpuGeneration = 0;
                Has32BitMultiply = false;
                Has32BitDivide = false;
                Has64BitMultiply = false;
                HasBarrelShifter = false;
                HasMove16 = false;
                HasBitfield = false;
                HasBcc32 = false;
                HasCallm = false;
                HasCas = false;
                HasCas2 = false;
                HasScaleIndex = false;
                HasFullExtension = false;
                IsSuperscalar = false;
                HasBranchCache = false;
                PreferredAlignment = 2;
                AvoidMultiplyInstructions = false;
                AvoidDivideInstructions = false;
                AvoidBitfields = false;
                HasIntegratedFpu = false;
                HasFullFpu = false;
                break;

            case "68010":
                CpuGeneration = 1;
                Has32BitMultiply = false;
                Has32BitDivide = false;
                Has64BitMultiply = false;
                HasBarrelShifter = false;
                HasMove16 = false;
                HasBitfield = false;
                HasBcc32 = false;
                HasCallm = false;
                HasCas = false;
                HasCas2 = false;
                HasScaleIndex = false;
                HasFullExtension = false;
                IsSuperscalar = false;
                HasBranchCache = false;
                PreferredAlignment = 2;
                AvoidMultiplyInstructions = false;
                AvoidDivideInstructions = false;
                AvoidBitfields = false;
                HasIntegratedFpu = false;
                HasFullFpu = false;
                break;

            case "020":
            case "68020":
                CpuGeneration = 2;
                Has32BitMultiply = true;
                Has32BitDivide = true;
                Has64BitMultiply = true;
                HasBarrelShifter = true;
                HasMove16 = false;
                HasBitfield = true;
                HasBcc32 = true;
                HasCallm = true;
                HasCas = true;
                HasCas2 = true;
                HasScaleIndex = true;
                HasFullExtension = true;
                IsSuperscalar = false;
                HasBranchCache = false;
                PreferredAlignment = 2;
                AvoidMultiplyInstructions = false;
                AvoidDivideInstructions = false;
                AvoidBitfields = false;
                HasIntegratedFpu = false;
                HasFullFpu = true;  // With external 68881/68882
                break;

            case "030":
            case "68030":
                CpuGeneration = 3;
                Has32BitMultiply = true;
                Has32BitDivide = true;
                Has64BitMultiply = true;
                HasBarrelShifter = true;
                HasMove16 = false;
                HasBitfield = true;
                HasBcc32 = true;
                HasCallm = true;
                HasCas = true;
                HasCas2 = true;
                HasScaleIndex = true;
                HasFullExtension = true;
                IsSuperscalar = false;
                HasBranchCache = false;
                PreferredAlignment = 2;
                AvoidMultiplyInstructions = false;
                AvoidDivideInstructions = false;
                AvoidBitfields = false;
                HasIntegratedFpu = false;
                HasFullFpu = true;  // With external 68881/68882
                break;

            case "040":
            case "68040":
                CpuGeneration = 4;
                Has32BitMultiply = true;
                Has32BitDivide = true;
                Has64BitMultiply = true;
                HasBarrelShifter = true;
                HasMove16 = true;
                HasBitfield = true;
                HasBcc32 = true;
                HasCallm = false;  // Removed in 68040
                HasCas = true;
                HasCas2 = true;
                HasScaleIndex = true;
                HasFullExtension = true;
                IsSuperscalar = false;
                HasBranchCache = false;
                PreferredAlignment = 4;
                AvoidMultiplyInstructions = false;
                AvoidDivideInstructions = false;
                AvoidBitfields = false;
                HasIntegratedFpu = true;
                HasFullFpu = false;  // 040 FPU lacks transcendentals
                break;

            case "060":
            case "68060":
                CpuGeneration = 6;
                Has32BitMultiply = true;
                Has32BitDivide = true;
                Has64BitMultiply = true;
                HasBarrelShifter = true;
                HasMove16 = true;
                HasBitfield = true;
                HasBcc32 = true;
                HasCallm = false;
                HasCas = true;
                HasCas2 = true;
                HasScaleIndex = true;
                HasFullExtension = true;
                IsSuperscalar = true;  // Dual-issue pipeline!
                HasBranchCache = true;
                PreferredAlignment = 8;
                AvoidMultiplyInstructions = true;  // Slow on 060, use shifts/adds
                AvoidDivideInstructions = true;    // Very slow on 060
                AvoidBitfields = true;             // Slow on 060
                HasIntegratedFpu = true;
                HasFullFpu = false;
                break;

            case "68080":
            case "apollo":
                CpuGeneration = 8;
                Has32BitMultiply = true;
                Has32BitDivide = true;
                Has64BitMultiply = true;
                HasBarrelShifter = true;
                HasMove16 = true;
                HasBitfield = true;
                HasBcc32 = true;
                HasCallm = false;
                HasCas = true;
                HasCas2 = true;
                HasScaleIndex = true;
                HasFullExtension = true;
                IsSuperscalar = true;
                HasBranchCache = true;
                PreferredAlignment = 8;
                AvoidMultiplyInstructions = false;  // Fast on 68080
                AvoidDivideInstructions = false;    // Fast on 68080
                AvoidBitfields = false;
                HasIntegratedFpu = true;
                HasFullFpu = true;
                break;

            default:
                // Default to 68000 for safety
                goto case "68000";
        }
    }

    /// <summary>
    /// Check if this CPU is at least the specified generation
    /// </summary>
    public bool IsAtLeast(int generation) => CpuGeneration >= generation;

    /// <summary>
    /// Get optimal instruction for multi-bit shift based on CPU
    /// </summary>
    public string GetShiftStrategy(int shiftAmount)
    {
        if (HasBarrelShifter)
        {
            // 68020+: Barrel shifter handles any shift count efficiently
            return "barrel";
        }
        else if (shiftAmount <= 8)
        {
            // 68000: Small shifts are efficient
            return "immediate";
        }
        else
        {
            // 68000: Large shifts should use loop or alternative
            return "loop";
        }
    }

    /// <summary>
    /// Should we use multiply for constant multiplication?
    /// </summary>
    public bool PreferMultiplyForConstant(int constant)
    {
        if (AvoidMultiplyInstructions)
        {
            // 68060: Prefer shift/add sequences for small constants
            return constant > 100;  // Only use multiply for large constants
        }
        return true;
    }

    /// <summary>
    /// Recommended method for clearing a register
    /// </summary>
    public string GetClearRegisterMethod()
    {
        if (IsSuperscalar)
        {
            // 68060: MOVEQ is dual-issue friendly
            return "moveq";
        }
        else if (IsAtLeast(2))
        {
            // 68020-040: Both are fine, slight preference for MOVEQ
            return "moveq";
        }
        else
        {
            // 68000: MOVEQ is slightly faster
            return "moveq";
        }
    }
}
