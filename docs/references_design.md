# Non-Null References in Novus

## Core Principle

**References (`&T` and `&mut T`) are GUARANTEED NON-NULL at compile time.**

This is enforced by the type system and semantic analysis. It is **impossible** to create a null reference in safe Novus code.

---

## Two Pointer Types

### 1. References: `&T` and `&mut T` (ALWAYS non-null)

```novus
fn read_value(x: &i32) {
    // x is GUARANTEED to point to valid memory
    println("Value: {}", *x)
    // No null check needed - compiler guarantees it!
}

fn modify_value(x: &mut i32) {
    *x = 42  // GUARANTEED safe - x cannot be null
}
```

✅ Guaranteed non-null
✅ Compiler enforced
✅ No runtime overhead (just a pointer)
✅ Can't be reassigned to point elsewhere (like C++ references)

### 2. Raw Pointers: `*T` (CAN be null)

```novus
fn ffi_function(ptr: *u8) {
    if ptr == null {  // Must check!
        return
    }
    // Now safe to use
}
```

❌ Can be null
❌ Must check before use
✅ Required for FFI
✅ Required for nullable optional pointers

---

## How Non-Null is Guaranteed

### Valid Ways to Create References

#### 1. Borrow from a variable

```novus
var x = 10
let r: &i32 = &x  // Always valid - x exists

var y = 20
let rm: &mut i32 = &mut y  // Always valid
```

**Why safe:** Variables exist in memory, so their addresses are always valid.

#### 2. Borrow from a struct field

```novus
struct Point {
    x: i32
    y: i32
}

var p = Point { x: 10, y: 20 }
let rx: &i32 = &p.x  // Always valid - p.x exists
```

#### 3. Borrow from array element

```novus
var arr = [1, 2, 3, 4, 5]
let r: &i32 = &arr[0]  // Always valid (bounds checked)
```

#### 4. Borrow from another reference

```novus
fn pass_along(x: &i32) -> &i32 {
    return x  // x is already non-null, so return value is non-null
}
```

#### 5. Borrow from Box/Rc

```novus
var buf = Box.alloc::<u8>(1024, MEMF_CHIP)?
let r: &u8 = &buf[0]  // Always valid - Box guarantees valid memory
```

### INVALID Ways to Create References (Compiler errors)

#### 1. Cannot create from null

```novus
let r: &i32 = null  // COMPILE ERROR: cannot assign null to reference
```

#### 2. Cannot create from arbitrary pointer without check

```novus
var ptr: *i32 = get_nullable_pointer()
let r: &i32 = ptr  // COMPILE ERROR: must check for null first

// Correct way:
if ptr != null {
    let r: &i32 = unsafe { &(*ptr) }  // Explicit unsafe conversion
    // Use r...
}
```

#### 3. Cannot create from integer

```novus
let r: &i32 = 0x1000 as &i32  // COMPILE ERROR
```

#### 4. Cannot create from uninitialized memory

```novus
var x: i32  // Uninitialized
let r = &x  // COMPILE ERROR: x not initialized
```

---

## Type System Rules

### Rule 1: References and Pointers are Distinct Types

```novus
var x = 10

let r: &i32 = &x       // Reference (non-null)
let p: *i32 = &x       // Pointer (can be null)

// These are NOT interchangeable:
let r2: &i32 = p       // ERROR: *i32 is not &i32
let p2: *i32 = r       // OK: references can convert to pointers
```

### Rule 2: Cannot Assign Null to Reference

```novus
var r: &i32 = &x
r = null  // COMPILE ERROR: references cannot be null
```

### Rule 3: Pointer to Reference Requires Null Check

```novus
fn nullable_ptr() -> *i32 { /* ... */ }

var ptr = nullable_ptr()

// Option A: Check and use as pointer
if ptr != null {
    *ptr = 42  // OK - checked
}

// Option B: Convert to reference (unsafe)
if ptr != null {
    let r = unsafe { &(*ptr) }
    *r = 42  // OK - guaranteed non-null after check
}

// Option C: Use safe wrapper
let r = ptr.as_ref()?  // Returns Option<&i32>
*r = 42
```

### Rule 4: FFI Functions Return Pointers, Not References

```novus
// FFI (can return null)
extern fn AllocMem(size: u32, flags: u32) -> *u8  // Nullable pointer

// Safe wrapper (returns reference)
fn alloc_mem(size: u32, flags: u32) -> Result<Box<u8>, Error> {
    let ptr = AllocMem(size, flags)
    if ptr == null {
        return Err(Error.OutOfMemory)
    }
    return Ok(unsafe { Box.from_raw(ptr, size, flags) })
}
```

---

## Grammar Changes

```antlr
type
    : '&' 'mut'? type      # ReferenceType    // Non-null reference
    | '*' type             # PointerType      // Nullable pointer
    | // ... existing types
    ;

expression
    : '&' 'mut'? expression   # BorrowExpr
    | '*' expression          # DerefExpr
    | // ... existing expressions
    ;
```

---

## Semantic Analysis Enforcement

### Check 1: Reference Creation

```csharp
public override IrType? VisitBorrowExpr(BorrowExprContext context) {
    var expr = Visit(context.expression())

    // Can only borrow from lvalues (variables, fields, array elements)
    if (!IsLValue(context.expression())) {
        Error("cannot borrow from temporary value")
        return null
    }

    // Check if source is initialized
    if (!IsInitialized(context.expression())) {
        Error("cannot borrow from uninitialized variable")
        return null
    }

    var isMutable = context.GetChild(0)?.GetText() == "&mut"
    return new IrRefType(expr, isMutable)
}
```

### Check 2: Reference Assignment

```csharp
public override IrType? VisitAssignment(AssignmentContext context) {
    var targetType = GetType(context.target())
    var valueType = GetType(context.value())

    // Cannot assign null to reference
    if (targetType is IrRefType && IsNullLiteral(context.value())) {
        Error("cannot assign null to reference type")
        return null
    }

    // Cannot assign pointer to reference without conversion
    if (targetType is IrRefType && valueType is IrPointerType) {
        Error("cannot convert nullable pointer to non-null reference")
        Suggestion("use 'if ptr != null { &(*ptr) }' to convert after null check")
        return null
    }

    return targetType
}
```

### Check 3: Pointer to Reference Conversion

```csharp
// Only allowed in unsafe blocks
public override IrType? VisitDerefExpr(DerefExprContext context) {
    var expr = Visit(context.expression())

    if (expr is IrPointerType ptrType) {
        // Dereferencing pointer in safe code requires null check
        if (!IsInsideNullCheck(context)) {
            Warning("dereferencing potentially null pointer")
            Suggestion("check for null before dereferencing")
        }
        return ptrType.PointeeType
    }

    // ... handle other cases
}
```

---

## Examples

### Example 1: Simple Reference Passing

```novus
fn add_ten(x: &mut i32) {
    *x = *x + 10
}

fn main() {
    var value = 5
    add_ten(&mut value)  // Pass mutable reference
    println("Value: {}", value)  // Prints: Value: 15
}
```

### Example 2: Read-Only Reference

```novus
struct Point {
    x: i32
    y: i32
}

fn print_point(p: &Point) {
    println("({}, {})", p.x, p.y)  // Can read
    // p.x = 100  // ERROR: p is immutable reference
}

fn move_point(p: &mut Point, dx: i32, dy: i32) {
    p.x = p.x + dx  // Can modify
    p.y = p.y + dy
}

fn main() {
    var point = Point { x: 10, y: 20 }
    print_point(&point)           // Immutable borrow
    move_point(&mut point, 5, 3)  // Mutable borrow
    print_point(&point)
}
```

### Example 3: FFI with Null Safety

```novus
use ffi::exec::*

// Raw FFI (returns nullable pointer)
extern fn AllocMem(size: u32, flags: u32) -> *u8

// Safe wrapper (returns non-null reference in Box)
fn alloc_buffer(size: u32, flags: u32) -> Result<Box<u8>, Error> {
    let ptr = AllocMem(size, flags)

    if ptr == null {
        return Err(Error.OutOfMemory)
    }

    // Unsafe conversion: we checked for null, so it's safe
    return Ok(unsafe { Box.from_raw(ptr, size, flags) })
}

fn main() -> Result<(), Error> {
    // Safe: buffer is guaranteed non-null
    var buffer = alloc_buffer(1024, MEMF_CHIP)?

    // Can safely use without null checks
    buffer[0] = 42

    return Ok(())
}  // buffer automatically freed
```

### Example 4: Optional References

For cases where you might not have a reference:

```novus
// Option type wraps the reference
fn find_max(arr: &[i32]) -> Option<&i32> {
    if arr.len() == 0 {
        return None
    }

    var max = &arr[0]
    for i in 1..arr.len() {
        if arr[i] > *max {
            max = &arr[i]
        }
    }

    return Some(max)  // Returns non-null reference wrapped in Option
}

fn main() {
    var numbers = [5, 2, 8, 1, 9]

    match find_max(&numbers) {
        Some(max) => println("Max: {}", *max),  // max is guaranteed non-null
        None => println("Empty array")
    }
}
```

### Example 5: Method Calls (self is a reference)

```novus
struct Counter {
    value: i32
}

impl Counter {
    // Immutable self
    fn get(&self) -> i32 {  // self is &Counter
        return self.value
    }

    // Mutable self
    fn increment(&mut self) {  // self is &mut Counter
        self.value = self.value + 1
    }
}

fn main() {
    var counter = Counter { value: 0 }

    println("Value: {}", counter.get())  // Auto-borrows as &counter
    counter.increment()                   // Auto-borrows as &mut counter
    println("Value: {}", counter.get())
}
```

---

## Lifetime Tracking (Future)

For now, references have simple rules:
1. Can't outlive the thing they borrow from
2. Can't have multiple mutable borrows

**Simple check:** References can't be stored in structs (for now).

```novus
struct Bad {
    ptr: &i32  // ERROR: references in structs require lifetime annotations
}

// This is OK (temporary):
fn good(x: &i32) {
    let y = x  // OK: y doesn't outlive x
}
```

**Future:** Add lifetime annotations like Rust:

```novus
struct Container<'a> {
    value: &'a i32
}
```

But we don't need this initially!

---

## Implementation Phases

### Phase 1: Basic References (Week 1)
- [ ] Add `&T` and `&mut T` to type system
- [ ] Parse `&` and `&mut` expressions
- [ ] Enforce non-null in semantic analyzer
- [ ] Prevent null assignment to references
- [ ] Prevent uninitialized borrows

### Phase 2: Borrow Rules (Week 2)
- [ ] Track mutable vs immutable borrows
- [ ] Prevent multiple mutable borrows
- [ ] Prevent mutable + immutable borrows simultaneously
- [ ] Simple lifetime checks (can't outlive source)

### Phase 3: Code Generation (Week 3)
- [ ] Generate same code as pointers (references are just pointers)
- [ ] Auto-dereference in expressions
- [ ] Method call syntax (auto-borrow)

### Phase 4: Advanced (Future)
- [ ] Lifetime annotations for structs
- [ ] Full borrow checker
- [ ] Interior mutability (RefCell, etc.)

---

## Benefits

✅ **Guaranteed null-safety** - References can NEVER be null
✅ **Zero runtime overhead** - References compile to simple pointers
✅ **Clear intent** - `&T` vs `&mut T` shows if you're reading or modifying
✅ **Compiler enforced** - Can't mess up even if you try
✅ **FFI compatible** - Raw pointers still available for C APIs
✅ **Modern safety** - Same guarantees as Rust
✅ **Simple to understand** - No complex borrow checker (initially)

---

## Comparison: References vs Pointers

| Feature | Reference (`&T`) | Pointer (`*T`) |
|---------|------------------|----------------|
| Can be null? | ❌ NEVER | ✅ Yes |
| Compiler enforced? | ✅ Yes | ❌ No |
| Runtime overhead? | None | None |
| Can reassign? | ❌ No (like C++) | ✅ Yes |
| For FFI? | ❌ No | ✅ Yes |
| Dereference syntax | `*r` or auto | `*p` |
| Create syntax | `&x` or `&mut x` | `&x` or FFI |

---

## FAQ

### Q: What about null pointers for "not found" cases?

**A:** Use `Option<&T>` instead:

```novus
fn find(arr: &[i32], target: i32) -> Option<&i32> {
    for i in 0..arr.len() {
        if arr[i] == target {
            return Some(&arr[i])  // Found: return reference
        }
    }
    return None  // Not found: no reference
}
```

### Q: How do I work with NDK functions that return nullable pointers?

**A:** Check for null and convert:

```novus
let ptr = OpenLibrary("dos.library", 0)

if ptr == null {
    return Err(Error.LibraryNotFound)
}

// Now safe to use (still as pointer)
// OR convert to reference:
let lib_ref = unsafe { &(*ptr) }
```

### Q: Can references dangle?

**A:** Not with basic lifetime checks:

```novus
fn bad() -> &i32 {
    var x = 10
    return &x  // ERROR: x will be destroyed, reference would dangle
}
```

### Q: Do I need to understand lifetimes?

**A:** Not initially! Simple rules:
1. Can only borrow from things that exist
2. Can't return references to local variables
3. Can't store references in structs (for now)

---

## Summary

**References (`&T` and `&mut T`) are GUARANTEED non-null by:**

1. **Type system** - References are distinct from pointers
2. **Semantic analysis** - Compiler rejects null assignments
3. **Borrow rules** - Can only borrow from valid lvalues
4. **Initialization tracking** - Can't borrow uninitialized variables
5. **FFI separation** - FFI returns pointers, not references

**Result:** It is **impossible** to have a null reference in safe Novus code!

This gives you the same null-safety guarantees as Rust, with a simpler initial implementation.

Ready to implement? 🚀
