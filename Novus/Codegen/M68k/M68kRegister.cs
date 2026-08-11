namespace Novus.Codegen.M68k;

/// <summary>
/// Motorola 68k registers following Amiga ABI conventions
/// </summary>
public enum M68kRegister
{
    // Data registers
    D0 = 0,
    D1 = 1,
    D2 = 2,
    D3 = 3,
    D4 = 4,
    D5 = 5,
    D6 = 6,
    D7 = 7,

    // Address registers
    A0 = 8,
    A1 = 9,
    A2 = 10,
    A3 = 11,
    A4 = 12,
    A5 = 13,  // Frame pointer
    A6 = 14,  // Library base / static base
    A7 = 15,  // Stack pointer (SP)

    // Special aliases
    FP = A5,
    SP = A7
}

/// <summary>
/// Register classification for allocation and calling convention
/// </summary>
public enum RegisterClass
{
    /// <summary>
    /// Volatile registers - caller saved, can be freely used by callee
    /// D0-D1, A0-A1
    /// </summary>
    Volatile,

    /// <summary>
    /// Preserved registers - callee saved, must be preserved across calls
    /// D2-D7, A2-A4
    /// </summary>
    Preserved,

    /// <summary>
    /// Special purpose registers - not available for general allocation
    /// A5 (FP), A6 (library base), A7 (SP)
    /// </summary>
    Special
}

/// <summary>
/// Register utility methods
/// </summary>
public static class M68kRegisterExtensions
{
    public static bool IsDataRegister(this M68kRegister reg)
    {
        return reg >= M68kRegister.D0 && reg <= M68kRegister.D7;
    }

    public static bool IsAddressRegister(this M68kRegister reg)
    {
        return reg >= M68kRegister.A0 && reg <= M68kRegister.A7;
    }

    public static RegisterClass GetRegisterClass(this M68kRegister reg)
    {
        return reg switch
        {
            M68kRegister.D0 or M68kRegister.D1 or M68kRegister.A0 or M68kRegister.A1 => RegisterClass.Volatile,
            M68kRegister.D2 or M68kRegister.D3 or M68kRegister.D4 or M68kRegister.D5 or M68kRegister.D6 or M68kRegister.D7 or M68kRegister.A2 or M68kRegister.A3 or M68kRegister.A4 => RegisterClass.Preserved,
            _ => RegisterClass.Special
        };
    }

    public static string ToAsmString(this M68kRegister reg)
    {
        return reg switch
        {
            M68kRegister.D0 => "d0",
            M68kRegister.D1 => "d1",
            M68kRegister.D2 => "d2",
            M68kRegister.D3 => "d3",
            M68kRegister.D4 => "d4",
            M68kRegister.D5 => "d5",
            M68kRegister.D6 => "d6",
            M68kRegister.D7 => "d7",
            M68kRegister.A0 => "a0",
            M68kRegister.A1 => "a1",
            M68kRegister.A2 => "a2",
            M68kRegister.A3 => "a3",
            M68kRegister.A4 => "a4",
            M68kRegister.A5 => "a5",
            M68kRegister.A6 => "a6",
            M68kRegister.A7 => "a7",
            _ => throw new ArgumentException($"Unknown register: {reg}")
        };
    }
}
