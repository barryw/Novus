// Novus Runtime - Hardware Detection
// CPU, FPU, chipset, and display detection functions

#include "novus_runtime.h"

// AttnFlags bits from exec/execbase.h - only define if not already defined
#ifndef AFF_68010
#define AFF_68010     (1<<0)   // 68010 or better
#endif
#ifndef AFF_68020
#define AFF_68020     (1<<1)   // 68020 or better
#endif
#ifndef AFF_68030
#define AFF_68030     (1<<2)   // 68030 or better
#endif
#ifndef AFF_68040
#define AFF_68040     (1<<3)   // 68040 or better
#endif
#ifndef AFF_68060
#define AFF_68060     (1<<7)   // 68060 or better
#endif
#ifndef AFF_68881
#define AFF_68881     (1<<4)   // 68881 or 68882 FPU
#endif
#ifndef AFF_68882
#define AFF_68882     (1<<5)   // 68882 FPU (or better)
#endif
#ifndef AFF_FPU40
#define AFF_FPU40     (1<<6)   // 68040/68060 internal FPU
#endif

// SystemCPU enum values (must match std::system::SystemCPU)
typedef enum {
    SystemCPU_M68000 = 0,
    SystemCPU_M68010 = 1,
    SystemCPU_M68020 = 2,
    SystemCPU_M68030 = 3,
    SystemCPU_M68040 = 4,
    SystemCPU_M68060 = 5
} SystemCPU;

// SystemFPU enum values (must match std::system::SystemFPU)
typedef enum {
    SystemFPU_None = 0,
    SystemFPU_M68881 = 1,
    SystemFPU_M68882 = 2,
    SystemFPU_M68040 = 3,
    SystemFPU_M68060 = 4
} SystemFPU;

// SystemChipset enum values (must match std::system::SystemChipset)
typedef enum {
    SystemChipset_OCS = 0,
    SystemChipset_ECS = 1,
    SystemChipset_AGA = 2
} SystemChipset;

// DisplayType enum values (must match std::hardware::chipset::DisplayType)
typedef enum {
    DisplayType_PAL = 0,
    DisplayType_NTSC = 1
} DisplayType;

/**
 * Detect CPU type at runtime
 * Reads ExecBase->AttnFlags to determine which CPU features are available
 */
SystemCPU __detect_cpu(void)
{
    uint16_t attn_flags = ((struct ExecBase *)SysBase)->AttnFlags;

    if (attn_flags & AFF_68060) return SystemCPU_M68060;
    if (attn_flags & AFF_68040) return SystemCPU_M68040;
    if (attn_flags & AFF_68030) return SystemCPU_M68030;
    if (attn_flags & AFF_68020) return SystemCPU_M68020;
    if (attn_flags & AFF_68010) return SystemCPU_M68010;
    return SystemCPU_M68000;
}

/**
 * Detect FPU type at runtime
 * Reads ExecBase->AttnFlags to determine which FPU is present
 */
SystemFPU __detect_fpu(void)
{
    uint16_t attn_flags = ((struct ExecBase *)SysBase)->AttnFlags;

    // Check 68060 first (most specific)
    if ((attn_flags & AFF_68060) && (attn_flags & AFF_FPU40)) {
        return SystemFPU_M68060;
    }

    // Check 68040 internal FPU
    if ((attn_flags & AFF_68040) && (attn_flags & AFF_FPU40)) {
        return SystemFPU_M68040;
    }

    // Check for 68882 (more specific than 68881)
    if (attn_flags & AFF_68882) {
        return SystemFPU_M68882;
    }

    // Check for 68881
    if (attn_flags & AFF_68881) {
        return SystemFPU_M68881;
    }

    return SystemFPU_None;
}

/**
 * Detect chipset type at runtime
 * Checks graphics.library ChipRevBits0 to determine OCS/ECS/AGA
 *
 * IMPORTANT: ChipRevBits0 detection requires V39 SetPatch to be accurate.
 * On systems with library version < 39, this will default to OCS.
 *
 * NOTE: RTG (UAEGFX/Picasso96/CyberGraphX) does NOT affect this detection.
 * This always returns the native Amiga chipset (OCS/ECS/AGA), regardless
 * of what graphics system is currently active for display.
 */
SystemChipset __detect_chipset(void)
{
    struct GfxBase *GfxBase;
    uint8_t chip_rev;
    SystemChipset result;

    // Open graphics.library V36+ to check chipset
    GfxBase = (struct GfxBase *)OpenLibrary("graphics.library", 36L);
    if (GfxBase == NULL) {
        // Very old system (pre-2.0) - must be OCS
        return SystemChipset_OCS;
    }

    // ChipRevBits0 is only reliable on V39+ (requires SetPatch)
    if (GfxBase->LibNode.lib_Version < 39) {
        CloseLibrary((struct Library *)GfxBase);
        return SystemChipset_OCS;
    }

    // Read ChipRevBits0 directly from GfxBase structure
    // ChipRevBits0 is a UBYTE (single byte) at offset 476 in struct GfxBase
    chip_rev = GfxBase->ChipRevBits0;

    CloseLibrary((struct Library *)GfxBase);

    // Bit definitions from graphics/gfxbase.h:
    // GFXB_BIG_BLITS  0  (bit 0) = 0x01
    // GFXB_HR_AGNUS   0  (bit 0) = 0x01 - ECS Agnus
    // GFXB_HR_DENISE  1  (bit 1) = 0x02 - ECS Denise
    // GFXB_AA_ALICE   2  (bit 2) = 0x04 - AGA Alice (replaces Denise)
    // GFXB_AA_LISA    3  (bit 3) = 0x08 - AGA Lisa (replaces Agnus)
    // GFXB_AA_MLISA   4  (bit 4) = 0x10 - AGA MLISA (internal use only)

    // AGA check: Both ALICE (bit 2) and LISA (bit 3) must be set
    // GFXF_AA_ALICE = 0x04, GFXF_AA_LISA = 0x08
    if ((chip_rev & 0x0C) == 0x0C) {
        result = SystemChipset_AGA;
    }
    // ECS check: HR_DENISE (bit 1) or HR_AGNUS (bit 0)
    // GFXF_HR_DENISE = 0x02, GFXF_HR_AGNUS = 0x01
    else if (chip_rev & 0x03) {
        result = SystemChipset_ECS;
    }
    // Default to OCS
    else {
        result = SystemChipset_OCS;
    }

    return result;
}

/**
 * Detect display type (PAL vs NTSC) at runtime
 *
 * Uses GfxBase->DisplayFlags which is the most reliable method.
 * This flag is set by the OS during initialization based on the
 * actual hardware configuration (Agnus chip ID and system preferences).
 *
 * Alternative methods (not used):
 * - VPOSR register ($DFF004): Can be unreliable on some systems
 * - SysBase->VBlankFrequency: 50=PAL, 60=NTSC (requires newer exec)
 *
 * NOTE: This returns the system's native display type, not necessarily
 * what a program is currently using (e.g., PAL games on NTSC with
 * mode promotion).
 */
DisplayType __detect_display(void)
{
    struct GfxBase *GfxBase;
    DisplayType result;

    // Open graphics.library V33+ (Kickstart 1.2+)
    GfxBase = (struct GfxBase *)OpenLibrary("graphics.library", 33L);
    if (GfxBase == NULL) {
        // Can't determine - default to PAL (more common in Amiga world)
        return DisplayType_PAL;
    }

    // Check DisplayFlags - bit 5 (0x20) = DISPLAYPAL
    // From graphics/gfxbase.h: DISPLAYPAL = 0x0020
    if (GfxBase->DisplayFlags & 0x0020) {
        result = DisplayType_PAL;
    } else {
        result = DisplayType_NTSC;
    }

    CloseLibrary((struct Library *)GfxBase);
    return result;
}

/**
 * Get total chip RAM installed in the system
 *
 * IMPORTANT: AvailMem(MEMF_TOTAL) does NOT work as you might expect!
 * According to the AmigaOS autodocs, AvailMem() ALWAYS returns free memory,
 * even with MEMF_TOTAL flag. The MEMF_TOTAL flag is poorly documented and
 * does not mean "total installed RAM".
 *
 * The correct way to get total chip RAM is via ExecBase->MaxLocMem,
 * which is documented as "top of chip memory". Since chip RAM starts at
 * address 0, MaxLocMem gives us the total chip RAM size.
 */
uint32_t __get_chip_ram_total(void)
{
    struct ExecBase *execBase = (struct ExecBase *)SysBase;

    // MaxLocMem is the top of chip memory (chip RAM starts at address 0)
    return execBase->MaxLocMem;
}

/**
 * Get free chip RAM
 *
 * Uses Exec AvailMem() with MEMF_CHIP to query the free chip memory.
 * This sums the free bytes across all chip memory MemHeaders.
 */
uint32_t __get_chip_ram_free(void)
{
    return AvailMem(MEMF_CHIP);
}

/**
 * Get largest free chip RAM block
 *
 * Uses Exec AvailMem() with MEMF_CHIP | MEMF_LARGEST to find the largest
 * contiguous block of free chip memory. This is useful for determining if
 * a large allocation will succeed.
 *
 * Note: This is a slow operation as it scans all free blocks.
 */
uint32_t __get_chip_ram_largest(void)
{
    return AvailMem(MEMF_CHIP | MEMF_LARGEST);
}
