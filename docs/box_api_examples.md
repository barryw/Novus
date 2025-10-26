# Box<T> API and Usage

## What is Box<T>?

`Box<T>` is a smart pointer that owns heap-allocated memory and automatically frees it when it goes out of scope.

---

## Box.alloc() - For Buffers/Arrays

**Allocates memory for multiple items of type T**

```novus
// Allocate 1024 bytes of chip memory
let buffer: Box<u8> = Box.alloc(1024, MEMF_CHIP | MEMF_CLEAR)?

// buffer is now a smart pointer to 1024 bytes
// Automatically calls FreeMem(ptr, 1024) at scope exit
```

### Accessing the Memory

```novus
// Index into it like an array
buffer[0] = 42
buffer[10] = 0xFF

let value = buffer[5]  // Read

// Get raw pointer when you need it (for FFI calls)
let raw_ptr: *u8 = buffer.as_ptr()
Read(file, raw_ptr, 1024)  // Pass to NDK function
```

### Complete Example

```novus
use ffi::dos::*
use ffi::exec::*

fn read_file(path: str) -> Result<(), Error> {
    // Open file
    let file = Open(path.as_ptr(), MODE_OLDFILE)
    if file == 0 {
        return Err(Error.CannotOpen)
    }
    defer Close(file)

    // Allocate buffer (automatically freed at scope exit!)
    let buffer = Box.alloc::<u8>(4096, MEMF_PUBLIC | MEMF_CLEAR)?

    // Read into buffer (NDK function needs raw pointer)
    let bytes_read = Read(file, buffer.as_ptr(), 4096)

    if bytes_read < 0 {
        return Err(Error.ReadFailed)
    }

    // Process buffer
    for i in 0..bytes_read {
        let byte = buffer[i]
        println!("Byte {}: 0x{:02x}", i, byte)
    }

    return Ok(())
}  // buffer automatically freed here!
```

---

## Box.new() - For Single Values

**Allocates memory for a single instance of T**

```novus
// Allocate a single struct on the heap
let config = Box.new(AppConfig {
    width: 320,
    height: 200,
    colors: 16
})?

// Access fields
println!("Width: {}", config.width)
config.colors = 32  // Mutable

// Get pointer to struct
let raw_ptr: *AppConfig = config.as_ptr()
```

---

## Box API Reference

```novus
pub struct Box<T> {
    ptr: *T
    size: u32    // Size in bytes
    count: u32   // Number of T items
    flags: u32   // Memory flags used
}

impl Box<T> {
    // Allocate array of count items
    pub fn alloc(count: u32, flags: u32) -> Result<Box<T>, MemError>

    // Allocate single item with value
    pub fn new(value: T) -> Result<Box<T>, MemError>

    // Get raw pointer (doesn't give up ownership)
    pub fn as_ptr(&self) -> *T

    // Get mutable raw pointer
    pub fn as_mut_ptr(&mut self) -> *mut T

    // Convert to raw pointer (gives up ownership, no auto-free!)
    pub fn into_raw(self) -> *T

    // Wrap existing raw pointer (takes ownership)
    pub unsafe fn from_raw(ptr: *T, count: u32, flags: u32) -> Box<T>

    // Index operations (for arrays)
    pub fn get(&self, index: u32) -> &T
    pub fn get_mut(&mut self, index: u32) -> &mut T

    // Automatic cleanup (called by compiler)
    pub fn drop(&mut self)
}

// Operator overloading (compiler magic)
impl Index<u32> for Box<T> {
    fn index(&self, i: u32) -> &T {
        return self.get(i)
    }
}

impl IndexMut<u32> for Box<T> {
    fn index_mut(&mut self, i: u32) -> &mut T {
        return self.get_mut(i)
    }
}
```

---

## Usage Patterns

### Pattern 1: Byte Buffer

```novus
fn allocate_buffer() -> Result<Box<u8>, Error> {
    let buf = Box.alloc::<u8>(1024, MEMF_CHIP)?

    // Zero it out
    for i in 0..1024 {
        buf[i] = 0
    }

    return Ok(buf)
}  // If error occurs, buf is automatically freed
```

### Pattern 2: Struct Buffer

```novus
struct Point {
    x: i16
    y: i16
}

fn allocate_points() -> Result<Box<Point>, Error> {
    // Allocate 100 Points
    let points = Box.alloc::<Point>(100, MEMF_PUBLIC)?

    // Initialize
    for i in 0..100 {
        points[i] = Point { x: i as i16, y: i as i16 }
    }

    return Ok(points)
}
```

### Pattern 3: Single Heap Object

```novus
struct LargeData {
    buffer: [u8; 10000]
    metadata: [u32; 1000]
}

fn create_large_data() -> Result<Box<LargeData>, Error> {
    // Too big for stack, allocate on heap
    let data = Box.new(LargeData {
        buffer: [0u8; 10000],
        metadata: [0u32; 1000]
    })?

    return Ok(data)
}
```

### Pattern 4: Passing to NDK Functions

```novus
fn copy_memory_example() -> Result<(), Error> {
    let src = Box.alloc::<u8>(1024, MEMF_PUBLIC)?
    let dst = Box.alloc::<u8>(1024, MEMF_PUBLIC)?

    // Fill source
    for i in 0..1024 {
        src[i] = (i & 0xFF) as u8
    }

    // Copy using NDK function
    CopyMem(src.as_ptr(), dst.as_ptr(), 1024)

    // Verify
    for i in 0..1024 {
        assert!(dst[i] == src[i])
    }

    return Ok(())
}  // Both src and dst automatically freed!
```

### Pattern 5: Converting Between Box and Raw Pointer

```novus
// Take raw pointer and wrap it
fn wrap_existing(ptr: *u8, size: u32) -> Box<u8> {
    // UNSAFE: You promise this pointer is valid and you own it
    return unsafe { Box.from_raw(ptr, size, MEMF_PUBLIC) }
}  // Box will now free this pointer when dropped

// Give up ownership
fn give_away_memory() -> *u8 {
    let buffer = Box.alloc::<u8>(1024, MEMF_CHIP).unwrap()

    // Convert to raw pointer (no automatic cleanup!)
    return buffer.into_raw()
    // Caller is now responsible for calling FreeMem
}
```

---

## Type Inference

The compiler can often infer the type:

```novus
// Explicit type
let buffer: Box<u8> = Box.alloc(1024, MEMF_CHIP)?

// Type inferred from usage
let buffer = Box.alloc::<u8>(1024, MEMF_CHIP)?

// Type inferred from context
fn needs_u8_buffer(buf: Box<u8>) { }

let buffer = Box.alloc(1024, MEMF_CHIP)?  // Inferred as Box<u8>
needs_u8_buffer(buffer)
```

---

## Error Handling

```novus
// Returns Result<Box<T>, MemError>
let buffer = Box.alloc::<u8>(1024, MEMF_CHIP)?

// Or handle explicitly
let buffer = match Box.alloc::<u8>(1024, MEMF_CHIP) {
    Ok(buf) => buf,
    Err(MemError.OutOfMemory) => {
        println("Out of memory!")
        return Err(Error.NoMemory)
    }
}
```

---

## Comparison with Manual Management

### Without Box (Manual)

```novus
fn manual_way() -> i32 {
    let buffer = AllocMem(1024, MEMF_CHIP | MEMF_CLEAR)
    if buffer == null {
        return -1
    }

    // Use buffer...
    buffer[0] = 42  // Requires pointer arithmetic

    if some_error_condition {
        FreeMem(buffer, 1024)  // Must remember!
        return -1
    }

    // More code...

    FreeMem(buffer, 1024)  // Must remember!
    return 0
}
```

### With Box (Automatic)

```novus
fn box_way() -> Result<(), Error> {
    let buffer = Box.alloc::<u8>(1024, MEMF_CHIP | MEMF_CLEAR)?

    // Use buffer...
    buffer[0] = 42  // Clean array syntax

    if some_error_condition {
        return Err(Error.SomeError)  // Auto-freed!
    }

    // More code...

    return Ok(())  // Auto-freed!
}
```

---

## Ownership Transfer

```novus
fn allocate() -> Result<Box<u8>, Error> {
    let buffer = Box.alloc(1024, MEMF_CHIP)?
    return Ok(buffer)  // Ownership transferred to caller
}  // No free here!

fn use_it() -> Result<(), Error> {
    let my_buffer = allocate()?
    // Use my_buffer...
    return Ok(())
}  // Free happens here!
```

---

## Real-World Example: Screen Buffer

```novus
struct ScreenBuffer {
    pixels: Box<u8>
    width: u16
    height: u16
}

impl ScreenBuffer {
    pub fn new(width: u16, height: u16) -> Result<ScreenBuffer, Error> {
        let size = (width as u32) * (height as u32)
        let pixels = Box.alloc::<u8>(size, MEMF_CHIP | MEMF_CLEAR)?

        return Ok(ScreenBuffer {
            pixels: pixels,
            width: width,
            height: height
        })
    }

    pub fn set_pixel(&mut self, x: u16, y: u16, color: u8) {
        let offset = (y as u32) * (self.width as u32) + (x as u32)
        self.pixels[offset] = color
    }

    pub fn get_pixel(&self, x: u16, y: u16) -> u8 {
        let offset = (y as u32) * (self.width as u32) + (x as u32)
        return self.pixels[offset]
    }

    pub fn as_ptr(&self) -> *u8 {
        return self.pixels.as_ptr()
    }
}

// Usage
fn render() -> Result<(), Error> {
    let screen = ScreenBuffer.new(320, 200)?

    screen.set_pixel(10, 10, 15)
    screen.set_pixel(20, 20, 31)

    // Pass to graphics library
    let screen_ptr = screen.as_ptr()
    WritePixelArray(screen_ptr, /* ... */)

    return Ok(())
}  // screen.pixels automatically freed!
```

---

## Summary

**What Box.alloc returns:**
- A smart pointer (`Box<T>`) that owns heap memory
- Acts like an array with `[]` indexing
- Has `.as_ptr()` method for FFI calls
- Automatically calls FreeMem when dropped

**Key Benefits:**
- ✅ Can't forget to free
- ✅ Can't double-free
- ✅ Clean array syntax
- ✅ Works with NDK functions
- ✅ Zero overhead vs manual management
