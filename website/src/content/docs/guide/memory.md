---
title: Memory Management
description: Learn about memory management in Novus including stack, heap, ownership, borrowing, and AmigaOS memory
---

Memory management in Novus is explicit and deterministic. There's no garbage collector - you control when memory is allocated and freed. This guide covers how memory works, ownership, borrowing, and AmigaOS-specific memory features.

## Stack vs Heap

### Stack Memory

The stack is fast, automatic memory with limited size:

```novus
pub fn main() -> i32 {
    let x = 42           // Allocated on stack
    let arr = [1, 2, 3]  // Array on stack

    // Automatically freed when function returns
    return 0
}
```

Stack characteristics:
- **Fast**: Simple pointer adjustment
- **Automatic**: Allocated/freed automatically
- **Limited**: Small size (typically 4KB-8KB on Amiga)
- **Lifetime**: Tied to function scope
- **Order**: LIFO (Last In, First Out)

### Heap Memory

The heap is slower but larger and more flexible:

```novus
from std::ffi::exec import AllocMem, FreeMem
from std::ffi::amiga_consts import MEMF_ANY

pub fn main() -> i32 {
    // Allocate 1KB on heap
    let ptr = AllocMem(1024, MEMF_ANY)

    if ptr == 0 {
        return -1  // Allocation failed
    }

    // Use memory...

    // Must manually free
    FreeMem(ptr, 1024)

    return 0
}
```

Heap characteristics:
- **Slower**: Requires memory manager
- **Manual**: Must explicitly free
- **Large**: Limited only by available RAM
- **Lifetime**: Until explicitly freed
- **Flexible**: Allocate at runtime

### When to Use Each

**Use the stack for:**
- Local variables
- Small arrays with known size
- Short-lived data
- Function parameters

**Use the heap for:**
- Large data structures
- Variable-sized data
- Data that outlives function scope
- Data shared between functions

## Ownership

Ownership is Novus's core memory safety concept. Every value has a single owner:

```novus
struct Point {
    x: i32,
    y: i32,
}

pub fn main() -> i32 {
    let p1 = Point { x: 10, y: 20 }  // p1 owns the Point

    // Transfer ownership (move)
    let p2 = p1

    // ERROR: p1 no longer valid!
    // let x = p1.x

    // p2 is the owner now
    let y = p2.y  // OK

    return 0
}
```

Ownership rules:
1. Each value has exactly one owner
2. When the owner goes out of scope, the value is freed
3. Values can be moved to a new owner
4. After moving, the old variable is invalid

### Ownership Transfer

```novus
fn takes_ownership(p: Point) {
    // Function now owns p
    // p is freed when function returns
}

pub fn main() -> i32 {
    let point = Point { x: 5, y: 10 }

    takes_ownership(point)
    // point is no longer valid here

    return 0
}
```

### Returning Ownership

```novus
fn create_point() -> Point {
    let p = Point { x: 0, y: 0 }
    return p  // Ownership transferred to caller
}

pub fn main() -> i32 {
    let point = create_point()  // Receives ownership
    return 0
}
```

## Borrowing

Borrowing allows temporary access without transferring ownership:

### Immutable Borrows (`&T`)

```novus
struct Point {
    x: i32,
    y: i32,
}

fn print_point(p: &Point) {
    // Borrow p (read-only)
    // p is still owned by caller
}

pub fn main() -> i32 {
    let point = Point { x: 10, y: 20 }

    print_point(&point)  // Borrow point
    print_point(&point)  // Can borrow again

    // point still valid
    let x = point.x

    return 0
}
```

Immutable borrows:
- Read-only access
- Multiple immutable borrows allowed
- Original owner cannot modify during borrow

### Mutable Borrows (`&var T`)

```novus
fn move_point(p: &var Point, dx: i32, dy: i32) {
    p.x = p.x + dx
    p.y = p.y + dy
}

pub fn main() -> i32 {
    var point = Point { x: 10, y: 20 }

    move_point(&point, 5, 10)  // Mutable borrow

    // point is now (15, 30)
    return 0
}
```

Mutable borrow rules:
- Only one mutable borrow at a time
- No other borrows (immutable or mutable) while mutably borrowed
- Prevents data races at compile time

### Borrow Checker Rules

```novus
pub fn main() -> i32 {
    var x = 42

    let r1 = &x      // OK - immutable borrow
    let r2 = &x      // OK - multiple immutable borrows
    // let r3 = &x   // ERROR - cannot mutably borrow while immutably borrowed

    let y = *r1 + *r2  // Use borrows

    let r3 = &x      // OK - previous borrows ended
    *r3 = 100        // Modify through mutable borrow

    return 0
}
```

## Defer: Automatic Cleanup

The `defer` statement ensures cleanup code runs when scope exits:

```novus
from std::ffi::exec import AllocMem, FreeMem
from std::ffi::amiga_consts import MEMF_ANY

pub fn main() -> i32 {
    let ptr = AllocMem(1024, MEMF_ANY)
    if ptr == 0 {
        return -1
    }

    // Schedule cleanup - runs at scope exit
    defer {
        FreeMem(ptr, 1024)
    }

    // Use memory...
    // Even if we return early, defer executes

    if some_condition {
        return 0  // defer still runs!
    }

    return 0  // defer runs here too
}
```

Defer characteristics:
- Executes in LIFO order (last defer first)
- Runs even on early return
- Runs **after** return value is computed
- Perfect for resource cleanup

### Multiple Defers

```novus
fn open_resources() -> i32 {
    let r1 = alloc_resource(1)
    defer {
        free_resource(r1)  // Runs third
    }

    let r2 = alloc_resource(2)
    defer {
        free_resource(r2)  // Runs second
    }

    let r3 = alloc_resource(3)
    defer {
        free_resource(r3)  // Runs first

    }

    // Use resources...

    return 0
    // Execution order:
    // 1. Return value computed (0)
    // 2. free_resource(r3)
    // 3. free_resource(r2)
    // 4. free_resource(r1)
    // 5. Function returns
}
```

## AmigaOS Memory

AmigaOS provides flexible memory allocation with different memory types:

### Memory Types

```novus
from std::ffi::amiga_consts import *

// Any available memory
let ptr1 = AllocMem(1024, MEMF_ANY)

// Chip memory (accessible by custom chips)
let ptr2 = AllocMem(1024, MEMF_CHIP)

// Fast memory (not accessible by custom chips, but faster)
let ptr3 = AllocMem(1024, MEMF_FAST)

// Public memory (can be shared across tasks)
let ptr4 = AllocMem(1024, MEMF_PUBLIC)

// Clear to zero
let ptr5 = AllocMem(1024, MEMF_CLEAR | MEMF_ANY)
```

Memory flags:
- `MEMF_ANY` - Any available memory
- `MEMF_CHIP` - Chip memory (for graphics, audio, blitter)
- `MEMF_FAST` - Fast memory (for CPU-only data)
- `MEMF_PUBLIC` - Public (task-sharable)
- `MEMF_CLEAR` - Initialize to zero

### Chip vs Fast Memory

**Chip Memory:**
- Accessible by custom chips (Blitter, Copper, Paula)
- Required for graphics data, audio samples, sprite data
- Limited (512KB-2MB depending on model)
- Slower CPU access

**Fast Memory:**
- Only accessible by CPU
- Not usable for hardware DMA
- Can be much larger (up to 512MB on accelerated systems)
- Faster CPU access

```novus
// Graphics data must be in chip memory
let screen = AllocMem(320 * 200, MEMF_CHIP | MEMF_CLEAR)

// Application data can use fast memory
let buffer = AllocMem(100000, MEMF_FAST)
```

### Safe AmigaOS Memory Allocation

```novus
from std::core import Result
from std::error::errors import ExecError
from std::ffi::exec import AllocMem, FreeMem
from std::ffi::amiga_consts import MEMF_ANY

fn allocate_buffer(size: u32) -> Result<*u8, ExecError> {
    let ptr = AllocMem(size, MEMF_ANY)

    if ptr == 0 {
        return Result::Err(ExecError::NoMemory)
    }

    return Result::Ok((*u8)ptr)
}

pub fn main() -> i32 {
    let result = allocate_buffer(1024)

    match result {
        Result::Ok(ptr) => {
            defer {
                FreeMem((u32)ptr, 1024)
            }

            // Use buffer...

            return 0
        },
        Result::Err(_) => {
            return -1
        }
    }
}
```

## RAII: Resource Acquisition Is Initialization

RAII uses ownership to automatically manage resources:

```novus
struct Buffer {
    ptr: *u8,
    size: u32,
}

impl Drop for Buffer {
    fn drop(self: &var Buffer) {
        if self.ptr != 0 {
            FreeMem((u32)self.ptr, self.size)
        }
    }
}

fn Buffer::new(size: u32) -> Result<Buffer, ExecError> {
    let ptr = AllocMem(size, MEMF_ANY)

    if ptr == 0 {
        return Result::Err(ExecError::NoMemory)
    }

    return Result::Ok(Buffer {
        ptr: (*u8)ptr,
        size: size,
    })
}

pub fn main() -> i32 {
    let buffer = match Buffer::new(1024) {
        Result::Ok(b) => b,
        Result::Err(_) => return -1
    }

    // Use buffer...

    // Automatically freed when buffer goes out of scope
    return 0
}
```

The `Drop` trait automatically cleans up when the value goes out of scope.

## Memory Safety Examples

### Safe Array Access

```novus
pub fn main() -> i32 {
    let arr = [1, 2, 3, 4, 5]

    // Safe - bounds checked in debug builds
    let x = arr[2]  // x = 3

    // Unsafe - will panic in debug builds
    // let y = arr[10]  // Out of bounds!

    return 0
}
```

### Preventing Use-After-Free

```novus
fn dangerous() -> i32 {
    var x = 42
    let r = &x

    // ERROR: x's lifetime ends here
    return *r  // Compiler error - r outlives x
}
```

The compiler prevents accessing memory after it's freed.

### Preventing Data Races

```novus
fn no_data_races() {
    var x = 42

    let r1 = &x      // Immutable borrow
    let r2 = &x      // OK - multiple immutable borrows

    // let r3 = &x   // ERROR - cannot mutably borrow while immutably borrowed

    // Use r1 and r2...

    let r3 = &x      // OK - r1 and r2 are done
    *r3 = 100        // Modify through mutable borrow
}
```

## Best Practices

1. **Prefer stack allocation**: Use stack for small, short-lived data
2. **Use defer for cleanup**: Ensures resources are freed even on early return
3. **Minimize heap allocations**: They're slow on 68k systems
4. **Use RAII wrappers**: Automatic cleanup prevents leaks
5. **Choose memory type carefully**: Use CHIP for hardware, FAST for CPU
6. **Avoid raw pointers**: Use references when possible
7. **Check allocation results**: Memory can fail on Amiga systems

## Common Patterns

### Temporary Buffer

```novus
fn process_data() -> Result<i32, ExecError> {
    let buffer = AllocMem(4096, MEMF_FAST)
    if buffer == 0 {
        return Result::Err(ExecError::NoMemory)
    }

    defer {
        FreeMem(buffer, 4096)
    }

    // Process with buffer...

    return Result::Ok(0)
}
```

### Resource Pool

```novus
struct Pool {
    memory: *u8,
    size: u32,
    used: u32,
}

impl Pool {
    fn allocate(self: &var Pool, size: u32) -> Option<*u8> {
        if self.used + size > self.size {
            return Option::None
        }

        let ptr = (u32)self.memory + self.used
        self.used = self.used + size

        return Option::Some((*u8)ptr)
    }

    fn reset(self: &var Pool) {
        self.used = 0
    }
}
```

### Handle Pattern

```novus
struct ScreenHandle {
    screen: *Screen,
}

impl Drop for ScreenHandle {
    fn drop(self: &var ScreenHandle) {
        if self.screen != 0 {
            CloseScreen(self.screen)
        }
    }
}

fn open_screen_safe(width: u16, height: u16) -> Result<ScreenHandle, str> {
    let screen = OpenScreen(width, height)

    if screen == 0 {
        return Result::Err("Failed to open screen")
    }

    return Result::Ok(ScreenHandle { screen: screen })
}
```

## Memory Layout

### Struct Memory Layout

```novus
struct Point {
    x: i32,  // Offset 0, 4 bytes
    y: i32,  // Offset 4, 4 bytes
}
// Total size: 8 bytes

struct Color {
    r: u8,  // Offset 0, 1 byte
    g: u8,  // Offset 1, 1 byte
    b: u8,  // Offset 2, 1 byte
    a: u8,  // Offset 3, 1 byte
}
// Total size: 4 bytes
```

Structs are laid out sequentially in memory with natural alignment.

### Array Memory Layout

```novus
let arr: [i32; 4] = [10, 20, 30, 40]
// Memory layout:
// [10] [20] [30] [40]
// Elements are contiguous
```

## Coming from C

Key differences from C:

| C | Novus |
|---|-------|
| `int x = malloc(10);` | `let x = AllocMem(10, MEMF_ANY)` |
| `free(ptr);` | `FreeMem(ptr, size)` |
| Manual memory management | Ownership + borrowing |
| Pointer arithmetic | Safe references |
| `NULL` pointers | `Option<*T>` |
| Dangling pointers possible | Prevented by borrow checker |
| Data races possible | Prevented by borrow checker |

Key advantages:
- **Memory safety**: Borrow checker prevents use-after-free
- **No null pointer crashes**: Use `Option` for nullable pointers
- **No data races**: Mutable borrowing rules prevent races
- **RAII**: Automatic cleanup with `Drop` trait
- **Explicit ownership**: Clear who owns what

Novus gives you C-like control with Rust-like safety.
