# Novus Inline Assembly Specification

**Version:** 1.0 (Draft)
**Status:** Design Phase
**Last Updated:** 2025-12-06

## Overview

Novus provides **inline assembly** as a first-class language feature for performance-critical code, hardware access, and bootstrapping the runtime. Unlike external assembly files, inline assembly allows 68k instructions to be embedded directly within Novus functions with type-safe parameter binding and explicit register control.

**Key Design Principle:** Inline assembly is not merely an escape hatch—it's a carefully designed feature that maintains type safety at boundaries while granting full control over register allocation, instruction ordering, and hardware access.

**Primary Use Cases:**
- Bootstrapping the Novus runtime (self-hosting)
- Direct hardware register manipulation
- Performance-critical inner loops (after profiling)
- CPU-specific optimizations
- AmigaOS library vector implementations

**Safety Model:** All inline assembly must be inside `unsafe` blocks, signaling that the compiler cannot verify memory safety or type correctness within the assembly block.

---

## Table of Contents

1. [Syntax Overview](#syntax-overview)
2. [Grammar Specification](#grammar-specification)
3. [Register Constraints](#register-constraints)
4. [Parameter Binding](#parameter-binding)
5. [Return Values](#return-values)
6. [Clobber Declarations](#clobber-declarations)
7. [Volatile Semantics](#volatile-semantics)
8. [CPU Profile Validation](#cpu-profile-validation)
9. [Type Checking](#type-checking)
10. [Examples](#examples)
11. [Comparison to SAS/C and VBCC](#comparison-to-sasc-and-vbcc)
12. [Future Extensions](#future-extensions)
13. [Implementation Notes](#implementation-notes)

---

## Syntax Overview

Inline assembly uses the `asm` keyword within `unsafe` blocks:

```novus
unsafe asm(inputs) -> return_type {
    "assembly instructions"
}
```

### Minimal Example

```novus
fn swap_bytes(value: u32) -> u32 {
    unsafe asm(value) -> u32 {
        "ror.w #8,d0"
        "swap d0"
        "ror.w #8,d0"
    }
}
```

### Full Form

```novus
unsafe asm(param1 in d0, param2 in d1) -> u32 in d0
    volatile
    clobbers(d2, a0, memory)
{
    "move.l %param1,d2"
    "add.l %param2,d2"
    "move.l d2,%result"
}
```

---

## Grammar Specification

### EBNF Grammar

```ebnf
inline_asm ::= 'unsafe' 'asm' '(' input_list? ')' return_spec?
               volatility? clobber_spec? asm_block

input_list ::= input (',' input)*

input ::= identifier register_binding?

register_binding ::= 'in' register_name

return_spec ::= '->' type_spec register_binding?
              | '->' '(' multi_return_list ')'

multi_return_list ::= type_in_reg (',' type_in_reg)*

type_in_reg ::= type_spec 'in' register_name

volatility ::= 'volatile'

clobber_spec ::= 'clobbers' '(' clobber_list ')'

clobber_list ::= clobber_item (',' clobber_item)*

clobber_item ::= register_name | 'memory'

asm_block ::= '{' instruction_list '}'

instruction_list ::= string_literal+

register_name ::= 'd0' | 'd1' | 'd2' | 'd3' | 'd4' | 'd5' | 'd6' | 'd7'
                | 'a0' | 'a1' | 'a2' | 'a3' | 'a4' | 'a5' | 'a6' | 'a7'
```

### Substitution Syntax

Within assembly instruction strings:
- `%param_name` → substitutes with the register containing that parameter
- `%result` → substitutes with the register holding the return value

---

## Register Constraints

### Amiga ABI Register Convention

Novus follows the standard Amiga ABI (same as C/VBCC):

| Register(s) | Purpose | Preserved Across Calls |
|-------------|---------|------------------------|
| d0, d1 | Arguments / Return values | No (caller-saved) |
| d2-d7 | General purpose | Yes (callee-saved) |
| a0, a1 | Arguments (pointers) | No (caller-saved) |
| a2-a6 | General purpose | Yes (callee-saved) |
| a7 (sp) | Stack pointer | Yes (must maintain) |

### Register Allocation Rules

**1. Inferred Register Allocation**

If no explicit register constraints are given, the compiler infers register allocation based on the Amiga ABI:

```novus
// Compiler infers: value in d0, return in d0
fn swap_bytes(value: u32) -> u32 {
    unsafe asm(value) -> u32 {
        "ror.w #8,d0"
        "swap d0"
        "ror.w #8,d0"
    }
}
```

Inference rules:
- First data parameter → `d0`
- Second data parameter → `d1`
- First pointer parameter → `a0`
- Second pointer parameter → `a1`
- Additional parameters → error (require explicit binding)
- Return value → `d0` (or `d0:d1` for 64-bit)

**2. Explicit Register Binding**

Developers can specify exact register allocation:

```novus
fn add_explicit(a: u32 in d2, b: u32 in d3) -> u32 in d0 {
    unsafe asm(a, b) -> u32 in d0 {
        "move.l %a,d0"
        "add.l %b,d0"
    }
}
```

**3. Mixed Parameter Types**

Pointers go to address registers, data to data registers:

```novus
fn memset(ptr: *u8 in a0, value: u8 in d0, count: u32 in d1) {
    unsafe asm(ptr, value, count) -> void
        clobbers(a1)
    {
        "move.l %ptr,a1"
        "subq.l #1,%count"
        ".loop:"
        "move.b %value,(a1)+"
        "dbf %count,.loop"
    }
}
```

---

## Parameter Binding

### Named Parameter Substitution

Parameters are referenced in assembly using `%parameter_name`:

```novus
fn scale(value: u32 in d0, factor: u32 in d1) -> u32 in d0 {
    unsafe asm(value, factor) -> u32 in d0 {
        "muls.l %factor,%value"  // Expands to: muls.l d1,d0
    }
}
```

The compiler performs substitution at compile time:
- `%value` → `d0`
- `%factor` → `d1`
- `%result` → `d0` (implicit for return value)

### Symbol References

Use `param = &global_var` to pass addresses:

```novus
static mut copper_list: *u16 = null

fn load_copper() {
    unsafe asm(ptr = &copper_list) -> void
        volatile
        clobbers(a0, memory)
    {
        "move.l %ptr,a0"
        "move.l (a0),0xDFF080"  // COP1LC
    }
}
```

This generates:
```asm
    lea     _copper_list,a0      ; %ptr expands to a0
    move.l  (a0),0xDFF080
```

---

## Return Values

### Single Return Value

Most functions return a single value in `d0`:

```novus
fn get_execbase() -> *Library in a0 {
    unsafe asm() -> *Library in a0 {
        "move.l 4.w,a0"  // Read ExecBase from absolute address 4
    }
}
```

**Register Selection:**
- Pointers → address register (`a0`-`a6`)
- Integers/data → data register (`d0`-`d7`)
- By convention, use `d0` for data, `a0` for pointers

### Multi-Register Returns

For functions that return multiple values (e.g., reading hardware registers):

```novus
fn read_eclock() -> (u32 in d0, u32 in d1) {
    unsafe asm() -> (u32 in d0, u32 in d1)
        volatile
    {
        "move.l 0xDFF024,d0"  // VPOSR
        "move.l 0xDFF026,d1"  // VHPOSR
    }
}

fn example() {
    let (lo, hi) = read_eclock()
    // lo contains d0 value, hi contains d1 value
}
```

**Allowed Return Register Combinations:**
- `(u32 in d0, u32 in d1)` — two data registers
- `(u32 in d0, *T in a0)` — mixed data and pointer
- `(*T in a0, *U in a1)` — two pointers

**Restrictions:**
- Maximum two return values
- Registers must be `d0`, `d1`, `a0`, or `a1` (caller-saved registers only)
- No `d2`-`d7` or `a2`-`a6` for returns (these are callee-saved)

### Void Return

Functions with no return value:

```novus
fn disable_interrupts() {
    unsafe asm() -> void volatile {
        "move.w #0x7FFF,0xDFF09A"  // INTENA
    }
}
```

---

## Clobber Declarations

### Purpose

Clobbers inform the compiler which registers are modified by the assembly block, allowing it to:
1. Save/restore callee-saved registers around the asm block
2. Avoid allocating clobbered registers for live values
3. Generate correct code for register allocation

### Syntax

```novus
clobbers(register_list)
```

Where `register_list` is a comma-separated list of:
- Register names: `d0`, `d1`, `a0`, etc.
- `memory` — special clobber indicating memory writes

### Example: Register Clobbers

```novus
fn complex_calc(a: u32, b: u32) -> u32 {
    unsafe asm(a in d0, b in d1) -> u32 in d0
        clobbers(d2, d3, a0)  // We use d2, d3, a0 internally
    {
        "move.l %a,d2"
        "muls.l %b,d2"
        "move.l d2,d3"
        "lea temp_buffer,a0"
        "move.l d3,(a0)"
        "move.l (a0),d0"
    }
}
```

**What the compiler does:**
1. Generates prologue to save `d2`, `d3`, `a0` (callee-saved registers)
2. Emits the inline assembly
3. Generates epilogue to restore saved registers

**Generated assembly:**
```asm
complex_calc:
    movem.l d2-d3/a0,-(sp)    ; Save clobbered callee-saved registers

    ; Inline assembly block
    move.l d0,d2
    muls.l d1,d2
    move.l d2,d3
    lea temp_buffer,a0
    move.l d3,(a0)
    move.l (a0),d0

    movem.l (sp)+,d2-d3/a0    ; Restore saved registers
    rts
```

### Memory Clobber

The `memory` clobber indicates that the assembly writes to memory locations that might be visible to other code:

```novus
fn write_hardware_reg(value: u16) {
    unsafe asm(value in d0) -> void
        volatile
        clobbers(memory)  // Writes to hardware register
    {
        "move.w %value,0xDFF180"  // COLOR00
    }
}
```

**Effect of `memory` clobber:**
- Forces compiler to flush cached values to memory before the asm block
- Prevents reordering of memory operations across the asm block
- Acts as a memory barrier

### Implicit Clobbers

Certain registers are **implicitly clobbered** and don't need to be declared:
- Input registers (already specified in parameter bindings)
- Output registers (specified in return value)
- `d0`, `d1`, `a0`, `a1` (caller-saved, always assumed clobbered)

**Example:**
```novus
// d0 and d1 are implicit clobbers (inputs/outputs)
fn add(a: u32, b: u32) -> u32 {
    unsafe asm(a, b) -> u32 {
        "add.l d1,d0"
    }
    // No need to write: clobbers(d0, d1)
}
```

### Rules for Clobber Lists

1. **Only list callee-saved registers** (`d2`-`d7`, `a2`-`a6`)
2. **Don't list caller-saved registers** (`d0`, `d1`, `a0`, `a1`) — always assumed clobbered
3. **Don't list input/output registers** — already tracked by compiler
4. **Always declare `memory` if writing to memory** outside local stack frame

---

## Volatile Semantics

### Purpose

The `volatile` keyword prevents the compiler from:
1. Reordering the assembly block relative to other operations
2. Eliminating the assembly block even if results appear unused
3. Optimizing away repeated asm blocks

### When to Use Volatile

**Always use `volatile` for:**
- Hardware register access (reading/writing custom chips)
- Memory-mapped I/O
- Interrupt handlers
- Synchronization primitives
- Side effects that aren't visible to the compiler

**Don't use `volatile` for:**
- Pure computation (mathematical operations)
- Functions with no side effects
- Operations where reordering is safe

### Example: Hardware Access

```novus
// Reading hardware register must be volatile
fn read_mouse_x() -> u16 {
    unsafe asm() -> u16 in d0 volatile {
        "move.w 0xDFF00A,d0"  // JOY0DAT
    }
}

// Writing hardware register must be volatile
fn set_color(reg: u16, value: u16) {
    unsafe asm(reg in d0, value in d1) -> void volatile {
        "lsl.w #1,%reg"
        "lea 0xDFF180,a0"
        "move.w %value,0(a0,%reg.w)"
    }
}
```

### Example: Memory Barrier

```novus
fn memory_barrier() {
    unsafe asm() -> void
        volatile
        clobbers(memory)
    {
        "nop"  // Empty barrier, but prevents reordering
    }
}
```

**Effect:**
- Compiler cannot move memory operations across this barrier
- Ensures ordering of reads/writes relative to barrier

### Volatile + Clobbers

Combine `volatile` with `clobbers(memory)` for hardware writes:

```novus
fn copper_write(addr: u32, value: u16) {
    unsafe asm(addr in a0, value in d0) -> void
        volatile
        clobbers(memory)
    {
        "move.w %value,(%addr)"
    }
}
```

---

## CPU Profile Validation

### Purpose

Novus validates that assembly instructions are compatible with the target CPU profile, preventing runtime crashes from illegal instructions.

### CPU Profiles

| Profile | Supported Instructions |
|---------|------------------------|
| `68020` | 32-bit operations, bitfields, PC-relative |
| `68030` | Same as 68020 (identical instruction set) |
| `68040` | Cache instructions, move16 |
| `68060` | Optimized ops (avoid unimplemented instructions) |
| `68080` | Apollo Core, AMMX extensions |

### Example: CPU-Constrained Code

```novus
// This function requires 68020+ for muls.l instruction
@cpu(min = M68020)
fn fixed_mul(a: i32, b: i32) -> i32 {
    unsafe asm(a, b) -> i32 {
        "muls.l d1,d0"  // 32x32→32 multiply (68020+)
        "asr.l #16,d0"
    }
}
```

**Compiler behavior:** the function is accepted on every supported target because
68020 is the language minimum.

### Instruction Set Validation

The compiler parses assembly instructions and checks against CPU profile:

```novus
// ERROR: bfextu requires 68020+
fn extract_bitfield(value: u32, offset: u32) -> u32 {
    unsafe asm(value, offset) -> u32 {
        "bfextu d0{0:d1},d0"  // ❌ Bitfield instruction (68020+)
    }
}
```

Instructions introduced after 68020 are rejected when the configured target is
too old for them. For example, `move16` requires a 68040 target.

---

## Type Checking

### Type Safety at Boundaries

While the compiler cannot verify correctness inside the asm block, it enforces type safety at the boundaries:

**1. Parameter Type Checking**

```novus
fn scale(value: u32, factor: u16) -> u32 {
    unsafe asm(value, factor) -> u32 {
        "muls.w d1,d0"  // Compiler trusts you used correct sizes
    }
}

// ❌ Type error: cannot pass f32 to asm expecting u32
let result = scale(3.14, 2)
```

**2. Return Type Checking**

```novus
// ✅ Correct: return type matches declaration
fn get_word() -> u16 {
    unsafe asm() -> u16 {
        "move.w #42,d0"
    }
}

// ❌ Caller expects u16, but may get garbage in upper 16 bits of d0
fn buggy() -> u16 {
    unsafe asm() -> u16 {
        "move.l #0x12345678,d0"  // Returns 32 bits, but declared as u16!
    }
}
```

**Compiler behavior:**
- For `u16` return, only lower 16 bits of `d0` are used
- Upper 16 bits are undefined (not cleared by compiler)
- Developer must ensure correct instruction sizes

**3. Pointer Type Checking**

```novus
fn write_ptr(ptr: *u32, value: u32) {
    unsafe asm(ptr, value) {
        "move.l %value,(%ptr)"
    }
}

// ✅ Correct
let x: u32 = 0
write_ptr(&x, 42)

// ❌ Type error: &u16 is not compatible with *u32
let y: u16 = 0
write_ptr(&y, 42)
```

### Size Annotations

For clarity, developers should match instruction sizes to types:

| Type | Size | Instruction Suffix |
|------|------|--------------------|
| `u8`, `i8` | 8-bit | `.b` (byte) |
| `u16`, `i16` | 16-bit | `.w` (word) |
| `u32`, `i32`, `*T` | 32-bit | `.l` (long) |

**Example:**
```novus
fn correct_sizes(byte: u8, word: u16, long: u32) {
    unsafe asm(byte, word, long) {
        "move.b d0,d2"   // u8 → byte instruction
        "move.w d1,d3"   // u16 → word instruction
        "move.l a0,a1"   // u32 → long instruction
    }
}
```

---

## Examples

### Example 1: Byte Swapping

```novus
/// Swap bytes in a 32-bit word (endianness conversion)
fn swap_u32(value: u32) -> u32 {
    unsafe asm(value) -> u32 {
        "ror.w #8,d0"   // Swap bytes in lower word
        "swap d0"       // Swap upper and lower words
        "ror.w #8,d0"   // Swap bytes in new lower word
    }
}

fn example() {
    let big_endian: u32 = 0x12345678
    let little_endian = swap_u32(big_endian)
    // little_endian = 0x78563412
}
```

### Example 2: Get ExecBase

```novus
use std::exec::Library

/// Read ExecBase pointer from absolute address 4
fn get_execbase() -> *Library in a0 {
    unsafe asm() -> *Library in a0 {
        "move.l 4.w,a0"  // Read absolute address 4 into a0
    }
}

fn example() {
    let exec = get_execbase()
    // exec now points to exec.library base
}
```

### Example 3: Disable Interrupts

```novus
/// Disable all interrupts and return previous state
fn disable_interrupts() -> u16 {
    unsafe asm() -> u16 in d0
        volatile
        clobbers(memory)
    {
        "move.w 0xDFF01C,d0"       // Read INTENAR
        "move.w #0x7FFF,0xDFF09A"  // Clear all INTENA bits
    }
}

/// Restore interrupt state
fn restore_interrupts(state: u16) {
    unsafe asm(state in d0) -> void
        volatile
        clobbers(memory)
    {
        "or.w #0x8000,%state"      // Set master enable bit
        "move.w %state,0xDFF09A"   // Write to INTENA
    }
}

fn critical_section() {
    let old_ints = disable_interrupts()
    defer restore_interrupts(old_ints)  // Restore on exit

    // Critical code here - interrupts disabled
}
```

### Example 4: Fast Memory Copy

```novus
/// Fast memory copy using movem (48 bytes at a time)
fn memcpy_fast(dst: *u8, src: *u8, count: u32) {
    unsafe asm(dst in a0, src in a1, count in d0) -> void
        clobbers(d1, d2, d3, d4, d5, d6, a2, a3, memory)
    {
        "lsr.l #4,%count"          // count / 16 (48-byte blocks)
        "subq.l #1,%count"
        ".loop:"
        "movem.l (%src)+,d1-d6/a2-a3"   // Read 32 bytes
        "movem.l d1-d6/a2-a3,(%dst)"    // Write 32 bytes
        "adda.l #32,%dst"
        "movem.l (%src)+,d1-d4"         // Read 16 more bytes
        "movem.l d1-d4,(%dst)"          // Write 16 bytes
        "adda.l #16,%dst"
        "dbf %count,.loop"
    }
}
```

### Example 5: Library Vector with Specific Registers

```novus
use std::exec::Library

/// Library Open vector (must follow Amiga ABI)
@libvec(offset = -6)
fn lib_open(name: *u8 in a1, version: u32 in d0, lib: *Library in a6)
    -> *Library in d0
{
    unsafe asm(name, version, lib) -> *Library in d0 {
        // Increment open count
        "addq.w #1,32(a6)"     // lib_OpenCnt at offset 32

        // Return library base
        "move.l a6,d0"
    }
}
```

### Example 6: Bitfield Extraction (68020+)

```novus
/// Extract bitfield from value (requires 68020+)
@cpu(min = M68020)
fn extract_bits(value: u32, offset: u32, width: u32) -> u32 {
    unsafe asm(value in d0, offset in d1, width in d2) -> u32 in d0 {
        // bfextu extracts unsigned bitfield
        // Format: bfextu source{offset:width},dest
        "bfextu %value{%offset:%width},d0"
    }
}

fn example() {
    // Extract bits 8-15 (byte 1) from 0x12345678
    let byte = extract_bits(0x12345678, 8, 8)
    // byte = 0x34
}
```

### Example 7: Copper List Installation

```novus
static mut copper_list: *u16 = null

/// Load copper list into COP1LC/COP2LC
fn install_copper() {
    unsafe asm(ptr = &copper_list) -> void
        volatile
        clobbers(a0, d0, memory)
    {
        "move.l %ptr,a0"           // Get address of copper_list variable
        "move.l (a0),d0"           // Load copper list pointer
        "move.l d0,0xDFF080"       // COP1LC (low 3 bytes)
        "move.l d0,0xDFF084"       // COP2LC (low 3 bytes)
    }
}
```

### Example 8: Multi-Register Return

```novus
/// Read CIA timer values
fn read_timers() -> (u16 in d0, u16 in d1) {
    unsafe asm() -> (u16 in d0, u16 in d1)
        volatile
        clobbers(a0)
    {
        "lea 0xBFE001,a0"      // CIA-A base
        "move.b 0x400(a0),d0"  // Timer A low
        "lsl.w #8,d0"
        "move.b 0x500(a0),d0"  // Timer A high
        "move.b 0x600(a0),d1"  // Timer B low
        "lsl.w #8,d1"
        "move.b 0x700(a0),d1"  // Timer B high
    }
}

fn example() {
    let (timer_a, timer_b) = read_timers()
    // Use timer values
}
```

---

## Comparison to SAS/C and VBCC

### SAS/C Inline Assembly

**SAS/C syntax:**
```c
int swap_bytes(int value) {
    __asm {
        ror.w #8,d0
        swap d0
        ror.w #8,d0
    }
}
```

**Characteristics:**
- No parameter binding (relies on calling convention)
- No register constraints
- No clobber declarations
- No type checking at boundaries

### VBCC Inline Assembly

**VBCC syntax:**
```c
int add(int a, int b) {
    return __asm("add.l %1,%0", "=d"(a), "d"(b));
}
```

**Characteristics:**
- GCC-style asm syntax
- Register constraint strings (`"=d"`, `"a"`)
- Complex constraint language
- Not well-documented for Amiga target

### Novus Advantages

| Feature | SAS/C | VBCC | Novus |
|---------|-------|------|-------|
| **Named parameters** | ❌ No | ⚠️ Positional (`%0`, `%1`) | ✅ Named (`%param`) |
| **Explicit registers** | ❌ No | ⚠️ Constraint strings | ✅ `in d0` syntax |
| **Multi-register return** | ❌ No | ❌ No | ✅ `(u32 in d0, u32 in d1)` |
| **Clobber declarations** | ❌ No | ✅ Yes | ✅ `clobbers(...)` |
| **Volatile semantics** | ❌ No | ✅ Yes | ✅ `volatile` keyword |
| **CPU validation** | ❌ No | ❌ No | ✅ Validates instructions |
| **Type checking** | ❌ No | ⚠️ Limited | ✅ At boundaries |
| **Safety model** | ❌ No | ❌ No | ✅ Requires `unsafe` |

**Novus Design Goals:**
1. **More explicit than SAS/C** — no hidden assumptions about registers
2. **More readable than VBCC** — named parameters instead of `%0`, `%1`
3. **Safer than both** — type checking, CPU validation, `unsafe` requirement
4. **First-class feature** — not an afterthought, designed for Amiga hardware access

---

## Future Extensions

The following features are **deferred to future versions** and documented here for completeness:

### 1. Macro Support

**Current:** Use external `.i` files for macros
**Future (v1.5+):** Inline macro definitions

```novus
// Future syntax (NOT IMPLEMENTED)
asm_macro! SAVE_REGS {
    "movem.l d2-d7/a2-a6,-(sp)"
}

fn example() {
    unsafe asm() {
        SAVE_REGS!
        "nop"
    }
}
```

### 2. Literal Data Embedding

**Current:** Use external data sections
**Future (v1.5+):** Embed data in asm blocks

```novus
// Future syntax (NOT IMPLEMENTED)
unsafe asm() {
    data {
        "dc.w 0x1234,0x5678"
        "dc.l table_data"
    }
    code {
        "lea table_data,a0"
        "move.w (a0),d0"
    }
}
```

### 3. Section Control

**Current:** All asm goes to `.text` section
**Future (v2.0+):** Explicit section placement

```novus
// Future syntax (NOT IMPLEMENTED)
@section(.text.fast)
unsafe asm() {
    "nop"
}
```

### 4. @no_frame Attribute

**Current:** Frame setup always generated for non-trivial functions
**Future (v1.5+):** Disable frame pointer for leaf functions

```novus
// Future syntax (NOT IMPLEMENTED)
@no_frame
fn leaf_function(x: u32) -> u32 {
    unsafe asm(x) -> u32 {
        "add.l #1,d0"
    }
}
```

---

## Implementation Notes

### Compiler Phases

**1. Parsing**
- Parse `asm` block as special syntax node
- Extract parameter bindings, return spec, clobbers, volatility
- Store assembly instructions as string literals

**2. Type Checking**
- Validate parameter types match declared types
- Validate return type matches function signature
- Check register constraints are valid (d0-d7, a0-a6)
- Verify clobber list doesn't include input/output registers

**3. CPU Validation**
- Parse assembly instructions (simple regex-based parser)
- Check each instruction against CPU profile instruction set
- Emit error if instruction requires higher CPU than target

**4. Register Allocation**
- Assign input parameters to specified or inferred registers
- Generate prologue to save clobbered callee-saved registers
- Generate epilogue to restore saved registers
- Handle `volatile` by emitting memory barriers

**5. Code Generation**
- Substitute `%param_name` with actual register names
- Emit assembly instructions verbatim (after substitution)
- Wrap with prologue/epilogue if needed

**Generated Code Example:**

**Novus source:**
```novus
fn calc(a: u32, b: u32) -> u32 {
    unsafe asm(a, b) -> u32 clobbers(d2, d3) {
        "move.l %a,d2"
        "add.l %b,d2"
        "move.l d2,d0"
    }
}
```

**Generated assembly:**
```asm
_calc:
    movem.l d2-d3,-(sp)     ; Prologue: save clobbered regs

    ; Inline assembly (with substitutions)
    move.l  d0,d2           ; %a → d0
    add.l   d1,d2           ; %b → d1
    move.l  d2,d0

    movem.l (sp)+,d2-d3     ; Epilogue: restore regs
    rts
```

### Instruction Validation

The compiler uses a simple instruction parser to validate CPU compatibility:

**Validation algorithm:**
1. Extract instruction mnemonic (first word)
2. Look up in CPU instruction table:
   ```csharp
   static readonly Dictionary<string, CpuProfile> Instructions = new() {
       ["move"] = CpuProfile.M68020,
       ["muls.l"] = CpuProfile.M68020,
       ["bfextu"] = CpuProfile.M68020,
       ["move16"] = CpuProfile.M68040,
       // ...
   };
   ```
3. Check if `target_cpu >= required_cpu`
4. Emit error if instruction not available

**Implementation notes:**
- Parser is intentionally simple (regex-based)
- Does not validate operand validity (vasm does that)
- Only checks CPU compatibility
- Future: integrate full 68k instruction database

### Safety Analysis

**What the compiler checks:**
- Parameter types at call site
- Return type matches function signature
- Register constraints are valid
- CPU profile compatibility
- Clobber list doesn't include I/O registers

**What the compiler CANNOT check:**
- Correctness of assembly instructions
- Register usage matches declarations
- Memory safety within asm block
- Correct instruction sizes (`.b`, `.w`, `.l`)

**Developer responsibility:**
- Ensure assembly is correct for given inputs/outputs
- Match instruction sizes to types
- Preserve stack alignment
- Don't corrupt caller's registers (use clobbers correctly)

### Testing Strategy

**Unit tests:**
- Parse all asm syntax variants
- Validate type checking
- Check CPU instruction validation
- Verify register allocation

**Integration tests:**
- Compile asm blocks and verify generated code
- Test parameter substitution
- Validate prologue/epilogue generation
- Check clobber handling

**Runtime tests:**
- Execute asm functions on actual Amiga/emulator
- Verify results match expected values
- Test with different CPU profiles

---

## Summary

Novus inline assembly is designed as a **first-class language feature** for direct hardware control while maintaining safety at boundaries:

**Key Features:**
- ✅ Named parameter binding with `%param` substitution
- ✅ Explicit register constraints (`in d0`)
- ✅ Multi-register returns (`(u32 in d0, u32 in d1)`)
- ✅ Clobber declarations for correct register allocation
- ✅ Volatile semantics for hardware access
- ✅ CPU profile validation
- ✅ Type checking at boundaries
- ✅ Requires `unsafe` blocks

**Philosophy:**
- Explicit over implicit
- Readable and maintainable
- Safe by default (unsafe required)
- Designed for Amiga hardware

**Use Cases:**
- Bootstrapping runtime (self-hosting goal)
- Direct hardware manipulation
- Performance-critical code (after profiling)
- AmigaOS library/device vectors

---

**Next Steps:**
1. Implement parser for `asm` blocks
2. Add CPU instruction database
3. Generate prologue/epilogue for clobbers
4. Integrate with register allocator
5. Add comprehensive tests

---

**References:**
- [Amiga ABI Reference](AmigaOS_ABI_Reference.md)
- [Language Design Document](LanguageDesignDoc.md)
- [Assembly Integration Guide](ASSEMBLY_INTEGRATION_GUIDE.md)
- [M68k Instruction Set](http://www.easy68k.com/paulrsm/doc/trick68k.htm)
