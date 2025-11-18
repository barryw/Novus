# Implementing Amiga Devices in Novus

This document describes the pattern for wrapping AmigaOS devices (like `timer.device`, `input.device`, `console.device`) in Novus with a safe, ergonomic API.

## Device vs Library Pattern

AmigaOS devices differ from libraries in how they're opened and used:

| Aspect | Libraries | Devices |
|--------|-----------|---------|
| Opening | `OpenLibrary()` returns shared base | `OpenDevice()` per-instance with IORequest |
| Closing | `CloseLibrary()` on base | `CloseDevice()` on IORequest |
| Usage | Call functions via base pointer | Send IORequests with `DoIO()`/`SendIO()` |
| State | Shared global state | Per-instance state (port, request) |

## Implementation Steps

### 1. Add Device Constants

Add command constants to `Novus/std/ffi/amiga_consts.novus`:

```novus
// Timer device commands
pub const TR_ADDREQUEST: u16 = 9
pub const TR_GETSYSTIME: u16 = 11

// Timer device units
pub const UNIT_MICROHZ: u32 = 0
pub const UNIT_VBLANK: u32 = 1
```

### 2. Define FFI Structs

Ensure the device-specific structs are defined in `Novus/std/ffi/amiga_structs.novus`:

```novus
pub struct timerequest {
    tr_node: IORequest,
    tr_time: timeval,
}

pub struct timeval {
    tv_secs: u32,
    tv_micro: u32,
}
```

### 3. Update Code Generator

Add device headers to `Novus/Codegen/CCodeGenerator.cs` in the `GetAmigaHeaders()` method:

```csharp
// Timer device
sb.AppendLine("#include <devices/timer.h>");
```

Add device-specific typedefs:

```csharp
// Timer device types
sb.AppendLine("typedef struct timerequest timerequest;");
sb.AppendLine("typedef struct timeval timeval;");
```

### 4. Create High-Level Wrapper

Create a new file in `Novus/std/system/` (e.g., `timer.novus`) with:

#### Error Type

```novus
pub enum TimerError {
    OpenFailed,
    NoMemory,
    CommandFailed(i8),
}
```

#### RAII Handle Struct

```novus
pub struct Timer {
    timereq: *timerequest,
    port: *MsgPort,
}
```

The handle stores:
- Pointer to the device-specific request structure
- Pointer to the message port for replies

#### Constructor Methods

```novus
impl Timer {
    /// Open with specific unit
    fn open(unit: u32) -> Result<Timer, TimerError> {
        // 1. Create message port
        let port = CreateMsgPort()
        if (u32)port == 0 {
            return Result::Err(TimerError::NoMemory)
        }

        // 2. Create IORequest (size must match device request struct)
        let req = CreateIORequest(port, @sizeof(timerequest))
        if (u32)req == 0 {
            DeleteMsgPort(port)
            return Result::Err(TimerError::NoMemory)
        }

        let timereq = (*timerequest)req

        // 3. Open device
        unsafe {
            let ioreq_ptr = &timereq.tr_node
            let ioreq = (*IORequest)ioreq_ptr
            let error = OpenDevice("timer.device".as_cstr(), unit, ioreq, 0)
            if error != 0 {
                DeleteIORequest(req)
                DeleteMsgPort(port)
                return Result::Err(TimerError::OpenFailed)
            }
        }

        return Result::Ok(Timer { timereq: timereq, port: port })
    }

    /// Convenience constructors for common units
    pub fn microhz() -> Result<Timer, TimerError> {
        return Timer::open(UNIT_MICROHZ)
    }
}
```

#### Device Operations

```novus
impl Timer {
    pub fn get_time(&self) -> Result<Duration, TimerError> {
        // Set command
        self.timereq.tr_node.io_Command = TR_GETSYSTIME

        // Execute synchronously
        let ioreq_ptr = &self.timereq.tr_node
        let ioreq = (*IORequest)ioreq_ptr
        let error = DoIO(ioreq)

        if error != 0 {
            return Result::Err(TimerError::CommandFailed(error))
        }

        // Extract result from request struct
        return Result::Ok(Duration::from_timeval(self.timereq.tr_time))
    }
}
```

#### Drop Implementation (RAII Cleanup)

```novus
impl Drop for Timer {
    fn drop(&mut self) {
        if let req = self.timereq {
            // Close device first
            unsafe {
                let ioreq_ptr = &req.tr_node
                let ioreq = (*IORequest)ioreq_ptr
                CloseDevice(ioreq)
            }

            // Delete IORequest
            DeleteIORequest((*u8)req)
            self.timereq = (*timerequest)0
        }

        if let port = self.port {
            DeleteMsgPort(port)
            self.port = (*MsgPort)0
        }
    }
}
```

### 5. Add Convenience Functions

Provide static functions for one-off operations:

```novus
/// Simple delay without managing a timer handle
pub fn delay(duration: Duration) -> Result<(), TimerError> {
    let timer = Timer::microhz()?
    return timer.delay(duration)
}

/// Get current system time
pub fn now() -> Result<Duration, TimerError> {
    let timer = Timer::microhz()?
    return timer.get_time()
}
```

## Key Patterns

### Pointer Casting for IORequest

Device requests embed `IORequest` as their first field. To call `DoIO()`, you need to cast:

```novus
let ioreq_ptr = &self.timereq.tr_node
let ioreq = (*IORequest)ioreq_ptr
let error = DoIO(ioreq)
```

### User-Friendly Types

Wrap low-level structures in user-friendly types:

```novus
pub struct Duration {
    secs: u32,
    micros: u32,
}

impl Duration {
    pub fn millis(ms: u32) -> Duration { ... }
    fn to_timeval(&self) -> timeval { ... }
    fn from_timeval(tv: timeval) -> Duration { ... }
}
```

### Result-Based Error Handling

Always return `Result` types:

```novus
pub fn get_time(&self) -> Result<Duration, TimerError>
pub fn delay(&self, duration: Duration) -> Result<(), TimerError>
```

### Resource Cleanup Order

In `Drop`, clean up in reverse order of acquisition:
1. Close device
2. Delete IORequest
3. Delete message port

## Testing

Create a test file in `Novus.Tests/Examples/`:

```novus
from std::system::timer import Timer, Duration, delay, now
from std::strings::format import Formatter, Display
from std::io::core import write

pub fn main() -> i32 {
    // Test getting time
    match now() {
        Result::Ok(time) => {
            write(f"Time: {time.get_secs()} seconds\n")
        },
        Result::Err(_) => {
            write("ERROR: Failed to get time\n")
            return 20
        }
    }

    // Test delay
    match delay(Duration::millis(500)) {
        Result::Ok(_) => write("Delay complete\n"),
        Result::Err(_) => return 20
    }

    return 0
}
```

Compile and copy to the Amiga shared drive for testing:

```bash
./Novus/bin/Release/net9.0/Novus compile Novus.Tests/Examples/timer_test.novus -o /tmp/timer_test
cp /tmp/timer_test /Users/barry/Emulation/Amiga/A4000-DH0/Barry/
```

## Devices to Implement

Using this pattern, the following devices can be wrapped:

- `timer.device` - Timing and delays (implemented)
- `input.device` - Input events
- `console.device` - Console I/O
- `clipboard.device` - Clipboard access
- `trackdisk.device` - Direct disk access
- `audio.device` - Audio output
- `narrator.device` - Speech synthesis
- `gameport.device` - Joystick/mouse input
- `keyboard.device` - Raw keyboard input
- `serial.device` - Serial port
- `parallel.device` - Parallel port

Each follows the same pattern: create port, create request, open device, send commands, close and cleanup.
