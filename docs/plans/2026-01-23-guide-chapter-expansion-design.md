# Programmer's Guide Chapter Expansion Design

**Date:** 2026-01-23
**Status:** Approved
**Target:** Expand first 10 chapters to commercial-quality reference manual standard

## Goals

- Transform the Novus Programmer's Guide into a 600-700 page reference comparable to:
  - Commodore 64 Programmer's Reference Guide
  - Amiga ROM Kernel Reference Manual
- Prioritize smallest chapters first (most room to grow)
- Full commercial-quality treatment across the board

## Design Decisions

1. **Content approach:** Mixed — snippets for concepts, complete programs at chapter end
2. **"Coming from C" sidebars:** Expanded significantly with side-by-side C/Novus for every major concept
3. **Showcase programs:** 300-500+ lines, impressive demos that show off Novus capabilities
4. **Chapter order:** Smallest first — Introduction, Getting Started, Attributes, Error Handling

---

## Chapter 1: Introduction (121 → ~400 lines)

### Expansion Plan

**§1.1 What is Novus? (expand)**
- Add concrete code comparison: same task in C vs Novus (window opening with error handling)
- Show the generated C output to prove "no magic"
- Add diagram: Novus compilation pipeline (source → IR → C → vasm → vlink → Amiga executable)

**§1.2 Why Novus Exists (expand)**
- Real bug examples from actual Amiga software (anonymized) that Novus prevents
- Memory leak patterns in C vs automatic cleanup in Novus
- Side-by-side: 40-line C with goto cleanup vs 15-line Novus with RAII

**§1.3 Design Philosophy (expand)**
- Each principle gets a concrete example, not just description
- "Explicit over implicit" — show what hidden allocations look like in other languages
- "Predictable performance" — show generated 68k assembly for a simple function

**§1.4 NEW: A Taste of Novus**
- 50-line complete program that opens a window, draws something, waits for close
- Annotated line-by-line explaining Novus idioms
- Equivalent C version in "Coming from C" sidebar (~80 lines to do the same thing)

**§1.5 NEW: What Novus Doesn't Do**
- Honest about limitations: no garbage collection, no exceptions, single-threaded (uses AmigaOS tasks)
- Sets expectations correctly

### Showcase Program

**"Copper Rainbow"** — ~150 lines that creates a full-screen color gradient using the copper. Impressive visually, demonstrates Novus's Amiga-native nature right from page one.

---

## Chapter 2: Getting Started (342 → ~800 lines)

### Expansion Plan

**§2.1 Installation (expand)**
- Platform-specific sections: macOS, Linux, Windows (with WSL notes)
- Verifying the toolchain works: `novus --version`, `vc --version`
- Troubleshooting common installation issues (NDK path, VBCC not found)
- "Coming from C": comparison with setting up VBCC/SAS-C directly

**§2.2 Your First Program (expand)**
- Not just "Hello World" — a proper Amiga program that opens DOS and prints
- Explain every line in depth (what's `fn main() -> i32` really doing?)
- Show the generated C code and explain the mapping
- Run it in emulator with screenshots showing expected output

**§2.3 NEW: Your First GUI Program**
- Open a window with Intuition, print "Hello" in it, wait for close gadget
- Full walkthrough of the build-run-debug cycle
- "Coming from C": show the equivalent C with OpenWindowTags

**§2.4 Project Structure (expand)**
- Full `project.toml` reference with every option explained
- Workspace setup for multi-project builds
- Directory conventions (where stdlib lives, where your code goes)

**§2.5 The Compilation Pipeline (NEW)**
- Diagram: .novus → IR → .c → .s → .o → executable
- `--emit-ir` and `--emit-asm` explained with examples
- How to read the generated C when debugging
- How to read the generated assembly for optimization

**§2.6 Development Workflow (expand)**
- Editor setup (VS Code extension mention)
- Using `novus check` for fast feedback
- Using `novus fmt` to keep code clean
- Running tests with `novus test`

**§2.7 Running on Real Hardware (NEW)**
- Copying to CF card / SD card
- Network transfer with `scp` to a networked Amiga
- Using FS-UAE for testing (recommended settings)
- Using WinUAE / Amiberry

### Showcase Program

**"System Info"** — ~350 lines that queries and displays: CPU type, chipset, memory (chip/fast), Kickstart version, mounted volumes, and screen modes. Practical utility that exercises DOS, Exec, and Graphics library calls. Includes proper error handling and clean output formatting.

---

## Chapter 9: Attributes (584 → ~1000 lines)

### Expansion Plan

**§9.1 What Are Attributes? (expand)**
- Compile-time metadata that affects code generation
- Syntax: `@name` vs `#[name(args)]` — when to use which
- "Coming from C": comparison with `__attribute__((...))` and pragmas

**§9.2 Function Attributes (expand)**
- `@inline` / `@noinline` — when to use, 68k code size vs speed tradeoffs
- `@export` — keeping symbols for external linking, library creation
- `@cold` / `@hot` — branch prediction hints for 68060
- `@naked` — for hand-written assembly functions
- Each with generated assembly comparison showing the effect

**§9.3 Struct Attributes (expand)**
- `@packed` — when you need it (hardware registers, IFF chunks)
- `@aligned(N)` — DMA requirements, cache line alignment on 68040/060
- `@repr(C)` — guaranteeing C ABI layout for FFI
- Show memory layout diagrams for each

**§9.4 Amiga-Specific Attributes (NEW — major section)**
- `@atomic` — wraps in Forbid/Permit, when to use vs semaphores
- `@interrupt` — interrupt handler requirements, register preservation
- `@chip` — force allocation to chip RAM
- `@fast` — prefer fast RAM allocation
- Each with complete working examples

**§9.5 Testing Attributes (expand)**
- `@test` — full test lifecycle, setup/teardown patterns
- `@test(skip = "reason")` — conditional skipping
- `@test(should_panic)` — testing failure cases
- `@benchmark` — microbenchmarking on 68k, dealing with CIA timers
- Writing meaningful benchmarks (warm-up, iteration count)

**§9.6 Derive Attributes (expand)**
- `#[derive(Eq, Hash)]` — what code gets generated
- `#[derive(Clone)]` — deep vs shallow copy implications
- When to derive vs manual implementation
- "Coming from C": this is like code generation but type-safe

**§9.7 Build Attributes (expand)**
- `#[stack_size(N)]` — when default 4K isn't enough
- `#[version("1.0.0")]` — embedding version in executable
- `#[resident]` — creating resident modules
- Conditional compilation attributes (future)

**§9.8 Custom Attributes (NEW)**
- Brief note on attribute extensibility design
- How library authors might define domain-specific attributes

### Showcase Program

**"Interrupt-Driven Music Player"** — ~400 lines that:
- Uses `@interrupt` to install a CIA timer interrupt handler
- Uses `@atomic` for safe communication between interrupt and main code
- Uses `@chip` to ensure sample data is in chip RAM
- Plays a simple MOD-style pattern (not full MOD, just demonstrates the concepts)
- Includes proper cleanup on exit

---

## Chapter 10: Error Handling (424 → ~900 lines)

### Expansion Plan

**§10.1 Philosophy: Errors as Values (expand)**
- Deep dive: why not exceptions? (stack unwinding cost on 68k, unpredictable cleanup)
- Why not errno? (global state, easy to ignore, thread-unsafe)
- "Coming from C": the IoErr() pattern and its problems
- Diagram: control flow with Result vs exceptions

**§10.2 The Result Type (expand)**
- Full method reference: `is_ok()`, `is_err()`, `unwrap()`, `unwrap_or()`, `expect()`, `ok()`, `err()`
- When each is appropriate
- "Coming from C": mapping to traditional return code patterns

**§10.3 The Option Type (expand)**
- When to use Option vs Result (absence vs failure)
- Full method reference with examples
- Combining Option and Result (`ok_or()`, `transpose()`)
- "Coming from C": this is your null check, but the compiler enforces it

**§10.4 The ? Operator In Depth (NEW)**
- Desugaring: what ? actually compiles to
- Chaining multiple ? calls
- Using ? in main() — return codes to AmigaOS
- Limitations: can only use in functions returning Result
- Generated code comparison showing zero overhead

**§10.5 Standard Error Types (expand)**
- **DosError**: every variant explained with IoErr() code mapping
- **ExecError**: memory, signals, tasks, libraries
- **IntuitionError**: windows, screens, gadgets
- **GraphicsError**: bitmaps, sprites, fonts
- **NovusError**: the unified wrapper
- When to use specific vs unified errors
- Table: IoErr() code → DosError variant (complete mapping)

**§10.6 Error Conversion and Propagation (NEW)**
- The `From` trait for automatic error conversion
- Building error hierarchies
- Converting between error types with match
- Wrapping third-party errors

**§10.7 Creating Custom Error Types (expand)**
- Domain-specific error enums
- Implementing `message()` for human-readable output
- Error context: adding "what were we trying to do?"
- Composing errors from multiple sources

**§10.8 Error Handling Patterns (NEW — major section)**
- **Early return pattern**: validate inputs, fail fast
- **Fallback chains**: try A, else try B, else default
- **Cleanup on error**: combining defer with Result
- **Logging errors**: capturing error context before handling
- **Retry pattern**: transient failures (disk not ready, etc.)
- Each pattern with complete code example

**§10.9 Panic: When Errors Are Bugs (NEW)**
- `panic!()` vs returning errors
- When panic is appropriate (invariant violations, impossible states)
- What happens on panic (on Amiga: Alert? Graceful exit?)
- `assert!()` and debug-only checks
- "Coming from C": this is like assert() but always enabled in debug

**§10.10 Real-World Error Handling (NEW)**
- Case study: file loader with full error handling
- Every failure mode covered: file not found, read error, parse error, out of memory
- How errors propagate up the call stack
- User-facing error messages

### Showcase Program

**"Resilient File Copier"** — ~450 lines that:
- Copies files/directories with full error recovery
- Handles: disk full (prompt for new disk), read errors (retry N times), write protected (skip or abort)
- Uses all standard error types (DosError for files, ExecError for memory)
- Shows progress with percentage
- Demonstrates comprehensive error handling in a real utility
- "Coming from C" sidebar shows equivalent C would be 800+ lines with gotos

---

## Implementation Approach

### Writing Order
1. Chapter 1 (Introduction) — Sets the tone, relatively standalone
2. Chapter 10 (Error Handling) — Needed for showcase programs in other chapters
3. Chapter 2 (Getting Started) — References error handling patterns
4. Chapter 9 (Attributes) — Most complex showcase program, do last

### Per-Chapter Process
1. Expand existing sections with deeper explanations
2. Add new sections as outlined
3. Create "Coming from C" sidebars with side-by-side code
4. Write the showcase program (as a working .novus file first, then embed in LaTeX)
5. Add diagrams where specified (compilation pipeline, memory layouts)
6. Review for consistency with other chapters

### Showcase Programs
- Each will be written as a standalone `.novus` file in `Novus.Tests/Examples/`
- Compiled and tested on emulator before embedding in documentation
- This ensures every code sample in the guide actually works

### Estimated Effort
- Chapter 1: ~2-3 hours (smaller expansion, simpler showcase)
- Chapter 10: ~4-5 hours (many patterns, comprehensive showcase)
- Chapter 2: ~4-5 hours (multiple complete programs, tooling depth)
- Chapter 9: ~5-6 hours (Amiga-specific depth, complex showcase with interrupts)

---

## Future Work

After these four chapters are complete:
- Expand medium chapters (3, 6, 7, 8) with same treatment
- Add Part II: Standard Library (planned chapters 12-15)
- Add Part III: Advanced Topics (planned chapters 16-19)
- Target: 600-700 pages total
