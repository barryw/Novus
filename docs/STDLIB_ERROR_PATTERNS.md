# Standard Library Error Handling Patterns

## Overview

This document defines consistent patterns for using `Result<T, E>` in Novus standard library wrappers around AmigaOS APIs.

## Core Principles

1. **Every fallible operation returns Result** - No NULL checks, no special return values
2. **Use the appropriate error type** - DosError for DOS, ExecError for Exec, etc.
3. **Convert raw error codes immediately** - Wrap AmigaOS errors in typed enums
4. **Preserve error information** - Use `dos_last_error()` etc. to get details
5. **Consistent patterns** - Same approach across all libraries

## Error Type Hierarchy

```
std/error.novus
├── DosError       - DOS library (files, locks, I/O)
├── ExecError      - Exec library (memory, tasks, signals)
├── IntuitionError - Intuition (windows, screens, IDCMP)
├── GraphicsError  - Graphics (bitmaps, rastports, copper)
└── NovusError     - Wrapper for multi-subsystem errors
```

## Pattern 1: DOS Operations (File/I/O)

**Use `Result<T, DosError>`**

```novus
from dos import Open, MODE_OLDFILE
from error import DosError, dos_last_error

// Open a file for reading
pub fn open_file(path: String) -> Result<i32, DosError> {
    let fh = Open(path, MODE_OLDFILE)

    if fh == 0 {
        // NULL return - get the actual error
        let err = dos_last_error()
        return Result::Err(err)
    }

    return Result::Ok(fh)
}
```

**Pattern:**
- Check for NULL/0 return
- Call `dos_last_error()` to get typed error
- Return `Result::Ok(value)` on success

## Pattern 2: Exec Operations (Memory/Tasks)

**Use `Result<T, ExecError>`**

```novus
from exec import AllocMem, MEMF_PUBLIC
from error import ExecError

// Allocate memory
pub fn alloc_memory(size: u32) -> Result<i32, ExecError> {
    let ptr = AllocMem(size, MEMF_PUBLIC)

    if ptr == 0 {
        return Result::Err(ExecError::NoMem)
    }

    return Result::Ok(ptr)
}
```

**Pattern:**
- Check for NULL return
- Map to appropriate `ExecError` variant
- No separate error code to fetch (Exec doesn't have IoErr equivalent)

## Pattern 3: Intuition Operations (GUI)

**Use `Result<T, IntuitionError>`**

```novus
from intuition import OpenWindow
from error import IntuitionError

// Open a window
pub fn create_window(tags: i32) -> Result<i32, IntuitionError> {
    let window = OpenWindow(tags)

    if window == 0 {
        return Result::Err(IntuitionError::WindowOpenFailed)
    }

    return Result::Ok(window)
}
```

**Pattern:**
- Check for NULL return
- Map to specific error variant based on operation
- No error code to fetch

## Pattern 4: Graphics Operations

**Use `Result<T, GraphicsError>`**

```novus
from graphics import AllocBitMap
from error import GraphicsError

// Allocate a bitmap
pub fn alloc_bitmap(width: u16, height: u16, depth: u8) -> Result<i32, GraphicsError> {
    let bitmap = AllocBitMap(width, height, depth, 0, 0)

    if bitmap == 0 {
        return Result::Err(GraphicsError::BitMapAllocFailed)
    }

    return Result::Ok(bitmap)
}
```

## Pattern 5: Multi-Subsystem Operations

**Use `Result<T, NovusError>` when mixing subsystems**

```novus
from error import NovusError, DosError, IntuitionError

// Complex operation using both DOS and Intuition
pub fn load_and_display(filename: String) -> Result<i32, NovusError> {
    // Open file (DOS operation)
    let fh_result = open_file(filename)
    match fh_result {
        Ok(fh) => {
            // Continue processing
        },
        Err(dos_err) => {
            // Convert DosError to NovusError
            return Result::Err(novus_error_from_dos(dos_err))
        },
    }

    // Open window (Intuition operation)
    let win_result = create_window(0)
    match win_result {
        Ok(win) => {
            return Result::Ok(win)
        },
        Err(intui_err) => {
            // Convert IntuitionError to NovusError
            return Result::Err(novus_error_from_intuition(intui_err))
        },
    }
}
```

## Pattern 6: Operations That Cannot Fail

**Return the value directly - no Result**

```novus
from dos import DateStamp

// Get current timestamp - always succeeds
pub fn current_timestamp() -> i32 {
    let ds = DateStamp()
    return ds
}
```

**Only use Result if the operation can actually fail!**

## Pattern 7: Validation Errors (User Input)

**Use appropriate error type based on context**

```novus
from error import DosError

// Validate path - use DosError since it's filesystem-related
pub fn validate_path(path: String) -> Result<void, DosError> {
    if path.len == 0 {
        return Result::Err(DosError::InvalidInput)
    }

    if path.len > 255 {
        return Result::Err(DosError::ObjectTooLarge)
    }

    return Result::Ok(void)
}
```

## Common Mistakes to Avoid

### ❌ DON'T: Use i32 as error type
```novus
fn bad_open() -> Result<i32, i32> {  // WRONG!
    return Result::Err(-1)  // Meaningless error code
}
```

### ✓ DO: Use typed error enum
```novus
fn good_open() -> Result<i32, DosError> {
    return Result::Err(DosError::NotFound)  // Clear, typed error
}
```

### ❌ DON'T: Lose error information
```novus
fn bad_read() -> Result<i32, DosError> {
    let bytes = Read(fh, buffer, size)
    if bytes < 0 {
        return Result::Err(DosError::Unknown)  // Lost the real error!
    }
}
```

### ✓ DO: Preserve error details
```novus
fn good_read() -> Result<i32, DosError> {
    let bytes = Read(fh, buffer, size)
    if bytes < 0 {
        let err = dos_last_error()  // Get actual error
        return Result::Err(err)
    }
}
```

### ❌ DON'T: Return Result when operation can't fail
```novus
fn bad_add(a: i32, b: i32) -> Result<i32, DosError> {  // WRONG!
    return Result::Ok(a + b)  // Addition can't fail
}
```

### ✓ DO: Return value directly
```novus
fn good_add(a: i32, b: i32) -> i32 {
    return a + b
}
```

## Checklist for Stdlib Functions

When wrapping an AmigaOS function, ask:

1. ✓ Can this operation fail?
   - Yes → Use Result
   - No → Return value directly

2. ✓ Which subsystem?
   - DOS → `Result<T, DosError>`
   - Exec → `Result<T, ExecError>`
   - Intuition → `Result<T, IntuitionError>`
   - Graphics → `Result<T, GraphicsError>`
   - Multiple → `Result<T, NovusError>`

3. ✓ Does it have error codes?
   - DOS: Call `dos_last_error()`
   - Others: Map NULL/failure to specific enum variant

4. ✓ Is error information preserved?
   - Get actual error, don't use generic Unknown

5. ✓ Is the pattern consistent with similar functions?
   - Follow established patterns in same subsystem

## Example: Complete DOS Wrapper

```novus
// dos_wrapper.novus - Example of consistent DOS wrapping

from dos import Open, Close, Read, Write, Seek, MODE_OLDFILE, MODE_NEWFILE, OFFSET_BEGINNING
from error import DosError, dos_last_error

// Open file for reading
pub fn open_read(path: String) -> Result<i32, DosError> {
    let fh = Open(path, MODE_OLDFILE)
    if fh == 0 {
        return Result::Err(dos_last_error())
    }
    return Result::Ok(fh)
}

// Open file for writing
pub fn open_write(path: String) -> Result<i32, DosError> {
    let fh = Open(path, MODE_NEWFILE)
    if fh == 0 {
        return Result::Err(dos_last_error())
    }
    return Result::Ok(fh)
}

// Read from file
pub fn read_file(fh: i32, buffer: i32, size: i32) -> Result<i32, DosError> {
    let bytes = Read(fh, buffer, size)
    if bytes < 0 {
        return Result::Err(dos_last_error())
    }
    return Result::Ok(bytes)
}

// Write to file
pub fn write_file(fh: i32, buffer: i32, size: i32) -> Result<i32, DosError> {
    let bytes = Write(fh, buffer, size)
    if bytes < 0 {
        return Result::Err(dos_last_error())
    }
    return Result::Ok(bytes)
}

// Seek in file
pub fn seek_file(fh: i32, pos: i32, mode: i32) -> Result<i32, DosError> {
    let old_pos = Seek(fh, pos, mode)
    if old_pos < 0 {
        return Result::Err(dos_last_error())
    }
    return Result::Ok(old_pos)
}

// Close file - can't fail
pub fn close_file(fh: i32) {
    Close(fh)
}
```

## Summary

**Golden Rules:**
1. Every fallible AmigaOS call → Wrap in Result
2. Use the correct error type for the subsystem
3. Preserve error information (don't lose details)
4. Be consistent across the entire stdlib
5. If it can't fail, don't return Result

This ensures that **every** stdlib function that interacts with AmigaOS has proper, typed, explicit error handling.
