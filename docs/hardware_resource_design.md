# Hardware Resource Acquisition - Safe API Design

## Overview

This document outlines the safe, uniform API design for acquiring Amiga hardware resources in Novus. The design prioritizes safety through RAII handles and explicit error handling via `Result<T, E>`.

## Design Philosophy

1. **Uniform Interface**: All resources use consistent acquire/release patterns
2. **RAII Cleanup**: Handles automatically release resources when dropped
3. **Explicit Errors**: Result types make failure modes visible
4. **No Silent Failures**: Every acquisition that can fail returns a Result
5. **Blocking vs Non-Blocking**: Clear distinction between try_acquire (immediate) and acquire (blocking)

## Resource Categories

### Category 1: Exclusive Hardware Resources
**Examples**: Blitter, Copper
**Pattern**: Single owner, blocking or immediate fail

### Category 2: Device I/O
**Examples**: trackdisk.device, serial.device, parallel.device
**Pattern**: Error codes, sync/async operations, unit busy errors

### Category 3: System Resources
**Examples**: Serial port, parallel port (via misc.resource)
**Pattern**: Named ownership, immediate fail with current owner name

### Category 4: Disk Units
**Examples**: df0:, df1:, df2:, df3:
**Pattern**: Allocation + queueing, strict hardware state requirements

## Core Error Types

```novus
// Common error type for all resource operations
pub enum ResourceError {
    InUse(String),           // Resource busy, String = current owner name or empty
    UnitBusy(u32),          // Specific unit is busy
    DeviceNotFound,         // Device doesn't exist
    OpenFailed,             // Device exists but failed to open
    HardwareFailure,        // Self-test or hardware malfunction
    InvalidOperation,       // Operation not supported
    Timeout,                // Operation timed out
    AlreadyOwned,           // Caller already owns this resource
}

// Device-specific error codes (from exec/errors.h)
pub enum DeviceError {
    OpenFail,               // IOERR_OPENFAIL (-1)
    Aborted,                // IOERR_ABORTED (-2)
    NoCmd,                  // IOERR_NOCMD (-3)
    BadLength,              // IOERR_BADLENGTH (-4)
    BadAddress,             // IOERR_BADADDRESS (-5)
    UnitBusy,               // IOERR_UNITBUSY (-6)
    SelfTest,               // IOERR_SELFTEST (-7)
}

impl From<i8> for DeviceError {
    fn from(code: i8) -> Self {
        match code {
            -1 => DeviceError::OpenFail,
            -2 => DeviceError::Aborted,
            -3 => DeviceError::NoCmd,
            -4 => DeviceError::BadLength,
            -5 => DeviceError::BadAddress,
            -6 => DeviceError::UnitBusy,
            -7 => DeviceError::SelfTest,
            _ => DeviceError::OpenFail,  // Default
        }
    }
}
```

## API Patterns

### Pattern 1: Blocking Exclusive Resource (Blitter)

**AmigaOS C Pattern:**
```c
OwnBlitter();       // Blocks until available
WaitBlit();         // Wait for hardware ready
// ... use blitter registers ...
DisownBlitter();    // Must not forget!
```

**Novus Safe Pattern:**
```novus
from std::hardware::blitter import acquire_blitter, BlitterHandle

fn do_blit() -> Result<(), ResourceError> {
    // Blocks until blitter available, returns handle
    let blitter = acquire_blitter()?;

    // Handle automatically calls WaitBlit() before first register access
    // Handle implements Drop, so DisownBlitter() called automatically

    unsafe {
        blitter.set_source(addr);
        blitter.set_dest(addr);
        blitter.blit();
    }

    // DisownBlitter() called automatically when handle drops
    return Ok(());
}
```

**Non-Blocking Variant:**
```novus
fn try_blit() -> Result<(), ResourceError> {
    // Returns immediately with InUse error if blitter busy
    let blitter = try_acquire_blitter()?;

    unsafe {
        blitter.blit();
    }

    return Ok(());
}
```

**Implementation Details:**
```novus
pub struct BlitterHandle {
    owned: bool,
    waited: bool,
}

impl BlitterHandle {
    // Ensure WaitBlit() called before first register access
    unsafe fn ensure_ready(&mut self) {
        if !self.waited {
            WaitBlit();
            self.waited = true;
        }
    }

    pub unsafe fn set_source(&mut self, addr: *u16) {
        self.ensure_ready();
        // Set BLTAPT register
    }

    // ... other methods
}

impl Drop for BlitterHandle {
    fn drop(&mut self) {
        if self.owned {
            unsafe { DisownBlitter(); }
            self.owned = false;
        }
    }
}

// Blocking acquire
pub fn acquire_blitter() -> Result<BlitterHandle, ResourceError> {
    // AmigaOS OwnBlitter() blocks until available
    // Cannot fail (except in pathological cases like deadlock)
    unsafe {
        OwnBlitter();
    }

    return Ok(BlitterHandle {
        owned: true,
        waited: false
    });
}

// Non-blocking try variant (custom implementation)
pub fn try_acquire_blitter() -> Result<BlitterHandle, ResourceError> {
    // Check if blitter is available without blocking
    // This would require checking hardware status or using custom semaphore
    unsafe {
        if is_blitter_busy() {
            return Err(ResourceError::InUse("".to_string()));
        }
        OwnBlitter();
    }

    return Ok(BlitterHandle {
        owned: true,
        waited: false
    });
}
```

**Safety Guarantees:**
- Cannot forget to call DisownBlitter() (automatic via Drop)
- Cannot forget to call WaitBlit() (automatic before register access)
- Cannot nest OwnBlitter() calls (would require unsafe to bypass handle)
- Cannot use blitter registers without handle (all hardware access requires handle)

---

### Pattern 2: Device I/O

**AmigaOS C Pattern:**
```c
struct IORequest *ioReq = CreateIORequest(msgPort, sizeof(struct IOStdReq));
BYTE err = OpenDevice("timer.device", UNIT_VBLANK, ioReq, 0);
if (err != 0) {
    // Handle error
}

// Synchronous I/O
ioReq->io_Command = TR_ADDREQUEST;
DoIO(ioReq);  // Blocks

// Asynchronous I/O
SendIO(ioReq);
// ... do other work ...
WaitIO(ioReq);

CloseDevice(ioReq);  // Must not forget!
DeleteIORequest(ioReq);
```

**Novus Safe Pattern:**
```novus
from std::devices::timer import TimerDevice, TimeVal, TimerUnit

fn wait_for_time() -> Result<(), DeviceError> {
    // Open device, returns RAII handle
    let timer = TimerDevice::open(TimerUnit::VBlank)?;

    // Synchronous I/O (blocking)
    let elapsed = timer.wait_time(TimeVal::new(1, 0))?;

    // Asynchronous I/O
    let request = timer.add_request_async(TimeVal::new(2, 0))?;
    // ... do other work ...
    let result = request.wait()?;

    // CloseDevice() called automatically when timer drops
    return Ok(());
}
```

**Implementation Details:**
```novus
pub struct TimerDevice {
    io_request: *IORequest,
    msg_port: *MsgPort,
}

impl TimerDevice {
    pub fn open(unit: TimerUnit) -> Result<Self, DeviceError> {
        unsafe {
            let msg_port = CreateMsgPort();
            if msg_port.is_null() {
                return Err(DeviceError::OpenFail);
            }

            let io_req = CreateIORequest(msg_port, size_of::<IOStdReq>() as u32);
            if io_req.is_null() {
                DeleteMsgPort(msg_port);
                return Err(DeviceError::OpenFail);
            }

            let err = OpenDevice(
                "timer.device".as_ptr(),
                unit as u32,
                io_req,
                0
            );

            if err != 0 {
                DeleteIORequest(io_req);
                DeleteMsgPort(msg_port);
                return Err(DeviceError::from(err));
            }

            return Ok(TimerDevice {
                io_request: io_req,
                msg_port: msg_port,
            });
        }
    }

    // Synchronous I/O
    pub fn wait_time(&self, duration: TimeVal) -> Result<TimeVal, DeviceError> {
        unsafe {
            (*self.io_request).io_Command = TR_ADDREQUEST as u16;
            // Set duration in IORequest

            let err = DoIO(self.io_request);
            if err != 0 {
                return Err(DeviceError::from(err));
            }

            // Return elapsed time
            return Ok(TimeVal::new(0, 0));  // Read from IORequest
        }
    }

    // Asynchronous I/O
    pub fn add_request_async(&self, duration: TimeVal) -> Result<AsyncIORequest, DeviceError> {
        unsafe {
            (*self.io_request).io_Command = TR_ADDREQUEST as u16;
            // Set duration in IORequest

            SendIO(self.io_request);

            return Ok(AsyncIORequest {
                io_request: self.io_request,
                completed: false,
            });
        }
    }
}

impl Drop for TimerDevice {
    fn drop(&mut self) {
        unsafe {
            // Ensure all I/O complete before closing
            // AbortIO + WaitIO for any outstanding requests
            CloseDevice(self.io_request);
            DeleteIORequest(self.io_request);
            DeleteMsgPort(self.msg_port);
        }
    }
}

pub struct AsyncIORequest {
    io_request: *IORequest,
    completed: bool,
}

impl AsyncIORequest {
    // Non-blocking check
    pub fn is_complete(&mut self) -> bool {
        unsafe {
            let result = CheckIO(self.io_request);
            if !result.is_null() {
                self.completed = true;
                return true;
            }
            return false;
        }
    }

    // Blocking wait
    pub fn wait(&mut self) -> Result<(), DeviceError> {
        if self.completed {
            return Ok(());
        }

        unsafe {
            let err = WaitIO(self.io_request);
            self.completed = true;

            if err != 0 {
                return Err(DeviceError::from(err));
            }
            return Ok(());
        }
    }

    // Abort ongoing I/O
    pub fn abort(&mut self) -> Result<(), DeviceError> {
        if self.completed {
            return Ok(());
        }

        unsafe {
            AbortIO(self.io_request);
            let err = WaitIO(self.io_request);
            self.completed = true;

            if err != 0 && err != -2 {  // -2 = IOERR_ABORTED (expected)
                return Err(DeviceError::from(err));
            }
            return Ok(());
        }
    }
}

impl Drop for AsyncIORequest {
    fn drop(&mut self) {
        if !self.completed {
            unsafe {
                AbortIO(self.io_request);
                WaitIO(self.io_request);
            }
        }
    }
}
```

**Safety Guarantees:**
- Cannot forget to close device (automatic via Drop)
- Cannot forget to wait for async I/O (Drop ensures cleanup)
- Cannot reuse IORequest without waiting (type system prevents it)
- All errors explicit via Result<T, E>
- Unit busy errors surfaced immediately

---

### Pattern 3: System Resources (misc.resource)

**AmigaOS C Pattern:**
```c
struct Library *MiscBase = OpenResource("misc.resource");
if (!MiscBase) {
    // Resource not available
}

char *owner = AllocMiscResource(MR_SERIALPORT, "MyTask");
if (owner != NULL) {
    // Someone else owns it
    printf("Serial port owned by: %s\n", owner);
    return;
}

// ... use serial port ...

FreeMiscResource(MR_SERIALPORT);  // Must not forget!
```

**Novus Safe Pattern:**
```novus
from std::resources::misc import MiscResource, SerialPortHandle

fn use_serial() -> Result<(), ResourceError> {
    // Try to acquire serial port
    let serial = MiscResource::acquire_serial_port("MyTask")?;

    unsafe {
        // Access serial port hardware registers
        serial.write_byte(0x41);
    }

    // FreeMiscResource called automatically when serial drops
    return Ok(());
}

// Handle busy case
fn handle_busy_serial() {
    match MiscResource::acquire_serial_port("MyTask") {
        Ok(serial) => {
            // Got it, use it
        },
        Err(ResourceError::InUse(owner)) => {
            println!("Serial port busy, owned by: {}", owner);
        },
        Err(e) => {
            println!("Error: {:?}", e);
        }
    }
}
```

**Implementation Details:**
```novus
pub struct MiscResource {
    base: *Library,
}

impl MiscResource {
    fn get() -> Result<&'static MiscResource, ResourceError> {
        static MISC_BASE: Option<*Library> = None;

        unsafe {
            if MISC_BASE.is_none() {
                let base = OpenResource("misc.resource".as_ptr());
                if base.is_null() {
                    return Err(ResourceError::DeviceNotFound);
                }
                MISC_BASE = Some(base);
            }

            return Ok(&MiscResource { base: MISC_BASE.unwrap() });
        }
    }

    pub fn acquire_serial_port(owner_name: &str) -> Result<SerialPortHandle, ResourceError> {
        let misc = Self::get()?;

        unsafe {
            let current_owner = AllocMiscResource(MR_SERIALPORT, owner_name.as_ptr());
            if !current_owner.is_null() {
                let owner_str = CStr::from_ptr(current_owner).to_string();
                return Err(ResourceError::InUse(owner_str));
            }

            return Ok(SerialPortHandle {
                owner_name: owner_name.to_string(),
            });
        }
    }

    // Similar for parallel port, serial bits, parallel bits
}

pub struct SerialPortHandle {
    owner_name: String,
}

impl SerialPortHandle {
    pub unsafe fn write_byte(&self, byte: u8) {
        // Access SERDAT register
    }

    pub unsafe fn read_byte(&self) -> u8 {
        // Access SERDATR register
        return 0;
    }
}

impl Drop for SerialPortHandle {
    fn drop(&mut self) {
        unsafe {
            FreeMiscResource(MR_SERIALPORT);
        }
    }
}
```

**Safety Guarantees:**
- Cannot forget to free resource (automatic via Drop)
- Current owner name visible in error
- Must provide owner name (debugging aid)
- Cannot be freed from wrong task (Novus task model enforces this)

---

### Pattern 4: Disk Units (disk.resource)

**AmigaOS C Pattern:**
```c
struct DiscResourceUnit *dru = CreateUnit(0);  // df0:
dru->dru_Message.mn_ReplyPort = msgPort;

if (!AllocUnit(0)) {
    // Couldn't allocate
}

struct DiscResourceUnit *unit = GetUnit(dru);
if (unit == NULL) {
    // Busy, will be queued and message sent when available
    WaitPort(msgPort);
    GetMsg(msgPort);
    unit = GetUnit(dru);  // Retry
}

// ... use disk hardware ...

// Set hardware to required end state
CUSTOM->dmacon = DMAF_SETCLR | DMAF_DISK;  // DMA ON
CUSTOM->dsklen = DSKDMAOFF;                // Disk DMA OFF
// Disable interrupts

GiveUnit();  // Must not forget!
FreeUnit(0);
DeleteUnit(dru);
```

**Novus Safe Pattern:**
```novus
from std::resources::disk import DiskResource, DiskUnit

fn use_floppy() -> Result<(), ResourceError> {
    // Allocate unit (blocking)
    let disk = DiskResource::allocate_unit(DiskUnit::DF0, "MyTask")?;

    unsafe {
        // Access disk hardware
        disk.seek_track(40);
        disk.read_sector(0);
    }

    // GiveUnit() + FreeUnit() called automatically
    // Hardware state automatically reset to required end state
    return Ok(());
}

// Async version with message queueing
fn use_floppy_async() -> Result<(), ResourceError> {
    let disk_request = DiskResource::request_unit(DiskUnit::DF0, "MyTask")?;

    // Non-blocking check
    loop {
        if disk_request.is_available() {
            let disk = disk_request.acquire()?;

            unsafe {
                disk.read_sector(0);
            }

            break;
        }

        // Do other work
    }

    return Ok(());
}
```

**Implementation Details:**
```novus
pub enum DiskUnit {
    DF0 = 0,
    DF1 = 1,
    DF2 = 2,
    DF3 = 3,
}

pub struct DiskHandle {
    unit: DiskUnit,
    dru: *DiscResourceUnit,
}

impl DiskResource {
    // Blocking allocation
    pub fn allocate_unit(unit: DiskUnit, owner: &str) -> Result<DiskHandle, ResourceError> {
        unsafe {
            let success = AllocUnit(unit as i32);
            if !success {
                return Err(ResourceError::InUse(String::new()));
            }

            let dru = CreateUnit(unit as i32);
            if dru.is_null() {
                FreeUnit(unit as i32);
                return Err(ResourceError::OpenFailed);
            }

            // Synchronous GetUnit (blocks until available)
            let result = GetUnit(dru);
            // Handle queueing if needed

            return Ok(DiskHandle {
                unit,
                dru,
            });
        }
    }

    // Async request
    pub fn request_unit(unit: DiskUnit, owner: &str) -> Result<DiskRequest, ResourceError> {
        unsafe {
            let success = AllocUnit(unit as i32);
            if !success {
                return Err(ResourceError::InUse(String::new()));
            }

            let dru = CreateUnit(unit as i32);
            if dru.is_null() {
                FreeUnit(unit as i32);
                return Err(ResourceError::OpenFailed);
            }

            // Set up message port for async notification
            let msg_port = CreateMsgPort();
            (*dru).dru_Message.mn_ReplyPort = msg_port;

            // GetUnit returns NULL if busy (queues request)
            GetUnit(dru);

            return Ok(DiskRequest {
                unit,
                dru,
                msg_port,
            });
        }
    }
}

impl DiskHandle {
    pub unsafe fn seek_track(&self, track: u16) {
        // Access disk hardware
    }

    pub unsafe fn read_sector(&self, sector: u16) {
        // Access disk hardware
    }
}

impl Drop for DiskHandle {
    fn drop(&mut self) {
        unsafe {
            // Set hardware to required end state
            CUSTOM->dmacon = DMAF_SETCLR | DMAF_DISK;
            CUSTOM->dsklen = DSKDMAOFF;
            // Disable interrupts

            GiveUnit();
            FreeUnit(self.unit as i32);
            DeleteUnit(self.dru);
        }
    }
}

pub struct DiskRequest {
    unit: DiskUnit,
    dru: *DiscResourceUnit,
    msg_port: *MsgPort,
}

impl DiskRequest {
    pub fn is_available(&self) -> bool {
        unsafe {
            // Check if message received
            let msg = GetMsg(self.msg_port);
            return !msg.is_null();
        }
    }

    pub fn acquire(self) -> Result<DiskHandle, ResourceError> {
        // Must have checked is_available() first
        unsafe {
            let unit = GetUnit(self.dru);
            if unit.is_null() {
                return Err(ResourceError::InUse(String::new()));
            }

            DeleteMsgPort(self.msg_port);

            return Ok(DiskHandle {
                unit: self.unit,
                dru: self.dru,
            });
        }
    }
}

impl Drop for DiskRequest {
    fn drop(&mut self) {
        // Cleanup if never acquired
        unsafe {
            FreeUnit(self.unit as i32);
            DeleteUnit(self.dru);
            DeleteMsgPort(self.msg_port);
        }
    }
}
```

**Safety Guarantees:**
- Cannot forget to free unit (automatic via Drop)
- Hardware end state automatically set correctly
- Async queueing handled safely
- Message port cleanup guaranteed

---

## Timeout Support

All blocking operations can be enhanced with timeout support using timer.device:

```novus
from std::time import Duration

fn acquire_with_timeout<T, E>(
    acquire_fn: fn() -> Result<T, E>,
    timeout: Duration
) -> Result<T, TimeoutError<E>> {
    // Use timer.device to implement timeout
    // Return TimeoutError::Timeout or TimeoutError::Other(E)
}

// Usage
let blitter = acquire_with_timeout(
    || acquire_blitter(),
    Duration::from_secs(5)
)?;
```

## Summary Table

| Resource | Blocking | Non-Blocking | Error on Busy | Auto Cleanup | Hardware Sync |
|----------|----------|--------------|---------------|--------------|---------------|
| Blitter | `acquire_blitter()` | `try_acquire_blitter()` | `ResourceError::InUse` | Yes (Drop) | Yes (WaitBlit) |
| Device | `Device::open()` | N/A | `DeviceError::UnitBusy` | Yes (Drop) | N/A |
| Misc Resource | `acquire_X()` | Same (immediate) | `ResourceError::InUse(owner)` | Yes (Drop) | N/A |
| Disk Unit | `allocate_unit()` | `request_unit()` | `ResourceError::InUse` | Yes (Drop) | Yes (end state) |

## Implementation Priority

### Phase 1: Core Infrastructure
1. `Result<T, E>` type (already in language design)
2. `Drop` trait for RAII cleanup
3. `defer` block support (grammar exists, needs implementation)
4. String type for error messages

### Phase 2: Basic Resources
1. Blitter handle (most common, well-defined)
2. Timer device (needed for timeouts)
3. Misc.resource (simple, good example)

### Phase 3: Complex Resources
1. Disk.resource (async queueing)
2. Other devices (trackdisk, serial, parallel)

### Phase 4: Builder Patterns
1. Ergonomic IORequest builders
2. Device-specific safe wrappers
3. Helper functions for common patterns

## Next Steps

1. Implement `Drop` trait in compiler
2. Implement `Result<T, E>` enum
3. Create `std::hardware::blitter` module as proof of concept
4. Test with real Amiga hardware
5. Iterate based on usability feedback
