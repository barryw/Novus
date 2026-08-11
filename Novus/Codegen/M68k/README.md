# Novus M68k Direct Assembly Backend

This directory contains a prototype direct 68k assembly code generator that bypasses the C intermediate step.

## Overview

The M68k backend generates vasm-compatible Motorola 68020+ assembly directly from Novus IR, providing:
- Better optimization control
- Path toward self-hosting
- Elimination of C toolchain dependency (VBCC)
- Direct access to 68k instructions

## Architecture

### M68kRegister.cs
Defines 68k register enumerations and utilities:
- Data registers (D0-D7)
- Address registers (A0-A7)
- Register classification (Volatile, Preserved, Special)
- Amiga ABI register conventions

### RegisterAllocator.cs
Simple stack-based register allocation:
- All local variables allocated to stack
- D0-D1: Temporary/return values (volatile)
- D2-D7: Local variables (preserved)
- A0-A1: Temporary addresses (volatile)
- A2-A4: Local pointers (preserved)
- A5: Frame pointer
- A6: Library base (for system calls)
- A7: Stack pointer

### InstructionSelector.cs
Maps IR operations to 68k instructions:
- Arithmetic: ADD, SUB, MUL, DIV, MOD
- Logic: AND, OR, XOR
- Shifts: LSL, LSR, ASL, ASR
- Comparisons: CMP + Scc
- Branches: BRA, Bcc
- Memory: MOVE, LEA

### M68kCodeGenerator.cs
Main code generator:
- Generates complete assembly file with sections
- Function prologue/epilogue (LINK/UNLK)
- String literals in data section
- Static variables in BSS section
- Proper symbol export (XDEF)

## Usage

Compile with the M68k backend using the `--backend=m68k` flag:

```bash
novus compile myfile.novus --backend=m68k --cpu=68020
```

This will generate `.s` assembly files instead of `.c` files.

## Current Status

**Prototype Implementation** - Supports:
- ✅ Basic arithmetic operations (add, sub, mul, div, mod)
- ✅ Bitwise operations (and, or, xor, shl, shr)
- ✅ Integer comparisons (eq, ne, lt, le, gt, ge)
- ✅ Control flow (if/else, loops via labels)
- ✅ Simple function calls (stack-based parameters)
- ✅ Return statements
- ✅ String literals
- ✅ Local variables (stack-allocated)
- ✅ Function prologue/epilogue
- ✅ CPU target directives

**Not Yet Implemented:**
- ❌ Struct member access
- ❌ Array indexing
- ❌ Pointer dereferencing
- ❌ AmigaOS library calls
- ❌ Register allocation optimization
- ❌ Inline assembly
- ❌ Hardware register access
- ❌ Advanced optimizations (peephole, etc.)
- ❌ Debug symbol generation
- ❌ Full Amiga ABI (currently simplified)

## Example Output

For a simple function:
```novus
fn add(a: i32, b: i32) -> i32 {
    return a + b;
}
```

Generates:
```asm
; Function: add
; Returns: i32
; Visibility: Public

    XDEF      add

add:
    ; Function prologue
    link      a5,#-8

    ; Load parameters
    move.l    8(a5),d0        ; a
    move.l    12(a5),d1       ; b

    ; Add
    add.l     d1,d0

    ; Return
    bra       add_epilogue

add_epilogue:
    ; Function epilogue
    unlk      a5
    rts
```

## Future Work

1. **Register Allocation**: Implement proper liveness analysis and graph coloring
2. **Peephole Optimization**: Instruction pattern matching and replacement
3. **AmigaOS Integration**: Full library call support with A6-based calling
4. **Hardware Access**: Direct copper/blitter/custom chip register access
5. **Inline Assembly**: Support for `asm {}` blocks
6. **Debug Symbols**: Generate debug information for crashes/debugging
7. **Self-Hosting**: Bootstrap Novus compiler on AmigaOS

## Testing

See `Novus.Tests/M68kCodeGeneratorTests.cs` for unit tests covering:
- Simple functions
- Arithmetic operations
- Conditional branches
- String literals
- Multiple functions
- CPU directives

## Notes

This is a prototype focused on correctness and architecture over completeness. It demonstrates the feasibility of direct 68k code generation and provides a foundation for future enhancements toward self-hosting.
