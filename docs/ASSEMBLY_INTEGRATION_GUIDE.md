# Novus Assembly Integration Guide

**Status:** v1.0 Feature
**Last Updated:** 2025-11-09

## Overview

Novus supports **external assembly files** for performance-critical code and direct hardware access. Assembly code integrates seamlessly with Novus using the standard Amiga ABI, allowing bidirectional interop (Novus ↔ Assembly) just like C/VBCC.

**v1.0 Approach:** External `.s` files only (no inline assembly)
**Future:** Inline assembly may be added in v1.5+ if usage patterns demonstrate need

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [When to Use Assembly](#when-to-use-assembly)
3. [Calling Convention](#calling-convention)
4. [Calling Assembly from Novus](#calling-assembly-from-novus)
5. [Calling Novus from Assembly](#calling-novus-from-assembly)
6. [Build System Integration](#build-system-integration)
7. [Complete Examples](#complete-examples)
8. [Best Practices](#best-practices)
9. [Debugging](#debugging)
10. [Common Patterns](#common-patterns)

---

## Quick Start

### 1. Write Assembly Function

Create `src/math.s`:

```asm
; Fast 16.16 fixed-point multiply
; extern fn fixed_mul(a: i32, b: i32) -> i32
        .section .text
        .xdef   _fixed_mul

_fixed_mul:
        ; a in d0, b in d1, return in d0
        muls.l  d1,d0           ; d0 = a * b (68020+)
        asr.l   #16,d0          ; shift right 16 bits
        rts
```

### 2. Declare in Novus

Create `src/main.novus`:

```novus
// Declare assembly function
extern fn fixed_mul(a: i32, b: i32) -> i32

fn main() -> i32 {
    let result = fixed_mul(65536, 32768)  // 1.0 * 0.5 = 0.5
    return result  // Returns 32768 (0.5 in fixed-point)
}
```

### 3. Configure Build

Edit `novus.toml`:

```toml
[package]
name = "my_game"
version = "1.0.0"

[build]
asm_files = ["src/math.s"]
cpu = "68020"  # Required for muls.l instruction
```

### 4. Build

```bash
novusc build
```

The compiler will:
1. Compile `src/main.novus` → `main.o`
2. Assemble `src/math.s` via vasm → `math.o`
3. Link both → executable

---

## When to Use Assembly

### ✅ Good Use Cases

- **Performance-critical inner loops** (after profiling proves need)
- **Direct hardware manipulation** not exposed via `std/ffi`
- **Legacy assembly code integration**
- **Specialized algorithms** (fixed-point math, decompression, crypto)
- **Startup/low-level code** (boot sequence, hardware detection)

### ❌ Bad Use Cases

- **Simple hardware access** → use `std/ffi` abstractions instead
- **Copper/Blitter operations** → use hardware DSLs (§23 of LanguageDesignDoc.md)
- **Memory/task/signal management** → use `std/exec` wrappers
- **Premature optimization** → profile first, use Novus until proven slow

**Rule of Thumb:** If you can do it with OS library calls or Novus stdlib, do that first. Only drop to assembly when profiling shows clear benefit.

---

## Calling Convention

Novus follows the standard **Amiga ABI** (same as C/VBCC):

### Function Arguments

**Arguments passed left-to-right:**
- First 4 args: `d0`, `d1`, `a0`, `a1`
- Additional args: stack (word-aligned)
- 64-bit values: split across register pairs (`d0:d1`)

**Examples:**
```asm
; fn func(a: i32) -> i32
;   a in d0, return in d0

; fn func(a: i32, b: i32) -> i32
;   a in d0, b in d1, return in d0

; fn func(p: *u8, len: u32) -> i32
;   p in a0, len in d1, return in d0

; fn func(a: i32, b: i32, c: i32, d: i32, e: i32) -> i32
;   a in d0, b in d1, c in a0, d in a1, e on stack
```

### Return Values

- **Integer/pointer (≤32-bit):** `d0`
- **64-bit:** `d0` (high 32) and `d1` (low 32)
- **Structs:** returned via pointer passed in `a0` (caller allocates)

### Register Preservation

**Callee-saved (must preserve if used):**
- Data: `d2-d7`
- Address: `a2-a6`

**Caller-saved (can be clobbered):**
- Data: `d0-d1`
- Address: `a0-a1`

**Special:**
- `a6` = frame pointer (if used)
- `a7` = stack pointer (must maintain)

---

## Calling Assembly from Novus

### Step 1: Declare Function

Use `extern fn` to declare an assembly function:

```novus
// Assembly function implemented in math.s
extern fn fast_multiply(a: i32, b: i32) -> i32

// Unsafe operations require unsafe block
extern fn memset_fast(ptr: *u8, value: u8, count: u32)

fn example() {
    // Call assembly function
    let result = fast_multiply(100, 42)

    // Unsafe assembly call
    let buffer: [u8; 1024]
    unsafe {
        memset_fast((*u8)&buffer[0], 0, 1024)
    }
}
```

### Step 2: Implement in Assembly

Create `src/math.s`:

```asm
        .section .text
        .xdef   _fast_multiply      ; Export symbol

_fast_multiply:
        ; a in d0, b in d1, return in d0
        muls.l  d1,d0               ; 68020+ instruction
        rts

        .xdef   _memset_fast

_memset_fast:
        movem.l d2/a2,-(sp)         ; Save callee-saved regs
        move.l  a0,a2               ; ptr in a2
        move.b  d1,d2               ; value in d2
        move.l  a1,d1               ; count in d1
        subq.l  #1,d1               ; count-1 for dbf
.loop:
        move.b  d2,(a2)+            ; *ptr++ = value
        dbf     d1,.loop            ; loop until done
        movem.l (sp)+,d2/a2         ; Restore regs
        rts
```

### Rules for extern fn

1. **Function name must match:** Novus `fast_multiply` → assembly `_fast_multiply` (note leading `_`)
2. **Parameters must match ABI layout:** Compiler passes args in registers/stack
3. **No runtime checks:** Compiler trusts your declaration
4. **Use `unsafe` for memory operations:** Assembly bypasses Novus safety

---

## Calling Novus from Assembly

### Step 1: Make Novus Function Public

```novus
// This function can be called from assembly
pub fn plot_pixel(bitmap: *u8, x: u16, y: u16, color: u8) {
    let offset = (y as u32) * 320 + (x as u32)
    unsafe {
        bitmap[offset] = color
    }
}

// Private functions cannot be called from assembly
fn helper() {  // No pub = internal only
    // ...
}
```

**Symbol Visibility:**
- `pub fn` → exported symbol (`.xdef _function_name` in assembly)
- Private → internal linkage only
- Use `pub` when assembly needs to call Novus code

### Step 2: Call from Assembly

```asm
        .section .text
        .xdef   _draw_hline
        .xref   _plot_pixel         ; Reference Novus function

_draw_hline:
        ; void draw_hline(u8 *bitmap, u16 y, u16 x1, u16 x2, u8 color)
        movem.l d2-d4/a2,-(sp)      ; Save registers
        move.l  4+16(sp),a2         ; bitmap ptr
        move.w  8+16(sp),d2         ; y
        move.w  10+16(sp),d3        ; x1
        move.w  12+16(sp),d4        ; x2
        move.b  14+16(sp),d1        ; color

.loop:
        cmp.w   d4,d3               ; x1 > x2?
        bgt.s   .done

        ; Call plot_pixel(bitmap, x1, y, color)
        move.l  a2,a0               ; arg0: bitmap (a0)
        move.w  d3,d0               ; arg1: x (d0)
        move.w  d2,d1               ; arg2: y (d1)
        ; arg3: color already in low byte
        jsr     _plot_pixel         ; Call Novus function

        addq.w  #1,d3               ; x1++
        bra.s   .loop

.done:
        movem.l (sp)+,d2-d4/a2      ; Restore registers
        rts
```

---

## Build System Integration

### Basic Configuration

Add assembly files to `novus.toml`:

```toml
[package]
name = "my_game"
version = "1.0.0"

[build]
# Simple: list of assembly files
asm_files = [
    "src/math.s",
    "src/graphics.s",
    "src/audio.s"
]

# Default CPU profile for all files
cpu = "68020"
```

### Advanced Configuration

Per-file CPU profiles:

```toml
[package]
name = "my_game"
version = "1.0.0"

[build]
# Global defaults
cpu = "68020"

# Simple list (uses global cpu setting)
asm_files = ["src/util.s"]

# Per-file configuration
[[build.asm]]
file = "src/fast_blit.s"
cpu = "68020"              # Uses 020+ instructions

[[build.asm]]
file = "src/copper.s"
cpu = "68020"              # Minimum supported CPU

[[build.asm]]
file = "src/ammx_blend.s"
cpu = "68080"              # Apollo Core only
```

### Build Process

When you run `novusc build`:

1. **Compile Novus sources** → `.o` files
   ```bash
   novusc compile src/main.novus -o build/main.o
   ```

2. **Assemble assembly files** via vasm → `.o` files
   ```bash
   vasmm68k_mot -Fhunk -m68020 src/math.s -o build/math.o
   ```

3. **Link all objects** via vlink → executable
   ```bash
   vlink -bamigahunk -Bstatic build/*.o -o my_game
   ```

**Compiler flags forwarded to vasm:**
- `--cpu 68020` → `-m68020`
- `--opt-level release` → optimization flags
- Debug symbols maintained for `novusc inspect`

---

## Complete Examples

### Example 1: Fixed-Point Math Library

**src/fixedpoint.s:**
```asm
; Fast 16.16 fixed-point math routines
; Requires 68020+ for muls.l/divs.l
        .section .text

; Fixed-point multiply: (a * b) >> 16
        .xdef   _fixed_mul
_fixed_mul:
        muls.l  d1,d0               ; d0 = a * b (32×32 → 32)
        asr.l   #16,d0              ; shift right 16 bits
        rts

; Fixed-point divide: (a << 16) / b
        .xdef   _fixed_div
_fixed_div:
        asl.l   #16,d0              ; shift a left 16 bits
        divs.l  d1,d0               ; d0 = d0 / d1
        rts

; Fixed-point square root (Newton-Raphson)
        .xdef   _fixed_sqrt
_fixed_sqrt:
        move.l  d2,-(sp)            ; Save d2
        move.l  d0,d1               ; guess = input
        moveq   #8,d2               ; 8 iterations
.loop:
        move.l  d0,d1               ; temp = input
        asl.l   #16,d1              ; temp << 16
        divs.l  d1,d1               ; temp / guess
        add.l   d1,d1               ; guess = (guess + temp) / 2
        lsr.l   #1,d1
        subq.l  #1,d2
        bne.s   .loop
        move.l  d1,d0               ; return guess
        move.l  (sp)+,d2            ; Restore d2
        rts
```

**src/game.novus:**
```novus
// Fixed-point math library (16.16 format)
extern fn fixed_mul(a: i32, b: i32) -> i32
extern fn fixed_div(a: i32, b: i32) -> i32
extern fn fixed_sqrt(a: i32) -> i32

// Constants (16.16 fixed-point)
const FIXED_ONE: i32 = 65536        // 1.0
const FIXED_HALF: i32 = 32768       // 0.5
const FIXED_PI: i32 = 205887        // 3.14159...

struct Vector2D {
    x: i32,  // 16.16 fixed-point
    y: i32,
}

impl Vector2D {
    fn length(&self) -> i32 {
        // sqrt(x*x + y*y)
        let x_squared = fixed_mul(self.x, self.x)
        let y_squared = fixed_mul(self.y, self.y)
        return fixed_sqrt(x_squared + y_squared)
    }

    fn normalize(&self) -> Vector2D {
        let len = self.length()
        return Vector2D {
            x: fixed_div(self.x, len),
            y: fixed_div(self.y, len),
        }
    }
}

fn main() -> i32 {
    let v = Vector2D { x: FIXED_ONE * 3, y: FIXED_ONE * 4 }
    let length = v.length()  // Should be ~5.0 (327680)

    let normalized = v.normalize()
    // normalized.x should be 0.6, normalized.y should be 0.8

    return 0
}
```

**novus.toml:**
```toml
[package]
name = "fixedpoint_demo"
version = "1.0.0"

[build]
asm_files = ["src/fixedpoint.s"]
cpu = "68020"  # Required for muls.l/divs.l
```

### Example 2: Fast Memory Operations

**src/memory.s:**
```asm
; Fast memory operations
        .section .text

; Fast memset: memset_fast(ptr: *u8, value: u8, count: u32)
        .xdef   _memset_fast
_memset_fast:
        move.l  a0,a1               ; ptr in a1
        move.b  d1,d0               ; value in d0
        move.l  a1,d1               ; count in d1

        ; Check for small sizes
        cmp.l   #64,d1
        blt.s   .byte_loop

        ; Expand byte to longword
        move.b  d0,d2
        lsl.w   #8,d2
        or.b    d0,d2
        move.w  d2,d0
        swap    d0
        move.w  d2,d0               ; d0 now has 4 copies of byte

        ; Align to longword boundary
        move.l  a1,d2
        btst    #0,d2
        beq.s   .aligned
        move.b  d0,(a1)+
        subq.l  #1,d1
.aligned:

        ; Fill longwords
        move.l  d1,d2
        lsr.l   #2,d2               ; count / 4
        subq.l  #1,d2
.long_loop:
        move.l  d0,(a1)+
        dbf     d2,.long_loop

        ; Fill remaining bytes
        and.l   #3,d1
.byte_loop:
        subq.l  #1,d1
        bmi.s   .done
        move.b  d0,(a1)+
        bra.s   .byte_loop
.done:
        rts

; Fast memcpy: memcpy_fast(dst: *u8, src: *u8, count: u32)
        .xdef   _memcpy_fast
_memcpy_fast:
        movem.l d2-d7/a2-a3,-(sp)   ; Save registers
        move.l  a0,a2               ; dst in a2
        move.l  a1,a3               ; src in a3
        move.l  d0,d7               ; count in d7

        ; Use movem for large copies
        cmp.l   #128,d7
        blt.s   .small_copy

        ; Copy 48 bytes at a time
        move.l  d7,d0
        lsr.l   #4,d0               ; count / 16
        subq.l  #1,d0
.movem_loop:
        movem.l (a3)+,d1-d6/a0-a1   ; Read 32 bytes
        movem.l d1-d6/a0-a1,(a2)    ; Write 32 bytes
        adda.l  #32,a2
        movem.l (a3)+,d1-d4         ; Read 16 more bytes
        movem.l d1-d4,(a2)          ; Write 16 bytes
        adda.l  #16,a2
        dbf     d0,.movem_loop

        ; Copy remaining bytes
        and.l   #15,d7
.small_copy:
        subq.l  #1,d7
        bmi.s   .copy_done
.byte_loop:
        move.b  (a3)+,(a2)+
        dbf     d7,.byte_loop
.copy_done:
        movem.l (sp)+,d2-d7/a2-a3   ; Restore registers
        rts
```

**src/main.novus:**
```novus
extern fn memset_fast(ptr: *u8, value: u8, count: u32)
extern fn memcpy_fast(dst: *u8, src: *u8, count: u32)

fn clear_screen(screen: *u8, size: u32) {
    unsafe {
        memset_fast(screen, 0, size)
    }
}

fn blit_sprite(dst: *u8, src: *u8, width: u32, height: u32) {
    unsafe {
        memcpy_fast(dst, src, width * height)
    }
}

fn main() -> i32 {
    let screen_size: u32 = 320 * 256
    let buffer: [u8; 320 * 256]

    clear_screen((*u8)&buffer[0], screen_size)

    return 0
}
```

---

## Best Practices

### Memory Safety

**Problem:** Assembly bypasses Novus safety checks

**Solution:** Always use `unsafe` and validate manually

```novus
extern fn process_buffer(ptr: *u8, len: u32) -> i32

fn safe_process(buf: []u8) -> Result<i32, Error> {
    if buf.len() == 0 {
        return Result::Err(Error::InvalidInput)
    }

    unsafe {
        let result = process_buffer(buf.as_ptr(), buf.len() as u32)
        if result < 0 {
            return Result::Err(Error::ProcessFailed)
        }
        return Result::Ok(result)
    }
}
```

### Register Preservation

**Problem:** Forgetting to save/restore callee-saved registers

**Solution:** Always use `movem.l` to save/restore

```asm
my_function:
        movem.l d2-d7/a2-a6,-(sp)   ; Save ALL callee-saved regs

        ; ... your code using d2-d7, a2-a6 ...

        movem.l (sp)+,d2-d7/a2-a6   ; Restore before return
        rts
```

**Optimization:** Only save registers you actually use

```asm
my_function:
        movem.l d2-d3/a2,-(sp)      ; Only save d2-d3 and a2

        ; ... your code using only d2, d3, a2 ...

        movem.l (sp)+,d2-d3/a2      ; Restore only what you saved
        rts
```

### Stack Alignment

**Problem:** Misaligned stack violates the Amiga 68k ABI

**Solution:** Keep stack word-aligned

```asm
        ; WRONG - pushes byte (misaligns stack)
        move.b  d0,-(sp)

        ; RIGHT - push word
        move.w  d0,-(sp)

        ; WRONG - odd number of bytes
        subq.l  #3,sp

        ; RIGHT - even number
        subq.l  #4,sp
```

### CPU Profile Awareness

**Problem:** Using instructions newer than the configured 68020+ target

**Solution:** Mark assembly files with minimum CPU

```toml
[[build.asm]]
file = "src/fast_mul.s"
cpu = "68020"              # Uses muls.l instruction
```

Or use conditional assembly:

```asm
        .ifdef  CPU_68020
        muls.l  d1,d0           ; Fast on 020+
        .else
        muls.l  d1,d0           ; Native on the Novus 68020 baseline
        .endif
```

### Error Handling

**Problem:** Assembly can't return `Result<T, E>`

**Solution:** Use error codes and wrap in Novus

```asm
; Returns 0 on success, negative error code on failure
_asm_operation:
        ; ... do work ...
        tst.l   d0
        bne.s   .error
        moveq   #0,d0           ; Success
        rts
.error:
        moveq   #-1,d0          ; Error
        rts
```

```novus
extern fn asm_operation() -> i32

fn safe_operation() -> Result<(), Error> {
    let result = asm_operation()
    if result < 0 {
        return Result::Err(Error::OperationFailed)
    }
    return Result::Ok(())
}
```

---

## Debugging

### Symbol Visibility

View all symbols (Novus + assembly):

```bash
novusc inspect my_game --symbols
```

Output:
```
SYMBOLS:
  0x00001000  T  _main
  0x00001050  T  _fixed_mul
  0x00001060  T  _fixed_div
  0x00001100  T  _plot_pixel
  0x00001200  D  _global_data
```

### Disassembly

View final linked assembly:

```bash
m68k-amigaos-objdump -d my_game
```

Useful for:
- Verifying calling convention
- Checking register usage
- Finding optimization opportunities
- Understanding code layout

### Link Map

Generate link map with addresses and sections:

```bash
novusc build --emit-map
```

Creates `my_game.map`:
```
SECTION .text (0x00001000 - 0x00002FFF)
  _main                 0x00001000
  _fixed_mul            0x00001050
  _fixed_div            0x00001060

SECTION .data (0x00010000 - 0x00010FFF)
  _global_buffer        0x00010000
```

### Common Issues

**Issue:** Undefined symbol `_my_function`
```
Error: Undefined reference to `_my_function`
```
**Solution:**
- Check function name matches (Novus `my_function` → asm `_my_function`)
- Verify `.xdef _my_function` in assembly file
- Check assembly file is listed in `novus.toml`

**Issue:** Wrong calling convention
```
Program crashes or returns garbage
```
**Solution:**
- Check parameter order matches ABI (d0, d1, a0, a1, stack)
- Verify register preservation (save/restore d2-d7, a2-a6)
- Ensure return value in d0

**Issue:** Stack corruption
```
Random crashes, especially after function returns
```
**Solution:**
- Verify stack alignment (word-aligned)
- Check `movem.l` save/restore pairs match
- Ensure `link`/`unlk` pairs match if used
- Don't modify caller's stack frame

---

## Common Patterns

### Pattern 1: Passing Slices to Assembly

**Problem:** Novus slices are fat pointers (ptr + len)

**Solution:** Pass pointer and length separately

```novus
extern fn process_buffer(ptr: *u8, len: u32) -> i32

fn process(buf: []u8) -> i32 {
    unsafe {
        return process_buffer(buf.as_ptr(), buf.len() as u32)
    }
}
```

```asm
_process_buffer:
        ; ptr in a0, len in d1
        move.l  a0,a1           ; Use a1 for iteration
        subq.l  #1,d1           ; len-1 for dbf
.loop:
        ; Process byte at (a1)
        addq.l  #1,a1           ; Next byte
        dbf     d1,.loop
        rts
```

### Pattern 2: Returning Structures

**Problem:** Structs don't fit in d0

**Solution:** Caller passes pointer in a0

```novus
struct Point { x: i16, y: i16 }

extern fn make_point(x: i16, y: i16) -> Point

fn example() -> Point {
    return make_point(100, 200)
}
```

Compiler transforms to:

```asm
; Caller (Novus-generated):
        sub.l   #4,sp           ; Allocate space for Point
        move.l  sp,a0           ; Pass pointer in a0
        move.w  #100,d0         ; x arg
        move.w  #200,d1         ; y arg
        jsr     _make_point
        ; Result now at (sp)

_make_point:
        ; a0 = pointer to result struct
        move.w  d0,(a0)         ; Store x
        move.w  d1,2(a0)        ; Store y
        rts
```

### Pattern 3: Loop Optimization

**Problem:** Novus loops might not be as fast as assembly

**Solution:** Write tight assembly loops

```asm
; Sum array of u32: sum_array(arr: *u32, len: u32) -> u32
_sum_array:
        moveq   #0,d0           ; sum = 0
        move.l  a1,d1           ; count
        subq.l  #1,d1           ; len-1 for dbf
        bmi.s   .done           ; Empty array
.loop:
        add.l   (a0)+,d0        ; sum += *arr++
        dbf     d1,.loop
.done:
        rts
```

**Novus usage:**
```novus
extern fn sum_array(arr: *u32, len: u32) -> u32

fn calculate_total(values: []u32) -> u32 {
    unsafe {
        return sum_array(values.as_ptr(), values.len() as u32)
    }
}
```

### Pattern 4: Hardware Register Access

**Problem:** Need to manipulate custom chips

**Solution:** Assembly for timing-critical access

```asm
; Wait for vertical blank
_wait_vblank:
.wait:
        move.w  $dff004,d0      ; Read VPOSR
        and.w   #$1ff,d0        ; Mask to get vertical position
        cmp.w   #300,d0         ; Wait for line 300
        bne.s   .wait
        rts

; Set color register
; set_color(reg: u16, color: u16)
_set_color:
        ; reg in d0, color in d1
        and.w   #31,d0          ; Clamp to COLOR00-COLOR31
        lsl.w   #1,d0           ; reg * 2 (word offset)
        lea     $dff180,a0      ; COLOR00 base
        move.w  d1,0(a0,d0.w)   ; Write to COLOR[reg]
        rts
```

**Novus usage:**
```novus
extern fn wait_vblank()
extern fn set_color(reg: u16, color: u16)

fn setup_palette() {
    // Set up color palette
    set_color(0, 0x000)  // Black
    set_color(1, 0xFFF)  // White
    set_color(2, 0xF00)  // Red

    wait_vblank()        // Wait for vblank before updating
}
```

---

## Next Steps

1. **Read the ABI Reference:** `docs/AmigaOS_ABI_Reference.md`
2. **Explore Templates:** `templates/assembly/` directory
3. **Study Examples:** `examples/asm_interop/` directory
4. **Profile First:** Use Novus until assembly is proven necessary
5. **Start Small:** Begin with simple functions, add complexity gradually

---

## Additional Resources

- **Language Design Doc:** `docs/LanguageDesignDoc.md` (§28: Assembly Integration)
- **ABI Reference:** `docs/AmigaOS_ABI_Reference.md`
- **VBCC Documentation:** http://www.compilers.de/vbcc.html
- **68k Assembly Tutorial:** https://wiki.amigaos.net/wiki/68k_Assembly
- **Amiga Hardware Reference:** http://amigadev.elowar.com/

---

**Remember:** Assembly is a sharp tool. Use it wisely, and only when profiling proves it's needed. Most Novus code should stay in safe, readable Novus!
