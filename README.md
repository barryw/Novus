# Novus

[![Build Status](https://ci.barrywalker.io/api/badges/5/status.svg)](https://ci.barrywalker.io/repos/5)

**A modern systems programming language for the Amiga**

> *"New code for classic machines"*

---

## What is Novus?

Novus is a compiled systems language designed specifically for the Amiga 68k platform. It combines modern language ergonomics — Result types, pattern matching, safe memory management — with direct hardware access and the efficiency that 68k systems demand.

```novus
from std::ui::screen import ScreenHandle
from std::ffi::graphics import SetAPen, RectFill

pub fn main() -> i32 {
    let result = ScreenHandle::lores("Demo", 5)

    match result {
        Result::Ok(screen) => {
            let rp = screen.rastport()
            SetAPen(rp, 2)
            RectFill(rp, 10, 20, 100, 80)
            return 0
        },
        Result::Err(_) => {
            return 1
        }
    }
}
```

This compiles to clean 68k assembly that runs natively on any Amiga.

## Features

### Implemented

- **Modern syntax** with type inference and no semicolons
- **Result & Option types** for safe error handling
- **Pattern matching** with exhaustiveness checking
- **Generics** with monomorphization
- **RAII** with automatic resource cleanup via `Drop` trait
- **Async/await** stackless coroutines using Exec signals
- **AmigaOS integration** — screens, windows, graphics, audio, networking
- **68020/030/040/060** CPU targeting with optimized output
- **OCS/ECS/AGA** chipset-aware compilation
- **Copper & Blitter DSLs** with compile-time validation

### Standard Library Highlights

- `std::ui::screen` / `std::ui::window` — Safe screen and window management
- `std::collections` — Vec, HashMap, HashSet, VecDeque, and more
- `std::strings` — UTF-8 aware string handling
- `std::io` — File I/O with RAII handles
- `std::net` — TCP/UDP networking via bsdsocket.library
- `std::audio` — Paula audio and ProTracker module playback
- `std::ffi` — Complete AmigaOS FFI bindings (Exec, DOS, Intuition, Graphics...)

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- VBCC toolchain (for assembly and linking)

### Build & Run

```bash
# Build the compiler
dotnet build

# Compile a Novus program
dotnet run --project Novus -- compile examples/hello.novus -o hello

# Run tests
dotnet test
```

### Example: Hello Amiga

```novus
from std::io::file import println

pub fn main() -> i32 {
    println("Hello, Amiga!")
    return 0
}
```

## Documentation

- **[novuslang.com](https://novuslang.com)** — Official website with guides and examples
- **[Language Design Doc](LanguageDesignDoc.md)** — Complete language specification
- **[Programmer's Guide](guide/)** — Comprehensive reference (PDF)

## Architecture

```
Source (.novus)
    ↓
Lexer/Parser (ANTLR4)
    ↓
Semantic Analysis
    ↓
IR (SSA-style)
    ↓
C Code Generator
    ↓
VBCC (compile + link)
    ↓
Amiga Executable (HUNK format)
```

## Philosophy

1. **Explicit over implicit** — No hidden allocations or magic
2. **Predictable performance** — Deterministic execution, no GC
3. **Respect the machine** — Leverage Amiga hardware, don't hide it
4. **Amiga first** — Not cross-platform; authentic Amiga development

## Status

Novus is in active development. The compiler is self-hosting capable and produces working Amiga executables tested on real hardware (A4000/040).

## License

TBD

---

*Built with love for the Amiga community*
