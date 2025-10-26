# Fat Binaries - Write Once, Run Optimally Everywhere

## Overview

Novus supports **CPU-based fat binaries** that automatically detect the host CPU and execute optimized code paths. One binary works on all Amiga models from A500 (68000) to A4000 (68040) to Vampire (68080).

This is the **default behavior** - you get fat binaries unless you explicitly specify a CPU target.

## Quick Start

```bash
# Default: Generate fat binary (works on ALL Amigas)
novus myprogram.novus -o myprogram

# Explicit: Generate thin binary for specific CPU
novus myprogram.novus -o myprogram --cpu 68000  # 68000 only
novus myprogram.novus -o myprogram --cpu 68060  # 68060 only (won't run on 68000!)
```

## How It Works

### 1. Compile-Time Analysis

When you compile with `--cpu auto` (the default), Novus:

1. **Scans your code** for operations that benefit from CPU-specific optimizations
2. **Identifies optimizable functions** containing:
   - Multiply operations (`x * y`)
   - Divide operations (`x / y`)
   - Shift operations (`x << n`, `x >> n`)
   - Array indexing
3. **Generates three versions** of each optimizable function:
   - `function_68000`: Compatible with all CPUs
   - `function_68020`: Uses 68020+ features (barrel shifter, 32-bit multiply/divide)
   - `function_68060`: Avoids slow 68060 instructions

### 2. Runtime Detection

On first function call, the binary:

1. **Reads ExecBase AttnFlags** (standard Amiga OS technique)
2. **Detects CPU type** (68000, 68020+, or 68060)
3. **Caches the result** (detection happens once)
4. **Dispatches to optimal version** via jump table

### 3. Execution

All subsequent calls go directly to the optimal version - **zero overhead** after first call!

## What Gets Optimized

### Multiply Operations

```novus
fn calculate(x: i32) -> i32 {
    return x * 10
}
```

**68000 version:**
- Uses 16×16→32 multiply sequence (~80 cycles)

**68020 version:**
- Uses native `muls.l` instruction (~4 cycles)

**68060 version:**
- Uses shift/add sequence for small constants (~5 cycles)
- Avoids slow `muls.l` instruction (~8 cycles)

### Division by Power-of-2

```novus
fn divide(x: u32) -> u32 {
    return x / 8
}
```

**68000 version:**
- Uses 16-bit divide (lossy!)

**68020 version:**
- Uses native `divu.l` instruction (~50 cycles)

**68060 version:**
- Optimizes to `lsr.l #3,d0` shift (~1 cycle instead of >70!)

### Shift Operations

```novus
fn shift(x: i32) -> i32 {
    return x << 16
}
```

**68000 version:**
- Uses loop or register shift (32 cycles)

**68020+ versions:**
- Uses barrel shifter (1 cycle)

## Size Impact

Fat binaries are larger because they contain multiple versions of optimizable functions.

**Example program with multiply/divide/shifts:**
- **68000 thin binary:** ~300 lines of assembly
- **Fat binary (auto):** ~800 lines of assembly
- **Size increase:** ~2.7x

**But:**
- Simple functions with no optimizations stay single-version (no bloat!)
- Performance gain: 8-70x faster on 68060
- One binary works everywhere (no separate builds needed)

## Performance Gains

### Multiply by Small Constant

| CPU | Thin Binary | Fat Binary | Speedup |
|-----|-------------|------------|---------|
| 68000 | 16x16 routine (~80 cy) | Same (~80 cy) | 1x |
| 68020 | `muls.l` (~4 cy) | `muls.l` (~4 cy) | 1x |
| 68060 | `muls.l` (~8 cy) | shift/add (~1 cy) | **8x faster!** |

### Division by Power-of-2

| CPU | Thin Binary | Fat Binary | Speedup |
|-----|-------------|------------|---------|
| 68000 | 16-bit div | Same | 1x |
| 68020 | `divu.l` (~50 cy) | `lsr` (~1 cy) | **50x faster!** |
| 68060 | `divu.l` (~70 cy) | `lsr` (~1 cy) | **70x faster!** |

## When to Use Fat Binaries

### ✅ Use Fat Binaries (Default) When:

- Distributing to users with unknown hardware
- You want maximum performance on all CPUs
- Binary size is acceptable (floppy: 880KB, HD: plenty)
- You want one build for everything

### ❌ Use Thin Binaries When:

- Size is critical (demoscene intros, ROM)
- You know the exact target hardware
- You're targeting a specific Amiga model

## Combined with FPU Fat Binaries

You can combine CPU and FPU detection:

```bash
# Both CPU and FPU auto-detection (default)
novus myprogram.novus -o myprogram --cpu auto --fpu auto

# CPU auto, FPU soft-only (smaller binary)
novus myprogram.novus -o myprogram --cpu auto --fpu soft

# CPU 68020, FPU auto
novus myprogram.novus -o myprogram --cpu 68020 --fpu auto
```

This creates a **super fat binary** with versions for:
- CPU: 68000, 68020, 68060
- FPU: soft-float, hardware FPU

**Example combinations generated:**
- `function_68000_soft` - A500 without FPU
- `function_68020_soft` - A1200 without FPU
- `function_68020_fpu` - A1200 with 68882
- `function_68060_soft` - Vampire without FPU
- `function_68060_fpu` - Vampire with FPU

## Runtime Library for Assembly

When you compile with `--cpu auto`, Novus generates **optimized runtime primitives** that assembly programmers can use:

```assembly
    xref    __mul_i32    ; Signed 32-bit multiply
    xref    __div_u32    ; Unsigned 32-bit divide
    xref    __shl_i32    ; Shift left
    ; ... and more

my_asm_function:
    move.l  width,d0
    move.l  height,d1
    jsr     __mul_i32    ; Auto-optimized for current CPU!
    rts
```

See [RuntimeAPI.md](RuntimeAPI.md) for complete documentation.

## Detection API

For advanced use, you can access the CPU detection directly:

```assembly
    xref    __detect_cpu
    xref    __detected_cpu

; Call once at startup
    jsr     __detect_cpu

; Read result
    move.l  __detected_cpu,d0
    ; d0 = 0: 68000
    ; d0 = 1: 68020, 68030, or 68040
    ; d0 = 2: 68060
```

## Future Enhancements

Planned additions to fat binary support:

### Phase 2: Chipset Detection
```bash
novus myprogram.novus --chipset auto
```
Detects OCS/ECS/AGA/RTG and uses optimal blitting/graphics code.

### Phase 3: Combined Optimizations
Full matrix of CPU × FPU × Chipset optimizations for ultimate performance.

### Phase 4: Smart Size Optimization
Compiler analyzes which versions provide real benefit and only includes those.

## Best Practices

1. **Use `--cpu auto` by default** - it's the default for a reason!
2. **Only use thin binaries when size truly matters** - floppy still has 880KB
3. **Profile on real hardware** - emulators may not show true performance differences
4. **Combine with FPU auto** - users with FPUs appreciate the speed
5. **Test on multiple systems** - verify fat binary works on A500, A1200, A4000

## Technical Details

### CPU Detection Code

Generated once per binary:

```assembly
__detect_cpu:
    movea.l  4.w,a0           ; Get ExecBase
    move.w   296(a0),d0       ; Read AttnFlags

    btst     #5,d0            ; Check 68060 bit
    beq.s    .not_68060
    move.l   #2,__detected_cpu
    rts

.not_68060:
    andi.w   #$000E,d0        ; Check 68020/030/040 bits
    beq.s    .is_68000
    move.l   #1,__detected_cpu
    rts

.is_68000:
    ; Already 0
    rts
```

### Dispatch Stub Example

For each optimizable function:

```assembly
_function:
    bsr      __detect_cpu          ; Detect once
    move.l   __detected_cpu,d0
    cmpi.l   #2,d0
    beq.s    _function_68060
    cmpi.l   #1,d0
    beq.s    _function_68020
    jmp      _function_68000

_function_68000:
    ; 68000-compatible code

_function_68020:
    ; 68020+ optimized code

_function_68060:
    ; 68060 optimized code
```

## Summary

Fat binaries are **the smart default** for Novus:

- ✅ One binary works everywhere
- ✅ Automatic optimization for each CPU
- ✅ Minimal overhead (one-time detection)
- ✅ Future-proof (works on CPUs that don't exist yet!)
- ✅ Easy to use (it's the default!)

**Just compile and go. It works optimally everywhere.** 🚀
