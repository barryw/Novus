namespace Novus.SemanticAnalysis;

/// <summary>
/// Amiga chipset profiles defining hardware capabilities.
///
/// <list type="bullet">
///   <item><term>OCS</term><description>Original Chipset (A1000/A500/A2000) - Most restrictive</description></item>
///   <item><term>ECS</term><description>Enhanced Chipset (A500+/A600/A3000) - Extended features</description></item>
///   <item><term>AGA</term><description>Advanced Graphics Architecture (A1200/A4000) - Full feature set</description></item>
///   <item><term>Auto</term><description>Runtime detection - uses widest common subset at compile time</description></item>
/// </list>
/// </summary>
public enum ChipsetProfile
{
    /// <summary>
    /// Original Chipset (OCS) - A1000, A500, A2000
    /// Most restrictive feature set:
    /// - 12-bit color palette (4096 colors)
    /// - 32 colors in normal mode (HAM6 for 4096)
    /// - Sprites: 16 colors (shared palette)
    /// - Max resolution: 640x512i (PAL)
    /// - Copper: Basic WAIT/MOVE operations
    /// - Blitter: Standard operations
    /// </summary>
    OCS,

    /// <summary>
    /// Enhanced Chipset (ECS) - A500+, A600, A3000
    /// Extended features:
    /// - Same 12-bit color palette
    /// - Productivity modes (higher resolutions)
    /// - Super hires mode (1280x512i)
    /// - Copper: Extended beam wait range
    /// - Blitter: Same as OCS
    /// - Enhanced sprite modes
    /// </summary>
    ECS,

    /// <summary>
    /// Advanced Graphics Architecture (AGA) - A1200, A4000
    /// Full feature set:
    /// - 24-bit color palette (16.8 million colors)
    /// - Up to 256 colors in normal mode (HAM8 for 16M)
    /// - Enhanced sprites with more colors
    /// - Higher resolutions
    /// - Copper: Full AGA register access
    /// - Blitter: Extended capabilities
    /// </summary>
    AGA,

    /// <summary>
    /// Auto-detect at runtime.
    /// Compile-time validation uses widest common subset (OCS).
    /// Runtime code detects actual hardware.
    /// </summary>
    Auto
}

/// <summary>
/// Provides chipset capability checks for compile-time validation.
/// </summary>
public static class ChipsetCapabilities
{
    // Custom chip register ranges
    private const uint CustomChipBase = 0xDFF000;
    private const uint CustomChipEnd = 0xDFF1FF;

    // AGA-specific registers (not available on OCS/ECS)
    private static readonly HashSet<uint> AgaOnlyRegisters = new()
    {
        0xDFF100, // BPLCON3
        0xDFF104, // BPLCON4
        0xDFF106, // CLXCON2
        0xDFF180 + 64 * 2, // Extended color registers (32-63)
        // ... more AGA registers would go here
    };

    // ECS-specific registers
    private static readonly HashSet<uint> EcsOnlyRegisters = new()
    {
        0xDFF1DC, // BEAMCON0
        0xDFF1E4, // DIWHIGH
        // ... more ECS registers would go here
    };

    // Maximum vertical line for WAIT by chipset
    public static int MaxVerticalLine(ChipsetProfile profile, bool isPal = true)
    {
        return profile switch
        {
            // PAL: 312 lines, NTSC: 262 lines
            ChipsetProfile.OCS => isPal ? 255 : 255, // OCS Copper can only wait up to line 255
            ChipsetProfile.ECS => isPal ? 312 : 262, // ECS has extended WAIT range
            ChipsetProfile.AGA => isPal ? 312 : 262, // AGA same as ECS
            ChipsetProfile.Auto => isPal ? 255 : 255, // Use most restrictive
            _ => 255
        };
    }

    // Maximum horizontal position for WAIT
    public static int MaxHorizontalPosition(ChipsetProfile profile)
    {
        // All chipsets: 0-226 (0xE2)
        return 226;
    }

    /// <summary>
    /// Check if a register is accessible on the given chipset.
    /// </summary>
    public static bool IsRegisterAccessible(uint address, ChipsetProfile profile)
    {
        // Must be in custom chip range
        if (address < CustomChipBase || address > CustomChipEnd)
            return false;

        // Calculate offset from base
        uint offset = address - CustomChipBase;

        // AGA-only registers not available on OCS/ECS
        if (profile != ChipsetProfile.AGA && AgaOnlyRegisters.Contains(address))
            return false;

        // ECS-only registers not available on OCS
        if (profile == ChipsetProfile.OCS && EcsOnlyRegisters.Contains(address))
            return false;

        return true;
    }

    /// <summary>
    /// Check if a Copper WAIT position is valid for the chipset.
    /// </summary>
    public static bool IsWaitPositionValid(int vertical, int horizontal, ChipsetProfile profile, out string? errorMessage)
    {
        errorMessage = null;

        if (horizontal < 0 || horizontal > MaxHorizontalPosition(profile))
        {
            errorMessage = $"Horizontal position {horizontal} out of range (0-{MaxHorizontalPosition(profile)})";
            return false;
        }

        int maxV = MaxVerticalLine(profile);
        if (vertical < 0 || vertical > maxV)
        {
            if (profile == ChipsetProfile.OCS && vertical > 255)
            {
                errorMessage = $"Vertical position {vertical} exceeds OCS Copper limit (max 255). Use ECS or AGA for lines 256+";
            }
            else
            {
                errorMessage = $"Vertical position {vertical} out of range (0-{maxV})";
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if a color index is valid for the chipset.
    /// </summary>
    public static bool IsColorIndexValid(int index, ChipsetProfile profile, out string? errorMessage)
    {
        errorMessage = null;

        int maxColors = profile switch
        {
            ChipsetProfile.OCS => 32,
            ChipsetProfile.ECS => 32,
            ChipsetProfile.AGA => 256,
            ChipsetProfile.Auto => 32, // Most restrictive
            _ => 32
        };

        if (index < 0 || index >= maxColors)
        {
            errorMessage = $"Color index {index} out of range (0-{maxColors - 1}) for {profile} chipset";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if a 24-bit color value is valid for the chipset.
    /// </summary>
    public static bool IsColorValueValid(uint color, ChipsetProfile profile, out string? errorMessage)
    {
        errorMessage = null;

        if (profile == ChipsetProfile.AGA)
        {
            // AGA: 24-bit color
            if (color > 0xFFFFFF)
            {
                errorMessage = $"Color value 0x{color:X8} exceeds 24-bit range";
                return false;
            }
        }
        else
        {
            // OCS/ECS: 12-bit color (4 bits per component)
            // Valid values: 0x000 - 0xFFF
            if (color > 0xFFF)
            {
                errorMessage = $"Color value 0x{color:X4} exceeds 12-bit range (OCS/ECS). Use 0x000-0xFFF or target AGA";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get the number of bitplanes supported by the chipset.
    /// </summary>
    public static int MaxBitplanes(ChipsetProfile profile, bool isHam = false)
    {
        return profile switch
        {
            ChipsetProfile.OCS => isHam ? 6 : 5, // HAM6 or 32 colors
            ChipsetProfile.ECS => isHam ? 6 : 5, // Same as OCS
            ChipsetProfile.AGA => isHam ? 8 : 8, // HAM8 or 256 colors
            ChipsetProfile.Auto => isHam ? 6 : 5, // Most restrictive
            _ => 5
        };
    }

    /// <summary>
    /// Check if a sprite width is valid for the chipset.
    /// Note: Sprites are always 16 pixels wide on all chipsets.
    /// </summary>
    public static bool IsSpriteWidthValid(int width, ChipsetProfile profile, out string? errorMessage)
    {
        errorMessage = null;

        // Hardware constraint: all Amiga sprites are 16 pixels wide
        if (width != 16)
        {
            errorMessage = $"Sprite width must be 16 pixels (hardware constraint). Got {width}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if Blitter size is valid for the chipset.
    /// </summary>
    public static bool IsBlitterSizeValid(int width, int height, ChipsetProfile profile, out string? errorMessage)
    {
        errorMessage = null;

        // Width: 1-64 words (1-1024 pixels)
        if (width < 1 || width > 1024)
        {
            errorMessage = $"Blitter width {width} out of range (1-1024 pixels)";
            return false;
        }

        // Height: 1-1024 lines
        if (height < 1 || height > 1024)
        {
            errorMessage = $"Blitter height {height} out of range (1-1024 lines)";
            return false;
        }

        // Width must be word-aligned for OCS/ECS
        if (profile != ChipsetProfile.AGA && width % 16 != 0)
        {
            errorMessage = $"Blitter width {width} must be multiple of 16 for {profile} chipset";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parse chipset profile from string (case-insensitive).
    /// </summary>
    public static ChipsetProfile Parse(string? chipset)
    {
        return chipset?.ToUpperInvariant() switch
        {
            "OCS" => ChipsetProfile.OCS,
            "ECS" => ChipsetProfile.ECS,
            "AGA" => ChipsetProfile.AGA,
            "AUTO" or null or "" => ChipsetProfile.Auto,
            _ => ChipsetProfile.Auto
        };
    }
}
