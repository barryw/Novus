# Parameter Passing in Novus

## Current Implementation

Currently, **all parameters are passed by value** on the stack using C calling convention.

```novus
fn add(x: i32, y: i32) -> i32 {
    return x + y
}

var a = 10
var b = 20
var sum = add(a, b)  // a and b are copied to the stack
```

## Parameter Types

### Small Types (passed by value)

Primitives and pointers are passed directly on the stack (4 bytes each on 68000):

```novus
fn process(
    value: i32,      // 4 bytes on stack
    flag: bool,      // 4 bytes on stack (padded)
    ptr: *u8         // 4 bytes on stack
) {
    // ...
}
```

### Structs (currently passed by value)

**⚠️ Current behavior:** Structs are passed by value (copied to stack).

```novus
struct Point {
    x: i16
    y: i16
}

fn move_point(p: Point) {  // Point is copied to stack
    // Modifications to p don't affect caller's Point
}

var point = Point { x: 10, y: 20 }
move_point(point)  // Copies point
```

**Problem:** This is inefficient for large structs!

### Pointers (explicit pass by reference)

You can explicitly pass a pointer to avoid copying:

```novus
fn move_point(p: *Point) {  // Just pass the pointer (4 bytes)
    p.x = p.x + 10  // Modifies caller's Point
}

var point = Point { x: 10, y: 20 }
move_point(&point)  // Pass address of point
```

---

## Proposed Design

We should support both by-value and by-reference passing:

### Option 1: Explicit with `&` (Rust-style)

```novus
fn by_value(p: Point) {     // Copies Point
    p.x = 100  // Only affects local copy
}

fn by_ref(p: &Point) {      // Passes reference
    p.x = 100  // ERROR: can't modify through & reference
}

fn by_mut_ref(p: &mut Point) {  // Passes mutable reference
    p.x = 100  // OK: modifies caller's Point
}

var point = Point { x: 10, y: 20 }
by_value(point)      // Copies
by_ref(&point)       // Passes reference (read-only)
by_mut_ref(&mut point)  // Passes mutable reference
```

**Pros:**
- Explicit intent
- Compiler can enforce const-correctness
- Familiar to Rust developers

**Cons:**
- More verbose
- Requires borrow checker for full safety

### Option 2: Implicit for large types (Swift/C++-style)

```novus
// Compiler automatically passes large types by reference
fn process_buffer(buf: [u8; 4096]) {  // Automatically passed by reference
    // buf is read-only unless declared mut
}

// Small types still passed by value
fn add(x: i32, y: i32) -> i32 {  // Passed by value
    return x + y
}
```

**Pros:**
- Efficient by default
- Less verbose
- Familiar to Swift/C++ developers

**Cons:**
- Less explicit
- Magic threshold for "large"

### Option 3: Explicit with pointers (C-style - current)

```novus
// By value
fn by_value(p: Point) {
    p.x = 100  // Local copy
}

// By pointer (explicit)
fn by_pointer(p: *Point) {
    p.x = 100  // Modifies original
}

var point = Point { x: 10, y: 20 }
by_value(point)     // Copies
by_pointer(&point)  // Passes pointer
```

**Pros:**
- Simple, no magic
- Explicit control
- Works today

**Cons:**
- No const-correctness
- Easy to dereference null
- Verbose for common case

---

## Recommended Approach

**Hybrid: Start with Option 3 (current), add `&` references later**

### Phase 1 (Current - works today)

```novus
// By value - copy small types
fn add(x: i32, y: i32) -> i32 {
    return x + y
}

// By pointer - for large types or when you need to modify
fn init_screen(screen: *ScreenBuffer) {
    screen.width = 320
    screen.height = 200
}

var buf = ScreenBuffer { /* ... */ }
init_screen(&buf)
```

### Phase 2 (Add `&` references)

```novus
// Read-only reference (can't modify)
fn read_screen(screen: &ScreenBuffer) {
    println("Width: {}", screen.width)  // OK
    screen.width = 100  // ERROR: can't modify through &
}

// Mutable reference (can modify)
fn init_screen(screen: &mut ScreenBuffer) {
    screen.width = 320  // OK
}

var buf = ScreenBuffer { /* ... */ }
read_screen(&buf)      // Immutable borrow
init_screen(&mut buf)  // Mutable borrow
```

**Benefits:**
- References are guaranteed non-null
- Compiler enforces const-correctness
- Clear distinction between reading and modifying
- Still have raw pointers for FFI

### Phase 3 (Optional - Auto-pass large types by reference)

```novus
// Compiler automatically converts large structs to &
fn process(buf: Buffer) {  // Type is large, auto-converted to &Buffer
    // ...
}

// Same as writing:
fn process(buf: &Buffer) {
    // ...
}
```

---

## FFI Considerations

**Raw pointers (`*T`) are essential for FFI:**

```novus
// NDK functions take raw pointers
extern fn AllocMem(size: u32, flags: u32) -> *u8
extern fn FreeMem(ptr: *u8, size: u32)

fn allocate() -> *u8 {
    return AllocMem(1024, MEMF_CHIP)  // Returns raw pointer
}
```

**References (`&T`) would be for safe Novus code:**

```novus
// Safe wrapper using references
fn safe_copy(src: &[u8], dst: &mut [u8]) {
    // Internally calls CopyMem with raw pointers
    CopyMem(src.as_ptr(), dst.as_mut_ptr(), src.len())
}
```

---

## Examples

### Current (Phase 1 - works today)

```novus
struct Point {
    x: i16
    y: i16
}

// By value - simple and safe for small types
fn distance_from_origin(p: Point) -> f32 {
    return sqrt((p.x * p.x + p.y * p.y) as f32)
}

// By pointer - efficient for large types, can modify
fn translate(p: *Point, dx: i16, dy: i16) {
    p.x = p.x + dx
    p.y = p.y + dy
}

// Usage
var point = Point { x: 10, y: 20 }
var dist = distance_from_origin(point)  // Copies point (small, OK)
translate(&point, 5, 10)  // Passes pointer (efficient, can modify)
```

### Future (Phase 2 - with references)

```novus
struct ScreenBuffer {
    pixels: [u8; 64000]  // Large!
    width: u16
    height: u16
}

// Read-only reference - can't modify
fn render_to_screen(screen: &ScreenBuffer) {
    for i in 0..screen.width {
        // Read from screen...
    }
    // screen.pixels[0] = 42  // ERROR: can't modify through &
}

// Mutable reference - can modify
fn clear_screen(screen: &mut ScreenBuffer) {
    for i in 0..screen.pixels.len() {
        screen.pixels[i] = 0
    }
}

// Usage
var screen = ScreenBuffer { /* ... */ }
render_to_screen(&screen)      // Borrow (read-only)
clear_screen(&mut screen)      // Mutable borrow
```

---

## Summary

**Current (Phase 1):**
- ✅ All parameters passed by value
- ✅ Use `*T` pointers for large types or when you need to modify
- ✅ Works with FFI seamlessly

**Proposed (Phase 2):**
- Add `&T` for read-only references
- Add `&mut T` for mutable references
- Keep `*T` for FFI and unsafe code

**Future (Phase 3):**
- Optionally auto-convert large types to references
- Fully implement borrow checker for safety

---

## Grammar Changes Needed (Phase 2)

```antlr
type
    : '&' 'mut'? type      # ReferenceType
    | '*' type             # PointerType
    | // ... existing types
    ;

expression
    : '&' 'mut'? IDENTIFIER   # BorrowExpr
    | // ... existing expressions
    ;
```

This keeps backward compatibility while adding safer reference types!
