# Blitter Ownership Arbitration in Novus

## Overview

The Amiga blitter is a shared hardware resource accessed by:
- User programs
- The operating system (layers, graphics operations)
- The Copper (display coprocessor)

Before directly accessing the blitter hardware, programs **MUST** acquire exclusive ownership using `OwnBlitter()`. Failing to do this causes crashes when multiple components try to use the blitter simultaneously.

## The Problem: Manual Ownership Management

Traditional C code requires manual calls to acquire and release the blitter:

```c
OwnBlitter();
WaitBlit();
// ... set blitter registers ...
// ... trigger operation ...
WaitBlit();
DisownBlitter();
```

This approach has several problems:
1. Easy to forget `DisownBlitter()` on error paths
2. No automatic cleanup if function returns early
3. Blitter can be leaked if code panics/crashes
4. Verbose and error-prone

## The Solution: BlitterGuard RAII Pattern

Novus provides `BlitterGuard`, an RAII (Resource Acquisition Is Initialization) wrapper that automatically manages blitter ownership:

```novus
from amiga::sys::graphics::blitter import BlitterGuard

{
    let guard = BlitterGuard::acquire()

    // Blitter is now owned by this program
    // Do blitter operations...

    guard.wait()  // Wait for blitter to finish

    // Guard's Drop automatically calls WaitBlit() + DisownBlitter()
}
// Blitter is now released, even if we returned early!
```

## BlitterGuard API

### `BlitterGuard::acquire() -> BlitterGuard`

Acquires exclusive blitter ownership by calling `OwnBlitter()`.

**Example:**
```novus
let guard = BlitterGuard::acquire()
```

### `guard.wait()`

Waits for the blitter to complete any pending operations (calls `WaitBlit()`). This is non-destructive - the guard remains valid and blitter ownership is retained.

**Example:**
```novus
let guard = BlitterGuard::acquire()
// ... trigger blitter operation ...
guard.wait()  // Wait for completion
// ... do more blitter operations ...
guard.wait()  // Wait again
```

### `guard.is_busy() -> bool`

Non-blocking check if blitter is currently processing. Returns `true` if busy, `false` if idle.

**Example:**
```novus
let guard = BlitterGuard::acquire()
// ... trigger blitter operation ...
while guard.is_busy() {
    // Do other work while waiting
}
```

### Automatic Cleanup (Drop)

When `BlitterGuard` goes out of scope, its `Drop` implementation automatically:
1. Calls `WaitBlit()` to ensure all operations complete
2. Calls `DisownBlitter()` to release ownership
3. Works correctly even on early returns or errors

## Usage Patterns

### Basic Usage

```novus
from amiga::sys::graphics::blitter import BlitterGuard

fn do_blit_operation() {
    let guard = BlitterGuard::acquire()

    unsafe {
        // Set up blitter registers
        let bltcon0_ptr: *u16 = (*u16)(CUSTOM_BASE + BLTCON0)
        *bltcon0_ptr = BLTCON0_SRCA | BLTCON0_DEST | MINTERM_COPY

        // ... more blitter setup ...

        // Trigger operation
        let bltsize_ptr: *u16 = (*u16)(CUSTOM_BASE + BLTSIZE)
        *bltsize_ptr = (64 << 6) | 4  // 64 rows, 4 words
    }

    guard.wait()  // Wait for completion

    // Guard automatically releases blitter when function returns
}
```

### Error Handling

The guard ensures cleanup even on error paths:

```novus
fn blit_with_validation() -> Result<(), BlitterError> {
    let guard = BlitterGuard::acquire()

    // Validate something
    if some_error_condition {
        // Guard will still release blitter when we return
        return Result::Err(BlitterError::InvalidDimensions)
    }

    unsafe {
        // Do blitter operations...
    }

    guard.wait()
    return Result::Ok(())
}
```

### Multiple Sequential Operations

Guards can be created in sequence:

```novus
fn multiple_blits() {
    // First operation
    {
        let guard = BlitterGuard::acquire()
        // ... blit 1 ...
        guard.wait()
    } // Blitter released

    // Second operation
    {
        let guard = BlitterGuard::acquire()
        // ... blit 2 ...
        guard.wait()
    } // Blitter released
}
```

### Integration with High-Level APIs

The high-level `BlitterOps` API uses `BlitterGuard` internally, so you don't need to manually acquire the blitter:

```novus
from amiga::sys::graphics::blitter import BlitterOps
from amiga::sys::graphics::bitmap import BitMapHandle

fn high_level_blit(src: &BitMapHandle, dst: &mut BitMapHandle) -> Result<(), BlitterError> {
    // BlitterOps::copy_rect() uses BlitterGuard internally
    return BlitterOps::copy_rect(src, 0, 0, dst, 100, 50, 64, 32)
}
```

## Important Notes

1. **Never nest guards**: Don't acquire a second guard while one is already held. The Amiga blitter can only be owned by one context at a time.

   ```novus
   // ❌ BAD: Don't do this!
   let guard1 = BlitterGuard::acquire()
   let guard2 = BlitterGuard::acquire()  // Will hang!
   ```

2. **Keep guard in scope**: Don't drop the guard too early:

   ```novus
   // ❌ BAD: Guard drops before operation completes
   {
       let guard = BlitterGuard::acquire()
   } // Guard drops here

   unsafe {
       // This is unsafe! No blitter ownership!
       *bltsize_ptr = bltsize
   }
   ```

3. **Wait before dropping**: The guard's `Drop` calls `WaitBlit()`, but it's better to explicitly wait after triggering operations:

   ```novus
   // ✅ GOOD: Explicit wait
   let guard = BlitterGuard::acquire()
   // ... trigger operation ...
   guard.wait()  // Explicit wait
   // Guard drop will also wait, but redundant waits are cheap
   ```

4. **High-level APIs preferred**: Use `BlitterOps` when possible. Only acquire the guard manually when you need direct hardware access.

## Implementation Details

The `BlitterGuard` struct is simple:

```novus
pub struct BlitterGuard {
    acquired: bool,
}

impl BlitterGuard {
    pub fn acquire() -> BlitterGuard {
        OwnBlitter()
        return BlitterGuard { acquired: true }
    }

    pub fn wait(&self) {
        if self.acquired {
            WaitBlit()
        }
    }
}

impl Drop for BlitterGuard {
    fn drop(&mut self) {
        if self.acquired {
            WaitBlit()
            DisownBlitter()
            self.acquired = false
        }
    }
}
```

The `acquired` flag ensures `Drop` is idempotent and prevents double-free issues.

## Comparison with Traditional AmigaOS Code

### C/C++ (Manual Management)

```c
OwnBlitter();
WaitBlit();

// Set up registers
custom.bltcon0 = BLTCON0_SRCA | BLTCON0_DEST | 0xF0;
custom.bltapt = src_ptr;
custom.bltdpt = dst_ptr;
custom.bltsize = (64 << 6) | 4;

WaitBlit();
DisownBlitter();  // Easy to forget on error paths!
```

### Novus (RAII Pattern)

```novus
let guard = BlitterGuard::acquire()

unsafe {
    let con0_ptr: *u16 = (*u16)(CUSTOM_BASE + BLTCON0)
    *con0_ptr = BLTCON0_SRCA | BLTCON0_DEST | 0xF0

    let apt_ptr: *u32 = (*u32)(CUSTOM_BASE + BLTAPT)
    *apt_ptr = (u32)src_ptr

    let dpt_ptr: *u32 = (*u32)(CUSTOM_BASE + BLTDPT)
    *dpt_ptr = (u32)dst_ptr

    let size_ptr: *u16 = (*u16)(CUSTOM_BASE + BLTSIZE)
    *size_ptr = (64 << 6) | 4
}

guard.wait()
// Cleanup automatic - can't forget!
```

## See Also

- `/Users/barry/RiderProjects/Novus/Novus/std/graphics/blitter.novus` - Full BlitterGuard implementation
- `/Users/barry/RiderProjects/Novus/Novus/std/amiga/raw/graphics.novus` - FFI declarations for OwnBlitter/DisownBlitter/WaitBlit
- Amiga Hardware Reference Manual, Chapter 6 - Blitter hardware details
