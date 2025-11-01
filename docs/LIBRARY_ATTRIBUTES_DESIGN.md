# Library Attributes and Safety System Design

## Vision

Make library creation **delightfully simple** while keeping footguns locked in a safe. The compiler does the hard work so developers don't have to.

**"Scrubbing Bubbles" Philosophy**: We work hard so you don't have to!

## Goals

1. **Safe by default**: Impossible to guru without explicitly opting into danger
2. **Zero boilerplate**: No ROMTags, no wrappers, no manual offset tracking
3. **Delight developers**: "Holy shit, this is cool!" moments
4. **Escape hatches**: Power users can still do anything with `unsafe {}`

---

## Three Safety Tiers

### Tier 1: Safe by Default 🔒

```novus
@library(name = "example.library", version = 1)
pub struct ExampleLibrary {
    counter: u32,
}

impl ExampleLibrary {
    pub fn increment() -> u32 {
        self.counter += 1
        return self.counter
    }
}
```

**Compiler generates:**
- ✅ ROMTag with magic word 0x4AFC
- ✅ Library base with standard Library header prepended
- ✅ AutoInit structure
- ✅ Function vector table (offsets auto-assigned)
- ✅ A6 calling convention wrappers
- ✅ Default open/close/expunge implementations
- ✅ open_count auto-management
- ✅ Thread safety (auto-adds Forbid/Permit)

**Footguns locked:**
- ❌ Can't use raw AllocMem/FreeMem
- ❌ Can't manipulate open_count manually
- ❌ Can't cast pointers arbitrarily
- ❌ Can't index arrays out of bounds
- ❌ Can't forget to close opened libraries
- ❌ Can't use inline assembly

**What's allowed:**
- ✅ Safe memory APIs (Allocation<T>, Box<T>)
- ✅ Option/Result types
- ✅ defer blocks
- ✅ All normal Novus code

### Tier 2: Supervised Manual Control 🔓

```novus
@library(name = "example.library", version = 1)
pub struct ExampleLibrary {
    buffer: Option<Allocation<u8>>,
}

impl ExampleLibrary {
    pub fn allocate(size: u32) -> bool {
        // Supervised: must use Allocation<T>, not raw AllocMem
        self.buffer = Allocation::new(size, MEMF_FAST)

        match self.buffer {
            Some(ref mut buf) => {
                // Compiler tracks lifetime
                return true
            },
            None => return false
        }
    }

    pub fn cleanup() {
        // Compiler enforces: must check if Some before drop
        if let Some(ref mut buf) = self.buffer {
            buf.drop()
            self.buffer = None
        }
    }
}
```

**Footguns available but supervised:**
- ✅ Allocation<T> (size tracked automatically)
- ✅ Box<T> (RAII-style ownership)
- ✅ Manual lifetime management
- ⚠️ Compiler tracks allocations and warns about leaks
- ⚠️ Compiler errors on use-after-free

### Tier 3: Unsafe - Full Power 🔫

```novus
@library(name = "example.library", version = 1)
pub struct ExampleLibrary {
    raw_copper_list: i32,
}

impl ExampleLibrary {
    pub fn setup_copper() {
        unsafe {
            // Full power unlocked

            // Raw FFI
            let addr: i32 = AllocMem(1000, MEMF_CHIP)
            self.raw_copper_list = addr

            // Pointer manipulation
            let ptr: *u32 = addr as *u32
            *ptr = 0x01000000  // WAIT
            *(ptr + 1) = 0xFFFFFFFF  // End

            // Direct hardware access
            *(0xDFF080 as *u32) = addr  // COP1LC

            // Inline assembly (future)
            asm {
                move.l  d0,a0
                jsr     (a0)
            }
        }

        // Outside unsafe: COMPILER ERROR
        // let x = AllocMem(100, 1)  // ❌ AllocMem requires unsafe
    }
}
```

**Everything unlocked:**
- ✅ Raw FFI (AllocMem, FreeMem, OpenLibrary, etc.)
- ✅ Arbitrary pointer casts
- ✅ Direct hardware register access
- ✅ Inline assembly blocks
- ✅ Manual open_count manipulation
- ✅ Untracked allocations
- 🔫 You're on your own - compiler trusts you

---

## Library Attributes

### @library

Marks a struct as an AmigaOS library.

```novus
@library(
    name = "example.library",      // Required: Library name (must end in .library)
    version = 1,                   // Required: Library version
    revision = 0,                  // Optional: Revision number (default: 0)
    id = "example.library 1.0 (01 Nov 2025)",  // Optional: ID string (auto-generated if omitted)
    priority = 0,                  // Optional: Init priority (default: 0)
    abi_compatible_with = [1]      // Optional: List of compatible versions
)
pub struct MyLibrary {
    // Custom fields only - Library header auto-prepended
    my_data: u32,
}
```

**Compiler behavior:**
1. Prepends standard `Library` header as first field
2. Generates ROMTag structure
3. Generates AutoInit structure
4. Generates function vector table
5. Auto-assigns vector offsets in declaration order
6. Generates A6→stack wrappers for all functions

### @libfunc (Optional)

Explicitly marks a function as a library function. **Usually not needed** - compiler auto-detects public functions in impl block.

```novus
impl MyLibrary {
    // Auto-detected as library function (public in impl block)
    pub fn do_something() -> i32 {
        return 42
    }

    // Explicitly marked (optional, same behavior)
    @libfunc
    pub fn do_another_thing() -> i32 {
        return 99
    }

    // Private helper - NOT a library function
    fn internal_helper() -> i32 {
        return 1
    }
}
```

**Vector offset assignment:**
- Functions assigned offsets in **declaration order**
- First user function: -30
- Second user function: -36
- Third user function: -42
- etc.

### Smart Function Name Recognition

Compiler recognizes standard library lifecycle functions by name:

```novus
impl MyLibrary {
    // Recognized as LibInit (called during library initialization)
    pub fn init(seglist: u32) -> Option<*MyLibrary> {
        // Your init code
        // Compiler auto-generates if not provided
    }

    // Recognized as LibOpen (vector offset -6)
    pub fn open(version: u32) -> bool {
        // Return true = success (compiler returns self)
        // Return false = failure (compiler returns null)
        // Compiler auto-generates if not provided
    }

    // Recognized as LibClose (vector offset -12)
    pub fn close() {
        // Compiler auto-generates if not provided
        // Auto-checks for delayed expunge
    }

    // Recognized as LibExpunge (vector offset -18)
    pub fn expunge() -> u32 {
        // Compiler auto-generates if not provided
        // Auto-checks open_count, frees memory
    }

    // All other public functions are user-callable library functions
    pub fn my_function() -> i32 {
        return 42
    }
}
```

**If you don't provide lifecycle functions:**
- Compiler generates default implementations
- `init()`: Allocates library base, zeros custom fields
- `open()`: Increments open_count, returns self
- `close()`: Decrements open_count, checks delayed expunge
- `expunge()`: Checks open_count, frees memory, returns seglist

### @since (Version Tracking)

Marks features added in specific versions:

```novus
@library(name = "example.library", version = 2)
pub struct MyLibrary {
    counter: u32,

    @since(version = 2)
    new_field: u32,
}

impl MyLibrary {
    pub fn old_function() -> i32 {
        return 42
    }

    @since(version = 2)
    pub fn new_function() -> i32 {
        return self.new_field
    }
}
```

**Compiler behavior:**
- Validates vector offset stability (warns if functions reordered)
- Can generate version-specific vector tables
- Documentation shows which version introduced each function

### @threadsafe / @singlethreaded

Explicit thread safety control:

```novus
impl MyLibrary {
    // Compiler auto-adds Forbid()/Permit()
    @threadsafe
    pub fn increment_counter() {
        self.counter += 1
    }

    // No locking overhead
    @singlethreaded
    pub fn fast_read() -> u32 {
        return self.counter
    }

    // Default: compiler infers based on usage
    pub fn normal_function() -> i32 {
        return 42  // Read-only, no locking needed
    }
}
```

### @deprecated

Mark functions as deprecated:

```novus
impl MyLibrary {
    @deprecated(
        since = "2.0",
        note = "Use new_api() instead"
    )
    pub fn old_api() -> i32 {
        return 42
    }
}
```

**Compiler behavior:**
- Warnings when calling deprecated functions
- Documentation marks them clearly
- Still included in vector table (ABI compatibility)

---

## Unsafe System

### What Requires `unsafe {}`

**Raw FFI functions:**
```novus
// These require unsafe blocks:
AllocMem()      // Raw allocation (can leak, double-free)
FreeMem()       // Raw deallocation (can double-free, wrong size)
OpenLibrary()   // Manual library management (can leak)
CloseLibrary()  // Manual library management (can close wrong base)
OpenDevice()    // Manual device management
CloseDevice()   // Manual device management

// Direct hardware register access
*(0xDFF180 as *u16) = 0x0F00  // Custom chips

// Inline assembly (future)
asm { ... }
```

**Safe alternatives:**
```novus
// These are safe (no unsafe required):
Allocation::new()    // Tracked allocation
Box::new()          // Owned heap value
defer block.drop()  // RAII cleanup
Option::from_ptr()  // Safe null handling
```

### Unsafe Block Tracking

```novus
pub fn my_function() {
    // Safe code here

    unsafe {
        // Unsafe code here
        let ptr = AllocMem(100, MEMF_PUBLIC)
        // ...
        FreeMem(ptr, 100)
    }

    // Safe code again
}
```

**Compiler tracking:**
1. Tracks "unsafe context" state during semantic analysis
2. Errors if unsafe operations used outside `unsafe {}`
3. Warns about unsafe blocks (count, locations)
4. Build summary shows unsafe usage

**Example compiler output:**
```
Building example.library...
⚠ WARNING: 3 unsafe blocks detected
  - lib.novus:45: unsafe block (15 lines)
  - lib.novus:102: unsafe block (8 lines)
  - lib.novus:200: unsafe block (3 lines)

⚠ Unsafe code bypasses safety checks
⚠ Manual review required

Continue? [y/N]
```

### Unsafe Propagation

Unsafe blocks do **NOT** propagate:

```novus
pub fn safe_wrapper() {
    unsafe {
        do_unsafe_thing()
    }
}

pub fn caller() {
    // This is OK - safe_wrapper contains the unsafe
    safe_wrapper()  // No unsafe block needed here
}
```

This allows building safe abstractions on top of unsafe primitives.

---

## Auto-Generated Code

### What the Compiler Generates

For this simple library:

```novus
@library(name = "example.library", version = 1)
pub struct ExampleLibrary {
    counter: u32,
}

impl ExampleLibrary {
    pub fn increment() -> u32 {
        self.counter += 1
        return self.counter
    }
}
```

**Compiler generates:**

1. **Expanded struct** (internal representation):
```novus
pub struct ExampleLibrary {
    lib: Library,        // Auto-prepended
    counter: u32,        // User's field
    open_count: u32,     // Auto-added for tracking
}
```

2. **Default lifecycle functions** (if not provided):
```novus
pub fn init(seglist: u32) -> *ExampleLibrary {
    let size = @sizeof(ExampleLibrary)
    let ptr = AllocMem(size, MEMF_PUBLIC | MEMF_CLEAR)
    if ptr == 0 { return 0 }
    let base = ptr as *ExampleLibrary
    (*base).seglist = seglist
    (*base).open_count = 0
    return base
}

pub fn open(version: u32) -> *ExampleLibrary {
    (*base).open_count += 1
    return base
}

pub fn close() -> u32 {
    (*base).open_count -= 1
    if (*base).open_count == 0 && LIBF_DELEXP_set {
        return expunge()
    }
    return 0
}

pub fn expunge() -> u32 {
    if (*base).open_count > 0 {
        (*base).lib.lib_Flags |= LIBF_DELEXP
        return 0
    }
    let seglist = (*base).seglist
    FreeMem(base, @sizeof(ExampleLibrary))
    return seglist
}
```

3. **Assembly wrappers** (library_base.s):
```asm
ROMTag:
    dc.w    $4AFC
    dc.l    ROMTag
    dc.l    EndCode
    dc.b    $81
    dc.b    1
    dc.b    9
    dc.b    0
    dc.l    LibName
    dc.l    LibIDString
    dc.l    AutoInit

LibName:
    dc.b    'example.library',0

LibIDString:
    dc.b    'example.library 1.0',13,10,0

FuncTable:
    dc.l    _LibOpen
    dc.l    _LibClose
    dc.l    _LibExpunge
    dc.l    _LibReserved
    dc.l    _increment
    dc.l    -1
```

4. **A6 wrappers** (wrappers.s):
```asm
_increment:
    movem.l d2-d7/a2-a6,-(sp)
    move.l  a6,-(sp)
    jsr     _novus_increment
    addq.l  #4,sp
    movem.l (sp)+,d2-d7/a2-a6
    rts
```

**Total generated code:** ~200-300 lines of assembly + lifecycle functions

**Developer wrote:** 11 lines of Novus

---

## Self Keyword

The `self` keyword provides library-aware context:

```novus
impl ExampleLibrary {
    pub fn increment() -> u32 {
        // 'self' refers to library base (*ExampleLibrary)
        self.counter += 1
        return self.counter
    }

    pub fn get_counter() -> u32 {
        // Read-only access
        return self.counter
    }

    pub fn reset() {
        // Mutable access
        self.counter = 0
    }
}
```

**Compiler translation:**
```c
// What compiler generates internally:
u32 novus_increment(ExampleLibrary* base) {
    base->counter += 1;
    return base->counter;
}
```

The `self` parameter is implicit - compiler adds it automatically.

---

## Dependent Library Auto-Management

**Compiler scans for library usage:**

```novus
impl ExampleLibrary {
    pub fn print_message(msg: *u8) {
        // Compiler sees: println uses dos.library
        println("Message: {}", msg)

        // Compiler sees: EasyRequest uses intuition.library
        let req = EasyRequest(null, &easy_struct, null, msg)
    }
}
```

**Compiler auto-generates:**

1. **Hidden fields in struct:**
```novus
pub struct ExampleLibrary {
    lib: Library,
    counter: u32,
    // Auto-added by compiler:
    _dos_base: *u8,
    _intuition_base: *u8,
}
```

2. **Auto-open in init:**
```novus
pub fn init(seglist: u32) -> *ExampleLibrary {
    // ... allocate base ...

    // Auto-generated:
    (*base)._dos_base = OpenLibrary("dos.library", 0)
    if (*base)._dos_base == 0 {
        FreeMem(base, size)
        return 0
    }

    (*base)._intuition_base = OpenLibrary("intuition.library", 0)
    if (*base)._intuition_base == 0 {
        CloseLibrary((*base)._dos_base)
        FreeMem(base, size)
        return 0
    }

    return base
}
```

3. **Auto-close in expunge:**
```novus
pub fn expunge() -> u32 {
    // ... check open_count ...

    // Auto-generated cleanup:
    if (*base)._intuition_base != 0 {
        CloseLibrary((*base)._intuition_base)
    }
    if (*base)._dos_base != 0 {
        CloseLibrary((*base)._dos_base)
    }

    // ... free base ...
}
```

**Developer writes:** Just uses the functions

**Compiler handles:** Opening, storing bases, closing, error handling

---

## Build Output

### Safe Build (No Unsafe)

```bash
$ novusc build

Building example.library...
✓ Parsing complete
✓ Semantic analysis complete
✓ No unsafe blocks detected
✓ Memory safety verified
✓ Generating library code...
  - ROMTag structure
  - Function vectors (6 functions)
  - A6 calling convention wrappers
  - Lifecycle functions (using defaults)
✓ Dependent libraries detected:
  - dos.library (auto-managed)
✓ Assembling...
✓ Linking...

Output: build/example.library (4,128 bytes)

Library Summary:
  Name: example.library
  Version: 1.0
  Functions: 6 (4 lifecycle + 2 user)
  Safety: 100% safe (no unsafe blocks)
  Dependencies: dos.library
```

### Unsafe Build (With Unsafe Blocks)

```bash
$ novusc build

Building advanced.library...
✓ Parsing complete
✓ Semantic analysis complete
⚠ WARNING: 3 unsafe blocks detected
  Location          Lines  Reason
  ────────────────────────────────────────────────
  lib.novus:45      15     Raw copper list setup
  lib.novus:102     8      Direct hardware access
  lib.novus:200     3      Manual memory management

⚠ Unsafe code bypasses safety checks
⚠ Manual review required for:
  - Memory leaks
  - Use-after-free
  - Double-free
  - Null pointer dereference
  - Hardware conflicts

Continue? [y/N] y

✓ Generating library code...
✓ Assembling...
✓ Linking...

Output: build/advanced.library (8,456 bytes)

Library Summary:
  Name: advanced.library
  Version: 2.0
  Functions: 12 (4 lifecycle + 8 user)
  Safety: 75% safe (3 unsafe blocks in 3 functions)
  Dependencies: dos.library, graphics.library

⚠ UNSAFE CODE PRESENT - Use with caution
```

---

## Error Messages

### Using Unsafe Operation Outside Unsafe Block

```novus
pub fn oops() {
    let ptr = AllocMem(100, MEMF_PUBLIC)  // ❌
}
```

```
error[E1001]: unsafe operation requires unsafe block
  ┌─ lib.novus:45:15
  │
45│     let ptr = AllocMem(100, MEMF_PUBLIC)
  │               ^^^^^^^^^^^^^^^^^^^^^^^^^^
  │               |
  │               AllocMem is an unsafe FFI function
  │
  = note: AllocMem returns raw addresses and can leak memory
  = help: wrap this call in an unsafe block:

    unsafe {
        let ptr = AllocMem(100, MEMF_PUBLIC)
        // ... use ptr ...
        FreeMem(ptr, 100)
    }

  = help: or use safe alternative: Allocation::new(100, MEMF_PUBLIC)
```

### Inline Assembly Outside Unsafe

```novus
pub fn oops() {
    asm {
        move.l d0,d1
    }
}
```

```
error[E1002]: inline assembly requires unsafe block
  ┌─ lib.novus:52:5
  │
52│     asm {
  │     ^^^ inline assembly is inherently unsafe
  │
  = note: assembly can:
    - Access any register or memory
    - Corrupt stack or heap
    - Violate calling conventions
    - Cause hardware conflicts

  = help: wrap assembly in unsafe block:

    unsafe {
        asm {
            move.l d0,d1
        }
    }
```

### Library Without @library Attribute

```novus
pub struct MyLibrary {}
impl MyLibrary {
    pub fn do_thing() {}
}
```

```
error[E1003]: library requires @library attribute
  ┌─ lib.novus:10:1
  │
10│ pub struct MyLibrary {}
  │ ^^^^^^^^^^^^^^^^^^^^^^^ missing @library attribute
  │
  = note: this appears to be a library but lacks required metadata
  = help: add @library attribute:

    @library(name = "my.library", version = 1)
    pub struct MyLibrary {}
```

---

## Implementation Plan

### Phase 1: Unsafe System Foundation

1. **Add unsafe context tracking to SemanticAnalyzer**
   - Track when inside unsafe blocks
   - Error on unsafe operations outside unsafe blocks
   - Collect unsafe block locations for warnings

2. **Mark FFI functions as unsafe**
   - AllocMem, FreeMem, OpenLibrary, CloseLibrary
   - All raw pointer manipulation
   - Direct hardware access

3. **Add compiler warnings**
   - Count unsafe blocks
   - Show locations
   - Prompt for confirmation

### Phase 2: Library Attributes

1. **Add @library attribute parsing**
   - Extract name, version, revision, etc.
   - Validate library name (must end in .library)
   - Store metadata for code generation

2. **Implement smart function recognition**
   - Detect init/open/close/expunge by name
   - Auto-assign vector offsets
   - Validate signatures

3. **Add struct expansion**
   - Prepend Library header
   - Add open_count field
   - Add dependent library base fields

### Phase 3: Code Generation

1. **Generate ROMTag structure**
   - Magic word, library name, version
   - Point to AutoInit structure

2. **Generate function vector table**
   - Standard functions at fixed offsets
   - User functions in declaration order
   - Terminator

3. **Generate A6 wrappers**
   - For each library function
   - Translate A6 → stack calling convention
   - Preserve registers

4. **Generate default lifecycle functions**
   - If user doesn't provide them
   - Memory allocation/deallocation
   - open_count management

### Phase 4: Advanced Features

1. **Auto-library dependency detection**
   - Scan for library function usage
   - Auto-generate OpenLibrary/CloseLibrary
   - Track bases as hidden fields

2. **Thread safety analysis**
   - Detect shared mutable state
   - Auto-add locking where needed
   - Respect @threadsafe/@singlethreaded

3. **Version tracking**
   - @since attribute
   - ABI compatibility validation
   - Function reordering detection

---

## Future Enhancements

### Hot Reload Support

```novus
@library(
    name = "example.library",
    version = 2,
    hot_reload = true
)
```

Compiler generates migration code for seamless upgrades.

### Auto-Documentation

```bash
novusc build --generate-docs
```

Generates AutoDocs, .fd files, HTML documentation.

### Cross-Library Optimization

Link-time optimization for frequently-called library functions.

### Debug Mode Enhancements

```bash
novusc build --debug
```

Auto-generates:
- Call tracing
- Parameter validation
- Memory leak detection
- Open/close mismatch detection

---

## Summary

**What developers write:**
```novus
@library(name = "example.library", version = 1)
pub struct ExampleLibrary {
    counter: u32,
}

impl ExampleLibrary {
    pub fn increment() -> u32 {
        self.counter += 1
        return self.counter
    }
}
```

**What they get:**
- Complete library with ROMTag, vectors, wrappers
- Safe by default (no guru possible without unsafe)
- Zero boilerplate
- Full AmigaOS compatibility

**What they would have written in C:**
- 500+ lines across 6 files
- Manual ROMTag setup
- Manual wrapper functions
- Manual open_count tracking
- Easy to introduce bugs

**The difference:** 🤯

This is the "holy shit!" moment. This is why people will choose Novus over C.
