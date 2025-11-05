# Novus Language Project

## ⚠️ CRITICAL DEVELOPMENT RULES

**READ THIS FIRST - These rules override everything else:**

- **Never use the java ANTLR CLI**: We have the ANTLR nuget package which will rebuild the grammar on build.

- **Testing Amiga executables**: Copy to `/Users/barry/Emulation/Amiga/A4000-DH0/Barry` (shared drive for A4000 with 68040)

- **Documentation**: You have extensive docs in the `docs/` folder. Read them when needed.

- **Language Design**: Follow `LanguageDesignDoc.md` for language design decisions

- **Ultimate Goal**: Make the compiler self-hosting. We should be able to build a Novus compiler that runs on AmigaOS so we can do development completely on an Amiga

- **Compiler First**: Always ensure the compiler matches what we're trying to do, even if it means fixing the compiler. **Never reduce the scope of what we're trying to accomplish.**

- **🚨 NEVER USE VASM/VLINK DIRECTLY**: Always build Amiga executables using the Novus compiler. Always. The compiler handles the complete pipeline: compile → assemble → link. If you manually invoke `vasmm68k_mot` or `vlink`, you're doing it wrong.

---

## Overview

**Novus** is a modern systems programming language for the Amiga 68k ecosystem. It combines modern language ergonomics with direct hardware access and the efficiency required for 68k systems.

**Tagline:** "New code for classic machines" — a rebirth of Amiga development with modern design principles.

**Status:** Early design phase

## Core Philosophy

- **Explicit over implicit** — no hidden allocations, no runtime surprises
- **Predictable performance** — deterministic execution, no GC
- **Readable syntax over cleverness** — code should be obvious
- **Respect the machine** — leverage Amiga architecture instead of hiding it
- **Amiga first** — not cross-platform; focused on authentic Amiga development

## Implementation

- **Language:** C# (.NET 8+)
- **Platform:** Cross-platform on macOS, Linux, Windows
- **Pipeline:** `novusc` compiler → IR → 68k Assembly → VBCC (`vasm`/`vlink`) → Amiga executable

### Compilation Flow

```
Source (.novus) → Lexer → Parser → AST → Type Checker → IR Builder → Optimizer →
68k Code Generator → Assembly (.s) → vasm → vlink → Executable/Library/Device
```

## Language Features (Planned)

### Core (v1.0)
- **`Result[T,E]` & `Option[T]`** — mandatory in all std APIs, no null/exception paradigm
- **`defer` blocks** — deterministic resource cleanup (RAII-style)
- **Pattern matching** — powerful switch for enums & structs
- **Slices & views** — bounds-checked in debug builds
- **`unsafe` blocks** — for direct hardware or FFI use
- **Modules & imports** — no include hell
- **Inline `asm {}`** — for low-level access
- **Fixed-point math** — `fixed16`, `fixed32` with intrinsics
- **Handles instead of raw pointers** — safe resource ownership model
- **`async/await`** — stackless coroutines based on Exec signals/message ports

### Amiga-Specific Features
- **Copper DSL** — declarative copper lists with compile-time validation
- **Blitter Jobs DSL** — safe, typed blitter operations
- **Hardware register access** — symbolic registers, volatile semantics
- **AmigaOS FFI** — library/device/handler/interrupt templates
- **Graphics assets DSL** — sprites, BOBs, bitmaps, fonts with compile-time packing
- **Paula audio API** — channel management with async support

### Library/Device Support
- Build shared libraries (`.library`), devices (`.device`), DOS handlers, resources
- `@resident`, `@autoinit`, `@libvec`, `@devicevec` attributes
- Auto-generated ROMTags and vector tables
- Result-based APIs wrapping AmigaOS (Exec, Intuition, Graphics, DOS)

## Target Profiles

### CPU Profiles
- **m68k-000** — 68000/010 (strict subset, software 32-bit mul/div)
- **m68k-020** — 68020/030 (recommended default: bitfields, 32×32 ops, PC-relative)
- **m68k-040** — 68040 (cache-aware, avoids trappy ops)
- **m68k-060** — 68060 (optimized, strict op selection)
- **apx-080** — 68080/Apollo (future: AMMX intrinsics)

### Chipset Profiles
- **OCS** — Original chipset (A1000/A500/A2000)
- **ECS** — Enhanced chipset (A500+/A600/A3000)
- **AGA** — Advanced Graphics Architecture (A1200/A4000)
- **auto** — runtime detection with widest common subset validation

### Fat Binaries
- `--cpu fat:000,020,060` — multi-version dispatch based on CPU detection
- `@multiversion(cpu=[...])` attribute for automatic specialization

## Memory & Resource Management

- **RAII handles** — `ScreenHandle`, `WindowHandle`, `BitmapHandle`, etc.
- **`using` syntax** — deterministic cleanup at scope exit
- **Allocators** — global (Fast/Chip), arena, pool, slab
- **Safe by default** — bounds checks in debug, `Result` everywhere
- **Unsafe power tools** — explicit `unsafe` APIs for raw control
- **No garbage collection** — all lifetimes explicit and predictable

## Toolchain

### Tools
- `novusc build` — compile-assemble-link pipeline
- `novusc fmt` — format code
- `novusc inspect` — inspect symbols, ROMTags, vectors
- `novusc run` — run binary in UAE/PiStorm
- `novusc trace` — view async traces
- `novusc package` — bundle binaries for distribution
- `novusc copperviz` — visualize copper lists
- `novusc blitviz` — visualize blitter dependency graphs

### VBCC Integration
- Emits `vasm`-compatible assembly
- `vlink` handles HUNK format and relocations
- Proper section management, symbol export, base-relative code support
- Build reproducibility via `novus.lock` and `novus.toml`

## Calling Convention (Amiga ABI)

- **Return:** `d0` (and `d1` for 64-bit pairs)
- **Args:** left-to-right into `d0,d1,a0,a1` then stack
- **Preserved:** `d2-d7`, `a2-a6`
- **Volatile:** `d0-d1`, `a0-a1`
- **Frame pointer:** `a6` for non-leaf functions
- **Library vectors:** follow Exec/NDK conventions

## Design Inspirations

- **C / Pascal** — classic Amiga development roots
- **Zig / Rust** — modern system language design
- **AMOS / Blitz Basic** — accessible, creative Amiga-era simplicity
- **Lua / Swift** — clean, readable, minimal syntax

## Implementation Roadmap

- [ ] Lexer and parser
- [ ] Intermediate representation (IR)
- [ ] Code generation backend (VBCC or LLVM-MOS)
- [ ] Standard runtime library (`novuslib.a`)
- [ ] Toolchain (`novusc`, linker integration)
- [ ] IDE/editor support with syntax highlighting
- [ ] Demo applications and documentation

## Key Design Decisions to Remember

1. **No runtime surprises** — everything deterministic
2. **No hidden heap allocations**
3. **Every system call yields a `Result`**
4. **Unsafe is visible, explicit, and rare**
5. **Readable assembly output** — compiled 68k should be understandable
6. **Amiga first** — not a portable systems language, but an Amiga revival

## Working Notes

- All allocating APIs accept optional allocator parameter (default: global fast-mem)
- Hardware DSLs compile to exact register sequences with compile-time validation
- PAL/NTSC awareness built into video mode profiles
- Copper/Blitter operations are first-class typed ops with safety checks
- Sprite width must be 16 pixels (hardware constraint)
- BOBs are arbitrary-sized with auto-generated masks
- Fat pointers (ptr+len) used for slices with debug bounds checks
- Async lowering creates state machines backed by Exec signals

## Notes for Claude

- When implementing compiler features, prioritize safety and clear error messages
- Assembly output should be readable and debuggable
- Follow Amiga NDK conventions strictly for FFI
- Validate chipset constraints at compile-time when possible
- Keep expert users empowered with escape hatches
- Don't hide the hardware — expose it safely
