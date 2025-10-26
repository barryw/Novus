# FFI-Compatible Memory Management for Novus

## Design Goal

**Safe and modern by default, with escape hatches for manual control.**

- Raw pointers (`*T`) for FFI and manual management (The Old Way™)
- `Box<T>` for automatic cleanup of owned heap allocations
- `Rc<T>` for simple shared ownership (no borrow checker needed)
- Seamless interop with NDK functions
- Build safe wrappers on top of raw FFI

---

## Three Types of Pointers

### 1. Raw Pointer: `*T` (The Old Way™)

**No automatic cleanup. You're in charge.**

```novus
fn manual_memory() -> i32 {
    // Raw pointer - you manage it
    var mem: *u8 = AllocMem(1024, MEMF_CHIP | MEMF_CLEAR)

    if mem == null {
        return -1
    }

    // Use mem...

    // YOU must free it!
    FreeMem(mem, 1024)

    return 0
}
```

✅ Direct FFI interop
✅ Zero abstraction
❌ Easy to leak
❌ Easy to double-free
❌ Easy to use-after-free

**Use for:** Direct NDK calls, performance-critical code, when you need full control

---

### 2. Box<T> (Owned Pointer)

**Automatic cleanup when it goes out of scope. Single owner.**

```novus
fn safe_memory() -> i32 {
    // Box - automatically freed at end of scope
    var mem = Box.alloc(1024, MEMF_CHIP | MEMF_CLEAR)?

    // Use mem...
    mem[0] = 42

    return 0
}  // <-- FreeMem called automatically here!
```

✅ Automatic cleanup
✅ Can't forget to free
✅ Can't double-free
⚠️  Can't share (single owner)

**Use for:** Most allocations, buffers, temporary data

---

### 3. Rc<T> (Reference Counted Pointer)

**Shared ownership. Memory freed when last reference drops.**

```novus
fn shared_memory() {
    var data = Rc.new(ExpensiveStruct { ... })

    var copy1 = data.clone()  // ref_count = 2
    var copy2 = data.clone()  // ref_count = 3

    process(copy1)  // ref_count = 2 after this scope

}  // All refs dropped, ref_count = 0, memory freed
```

✅ Multiple owners
✅ Automatic cleanup when last owner drops
⚠️  Small overhead (ref counting)
⚠️  Can create cycles (use Weak<T> to break them)

**Use for:** Shared data structures, caches, object graphs

---

## Working with NDK Functions

### The Core Pattern

**NDK functions return raw pointers. You choose how to manage them.**

```novus
// std/ffi/exec.novus - Raw FFI layer
extern fn AllocMem(size: u32, flags: u32) -> *u8
extern fn FreeMem(ptr: *u8, size: u32)
extern fn OpenLibrary(name: *u8, version: u32) -> *Library
extern fn CloseLibrary(lib: *Library)
```

### Example 1: Wrapping AllocMem with Box

**Safe wrapper:**

```novus
// std/mem.novus - Safe wrapper layer
use exec::*

pub struct Box<T> {
    ptr: *T
    size: u32
    flags: u32
}

impl Box<T> {
    // Allocate memory and wrap in Box for automatic cleanup
    pub fn alloc(size: u32, flags: u32) -> Result<Box<T>, MemError> {
        var ptr = AllocMem(size, flags)

        if ptr == null {
            return Err(MemError.OutOfMemory)
        }

        return Ok(Box { ptr: ptr as *T, size: size, flags: flags })
    }

    // Convert Box to raw pointer (gives up ownership)
    pub fn into_raw(self) -> *T {
        var ptr = self.ptr
        forget(self)  // Don't call drop on self
        return ptr
    }

    // Wrap existing raw pointer in Box (takes ownership)
    pub unsafe fn from_raw(ptr: *T, size: u32, flags: u32) -> Box<T> {
        return Box { ptr: ptr, size: size, flags: flags }
    }

    // Access the raw pointer (doesn't give up ownership)
    pub fn as_ptr(&self) -> *T {
        return self.ptr
    }

    // Automatic cleanup
    pub fn drop(&mut self) {
        if self.ptr != null {
            FreeMem(self.ptr as *u8, self.size)
        }
    }
}
```

**Usage:**

```novus
fn example1() -> i32 {
    // Safe: Box automatically calls FreeMem
    var buffer = Box.alloc(1024, MEMF_CHIP | MEMF_CLEAR)?

    // Use it
    buffer.ptr[0] = 42

    return 0
}  // FreeMem called automatically


fn example2() -> i32 {
    // Manual: You control everything
    var buffer = AllocMem(1024, MEMF_CHIP | MEMF_CLEAR)

    if buffer == null {
        return -1
    }

    // Use it
    buffer[0] = 42

    // Don't forget!
    FreeMem(buffer, 1024)

    return 0
}
```

---

### Example 2: Wrapping OpenLibrary

**Problem:** NDK libraries need to be opened and closed. Easy to forget `CloseLibrary`.

**Safe wrapper:**

```novus
// std/exec.novus - Safe wrapper
use ffi::exec::*

pub struct Library {
    base: *exec.Library  // Raw pointer to library base
    name: str
}

impl Library {
    pub fn open(name: str, version: u32) -> Result<Library, LibError> {
        var base = OpenLibrary(name.as_ptr(), version)

        if base == null {
            return Err(LibError.NotFound)
        }

        return Ok(Library { base: base, name: name })
    }

    // Get raw pointer for calling library functions
    pub fn base(&self) -> *exec.Library {
        return self.base
    }

    // Automatic cleanup
    pub fn drop(&mut self) {
        if self.base != null {
            CloseLibrary(self.base)
        }
    }
}
```

**Usage:**

```novus
fn safe_way() -> Result<(), Error> {
    // Library automatically closed at end of scope
    var dos_lib = Library.open("dos.library", 0)?

    // Use the library...
    var output = Output()  // dos.library function

    return Ok(())
}  // CloseLibrary called automatically


fn manual_way() -> i32 {
    // The Old Way™ - you manage it
    var dos_base = OpenLibrary("dos.library", 0)

    if dos_base == null {
        return -1
    }

    // Use the library...
    var output = Output()

    // Don't forget!
    CloseLibrary(dos_base)

    return 0
}
```

---

### Example 3: Sharing with Rc<T>

**Problem:** Multiple parts of your program need access to the same library.

```novus
// std/mem.novus
pub struct Rc<T> {
    ptr: *RcBox<T>
}

struct RcBox<T> {
    ref_count: u32
    value: T
}

impl Rc<T> {
    pub fn new(value: T) -> Rc<T> {
        // Allocate space for ref count + value
        var size = sizeof(RcBox<T>)
        var ptr = AllocMem(size, MEMF_PUBLIC | MEMF_CLEAR) as *RcBox<T>

        ptr.ref_count = 1
        ptr.value = value

        return Rc { ptr: ptr }
    }

    pub fn clone(&self) -> Rc<T> {
        self.ptr.ref_count = self.ptr.ref_count + 1
        return Rc { ptr: self.ptr }
    }

    pub fn get(&self) -> &T {
        return &self.ptr.value
    }

    pub fn drop(&mut self) {
        if self.ptr == null {
            return
        }

        self.ptr.ref_count = self.ptr.ref_count - 1

        if self.ptr.ref_count == 0 {
            // Call T's drop if it has one
            drop(&mut self.ptr.value)

            // Free the allocation
            FreeMem(self.ptr as *u8, sizeof(RcBox<T>))
        }
    }
}
```

**Usage with shared library:**

```novus
struct App {
    dos: Rc<Library>
    graphics: Rc<Library>
}

fn create_app() -> Result<App, Error> {
    var dos = Rc.new(Library.open("dos.library", 0)?)
    var graphics = Rc.new(Library.open("graphics.library", 0)?)

    return Ok(App { dos: dos, graphics: graphics })
}

fn use_app(app: &App) {
    // Both subsystems can share the libraries
    var subsystem1 = SubSystem1 { dos: app.dos.clone() }
    var subsystem2 = SubSystem2 { dos: app.dos.clone() }

    // Libraries automatically closed when last Rc drops
}
```

---

## Real-World Example: Opening a Window

**Raw FFI way (The Old Way™):**

```novus
use ffi::intuition::*
use ffi::graphics::*

fn open_window_manual() -> i32 {
    var intuition_base = OpenLibrary("intuition.library", 0)
    if intuition_base == null {
        return -1
    }

    var graphics_base = OpenLibrary("graphics.library", 0)
    if graphics_base == null {
        CloseLibrary(intuition_base)  // Must clean up!
        return -1
    }

    var window = OpenWindow(&NewWindow {
        left: 0,
        top: 0,
        width: 320,
        height: 200,
        // ... more fields ...
    })

    if window == null {
        CloseLibrary(graphics_base)
        CloseLibrary(intuition_base)
        return -1
    }

    // Use window...

    // Cleanup (easy to forget or get wrong order!)
    CloseWindow(window)
    CloseLibrary(graphics_base)
    CloseLibrary(intuition_base)

    return 0
}
```

**Safe wrapper way:**

```novus
use intuition::*  // Safe wrapper
use graphics::*

fn open_window_safe() -> Result<(), Error> {
    var intuition = Library.open("intuition.library", 0)?
    var graphics = Library.open("graphics.library", 0)?

    var window = Window.open(NewWindowSpec {
        left: 0,
        top: 0,
        width: 320,
        height: 200,
        // ... more fields ...
    })?

    // Use window...

    return Ok(())
}  // window closed, libraries closed - all automatic!
```

---

## Conversion Between Pointer Types

### Raw → Box (unsafe)

```novus
// You're promising this pointer is valid and you own it
let raw_ptr = AllocMem(1024, MEMF_CHIP)
let boxed = unsafe { Box.from_raw(raw_ptr, 1024, MEMF_CHIP) }
// Box will call FreeMem when dropped
```

### Box → Raw

```novus
// Give up ownership, you're responsible now
let boxed = Box.alloc(1024, MEMF_CHIP)?
let raw_ptr = boxed.into_raw()
// Must manually call FreeMem(raw_ptr, 1024)
```

### Box → Rc (for sharing)

```novus
let boxed = Box.alloc(1024, MEMF_CHIP)?
let shared = Rc.new(boxed)
// Can now clone and share
let copy = shared.clone()
```

---

## FFI Function Patterns

### Pattern 1: Function Returns Pointer You Own

```novus
// NDK: extern fn AllocMem(size: u32, flags: u32) -> *u8

// Safe wrapper:
pub fn alloc_mem(size: u32, flags: u32) -> Result<Box<u8>, MemError> {
    var ptr = AllocMem(size, flags)
    if ptr == null {
        return Err(MemError.OutOfMemory)
    }
    return Ok(unsafe { Box.from_raw(ptr, size, flags) })
}
```

### Pattern 2: Function Takes Pointer, Doesn't Take Ownership

```novus
// NDK: extern fn CopyMem(source: *u8, dest: *u8, size: u32)

// Safe wrapper:
pub fn copy_mem(source: &[u8], dest: &mut [u8]) {
    CopyMem(source.as_ptr(), dest.as_mut_ptr(), source.len())
}

// Or if you have Box:
let src_box = Box.alloc(1024, MEMF_PUBLIC)?
let dst_box = Box.alloc(1024, MEMF_PUBLIC)?
CopyMem(src_box.as_ptr(), dst_box.as_ptr(), 1024)
// Both boxes still valid, will be freed automatically
```

### Pattern 3: Function Takes Ownership of Pointer

```novus
// Some NDK function that takes ownership:
// extern fn AddToList(list: *List, node: *Node)
// The list now owns the node - you shouldn't free it

// Safe wrapper:
pub fn add_to_list(list: &mut List, node: Box<Node>) {
    // Give up ownership
    var raw_node = node.into_raw()
    AddToList(list.ptr, raw_node)
    // Don't drop node, list owns it now
}
```

### Pattern 4: Function Returns Borrowed Pointer

```novus
// NDK: extern fn FindTask(name: *u8) -> *Task
// Returns pointer to system-owned task - you don't free this!

// Safe wrapper:
pub fn find_task(name: Option<&str>) -> Option<&Task> {
    var name_ptr = match name {
        Some(n) => n.as_ptr(),
        None => null
    }

    var task_ptr = FindTask(name_ptr)

    if task_ptr == null {
        return None
    }

    // Return reference, not Box (we don't own it)
    return Some(unsafe { &*task_ptr })
}
```

---

## Implementation Plan

### Phase 1: Raw Pointer Foundation (Current State)
- ✅ Raw pointers work with FFI
- ✅ Manual AllocMem/FreeMem works

### Phase 2: Add Box<T> Type
```novus
// Built-in Box type in compiler

// In type system:
class IrBoxType : IrType {
    public IrType InnerType { get; }
    public int Size { get; }
    public int Flags { get; }
}

// User declares:
let buf: Box<u8> = Box.alloc(1024, MEMF_CHIP)?

// Compiler tracks:
// - buf owns the memory
// - buf.drop() must be called at scope exit
```

### Phase 3: Implement Drop Tracking
```csharp
// In IrBuilder.cs
class ScopeManager {
    Stack<Scope> _scopes = new();

    class Scope {
        List<(string name, IrType type)> OwnedValues = new();
    }

    void EnterScope() {
        _scopes.Push(new Scope());
    }

    void ExitScope() {
        var scope = _scopes.Pop();

        // Insert drop calls for all owned values (in reverse order)
        foreach (var (name, type) in scope.OwnedValues.Reverse()) {
            if (type is IrBoxType boxType) {
                InsertBoxDrop(name, boxType);
            } else if (HasDropMethod(type)) {
                InsertCustomDrop(name, type);
            }
        }
    }
}
```

### Phase 4: Generate Cleanup Code
```csharp
// In M68kCodeGenerator.cs
void GenerateBoxDrop(string varName, IrBoxType boxType) {
    // Load pointer from variable
    Emit($"\tmove.l\t{GetVarLocation(varName)},-(sp)");

    // Load size
    Emit($"\tmove.l\t#{boxType.Size},-(sp)");

    // Call FreeMem
    Emit($"\tjsr\t_FreeMem");
    Emit($"\tlea\t8(sp),sp");
}
```

### Phase 5: Add Rc<T> to Standard Library
```novus
// std/mem/rc.novus
pub struct Rc<T> {
    ptr: *RcBox<T>
}

// Implemented in Novus, not compiler magic
// Just uses Box and manual ref counting
```

---

## Documentation Structure

```
std/
  ffi/              # Raw FFI layer (unsafe, manual)
    exec.novus      # Raw extern declarations
    dos.novus
    intuition.novus
    graphics.novus

  mem/              # Memory management (safe wrappers)
    box.novus       # Box<T> utilities
    rc.novus        # Rc<T> implementation

  exec/             # Safe wrappers for exec
    library.novus   # Library type with auto-close
    memory.novus    # Memory allocation wrappers

  dos/              # Safe wrappers for dos
    file.novus      # File type with auto-close

  intuition/        # Safe wrappers for intuition
    window.novus    # Window type with auto-close
    screen.novus
```

---

## Examples Side-by-Side

### Example: Writing a File

**The Old Way™:**
```novus
use ffi::dos::*

fn write_file_manual(path: str, data: str) -> i32 {
    var dos_base = OpenLibrary("dos.library", 0)
    if dos_base == null {
        return -1
    }

    var file = Open(path.as_ptr(), MODE_NEWFILE)
    if file == 0 {
        CloseLibrary(dos_base)
        return -1
    }

    var result = Write(file, data.as_ptr(), data.len())

    Close(file)
    CloseLibrary(dos_base)

    return if result < 0 { -1 } else { 0 }
}
```

**The New Way:**
```novus
use dos::*

fn write_file_safe(path: str, data: str) -> Result<(), IoError> {
    var file = File.create(path)?
    file.write(data)?
    return Ok(())
}  // file closed automatically, dos.library closed automatically
```

---

## Summary

### For Users:

1. **Use safe wrappers by default** - `use dos::*` not `use ffi::dos::*`
2. **Box for single ownership** - Automatic cleanup
3. **Rc for shared ownership** - Simple ref counting
4. **Raw pointers when needed** - The Old Way™ always available

### For Library Authors:

1. **std/ffi/** has raw FFI - unsafe but direct
2. **std/** has safe wrappers - build on top of ffi
3. **Mark unsafe** functions that work with raw pointers
4. **Provide both** - raw for power users, safe for everyone else

### Benefits:

✅ Works seamlessly with NDK
✅ Safe by default
✅ Escape hatch available
✅ Zero overhead when using Box
✅ Explicit allocations (Box, Rc)
✅ No scary borrow checker
✅ No GC runtime
✅ Perfect for Amiga

---

## Next Steps

1. Implement Box<T> type in compiler
2. Add drop tracking and insertion
3. Create safe wrappers for common libraries
4. Write examples and documentation
5. Add Rc<T> to standard library

Want to start implementing this?
