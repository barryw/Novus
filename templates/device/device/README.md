# {{PROJECT_NAME}} Device Driver

This is the device driver project for {{PROJECT_NAME}}.device.

## Building

From the workspace root:
```bash
novusc build
```

Or from this directory:
```bash
novusc build
```

## Output Files

After building, these files are generated:

| File | Purpose |
|------|---------|
| `{{PROJECT_NAME}}.device` | Device binary - copy to DEVS: |
| `{{PROJECT_NAME}}.h` | C header for C clients |
| `{{PROJECT_NAME}}.novus` | Novus FFI binding |
| `{{PROJECT_NAME}}_wrappers.s` | A6 calling convention wrappers |
| `{{PROJECT_NAME}}_lifecycle.c` | Generated lifecycle functions |

## Installation

Copy the device to your Amiga's DEVS: directory:

```bash
copy {{PROJECT_NAME}}.device DEVS:
```

## Configuration

Edit `project.toml` to change build settings:

```toml
[package]
name = "{{PROJECT_NAME}}"
version = "1.0.0"
type = "device"

[build]
target_cpu = "68020"  # Novus minimum; also 68030, 68040, 68060, 68080
fpu = "auto"          # Options: auto, soft, 68881, 68882, 68040, 68060
optimization_level = 0  # Options: 0, 1, 2
```

## Device Architecture

### @device Attribute

The `@device` attribute defines the device:

```novus
@device(name = "{{PROJECT_NAME}}.device", units = 4)
pub struct MyDevice {
    // Device state fields
}
```

Parameters:
- `name` - Device filename (must end with `.device`)
- `units` - Maximum number of units (default: 1)

### @devicecmd Attribute

Command handlers are marked with `@devicecmd`:

```novus
@devicecmd(cmd = "CMD_READ")
pub fn cmd_read(ioReq: *IORequest, base: *MyDevice) -> i8 {
    // Handle read command
    return 0
}
```

Parameters:
- `cmd` - Command name (string) or number (int)
- `quick` - If true, command can complete without blocking
- `deferred` - If true, the handler owns the request and replies asynchronously

Deferred handlers must eventually call `ReplyMsg()` or return ownership through an
`@abortio` hook. Clients must collect every `SendIO()` with `WaitIO()`, including
after `AbortIO()`.

### Generated Functions

The compiler generates these lifecycle functions:
- `DevInit` - Called when device loads
- `DevOpen` - Called for each OpenDevice()
- `DevClose` - Called for each CloseDevice()
- `DevExpunge` - Called when device should unload
- `DevBeginIO` - Command dispatcher
- `DevAbortIO` - Abort pending I/O

## Adding Commands

1. Define a command constant:
```novus
pub const CMD_MYCOMMAND: u16 = 10
```

2. Add a handler:
```novus
@devicecmd(cmd = 10)
pub fn cmd_mycommand(ioReq: *IORequest, base: *MyDevice) -> i8 {
    // Handle command
    return 0
}
```

3. Rebuild - the dispatcher is updated automatically!

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
