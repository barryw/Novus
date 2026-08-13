# Building AmigaOS Device Drivers with Novus

This document describes how to create AmigaOS device drivers (.device) using Novus.

## Overview

Device drivers in AmigaOS differ from shared libraries in several key ways:

| Aspect | Libraries | Devices |
|--------|-----------|---------|
| Entry Point | Function vector table | BeginIO/AbortIO |
| Access | Direct function calls | IORequest commands |
| State | Shared global state | Per-unit state |
| Opening | `OpenLibrary()` | `OpenDevice()` with IORequest |
| Closing | `CloseLibrary()` | `CloseDevice()` with IORequest |

## Quick Start

### 1. Create a Device Project

```bash
# Create a new workspace with a device project
novusc new my-driver --type device
cd my-driver

# Or add a device to an existing workspace
novusc new my-driver --type device
```

### 2. Define the Device Structure

```novus
// my-driver/src/main.novus
from amiga::raw::structs import IORequest, Unit
from amiga::raw::consts import IOERR_NOCMD

// The @device attribute marks this struct as the device base.
// The compiler generates all boilerplate: ROMTag, lifecycle, BeginIO/AbortIO
@device(name = "mydriver.device", units = 4)
pub struct MyDriver {
    // Custom device state
    initialized: bool,
    buffer_ptr: *u8,
    buffer_size: u32,
}
```

### 3. Implement Command Handlers

```novus
// Device commands are marked with @devicecmd
impl MyDriver {
    // Standard CMD_RESET handler
    @devicecmd(cmd = "CMD_RESET")
    pub fn cmd_reset(ioReq: *IORequest, base: *MyDriver) -> i8 {
        unsafe {
            (*base).initialized = false
            (*base).buffer_size = 0
        }
        return 0  // Success (IOERR_SUCCESS)
    }

    // Standard CMD_READ handler
    @devicecmd(cmd = "CMD_READ")
    pub fn cmd_read(ioReq: *IORequest, base: *MyDriver) -> i8 {
        // Access IORequest fields:
        //   ioReq.io_Data   - buffer pointer
        //   ioReq.io_Length - requested length
        //   ioReq.io_Actual - set to actual bytes read

        // TODO: Implement read logic
        return (i8)IOERR_NOCMD
    }

    // Standard CMD_WRITE handler
    @devicecmd(cmd = "CMD_WRITE")
    pub fn cmd_write(ioReq: *IORequest, base: *MyDriver) -> i8 {
        // TODO: Implement write logic
        return (i8)IOERR_NOCMD
    }

    // Custom command (command number 9 = CMD_NONSTD)
    @devicecmd(cmd = 9, quick = true)  // quick = can complete without blocking
    pub fn cmd_custom(ioReq: *IORequest, base: *MyDriver) -> i8 {
        // Custom device logic
        return 0
    }
}
```

### 4. Build the Device

```bash
novusc build
```

This generates:
- `mydriver.device` - The device binary
- `mydriver.h` - C header for C clients
- `mydriver.novus` - Novus FFI binding for Novus clients
- `mydriver_wrappers.s` - A6 calling convention wrappers
- `mydriver_lifecycle.c` - Generated lifecycle functions

## @device Attribute

The `@device` attribute accepts these parameters:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | Yes | Device name (must end with `.device`) |
| `units` | int | No | Maximum number of units (default: 1) |

Example:
```novus
@device(name = "serial.device", units = 8)
pub struct SerialDevice { ... }
```

## @devicecmd Attribute

The `@devicecmd` attribute marks functions as command handlers:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `cmd` | string/int | Yes | Command name or number |
| `quick` | bool | No | If true, can complete without blocking |

### Standard Commands

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

Example:
```novus
// Using command name
@devicecmd(cmd = "CMD_READ")
pub fn read_data(...) -> i8 { ... }

// Using command number
@devicecmd(cmd = 9)
pub fn custom_cmd(...) -> i8 { ... }

// Quick command (can complete immediately)
@devicecmd(cmd = 10, quick = true)
pub fn fast_status(...) -> i8 { ... }
```

## Generated Code

### ROMTag

The compiler generates a `struct Resident` (ROMTag) that exec.library uses to find and initialize your device:

```c
struct Resident RomTag = {
    RTC_MATCHWORD,
    &RomTag,
    (APTR)(&RomTag + sizeof(struct Resident)),
    RTF_AUTOINIT,
    VERSION,
    NT_DEVICE,
    0,
    (char*)DevName,
    (char*)DevIdString,
    (APTR)&InitTable
};
```

### Device Base Structure

The compiler generates a device base structure:

```c
struct MyDriverBase {
    struct Device dev;              // Standard Device header
    BPTR dev_SegList;               // Segment list for unloading
    UWORD dev_Patch;                // Patch version (semver)
    struct MyDriverUnit* dev_Units[4];  // Unit pointers
    ULONG dev_TotalCommands;        // Statistics
    ULONG dev_TotalOpens;
    ULONG dev_TotalCloses;
    // Custom fields from @device struct
    bool initialized;
    uint8_t* buffer_ptr;
    uint32_t buffer_size;
};
```

### Unit Structure

Each unit gets its own state:

```c
struct MyDriverUnit {
    struct Unit unit;        // Standard Exec unit header
    ULONG unit_OpenCnt;      // Open count for this unit
    ULONG unit_Flags;        // Unit-specific flags
};
```

### Lifecycle Functions

The compiler generates these lifecycle functions:

- **DevInit** - Called when device loads; initializes base structure
- **DevOpen** - Called when program opens device; creates/gets unit
- **DevClose** - Called when program closes device; decrements counts
- **DevExpunge** - Called when device should unload; frees units
- **DevReserved** - Reserved function (returns 0)
- **DevBeginIO** - Command dispatcher; routes to your handlers
- **DevAbortIO** - Aborts pending I/O requests

### Function Vector Table

Devices have a fixed vector layout:

| Offset | Function |
|--------|----------|
| -6 | Open |
| -12 | Close |
| -18 | Expunge |
| -24 | Reserved |
| -30 | BeginIO |
| -36 | AbortIO |

## Using Your Device

### From Novus

The compiler generates a Novus FFI binding:

```novus
from my_driver import MyDriver, MyDriverError, CMD_CUSTOM

pub fn main() -> i32 {
    // Open device unit 0
    match MyDriver::open(0) {
        Result::Ok(dev) => {
            // Send custom command
            match dev.command(CMD_CUSTOM) {
                Result::Ok(_) => println("Command succeeded"),
                Result::Err(e) => println("Command failed"),
            }
            // Device automatically closed when dev goes out of scope
        },
        Result::Err(e) => {
            println("Failed to open device")
            return 1
        }
    }
    return 0
}
```

### From C

```c
#include <exec/io.h>
#include <exec/devices.h>
#include "mydriver.h"

int main(void) {
    struct MsgPort* port = CreateMsgPort();
    struct IORequest* req = CreateIORequest(port, sizeof(struct IORequest));

    if (OpenDevice("mydriver.device", 0, req, 0) == 0) {
        // Send custom command
        req->io_Command = CMD_MYDRIVER_CUSTOM;
        DoIO(req);

        CloseDevice(req);
    }

    DeleteIORequest(req);
    DeleteMsgPort(port);
    return 0;
}
```

## Installation

Copy your `.device` file to `DEVS:` on your Amiga:

```
copy mydriver.device DEVS:
```

The device will be automatically found by exec.library when programs try to open it.

## Comparison with Libraries

### When to Use Libraries

- You have multiple functions to expose
- Functions are called directly
- No per-instance state needed
- Examples: graphics.library, dos.library

### When to Use Devices

- You're interfacing with hardware
- You need per-unit state
- You use the IORequest/DoIO pattern
- Examples: serial.device, timer.device

## Best Practices

1. **Initialize statistics in DevInit** - Track command counts, opens, closes
2. **Handle unit allocation failure** - Return IOERR_OPENFAIL gracefully
3. **Use quick I/O when possible** - Set `quick = true` for fast commands
4. **Free all resources in DevExpunge** - Don't leak unit structures
5. **Validate unit numbers** - Check against max units in DevOpen
6. **Handle aborted requests** - Implement proper DevAbortIO logic

## Error Codes

Standard IORequest error codes:

| Name | Value | Description |
|------|-------|-------------|
| IOERR_SUCCESS | 0 | Command succeeded |
| IOERR_OPENFAIL | -1 | OpenDevice failed |
| IOERR_ABORTED | -2 | Request was aborted |
| IOERR_NOCMD | -3 | Unknown command |
| IOERR_BADLENGTH | -4 | Invalid length |
| IOERR_BADADDRESS | -5 | Invalid address |
| IOERR_UNITBUSY | -6 | Unit is busy |

## See Also

- [AddingLibrarySupport.md](AddingLibrarySupport.md) - Building shared libraries
- [ImplementingAmigaDevices.md](ImplementingAmigaDevices.md) - Using existing devices
- AmigaOS SDK documentation for exec device interface
