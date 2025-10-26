# Novus Optimized Runtime Library for Assembly Programmers

## Overview

When you compile Novus code with `--cpu auto` (the default), the compiler generates an **optimized runtime library** that assembly programmers can use for free!

These are **CPU-aware primitives** that automatically dispatch to the best implementation for the current CPU.

## Why Use These?

- ✅ **No manual CPU detection** - automatically uses the right version
- ✅ **Optimized for each CPU** - 68000, 68020+, and 68060 versions
- ✅ **Battle-tested** - same code used by Novus compiler
- ✅ **Zero overhead** - detection happens once, then direct jumps
- ✅ **Register-efficient** - preserves all registers except d0

## Available Functions

All functions follow this convention:
- **Input**: `d0` = first operand, `d1` = second operand (or shift count)
- **Output**: `d0` = result
- **Preserved**: All registers except `d0` (including `a0-a6`, `d1-d7`)

### Multiply

#### `__mul_i32` - Signed 32-bit Multiply
```assembly
; Multiply two signed 32-bit integers: d0 = d0 * d1
; Example: Calculate area = width * height
    move.l  width,d0
    move.l  height,d1
    jsr     __mul_i32       ; d0 = width * height
```

**CPU Optimizations:**
- **68000**: Uses 16x16→32 multiply sequence
- **68020**: Uses native `muls.l` instruction
- **68060**: Uses native `muls.l` (runtime values can't use shift/add)

#### `__mul_u32` - Unsigned 32-bit Multiply
```assembly
; Multiply two unsigned 32-bit integers: d0 = d0 * d1
    move.l  count,d0
    move.l  size,d1
    jsr     __mul_u32       ; d0 = count * size
```

**CPU Optimizations:**
- **68000**: Uses 16x16→32 multiply sequence
- **68020/68060**: Uses native `mulu.l` instruction

### Divide

#### `__div_u32` - Unsigned 32-bit Divide
```assembly
; Divide two unsigned 32-bit integers: d0 = d0 / d1
; Example: Calculate average = total / count
    move.l  total,d0
    move.l  count,d1
    jsr     __div_u32       ; d0 = total / count
```

**CPU Optimizations:**
- **68000**: Uses 16-bit divide (lossy for large values!)
- **68020**: Uses native `divu.l` instruction
- **68060**: Uses native `divu.l` (slow, but unavoidable)

**Note**: Signed division is complex and not yet provided. Use unsigned division or implement your own.

### Shifts

#### `__shl_i32` - Shift Left
```assembly
; Shift left: d0 = d0 << d1
; Example: Multiply by 16 (shift left by 4)
    move.l  value,d0
    moveq   #4,d1
    jsr     __shl_i32       ; d0 = value << 4
```

**CPU Optimizations:**
- **68000**: Uses immediate shift for ≤8 bits, register shift for >8
- **68020+**: Uses barrel shifter (fast for any count)
- **68060**: Uses barrel shifter (dual-issue friendly)

#### `__shr_i32` - Signed Shift Right (Arithmetic)
```assembly
; Arithmetic shift right (preserves sign): d0 = d0 >> d1
; Example: Divide signed number by 4 (shift right by 2)
    move.l  signed_value,d0
    moveq   #2,d1
    jsr     __shr_i32       ; d0 = signed_value >> 2 (sign-extended)
```

#### `__shr_u32` - Unsigned Shift Right (Logical)
```assembly
; Logical shift right (zero-fill): d0 = d0 >> d1
; Example: Extract upper bits
    move.l  value,d0
    moveq   #16,d1
    jsr     __shr_u32       ; d0 = value >> 16 (unsigned)
```

## Complete Example

```assembly
; Calculate: result = ((x * 10) + y) >> 2

    section text,code

    ; Import Novus runtime
    xref    __mul_i32
    xref    __shr_i32

    xdef    _calculate
_calculate:
    link    a6,#0

    ; Multiply x by 10
    move.l  8(a6),d0        ; x
    moveq   #10,d1
    jsr     __mul_i32       ; d0 = x * 10

    ; Add y
    add.l   12(a6),d0       ; d0 = (x * 10) + y

    ; Shift right by 2 (divide by 4)
    moveq   #2,d1
    jsr     __shr_i32       ; d0 = result >> 2

    unlk    a6
    rts
```

## Linking with Novus Code

When you link your assembly with Novus-compiled code:

```bash
# Compile your Novus program (generates runtime library)
novus myprogram.novus -o myprogram.s --emit-asm

# Assemble both your code and Novus code
vasmm68k_mot -Fhunk -o myasm.o myassembly.s
vasmm68k_mot -Fhunk -o novus.o myprogram.s

# Link together
vlink -bamigahunk -o final novus.o myasm.o -lvc
```

The Novus-compiled code provides the runtime library, and your assembly code can call it!

## Performance Notes

### 68000
- **Multiply**: ~60-100 cycles (16x16 sequence)
- **Divide**: Limited to 16-bit (not true 32-bit!)
- **Shifts**: 2 cycles per bit shifted

### 68020-68040
- **Multiply**: ~4 cycles (`muls.l`/`mulu.l`)
- **Divide**: ~40-60 cycles (`divs.l`/`divu.l`)
- **Shifts**: 1 cycle (barrel shifter)

### 68060
- **Multiply**: ~8 cycles (slow on 68060!)
- **Divide**: >70 cycles (very slow!)
- **Shifts**: 1 cycle (barrel shifter)

**Tip**: On 68060, if you're multiplying by a small constant known at assembly time, use shift/add manually instead of `__mul_i32` for better performance!

## CPU Detection API

If you need direct CPU detection (advanced use):

```assembly
    xref    __detect_cpu    ; Detection routine
    xref    __detected_cpu  ; Result variable

; Detect CPU (call once at startup)
    jsr     __detect_cpu

; Read result
    move.l  __detected_cpu,d0
    ; d0 = 0: 68000
    ; d0 = 1: 68020, 68030, or 68040
    ; d0 = 2: 68060
```

## Future Additions

Planned primitives for future releases:
- `__mod_u32` - Unsigned modulo
- `__div_i32` - Signed 32-bit divide
- `__mul64` - 64-bit multiply (returns d0:d1)
- Graphics blitting helpers
- Memory copy optimized for each CPU

## Questions?

The Novus runtime library is automatically generated and always available when compiling with `--cpu auto` (the default). It costs nothing to use and makes your assembly code faster and more portable!
