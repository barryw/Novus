---
title: Variables & Types
description: Learn about variables, type declarations, and the type system in Novus
---

Variables and types form the foundation of any Novus program. This guide covers how to declare variables, work with different types, and understand Novus's static type system.

## Variable Declarations

Novus provides three ways to declare bindings, each with different mutability and evaluation characteristics.

### Mutable Variables (`var`)

Use `var` for values that need to change during execution:

```novus
var x = 10
x = 20  // OK - can reassign
x = x + 5  // OK - now x is 25
```

Use `var` for:
- Loop counters and accumulators
- Temporary values that change
- State that evolves during execution

### Immutable Bindings (`let`)

Bindings declared with `let` cannot be reassigned:

```novus
let x = 10
x = 20  // ERROR - cannot reassign

let name = "Alice"
// name = "Bob"  // ERROR
```

Immutable bindings make code clearer and enable better compiler optimizations. Use `let` by default; switch to `var` only when you need mutability.

### Constants (`const`)

Constants are evaluated at compile time and must be known at compilation:

```novus
pub const SCREEN_WIDTH: u32 = 320
pub const SCREEN_HEIGHT: u32 = 200
pub const SCREEN_SIZE: u32 = SCREEN_WIDTH * SCREEN_HEIGHT
```

Constants:
- Must be literals or constant expressions
- Cannot call runtime functions
- Are typically declared at module scope with `pub` visibility

### Summary

| Keyword | Reassignable | Evaluation | Typical Scope |
|---------|--------------|------------|---------------|
| `var` | Yes | Runtime | Function/Block |
| `let` | No | Runtime | Function/Block |
| `const` | No | Compile-time | Module |

## Type Annotations

Novus has type inference, but you can always specify types explicitly:

```novus
// Type inference
let x = 42        // inferred as i32
let y = 100       // inferred as i32

// Explicit type annotations
let x: u16 = 42
let y: i64 = 100
let flag: bool = true
```

Type annotations use the syntax `name: Type`.

## Primitive Types

Novus provides several categories of primitive types:

### Integer Types

Novus has both signed and unsigned integers in multiple sizes:

| Type | Size | Range (Signed) | Range (Unsigned) |
|------|------|----------------|------------------|
| `i8` / `u8` | 8-bit | -128 to 127 | 0 to 255 |
| `i16` / `u16` | 16-bit | -32,768 to 32,767 | 0 to 65,535 |
| `i32` / `u32` | 32-bit | -2,147,483,648 to 2,147,483,647 | 0 to 4,294,967,295 |
| `i64` / `u64` | 64-bit | -(2^63) to (2^63)-1 | 0 to (2^64)-1 |

Examples:

```novus
let byte: u8 = 255
let count: i32 = -42
let address: u32 = 0xDFF000
let large: i64 = 1000000000
```

### Type Suffixes

You can specify the type of a literal using suffixes:

```novus
let x = 42u16    // u16
let y = 100i64   // i64
let z = 255u8    // u8
let w = -50i32   // i32

// Without suffix, defaults to i32
let n = 42       // i32
```

Available suffixes:
- Unsigned: `u8`, `u16`, `u32`, `u64`
- Signed: `i8`, `i16`, `i32`, `i64`

### Boolean Type

The `bool` type has two values: `true` and `false`:

```novus
let flag: bool = true
let done = false

if flag {
    // do something
}
```

### Character Type

The `char` type represents a single character (currently ASCII):

```novus
let c: char = 'A'
let newline: char = '\n'
let tab: char = '\t'
```

Escape sequences:
- `\n` - newline
- `\t` - tab
- `\\` - backslash
- `\'` - single quote
- `\0` - null character

### Fixed-Point Types

Novus provides fixed-point arithmetic for efficient fractional math on 68k CPUs without an FPU:

```novus
let angle: fixed16 = 45.0   // 8.8 fixed-point
let scale: fixed32 = 2.5    // 16.16 fixed-point
```

- `fixed16` - 8.8 format (8 integer bits, 8 fractional bits)
- `fixed32` - 16.16 format (16 integer bits, 16 fractional bits)

Fixed-point types are ideal for:
- Graphics transformations
- Physics calculations
- Audio mixing
- Any fractional math on 68k without FPU

## String Types

### String Literals (`str`)

String literals are written in double quotes:

```novus
let message: str = "Hello, Amiga!"
let path = "LIBS:mylib.library"
```

String literals are:
- Immutable
- UTF-8 encoded (currently ASCII)
- Stored in read-only data section

### String Interpolation

Novus supports f-strings for formatting:

```novus
let name = "Alice"
let age = 25
let message = f"Hello, {name}! You are {age} years old."
```

## Arrays

Arrays have a fixed size known at compile time:

```novus
// Array with inferred size (compiler determines length from elements)
let numbers: [i32] = [1, 2, 3, 4, 5]

// Array with type inference
let colors = [0xFF0000, 0x00FF00, 0x0000FF]

// Access elements (zero-indexed)
let first = numbers[0]
let second = numbers[1]

// Modify elements (if var)
var values = [10, 20, 30]
values[0] = 100
```

Array syntax: `[Type; Size]`

Arrays are:
- Fixed size at compile time
- Zero-indexed
- Bounds-checked in debug builds
- Stack-allocated by default

## Pointers and References

### Raw Pointers

Raw pointers can be null and require `unsafe` to dereference:

```novus
let ptr: *u8 = (*u8)0x100000  // Cast to pointer
let null_ptr: *i32 = 0        // Null pointer

// Dereferencing requires unsafe
unsafe {
    let value = *ptr
}
```

### References

References are guaranteed to be non-null:

```novus
let x = 42
let r: &i32 = &x        // Immutable reference
var y = 42
let m: &var i32 = &var y // Exclusive mutable reference
```

References:
- Are always valid (non-null)
- Have a lifetime tied to the value they reference
- Cannot outlive the value they reference

## Type Conversions

Novus requires explicit type conversions (casts):

```novus
let x: i32 = 42
let y: i64 = (i64)x     // Explicit cast

let addr: u32 = 0xDFF000
let ptr: *u16 = (*u16)addr  // Cast address to pointer

// Narrowing conversions (may lose data)
let big: i32 = 1000
let small: i8 = (i8)big  // May overflow!
```

No implicit conversions between types - you must be explicit.

## Type Inference

The compiler can infer types in many contexts:

```novus
let x = 42          // i32 (default integer type)
let y = true        // bool
let z = "hello"     // str
let a = [1, 2, 3]   // [i32; 3]
```

Function return types can also be inferred from context:

```novus
fn get_number() -> i32 {
    return 42  // return type matches function signature
}
```

Use type inference to reduce verbosity, but add explicit annotations when it improves clarity.

## Example: Putting It Together

```novus
pub const MAX_SPRITES: u32 = 8

fn main() -> i32 {
    // Variables with different types
    let width: u16 = 320
    let height: u16 = 200
    var sprite_count = 0u32

    // Arrays (size inferred from elements)
    let sprite_x: [i16] = [0, 16, 32, 48, 64, 80, 96, 112]
    let sprite_y: [i16] = [100, 100, 100, 100, 100, 100, 100, 100]

    // Fixed-point math
    let scale: fixed16 = 1.5

    // String formatting
    let status = f"Screen: {width}x{height}, Sprites: {sprite_count}"

    return 0
}
```

## Best Practices

1. **Prefer `let` over `var`**: Use immutable bindings by default
2. **Use explicit types for clarity**: Especially for function parameters and public APIs
3. **Choose the right integer size**: Use the smallest type that fits your data
4. **Use fixed-point for math**: Avoid floating-point on 68k without FPU
5. **Be explicit about casts**: Never rely on implicit conversions
6. **Use `const` for configuration**: Define magic numbers as named constants

## Coming from C

Key differences from C:

| C | Novus |
|---|-------|
| `int x = 10;` | `var x = 10` or `let x = 10` |
| `const int MAX = 100;` | `pub const MAX: i32 = 100` |
| `unsigned short x;` | `let x: u16` |
| `char c = 'A';` | `let c: char = 'A'` |
| `int arr[5] = {1,2,3,4,5};` | `let arr = [1, 2, 3, 4, 5]` |
| `char *str = "hello";` | `let str: str = "hello"` |
| `void *ptr = (void*)0x100;` | `let ptr: *u8 = (*u8)0x100` |

Key points:
- Types come **after** names: `x: i32` not `int x`
- No semicolons for declarations (except top-level items)
- Arrays include size in type: `[i32; 5]`
- String type is `str`, not `char*`
