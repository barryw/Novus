# Novus Device Template - {{PROJECT_NAME}}

This is a complete workspace template for creating AmigaOS device drivers (.device files) in Novus.

## Workspace Structure

```
{{PROJECT_NAME}}/
├── workspace.toml         # Workspace configuration
├── README.md              # This file
├── device/                # Device driver project
│   ├── project.toml       # Device build configuration
│   ├── README.md          # Device-specific documentation
│   └── src/
│       └── dev.novus      # Device driver source code
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
- `target/debug/devs/{{PROJECT_NAME}}.device` - The device driver binary
- `target/debug/bins/{{PROJECT_NAME}}-example` - The example program

### 2. Install to Amiga

Copy these files to your Amiga:

```bash
# Copy device to DEVS:
cp target/debug/devs/{{PROJECT_NAME}}.device /path/to/amiga/DEVS:/

# Copy example to test
cp target/debug/bins/{{PROJECT_NAME}}-example /path/to/amiga/
```

### 3. Run on Amiga

The device is now in DEVS:, so just run the example:
```bash
./{{PROJECT_NAME}}-example
echo $?  # Should print 0 on success
```

## What Gets Built

All artifacts are built to `target/debug/` (or `target/release/` with `--release`):

### Device Artifacts (`target/debug/devs/`)

| File | Purpose |
|------|---------|
| `{{PROJECT_NAME}}.device` | Device binary (copy to DEVS:) |
| `{{PROJECT_NAME}}.h` | C header with structures and commands |
| `{{PROJECT_NAME}}.novus` | Novus FFI bindings |
| `{{PROJECT_NAME}}_wrappers.s` | A6 calling convention wrappers |

### Example Artifacts (`target/debug/bins/`)

| File | Purpose |
|------|---------|
| `{{PROJECT_NAME}}-example` | Executable that uses the device |

## Device vs Library

Devices differ from libraries in several key ways:

| Aspect | Libraries | Devices |
|--------|-----------|---------|
| Entry Point | Function vector table | BeginIO/AbortIO |
| Access | Direct function calls | IORequest commands |
| State | Shared global state | Per-unit state |
| Opening | `OpenLibrary()` | `OpenDevice()` with IORequest |
| Closing | `CloseLibrary()` | `CloseDevice()` with IORequest |
| Location | LIBS: | DEVS: |

## How It Works

### The @device Attribute

The device uses a single attribute to define itself:

```novus
@device(name = "{{PROJECT_NAME}}.device", units = 4)
pub struct MyDevice {
    // Device state fields
    initialized: bool,
    buffer_ptr: *u8,
}

impl MyDevice {
    @devicecmd(cmd = "CMD_RESET")
    pub fn cmd_reset(ioReq: *IORequest, base: *MyDevice) -> i8 {
        // Handle CMD_RESET
        return 0
    }

    @devicecmd(cmd = 9)  // CMD_NONSTD = 9 (first custom command)
    pub fn cmd_custom(ioReq: *IORequest, base: *MyDevice) -> i8 {
        // Handle custom command
        return 0
    }
}
```

The compiler automatically generates:
- Device base structure with standard Device header
- Unit structure for per-unit state
- ROMTag and initialization code
- DevOpen/DevClose/DevExpunge lifecycle functions
- BeginIO command dispatcher
- AbortIO handler
- A6 wrapper functions for AmigaOS calling convention
- C headers and Novus FFI bindings

### Using the Device

From your program:

```novus
from amiga::raw::exec import OpenDevice, CloseDevice, DoIO
from amiga::raw::exec import CreateMsgPort, DeleteMsgPort
from amiga::raw::exec import CreateIORequest, DeleteIORequest

pub fn main() -> i32 {
    let port = CreateMsgPort()
    let req = CreateIORequest(port, @sizeof(IORequest))

    // Open device unit 0
    let error = OpenDevice("{{PROJECT_NAME}}.device", 0, req, 0)
    if error != 0 {
        return 1
    }

    // Send custom command
    req.io_Command = 9  // CMD_NONSTD
    DoIO(req)

    // Clean up
    CloseDevice(req)
    DeleteIORequest(req)
    DeleteMsgPort(port)
    return 0
}
```

## Standard Commands

| Name | Number | Description |
|------|--------|-------------|
| CMD_INVALID | 0 | Invalid command |
| CMD_RESET | 1 | Reset device to initial state |
| CMD_READ | 2 | Read data from device |
| CMD_WRITE | 3 | Write data to device |
| CMD_UPDATE | 4 | Flush buffers/sync |
| CMD_CLEAR | 5 | Clear buffers |
| CMD_STOP | 6 | Pause device |
| CMD_START | 7 | Resume device |
| CMD_FLUSH | 8 | Flush pending commands |
| CMD_NONSTD | 9 | First device-specific command |

## Customizing

### Add Command Handlers

Edit `device/src/dev.novus` and add `@devicecmd` methods:

```novus
impl MyDevice {
    @devicecmd(cmd = 10, quick = true)  // quick = can complete immediately
    pub fn cmd_status(ioReq: *IORequest, base: *MyDevice) -> i8 {
        // Return device status
        return 0
    }
}
```

### Add Device State

Add fields to the struct:

```novus
@device(name = "{{PROJECT_NAME}}.device", units = 4)
pub struct MyDevice {
    initialized: bool,
    buffer_ptr: *u8,
    buffer_size: u32,    // NEW
    error_count: u32,    // NEW
}
```

### Change Unit Count

Modify the `units` parameter:

```novus
@device(name = "{{PROJECT_NAME}}.device", units = 8)  // Support 8 units
```

## Project-Level Configuration

Each project has its own `project.toml` with settings for:

- **Type**: `device`, `library`, `cli`, or `workbench`
- **Optimization**: Level 0-2
- **CPU Target**: 68020, 68030, 68040, 68060, 68080
- **FPU**: `auto`, `soft`, `68881`, `68882`, `68040`, `68060`

See each project's README for details.

## Learn More

- `device/README.md` - Device project configuration
- `example/README.md` - Example project configuration
- [BuildingAmigaDevices.md](../../docs/BuildingAmigaDevices.md) - Full device documentation

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
- Build everything with one command
- Clear separation of concerns
- Each project independently configurable
- Easy to add more projects (tests, tools, etc.)
