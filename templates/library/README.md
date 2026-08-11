# Novus Library Template - {{PROJECT_NAME}}

This is a complete workspace template for creating AmigaOS shared libraries (.library files) in Novus.

## Workspace Structure

```
greeting/
├── workspace.toml         # Workspace configuration
├── README.md              # This file
├── library/               # Library project
│   ├── project.toml       # Library build configuration
│   ├── README.md          # Library-specific documentation
│   └── src/
│       └── lib.novus      # Library source code
└── example/               # Example program project
    ├── project.toml       # Example build configuration
    ├── README.md          # Example-specific documentation
    └── src/
        └── main.novus     # Example program source
```

## Quick Start

### 1. Build Everything

From this directory:
```bash
novusc build
```

This builds both projects to the centralized `target/` directory:
- `target/debug/libs/greeting.library` - The library binary
- `target/debug/bins/greeting-example` - The example program

All build artifacts go into `target/<config>/<type>/` keeping source directories clean.

### 2. Install to Amiga

Copy these two files to your Amiga:

```bash
# Copy library to LIBS:
cp target/debug/libs/greeting.library /path/to/amiga/LIBS:/

# Copy example to test
cp target/debug/bins/greeting-example /path/to/amiga/
```

### 3. Run on Amiga

The library is already in LIBS:, so just run the example:
```bash
./greeting-example
echo $?  # Should print 8
```

### 4. Run

**Novus example:**
```bash
cd Barry
./test-greeting
echo $?  # Should print 8
```

**C example** (demonstrates actual library calls):
```bash
# Build the C example
cd example
vc +aos68k test_greeting.c ../library/greeting.library/greeting_lib.o -o test_greeting_c

# Run it
./test_greeting_c
echo $?  # Should print 8 (result of Add(5,3))
```

## What Gets Built

All artifacts are built to `target/debug/` (or `target/release/` with `--release`):

### Library Artifacts (`target/debug/libs/`)

| File | Purpose |
|------|---------|
| `greeting.library` | Library binary (copy to LIBS:) |
| `greeting.h` | C header with function declarations |
| `greeting_lib.o` | Auto-open/close stub for linking |
| `greeting_lib.fd` | VBCC function description file |
| `greeting.novus` | Novus FFI bindings |

### Example Artifacts (`target/debug/bins/`)

| File | Purpose |
|------|---------|
| `greeting-example` | Executable that uses the library |

## Workspace Configuration

Edit `workspace.toml` to configure the workspace:

```toml
[workspace]
name = "greeting"
description = "A simple 'Hello World' library"
version = "1.0.0"

# Member projects (directory names)
members = ["library", "example"]
```

Projects are built in dependency order - if `example` depends on `library`, then `library` is built first automatically.

## Building Individual Projects

### Build just the library:
```bash
cd library
novusc build
```

### Build just the example:
```bash
cd example
novusc build
```

## Project-Level Configuration

Each project has its own `project.toml` with settings for:

- **Type**: `library`, `cli`, `workbench`, or `device`
- **Optimization**: Level 0-2
- **CPU Target**: 68020, 68030, 68040, 68060, 68080
- **FPU**: `auto`, `soft`, `68881`, `68882`, `68040`, `68060`
- **Features**: Project-specific feature flags

See each project's README for details.

## How It Works

### The @library Attribute

The library uses a single attribute to define itself:

```novus
@library(name = "greeting.library", version = 1, revision = 0)
pub struct GreetingLibrary {
    call_count: u32,
}

impl GreetingLibrary {
    pub fn GetVersion() -> u32 { return 65536 }
    pub fn Add(a: i32, b: i32) -> i32 { return a + b }
    pub fn GetCallCount() -> u32 { return 42 }
}
```

The compiler automatically generates:
- Library base structure with standard Library header
- ROMTag and initialization code
- A6 wrapper functions for AmigaOS calling convention
- C headers, FFI bindings, FD files, and auto-open stubs

### Auto-Open/Close

C programs can link `greeting_lib.o` to get automatic library management:

```c
#include "greeting.h"

int main() {
    // Library already opened by greeting_lib.o!
    ULONG version = GreetingLibrary_GetVersion();
    // Library closes automatically at exit
    return 0;
}
```

Compile with:
```bash
vc +aos68k myprogram.c ../library/greeting.library/greeting_lib.o -o myprogram
```

## Customizing

### Add Library Functions

Edit `library/src/lib.novus` and add methods to the impl block:

```novus
impl GreetingLibrary {
    pub fn Multiply(a: i32, b: i32) -> i32 {
        return a * b
    }
}
```

Rebuild - everything updates automatically!

### Add Library State

Add fields to the struct:

```novus
@library(name = "greeting.library", version = 1, revision = 0)
pub struct GreetingLibrary {
    call_count: u32,
    error_count: u32,     // NEW
    last_caller: *u8,     // NEW
}
```

### Change Settings

Each project's `project.toml` controls:
- Optimization level
- CPU target
- FPU requirements
- Output names
- Entry points

## Additional Examples

The `examples/` directory contains additional usage examples:

- `test_greeting.c` - Complete C example using AmigaOS Write() for output

See `examples/README.md` for build instructions.

## Learn More

- `library/README.md` - Library project configuration
- `example/README.md` - Example project configuration
- `examples/README.md` - Additional example programs

## Template Design

This template follows the Rust/Cargo workspace pattern:

```
Workspace (workspace.toml)
  ├── Project 1 (project.toml)
  │   └── Source files
  └── Project 2 (project.toml)
      └── Source files
```

Benefits:
- ✅ Build everything with one command
- ✅ Clear separation of concerns
- ✅ Each project independently configurable
- ✅ Easy to add more projects (tests, tools, etc.)
