# Graphics Data Unified Design

## Overview

This document specifies the unified graphics data system for Novus, designed to:
1. Minimize runtime overhead on resource-constrained Amiga hardware
2. Push maximum work to compile-time
3. Eliminate duplication between hardware and OS APIs
4. Provide type-safe abstractions for different graphics formats

## Core Principle: Compile-Time First

Static graphics assets (sprites, bitmaps, fonts) are converted to final hardware format **at compile time**. Data is marked with `@chip` attribute, compiler emits `__chip` prefix in C code, and VBCC places it in CHIP RAM.

**Zero runtime overhead** for static assets.

## Dual Nature Types

Each graphics type has two operational modes:

### Compile-Time Constants
- Data pointer points to `__chip` global variable
- `owns_memory: false` - no cleanup needed
- Zero allocation overhead
- Already in final hardware format

### Runtime Allocated
- Data allocated via `ChipMemHandle::new()`
- `owns_memory: true` - Drop trait frees memory
- Used for procedural graphics, dynamic content
- Explicit conversion cost

## SpriteData Refactoring

### Current Structure (hardware sprite.novus)
```novus
pub struct SpriteData {
    chip_mem: ChipMemHandle,  // Always owns memory
    height: u16,
}
```

### New Unified Structure
```novus
pub struct SpriteData {
    data_ptr: *u16,      // Points to sprite data in CHIP RAM
    owns_memory: bool,   // true if runtime allocated, false if compile-time const
    height: u16,         // Sprite height in scanlines
    width: u16,          // Always 16 for regular sprites
}

impl SpriteData {
    // Compile-time: points to __chip constant (future, requires compiler support)
    pub const fn from_const(data: &[u16], height: u16) -> SpriteData {
        return SpriteData {
            data_ptr: (*u16)data,
            owns_memory: false,
            width: 16,
            height: height,
        }
    }

    // Runtime: allocate and copy (current behavior)
    pub fn from_raw(data: *u16, len: u32) -> Result<SpriteData, GraphicsError> {
        // ... existing implementation, but using ChipMemHandle internally
        // Set owns_memory = true
    }

    pub fn as_ptr(&self) -> *u16 {
        return self.data_ptr
    }

    pub fn height(&self) -> u16 {
        return self.height
    }
}

impl Drop for SpriteData {
    fn drop(&mut self) {
        if self.owns_memory {
            // Free allocated CHIP RAM
            unsafe {
                FreeVec((*u8)self.data_ptr)
            }
        }
        // If !owns_memory, it's a compile-time constant, don't free
    }
}
```

### Migration Strategy

**Phase 1: Internal Refactoring (Backward Compatible)**
1. Add `data_ptr` and `owns_memory` fields to `SpriteData`
2. Keep `chip_mem` field temporarily for compatibility
3. Update `from_raw()` to populate both old and new fields
4. All `as_ptr()` calls use `data_ptr`
5. **No API changes** - fully backward compatible

**Phase 2: Compiler Support**
1. Implement `@chip` attribute
2. Implement compile-time `from_const()` function
3. Users can now write: `@chip const SPRITE: SpriteData = ...`

**Phase 3: Remove Legacy**
1. Remove `chip_mem` field
2. Update all internal code to use new fields

## Sprite Control Words

### Problem
Control words encode VSTART/VSTOP which depend on:
- Y position (runtime information)
- PAL/NTSC offset (system-dependent)

Cannot be computed at compile time.

### Solution
Compile-time sprite data has **zero control words** at offset 0-3:
```novus
const SPRITE_DATA: [u16] = [
    0x0000, 0x0000,  // Control words (filled at runtime)
    0x8000, 0x0000,  // Row 0: plane0, plane1
    0xC000, 0x0000,  // Row 1
    // ... rest of pixel data
    0x0000, 0x0000   // Terminator
]
```

Runtime fills control words when sprite is shown:
```novus
impl SpriteHandle {
    pub fn show(&mut self, sprite: &SpriteData, x: i16, y: i16) -> Result<(), GraphicsError> {
        // Calculate control words based on position
        let vstart = (u16)(y + (i16)get_vstart_offset())
        let vstop = vstart + sprite.height() + 1

        // Write control words to first 4 bytes of sprite data
        unsafe {
            let data = sprite.as_ptr()
            // Calculate and write POS/CTL words
            // ... (existing logic from move_to)
        }

        // Set sprite pointer
        // ...
    }
}
```

## Unified API for Hardware and OS Sprites

Both `amiga::sys::graphics::sprite` (hardware) and `amiga::sys::graphics::os_sprite` (OS) use the **same** `SpriteData` type.

### Hardware API (sprite.novus)
```novus
let sprite_data = SpriteData::from_raw(&CURSOR_DATA, len)?
let mut sprite = SpriteHandle::alloc(0)?
sprite.show(&sprite_data, 100, 50)?
```

### OS API (os_sprite.novus)
```novus
let sprite_data = SpriteData::from_raw(&CURSOR_DATA, len)?  // Same type!
let mut sprite = OsSpriteHandle::alloc(viewport, -1)?
sprite.show(&sprite_data, 100, 50)?
```

**No duplication!** Both APIs share the same data representation.

The only difference is how they're displayed:
- Hardware API: Direct register writes
- OS API: Calls graphics.library functions

## Future: BitmapData and BobData

### BitmapData (Planar Format)
```novus
pub struct BitmapData {
    data_ptr: *u8,       // Points to planar pixel data in CHIP RAM
    owns_memory: bool,   // true if runtime allocated
    width: u16,
    height: u16,
    depth: u8,           // Number of bitplanes (1-8)
    bytes_per_row: u16,  // Word-aligned
}

impl BitmapData {
    pub const fn from_const(data: &[u8], width: u16, height: u16, depth: u8) -> BitmapData
    pub fn alloc(width: u32, height: u32, depth: u32) -> Result<BitmapData, GraphicsError>
    pub fn plane_ptr(&self, plane: u8) -> *u8
    pub fn as_os_bitmap(&self) -> Result<*BitMap, GraphicsError>
}
```

### BobData (Planar + Mask)
```novus
pub struct BobData {
    data_ptr: *u8,       // Planar pixel data + mask plane at end
    owns_memory: bool,
    width: u16,
    height: u16,
    depth: u8,
    bytes_per_row: u16,
}

impl BobData {
    pub const fn from_const(data: &[u8], width: u16, height: u16, depth: u8) -> BobData
    pub fn alloc(width: u32, height: u32, depth: u32) -> Result<BobData, GraphicsError>
    pub fn mask_plane_ptr(&self) -> *u8
}
```

## Compiler Implementation Phases

### Phase 1: @chip Attribute
```novus
@chip
const SPRITE_DATA: [u16] = [...] // Emits __chip in generated C code
```

Compiler:
1. Parse `@chip` attribute on const declarations
2. Emit `__chip` prefix in C code: `__chip const unsigned short sprite_data[] = {...}`
3. VBCC linker places in CHIP RAM section

### Phase 2: Const Functions
Implement compile-time evaluation for:
- `SpriteData::from_const()`
- `BitmapData::from_const()`
- Pixel format converters (interleaved, planar, mask generation)

### Phase 3: @embed Macro
```novus
const SPRITE_RAW: [u8] = @embed("assets/sprite.raw")
const BACKGROUND_IFF: [u8] = @embed("assets/bg.iff")
```

Compiler reads file at compile-time, returns byte array.

### Phase 4: IFF ILBM Parser
Compile-time IFF parser extracts:
- Bitmap dimensions (BMHD chunk)
- Pixel data (BODY chunk, decompress if needed)
- Palette (CMAP chunk)

```novus
@chip
const BACKGROUND: BitmapData = BitmapData::from_iff(@embed("bg.iff"))
const BG_PALETTE: [Color; 32] = Palette::from_iff(@embed("bg.iff"))
```

## Memory Layout Examples

### Sprite (Interleaved)
```
Offset  Content
------  -------
0x00    Control word 0 (POS)  - filled at runtime
0x02    Control word 1 (CTL)  - filled at runtime
0x04    Row 0: plane0
0x06    Row 0: plane1
0x08    Row 1: plane0
0x0A    Row 1: plane1
...
0xNN    Terminator (0x0000)
0xNN+2  Terminator (0x0000)
```

### Bitmap (Planar)
```
Offset    Content
--------  -------
0x0000    Plane 0: all rows
0x0400    Plane 1: all rows (if depth >= 2)
0x0800    Plane 2: all rows (if depth >= 3)
...
```

### BOB (Planar + Mask)
```
Offset    Content
--------  -------
0x0000    Plane 0: all rows
0x0400    Plane 1: all rows
0x0800    Plane 2: all rows
...
0x1000    Mask plane: all rows
```

## Benefits

1. **Zero runtime overhead** for static assets (99% of use cases)
2. **Compile-time validation** of dimensions, color counts, etc.
3. **Smaller executables** - data already in final format, no runtime conversion
4. **Type safety** - can't pass bitmap data to sprite API
5. **No duplication** - hardware and OS APIs share same data types
6. **Explicit cost** - runtime conversion is visible in code
7. **AmigaOS compliant** - uses VBCC `__chip` attribute for CHIP RAM placement

## Implementation Status

- [x] Design document created
- [ ] Phase 1: Internal SpriteData refactoring
- [ ] Phase 2: Unify hardware and OS sprite APIs
- [ ] Phase 3: Add @chip attribute support to compiler
- [ ] Phase 4: Implement BitmapData type
- [ ] Phase 5: Implement BobData type
- [ ] Phase 6: Add @embed macro
- [ ] Phase 7: IFF ILBM parser

## References

- Amiga Hardware Reference Manual (control word format)
- VBCC documentation (`__chip` attribute)
- IFF ILBM specification
- AmigaOS NDK 3.9 (graphics.library, BitMap structure)
