# References in Novus (The Simple Version)

## Philosophy

**Novus is for average developers, not language lawyers.**

Most of the time, you shouldn't think about memory management at all. Use `Box<T>` and `Rc<T>`, and memory "just works."

When you need to pass large structs around efficiently, use references. They're **just non-null pointers** - nothing scary.

When you need full manual control (or FFI), use raw pointers. They work like C pointers.

---

## Three Levels of Complexity

### Level 1: Box/Rc - "I Don't Want to Think About Memory" (90% of code)

```novus
fn process_image() -> Result<(), Error> {
    // Allocate memory - automatic cleanup
    var pixels = Box.alloc::<u8>(64000, MEMF_CHIP)?

    // Use it
    pixels[0] = 42

    // Share something
    var config = Rc.new(load_config()?)
    spawn_worker(config.clone())
    spawn_worker(config.clone())

    return Ok(())
}  // Everything freed automatically
```

**This is where most developers live. No pointers, no references, no thinking.**

---

### Level 2: References - "Don't Copy This Big Thing" (9% of code)

Sometimes you have a large struct and passing it by value would be wasteful:

```novus
struct ScreenBuffer {
    pixels: [u8; 64000]
    width: u16
    height: u16
}

// Without references (copies 64KB!)
fn clear_screen_slow(screen: ScreenBuffer) {
    for i in 0..64000 {
        screen.pixels[i] = 0  // Modifying copy, not original!
    }
}

// With references (passes 4-byte pointer)
fn clear_screen(screen: &mut ScreenBuffer) {
    for i in 0..64000 {
        screen.pixels[i] = 0  // Modifying original
    }
}

var screen = ScreenBuffer { /* ... */ }
clear_screen(&mut screen)  // Pass by reference
```

**References are just:**
- A way to say "pass the address, not a copy"
- Guaranteed non-null (unlike C pointers)
- That's it. No complex rules.

#### Read-only vs Mutable

```novus
// Read-only reference - can't modify
fn print_screen(screen: &ScreenBuffer) {
    println("Screen: {}x{}", screen.width, screen.height)
    // screen.width = 100  // ERROR: can't modify
}

// Mutable reference - can modify
fn resize_screen(screen: &mut ScreenBuffer, w: u16, h: u16) {
    screen.width = w   // OK
    screen.height = h
}
```

**That's the only rule: `&` is read-only, `&mut` can modify.**

---

### Level 3: Raw Pointers - The Old Way™ (1% of code)

For FFI or when you need full manual control:

```novus
use ffi::exec::*

fn manual_memory() -> i32 {
    // Old school C-style
    var ptr: *u8 = AllocMem(1024, MEMF_CHIP)

    if ptr == null {
        return -1
    }

    // Use ptr...
    ptr[0] = 42

    // Don't forget to free!
    FreeMem(ptr, 1024)

    return 0
}
```

**Raw pointers:**
- Can be null (you must check!)
- No automatic cleanup
- Required for FFI
- Escape hatch when you need it

---

## The Key Difference: References vs Pointers

| | Reference `&T` | Pointer `*T` |
|---|---|---|
| **Can be null?** | ❌ NO! | ✅ Yes |
| **Automatic cleanup?** | N/A (doesn't own) | ❌ No |
| **When to use?** | Passing large structs | FFI, manual control |
| **Check for null?** | ❌ Never needed | ✅ Always check |

---

## How References Stay Non-Null (No Borrow Checker!)

**Simple rule:** You can only create a reference from something that exists.

```novus
// ✅ These work - all valid sources:
var x = 10
let r1 = &x              // From variable

var arr = [1, 2, 3]
let r2 = &arr[0]         // From array

var point = Point { x: 10, y: 20 }
let r3 = &point.x        // From field

// ❌ These are errors - obvious problems:
let r = null             // ERROR: null is not allowed
let r = &*null_ptr       // ERROR: dereferencing null

var uninit: i32          // Not initialized
let r = &uninit          // ERROR: can't reference uninitialized
```

**No complex tracking. Just common sense checks.**

---

## Real World Examples

### Example 1: Graphics Library (Level 1 - Box)

```novus
use graphics::*

fn render_scene() -> Result<(), Error> {
    // Allocate screen buffer - automatic cleanup
    var screen = Box.alloc::<u8>(64000, MEMF_CHIP)?

    // Clear it
    for i in 0..64000 {
        screen[i] = 0
    }

    // Render sprites
    draw_sprite(&mut screen, sprite1)
    draw_sprite(&mut screen, sprite2)

    // Copy to screen
    copy_to_display(screen.as_ptr())

    return Ok(())
}  // screen freed automatically

fn draw_sprite(screen: &mut Box<u8>, sprite: &Sprite) {
    // screen is passed by reference - no 64KB copy!
    // ...
}
```

### Example 2: File I/O (Level 1 - defer + Box)

```novus
use ffi::dos::*

fn read_file(path: str) -> Result<Vec<u8>, Error> {
    // Open file
    var file = Open(path.as_ptr(), MODE_OLDFILE)
    if file == 0 {
        return Err(Error.CannotOpen)
    }
    defer { Close(file) }  // Auto-close

    // Allocate buffer
    var buffer = Box.alloc::<u8>(4096, MEMF_PUBLIC)?
    // Auto-freed at scope exit

    // Read
    var bytes = Read(file, buffer.as_ptr(), 4096)

    // Return data
    return Ok(buffer.to_vec())
}
```

### Example 3: NDK Wrapper (Level 2 - References)

```novus
pub struct Window {
    handle: *IntuitionWindow  // Raw pointer from NDK
}

impl Window {
    pub fn open(spec: &WindowSpec) -> Result<Window, Error> {
        // spec is passed by reference - no copy

        let handle = OpenWindow(spec.to_ndk_struct())
        if handle == null {
            return Err(Error.CannotOpen)
        }

        return Ok(Window { handle: handle })
    }

    // Method receiver is implicitly a reference
    pub fn clear(&mut self) {
        // self is &mut Window
        // No need to think about it!
    }

    pub fn width(&self) -> u16 {
        // self is &Window
        return self.handle.Width
    }
}

impl Drop for Window {
    fn drop(&mut self) {
        CloseWindow(self.handle)  // Auto-cleanup!
    }
}

// Usage - simple!
fn main() -> Result<(), Error> {
    var window = Window.open(&WindowSpec {
        width: 320,
        height: 200,
        // ...
    })?

    window.clear()
    println("Width: {}", window.width())

    return Ok(())
}  // window.drop() called automatically
```

### Example 4: Manual Control (Level 3 - Raw Pointers)

```novus
use ffi::exec::*

// Sometimes you WANT manual control
fn custom_pool_allocator() -> *PoolHeader {
    // Create custom memory pool
    var pool = CreatePool(MEMF_PUBLIC, 4096, 2048)

    if pool == null {
        return null
    }

    // Return raw pointer - caller manages it
    return pool
}

fn use_pool(pool: *PoolHeader) {
    if pool == null {
        return
    }

    // Allocate from pool
    var mem = AllocPooled(pool, 256)

    // Use mem...

    // Free back to pool
    FreePooled(pool, mem, 256)
}
```

---

## Guidelines

### When to use each:

**Use Box/Rc (Level 1) - Default choice:**
```novus
var buffer = Box.alloc::<u8>(1024, MEMF_CHIP)?
var config = Rc.new(Config::load()?)
```
- Automatic memory management
- Safe and simple
- Use 90% of the time

**Use references (Level 2) - When:**
```novus
fn process(data: &mut LargeStruct) { }
```
- Passing large structs (avoid copies)
- Method receivers (`&self`, `&mut self`)
- Want to modify caller's data
- Use ~9% of the time

**Use raw pointers (Level 3) - When:**
```novus
extern fn AllocMem(size: u32, flags: u32) -> *u8
```
- FFI functions (NDK, etc.)
- Custom memory managers
- You know what you're doing
- Use ~1% of the time

---

## What References DON'T Have (Unlike Rust)

❌ **No borrow checker** - We don't track who has references or for how long

❌ **No lifetime annotations** - No `<'a>` stuff

❌ **No rules about multiple mutable references** - Keep it simple

❌ **No "fighting the compiler"** - If it makes sense, it works

---

## What References DO Have (Safety Without Complexity)

✅ **Non-null guarantee** - References can't be null, period

✅ **Type system enforcement** - Compiler prevents null references

✅ **Clear intent** - `&T` vs `&mut T` shows read vs write

✅ **Zero overhead** - Just a pointer at runtime

✅ **FFI compatible** - References convert to pointers when needed

---

## For Swift Developers

If you know Swift, Novus feels familiar:

```swift
// Swift
func clear(screen: inout ScreenBuffer) {
    screen.pixels[0] = 0
}

var screen = ScreenBuffer()
clear(screen: &screen)
```

```novus
// Novus - same idea!
fn clear(screen: &mut ScreenBuffer) {
    screen.pixels[0] = 0
}

var screen = ScreenBuffer { /* ... */ }
clear(&mut screen)
```

**Key difference:** Swift's `ARC` is automatic and hidden. Novus's `Box`/`Rc` is explicit but simple.

---

## Summary

**Most Novus code (90%):**
```novus
var data = Box.alloc(1024, MEMF_CHIP)?
// Use data...
// Automatically freed
```

**Sometimes (9%):**
```novus
fn process(big_thing: &mut BigStruct) {
    // Pass by reference, not copy
}
```

**Rarely (1%):**
```novus
var ptr: *u8 = AllocMem(1024, MEMF_CHIP)
// Manual management for special cases
```

**No borrow checker. No lifetimes. No complexity.**

Just three simple levels - use the simplest one that works!

Ready to implement? 🚀
