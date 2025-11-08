# Chipset Detection Issue Analysis and Fix

## Problem Summary

The chipset detection code in `/Users/barry/RiderProjects/Novus/runtime/novus_runtime.c` is incorrectly detecting chipsets on an emulated Amiga A4000 with AGA chipset.

**Observed Value:** `ChipRev: 0x1F030000`
**Expected Result:** AGA
**Actual Result:** Likely OCS (incorrect)

---

## Root Cause Analysis

### The Bug

The code at line 464 has a critical error:

```c
// WRONG: Extracts upper 16 bits instead of lower 16 bits
uint16_t chip_flags = (uint16_t)(chip_rev >> 16);
```

This extracts `0x1F03` from `0x1F030000`, but **ChipRevBits0 stores the flags in the LOWER 16 bits**, not the upper 16 bits!

### Correct Interpretation

The value `0x1F030000` should be interpreted as:
- **Lower 16 bits:** `0x0000` (the actual chip revision flags)
- **Upper 16 bits:** `0x1F03` (NOT the chip flags - possibly version/other data)

### Why We're Getting OCS

With the buggy code extracting `0x1F03`:
- Bit 1 (0x02 - ALICE): SET
- Bit 2 (0x04 - LISA): NOT SET
- Bits 12-13 (0x3000 - HR_AGNUS/HR_DENISE): SET

The AGA check `(chip_flags & 0x0006) == 0x0006` fails because only ALICE is set, not LISA.
The ECS check `(chip_flags & 0x3000)` would pass, so it should return ECS, not OCS.

**BUT** - if the user is seeing OCS, there may be additional issues with RTG mode overriding native chipset detection.

---

## Correct ChipRevBits0 Structure

According to AmigaOS NDK documentation (`graphics/gfxbase.h`):

```c
// Bit positions (GFXB_*)
#define GFXB_BIG_BLITS   0
#define GFXB_HR_AGNUS    0  // ECS
#define GFXB_HR_DENISE   1  // ECS
#define GFXB_AA_ALICE    2  // AGA
#define GFXB_AA_LISA     3  // AGA
#define GFXB_AA_MLISA    4  // Internal use only

// Flag values (GFXF_*)
#define GFXF_BIG_BLITS   (1<<0)  // 0x0001
#define GFXF_HR_AGNUS    (1<<0)  // 0x0001
#define GFXF_HR_DENISE   (1<<1)  // 0x0002
#define GFXF_AA_ALICE    (1<<2)  // 0x0004
#define GFXF_AA_LISA     (1<<3)  // 0x0008
```

**ChipRevBits0 is a ULONG (32-bit) value where flags are in the LOWER 16 bits.**

---

## The Correct Fix

### Line 464 Should Be:

```c
// Extract LOWER 16 bits (the actual chip revision flags)
uint16_t chip_flags = (uint16_t)(chip_rev & 0xFFFF);
```

### Updated Detection Logic:

```c
// AGA check: Both ALICE (0x0004) and LISA (0x0008) must be set
if ((chip_flags & 0x000C) == 0x000C) {
    result = SystemChipset_AGA;
}
// ECS check: HR_DENISE (0x0002) or HR_AGNUS (0x0001)
// Note: HR_AGNUS and BIG_BLITS share bit 0
else if (chip_flags & 0x0003) {
    result = SystemChipset_ECS;
}
else {
    result = SystemChipset_OCS;
}
```

**Key Changes:**
1. Use `& 0xFFFF` instead of `>> 16` to get lower 16 bits
2. Change AGA mask from `0x0006` to `0x000C` (bits 2+3, not bits 1+2)
3. Change ECS mask from `0x3000` to `0x0003` (bits 0+1, not bits 12+13)

---

## RTG Detection Issue

### What is RTG?

RTG (Retargetable Graphics) allows AmigaOS to use third-party graphics cards that bypass the native OCS/ECS/AGA chipsets:

- **UAEGFX** - UAE emulator graphics (software RTG in WinUAE/FS-UAE)
- **Picasso96** - RTG driver for Picasso series cards
- **CyberGraphX** - RTG driver for CyberVision cards (became the de facto standard)

### The Problem

When RTG is active, the system may be using a graphics card that has NO native chipset, or the native chipset is inactive. Our current code only detects the **native chipset** via `ChipRevBits0`, not whether RTG is active.

### Why This Matters

An A4000 with AGA chipset might be running in:
1. **Native AGA mode** - Using Paula, Denise, Alice, Lisa chips
2. **RTG mode (UAEGFX/Picasso96)** - Native chipset idle, software rendering

The user's theory about UAEGFX is likely correct if they're running WinUAE/FS-UAE with RTG enabled.

---

## Comprehensive Detection Strategy

We need **TWO separate detection functions:**

### 1. Native Chipset Detection

Detects the hardware chipset (OCS/ECS/AGA) regardless of whether it's active:

```c
SystemChipset __detect_native_chipset(void)
{
    // Uses ChipRevBits0 as corrected above
}
```

### 2. RTG Detection

Detects if RTG is active and what type:

```c
typedef enum {
    SystemRTG_None = 0,      // No RTG, using native chipset
    SystemRTG_UAEGFX = 1,    // UAE Graphics (emulator)
    SystemRTG_Picasso96 = 2, // Picasso96 driver
    SystemRTG_CyberGraphX = 3 // CyberGraphX driver
} SystemRTG;

SystemRTG __detect_rtg(void)
{
    struct Library *P96Base;
    struct Library *CyberGfxBase;

    // Try to open Picasso96 library
    P96Base = OpenLibrary("Picasso96API.library", 0L);
    if (P96Base != NULL) {
        CloseLibrary(P96Base);

        // Check if it's UAEGFX (common in emulators)
        // UAEGFX boards typically have specific board names
        // More sophisticated detection would query board info
        return SystemRTG_Picasso96;  // or UAEGFX variant
    }

    // Try to open CyberGraphX library
    CyberGfxBase = OpenLibrary("cybergraphics.library", 40L);
    if (CyberGfxBase != NULL) {
        CloseLibrary(CyberGfxBase);
        return SystemRTG_CyberGraphX;
    }

    return SystemRTG_None;
}
```

### 3. Combined Detection

For user-facing display:

```c
typedef struct {
    SystemChipset native_chipset;  // OCS/ECS/AGA
    SystemRTG rtg_mode;             // RTG type or None
    bool using_rtg;                 // Is RTG currently active?
} SystemGraphics;

SystemGraphics __detect_graphics(void)
{
    SystemGraphics result;
    result.native_chipset = __detect_native_chipset();
    result.rtg_mode = __detect_rtg();
    result.using_rtg = (result.rtg_mode != SystemRTG_None);
    return result;
}
```

---

## Enhanced Detection with Board Query

For more precise RTG detection:

```c
#include <libraries/Picasso96.h>

bool __is_rtg_screen_active(void)
{
    struct Library *P96Base;
    bool is_rtg = false;

    P96Base = OpenLibrary("Picasso96API.library", 0L);
    if (P96Base != NULL) {
        // Check if current screen is RTG
        struct Screen *screen = LockPubScreen(NULL);
        if (screen != NULL) {
            // Query if this screen is using RTG
            // (Picasso96 provides p96GetBitMapAttr() for this)
            is_rtg = p96GetBitMapAttr(screen->RastPort.BitMap, P96BMA_ISP96) != 0;
            UnlockPubScreen(NULL, screen);
        }
        CloseLibrary(P96Base);
    }

    return is_rtg;
}
```

---

## Important Notes

### SetPatch Requirement

From the documentation: **ChipRevBits0 detection will NOT work unless V39 SetPatch has been executed.**

This is crucial for AmigaOS 2.x/3.x systems. Our code should:
1. Check if `graphics.library` version >= 39
2. Assume OCS if version < 39
3. Trust ChipRevBits0 only on v39+

### Library Version Check

```c
SystemChipset __detect_chipset(void)
{
    struct Library *GfxBase;

    GfxBase = OpenLibrary("graphics.library", 36L);
    if (GfxBase == NULL) {
        return SystemChipset_OCS;  // Pre-2.0 system
    }

    // Check version - ChipRevBits0 only reliable on v39+
    if (GfxBase->lib_Version < 39) {
        CloseLibrary(GfxBase);
        return SystemChipset_OCS;  // Can't trust ChipRevBits0
    }

    // Now safe to check ChipRevBits0...
    // [rest of detection code]
}
```

---

## Summary of Fixes Needed

### Immediate Fixes (Critical):
1. **Line 464**: Change `>> 16` to `& 0xFFFF`
2. **Line 468**: Change AGA mask from `0x0006` to `0x000C`
3. **Line 472**: Change ECS mask from `0x3000` to `0x0003`
4. **Line 420**: Add version check for lib_Version >= 39

### Enhanced Features (Recommended):
1. Add `SystemRTG` enum and `__detect_rtg()` function
2. Add `SystemGraphics` struct combining native + RTG info
3. Update Novus stdlib to expose RTG detection
4. Add proper Picasso96/CyberGraphX detection

### Documentation Updates:
1. Document RTG vs native chipset distinction
2. Add warning about SetPatch requirement
3. Explain when to use native vs RTG detection

---

## Test Cases

### Expected Results After Fix:

| ChipRevBits0 | Extracted Flags | Expected Result |
|--------------|----------------|-----------------|
| `0x1F030000` | `0x0000`       | OCS (no flags)  |
| `0x00000001` | `0x0001`       | ECS (HR_AGNUS)  |
| `0x00000002` | `0x0002`       | ECS (HR_DENISE) |
| `0x00000003` | `0x0003`       | ECS (both)      |
| `0x0000000C` | `0x000C`       | AGA (ALICE+LISA)|
| `0x00001F0F` | `0x1F0F`       | AGA (bits 2+3 set)|

### RTG Scenarios:

1. **A4000 with AGA, no RTG active**
   - Native: AGA
   - RTG: None
   - Display: "AGA chipset"

2. **A4000 with AGA, UAEGFX RTG active**
   - Native: AGA
   - RTG: UAEGFX/Picasso96
   - Display: "UAEGFX (RTG) on AGA system"

3. **A1200 with ECS, Picasso96 card**
   - Native: ECS
   - RTG: Picasso96
   - Display: "Picasso96 (RTG) on ECS system"

---

## References

- AmigaOS NDK 3.9 `graphics/gfxbase.h` - ChipRevBits0 definition
- AmigaOS Wiki: Classic Graphics Primitives
- EAB Thread: "Best way of detecting the AGA chipset?"
- Picasso96 API documentation
- CyberGraphX library documentation
