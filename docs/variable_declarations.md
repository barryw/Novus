# Variable Declarations in Novus

Novus uses three keywords for declaring identifiers:

## `var` - Mutable Variables

Variables that can be reassigned.

```novus
var x = 10
x = 20  // OK - can reassign

var buffer = Box.alloc(1024, MEMF_CHIP)?
buffer = Box.alloc(2048, MEMF_PUBLIC)?  // OK
```

**Use for:** Most variables, counters, accumulators, temporary values.

---

## `let` - Immutable Bindings

Bindings that cannot be reassigned (like JavaScript/TypeScript `const`).

```novus
let x = 10
x = 20  // ERROR - cannot reassign

let file = Open("data.txt", MODE_OLDFILE)
file = Open("other.txt", MODE_OLDFILE)  // ERROR
```

**Use for:** Values that shouldn't change, function parameters, loop variables.

**Benefits:**
- Prevents accidental modification
- Makes code intent clearer
- Compiler can optimize better

---

## `const` - Compile-Time Constants

Constants evaluated at compile time. Must be literal values or constant expressions.

```novus
pub const SCREEN_WIDTH: u32 = 320
pub const SCREEN_HEIGHT: u32 = 200
pub const SCREEN_SIZE: u32 = SCREEN_WIDTH * SCREEN_HEIGHT

pub const MEMF_CHIP: u32 = 2
pub const MEMF_CLEAR: u32 = 65536
pub const CHIP_CLEAR: u32 = MEMF_CHIP | MEMF_CLEAR  // Constant expression
```

**Use for:** Configuration values, flag constants, sizes, offsets.

**Requirements:**
- Must be compile-time evaluable
- Can only use literals and other constants
- Cannot use function calls or runtime values
- Typically declared at module scope with `pub`

---

## Comparison

| Keyword | Can reassign? | Runtime/Compile-time | Scope | Example |
|---------|---------------|---------------------|-------|---------|
| `var` | ✅ Yes | Runtime | Function/Block | `var x = 10` |
| `let` | ❌ No | Runtime | Function/Block | `let x = 10` |
| `const` | ❌ No | **Compile-time** | Module | `pub const X: u32 = 10` |

---

## Examples

### Simple function

```novus
fn calculate(width: i32, height: i32) -> i32 {
    // width and height are immutable (function parameters are let by default)

    var area = width * height  // Mutable - can reassign
    area = area * 2

    let result = area + 10  // Immutable - cannot reassign

    return result
}
```

### With constants

```novus
pub const MAX_BUFFER_SIZE: u32 = 4096
pub const DEFAULT_FLAGS: u32 = MEMF_PUBLIC | MEMF_CLEAR

fn allocate_buffer(size: u32) -> Result<Box<u8>, Error> {
    let actual_size = if size > MAX_BUFFER_SIZE {
        MAX_BUFFER_SIZE
    } else {
        size
    }

    var buffer = Box.alloc(actual_size, DEFAULT_FLAGS)?

    return Ok(buffer)
}
```

### Defer with variables

```novus
fn process_file(path: str) -> Result<(), Error> {
    var file = Open(path.as_ptr(), MODE_OLDFILE)
    if file == 0 { return Err(Error.CannotOpen) }
    defer { Close(file) }

    var bytes_read = 0  // Track total

    defer {
        println("Read {} bytes total", bytes_read)
    }

    // bytes_read can be modified
    bytes_read = Read(file, buffer, 1024)

    return Ok(())
}
```

---

## Design Rationale

This follows the JavaScript/TypeScript model:

- **`var`** is like `let` in JavaScript (mutable, block-scoped)
- **`let`** is like `const` in JavaScript (immutable binding)
- **`const`** is unique to Novus (compile-time constant)

**Why this model?**

1. **Familiar** - JavaScript/TypeScript developers will recognize it immediately
2. **Clear intent** - `let` signals "this doesn't change"
3. **Performance** - Compiler can optimize immutable bindings
4. **Safety** - Prevents accidental modification
5. **Flexible** - Use `var` when you need mutability, `let` when you don't

---

## Migration from other languages

### From Rust
- Rust `let` → Novus `let` (immutable)
- Rust `let mut` → Novus `var` (mutable)
- Rust `const` → Novus `const` (compile-time constant)

### From C/C++
- C/C++ `int x` → Novus `var x` (mutable)
- C/C++ `const int x` → Novus `let x` (immutable at runtime)
- C/C++ `#define X` → Novus `pub const X` (compile-time constant)

### From JavaScript/TypeScript
- JS/TS `let` → Novus `var` (mutable)
- JS/TS `const` → Novus `let` (immutable binding)
- No equivalent → Novus `const` (compile-time constant)

### From Swift
- Swift `var` → Novus `var` (mutable)
- Swift `let` → Novus `let` (immutable)
- No equivalent → Novus `const` (compile-time constant)
