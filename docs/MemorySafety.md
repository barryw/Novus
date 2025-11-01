# Memory Safety in Novus

Novus provides multiple levels of memory safety, from low-level manual control to high-level RAII-style abstractions.

## Safety Levels

### Level 0: Raw FFI (⛔ UNSAFE - Expert Only)

```novus
from std::ffi::exec import AllocMem, FreeMem

// Raw C-style allocation - returns i32 (address or 0)
let addr: i32 = AllocMem(1024, 1)
if addr == 0 {
    // allocation failed
}
// Easy to:
// - Double-free
// - Use after free
// - Forget to free
// - Pass wrong size to FreeMem
FreeMem(addr, 1024)  // Must remember exact size!
```

**Use when**: Interfacing with AmigaOS libraries that expect raw addresses

---

### Level 1: Option Wrappers (⚠️ LOW-LEVEL)

```novus
from std::exec import AllocMem, FreeMem, MEMF_PUBLIC

// Returns Option<*u8> instead of null
let mem: Option<*u8> = AllocMem(1024u32, MEMF_PUBLIC)

match mem {
    Some(ptr) => {
        // Use ptr...
        FreeMem(ptr, 1024u32)  // Still must remember size!
    },
    None => { /* allocation failed */ }
}
```

**Improvements**:
- ✅ Explicit failure handling with Option
- ✅ No null pointer crashes

**Still allows**:
- ❌ Double-free
- ❌ Use-after-free
- ❌ Forgetting to free
- ❌ Wrong size in FreeMem

**Use when**: You need manual control for performance-critical code

---

### Level 2: RAII with defer (✅ SEMI-SAFE)

```novus
from std::exec import AllocMem, FreeMem, MEMF_PUBLIC

let mem: Option<*u8> = AllocMem(1024u32, MEMF_PUBLIC)

match mem {
    Some(ptr) => {
        // Automatic cleanup at scope exit
        defer FreeMem(ptr, 1024u32)

        // Use ptr...
        // FreeMem automatically called when block exits
    },
    None => { /* allocation failed */ }
}
```

**Improvements**:
- ✅ Can't forget to free (automatic)
- ✅ Exception-safe (defer runs even on early return)

**Still allows**:
- ❌ Double-free (if you defer twice)
- ❌ Use-after-free (if you manually call FreeMem)
- ❌ Wrong size in defer

**Use when**: You need RAII-style cleanup but want manual pointer control

---

### Level 3: Typed Allocation (✅✅ SAFER)

```novus
from std::mem import Allocation, MEMF_FAST

// Type-safe bulk allocation
let alloc_opt: Option<Allocation<i32>> = Allocation::new(100u32, MEMF_FAST)

match alloc_opt {
    Some(mut alloc) => {
        // Size is remembered automatically
        let count: u32 = alloc.count()  // 100
        let bytes: u32 = alloc.size_bytes()  // 400

        // Get typed pointer
        let ptr: *i32 = alloc.as_mut_ptr()
        ptr[0] = 42
        ptr[99] = 100

        // Explicit cleanup (size automatic!)
        alloc.drop()
    },
    None => { /* allocation failed */ }
}
```

**Improvements**:
- ✅ Size remembered automatically
- ✅ Type-safe (can't mix i32* with u32*)
- ✅ Can't pass wrong size to drop()
- ✅ Explicit lifetime management

**Still allows**:
- ❌ Use-after-free (if you keep pointer after drop)
- ❌ Forgetting to call drop()

**Use when**:
- Implementing collections (Vec, HashMap, etc.)
- Managing arrays/buffers
- FFI with typed buffers

---

### Level 4: Box<T> (✅✅✅ SAFEST)

```novus
from std::mem import Box

// Single heap value
let boxed_opt: Option<Box<i32>> = Box::new(42)

match boxed_opt {
    Some(mut boxed) => {
        // Size is automatic (@sizeof(i32))
        let value: i32 = *boxed.get()
        *boxed.get_mut() = 100

        // Extract value and free in one operation
        let final_value: i32 = boxed.into_inner()
        // boxed is now dropped and can't be used
    },
    None => { /* allocation failed */ }
}
```

**Improvements**:
- ✅ Size completely automatic
- ✅ Type-safe
- ✅ Simple API
- ✅ `into_inner()` extracts value safely

**Still allows**:
- ❌ Use-after-free (if you keep pointer after drop)

**Use when**:
- Single large values on heap
- Recursive data structures
- Simple heap allocations

---

## Future: Move Semantics & Linear Types

When Novus adds move semantics and linear types, we'll achieve **complete safety**:

```novus
// Future syntax (not yet implemented)
let alloc: Allocation<i32> = Allocation::new(100).unwrap();

// alloc is moved into function (original binding invalid)
use_allocation(move alloc);

// Compiler error: alloc was moved!
// alloc.drop();  // ❌ Compile error
```

This will prevent:
- ✅ Use-after-free (compiler tracks ownership)
- ✅ Double-free (can't drop twice)
- ✅ Forgetting to free (compiler enforces Drop)

---

## Recommendations

| Use Case | Recommended Level |
|----------|------------------|
| AmigaOS FFI | Level 0 (Raw FFI) |
| Manual optimization | Level 1 (Option wrappers) + defer |
| Collections (Vec, HashMap) | Level 3 (Allocation<T>) |
| Single heap values | Level 4 (Box<T>) |
| Recursive structures | Level 4 (Box<T>) |

---

## Examples

### Example: Manual Buffer Management

```novus
from std::mem import Allocation, MEMF_CHIP

fn process_graphics() -> bool {
    // Allocate chip memory for graphics
    let buffer_opt = Allocation::new(320u32 * 256u32, MEMF_CHIP)

    match buffer_opt {
        Some(mut buffer) => {
            defer buffer.drop()  // RAII cleanup

            let pixels: *u8 = buffer.as_mut_ptr()

            // Use pixels...
            pixels[0] = 255u8

            return true
            // buffer.drop() called automatically
        },
        None => {
            return false  // Out of memory
        }
    }
}
```

### Example: Heap-Allocated Struct

```novus
from std::mem import Box

struct BigData {
    values: [1024]i32,
    name: String,
}

fn make_big_data() -> Option<Box<BigData>> {
    let data = BigData {
        values: {0, 1, 2, ...},  // 4KB array
        name: "test",
    }

    return Box::new(data)
}

fn main() -> i32 {
    let boxed_opt = make_big_data()

    match boxed_opt {
        Some(mut boxed) => {
            defer boxed.drop()

            let data_ptr = boxed.get_mut()
            data_ptr.values[0] = 999

            return 42
        },
        None => return 1
    }
}
```

---

## Safety Checklist

When using manual memory management:

- [ ] Every allocation has corresponding free
- [ ] Use `defer` for automatic cleanup
- [ ] Don't call FreeMem/drop() twice
- [ ] Don't use pointer after free
- [ ] Use Allocation<T> to avoid size tracking bugs
- [ ] Use Box<T> for simple single values
- [ ] Consider if you really need manual allocation

**Best practice**: Use the highest safety level that meets your performance needs.
