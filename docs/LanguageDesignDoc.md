# Novus Language Design Document

## 1. Overview

**Name:** Novus
**Meaning:** Latin for “new”; symbolizing rebirth and innovation.
**Summary:** Novus is a modern systems programming language for the Amiga ecosystem. It blends the clarity and ergonomics of modern languages with the direct hardware access and efficiency required on 68k systems.
**Philosophy:** Simplicity, precision, and authenticity — a rebirth of Amiga development with modern design principles.
**Status:** Implemented language with explicitly marked design proposals. The executable
templates and `docs/AMIGA_LANGUAGE_AUDIT.md` are the current Amiga integration baseline.

---

## 2. Goals & Non-Goals

### Goals

* Provide a clean, expressive language for Amiga hardware and AmigaOS.
* Compile to highly efficient 68k machine code (via VBCC or LLVM-MOS backend).
* Offer modern language constructs (modules, structs, pattern matching) without hiding the hardware.
* Integrate seamlessly with AmigaOS libraries (Exec, Intuition, Graphics, DOS).
* Deliver a fast, deterministic runtime without garbage collection.

### Non-Goals

* Not cross-platform; Amiga is the focus.
* Not a managed or interpreted language.
* Not intended to abstract away hardware details — direct control is encouraged.

---

## 3. Target Audience

* Retro developers who want modern tooling.
* Game and demo coders seeking tighter Amiga integration.
* System programmers and experimenters.
* Educators introducing structured low-level programming.

---

## 4. Compilation Model

* **Frontend:** Novus → Intermediate Representation (IR)
* **Backend:** IR → 68k Assembly → Amiga Hunk format
* **Toolchain:** `novusc` (compiler), `novuslib` (standard library)
* **Linking:** Direct linking against AmigaOS system libraries.
* **Output:** `.hunk` executable, optionally with `.info` Workbench icon.

```text
[ Source ] -> [ Parser ] -> [ IR ] -> [ Codegen ] -> [ VBCC/LLVM-MOS ] -> [ Binary ]
```

---

## 5. High-Level Features

* **Novus-native systems syntax** with modern ergonomics.
* **Strong typing** with optional type inference.
* **Structs, enums, constants, and inline assembly** supported.
* **Modules & imports** for code organization.
* **Explicit memory management** with deterministic allocation.
* **Compile-time evaluation** (`const fn`-style).
* **Pattern matching** (`match` statements).
* **Slices and arrays** with bounds checks (compile-time where possible).
* **Fixed-point arithmetic** for efficient math.
* **Inline Amiga hardware access** through symbolic registers.

---

## 6. Amiga-Specific Features

### 6.1 Copper Lists

> ⚠️ **Status: 📅 Planned (v1.5)** — Parser grammar exists but codegen not implemented. Use inline assembly or `std/amiga/raw/` for now.

```novus
// Future DSL syntax (not yet implemented):
// copperlist {
//     move COLOR00, $0F0
//     wait 100, 0
//     move COLOR00, $00F
// }

// Current approach: use inline assembly or raw copper word arrays
```

### 6.2 Hardware Register Access

```novus
unsafe {
    write_volatile(color00, $0F0)
    memory_fence()
    let current = read_volatile(color00)
}
```

### 6.3 Exec & Intuition Integration

```novus
task := exec.CreateTask("MyTask", priority: 10)
window := intuition.OpenWindow(title: "Novus Demo", width: 320, height: 200)
```

### 6.4 Fixed-Point Math

`fixed16` uses 8.8 scaling and `fixed32` uses 16.16 scaling. Literals, arithmetic,
comparisons, and numeric conversions are implemented.

```novus
let angle: fixed16 = 45.0
let doubled = angle * 2.0
```

---

## 7. Example Program

This example uses the application-level Amiga UI API. Window ownership, native
message replies, and cleanup stay inside the library.

```novus
from std::core import Result
from amiga::ui import Bounds, Event, UiError, WindowBuilder

fn run() -> Result<(), UiError> {
    var ui = WindowBuilder::workbench_or_own()?
    var menus = ui.menu_builder()
    let menu = menus.build()?
    let window = ui.build("Hello Novus", Bounds::new(40, 30, 320, 200), menu)?

    for event in window.events() {
        match event {
            Event::Close => break,
            _ => {},
        }
    }
    return Result::Ok(())
}
```

---

## 8. Design Philosophy

* **Explicit over implicit.**
* **Predictable performance.**
* **Readable syntax over cleverness.**
* **Respect the machine.** Leverage the Amiga’s architecture instead of hiding it.

---

## 9. Implementation Roadmap

* [x] Lexer and parser (ANTLR4-based)
* [x] Intermediate representation (SSA-based IR)
* [x] Type checker and semantic analysis
* [x] Code generation backend (VBCC toolchain)
* [x] Standard runtime library (`std/`)
* [x] Toolchain (`novusc` with build, compile, check, fmt)
* [x] IDE/editor support (LSP with full diagnostics)
* [x] Async runtime (stackless coroutines)
* [x] Move semantics and RAII/Drop support
* [ ] Hardware DSLs (copper, blitter) — v1.5
* [ ] Graphics assets DSL (sprites, BOBs) — v1.5
* [ ] Fat binary support (multi-CPU dispatch) — v1.5
* [ ] Self-hosting compiler (ultimate goal)

---

## 10. Inspirations

* **C / Pascal** — The classic Amiga development roots.
* **Zig / Rust** — Modern system language design.
* **AMOS / Blitz Basic** — Accessible, creative Amiga-era simplicity.
* **Lua / Swift** — Clean, readable, minimal syntax.

---

## 11. Example CLI Usage

```bash
novusc hello.novus -o hello
hello
```

---

## 12. Taglines

* *"Novus — new code for classic machines."*
* *"Amiga reborn. Powered by Novus."*
* *"Old silicon, new fire."*
* *"The future, written in Novus."*

---

## 13. Building Libraries, Devices, Resources, Handlers & Interrupts

Novus generates resident tags, autoinit data, vector tables, A6 thunks, and lifecycle
bookkeeping from ordinary Novus declarations. Use `novus new library`, `device`,
`resource`, or `handler` for complete buildable examples.

### 13.1 Amiga register ABI

```novus
amiga fn hook(hook: *Hook in a0, object: *u8 in a2,
              message: *u8 in a1) -> u32 in d0 {
    return 0
}

type HookFn = amiga fn(*Hook in a0, *u8 in a2, *u8 in a1) -> u32 in d0
```

Register bindings are part of the function type, so incompatible callback signatures
are rejected before code generation.

### 13.2 Libraries

```novus
@library(name = "mydemo.library")
pub struct MyDemo {
    opens: u32,
}

impl MyDemo {
    pub fn add(left: i32, right: i32) -> i32 { return left + right }

    @libinit
    pub fn loaded(state: *MyDemo) -> bool {
        unsafe { (*state).opens = 0 }
        return true
    }
}
```

Optional `@libopen`, `@libclose`, and `@libexpunge` hooks receive persistent state;
generated code owns open counts and delayed expunge.

### 13.3 Devices and deferred I/O

`@devicecmd(cmd = ..., quick = true)` completes in BeginIO. A handler marked
`deferred = true` owns the request and must reply later. `@abortio` atomically recovers
that ownership and reports whether generated code should reply `IOERR_ABORTED`.

```novus
@devicecmd(cmd = 11, deferred = true)
pub fn begin_async(request: *IORequest, state: *DeviceState) -> i8 {
    // Queue request for a task or interrupt server.
    return 0
}

@abortio
pub fn abort_async(request: *IORequest, state: *DeviceState) -> bool {
    return false
}
```

Lifecycle hooks are `@deviceinit`, `@deviceopen`, `@deviceclose`, and
`@deviceexpunge`.

### 13.4 Permanent Exec resources

```novus
@resource(name = "counter.resource")
pub struct CounterState { value: u32 }

@resourcefunc
pub fn read(state: *CounterState) -> u32 {
    unsafe { return (*state).value }
}
```

The first parameter is generated persistent state and is hidden from callers.
`@resourceinit` optionally initializes it.

### 13.5 DOS handlers

Handler programs use `amiga::sys::dos::Packet`. `Packet::wait()` takes ownership,
`reply()` replies once, and `Drop` returns `ERROR_ACTION_NOT_KNOWN` if a branch forgets.
The handler template includes ACTION_STARTUP and ACTION_DIE handling.

### 13.6 Interrupts

`@interrupt` generates an Exec-compatible interrupt-server entry that returns with RTS.
`@interrupt_vector` is the explicit raw-vector form that returns with RTE. Interrupt
safety validation rejects allocation, blocking, async, and other unsafe calls.

### 13.7 Layout and shared memory

Use native `union` for overlaid ABI storage; reading a union field requires `unsafe`.
Use `read_volatile`, `write_volatile`, and `memory_fence` for device registers or memory
shared with hardware.

---

## 17. Modern Features Roadmap

> **Purpose:** Track which modern language concepts will be included in Novus, when, and why — balancing expressive power with performance and Amiga authenticity.

### Implementation Status Legend

- ✅ **Implemented** — Feature is complete and tested
- 🚧 **Partial** — Feature exists but incomplete
- 📅 **Planned** — Feature is designed but not yet implemented
- ❌ **Deferred** — Feature is postponed to a future version

### 🧩 v1.0 (Foundations – "A New System Language")

**Primary goals:** Stability, predictable performance, clean syntax, and full AmigaOS integration.

| Feature                              | Status           | Notes                                    |
| ------------------------------------ | ---------------- | ---------------------------------------- |
| `Result[T,E]` & `Option[T]`          | ✅ Implemented    | Mandatory in all std APIs                |
| `async/await` (stackless coroutines) | ✅ Implemented    | State machine transformation, signal-based futures |
| `defer` blocks                       | ✅ Implemented    | Deterministic resource cleanup           |
| Drop/RAII trait                      | ✅ Implemented    | Full ownership & move semantics          |
| `if let` / `let else`                | ✅ Implemented    | Pattern matching in conditionals         |
| `while var` syntax                   | ✅ Implemented    | Pattern-based loop conditions            |
| Turbofish (`::<T>`)                  | ✅ Implemented    | Explicit generic type specification      |
| Slices & views                       | ✅ Implemented    | Bounds-checked in debug builds           |
| `unsafe` blocks                      | ✅ Implemented    | For direct hardware or FFI use           |
| Modules & imports                    | ✅ Implemented    | No include hell                          |
| Pattern matching                     | ✅ Implemented    | Powerful match for enums & structs       |
| Generics (monomorphization)          | ✅ Implemented    | Full generic type support                |
| Trait bounds & where clauses         | ✅ Implemented    | Type constraints on generics             |
| Handles instead of raw pointers      | ✅ Implemented    | Safe resource ownership model            |
| AmigaOS FFI                          | ✅ Implemented    | exec, dos, graphics, intuition bindings  |
| Inline `asm {}`                      | ✅ Implemented    | Full register binding, clobbers, use clause |
| Testing framework (`#[test]`)        | ✅ Implemented    | Built into compiler with assertions      |
| Compiler toolchain (`novusc`)        | ✅ Implemented    | build, compile, check, fmt commands      |
| LSP / Editor support                 | ✅ Implemented    | Full language server with diagnostics    |
| Fixed-point math                     | 🚧 Partial       | Tokens exist, semantics not implemented  |
| Copper/Blitter DSL                   | 📅 Planned v1.5  | Parser rules exist, codegen not implemented |
| Async `sleep`, `vblank`, `input`     | ✅ Implemented    | Backed by timer & input devices          |
| Compile-time constants (`const fn`)  | 🚧 Partial       | Basic const evaluation                   |
| Libraries, devices, resources        | ✅ Implemented    | Native project attributes and generated residents |

### 🧠 v1.5 (Expressiveness & Ergonomics)

**Goal:** Improve developer productivity, add richer type and meta capabilities.

| Feature                               | Status           | Notes                                    |
| ------------------------------------- | ---------------- | ---------------------------------------- |
| Traits / Interfaces                   | ✅ Implemented    | Compile-time monomorphization            |
| Struct methods & extensions           | ✅ Implemented    | `impl Type { fn ... }` blocks            |
| Generics refinement                   | ✅ Implemented    | Type constraints via where clauses       |
| Async channels & streams              | ✅ Implemented    | Built atop Exec ports                    |
| Workspaces & multi-project builds     | ✅ Implemented    | `novus.toml` based builds                |
| Copper/Blitter DSL                    | 📅 Planned       | Hardware script DSLs                     |
| Graphics Assets DSL (sprites/BOBs)    | 📅 Planned       | Compile-time asset packing               |
| Fat binaries (multi-CPU dispatch)     | 📅 Planned       | 68020+ variants only                     |
| Closures                              | ✅ Implemented    | Capture analysis and owning environments |
| Compile-time reflection               | 📅 Planned       | For auto-docs & serialization            |
| String interpolation                  | 📅 Planned       | `"Hello {name}"` syntax                  |
| Built-in doc generator                | 📅 Planned       | Similar to Rustdoc                       |
| REPL / interactive mode               | 📅 Planned       | For experimentation & education          |

### 🚀 v2.0+ (Experimental / Forward-Looking)

**Goal:** Push Novus beyond 1990 limitations without losing authenticity.

| Feature                                      | Status           | Notes                                       |
| -------------------------------------------- | ---------------- | ------------------------------------------- |
| Lightweight concurrency (`spawn`, `join`)    | ✅ Implemented    | Built on Exec `CreateTask`                  |
| Safe borrow checking (lightweight lifetimes) | ✅ Implemented    | Reference lifetime tracking                 |
| Move semantics & use-after-move detection    | ✅ Implemented    | Compile-time move checker                   |
| Structural pattern destructuring             | ✅ Implemented    | `let Point{x, y} = pt` syntax               |
| Incremental compilation / hot reload         | 📅 Planned       | Useful on emulators                         |
| SIMD/vector types for Blitter ops            | 📅 Planned       | Exploits Amiga chipset parallelism          |
| Compile-time codegen (`@derive`)             | 📅 Planned       | Auto-impls, serialization, etc.             |
| AmigaOS resource reflection                  | 📅 Planned       | Introspect registered libraries/devices     |
| Modern Amiga toolchain integration           | 📅 Planned       | `novusc run` boots directly in UAE/PiStorm  |
| 68080/Apollo (AMMX) intrinsics               | 📅 Planned       | Advanced vector operations                  |

### 💬 Design Principles Recap

1. **No runtime surprises** – Everything deterministic.
2. **No hidden heap allocations.**
3. **Every system call yields a `Result`.**
4. **Unsafe is visible, explicit, and rare.**
5. **Readable assembly output** – Compiled 68k code should be understandable to mortals.
6. **Amiga first** – Not a “portable systems language” but a *revival* of Amiga development.

---

## 19. Toolchain Architecture & Pain Points

> **Purpose:** Describe the compiler pipeline, explain why VBCC is the target assembler/linker, and address the traditional pain points of the Amiga build ecosystem.

### ⚙️ 19.1 Overview

Novus emits optimized 68k assembly. The assembly is assembled and linked by **VBCC** using `vasm` and `vlink`.
Pipeline:

```
novusc → Novus IR → 68k assembly → vasm → object.o → vlink → executable
```

Benefits:

* Full control of IR → ASM.
* Avoids legacy compiler quirks (SAS/C, DICE).
* Uses VBCC’s stable 68k backend.
* Maintains AmigaOS compatibility (HUNK format).

### ⚠️ 19.2 Classic Amiga Toolchain Pain Points

| Problem            | Old Pain                                  | Novus + VBCC Solution                             |
| ------------------ | ----------------------------------------- | ------------------------------------------------- |
| Multiple compilers | SAS/C, DICE, Aztec, GCC all incompatible. | One unified front end targeting VBCC.             |
| Cryptic makefiles  | Complex flags like `-noixemul -fbaserel`. | Declarative `novus.toml` builds.                  |
| Assembler dialects | `phxass`, `devpac`, `as` differ.          | Emit VBCC-compatible `vasm` syntax.               |
| Linker hell        | Manual base-rel relocations.              | `vlink` invoked with correct HUNK flags.          |
| Tool fragmentation | Many binaries chained manually.           | `novusc build` orchestrates end-to-end.           |
| Header mismatches  | Inconsistent NDKs.                        | Canonical FFI in `std/amiga/raw/*`.                     |
| Cross-compiling    | Hand-maintained environments.             | Bundled cross-VBCC for all hosts.                 |
| Binary inspection  | Relied on ancient tools.                  | `novusc inspect` shows symbols, ROMTags, vectors. |

### 🧩 19.3 VBCC Integration Model

```toml
[target]
arch = "m68k"
cpu = "68020"
os   = "amigaos"
assembler = "vasm"
linker    = "vlink"

[output]
kind = "library"
name = "graphics.library"
```

Example build:

```bash
novusc mylib.novus -S -o mylib.s
vasm -Fhunk -m68020 -phxass mylib.s -o mylib.o
vlink -bamigahunk -Bstatic mylib.o -o LIBS/mylib.library
```

Features:

* Auto-selects CPU flags.
* Auto-detects required link libs.
* Emits `.map` and `.sym` manifests.
* Optional `--emit-c` for VBCC comparison/debug.

### 🧠 19.4 Base-Relative Code & Data

**Pain:** Manual `A4` relative data, fragile offsets.
**Fix:** Compiler emits base-rel segments automatically; `novusc` links with `-baserel`; ABI guarantees `A6`/`A4` preservation.

### 🧱 19.5 CPU Compatibility

**Pain:** Multiple builds across the supported 68020–68080 range.
**Fix:**

```bash
novusc --cpu 68020 --opt-level release
```

→ Emits CPU-tuned ASM; `@cpu()` attributes in inline ASM.

### 🧮 19.6 Linking & Symbols

**Pain:** Broken symbols and jump tables.
**Fix:** Symbol manifests validated pre-link; vector order and ROMTag verified post-link.

### 🧰 19.7 Build Reproducibility

**Pain:** Non-deterministic rebuilds.
**Fix:** `novus.lock` stores tool versions; build hash verification via `novusc verify-build`.

### 🚀 19.8 Tooling Summary

| Tool              | Purpose                          | Status           |
| ----------------- | -------------------------------- | ---------------- |
| `novus build`     | Compile–assemble–link pipeline   | ✅ Implemented    |
| `novus compile`   | Compile single file              | ✅ Implemented    |
| `novus check`     | Type-check without codegen       | ✅ Implemented    |
| `novus fmt`       | Format code                      | ✅ Implemented    |
| `novus test`      | Run test suite                   | ✅ Implemented    |
| `novus bench`     | Run benchmarks                   | ✅ Implemented    |
| `novus inspect`   | Inspect symbols, ROMTags         | 🚧 Partial        |
| `novus run`       | Run binary in UAE/PiStorm        | 📅 Planned        |
| `novus trace`     | View async traces                | 📅 Planned        |
| `novus package`   | Bundle binaries for distribution | 📅 Planned        |
| `novus copperviz` | Visualize copper lists           | 📅 Planned        |
| `novus blitviz`   | Visualize blitter jobs           | 📅 Planned        |

### 🔮 19.9 Future Integration (v2+)

* Parallel assembly & smart caching.
* Remote PiStorm deployment/testing.
* Optional LLVM backend.
* Binary diff and IR visualizer.

### 💬 19.10 Summary

VBCC = reliable, Amiga-native backend.
Novus = expressive, safe front-end.
Together they deliver a modern yet authentic Amiga development experience.

---

## 20. Compiler Architecture

> **Purpose:** Outline the Novus compiler implementation targeting modern systems using .NET and C#; describe how it emits 68k assembly and integrates with VBCC.

### 🧠 20.1 Implementation Platform

* **Language:** C# (.NET 8+)
* **Design:** Modular compiler pipeline written in managed code, cross-platform on macOS, Linux, and Windows.
* **Frontend:** Tokenizer, parser, and type checker implemented in C#.
* **Backend:** Custom 68k code generator producing `vasm`-compatible assembly.

### ⚙️ 20.2 Compilation Pipeline

```
Source (.novus)
   ↓
Lexer → Parser → AST
   ↓
Type Checker → IR Builder
   ↓
Optimizer (constant folding, inlining, etc.)
   ↓
68k Code Generator → Assembly (.s)
   ↓
vasm (assemble)
   ↓
vlink (link)
   ↓
Executable / Library / Device
```

### 🧩 20.3 Integration with VBCC

* **VBCC assembler (`vasm`)** receives `.s` output.
* **VBCC linker (`vlink`)** handles relocations and HUNK format.
* Novus emits proper `.section`, `.xdef`, `.xref`, `.align`, and relocation directives.
* `novusc` orchestrates assembler/linker invocations with correct flags.

### 🧮 20.4 Intermediate Representation (IR)

* SSA-like form for simplicity and optimization.
* Optimizations: constant folding, dead code elimination, inlining, and branch simplification.
* IR nodes map 1:1 to 68k instruction templates.

### 🧰 20.5 Assembly Emitter

* Outputs `vasm` syntax with readable comments.
* Annotates each instruction with source line numbers for debugging.
* Handles base-relative data, PC-relative addressing, and Exec ABI preservation.

### 🔍 20.6 Debugging & Tooling

* `--emit-ir` dumps intermediate form.
* `--emit-asm` shows final 68k code.
* `--trace` visualizes optimization passes.
* Integrated symbol map output for `novusc inspect`.

### 🧱 20.7 Cross-Platform Development

* Built on .NET → runs on macOS, Linux, Windows.
* Supports local testing and cross-compilation for Amiga.
* Can be distributed as a self-contained binary (`novusc`) with embedded VBCC toolchain.

### 🚀 20.8 Goals

* Deterministic output: same input = same binary.
* Full source–assembly traceability.
* Developer ergonomics and modern diagnostics.
* Seamless end-to-end build with VBCC.

---

---

## 22. Resource Ownership & Allocation Model

> **Goal:** Make everyday resource management (screens, windows, bitmaps, audio buffers) braindead simple via safe handles & RAII — **without** removing low‑level control. Developers can still use explicit `alloc/free`, arenas, and pools when they want maximum control.

### 22.1 Ownership, Handles, and RAII

* **Owned handles** (e.g., `ScreenHandle`, `WindowHandle`, `BitmapHandle`) encapsulate OS/chip resources.
* **`using`** syntax provides deterministic cleanup at scope exit; equivalent to `defer close(handle)`.
* Handles are **move‑only**; borrowed views (`&Screen`) cannot outlive their owner.
* Parent/child lifetimes are enforced: closing a `Screen` closes child `Window`s, sprites, etc., in a defined order.

```novus
using screen = try gfx.open_screen(320,256,5)
using win    = try ui.open_window(screen, "HUD")
// auto-closes: win, then screen
```

### 22.2 Allocators (Fast vs Chip, Custom Arenas & Pools)

> **Implementation Status:**
> - ✅ Global Fast/Chip allocation — Implemented via `std/memory/`
> - 🚧 Arena allocator — Partial implementation
> - 📅 Pool/Slab allocators — Not yet implemented
> - 📅 Custom allocator parameter on APIs — Not yet implemented

All allocating APIs accept an optional **allocator** parameter; default is a global fast‑mem allocator.

```novus
// Current working API:
from std::memory::block import MemoryBlock
let block = MemoryBlock::alloc(1024, MEMF_PUBLIC)?

// Future syntax (not yet implemented):
// var arena = mem.arena(size: 128*1024, kind: Chip)
// using screen = try gfx.open_screen(320,256,5, allocator: arena)
```

Provided allocators in `std/mem` (planned):

* **Global**: `mem.global()` (Fast) and `mem.chip()` (Chip).
* **Arena** (bump): `mem.arena(size, kind)` — linear alloc, `reset()` to free en masse.
* **Pool** (fixed‑size): `mem.pool(block_size, capacity, kind)` — O(1) alloc/free.
* **Slab** (typed): `mem.slab[T](capacity, kind)` — object pooling; returns `Handle[T]`.

### 22.3 Safe Allocation API (default)

```novus
// Untyped buffers
fn mem.alloc(bytes: u32, kind: MemKind = Fast, align: u16 = 8, zeroed: bool = false)
    -> Result[BufMut, Error]
fn mem.free(buf: BufMut)
fn mem.realloc(buf: BufMut, new_size: u32) -> Result[BufMut, Error]

// Typed convenience
fn mem.make[T](count: u32, kind: MemKind = Fast) -> Result[SliceMut[T], Error]
fn mem.dispose[T](slice: SliceMut[T])
```

* `BufMut` and `SliceMut[T]` are **fat pointers** (ptr+len) with bounds checks in debug.
* All functions are **fallible** and return `Result` (no silent NULLs).

### 22.4 Footguns On Demand (Unsafe Power Tools)

For experts who want full control, Novus exposes **unsafe** variants:

```novus
unsafe fn mem.alloc_unchecked(bytes: u32, kind: MemKind, align: u16 = 2) -> *u8
unsafe fn mem.free_ptr(ptr: *u8, kind: MemKind)
unsafe fn mem.from_raw_mut(ptr: *u8, len: u32) -> BufMut   // wrap raw memory
unsafe fn mem.to_raw(buf: BufMut) -> *u8                   // unwrap; caller owns
```

Rules:

* Marked `unsafe`; caller guarantees validity, alignment, and lifetime.
* Debug builds can **poison** freed memory and assert double frees.
* Release builds elide checks for speed.

### 22.5 Arenas & Pools: Examples

**Arena (frame allocator):**

```novus
var frame = mem.arena(64*1024, kind: Fast)
var tmp = try mem.alloc_in(frame, bytes: 4096)
// ... use tmp ...
frame.reset() // frees all arena allocations at once
```

**Object pool (sprites):**

```novus
var sprites = mem.slab[Sprite](capacity: 64, kind: Chip)
let s = try sprites.acquire()
// ... configure sprite ...
sprites.release(s)
```

**Fixed block pool (messages):**

```novus
var mpool = mem.pool(block_size: 256, capacity: 128, kind: Fast)
let msg = try mpool.alloc()
// ...
mpool.free(msg)
```

### 22.6 Integration with Handles

* Opening a `Screen` allocates bitplanes and copper buffers **inside the handle**; freeing the handle frees those.
* You can supply a custom allocator to any resource‑creating API:

```novus
using screen = try gfx.open_screen(320,256,5, allocator: mem.chip())
using sprite = try gfx.load_sprite("ship.spr", allocator: mem.chip())
```

* To extend lifetime beyond scope, **detach** ownership explicitly:

```novus
using win = try ui.open_window(screen, "Tool")
let owned = win.detach() // caller must ui.close(owned) later
```

### 22.7 Debug Aids

* **Leak detector** (debug): warns on dropped but unclosed handles.
* **Lifetime lints**: “opened here, never closed” unless wrapped in `using`/`defer`.
* **Allocator tracing**: `novusc trace --alloc` correlates sites, sizes, and callers.

### 22.8 Philosophy

* **Safe by default**: RAII handles and `Result` everywhere.
* **Footguns available**: explicit `unsafe` APIs for raw control.
* **Deterministic**: no GC; lifetimes are visible and predictable.
* **Choice**: arenas, pools, and slabs for performance‑critical code.

---

## 23. Hardware DSLs & Safe HAL (Copper · Blitter · Paula · Sprites/Bitplanes)

> ⚠️ **Implementation Status: 📅 PLANNED (v1.5)**
>
> This section describes the *design specification* for hardware DSLs. The parser grammar includes support for `copper` and `blitter` blocks, but semantic analysis and code generation are **not yet implemented**. For v1.0, use the `std/amiga/raw/` bindings and inline assembly for direct hardware access.

> **Purpose:** Make the *fun stuff* braindead simple and insanely powerful. These DSLs compile to exact register sequences (Copper words, BLTCONx, Paula periods, etc.) with compile‑time checks, PAL/NTSC awareness, and zero inline asm for 99% of use cases.

### 23.1 Design Principles

1. **First‑class, typed ops** for each chipset unit (Copper, Blitter, Paula).
2. **Compile‑time validation** of addresses, ranges, timing constraints.
3. **Deterministic output**: inspectable, one‑to‑one with hardware words.
4. **Memory‑kind aware**: `ChipBuf` vs `FastBuf` enforced by types.
5. **Async‑friendly**: helpers that integrate with `await vblank()` and device signals.

---

### 23.2 Copper DSL (`hw.copper`)

#### 23.2.1 Core API

```novus
import hw.copper as cop

fn build_example() -> cop.List {
  return cop.build {
    move(COLOR00, RGB(255,0,0))
    wait(scan(64))
    move(COLOR00, RGB(0,0,255))
    wait(scan(128))
    end()
  }
}
```

**Ops:**

* `move(reg: CopReg, value: u16)` — only Copper‑legal registers.
* `wait(v: VPos, h: HPos = h(0), bmask: u8 = $FF)` — even `h` enforced.
* `skip(v: VPos, h: HPos = h(0), bmask: u8 = $FF)`
* `end()` — inserts `COPPER_END`.

**Position helpers:** `scan(y)`, `beam(y,x)`, `pal()`, `ntsc()` toggle; compiler knows max scans.

#### 23.2.2 Convenience & Patterns

```novus
cop.palette_gradient(range: scan(40)..scan(180), reg: COLOR00,
                     from: RGB(255,0,0), to: RGB(0,0,255), steps: 32)

cop.band(y: 80..120) {
  move(BPLCON0, Mode.bitplanes(5))
  move(COLOR01, RGB(32,255,32))
}
```

#### 23.2.3 Safety & Diagnostics

* **Checks**: illegal reg; OOB color; odd `hpos`; invalid wait order; forbidden write windows.
* **Errors** (examples):

  * `error: COLOR06 is not writable by the Copper`
  * `error: WAIT hpos must be even (got 133)`
  * `warning: PAL mode: scan(312) is outside visible range`

#### 23.2.4 Integration

```novus
using s = try gfx.open_screen(320,256,5)
let list = build_example()
s.set_copper(list)        // uploads to chipmem and enables DMA
await vblank()            // sync helper from std/time
```

#### 23.2.5 Tooling

* `novusc copperviz list.cop` → visual timeline (PNG/ASCII).
* `--emit-words` dumps raw 16‑bit Copper words with labels.

---

### 23.3 Blitter Jobs DSL (`hw.blit`)

#### 23.3.1 Core API

```novus
import hw.blit as blt

fn draw_sprite(dst: BitmapHandle, src: BitmapHandle, x:i16,y:i16) -> Result[(),Error] {
  return blt.job {
    op      = CopyMasked(src, mask = src.mask)
    target  = dst.at(x,y)
    size    = pixels(width:32, height:32)
    fence   = Auto   // inserts BLTWAIT if needed
  }
}
```

**Ops/Builders:**

* `Copy`, `CopyMasked(mask)`, `OR`, `XOR`, `MinTerm(m)`, `Fill`, `Line`
* `size = pixels(w,h)` or `tiles(cols,rows, tile_w, tile_h)`
* `source`, `target`, `modulo`, `shift`, `descending` flags

#### 23.3.2 Safety & Performance

* Auto‑computes **minterm**, **modulo**, **BLTSIZE**, **BLTCONx**.
* Ensures **chipmem residency** for sources/targets.
* Bounds checks in `debug`; elided in `release` when provable.
* **Batching**: queue multiple jobs → `blt.fence()` to wait once.

#### 23.3.3 Common Helpers

```novus
blt.fill(dst.rect(0,0,320,10), color: 1, or_mode: true)
blt.line(dst, from(10,10), to(120,88), color: 3, inclusive: true)
```

---

### 23.4 Paula Audio (`audio`)

#### 23.4.1 Channels API

```novus
let ch0 = try audio.open_channel(0)
try ch0.play(sample, rate: 22050, loop: true, volume: 48)
try ch0.set_period(note_to_period(A4))
```

* Ensures **chipmem** sample placement; optional resampler to legal periods.
* Async: `await ch0.on_complete()`; cancel with token.

#### 23.4.2 Mix & Timing

* `audio.mix(samples: []i8, to: ChipBuf)` helper for offline compose.
* VBlank or CIA‑timer synced callbacks via `await vblank()` or `await timer(ms)`.

---

### 23.5 Sprites & Bitplanes

#### 23.5.1 Sprite API

```novus
using s = try gfx.open_screen(320,256,5)
let spr = try gfx.load_sprite("ship.spr", allocator: mem.chip())
try s.sprite(0).show(spr).at(100,120)
```

* Validates **alignment**, **stride**, **chipmem** residency.
* Per‑frame upload helpers; double‑buffering utilities.

#### 23.5.2 Bitplanes API

```novus
try s.bitplane(2).set_data(buf: ChipBuf)
try s.set_depth(5)
```

* Ensures modulo correctness, plane alignment, and DMA cutoff safety.

---

### 23.6 Interrupts & Timing Helpers

* Prefer **signal‑first** pattern; tiny ISRs:“set signal, return”.
* Helpers:

```novus
let sig = exec.alloc_signal()?
install_vblank_handler(sig)
async fn tick() { loop { await signal(sig); update(); } }
```

* `await vblank()`, `await raster(y)`, `await timer(ms)` provided.

---

### 23.7 Memory Classes & Uploaders

* `ChipBuf` and `FastBuf` types enforce correct residency.
* `chip.upload(list)` guarantees chipmem placement & alignment.
* Zero‑copy when possible; safe copies when crossing mem kinds.

---

### 23.8 PAL/NTSC Awareness & Mode Profiles

* Compile‑time `@video_mode(pal|ntsc|auto)` attribute adjusts scan ranges and timing.
* `gfx.mode()` returns current profile (for runtime portability).

---

### 23.9 Introspection & Debuggability

* `--emit-copper` and `--emit-blit` dump finalized words/regs.
* Source ↔ word sourcemaps for IDEs.
* `novusc copperviz` renders visual timelines; `novusc blitviz` shows blit dependency graphs.

---

### 23.10 Philosophy

* **Make the right thing the easy thing.**
* Keep **experts empowered**: every DSL emits clean, readable ASM you can audit.
* **No hidden magic**: errors are explicit, outputs are deterministic.

---

## 24. Library Abstraction Layers & FFI Strategy (Exec · Intuition · Graphics · DOS · Devices)

> **Goal:** Expose the full power of the Amiga libraries without "re‑implementing C". Novus provides a tiered model: **thin FFI**, **safe wrappers**, and **ergonomic builders/DSLs** — all zero‑cost and always allowing escape hatches to raw calls.

### 24.1 Design Tenets

1. **No duplication of effort**: raw NDK prototypes are available as‑is via `extern "amiga"` in `std/amiga/raw/*`.
2. **Opt‑in safety**: idiomatic Novus APIs wrap NDK with `Result`/`Option`, typed handles, and RAII.
3. **Zero‑cost abstractions**: builders and DSLs lower to the same calls/TagItem arrays/IORequests a C expert would write.
4. **Capability types, not globals**: permissions and ownership are expressed in types (e.g., `CopperAccess`).
5. **Escape hatches**: you can always call raw `extern` or embed `asm`.

---

### 24.2 Thin Layer: Canonical FFI (1:1 with NDK)

* Location: `std/amiga/raw/exec`, `std/amiga/raw/intuition`, `std/amiga/raw/graphics`, `std/amiga/raw/dos`, `std/amiga/raw/devs/*`.
* Generated from `.fd`/protos + a mapping file (see §13 NDK→Result mapping).
* Example:

```novus
extern "amiga" fn OpenLibrary(name: cstr, version: u32) -> *Library
extern "amiga" fn CloseLibrary(lib: *Library)
extern "amiga" fn OpenWindowTagList(tags: *TagItem) -> *Window
```

* **Contract**: never changes signatures; used by safe layers and by experts.

---

### 24.3 Safe Layer: Result + Handles (RAII, typed)

* Location: `std/sys/*` and `std/ui/*`.
* Adds: version checks, lifetime management, chip/fast memory residency, `Result` error mapping.

**Exec**

```novus
module sys.exec

pub struct MsgPortHandle { /* opaque */ }

pub fn create_port(name: str) -> Result[MsgPortHandle, Error]
pub fn delete_port(port: MsgPortHandle)

pub fn alloc_signal() -> Result[Signal, Error]
pub fn wait(mask: u32) -> u32
```

**Intuition (typed TagLists)**

```novus
module ui.window

pub struct WindowHandle { /* ... */ }

pub fn open(builder: WindowBuilder) -> Result[WindowHandle, Error]

pub struct WindowBuilder {
  title: str,
  size: Size,
  pos:  Position = Centered,
  flags: WindowFlags = Default,
}

impl WindowBuilder {
  fn to_tags(self) -> []TagItem  // zero‑copy view for OpenWindowTagList
}
```

**Graphics**

```novus
module gfx
pub fn open_screen(w:u16,h:u16,depth:u8, allocator:Allocator = mem.chip())
  -> Result[ScreenHandle, Error]

pub fn close(screen: ScreenHandle)
pub fn bitplane(screen:&ScreenHandle, n:u8) -> BitplaneView
```

**DOS (Result + async packets)**

```novus
module fs
pub struct FileHandle { /* opaque */ }

pub fn open(path: str, mode: OpenMode) -> Result[FileHandle, Error]
pub fn read_all(path: str) -> Result[[]u8, Error]

async pub fn open_async(path: str, mode: OpenMode) -> Result[FileHandle, Error]
async pub fn read_exact_async(f: FileHandle, buf: []mut u8) -> Result[(), Error]
```

---

### 24.4 Ergonomic Layer: Builders & DSLs (zero‑cost sugar)

These emit the **same TagItem arrays, IORequests, and register writes** you would hand‑craft in C, but with types and checks.

**Intuition Window Builder → TagList**

```novus
using win = try ui.window.open(WindowBuilder {
  title: "Nova HUD",
  size:  Size{320, 200},
  pos:   Centered,
  flags: Standard | DepthGadget | DragBar,
})
```

Lowering (conceptual): builds a stack TagItem array → calls `OpenWindowTagList(tags)`; no heap, no runtime overhead.

**DOS Path API**

```novus
let home = path.home() / "S" / "User-Startup"
let bytes = try fs.read_all(home)
```

→ Uses `ExAll/Examine` under the hood for traversal; returns `Result`.

**Devices**

```novus
using trackdisk = try dev.open("trackdisk.device")
let info = try trackdisk.get_geometry()
```

→ Constructs and sends IORequests; maps error codes to `Error`.

---

### 24.5 Versioning & Feature Gates

* Each safe module declares **minimum library versions**; open fails with `Error.Unsupported(version)`.
* `@requires(graphics>=39)` attributes allow compile‑time gating.
* Runtime feature checks cache vectors/capabilities for hot paths.

---

### 24.6 Capability Types (power without globals)

* Example: `CopperAccess`, `DMABlitter`, `AudioChan<N>` prove access rights.
* APIs require the capability instead of raw global state.

```novus
let cap = try hw.acquire_copper()
try hw.copper.install(cap, list)
```

---

### 24.7 Escape Hatches (no walls)

1. Call raw FFI directly:

```novus
extern "amiga" fn SetWindowTitles(w:*Window, t:cstr, s:cstr)
```

2. Build raw `TagItem` arrays yourself; pass into safe wrappers.
3. Use `asm {}` or direct register declarations (see §22).

---

### 24.8 Examples: C vs. Novus (side‑by‑side intent)

**Open a window**

```c
// C
struct TagItem tags[] = {
  {WA_Title, (IPTR)"Nova HUD"},
  {WA_Width, 320}, {WA_Height, 200},
  {WA_Flags, WFLG_SIZEGADGET|WFLG_DRAGBAR},
  {TAG_DONE, 0}
};
struct Window* w = OpenWindowTagList(NULL, tags);
if(!w) return 0;
```

```novus
// Novus (same TagList under the hood)
using win = try ui.window.open(WindowBuilder{
  title:"Nova HUD", size:Size{320,200}, flags: SizeGadget|DragBar
})
```

**Read a file**

```c
BPTR f = Open("S:User-Startup", MODE_OLDFILE);
if (!f) return IoErr();
// ... Read/Close with error checks ...
```

```novus
let bytes = try fs.read_all("S:User-Startup")
```

---

### 24.9 Zero‑Cost Guarantee

* Builders allocate on the **stack**; TagItem arrays/slices passed directly to FFI.
* No implicit heap allocations or background threads.
* Release builds inline through wrappers to identical call sequences as C.

---

### 24.10 Generation & Maintenance

* A codegen tool scans NDK `.fd`/includes and produces:

  * Raw FFI declarations (`extern "amiga"`).
  * Safe wrappers with `Result` and handle types.
  * YAML per‑function failure semantics (sentinels, IoErr, D0 codes) used by the generator.
* Continuous tests compare Novus wrapper calls against a C reference harness to ensure ABI parity.

---

### 24.11 What we will **not** do

* We will not hide the OS — everything maps back to Exec/Intuition/Graphics/DOS.
* We will not invent incompatible paradigms that make Amiga docs useless.
* We will not add GC, exceptions, or implicit threads.

---

### 24.12 Summary

Novus exposes NDK power with clarity and safety:

* **Thin FFI** for experts, **safe handles** for day‑to‑day work, **builders/DSLs** for joy.
* Always zero‑cost, always auditable, always Amiga.

---

## 25. Amiga‑Centric Graphics Assets DSL (Sprites · BOBs · Bitmaps · Fonts)

> ⚠️ **Implementation Status: 📅 PLANNED (v1.5)**
>
> This section describes the *design specification* for graphics asset DSLs. These features are **not yet implemented**. For v1.0, use the `std/graphics/` wrappers and load assets from files or embed raw data.

> **Goal:** Make it **stupidly simple** to put pixels on the screen using authentic Amiga concepts. No NES‑style tiles. Focus on: **hardware sprites** (always 1 word wide), **BOBs** (blitter objects with masks), **bitmaps** (interleaved bitplanes), and **bitmap fonts**. All compile to chip‑ready data with zero inline asm.

### 25.1 Design Overview

* **Author at source** using compact, readable pixel notation.
* Compiler **packs to interleaved bitplanes** (OCS/ECS: 1–6 bpls; AGA later), computes modulos, and validates alignment.
* Emits **ChipBuf** assets + metadata (width, height, depth, modulo, mask offsets).
* One‑liners to draw: `sprite.show(...)`, `bob.draw(...)`, `bitmap.blit(...)`, `font.draw_text(...)`.

---

### 25.2 Hardware Sprites (16 px wide, arbitrary height)

Amiga sprites are 16 pixels (1 word) wide per pair of bitplanes; attach pairs for more colors.

#### 25.2.1 Sprite Authoring DSL

```novus
import gfx.sprite as spr

const SHIP = spr.bank {
  depth: 2,              // 2 bpls (4 colors); use attached:true for 4 bpls
  attached: false,

  sprite Idle {
    "..112211..2211.."
    "..112211..2211.."
    // ...as many rows as you want (height inferred)
  }

  sprite Thrust {
    "..112211..2211.."
    "..1122ff..22ff.."
    // f = palette index 15 when depth>=4; symbols must < 2^depth
  }
}
```

#### 25.2.2 Using Sprites

```novus
using s = try gfx.open_screen(320,256,4)
let bank = SHIP.upload(mem.chip())
try s.sprite(0).show(bank.sprite("Idle")).at(100,120)
try s.sprite(0).set_frame(bank.sprite("Thrust"))
```

* Validations: width **must be 16**, height ≤ 256, attached sprites require depth=4.
* Auto‑computes control words, data pointers, and attaches pairs when `attached:true`.

---

### 25.3 BOBs (Blitter Objects) with Masks

BOBs are arbitrary‑sized, multi‑bitplane images blitted into bitmaps/screens, usually with a **1‑bit mask**.

#### 25.3.1 BOB Authoring DSL

```novus
import gfx.bob as bob

const HUD = bob.bank {
  depth: 3,                 // 8 colors
  mask: Auto,               // Auto‑generate 1‑bit mask from non‑zero pixels (or None/From(plane))

  bob Ship32x32 {
    size: {w:32, h:32}
    "..1122..1122..1122..1122..1122..1122..1122..1122"
    // 32 chars per row; repeat for 32 rows (omitted for brevity)
  }
}
```

#### 25.3.2 Drawing BOBs

```novus
let assets = HUD.upload(mem.chip())
try bob.draw(dst: s.bitmap(), src: assets["Ship32x32"], at:{x:100,y:120})
```

* Auto‑computes **minterm**, **BLTCONx**, **modulos**, **BLTSIZE**, and inserts `BLTWAIT` when `fence:Auto`.
* Debug checks ensure **chipmem** residency and bounds; elided in release.

---

### 25.4 Bitmaps (Interleaved Bitplanes)

Define raw interleaved bitmaps that match screen depth and draw them directly.

```novus
import gfx.bitmap as bmp

const PANEL = bmp.define {
  depth: 4,
  size: {w: 128, h: 32},
  // per‑plane or indexed rows allowed; compiler packs to interleaved bitplanes
  plane 0 {
    "################................................................................"
    // 32 rows of 128 columns total
  }
  plane 1 { /* ... */ }
  plane 2 { /* ... */ }
  plane 3 { /* ... */ }
}

let chip_panel = PANEL.upload(mem.chip())
try chip_panel.blit(to: s.bitmap(), at:{x:0,y:0})
```

---

### 25.5 Bitmap Fonts (Monospace & Variable Width)

Define bitmap fonts as glyph grids; compiler builds bitplanes and **glyph metrics**.

```novus
import gfx.font as font

const SYSFONT = font.define {
  depth: 2,
  cell: {w:8, h:8},         // monospace cell size
  map: "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789:.-! ?",
  glyph A { "..11..11" "..11..11" "..111111" "..11..11" "..11..11" "........" "........" "........" }
  // ... other glyphs ...
}

let f = SYSFONT.upload(mem.chip())
try f.draw_text(s.bitmap(), at:{x:16,y:16}, text:"AMIGA NOVUS", color:3)
```

* Variable‑width: provide `advance` or let compiler compute from left/right trims.
* Optional **shadow/outline** effects via helper that composes multiple blits.

---

### 25.6 Notation & Symbols

* **Indexed notation**: one character per pixel (e.g., `0..9 A..Z a..z _`) → palette index. `.` = 0. Must be `< 2^depth`.
* **Per‑plane notation**: `plane N { "#..#" ... }` with `#`=1, `.`=0.
* Mixed usage allowed; compiler validates consistency.

---

### 25.7 Validation & Diagnostics

* Sprites: width==16; attached mode requires depth=4; height ≤ 256.
* BOBs/Bitmaps: rows must match `w`; correct number of rows `h` required.
* Palette indices within range; alignment to 16‑bit words; modulo computed.
* Examples:

  * `error: sprite width must be 16 (got 20)`
  * `error: symbol 'G'(16) exceeds depth=3 (max 7)`
  * `error: 30 rows given (expected 32)`

---

### 25.8 Runtime API Summary

* **Sprites**: `spr.bank.upload() -> SpriteBank`, `screen.sprite(n).show(frame)`
* **BOBs**: `bob.bank.upload() -> BobBank`, `bob.draw(dst, src, at, fence=Auto)`
* **Bitmaps**: `bmp.define.upload() -> ChipBitmap`, `chipbmp.blit(to, at)`
* **Fonts**: `font.define.upload() -> ChipFont`, `font.draw_text(bitmap, at, text, color)`

All uploads ensure **chipmem** placement and return handles with depth/stride metadata matching the target screen.

---

### 25.9 Interop & Export

* `export_iff()` to generate ILBM with palette for docs/tools.
* `--emit-bpls` compiler flag dumps raw interleaved planes for inspection.
* Builders produce the same structures a C pro would hand‑craft, but safer and faster.

---

### 25.10 Philosophy

* **Amiga‑native first**: sprites, BOBs, bitmaps, fonts — no NES tiles.
* **One‑liners to draw**; **Result** on all fallible ops.
* **Deterministic and inspectable**: you can audit emitted words and blit regs any time.

---

## 26. Target Profiles & Fat Binaries (CPU · Chipset)

> **Implementation Status:**
> - ✅ CPU target selection (`--cpu 68020/68030/68040/68060/68080`) — Implemented
> - 📅 Chipset profiles (`--chipset OCS|ECS|AGA`) — Not yet implemented
> - 📅 68020+ fat binaries — Not yet implemented
> - 📅 `@multiversion` attribute — Not yet implemented

> **Purpose:** Let developers tune 68020+ programs without forking code. Profiles guarantee the compiler never emits instructions newer than the chosen target.

### 26.1 CPU ISA Profiles

* **`m68k-020`** (68020/030): minimum profile; bitfields, 32×32 mul/div, and PC-relative forms. **Default.**
* **`m68k-040`** (68040): caches/pipeline; certain ops trap (emulated by OS); we avoid trappy ops unless gated.
* **`m68k-060`** (68060): even stricter on trappy ops; we prefer core integer ops by default.
* **`apx-080`** (68080/Apollo): optional profile; future intrinsics (e.g., AMMX) via `std/intrin/ammx`.

**CLI:**

```bash
novusc --cpu 68020            # single target
novusc --cpu 68060            # accelerated
```

### 26.2 Chipset Profiles (Independent of CPU)

* **`chipset=OCS | ECS | AGA | auto`**

  * Governs max bitplanes, sprite count/width, color registers, scan ranges, DMA quirks.
  * DSLs (`hw.copper`, `hw.blit`, sprites/bitplanes) validate against the active chipset profile.

**CLI:**

```bash
novusc --chipset OCS
novusc --chipset auto         # detect at runtime, validate at compile-time with widest common subset
```

### 26.3 Feature Gating in Code

Attributes declare CPU requirements clearly and safely:

```novus
@cpu(min=68020)
fn fast_fixed_mul(a: fixed32, b: fixed32) -> fixed32 { /* 020+ lowering */ }

@cpu(68060)
fn ammx_sprite_blend(...) { /* 060/080+ intrinsics when available */ }
```

* The compiler refuses to emit gated code for incompatible profiles.
* Inline `asm {}` also requires `@cpu(min=...)` when using profile-specific opcodes.

### 26.4 Fat Binaries (Multi-Version Dispatch)

Build multiple ISA versions of hot functions and auto-dispatch at runtime.

**CLI:**

```bash
novusc --cpu fat:020,040,060
```

**Source (two ways):**

```novus
// Let compiler clone & specialize automatically
@multiversion(cpu=[020,040,060])
fn memcpy(dst: []mut u8, src: []u8, n: u32) { /* generic body; compiler specializes */ }

// Or write explicit versions
fn memcpy(dst:[]mut u8, src:[]u8, n:u32) { /* 020 baseline */ }
@impl(cpu=020) fn memcpy(...) { /* 020+ using bitfields/addressing */ }
@impl(cpu=060) fn memcpy(...) { /* 060-tuned */ }
```

**Dispatch:**

* A tiny startup probe inspects `ExecBase->AttnFlags` / CPU ID once and stores an enum.
* Call sites jump to the best-matching version. Overhead is typically one indirect jump.

### 26.5 Codegen Guarantees

* Per profile, the compiler **never** emits unsupported instructions.
* 68040/68060 **trappy** ops are avoided unless explicitly gated (`@cpu(040|060)`).
* IR→ASM lowering picks addressing modes valid for the profile; inline asm is validated too.

### 26.6 Recommended Defaults

* **Project default:** `--cpu 68020 --chipset auto` (best balance for A1200/A4000/accelerated machines).
* **Minimum build:** `--cpu 68020 --chipset OCS`.
* **Accelerated build:** `--cpu 68060 --chipset AGA`.
* **One binary to rule them all:** a future 68020+ fat target.

### 26.7 Interaction with DSLs & Stdlib

* Hardware DSLs choose the **widest safe lowering** for the active profile (e.g., blitter waits, copper ranges).
* Stdlib uses `@cpu(min=...)` internally to specialize routines; portable fallbacks remain available.

### 26.8 Testing Matrix

`novusc test --matrix` runs suites across selected CPU/chipset profiles under emulation (UAE) and, optionally, on PiStorm.

---

---

## 27. IR → 68k Lowering (Codegen Details)

> **Purpose:** Specify how Novus IR is translated into Motorola 68k assembly for each CPU profile, including register allocation, calling conventions, stack frames, hardware/volatile semantics, and async state machines.

### 27.1 Calling Convention (Amiga ABI)

* **Return:** `d0` (and `d1` for 64-bit pair if needed).
* **Args (by value):** left-to-right into registers then stack. Preferred registers: `d0,d1,a0,a1` (profile-tunable). Excess → stack (word-aligned).
* **Preserved (callee-save):** `d2–d7`, `a2–a6`.
* **Volatile (caller-save):** `d0–d1`, `a0–a1`.
* **Frame pointer:** `a6` used as frame base in non-leaf or when needed for baserel code.
* **Library/device vectors:** follow Exec/NDK conventions; vtable thunks preserve `a6`/`a4` and use `jsr`/`jmp (a6,disp)`.

### 27.2 Stack Frame Layout

```
(sp+)  : return addr
(sp+4) : saved a6 (if used)
(sp+8) : locals / spills / temps (aligned)
```

* **Prologue (typical):**

```asm
link    a6,#-locals_size
movem.l d2-d7/a2-a5,-(sp)    ; save callee-saved as required
```

* **Epilogue:**

```asm
movem.l (sp)+,d2-d7/a2-a5
unlk    a6
rts
```

* Leaf functions may omit `link/unlk` and use `addq.l #n,sp`.

### 27.3 Register Allocation

* Linear-scan per block with live-range splitting; coalescing for copy elimination.
* Preference: address calc in `a*`, arithmetic in `d*`.
* CPU-profile-aware addressing uses forms supported by the selected 68020+ target.

### 27.4 IR Op → Instruction Mapping (Core)

| IR              | 68020+ lowering                         |
| --------------- | --------------------------------------- |
| `add/sub`       | `add.*` / `sub.*`                       |
| `mul i32`       | `muls.l` / `mulu.l`                     |
| `div i32`       | `divs.l` / `divu.l`                     |
| `and/or/xor`    | `and/or/eor.*`                          |
| `shl/shr`       | `lsl/lsr/asl/asr`                       |
| `cmp.*`         | `cmp.*` + `scc` when profitable         |
| `load`          | `move.*` with target-valid address mode |
| `store`         | `move.* dN/aN,(addr)`         | same                                   |
| `lea`           | `lea`                         | `lea` / PC-relative forms              |
| `memcpy/memset` | small inline loop             | larger: `movem`/unroll (profile-tuned) |
| `phi`           | copy insertion at block edges | same                                   |
| `call`          | `jsr symbol`                  | same + `bsr` when local                |
| `ret`           | `rts`                         | `rts`                                  |

### 27.5 Volatile / Hardware Access

* IR `hw.write reg, val` → `move.w #val,$DFFxxx` (or `.l` size as required).
* `:=` operator enforces **volatile**: no reordering across the op; codegen emits a **barrier** (dummy `mov` to memory or `nop` fence) around sequences when needed.
* Reads (`hw.read`) use `move.w $DFFxxx,dN` and mark as volatile.

### 27.6 Control Flow & Branching

* IR `br cond, L1, L2` lowers to `tst/cmp` + `beq/bne/bcc/bcs` etc.
* **Fallthrough shaping** picks the likely path (profiled later) to reduce branches.
* Switch/match may lower to jump tables (020+) or chained branches (000).

### 27.7 Fixed-Point Math

* `fixed16` (8.8): uses `muls.w` then `asr.l #8` (+ rounding option).
* `fixed32` (16.16): 020+: `muls.l` + `asr.l #16`; 000: call into `__novus_fixmul32` helper (inlined if small).

### 27.8 Slices & Bounds Checks

* Slice access `buf[i]` in **debug**: `cmp i,len ; bhi trap`. Trap calls `__novus_bounds_fail`.
* **Release**: checks elided if proven safe (range analysis) else keep single compare.

### 27.9 `Result` / `Option` Conventions

* Tagged union layout: `tag: u8 (aligned to word)` + payload.
* Fast-path for `Ok`: branch around small `Err` construction.
* `try/?` sugar emits `cmp tag,OK ; bne .return_err` with tail `rts`/`ret` using current function’s `Result` layout.

### 27.10 Async Lowering (Stackless Coroutines)

* Each `async fn` becomes a **state struct** in `.bss/.data` (or stack/arena) with fields for locals + `state u8`.
* `await` lowers to a call to the callee future’s `poll`, returning `Pending|Ready`.
* Executor loop uses `Wait(mask)` and signals to resume.
* **Prologue/epilogue** manage saving live registers into the state struct before returning `Pending`.

**Snippet (conceptual):**

```asm
; poll(ReadFileFuture *f)
cmp.b   #0,(f).state
beq.s   .start
...
.start:
; kick async op, store waker signal, set state=1
move.b  #1,(f).state
moveq   #PENDING,d0
rts
.state1:
; check completion, produce Ready or keep Pending
```

### 27.11 Prologue/Epilogue Variants

* **Leaf fast-path:** use `addq/subq sp` instead of `link/unlk`.
* **Interrupt/ISR** (`@interrupt(level)`): compiler emits appropriate prologue preserving `sr`, minimal registers; returns quickly after setting a signal.

### 27.12 Library/Device Vector Lowering

* `@libvec`/`@devicevec` functions land in a **fixed order table**; thunks ensure ABI compliance and base-relative addressing.
* Auto-generated **ROMTag**/AutoInit data emitted in dedicated sections; `vlink` script places them per NDK requirements.

### 27.13 Baserel & PC-rel (Position-Independent)

* For libraries/devices, data accesses use `a4`-relative baserel sequences (emitted when target kind requires).
* Code uses PC-relative addressing when profitable (`020+`).

### 27.14 CPU Profile Rules

* **000 profile:** no `muls.l/divs.l`, no bitfield ops, simple addressing; helper calls for 32-bit mul/div; jump tables avoided.
* **020+ profile:** enable bitfields, scaled index, PC-relative forms; 32×32 mul/div native.
* **040/060:** avoid trappy FP/bitfield forms unless gated; prefer integer sequences.
* **080 future:** gated intrinsics map to AMMX via `std/intrin`.

### 27.15 Example: Lowering Walkthrough

**Source:**

```novus
fn clamp_add(a:i16, b:i16, max:i16) -> i16 {
  let s = a + b
  if s > max { return max }
  return s
}
```

**IR:**

```
%0 = add i16 %a,%b
%1 = cmp.gt %0,%max
br %1, Lmax, Lret
Lmax: ret %max
Lret: ret %0
```

**68020 baseline ASM (sketch):**

```asm
; a=i16 in d0, b=i16 in d1, max in d2, ret in d0
add.w   d1,d0          ; s=a+b
cmp.w   d2,d0          ; s ? max
bls.s   .ret_s         ; if s<=max -> s
move.w  d2,d0          ; else -> max
.ret_s:
rts
```

### 27.16 Example: Volatile Write Sequence

**Source:** `COLOR00 := 0x0F0`

```asm
move.w  #$0F0,$DFF180    ; marked volatile; scheduler will not reorder across this
```

### 27.17 Example: Blitter Job Fence (Auto)

**Source:**

```novus
blt.job { op=Copy(src), target=dst, size=pixels(32,32), fence=Auto }
```

**Lowering:**

```asm
; ensure previous blit complete
btst    #6,$DFF002       ; DMACONR busy bit
bne.s   *-4              ; wait loop
; set BLTCONx/BLTSIZE/etc. and kick blit
```

### 27.18 Diagnostics from Codegen

* Illegal op for profile → hard error with suggestion (`--cpu 68020` or add `@cpu(min=020)`).
* Misaligned stack/local → compile-time fixup with warning.
* Tail-call eligibility notes for hot paths.

### 27.19 Emission Format

* `vasm` syntax; sections: `.text`, `.data`, `.bss`, `.novus.resident`, `.novus.vectors`.
* Labels stable and human-readable (`func$L1`).
* Source line comments emitted with `; #line` for `novusc inspect`.

---

## 28. Assembly Integration (External Assembly Files)

> **Purpose:** Enable developers to write performance-critical or hardware-specific code in 68k assembly while maintaining seamless interop with Novus code. For v1.0, we support **external assembly files only** (not inline assembly), following the proven C/VBCC pattern familiar to Amiga developers.

### 28.1 Design Decision: External Assembly Only (v1.0)

After analyzing real-world Amiga development patterns, we've chosen to support **external `.s` assembly files** rather than inline assembly for v1.0:

**Rationale:**
* **80% of real Amiga assembly is in separate files** — large optimized routines, copper/blitter code, hardware initialization
* **Small assembly snippets are rare** in authentic Amiga development; most cases should use OS library calls
* **Familiar to C/VBCC developers** — same pattern as existing Amiga toolchain
* **Keeps compiler simple** for self-hosting goals
* **Zero parser/codegen complexity** for inline assembly context management
* **Inline assembly can be added later** if proven necessary (v1.5+)

**When to use assembly:**
* Performance-critical inner loops (after profiling shows need)
* Direct hardware manipulation not exposed via std/amiga/raw
* Legacy assembly code integration
* Specialized algorithms (fixed-point math kernels, decompression, crypto)

**When NOT to use assembly:**
* Simple hardware access → use `std/amiga/raw` abstractions
* Copper/Blitter operations → use hardware DSLs (§23)
* Memory/task/signal management → use `std/exec` wrappers

### 28.2 Calling Convention (Novus ↔ Assembly ABI)

Novus follows the standard **Amiga ABI** for interop with assembly (same as C/VBCC):

#### 28.2.1 Function Calls

**Arguments (left-to-right):**
* First 4 args: `d0`, `d1`, `a0`, `a1` (value parameters)
* Additional args: pushed onto stack (word-aligned)
* 64-bit values: split across register pairs (e.g., `d0:d1`)

**Return Values:**
* Integer/pointer: `d0` (32-bit or smaller)
* 64-bit: `d0` (high) and `d1` (low)
* Structs: returned via pointer passed in `a0` (caller allocates)

**Register Preservation:**
* **Callee-saved (must preserve):** `d2-d7`, `a2-a6`
* **Caller-saved (volatile):** `d0-d1`, `a0-a1`
* **Frame pointer:** `a6` (if used)
* **Stack pointer:** `a7` (sp) must be maintained

**Example:**
```asm
; extern fn fast_multiply(a: i32, b: i32) -> i32
_fast_multiply:
    ; a in d0, b in d1, return in d0
    muls.l  d1,d0           ; d0 = d0 * d1 (68020+)
    rts
```

### 28.3 Declaring External Assembly Functions

Use `extern fn` to declare assembly functions callable from Novus:

```novus
// Declare an assembly function (implemented in math.s)
extern fn fast_multiply(a: i32, b: i32) -> i32

fn calculate() -> i32 {
    let result = fast_multiply(100, 42)  // Calls assembly routine
    return result
}
```

**Rules:**
* `extern fn` declarations have no body
* Function name must match assembly symbol (with leading `_`)
* Parameters and return types must match ABI layout
* Compiler trusts the declaration — **no runtime checks**

### 28.4 Calling Novus from Assembly

Assembly code can call Novus functions using the same ABI:

**Novus side:**
```novus
// This function can be called from assembly
pub fn novus_helper(x: i32, y: i32) -> i32 {
    return x + y * 2
}
```

**Assembly side:**
```asm
; Call Novus function from assembly
    move.l  #100,d0         ; first arg (x)
    move.l  #42,d1          ; second arg (y)
    jsr     _novus_helper   ; call Novus function
    ; result in d0
```

**Symbol visibility:**
* `pub fn` → exported symbol (`.xdef _function_name`)
* Private functions → internal linkage only
* Use `pub` when assembly needs to call Novus code

### 28.5 Build System Integration

Specify assembly files in `novus.toml`:

```toml
[package]
name = "my_game"
version = "1.0.0"

[build]
asm_files = [
    "src/fast_blit.s",
    "src/copper_routines.s",
    "src/audio_mixing.s"
]

# Optional: per-file CPU profile
[[build.asm]]
file = "src/fast_blit.s"
cpu = "68020"              # requires 020+ instructions

[[build.asm]]
file = "src/copper_routines.s"
cpu = "68020"              # minimum supported CPU
```

**Build process:**
1. Compile Novus sources → `.o` files
2. Assemble `.s` files via `vasm` → `.o` files
3. Link all `.o` files via `vlink` → executable

**Compiler flags forwarded to vasm:**
* `--cpu 68020` → `-m68020`
* `--opt-level release` → optimization flags
* Debug symbols maintained for `novusc inspect`

### 28.6 Example: Fast Fixed-Point Math

**Assembly implementation (math.s):**
```asm
; Fast 16.16 fixed-point multiply
; extern fn fixed_mul(a: i32, b: i32) -> i32
;
; a in d0, b in d1, return in d0
        .section .text
        .xdef   _fixed_mul

_fixed_mul:
        muls.l  d1,d0           ; d0 = a * b (32×32 → 32, 68020+)
        asr.l   #16,d0          ; shift right 16 bits
        rts

; Fast 16.16 divide
; extern fn fixed_div(a: i32, b: i32) -> i32
        .xdef   _fixed_div

_fixed_div:
        asl.l   #16,d0          ; shift a left 16 bits
        divs.l  d1,d0           ; d0 = d0 / d1 (68020+)
        rts
```

**Novus usage (game.novus):**
```novus
// Declare assembly routines
extern fn fixed_mul(a: i32, b: i32) -> i32
extern fn fixed_div(a: i32, b: i32) -> i32

fn update_position(pos: i32, velocity: i32, dt: i32) -> i32 {
    // Use fast assembly fixed-point math
    let delta = fixed_mul(velocity, dt)
    return pos + delta
}

fn calculate_ratio(numerator: i32, denominator: i32) -> i32 {
    return fixed_div(numerator, denominator)
}
```

### 28.7 Example: Calling Novus from Assembly

**Novus helper (graphics.novus):**
```novus
pub fn plot_pixel(bitmap: *u8, x: u16, y: u16, color: u8) {
    let offset = (y as u32) * 320 + (x as u32)
    unsafe {
        bitmap[offset] = color
    }
}
```

**Assembly routine (draw.s):**
```asm
; Fast horizontal line using Novus helper
; void draw_hline(u8 *bitmap, u16 y, u16 x1, u16 x2, u8 color)
        .section .text
        .xdef   _draw_hline
        .xref   _plot_pixel

_draw_hline:
        movem.l d2-d4/a2,-(sp)  ; save registers
        move.l  4+16(sp),a2     ; bitmap ptr
        move.w  8+16(sp),d2     ; y
        move.w  10+16(sp),d3    ; x1
        move.w  12+16(sp),d4    ; x2
        move.b  14+16(sp),d1    ; color (extend to word)

.loop:
        cmp.w   d4,d3           ; x1 > x2?
        bgt.s   .done

        ; Call plot_pixel(bitmap, x1, y, color)
        move.l  a2,a0           ; arg0: bitmap
        move.w  d3,d0           ; arg1: x
        move.w  d2,d1           ; arg2: y
        ; color already in low byte
        jsr     _plot_pixel

        addq.w  #1,d3           ; x1++
        bra.s   .loop

.done:
        movem.l (sp)+,d2-d4/a2
        rts
```

### 28.8 Safety and Best Practices

**Memory Safety:**
* Assembly bypasses Novus safety checks — **caller must ensure validity**
* Pass slice lengths separately; assembly cannot know Novus slice bounds
* Use `unsafe` blocks when calling assembly that manipulates memory

**Example:**
```novus
extern fn memset_fast(ptr: *u8, value: u8, count: u32)

fn clear_buffer(buf: []mut u8) {
    unsafe {
        // Explicitly pass pointer and length
        memset_fast(buf.as_ptr(), 0, buf.len() as u32)
    }
}
```

**Register Preservation:**
* Assembly **must** preserve `d2-d7`, `a2-a6` if used
* Failure to preserve causes subtle bugs in caller
* Use `movem.l` to save/restore efficiently

**Stack Alignment:**
* Keep stack word-aligned (even addresses)
* Use `link`/`unlk` for local variables
* Don't corrupt caller's stack frame

**CPU Profile Awareness:**
* Mark assembly files with minimum CPU requirement
* Use `@cpu(min=68020)` attribute or `cpu = "68020"` in novus.toml
* Compiler refuses to link incompatible profiles

### 28.9 Struct Passing and Layout

**By-value structs** (small, ≤8 bytes):
* Passed in registers if possible
* Larger → passed by pointer

**By-pointer:**
```novus
struct Point { x: i16, y: i16 }

extern fn transform_point(p: *Point)

fn move_point(p: Point) {
    transform_point(&p)  // Pass pointer to stack copy
}
```

**Assembly side:**
```asm
_transform_point:
        move.l  a0,a1           ; point ptr in a0
        move.w  (a1),d0         ; load x
        add.w   #10,d0          ; x += 10
        move.w  d0,(a1)         ; store x
        rts
```

**Packed structs:**
* Use `@packed` to match assembly expectations
* Default: natural alignment (word-aligned)

### 28.10 Debugging Assembly Integration

**Symbol visibility:**
```bash
novusc inspect my_game --symbols
```
Shows all Novus and assembly symbols with addresses.

**Disassembly:**
```bash
m68k-amigaos-objdump -d my_game
```
View final linked assembly with Novus/assembly interleaved.

**Link map:**
```bash
novusc build --emit-map
```
Generates `.map` file showing symbol addresses and sections.

### 28.11 Future: Inline Assembly (v1.5+)

If usage patterns demonstrate need, we may add inline assembly:

```novus
// FUTURE (not in v1.0)
fn copper_wait(line: u16) {
    unsafe asm {
        "move.w {line},d0",
        "or.w #$8001,d0",
        "move.l d0,$dff088",
        line = in(reg) line,
    }
}
```

This requires:
* Parser extensions for `asm {}` blocks
* Register constraint system
* Value capture from Novus scope
* Volatile/clobber semantics

**Defer until proven necessary** — external assembly covers 95% of real needs.

### 28.12 Templates and Examples

The Novus SDK provides:
* `templates/assembly/math.s` — fixed-point math routines
* `templates/assembly/copper.s` — copper list manipulation
* `templates/assembly/blitter.s` — blitter job setup
* `examples/asm_interop/` — complete Novus+assembly projects

### 28.13 Summary

**v1.0 Assembly Integration:**
* ✅ External `.s` files linked via build system
* ✅ Bidirectional interop (Novus ↔ Assembly)
* ✅ Standard Amiga ABI (same as C/VBCC)
* ✅ CPU profile awareness
* ✅ Symbol inspection and debugging
* ❌ Inline assembly (deferred to v1.5+)

**Philosophy:**
* Make assembly integration **easy but explicit**
* Leverage familiar Amiga development patterns
* Keep 95% of code in safe, readable Novus
* Use assembly only when justified by profiling or hardware requirements

---

## 29. Floating‑Point & Numeric Modes

> **Purpose:** Provide portable and fast floating‑point across 68020→68080 by defaulting to **soft‑float** semantics and optionally using hardware FPUs when available. Maintain deterministic behavior across profiles unless the developer opts into fast approximations.

### 29.1 Build/Profile Flags

```bash
--fpu none|68881|68040|68060|auto
```

* **none**: pure soft‑float (no FPU opcodes emitted).
* **68881/2**: classic 80‑bit FPU (fp0–fp7).
* **68040**: reduced FPU (no transcendental in hardware).
* **68060**: stricter; many FP ops trap without emulation.
* **auto**: runtime probe + multiversion for hot paths.

### 29.2 Types & Semantics

* `f32`, `f64` = IEEE‑754 (round‑to‑nearest, ties‑to‑even).
* Optional `f80` only when `--fpu >= 68881` (not portable otherwise).
* `fixed16`, `fixed32` provided as first‑class fixed‑point types for realtime work.

### 29.3 ABI Rules

* **FFI ABI**: always soft‑float (floats passed/returned in integer regs per Amiga ABI).
* **Internal ABI**: with hard‑float, the compiler may keep values in fp regs *within a module*; never crosses FFI boundaries.
* Returns: `d0/d1` (soft) or `fp0` (hard, internal‑only).

### 29.4 Lowering Strategy

* **Soft‑float**: `+ − × ÷ cmp sqrt` inline helpers; transcendentals via polynomial/LUT kernels.
* **Hard‑float**: map to `fadd/fsub/fmul/fdiv/fcmp/fsqrt …` with fp0–fp7 for supported FPUs.
* **040/060 safety**: avoid trappy opcodes unless gated with `@fpu(68040|68060)`; otherwise call helpers.

### 29.5 Multiversion & Dispatch

* `--fpu auto` emits soft + hard variants for selected hot functions.
* Startup probe reads ExecBase flags and dispatches to the best variant (one indirect branch).

### 29.6 Strict vs Fast Math

* Namespaces: `math.ieee.*` (strict) and `math.fast.*` (approximate, documented ulp).
* Attributes: `@precise_math` (default) and `@fast_math` (permits reassociation/approximations).

### 29.7 Ergonomics

* Literals inherit their type from context; an otherwise unconstrained float defaults to `f32`.
* Explicit casts use the normal form `(f32)value`, `(f64)value`, or `(fixed16)value`.
* Fixed/float interop helpers: `fx.to_f32()`, `f32.to_fixed16()`.

### 29.8 Diagnostics

* Emitting unsupported/trappy FP opcode for selected `--fpu` is a **hard error** with guidance.
* Inline `asm` using FP ops requires `@fpu(min=68881)`.

### 29.9 Interop with Amiga Math Libs

* `std/math` may call `mathieeesingbas`/`mathieeedoubbas` when available, else use internal kernels — transparent to user code.

### 29.10 Guidance: Float vs Fixed

* Prefer **fixed‑point** for inner loops and games on 000–030.
* Use **float** for tools, offline prep, or accelerated machines; remains portable via soft‑float.

---
