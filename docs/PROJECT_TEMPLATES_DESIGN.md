# Novus Project Templates Design

## Project Types (Packages)

### 1. **CLI Application** (`type = "cli"`)
- **Use case:** Command-line tools, utilities
- **Entry:** Uses VBCC C runtime for argc/argv
- **Output:** Executable binary
- **Template:** Simple main with argument parsing
- **Example:** File converters, compilers, grep-like tools

### 2. **Workbench Application** (`type = "workbench"`)
- **Use case:** GUI applications launched from Workbench
- **Entry:** Handles WBStartup message
- **Output:** Executable binary with icon
- **Template:** WBStartup handler + message reply
- **Example:** Text editors, image viewers, games

### 3. **Dual-Mode Application** (`type = "dual"`)
- **Use case:** Apps that work from both CLI and Workbench
- **Entry:** Detects launch mode and handles both
- **Output:** Executable binary with icon
- **Template:** Mode detection + dual handlers
- **Example:** Professional apps like DPaint, Directory Opus

### 4. **Shared Library** (`type = "library"`)
- **Use case:** Reusable code libraries
- **Entry:** Library initialization function
- **Output:** .library file
- **Template:** Library base, function vectors, init/expunge
- **Example:** Custom libraries, API wrappers

### 5. **Device Driver** (`type = "device"`)
- **Use case:** Hardware device drivers
- **Entry:** Device open/close/ioctl
- **Output:** .device file
- **Template:** Device structure, I/O command handlers
- **Example:** Printer drivers, custom hardware interfaces

### 6. **Handler** (`type = "handler"`)
- **Use case:** Filesystem handlers, virtual devices
- **Entry:** DOS packet handler
- **Output:** Handler executable
- **Template:** Packet processing loop
- **Example:** Network filesystems, virtual drives

### 7. **Resource** (`type = "resource"`)
- **Use case:** System resources (rare)
- **Entry:** Resource initialization
- **Output:** .resource file
- **Template:** Resource structure
- **Example:** Low-level system resources

---

## Project Structure

### Standard Layout

```
my-project/
├── novus.toml          # Project configuration
├── src/                # Source code
│   ├── main.novus      # Entry point (for executables)
│   └── lib.novus       # Library entry (for libraries)
├── tests/              # Test files (optional)
├── examples/           # Example code (optional)
├── docs/               # Documentation (optional)
├── build/              # Build output (generated)
└── .gitignore          # Git ignore file
```

### For Workbench Apps (additional files)

```
my-project/
├── icons/
│   └── my-project.info # Workbench icon
└── tooltypes.txt       # Default tool types
```

---

## novus.toml Schema Updates

### Add `type` field to `[package]` section:

```toml
[package]
name = "my-app"
version = "0.1.0"
type = "cli"              # NEW: cli, workbench, dual, library, device, handler, resource
description = "My awesome app"
authors = ["Your Name <you@example.com>"]
license = "MIT"
entry = "src/main.novus"  # Entry point (optional, inferred from type)

[build]
target_cpu = "68020"      # 68000, 68020, 68040, 68060
fpu = "auto"              # soft, 68881, 68040, auto
output = "build"
optimization_level = 0    # 0-3
emit_asm = false

[paths]
src = "src"
lib = []

[dependencies]
# Future: package dependencies
```

---

## Template Contents

### 1. CLI Application Template

**novus.toml:**
```toml
[package]
name = "{{PROJECT_NAME}}"
version = "0.1.0"
type = "cli"
description = "A CLI application"
authors = ["{{AUTHOR}}"]

[build]
target_cpu = "68020"
fpu = "auto"
```

**src/main.novus:**
```novus
// {{PROJECT_NAME}} - A command-line application for AmigaOS
//
// This template uses the VBCC C runtime which provides argc/argv
// just like standard C programs.

from std::io import println

pub fn main() -> i32 {
    println("Hello from {{PROJECT_NAME}}!")

    // TODO: Parse command-line arguments
    // TODO: Implement your CLI logic here

    return 0
}
```

---

### 2. Workbench Application Template

**novus.toml:**
```toml
[package]
name = "{{PROJECT_NAME}}"
version = "0.1.0"
type = "workbench"
description = "A Workbench application"
authors = ["{{AUTHOR}}"]

[build]
target_cpu = "68020"
fpu = "auto"
```

**src/main.novus:**
```novus
// {{PROJECT_NAME}} - A Workbench GUI application for AmigaOS
//
// This template handles WBStartup messages for Workbench launches.

from std::ffi::dos import Input, Output, Write
from std::ffi::exec import ReplyMsg, Forbid
from std::ffi::amiga_structs import WBStartup, WBArg

pub fn main() -> i32 {
    // Check if launched from Workbench or CLI
    let input_fh = Input()

    if input_fh == 0 {
        // Launched from Workbench
        return handle_workbench()
    } else {
        // Launched from CLI (fallback)
        return handle_cli()
    }
}

fn handle_workbench() -> i32 {
    // TODO: Get WBStartup message from process message port
    // TODO: Process files from sm_ArgList
    // TODO: Do your Workbench app logic

    // IMPORTANT: Must reply to WBStartup message!
    // Forbid()
    // ReplyMsg(wbmsg)

    return 0
}

fn handle_cli() -> i32 {
    let stdout = Output()
    let msg: String = "{{PROJECT_NAME}}: This is a Workbench application.\n"
    Write(stdout, (i32)(msg.ptr), msg.len)
    return 0
}
```

---

### 3. Dual-Mode Application Template

**src/main.novus:**
```novus
// {{PROJECT_NAME}} - Dual-mode application (CLI + Workbench)

from std::ffi::dos import Input
from std::args import parse_args

pub fn main() -> i32 {
    let input_fh = Input()

    if input_fh == 0 {
        // Workbench launch
        return run_workbench()
    } else {
        // CLI launch
        return run_cli()
    }
}

fn run_cli() -> i32 {
    // Parse command-line arguments using ReadArgs
    let args = parse_args("FILES/M") or {
        return 1
    }

    // TODO: CLI logic

    return 0
}

fn run_workbench() -> i32 {
    // TODO: Handle WBStartup

    return 0
}
```

---

### 4. Shared Library Template

**novus.toml:**
```toml
[package]
name = "{{PROJECT_NAME}}"
version = "0.1.0"
type = "library"
description = "A shared library"
authors = ["{{AUTHOR}}"]

[build]
target_cpu = "68020"
fpu = "auto"
```

**src/lib.novus:**
```novus
// {{PROJECT_NAME}}.library - AmigaOS shared library
//
// This template provides the basic structure for an AmigaOS library.
// Libraries use function vectors and require special initialization.

// Library version
pub const VERSION: i32 = 1
pub const REVISION: i32 = 0

// Library initialization
// Called when library is first loaded
pub fn lib_init() -> i32 {
    // TODO: Initialize library resources
    return 0
}

// Library cleanup
// Called when library is expunged
pub fn lib_expunge() -> i32 {
    // TODO: Clean up library resources
    return 0
}

// Library open
// Called when a program opens the library
pub fn lib_open() -> i32 {
    // TODO: Increment open count
    return 0
}

// Library close
// Called when a program closes the library
pub fn lib_close() -> i32 {
    // TODO: Decrement open count
    return 0
}

// Example library function
pub fn hello() -> String {
    return "Hello from {{PROJECT_NAME}}.library!"
}
```

---

### 5. Device Driver Template

**novus.toml:**
```toml
[package]
name = "{{PROJECT_NAME}}"
version = "0.1.0"
type = "device"
description = "An AmigaOS device driver"
authors = ["{{AUTHOR}}"]
```

**src/device.novus:**
```novus
// {{PROJECT_NAME}}.device - AmigaOS device driver
//
// Device drivers handle I/O requests through the exec device interface.

pub const VERSION: i32 = 1
pub const REVISION: i32 = 0

// Device initialization
pub fn dev_init() -> i32 {
    // TODO: Initialize device hardware
    return 0
}

// Device open
pub fn dev_open() -> i32 {
    // TODO: Open device unit
    return 0
}

// Device close
pub fn dev_close() -> i32 {
    // TODO: Close device unit
    return 0
}

// Begin I/O request
pub fn dev_begin_io(io_request: *i32) -> i32 {
    // TODO: Process I/O command
    return 0
}

// Abort I/O request
pub fn dev_abort_io(io_request: *i32) -> i32 {
    // TODO: Abort pending I/O
    return 0
}
```

---

## Command Syntax

```bash
# Basic usage
novusc new my-app                    # Default: CLI app
novusc new my-app --type cli         # Explicit CLI app
novusc new my-app --type workbench   # Workbench app
novusc new my-app --type dual        # Dual-mode app
novusc new my-app --type library     # Shared library
novusc new my-app --type device      # Device driver

# With options
novusc new my-app --type cli --author "Your Name" --license MIT

# In current directory
novusc new . --type cli
```

---

## Implementation Plan

### Phase 1: Basic Templates (Start Here)
1. ✅ Add `type` field to `PackageSection`
2. ✅ Create `NewCommand` class with CommandLineParser
3. ✅ Implement CLI template scaffolding
4. ✅ Implement Workbench template scaffolding
5. ✅ Create .gitignore template

### Phase 2: Advanced Templates
6. ⬜ Implement Library template
7. ⬜ Implement Device template
8. ⬜ Implement Dual-mode template

### Phase 3: Polish
9. ⬜ Add icon generation for Workbench apps
10. ⬜ Add example tests
11. ⬜ Add interactive mode (ask for options)

---

## File Organization

```
Novus/
├── Templates/
│   ├── cli/
│   │   ├── novus.toml.template
│   │   ├── src/
│   │   │   └── main.novus.template
│   │   └── .gitignore.template
│   ├── workbench/
│   │   ├── novus.toml.template
│   │   └── src/
│   │       └── main.novus.template
│   ├── dual/
│   ├── library/
│   └── device/
└── NewCommand.cs           # Implementation of 'new' command
```

---

## Example Output

```bash
$ novusc new hello-amiga --type cli --author "Barry"

Creating new CLI application: hello-amiga

  ✓ Created directory: hello-amiga/
  ✓ Created novus.toml
  ✓ Created src/main.novus
  ✓ Created .gitignore

Your project is ready!

Next steps:
  cd hello-amiga
  novusc build

Happy coding! 🚀
```

---

## Priority Order for Implementation

1. **CLI template** - Most common, simplest
2. **Workbench template** - Common for GUI apps
3. **Dual-mode template** - Professional apps
4. **Library template** - Code reuse
5. **Device template** - Hardware drivers (rare)
6. **Handler template** - Filesystem drivers (very rare)
7. **Resource template** - System resources (extremely rare)

---

**Next:** Implement `NewCommand.cs` with CLI and Workbench templates!
