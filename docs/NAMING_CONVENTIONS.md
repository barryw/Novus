# Novus Naming Conventions

This document defines the official naming standards for Novus code.

## Overview

Novus uses **case to convey meaning** - different naming styles distinguish between FFI boundaries, safety levels, and language constructs.

## The Rules

### Constants: `SCREAMING_SNAKE_CASE`

All constants use uppercase with underscores:

```novus
pub const MEMF_FAST: u32 = (1u32 << 2)
pub const IDCMP_MOUSEBUTTONS: u32 = $00000020u32
pub const MAX_PATH_LENGTH: u32 = 256
```

**Why:** Matches C/Amiga conventions, highly visible, universally understood.

### Types: `PascalCase`

Structs, enums, and type aliases use PascalCase:

```novus
pub struct Task { ... }
pub struct MemoryBlock { ... }
pub enum Option<T> { Some(T), None }
pub enum Result<T, E> { Ok(T), Err(E) }
```

**Why:** Standard in most languages, distinguishes types from values.

### FFI Functions: `PascalCase`

Raw FFI functions that map directly to AmigaOS use PascalCase to match C conventions:

```novus
// In std::ffi::exec (generated from SFD files)
extern pub fn AllocMem(byteSize: u32, requirements: u32) -> *u8
extern pub fn FreeMem(memoryBlock: *u8, byteSize: u32)
extern pub fn FindTask(name: *u8) -> *Task
extern pub fn OpenScreen(newScreen: *NewScreen) -> *Screen
```

**Why:** Matches Amiga NDK exactly, makes FFI boundary obvious, respects platform conventions.

### Wrapper Functions: `snake_case`

Safe Novus wrappers around FFI use snake_case:

```novus
// In std::exec (safe wrappers)
pub fn alloc_mem(byte_size: u32, requirements: u32) -> Option<*u8>
pub fn free_mem(memory_block: *u8, byte_size: u32)
pub fn find_task(name: Option<&str>) -> Option<&Task>
pub fn open_screen(tags: &[TagItem]) -> Result<&Screen, Error>
```

**Why:**
- Clear distinction from raw FFI (case shows safety level)
- Modern, readable (Rust, Go, Python all use snake_case)
- No prefix pollution (the case itself conveys meaning)

### Methods: `snake_case`

All impl methods use snake_case:

```novus
impl Task {
    pub fn name(&self) -> &str { ... }
    pub fn priority(&self) -> i8 { ... }
    pub fn is_ready(&self) -> bool { ... }
    pub fn set_priority(&mut self, pri: i8) { ... }
}

impl<T> Option<T> {
    pub fn is_some(&self) -> bool { ... }
    pub fn unwrap(self) -> T { ... }
}
```

**Why:** Consistent with function naming, reads naturally.

### Variables: `snake_case`

Local variables, function parameters, and struct fields use snake_case:

```novus
fn main() {
    let current_task = find_task(None).unwrap()
    let mem_block = alloc_mem(1024, MEMF_FAST)?
    let task_name = current_task.name()
}

pub struct MemoryBlock {
    ptr: *u8,
    size: u32,
}
```

**Why:** Consistent, readable, standard practice.

### Module Names: `snake_case`

Module files and namespaces use snake_case:

```
std/core.novus          → std::core
std/collections.novus   → std::collections
std/ffi/exec.novus     → std::ffi::exec
std/amiga_structs.novus → std::amiga_structs
```

**Why:** Unix/filesystem friendly, standard practice.

## The Pattern

The naming convention creates a **visual hierarchy**:

```novus
from std::ffi::exec import AllocMem, FindTask, MEMF_FAST  // FFI layer
from std::exec import alloc_mem, find_task                // Safe layer
from std::amiga_structs import Task                       // Types

pub fn do_something() {
    // CONSTANTS are LOUD
    let flags = MEMF_FAST | MEMF_CLEAR

    // FFI Functions Look Like C
    let raw_ptr = AllocMem(1024, flags)  // Unsafe, raw

    // wrapper_functions are safe and idiomatic
    let safe_mem = alloc_mem(1024, flags)?  // Safe, Result-based

    // Types Are Clear
    let task: Task = find_task(None).unwrap()

    // methods.are.chainable()
    if task.is_ready() {
        println!("Task: {}", task.name())
    }
}
```

## Benefits

1. **Self-documenting**: The case tells you the safety level and source
2. **No namespace pollution**: No need for `Safe` or `Raw` prefixes
3. **Familiar**: Follows Rust conventions (snake_case) while respecting Amiga conventions (PascalCase FFI)
4. **Pragmatic**: Easy to mix FFI and safe code when needed
5. **Grep-friendly**: Easy to search for `AllocMem` (FFI) vs `alloc_mem` (wrapper)

## Examples

### Memory Management

```novus
// FFI layer - matches Amiga C exactly
from std::ffi::exec import AllocMem, FreeMem, MEMF_FAST

// Safe wrapper layer
from std::core import alloc_mem, free_mem, MemoryBlock

// User code
fn allocate_buffer() -> Option<MemoryBlock> {
    alloc_mem(4096, MEMF_FAST)
}
```

### Task Management

```novus
// FFI
from std::ffi::exec import FindTask, SetTaskPri

// Wrappers
from std::exec import find_task

// Types
from std::amiga_structs import Task

// Extension methods
impl Task {
    pub fn set_priority(&mut self, pri: i8) {
        unsafe { SetTaskPri(self as *Task, pri) }
    }
}
```

### Graphics

```novus
// FFI
from std::ffi::graphics import OpenScreen, CloseScreen

// Wrappers
from std::graphics import open_screen, close_screen

// Types
from std::amiga_structs import Screen, NewScreen

impl Screen {
    pub fn width(&self) -> u16 { self.Width }
    pub fn height(&self) -> u16 { self.Height }
}
```

## Migration Guide

When updating old code:

1. **FFI functions** → Keep as PascalCase (they're generated)
2. **Wrapper functions** → Rename to snake_case:
   - `AllocMem()` wrapper → `alloc_mem()`
   - `FindTask()` wrapper → `find_task()`
   - `OpenScreen()` wrapper → `open_screen()`
3. **Types and constants** → Already correct (PascalCase and SCREAMING_SNAKE)

## The Golden Rule

**Case communicates intent:**
- `PascalCase` = FFI boundary (unsafe, matches C)
- `snake_case` = Safe Novus code (idiomatic, Result-based)
- `SCREAMING_SNAKE` = Constants (compile-time values)

When in doubt, ask: "Am I calling C, or calling Novus?"
