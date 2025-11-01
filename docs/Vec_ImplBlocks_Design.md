# Vec<T> and Impl Blocks - Design Document

## Overview

This document describes the design and implementation of `Vec<T>` (dynamic arrays) and `impl` blocks (methods on types) for Novus. These are **foundational features** that enable real-world programming.

## Goals

1. **Vec<T>**: Dynamic, growable arrays with automatic memory management
2. **Impl blocks**: Associate functions/methods with types
3. **Zero-cost abstractions**: No runtime overhead compared to manual implementation
4. **Amiga-friendly**: Work within 68k constraints (limited memory, no OS allocator on bare metal)

---

## Part 1: Vec<T> - Dynamic Arrays

### Memory Layout

```
Vec<T> = {
    ptr: *mut T,      // Pointer to heap-allocated buffer (4 bytes)
    len: u32,         // Number of elements currently stored (4 bytes)
    capacity: u32,    // Number of elements allocated (4 bytes)
}
// Total: 12 bytes per Vec (fits in 3 longwords)
```

**Invariants:**
- `len <= capacity` always
- If `capacity == 0`, `ptr` can be null
- If `capacity > 0`, `ptr` points to valid memory of at least `capacity * sizeof(T)` bytes

### Growth Strategy

**Doubling strategy:**
- Start with capacity 0 (no allocation)
- On first push: allocate capacity 4
- On subsequent growth: new_capacity = max(old_capacity * 2, old_capacity + 1)

**Why doubling?**
- Amortized O(1) push operations
- Balances memory usage vs. reallocation frequency
- Standard approach in Rust, C++, etc.

**Example growth sequence:**
```
0 → 4 → 8 → 16 → 32 → 64 → 128 → ...
```

### Operations

#### Construction

**Vec::new() -> Vec<T>**
```novus
let v: Vec<i32> = Vec::new()
```
- Allocates no memory
- `ptr = null`, `len = 0`, `capacity = 0`

**Vec::with_capacity(n: u32) -> Vec<T>**
```novus
let v: Vec<i32> = Vec::with_capacity(100)
```
- Pre-allocates space for `n` elements
- `ptr = allocate(n * sizeof(T))`, `len = 0`, `capacity = n`
- Use when you know size in advance

#### Push/Pop

**push(item: T)**
```novus
v.push(42)
```
- If `len == capacity`, grow buffer (reallocate + copy)
- Store `item` at `ptr[len]`
- Increment `len`
- **Time complexity:** Amortized O(1)

**pop() -> Option<T>**
```novus
let last = v.pop()  // Some(value) or None
```
- If `len == 0`, return `None`
- Decrement `len`
- Return `Some(ptr[len])`
- Does NOT shrink capacity
- **Time complexity:** O(1)

#### Indexing

**get(index: u32) -> Option<&T>**
```novus
let item = v.get(5)?  // Returns Option<&T>
```
- If `index >= len`, return `None`
- Return `Some(&ptr[index])`
- **Time complexity:** O(1)

**set(index: u32, value: T) -> Result<(), Error>**
```novus
v.set(5, 99)?
```
- If `index >= len`, return `Err(OutOfBounds)`
- Store `value` at `ptr[index]`
- **Time complexity:** O(1)

**Operator overloading (future):**
```novus
let x = v[5]     // Sugar for v.get(5).unwrap()
v[5] = 99        // Sugar for v.set(5, 99).unwrap()
```

#### Other Operations

**len() -> u32**
```novus
let count = v.len()
```
- Return `len`
- **Time complexity:** O(1)

**capacity() -> u32**
```novus
let cap = v.capacity()
```
- Return `capacity`
- **Time complexity:** O(1)

**clear()**
```novus
v.clear()
```
- Set `len = 0` (does not deallocate)
- **Time complexity:** O(1)

**is_empty() -> bool**
```novus
if v.is_empty() { ... }
```
- Return `len == 0`
- **Time complexity:** O(1)

#### Memory Management

**drop()**
- Called automatically when Vec goes out of scope
- If `capacity > 0`, deallocate `ptr`
- Integrates with `defer` blocks

**reserve(additional: u32)**
```novus
v.reserve(100)  // Ensure space for 100 more elements
```
- Ensure `capacity >= len + additional`
- If needed, reallocate and copy

**shrink_to_fit()**
```novus
v.shrink_to_fit()  // Reduce capacity to len
```
- Reallocate to `capacity = len`
- Use to reclaim memory after many pops

### IR Representation

```rust
// Built-in generic type
IrGenericType {
    name: "Vec",
    type_params: [T],
    fields: [
        { name: "ptr", type: *mut T },
        { name: "len", type: u32 },
        { name: "capacity", type: u32 },
    ]
}

// Operations become IR instructions
IrVecNew { result: %vec, element_type: T }
IrVecPush { vec: %vec, value: %value }
IrVecPop { vec: %vec, result: %option }
IrVecGet { vec: %vec, index: %index, result: %option }
IrVecSet { vec: %vec, index: %index, value: %value, result: %result }
IrVecLen { vec: %vec, result: %len }
IrVecClear { vec: %vec }
IrVecDrop { vec: %vec }
```

### Codegen Strategy

**Struct representation:**
```asm
; Vec<i32> layout in memory:
;   0(a0): ptr      (4 bytes)
;   4(a0): len      (4 bytes)
;   8(a0): capacity (4 bytes)
```

**Example: push operation**
```asm
_Vec_i32_push:
    ; Input: a0 = vec pointer, d0 = value to push
    ; Check if len == capacity (need to grow?)
    move.l  4(a0),d1        ; d1 = len
    move.l  8(a0),d2        ; d2 = capacity
    cmp.l   d2,d1
    beq     .grow           ; If equal, need to grow

.store:
    ; Store value at ptr[len]
    move.l  (a0),a1         ; a1 = ptr
    move.l  d0,(a1,d1.l*4)  ; ptr[len] = value (i32 = 4 bytes)

    ; Increment len
    addq.l  #1,d1
    move.l  d1,4(a0)        ; vec.len++
    rts

.grow:
    ; Reallocate buffer
    ; new_capacity = capacity * 2 (or 4 if capacity == 0)
    move.l  d2,d3
    beq     .first_alloc
    lsl.l   #1,d3           ; d3 = capacity * 2
    bra     .do_alloc
.first_alloc:
    moveq   #4,d3           ; Initial capacity = 4
.do_alloc:
    ; Calculate bytes needed
    move.l  d3,d4
    lsl.l   #2,d4           ; d4 = new_capacity * 4 (sizeof i32)

    ; Allocate new buffer
    move.l  d4,d0
    jsr     _allocate       ; d0 = new buffer pointer
    move.l  d0,a2           ; a2 = new buffer

    ; Copy old elements to new buffer
    tst.l   d2
    beq     .no_copy        ; Skip if capacity was 0
    move.l  (a0),a1         ; a1 = old buffer
    move.l  4(a0),d5        ; d5 = len (number to copy)
    subq.l  #1,d5           ; Adjust for dbf
.copy_loop:
    move.l  (a1)+,(a2)+
    dbf     d5,.copy_loop

    ; Free old buffer
    move.l  (a0),d0
    jsr     _deallocate

.no_copy:
    ; Update vec structure
    move.l  a2,(a0)         ; vec.ptr = new buffer
    move.l  d3,8(a0)        ; vec.capacity = new_capacity

    ; Now do the store
    bra     .store
```

### Allocator Integration

**Chip RAM vs Fast RAM:**
- Vec<T> uses **Fast RAM** by default (faster, more available)
- For graphics (sprites, bitmaps), use explicit chip RAM allocation
- Future: `Vec::new_chip()` for chip RAM allocation

**Allocator API:**
```novus
extern fn _allocate(size: u32) -> *mut u8
extern fn _deallocate(ptr: *mut u8)
extern fn _reallocate(ptr: *mut u8, new_size: u32) -> *mut u8
```

**Implementation options:**
1. **VBCC allocator** - Link against standard library
2. **Custom allocator** - For bare metal or custom heaps
3. **Amiga Exec AllocMem** - For system integration

---

## Part 2: Impl Blocks - Methods on Types

### Syntax

**Basic impl block:**
```novus
impl Point {
    fn new(x: i32, y: i32) -> Point {
        return Point { x: x, y: y }
    }

    fn distance(self, other: Point) -> i32 {
        let dx = self.x - other.x
        let dy = self.y - other.y
        return sqrt(dx*dx + dy*dy)
    }
}
```

**Usage:**
```novus
let p1 = Point::new(10, 20)       // Associated function
let p2 = Point::new(30, 40)
let dist = p1.distance(p2)        // Method call
```

### Self Parameter Variants

**Value self (takes ownership):**
```novus
fn consume(self) { ... }
```
- Moves `self` into the function
- Cannot use `self` after call

**Immutable reference &self:**
```novus
fn get_x(&self) -> i32 {
    return self.x
}
```
- Borrows `self` immutably
- Can call multiple times

**Mutable reference &mut self:**
```novus
fn set_x(&mut self, x: i32) {
    self.x = x
}
```
- Borrows `self` mutably
- Can modify fields

**No self (associated function):**
```novus
fn new() -> Point { ... }
```
- Called as `Point::new()`
- Like static method in C++/Java

### Method Resolution

**Lookup order:**
1. Methods defined in `impl Type`
2. Methods from trait implementations (future)
3. Methods from extensions (future)

**Example:**
```novus
let p = Point { x: 10, y: 20 }
p.distance(other)  // Looks up Point::distance
```

**Desugaring:**
```novus
// Method call:
p.distance(other)

// Desugars to:
Point::distance(p, other)
```

### Grammar Changes

**New productions:**
```antlr
implBlock
    : 'impl' genericParams? type '{' implItem* '}'
    ;

implItem
    : function
    ;

primaryExpression
    : /* existing rules */
    | type '::' IDENTIFIER '(' expressionList? ')'  // Associated function call
    ;

postfixExpression
    : /* existing rules */
    | postfixExpression '.' IDENTIFIER '(' expressionList? ')'  // Method call
    ;
```

### Semantic Analysis

**Impl block registration:**
```csharp
// Store methods per type
Dictionary<IrType, List<IrFunction>> _typeMethods = new();

void VisitImplBlock(ImplBlockContext ctx) {
    var type = ResolveType(ctx.type());
    var methods = new List<IrFunction>();

    foreach (var item in ctx.implItem()) {
        var method = VisitFunction(item.function());
        method.IsMethod = true;
        method.ReceiverType = type;
        methods.Add(method);
    }

    _typeMethods[type] = methods;
}
```

**Method call resolution:**
```csharp
IrValue VisitMethodCall(MethodCallContext ctx) {
    var receiver = Visit(ctx.receiver());
    var methodName = ctx.IDENTIFIER().GetText();

    // Look up method
    if (!_typeMethods.TryGetValue(receiver.Type, out var methods)) {
        ReportError($"No methods defined for type {receiver.Type}");
    }

    var method = methods.FirstOrDefault(m => m.Name == methodName);
    if (method == null) {
        ReportError($"Method {methodName} not found on type {receiver.Type}");
    }

    // Desugar to function call: Type::method(receiver, args...)
    var args = new List<IrValue> { receiver };
    args.AddRange(ctx.arguments());

    return new IrCall {
        FunctionName = $"{receiver.Type}::{methodName}",
        Arguments = args
    };
}
```

### IR Representation

**Method as function with receiver:**
```rust
IrFunction {
    name: "Point::distance",
    receiver_type: Some(Point),
    self_kind: Value,  // or Ref, or MutRef
    parameters: [other: Point],
    return_type: i32,
    body: ...
}

// Method call becomes:
IrCall {
    function_name: "Point::distance",
    arguments: [receiver, other]
}
```

### Codegen

**Method calls compile to function calls:**
```asm
; p1.distance(p2) becomes Point::distance(p1, p2)
_Point_distance:
    link    a6,#-16
    ; a6+8  = self (Point)
    ; a6+16 = other (Point)

    ; self.x
    move.l  8(a6),d0    ; self.x
    ; other.x
    move.l  16(a6),d1   ; other.x
    ; dx = self.x - other.x
    sub.l   d1,d0
    ; ... rest of calculation

    unlk    a6
    rts
```

**Associated function calls:**
```asm
; Point::new(10, 20)
; Just a regular function call
    move.l  #20,-(sp)
    move.l  #10,-(sp)
    jsr     _Point_new
    lea     8(sp),sp
```

---

## Part 3: Vec<T> Methods Using Impl Blocks

### Standard Vec<T> API

```novus
impl<T> Vec<T> {
    // Construction
    fn new() -> Vec<T> {
        return Vec { ptr: null, len: 0, capacity: 0 }
    }

    fn with_capacity(capacity: u32) -> Vec<T> {
        let ptr = if capacity > 0 {
            allocate(capacity * sizeof(T))
        } else {
            null
        }
        return Vec { ptr: ptr, len: 0, capacity: capacity }
    }

    // Accessors
    fn len(&self) -> u32 {
        return self.len
    }

    fn capacity(&self) -> u32 {
        return self.capacity
    }

    fn is_empty(&self) -> bool {
        return self.len == 0
    }

    // Modification
    fn push(&mut self, item: T) {
        if self.len == self.capacity {
            self.grow()
        }
        unsafe {
            *(self.ptr + self.len) = item
        }
        self.len = self.len + 1
    }

    fn pop(&mut self) -> Option<T> {
        if self.len == 0 {
            return None
        }
        self.len = self.len - 1
        unsafe {
            return Some(*(self.ptr + self.len))
        }
    }

    fn get(&self, index: u32) -> Option<&T> {
        if index >= self.len {
            return None
        }
        unsafe {
            return Some(&*(self.ptr + index))
        }
    }

    fn set(&mut self, index: u32, value: T) -> Result<(), Error> {
        if index >= self.len {
            return Err(Error::OutOfBounds)
        }
        unsafe {
            *(self.ptr + index) = value
        }
        return Ok(())
    }

    fn clear(&mut self) {
        self.len = 0
    }

    // Internal
    fn grow(&mut self) {
        let new_capacity = if self.capacity == 0 {
            4
        } else {
            self.capacity * 2
        }
        self.reserve(new_capacity - self.len)
    }

    fn reserve(&mut self, additional: u32) {
        let new_capacity = self.len + additional
        if new_capacity <= self.capacity {
            return
        }

        let new_ptr = allocate(new_capacity * sizeof(T))

        // Copy old elements
        unsafe {
            for i in 0..self.len {
                *(new_ptr + i) = *(self.ptr + i)
            }
        }

        // Free old buffer
        if self.capacity > 0 {
            deallocate(self.ptr)
        }

        self.ptr = new_ptr
        self.capacity = new_capacity
    }
}
```

### Example Usage

```novus
fn main() -> i32 {
    // Create empty vector
    var numbers: Vec<i32> = Vec::new()

    // Add elements
    numbers.push(10)
    numbers.push(20)
    numbers.push(30)

    // Access elements
    match numbers.get(1) {
        Some(value) => {
            // value is 20
        },
        None => { }
    }

    // Iterate (with manual indexing for now)
    var i: u32 = 0
    while i < numbers.len() {
        match numbers.get(i) {
            Some(value) => {
                // Do something with value
            },
            None => { }
        }
        i = i + 1
    }

    // Modify
    numbers.set(1, 99)?

    // Remove last
    let last = numbers.pop()

    // Clear all
    numbers.clear()

    return 0
}
```

---

## Implementation Phases

### Phase 1: Parser & Grammar (1 week)
- [ ] Add Vec<T> type syntax
- [ ] Add impl block syntax
- [ ] Add method call syntax (.)
- [ ] Add associated function call syntax (::)
- [ ] Update tests

### Phase 2: Semantic Analysis (1-2 weeks)
- [ ] Generic type checking for Vec<T>
- [ ] Impl block registration
- [ ] Method lookup and resolution
- [ ] Self parameter handling
- [ ] Type checking for method calls

### Phase 3: IR & Codegen (1-2 weeks)
- [ ] Vec<T> IR instructions
- [ ] Method call desugaring
- [ ] Vec operations codegen
- [ ] Memory allocation integration
- [ ] Self parameter in calling convention

### Phase 4: Standard Library (1 week)
- [ ] Implement Vec<T> in std/vec.novus
- [ ] Add all Vec methods
- [ ] Add tests
- [ ] Add examples

### Phase 5: Testing & Polish (1 week)
- [ ] Unit tests for Vec operations
- [ ] Integration tests for method calls
- [ ] Performance tests
- [ ] Memory leak tests
- [ ] Documentation

**Total estimated time: 4-6 weeks**

---

## Testing Strategy

### Unit Tests

**Vec operations:**
```novus
fn test_vec_new() {
    let v: Vec<i32> = Vec::new()
    assert(v.len() == 0)
    assert(v.capacity() == 0)
}

fn test_vec_push() {
    var v: Vec<i32> = Vec::new()
    v.push(10)
    v.push(20)
    assert(v.len() == 2)
}

fn test_vec_pop() {
    var v: Vec<i32> = Vec::new()
    v.push(10)
    let x = v.pop()
    match x {
        Some(val) => { assert(val == 10) },
        None => { panic("Expected Some") }
    }
}

fn test_vec_growth() {
    var v: Vec<i32> = Vec::new()
    for i in 0..100 {
        v.push(i)
    }
    assert(v.len() == 100)
}
```

**Method calls:**
```novus
fn test_method_call() {
    let p = Point::new(10, 20)
    assert(p.get_x() == 10)
}

fn test_method_mutation() {
    var p = Point::new(10, 20)
    p.set_x(99)
    assert(p.get_x() == 99)
}
```

### Integration Tests

**Real-world usage:**
```novus
fn test_entity_list() {
    var entities: Vec<Entity> = Vec::new()

    // Add entities
    for i in 0..10 {
        entities.push(Entity::new(i, i * 2))
    }

    // Update all
    for i in 0..entities.len() {
        match entities.get(i) {
            Some(entity) => {
                entity.update()
            },
            None => { }
        }
    }

    // Remove dead entities
    var i: u32 = 0
    while i < entities.len() {
        match entities.get(i) {
            Some(entity) => {
                if entity.is_dead() {
                    // TODO: remove at index
                }
            },
            None => { }
        }
        i = i + 1
    }
}
```

---

## Future Enhancements

### Vec Features (Phase 2)
- [ ] insert(index, value)
- [ ] remove(index) -> Option<T>
- [ ] extend(other: Vec<T>)
- [ ] append(&mut other: Vec<T>)
- [ ] drain(range) -> Iterator<T>
- [ ] truncate(len)
- [ ] swap(i, j)
- [ ] reverse()
- [ ] sort() (requires Ord trait)

### Iterator Integration (Phase 2)
- [ ] Implement Iterator trait for Vec
- [ ] iter() -> Iter<T>
- [ ] iter_mut() -> IterMut<T>
- [ ] into_iter() -> IntoIter<T>

### Operator Overloading (Phase 3)
- [ ] Index trait: v[i]
- [ ] IndexMut trait: v[i] = x
- [ ] Drop trait: Automatic cleanup

### Optimizations
- [ ] Small vector optimization (store first N elements inline)
- [ ] Copy-on-write for immutable usage
- [ ] Reserve extra space on growth to reduce allocations

---

## Open Questions

1. **Allocator abstraction?**
   - Pass allocator to Vec::new_in(allocator)?
   - Global allocator vs. per-Vec allocator?
   - Chip RAM vs Fast RAM selection?

2. **Bounds checking?**
   - Always check in debug?
   - Opt-out with unsafe indexing?
   - Panic vs. Result on bounds errors?

3. **Drop semantics?**
   - Automatic deallocation on scope exit?
   - Explicit drop() call?
   - Integration with defer blocks?

4. **Generic constraints?**
   - Can Vec<T> hold any type T?
   - Restrictions on T (must be sized, copyable, etc.)?

5. **Self parameter ABI?**
   - Pass by value, reference, or pointer?
   - Register or stack?
   - How to handle large self?

---

## References

- [Rust Vec<T> implementation](https://doc.rust-lang.org/src/alloc/vec/mod.rs.html)
- [Swift Array implementation](https://github.com/apple/swift/blob/main/stdlib/public/core/Array.swift)
- [C++ std::vector](https://en.cppreference.com/w/cpp/container/vector)
- [Amiga memory allocation](http://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node02D8.html)
