# Automatic Pass-by-Value or Reference

## The Problem

Swift developers don't think about whether something is passed by value or reference - the compiler figures it out.

Rust developers always know exactly what's happening - explicit is better than implicit.

**Novus gives you both: Smart defaults with explicit overrides.**

---

## Golden Rules

1. **Small types (≤ 16 bytes)**: Always passed by value (copied)
2. **Large types (> 16 bytes)**: Automatically passed by reference (read-only!)
3. **Want to modify?**: Use explicit `&mut` - never automatic
4. **Call sites**:
   - Implicit reference: `process(data)` - compiler adds `&`
   - Explicit mutable: `modify(&mut data)` - you write `&mut`

---

## The Rules

### Rule 1: Small Types = Pass by Value (Automatic)

Types ≤ 16 bytes are automatically passed by value (copied):

```novus
struct Point {
    x: i32  // 4 bytes
    y: i32  // 4 bytes
}  // Total: 8 bytes - passed by value

fn distance(p: Point) -> f32 {
    // p is a copy - total size 8 bytes
    return sqrt((p.x * p.x + p.y * p.y) as f32)
}

var point = Point { x: 10, y: 20 }
distance(point)  // Copies point (8 bytes - cheap!)
```

**Types that pass by value:**
- All primitives (i32, u8, bool, etc.) - 1-8 bytes
- Small structs (≤ 16 bytes)
- Pointers (*T) - 4 bytes
- References (&T) - 4 bytes

### Rule 2: Large Types = Pass by Reference (Automatic)

Types > 16 bytes are automatically passed by reference (read-only):

```novus
struct ScreenBuffer {
    pixels: [u8; 64000]  // 64KB
    width: u16
    height: u16
}  // Total: 64004 bytes - passed by reference automatically!

fn print_screen(screen: ScreenBuffer) {
    // screen is AUTOMATICALLY a reference (&ScreenBuffer)
    // Compiler converts this to pass-by-reference behind the scenes
    println("Size: {}x{}", screen.width, screen.height)  // OK: can read
}

fn clear_screen(screen: ScreenBuffer) {
    // screen is AUTOMATICALLY a reference (&ScreenBuffer) - READ-ONLY!
    for i in 0..64000 {
        screen.pixels[i] = 0  // ERROR: cannot modify through implicit reference!
    }
}

var screen = ScreenBuffer { /* ... */ }
print_screen(screen)  // Passes 4-byte pointer, not 64KB copy!
```

**Key rule: Implicit references are READ-ONLY.** You cannot modify through them.

### Rule 3: Explicit Wins (Override the Magic)

When you want control, be explicit:

```novus
// Explicitly immutable reference (&T)
fn print_screen(screen: &ScreenBuffer) {
    println("Size: {}x{}", screen.width, screen.height)
    // screen.width = 100  // ERROR: & is read-only
}

// Explicitly mutable reference (&mut T)
fn clear_screen(screen: &mut ScreenBuffer) {
    for i in 0..64000 {
        screen.pixels[i] = 0  // OK: &mut allows modification
    }
}

// Explicitly by value (even though it's large!)
fn copy_screen(screen: ScreenBuffer) {
    // Actually copies 64KB! Usually not what you want, but allowed
}

var screen = ScreenBuffer { /* ... */ }
print_screen(&screen)      // Explicit immutable reference
clear_screen(&mut screen)  // Explicit mutable reference
copy_screen(screen)        // Explicit copy
```

---

## The Magic: What the Compiler Does

### Scenario 1: Implicit Pass-by-Reference (Read-Only)

```novus
// User writes:
fn process(data: LargeStruct) {
    println("Size: {}", data.size)
    // No modifications - this works fine
}

// Compiler treats it as:
fn process(data: &LargeStruct) {
    println("Size: {}", data.size)
}

// Call:
var data = LargeStruct { /* ... */ }
process(data)      // Compiler converts to: process(&data)
```

**Implicit reference is ALWAYS read-only.** If you try to modify, you get a compile error:

```novus
fn bad_process(data: LargeStruct) {
    data.field = 42  // COMPILE ERROR: cannot modify 'data' through implicit reference
                     // Hint: use '&mut LargeStruct' if you need to modify
}
```

### Scenario 2: Explicit Mutable Reference

```novus
// User writes:
fn modify(data: &mut LargeStruct) {
    data.field = 42  // OK!
}

// Call:
var data = LargeStruct { /* ... */ }
modify(&mut data)  // Explicit: passing mutable reference
```

### Scenario 3: Small Types (No Magic)

```novus
// User writes:
fn add(p: Point) -> Point {
    return Point { x: p.x + 1, y: p.y + 1 }
}

// No conversion - Point is small (8 bytes)
// Passed by value, modifications don't affect caller

var p = Point { x: 10, y: 20 }
var p2 = add(p)  // p is copied
```

---

## The Threshold: 16 Bytes

**Why 16 bytes?**
- Fits in 4 registers on 68000 (d0-d3 or a0-a3)
- Small enough that copying is negligible
- Covers most common cases (Point, Color, small enums)

**Types that are ≤ 16 bytes (pass by value):**
- Primitives (i8, i16, i32, i64, u8, u16, u32, u64, f32, f64, bool)
- Pointers (*T) and references (&T)
- Small structs:
  ```novus
  struct Point { x: i32, y: i32 }           // 8 bytes
  struct Color { r: u8, g: u8, b: u8 }      // 3 bytes (padded to 4)
  struct Rect { x: i16, y: i16, w: i16, h: i16 }  // 8 bytes
  ```

**Types that are > 16 bytes (pass by reference):**
- Arrays: `[u8; 17]` or larger
- Large structs with many fields
- Structs containing large arrays

---

## Examples

### Example 1: Automatic Optimization

```novus
struct Tiny {
    x: i32
}  // 4 bytes

struct Medium {
    a: i32
    b: i32
    c: i32
    d: i32
}  // 16 bytes

struct Large {
    data: [u8; 1024]
}  // 1024 bytes

fn process_tiny(t: Tiny) {
    // Passed by value (4 bytes - cheap)
}

fn process_medium(m: Medium) {
    // Passed by value (16 bytes - still cheap)
}

fn process_large(l: Large) {
    // AUTOMATICALLY passed by reference (1KB - expensive to copy)
    // Compiler converts to: fn process_large(l: &Large)
}

var tiny = Tiny { x: 42 }
var medium = Medium { a: 1, b: 2, c: 3, d: 4 }
var large = Large { data: [0u8; 1024] }

process_tiny(tiny)      // Copy (4 bytes)
process_medium(medium)  // Copy (16 bytes)
process_large(large)    // Reference (4 bytes pointer)
```

### Example 2: Explicit When Needed

```novus
struct Buffer {
    data: [u8; 4096]
}

// Read-only - use implicit reference
fn compute_checksum(buf: Buffer) -> u32 {
    // buf is implicitly &Buffer (read-only)
    var sum = 0u32
    for i in 0..4096 {
        sum = sum + buf.data[i] as u32
    }
    return sum
}

// Need to modify - be explicit!
fn zero_buffer(buf: &mut Buffer) {
    for i in 0..4096 {
        buf.data[i] = 0
    }
}

// Force copy (unusual but allowed)
fn copy_buffer(buf: Buffer) -> Buffer {
    // Actually copies 4KB
    return buf
}

var buffer = Buffer { data: [0u8; 4096] }
var checksum = compute_checksum(buffer)  // Auto: &buffer
zero_buffer(&mut buffer)                 // Explicit: &mut
var buffer2 = copy_buffer(buffer)        // Explicit: copy
```

### Example 3: Methods (Always References)

```novus
struct Counter {
    value: i32
}

impl Counter {
    // self is always a reference in methods
    fn get(&self) -> i32 {
        return self.value
    }

    fn increment(&mut self) {
        self.value = self.value + 1
    }

    // Can also take other parameters
    fn add(&mut self, amount: i32) {
        self.value = self.value + amount
    }
}

var counter = Counter { value: 0 }
counter.increment()      // Auto: &mut counter
counter.add(10)          // Auto: &mut counter
println("{}", counter.get())  // Auto: &counter
```

---

## Call Site Syntax

### Implicit Reference (Compiler Decides)

```novus
fn process(data: LargeStruct) { /* ... */ }

var data = LargeStruct { /* ... */ }
process(data)  // Compiler automatically converts to process(&data)
```

**No `&` at call site!** Compiler does it for you.

### Explicit Reference (You Decide)

```novus
fn modify(data: &mut LargeStruct) { /* ... */ }

var data = LargeStruct { /* ... */ }
modify(&mut data)  // Explicit &mut at call site
```

**You write `&mut` at call site** to match the explicit signature.

---

## Error Messages Guide Users

### Error 1: Trying to Modify Implicit Reference

```novus
fn bad(buf: Buffer) {
    buf.data[0] = 42  // ERROR!
}
```

**Error:**
```
error: cannot modify 'buf' through implicit reference
 --> test.novus:2:5
  |
1 | fn bad(buf: Buffer) {
  |        --- 'buf' is implicitly an immutable reference because Buffer is large (4096 bytes)
2 |     buf.data[0] = 42
  |     ^^^^^^^^^^^^^^^^ cannot modify through immutable reference
  |
help: if you want to modify 'buf', use an explicit mutable reference:
  |
1 | fn bad(buf: &mut Buffer) {
  |             ^^^^
```

### Error 2: Mixing Implicit and Explicit at Call Site

```novus
fn process(data: LargeStruct) { /* ... */ }

var data = LargeStruct { /* ... */ }
process(&data)  // ERROR - don't need explicit &
```

**Error:**
```
error: unnecessary explicit reference
 --> test.novus:4:9
  |
4 | process(&data)
  |         ^^^^^ explicit reference not needed
  |
note: 'process' takes 'LargeStruct' by value, which is automatically converted to a reference
help: remove the '&':
  |
4 | process(data)
  |         ^^^^
```

---

## Implementation

### Semantic Analysis

```csharp
public override IrType? VisitParameter(ParameterContext context) {
    var type = ParseType(context.type())

    // Check if type is large
    if (IsLargeType(type) && !IsExplicitReference(context.type())) {
        // Mark this parameter as "implicitly by reference"
        return new IrImplicitRefType(type, isMutable: false)
    }

    return type
}

bool IsLargeType(IrType type) {
    return type.SizeInBytes > 16
}

bool IsExplicitReference(TypeContext typeCtx) {
    return typeCtx.GetText().StartsWith("&")
}
```

### Assignment Checking

```csharp
public override IrType? VisitAssignment(AssignmentContext context) {
    var target = GetVariable(context.target())

    // Check if trying to modify implicit reference
    if (target.Type is IrImplicitRefType implicitRef && !implicitRef.IsMutable) {
        Error($"cannot modify '{target.Name}' through implicit reference")
        Suggestion("use '&mut {TypeName}' if you need to modify")
        return null
    }

    // ... rest of assignment checking
}
```

### Code Generation

```csharp
// Implicit references compile to the same code as explicit references
// Just a pointer passed on the stack

void GenerateFunctionCall(IrCall call) {
    for (int i = 0; i < call.Arguments.Count; i++) {
        var arg = call.Arguments[i]
        var param = call.Function.Parameters[i]

        if (param.Type is IrImplicitRefType) {
            // Take address of argument
            EmitAddressOf(arg)
        } else {
            // Pass by value
            EmitLoad(arg)
        }
    }
}
```

---

## Comparison with Other Languages

| Language | Small Types | Large Types | Control |
|----------|-------------|-------------|---------|
| **C** | By value | By value (expensive!) | Manual (use *) |
| **C++** | By value | By value (expensive!) | Manual (use &) |
| **Rust** | By value | By value (expensive!) | Explicit & always |
| **Swift** | By value | By value (COW) | Automatic (classes vs structs) |
| **Go** | By value | By value | Explicit * for pointers |
| **Novus** | By value | **By reference (auto!)** | **Both auto + explicit** |

---

## Benefits

✅ **Efficient by default** - Large types automatically passed by reference

✅ **Zero overhead** - Same as manual references

✅ **Clear when explicit** - `&mut` shows you're modifying

✅ **No surprises** - Implicit refs are read-only

✅ **Swift-like ease** - Don't think about it most of the time

✅ **Rust-like control** - Explicit when you need it

---

## Summary

**The Rules:**

1. **Small (≤ 16 bytes): Pass by value automatically**
   ```novus
   fn process(point: Point) { }  // Copied
   ```

2. **Large (> 16 bytes): Pass by reference automatically (read-only)**
   ```novus
   fn process(buffer: Buffer) { }  // Auto-converted to &Buffer
   ```

3. **Explicit reference: Always pass by reference**
   ```novus
   fn process(buffer: &Buffer) { }     // Immutable ref
   fn modify(buffer: &mut Buffer) { }  // Mutable ref
   ```

4. **Call sites:**
   ```novus
   process(buffer)       // Implicit: compiler adds &
   modify(&mut buffer)   // Explicit: you write &mut
   ```

**Most of the time:** Just write `fn process(data: MyType)` and the compiler does the right thing.

**When you need control:** Add `&` or `&mut` to be explicit.

**Best of both worlds!** 🎯
