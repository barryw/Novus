# Novus Sprite System

**Version:** 1.0
**Status:** Implementation Ready
**Target:** AmigaOS 3.x, NDK 3.9, 68k processors

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Sprite Definition Syntax](#sprite-definition-syntax)
3. [Runtime API](#runtime-api)
4. [Color Management](#color-management)
5. [Memory Management](#memory-management)
6. [File Loading](#file-loading)
7. [Assembly Data](#assembly-data)
8. [Hardware Details](#hardware-details)
9. [Implementation Guide](#implementation-guide)
10. [Complete Examples](#complete-examples)

---

## Quick Start

Get a sprite on screen in 30 seconds:

```novus
from std::graphics::sprite import sprite, SpriteHandle
from std::graphics::screen import open_screen
from std::graphics::palette import rgb
from std::hardware::timer import vblank_wait

// Define sprite at compile time (always 16 pixels wide)
const CURSOR: Sprite = sprite! {
    pixels: [
        "1000000000000000",
        "1100000000000000",
        "1110000000000000",
        "1111000000000000",
        "1111100000000000",
        "1111110000000000",
        "1111111000000000",
        "1111111100000000",
    ]
}

pub fn main() -> i32 {
    let screen = open_screen(320, 256, 4).unwrap_or_panic()

    // Upload to CHIP RAM
    let cursor_data = CURSOR.upload().unwrap_or_panic()

    // Allocate hardware channel
    let mut sprite = SpriteHandle::alloc(0, &screen).unwrap_or_panic()

    // Set colors (color 0 = transparent)
    sprite.set_colors(&[rgb(255,255,255), rgb(128,128,128), rgb(64,64,64)]).unwrap_or_panic()

    // Show sprite
    sprite.show(&cursor_data, x: 160, y: 100).unwrap_or_panic()

    // Move it around
    for i in 0..100 {
        sprite.move_to(x: 160 + i, y: 100)
        vblank_wait()
    }

    return 0
}
```

**Key Points:**
- Sprites are ALWAYS 16 pixels wide (hardware constraint)
- Regular sprites have 4 colors (2 bitplanes), attached sprites have 16 colors (4 bitplanes)
- Type system enforces constraints - no depth parameter needed
- Automatic RAII cleanup via Drop trait

---

## Sprite Definition Syntax

### Regular Sprites (4 Colors)

Regular sprites always use 2 bitplanes for 4 colors (0-3):

```novus
from std::graphics::sprite import sprite

const PLAYER: Sprite = sprite! {
    pixels: [
        "0001111111110000",  // 0 = transparent
        "0011111111111000",  // 1 = color 1
        "0111222222221110",  // 2 = color 2
        "0112222222222110",  // 3 = color 3
        "1122233333222211",
        "1222333333332221",
        "1222333333332221",
        "0122333333332210",
        "0122333333332210",
        "0012233333222100",
        "0001222222221000",
        "0000122222210000",
        "0000012222100000",
        "0000001221000000",
        "0000000110000000",
        "0000000000000000",
    ]
}
```

**Validation:**
- Width must be exactly 16 characters (compile error otherwise)
- Only characters '0'-'3' allowed
- All rows must be same length
- Space ' ' and '0' both represent transparent

### Attached Sprites (16 Colors)

Attached sprites use 4 bitplanes for 16 colors (0-F):

```novus
from std::graphics::sprite import attached_sprite

const BOSS: AttachedSprite = attached_sprite! {
    pixels: [
        "0000000AA0000000",  // 0 = transparent
        "000000AAAA000000",  // 1-9 = colors 1-9
        "00000AABBAA00000",  // A-F = colors 10-15
        "0000AABBBBAA0000",
        "000AABBCCBBAA000",
        "00AABBCCCCBBAA00",
        "0AABBCCDDDCBBAA0",
        "0AABCCDDDDCCBAA0",
        "0AABCCDDDDCCBAA0",
        "0AABBCCDDDCBBAA0",
        "00AABBCCCCBBAA00",
        "000AABBCCBBAA000",
        "0000AABBBBAA0000",
        "00000AABBAA00000",
        "000000AAAA000000",
        "0000000AA0000000",
    ]
}
```

**Validation:**
- Width must be exactly 16 characters
- Characters '0'-'9' and 'A'-'F' (case-insensitive) allowed
- Attached sprites automatically use two hardware channels (even+odd pair)

### Pixel Character Reference

| Character | Regular Sprite | Attached Sprite | Meaning |
|-----------|---------------|-----------------|---------|
| '0' or ' ' | Transparent | Transparent | Color index 0 |
| '1'-'3' | Colors 1-3 | Colors 1-3 | Color indices |
| '4'-'9' | ERROR | Colors 4-9 | Color indices |
| 'A'-'F' | ERROR | Colors 10-15 | Color indices (hex) |

---

## Runtime API

### Sprite Type (Compile-Time)

```novus
pub struct Sprite {
    width: u16,      // Always 16
    height: u16,     // Height in pixels
    data: &[u16],    // Compile-time sprite data
}

impl Sprite {
    pub fn width(&self) -> u16      // Always 16
    pub fn height(&self) -> u16
    pub fn upload(&self) -> Result<SpriteData, GraphicsError>
}
```

### SpriteData (CHIP RAM)

```novus
pub struct SpriteData {
    chip_ptr: *u16,      // CHIP RAM pointer
    size_bytes: usize,   // Total size
    height: u16,
    // Automatically freed on Drop
}

impl SpriteData {
    pub fn from_sprite(sprite: &Sprite) -> Result<SpriteData, GraphicsError>
    pub fn height(&self) -> u16
    pub unsafe fn as_ptr(&self) -> *u16
}

impl Drop for SpriteData {
    fn drop(&mut self) {
        // Automatic CHIP RAM cleanup
    }
}
```

### SpriteHandle (Hardware Channel)

```novus
pub struct SpriteHandle {
    channel: u8,              // 0-7
    screen: &ScreenHandle,
    visible: bool,
    current_sprite: Option<&SpriteData>,
    position: (u16, u16),
}

impl SpriteHandle {
    // Allocate hardware sprite channel (0-7)
    pub fn alloc(channel: u8, screen: &ScreenHandle)
        -> Result<SpriteHandle, GraphicsError>

    // Show sprite at position
    pub fn show(&mut self, sprite_data: &SpriteData, x: u16, y: u16)
        -> Result<(), GraphicsError>

    // Move sprite (fast - just updates position registers)
    pub fn move_to(&mut self, x: u16, y: u16)

    // Hide sprite (writes to SPRxCTL register)
    pub fn hide(&mut self)

    // Change sprite image (updates DMA pointer)
    pub fn set_image(&mut self, sprite_data: &SpriteData)
        -> Result<(), GraphicsError>

    // Set 3-color palette (automatically writes to correct color registers)
    pub fn set_colors(&mut self, colors: &[Color; 3])
        -> Result<(), GraphicsError>

    // Query state
    pub fn is_visible(&self) -> bool
    pub fn position(&self) -> (u16, u16)
}

impl Drop for SpriteHandle {
    fn drop(&mut self) {
        self.hide()  // Auto-hide when handle dropped
    }
}
```

### AttachedSpriteHandle (16-Color Sprites)

```novus
pub struct AttachedSpriteHandle {
    even_channel: u8,         // 0, 2, 4, or 6
    odd_channel: u8,          // even_channel + 1
    screen: &ScreenHandle,
    visible: bool,
    current_sprite: Option<&SpriteData>,
    position: (u16, u16),
}

impl AttachedSpriteHandle {
    // Allocate attached sprite pair
    // even_channel must be 0, 2, 4, or 6
    pub fn alloc_pair(even_channel: u8, screen: &ScreenHandle)
        -> Result<AttachedSpriteHandle, GraphicsError>

    // Show 16-color sprite
    pub fn show(&mut self, sprite_data: &SpriteData, x: u16, y: u16)
        -> Result<(), GraphicsError>

    // Move both sprites together
    pub fn move_to(&mut self, x: u16, y: u16)

    // Hide both sprites
    pub fn hide(&mut self)

    // Change sprite image
    pub fn set_image(&mut self, sprite_data: &SpriteData)
        -> Result<(), GraphicsError>

    // Set 15-color palette (color 0 = transparent)
    pub fn set_colors(&mut self, colors: &[Color; 15])
        -> Result<(), GraphicsError>

    // Query state
    pub fn is_visible(&self) -> bool
    pub fn position(&self) -> (u16, u16)
    pub fn channels(&self) -> (u8, u8)
}

impl Drop for AttachedSpriteHandle {
    fn drop(&mut self) {
        self.hide()  // Auto-hide both sprites
    }
}
```

---

## Color Management

### Automatic Color Register Mapping

The sprite handle automatically knows which color registers to use based on the channel number. You never need to know register addresses.

**Color Register Assignment:**

| Sprite Channel(s) | Type | Color Registers | Colors Available |
|-------------------|------|-----------------|------------------|
| 0 or 1 | Regular | 17, 18, 19 | 3 + transparent |
| 2 or 3 | Regular | 21, 22, 23 | 3 + transparent |
| 4 or 5 | Regular | 25, 26, 27 | 3 + transparent |
| 6 or 7 | Regular | 29, 30, 31 | 3 + transparent |
| 0+1 | Attached | 16-31 | 15 + transparent |
| 2+3 | Attached | 16-31 | 15 + transparent |
| 4+5 | Attached | 16-31 | 15 + transparent |
| 6+7 | Attached | 16-31 | 15 + transparent |

**Note:** Sprites 0 and 1 share color registers 17-19 when used as independent sprites. Same for 2+3, 4+5, 6+7.

### Color Type

```novus
pub struct Color {
    r: u8,  // 0-255
    g: u8,  // 0-255
    b: u8,  // 0-255
}

impl Color {
    pub fn rgb(r: u8, g: u8, b: u8) -> Color
    pub fn from_amiga(color: u16) -> Color
    pub fn to_amiga_color(&self) -> u16
}

// Common colors
pub const BLACK:   Color = Color { r: 0,   g: 0,   b: 0   }
pub const WHITE:   Color = Color { r: 255, g: 255, b: 255 }
pub const RED:     Color = Color { r: 255, g: 0,   b: 0   }
pub const GREEN:   Color = Color { r: 0,   g: 255, b: 0   }
pub const BLUE:    Color = Color { r: 0,   g: 0,   b: 255 }
pub const YELLOW:  Color = Color { r: 255, g: 255, b: 0   }
pub const MAGENTA: Color = Color { r: 255, g: 0,   b: 255 }
pub const CYAN:    Color = Color { r: 0,   g: 255, b: 255 }
```

### Setting Colors

```novus
// Regular sprite (3 colors + transparent)
let mut sprite = SpriteHandle::alloc(0, &screen)?
sprite.set_colors(&[
    Color::rgb(255, 0, 0),    // Color 1: Red
    Color::rgb(0, 255, 0),    // Color 2: Green
    Color::rgb(0, 0, 255),    // Color 3: Blue
])?

// Attached sprite (15 colors + transparent)
let mut boss = AttachedSpriteHandle::alloc_pair(0, &screen)?
boss.set_colors(&[
    Color::rgb(255, 255, 255),  // Color 1
    Color::rgb(200, 200, 200),  // Color 2
    // ... 13 more colors ...
])?
```

---

## Memory Management

### RAII (Automatic Cleanup)

All sprite resources use RAII for automatic cleanup:

```novus
fn demo() -> Result<(), GraphicsError> {
    let screen = open_screen(320, 256, 4)?

    // Upload sprite to CHIP RAM
    let sprite_data = CURSOR.upload()?
    // sprite_data owns CHIP RAM allocation

    // Allocate hardware channel
    let mut sprite = SpriteHandle::alloc(0, &screen)?

    sprite.show(&sprite_data, x: 160, y: 100)?

    // Automatic cleanup on scope exit:
    // 1. sprite.drop() - hides sprite, releases channel
    // 2. sprite_data.drop() - frees CHIP RAM
    // 3. screen.drop() - closes screen

    Ok(())
}
```

### Sharing Sprite Data

Multiple sprites can share the same `SpriteData`:

```novus
let bullet_data = BULLET.upload()?

// Use same data for multiple bullets
let mut bullet0 = SpriteHandle::alloc(0, &screen)?
let mut bullet1 = SpriteHandle::alloc(1, &screen)?
let mut bullet2 = SpriteHandle::alloc(2, &screen)?

bullet0.show(&bullet_data, x: 50, y: 100)?
bullet1.show(&bullet_data, x: 100, y: 120)?
bullet2.show(&bullet_data, x: 150, y: 140)?

// bullet_data freed only once when it goes out of scope
```

### Explicit Cleanup with defer

```novus
fn demo() -> Result<(), GraphicsError> {
    let screen = open_screen(320, 256, 4)?
    defer { screen.close() }

    let sprite_data = CURSOR.upload()?
    defer { drop(sprite_data) }

    let mut sprite = SpriteHandle::alloc(0, &screen)?
    defer { sprite.hide() }

    sprite.show(&sprite_data, x: 160, y: 100)?

    // defer blocks run in reverse order on exit

    Ok(())
}
```

---

## File Loading

### Compile-Time File Loading

Load sprite data from disk at compile time:

```novus
// Load regular sprite
const SHIP: Sprite = sprite_from_file!("assets/ship.sprite")

// Load attached sprite
const ENEMY: AttachedSprite = attached_sprite_from_file!("assets/enemy.sprite")
```

### .sprite File Format

Simple text format for sprite data:

```
# ship.sprite
WIDTH 16
HEIGHT 12
TYPE REGULAR
PIXELS
0000011111100000
0001122222211000
0012222222222100
0122222222222210
0122222222222210
0122222222222210
0012222222222100
0001122222211000
0000011111100000
0000001111000000
0000000110000000
0000000000000000
```

**For attached sprites:**

```
# boss.sprite
WIDTH 16
HEIGHT 16
TYPE ATTACHED
PIXELS
0000000AA0000000
000000AAAA000000
00000AABBAA00000
# ... etc
```

**Features:**
- Lines starting with '#' are comments
- WIDTH, HEIGHT, TYPE are required metadata
- TYPE must be REGULAR or ATTACHED
- PIXELS section contains the visual data
- File is read and embedded at compile time
- Errors show file path and line number

---

## Assembly Data

### Pasting Existing Assembly Sprites

For users with existing assembly sprite data:

```novus
const ALIEN: Sprite = sprite_from_asm! {
    height: 16,
    data: [
        0x0000, 0x0000,  // Control words (filled at runtime)
        0x0000, 0x0000,
        0x0FF0, 0x0000,  // Row 0: plane 0, plane 1
        0x1FF8, 0x0810,  // Row 1: plane 0, plane 1
        0x3FFC, 0x1C38,  // Row 2: plane 0, plane 1
        0x7FFE, 0x3E7C,  // Row 3: plane 0, plane 1
        0xFFFF, 0x7EFE,  // Row 4: plane 0, plane 1
        0xFFFF, 0x7EFE,  // Row 5: plane 0, plane 1
        0x7FFE, 0x3E7C,  // Row 6: plane 0, plane 1
        0x3FFC, 0x1C38,  // Row 7: plane 0, plane 1
        0x1FF8, 0x0810,  // Row 8: plane 0, plane 1
        0x0FF0, 0x0000,  // Row 9: plane 0, plane 1
        0x07E0, 0x0000,  // Row 10: plane 0, plane 1
        0x03C0, 0x0000,  // Row 11: plane 0, plane 1
        0x0180, 0x0000,  // Row 12: plane 0, plane 1
        0x0000, 0x0000,  // Row 13: plane 0, plane 1
        0x0000, 0x0000,  // Row 14: plane 0, plane 1
        0x0000, 0x0000,  // Row 15: plane 0, plane 1
        0x0000, 0x0000,  // Terminator
        0x0000, 0x0000,
    ]
}
```

**For attached sprites:**

```novus
const BOSS: AttachedSprite = attached_sprite_from_asm! {
    height: 12,
    data: [
        // Even sprite (planes 0-1)
        0x0000, 0x0000,  // Control words
        0x0000, 0x0000,
        0x0FF0, 0x0000,  // Row 0
        // ... 11 more rows
        0x0000, 0x0000,  // Terminator
        0x0000, 0x0000,

        // Odd sprite (planes 2-3)
        0x0000, 0x0000,  // Control words
        0x0000, 0x0000,
        0x0000, 0x0FF0,  // Row 0
        // ... 11 more rows
        0x0000, 0x0000,  // Terminator
        0x0000, 0x0000,
    ]
}
```

**Validation:**
- Array size must match: 4 (control) + (height × 2 × 2) + 4 (terminator)
- For regular sprite: height × 4 words total data
- For attached sprite: height × 8 words total data (two sprites)

---

## Hardware Details

### Sprite Data Format in Memory

**Regular Sprite (2 bitplanes):**

```
Offset   Content
------   -------
0x00     SPRxPOS value (VSTART, HSTART high byte)
0x02     SPRxCTL value (VSTOP, ATTACH, HSTART low bit)
0x04     Row 0, Plane 0 (16 bits)
0x06     Row 0, Plane 1 (16 bits)
0x08     Row 1, Plane 0
0x0A     Row 1, Plane 1
...      (height × 4 bytes)
End      0x0000 (terminator)
End+2    0x0000 (terminator)

Total: 8 + (height × 4) bytes
```

**Attached Sprite (4 bitplanes):**

Two sprites in memory:
- Even sprite: Control + Planes 0-1 + Terminator
- Odd sprite: Control + Planes 2-3 + Terminator (ATTACH bit set)

```
Even sprite pointer → [Control][Plane 0/1 data][Terminator]
Odd sprite pointer →  [Control][Plane 2/3 data][Terminator]
```

### Hardware Registers

**Sprite N registers (N = 0-7):**

```
SPRnPTH  ($DFF120 + N×8): Sprite pointer high word
SPRnPTL  ($DFF122 + N×8): Sprite pointer low word
SPRnPOS  ($DFF140 + N×8): Sprite position start
SPRnCTL  ($DFF142 + N×8): Sprite control
SPRnDATA ($DFF144 + N×8): Sprite data A (plane 0)
SPRnDATB ($DFF146 + N×8): Sprite data B (plane 1)
```

**Example (Sprite 3):**
```
SPR3PTH = $DFF138
SPR3PTL = $DFF13A
SPR3POS = $DFF158
SPR3CTL = $DFF15A
```

### Position Calculation

**Horizontal:**
```
hardware_x = (logical_x + $81) >> 1
lsb_bit = (logical_x + $81) & 1

SPRxPOS bits 7-0 = hardware_x
SPRxCTL bit 0 = lsb_bit
```

**Vertical:**
```
vstart = logical_y
vstop = logical_y + height

SPRxPOS bits 15-8 = vstart
SPRxCTL bits 15-8 = vstop
```

**The $81 offset** is a hardware quirk for display timing alignment.

### Hardware Constraints

- Width: Exactly 16 pixels (enforced by hardware DMA)
- Height: Arbitrary (practical limit ~256 scanlines)
- Position: 0-319 horizontal (lores), 0-255 vertical (standard)
- Depth: 2 or 4 bitplanes only
- Memory: Must reside in CHIP RAM for DMA access
- Priority: Sprite 0 highest, Sprite 7 lowest

---

## Implementation Guide

### Compiler Implementation Checklist

**Phase 1: Macros**
- [ ] `sprite!` macro - parse visual pixel data
- [ ] `attached_sprite!` macro - parse hex pixel data
- [ ] Width validation (must be 16)
- [ ] Color range validation (0-3 for sprite!, 0-F for attached_sprite!)
- [ ] Row consistency checking
- [ ] Generate sprite data array in hardware format

**Phase 2: Runtime Types**
- [ ] `Sprite` and `AttachedSprite` types
- [ ] `SpriteData` with Drop trait for CHIP RAM
- [ ] `SpriteHandle` with Drop trait for auto-hide
- [ ] `AttachedSpriteHandle` with Drop trait

**Phase 3: Hardware Operations**
- [ ] `show()` - set DMA pointer + position + enable
- [ ] `hide()` - write SPRxCTL to disable
- [ ] `move_to()` - update SPRxPOS/SPRxCTL
- [ ] `set_image()` - swap DMA pointer
- [ ] `set_colors()` - write color registers

**Phase 4: File Loading**
- [ ] `sprite_from_file!` macro
- [ ] `.sprite` format parser
- [ ] Compile-time file I/O
- [ ] Error messages with file context

**Phase 5: Assembly Support**
- [ ] `sprite_from_asm!` macro
- [ ] Data size validation
- [ ] Hex literal parsing

### Bitplane Conversion Algorithm

Convert indexed pixels to hardware format:

```rust
fn convert_to_sprite_data(pixels: &[&str]) -> Vec<u16> {
    let height = pixels.len();
    let mut result = Vec::new();

    // Control words (filled at runtime)
    result.push(0x0000);
    result.push(0x0000);

    // Convert each row
    for row in pixels {
        let mut plane0: u16 = 0;
        let mut plane1: u16 = 0;

        for (x, ch) in row.chars().enumerate() {
            let color_index = char_to_color(ch);
            let bit = 15 - x;  // MSB first

            if color_index & 1 != 0 {
                plane0 |= 1 << bit;
            }
            if color_index & 2 != 0 {
                plane1 |= 1 << bit;
            }
        }

        result.push(plane0);
        result.push(plane1);
    }

    // Terminator
    result.push(0x0000);
    result.push(0x0000);

    result
}

fn char_to_color(ch: char) -> u8 {
    match ch {
        '0' | ' ' => 0,
        '1' => 1,
        '2' => 2,
        '3' => 3,
        '4' => 4,
        '5' => 5,
        '6' => 6,
        '7' => 7,
        '8' => 8,
        '9' => 9,
        'A' | 'a' => 10,
        'B' | 'b' => 11,
        'C' | 'c' => 12,
        'D' | 'd' => 13,
        'E' | 'e' => 14,
        'F' | 'f' => 15,
        _ => panic!("Invalid pixel character"),
    }
}
```

### Error Messages

**Width Error:**
```
error: sprite width must be exactly 16 pixels
  --> game.novus:42:9
   |
42 |         "01234567890123456789",
   |         ^^^^^^^^^^^^^^^^^^^^^^ this row has 20 pixels
   |
   = note: hardware sprites must be exactly 16 pixels wide
```

**Color Range Error:**
```
error: color index exceeds maximum for sprite type
  --> game.novus:87:23
   |
87 |         "012345670123",
   |                       ^ color '7' requires 4 bitplanes
   |
   = note: regular sprites support colors 0-3 only
   = help: use `attached_sprite!` for 16-color sprites
```

**Invalid Channel Error:**
```
error: attached sprite channel must be even
  --> game.novus:120:38
   |
120 | let sprite = AttachedSpriteHandle::alloc_pair(1, &screen)?
    |                                                ^ channel 1 is odd
    |
    = note: attached sprites must use channels 0, 2, 4, or 6
```

---

## Complete Examples

### Example 1: Simple Cursor

```novus
from std::graphics::sprite import sprite, SpriteHandle
from std::graphics::screen import open_screen
from std::graphics::palette import rgb
from std::hardware::timer import vblank_wait

const CURSOR: Sprite = sprite! {
    pixels: [
        "1000000000000000",
        "1100000000000000",
        "1110000000000000",
        "1111000000000000",
        "1111100000000000",
        "1111110000000000",
        "1111111000000000",
        "1111111100000000",
        "1111111110000000",
        "1111110000000000",
        "1101100000000000",
        "1000110000000000",
        "0000110000000000",
        "0000011000000000",
        "0000011000000000",
        "0000001000000000",
    ]
}

pub fn main() -> i32 {
    let screen = open_screen(320, 256, 4).unwrap_or_panic()
    let cursor_data = CURSOR.upload().unwrap_or_panic()
    let mut cursor = SpriteHandle::alloc(0, &screen).unwrap_or_panic()

    cursor.set_colors(&[rgb(0,0,0), rgb(255,255,255), rgb(128,128,128)]).unwrap_or_panic()
    cursor.show(&cursor_data, x: 160, y: 100).unwrap_or_panic()

    for i in 0..100 {
        cursor.move_to(x: 160 + i, y: 100)
        vblank_wait()
    }

    return 0
}
```

### Example 2: Attached Sprite (16 Colors)

```novus
from std::graphics::sprite import attached_sprite, AttachedSpriteHandle
from std::graphics::screen import open_screen
from std::graphics::palette import rgb
from std::hardware::timer import vblank_wait

const BOSS: AttachedSprite = attached_sprite! {
    pixels: [
        "0000000AA0000000",
        "000000AAAA000000",
        "00000AABBAA00000",
        "0000AABBBBAA0000",
        "000AABBCCBBAA000",
        "00AABBCCCCBBAA00",
        "0AABBCCDDDCBBAA0",
        "0AABCCDDDDCCBAA0",
        "0AABCCDDDDCCBAA0",
        "0AABBCCDDDCBBAA0",
        "00AABBCCCCBBAA00",
        "000AABBCCBBAA000",
        "0000AABBBBAA0000",
        "00000AABBAA00000",
        "000000AAAA000000",
        "0000000AA0000000",
    ]
}

pub fn main() -> i32 {
    let screen = open_screen(320, 256, 4).unwrap_or_panic()
    let boss_data = BOSS.upload().unwrap_or_panic()
    let mut boss = AttachedSpriteHandle::alloc_pair(0, &screen).unwrap_or_panic()

    let colors = [
        rgb(0, 0, 0),       // 1: Black
        rgb(50, 50, 50),    // 2: Dark gray
        rgb(100, 100, 100), // 3: Medium gray
        rgb(150, 150, 150), // 4: Light gray
        rgb(200, 0, 0),     // 5: Dark red
        rgb(255, 0, 0),     // 6: Red
        rgb(255, 100, 0),   // 7: Orange
        rgb(255, 200, 0),   // 8: Yellow-orange
        rgb(255, 255, 0),   // 9: Yellow
        rgb(170, 85, 0),    // A: Dark brown
        rgb(220, 110, 0),   // B: Brown
        rgb(255, 150, 50),  // C: Light brown
        rgb(255, 200, 100), // D: Tan
        rgb(100, 50, 0),    // E: Very dark brown
        rgb(255, 220, 180), // F: Beige
    ]
    boss.set_colors(&colors).unwrap_or_panic()

    boss.show(&boss_data, x: 160, y: 100).unwrap_or_panic()

    var dx: i16 = 1
    var x: u16 = 160
    for frame in 0..300 {
        x = (x as i16 + dx) as u16
        if x >= 280 || x <= 40 {
            dx = -dx
        }
        boss.move_to(x, y: 100)
        vblank_wait()
    }

    return 0
}
```

### Example 3: Multiple Bullets

```novus
from std::graphics::sprite import sprite, SpriteHandle
from std::graphics::screen import open_screen
from std::graphics::palette import rgb
from std::hardware::timer import vblank_wait

const BULLET: Sprite = sprite! {
    pixels: [
        "0000011111100000",
        "0001122222211000",
        "0012222222222100",
        "0122222222222210",
        "0122222222222210",
        "0012222222222100",
        "0001122222211000",
        "0000011111100000",
    ]
}

pub fn main() -> i32 {
    let screen = open_screen(320, 256, 4).unwrap_or_panic()
    let bullet_data = BULLET.upload().unwrap_or_panic()

    var sprites: [SpriteHandle; 4] = []
    for i in 0..4 {
        sprites[i] = SpriteHandle::alloc(i as u8, &screen).unwrap_or_panic()
        sprites[i].set_colors(&[rgb(255,255,0), rgb(255,128,0), rgb(255,0,0)]).unwrap_or_panic()
        sprites[i].show(&bullet_data, x: 50 + i * 80, y: 200).unwrap_or_panic()
    }

    for frame in 0..200 {
        for i in 0..4 {
            let (x, y) = sprites[i].position()
            let new_y = if y > 8 { y - 2 } else { 200 }
            sprites[i].move_to(x, new_y)
        }
        vblank_wait()
    }

    return 0
}
```

### Example 4: Animation Sequence

```novus
from std::graphics::sprite import sprite, SpriteHandle
from std::graphics::screen import open_screen
from std::hardware::timer import vblank_wait

const WALK_1: Sprite = sprite! {
    pixels: [ /* 16x16 walk frame 1 */ ]
}

const WALK_2: Sprite = sprite! {
    pixels: [ /* 16x16 walk frame 2 */ ]
}

const WALK_3: Sprite = sprite! {
    pixels: [ /* 16x16 walk frame 3 */ ]
}

pub fn main() -> i32 {
    let screen = open_screen(320, 256, 4).unwrap_or_panic()

    let frame1_data = WALK_1.upload().unwrap_or_panic()
    let frame2_data = WALK_2.upload().unwrap_or_panic()
    let frame3_data = WALK_3.upload().unwrap_or_panic()

    let frames = [&frame1_data, &frame2_data, &frame3_data]

    let mut player = SpriteHandle::alloc(0, &screen).unwrap_or_panic()
    player.show(&frame1_data, x: 100, y: 100).unwrap_or_panic()

    var x: u16 = 100
    for step in 0..30 {
        let frame_idx = step % 3
        player.set_image(frames[frame_idx]).unwrap_or_panic()
        player.move_to(x, y: 100)
        x = x + 2
        vblank_wait()
    }

    return 0
}
```

### Example 5: Loading from File

```novus
// assets/ship.sprite file:
//   WIDTH 16
//   HEIGHT 12
//   TYPE REGULAR
//   PIXELS
//   0000011111100000
//   0001122222211000
//   ...

const SHIP: Sprite = sprite_from_file!("assets/ship.sprite")

pub fn main() -> i32 {
    let screen = open_screen(320, 256, 4).unwrap_or_panic()
    let ship_data = SHIP.upload().unwrap_or_panic()

    let mut sprite = SpriteHandle::alloc(0, &screen).unwrap_or_panic()
    sprite.show(&ship_data, x: 160, y: 100).unwrap_or_panic()

    for i in 0..100 {
        vblank_wait()
    }

    return 0
}
```

---

## Performance Characteristics

### Operation Costs (68000 CPU)

| Operation | CPU Cycles | Notes |
|-----------|-----------|-------|
| `sprite.show()` | ~100 | 4 register writes (PTH, PTL, POS, CTL) |
| `sprite.hide()` | ~20 | 1 register write (CTL) |
| `sprite.move_to()` | ~40 | 2 register writes (POS, CTL) |
| `sprite.set_image()` | ~40 | 2 register writes (PTH, PTL) |
| `sprite.set_colors()` | ~60 | 3 register writes (COLOR17-19) |
| `data.upload()` | 1000+ | Copy to CHIP RAM (depends on size) |

### Memory Usage

**Regular Sprite (4 colors):**
```
Size = 8 + (height × 4) bytes

Example: 16-line sprite = 72 bytes CHIP RAM
```

**Attached Sprite (16 colors):**
```
Size = 16 + (height × 8) bytes

Example: 16-line sprite = 144 bytes CHIP RAM
```

### Best Practices

1. **Upload once, reuse everywhere**
   ```novus
   let bullet_data = BULLET.upload()?
   sprite0.show(&bullet_data, x: 50, y: 100)?
   sprite1.show(&bullet_data, x: 100, y: 120)?
   sprite2.show(&bullet_data, x: 150, y: 140)?
   ```

2. **Set colors once, not every frame**
   ```novus
   sprite.set_colors(&[RED, GREEN, BLUE])?

   for i in 0..100 {
       sprite.move_to(x: i, y: 100)  // Fast
       vblank_wait()
   }
   ```

3. **Use move_to() for position updates**
   ```novus
   // Good: Fast position update
   sprite.move_to(x, y)

   // Bad: Expensive DMA pointer update
   sprite.hide()
   sprite.show(&data, x, y)?
   ```

---

## Summary

### Design Principles

1. **Type Safety** - `Sprite` vs `AttachedSprite` enforced by compiler
2. **No Configuration** - Depth is implicit, width is always 16
3. **RAII Memory** - Automatic cleanup via Drop trait
4. **Compile-Time Validation** - Errors caught before runtime
5. **Hardware Abstraction** - Colors, positions automatic
6. **Zero Runtime Cost** - All conversion at compile time

### What Makes This Implementation-Ready

- Clear type system with no ambiguity
- Well-defined memory ownership semantics
- Concrete syntax for all use cases
- Complete API reference with all method signatures
- Detailed implementation roadmap
- Working examples ready to test
- Error messages specified for common mistakes

### Next Steps

1. Implement `sprite!` and `attached_sprite!` macros in compiler
2. Implement `SpriteData`, `SpriteHandle`, `AttachedSpriteHandle` in stdlib
3. Write example programs and test on UAE/real hardware
4. Iterate based on real-world usage

---

**Document Version:** 1.0
**Last Updated:** 2025-11-23
**Status:** Ready for Implementation
