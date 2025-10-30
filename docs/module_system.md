# Novus Module System

## Overview

The Novus module system provides clear, hierarchical organization for code with explicit visibility control. It's designed to be simple and intuitive, drawing inspiration from Rust's module system while maintaining an Amiga-appropriate terminology.

## Composition Units

```
Package (novus.toml)
  └─ Target(s) (one or more build artifacts)
      └─ Modules (one .novus file = one module)
          └─ Items (functions, types, constants, statics, impls)
```

### Package

A **package** is a project defined by `novus.toml`. It contains:
- Package metadata (name, version, authors)
- One or more targets
- Dependencies
- Build configuration

### Target

A **target** is a single build artifact. Types:
- `executable` - Workbench/CLI application
- `library` - Shared library (`.library`)
- `device` - Device driver (`.device`)
- `handler` - DOS handler
- `resource` - System resource

Each target has:
- A unique name
- An entry point (main source file)
- Its own module tree
- Optional dependencies on other targets

### Module

A **module** is a single `.novus` source file. The module name is the filename (without extension), and the module path is defined by the directory structure.

**Examples:**
- `src/main.novus` → module `main`
- `src/renderer.novus` → module `renderer`
- `src/graphics/bitmap.novus` → module `graphics::bitmap`
- `src/sound/player.novus` → module `sound::player`

### Items

Items are the declarations within a module:
- Functions (`fn`)
- Structs (`struct`)
- Enums (`enum`)
- Implementations (`impl`)
- Constants (`const`)
- Static variables (`static`)

## Visibility Modifiers

Novus has four visibility levels:

| Keyword | Scope | Accessible From | Use Case |
|---------|-------|----------------|----------|
| *(none)* | File-local | Only this module | Implementation details |
| `internal` | Target-wide | All modules in same target | Shared internals |
| `pub` | Exported | Other targets, other packages | Public API |
| `extern` | Link-time | Resolved by linker | FFI, hardware registers |

### Private (Default)

No visibility keyword means file-local (private to the module):

```novus
// Only visible in this file
const BUFFER_SIZE: i32 = 1024
static frame_count: i32 = 0

fn helper() -> i32 {
    return 42
}
```

### Internal

The `internal` keyword makes items visible to all modules within the same target, but not exported:

```novus
// Visible to all modules in this target, not exported
internal const SHARED_CONSTANT: i32 = 100
internal static render_cache: Mutex<Cache> = Mutex::new(Cache::new())

internal fn shared_helper() -> i32 {
    return 42
}
```

**Use cases:**
- Implementation details shared between modules in a library
- Internal state not part of the public API
- Helper functions used across the target

### Public

The `pub` keyword exports items, making them visible to other targets and packages:

```novus
// Exported, visible to everyone
pub const VERSION: &str = "1.0.0"
pub static SINE_TABLE: [i32; 360] = [/* ... */]

pub fn create_bitmap(w: i32, h: i32) -> Result<Bitmap, Error> {
    // ...
}
```

**Use cases:**
- Public API of libraries
- Exported functions in applications (for debugging/scripting)
- Constants and types meant for external use

### Extern

The `extern` keyword declares items defined elsewhere, resolved at link time:

```novus
// Provided by AmigaOS startup code
extern var SysBase: *ExecBase

// Provided by opened library
extern var GfxBase: *GfxLibrary

// Hardware register at specific address
extern var CUSTOM: *volatile Custom at 0xdff000

// Function provided by assembly file
extern fn asm_memcpy(dest: *u8, src: *u8, len: u32)
```

**Use cases:**
- AmigaOS library bases
- Hardware registers (memory-mapped I/O)
- Functions from other object files
- FFI with C libraries

## Constants vs Static Variables

### Constants (`const`)

Constants are **compile-time values** that are **inlined** wherever used:

```novus
const MAX_SPRITES: i32 = 8
const PI: f32 = 3.14159
pub const VERSION: &str = "1.0.0"
```

**Properties:**
- Must be computable at compile time
- No memory address (inlined at use sites)
- Always immutable
- Can be any type
- Preferred for configuration values

### Static Variables (`static`)

Static variables have a **fixed memory location** and live for the entire program:

```novus
static frame_count: Atomic<i32> = Atomic::new(0)
pub static SINE_TABLE: [i32; 360] = [/* ... */]
internal static render_state: Mutex<State> = Mutex::new(State::new())
```

**Properties:**
- Has a memory address (one instance in .data or .bss)
- Lives for entire program lifetime
- Immutable by default (use `static mut` for mutable)
- Must be initialized with a constant expression
- Type must be `Sync` for safe sharing between tasks

### Mutable Static Variables (`static mut`)

Mutable globals are allowed but require `unsafe` blocks to access:

```novus
static mut debug_buffer: [u8; 1024] = [0; 1024]

fn log_debug(msg: &str) {
    unsafe {
        // Access mutable global
        debug_buffer[0] = msg.as_bytes()[0]
    }
}
```

**WARNING:** Mutable statics are dangerous in multitasking environments. Prefer:
- `Atomic<T>` for simple counters
- `Mutex<T>` for complex data
- `static` (immutable) with interior mutability

## Imports

Use the `from ... import ...` syntax to bring items from other modules:

```novus
// Import specific items
from graphics::bitmap import Bitmap, create_bitmap
from std::collections import Vec

// Import all public items
from sound::player import *

// Import and rename
from graphics::bitmap import Bitmap as Bmp
```

**Import rules:**
- Can only import `pub` items from other targets
- Can import `internal` items from modules in same target
- Can import `pub` and `internal` items from same module (but why?)
- Cannot import private items

## Package Structure

### Single Target Package

Simple application with one executable:

```
myapp/
├── novus.toml
└── src/
    ├── main.novus
    ├── renderer.novus
    └── sound/
        ├── player.novus
        └── mixer.novus
```

**novus.toml:**
```toml
[package]
name = "myapp"
version = "0.1.0"

[[target]]
name = "myapp"
type = "executable"
entry = "src/main.novus"
```

### Multi-Target Package

Library with examples and tests:

```
mylib/
├── novus.toml
├── src/
│   ├── lib.novus
│   ├── bitmap.novus
│   └── text.novus
├── examples/
│   └── demo.novus
└── tests/
    └── test_bitmap.novus
```

**novus.toml:**
```toml
[package]
name = "mylib"
version = "1.0.0"

# Library target
[[target]]
name = "mylib"
type = "library"
entry = "src/lib.novus"

# Example executable
[[target]]
name = "demo"
type = "executable"
entry = "examples/demo.novus"
dependencies = ["mylib"]

# Test executable
[[target]]
name = "test"
type = "executable"
entry = "tests/test_bitmap.novus"
dependencies = ["mylib"]
```

## Target Dependencies

Targets can depend on other targets in the same package:

```toml
[[target]]
name = "mylib"
type = "library"
entry = "src/lib.novus"

[[target]]
name = "demo"
type = "executable"
entry = "examples/demo.novus"
dependencies = ["mylib"]  # Depends on mylib target
```

**Rules:**
- Dependencies must be explicitly declared
- Circular dependencies are not allowed
- Dependent target can only import `pub` items from dependency
- Build order is determined by dependency graph

## Code Generation

### C Backend Visibility

| Novus | C Translation |
|-------|---------------|
| `const` (private) | `static const` |
| `internal const` | `static const` |
| `pub const` | `extern const` (in header) |
| `static` (private) | `static` |
| `internal static` | `static` |
| `pub static` | `extern` (in header) |
| `extern var` | `extern` |

### 68k Assembly Visibility

| Novus | Assembly |
|-------|----------|
| `const` (private) | No `.xdef`, local label |
| `internal const` | No `.xdef`, local label |
| `pub const` | `.xdef _NAME` |
| `static` (private) | No `.xdef` |
| `internal static` | No `.xdef` |
| `pub static` | `.xdef _NAME` |
| `extern var` | `.xref _NAME` |

## Examples

### Library with Internal State

**src/lib.novus:**
```novus
from std::sync import Mutex

// Private constant (file-local)
const BUFFER_SIZE: i32 = 1024

// Internal state (shared across library modules)
internal static cache: Mutex<Cache> = Mutex::new(Cache::new())

// Public API
pub const VERSION: u32 = 1

pub fn initialize() -> Result<(), Error> {
    let mut c = cache.lock()?
    c.init()
    return Ok(())
}
```

**src/renderer.novus:**
```novus
from lib import cache  // Can access internal items

pub fn render() -> Result<(), Error> {
    let c = cache.lock()?
    // Use shared cache
    return Ok(())
}
```

### Application Using Library

**examples/demo.novus:**
```novus
from mylib import initialize, render, VERSION  // Only pub items

fn main() -> i32 {
    println("Using mylib version {}", VERSION)
    initialize().unwrap()
    render().unwrap()
    return 0
}
```

### Hardware Access

**src/hardware.novus:**
```novus
// Hardware registers
extern var CUSTOM: *volatile Custom at 0xdff000

// Wrapper function
pub fn set_color(index: u8, rgb: u16) {
    unsafe {
        (*CUSTOM).color[index] = rgb
    }
}
```

## Best Practices

1. **Default to private** - only make things `pub` when needed for external API
2. **Use `internal` for shared implementation** - better than making everything `pub`
3. **Prefer `const` over `static`** - unless you need a memory address
4. **Wrap `static mut` in types** - use `Atomic<T>` or `Mutex<T>` instead
5. **Document public APIs** - anything `pub` should have doc comments
6. **Keep modules focused** - one module, one responsibility
7. **Use target dependencies** - break large projects into multiple targets

## Future Enhancements

Potential future additions (not yet implemented):

- `pub(super)` - visible to parent module
- `pub(path::to::module)` - visible to specific module
- Inline modules with `mod { }` syntax
- Re-exports with `pub use`
- Conditional compilation with `#[cfg()]`
