# Novus Standard Library Style Guide

This document establishes the API conventions for the Novus standard library. Following these patterns ensures consistency across all stdlib types and makes the library intuitive for developers.

## Core Principles

1. **Result-First**: Use `Result<T, E>` as the primary return type for fallible operations
2. **Explicit Errors**: Provide specific error types, not generic failures
3. **No Silent Failures**: Operations that can fail must return `Result` or `Option`
4. **Predictable Naming**: Use consistent naming conventions across all types

---

## Constructor Naming

### `new()` - Default Constructor

For types that don't allocate memory or can be created with zero cost:

```novus
// Returns the type directly - no allocation, cannot fail
pub fn new() -> T
```

Examples:
- `Vec::<T>::new()` - Returns zeroed Vec (no allocation until first push)
- `String::new()` - Returns empty String (no allocation until first push)
- `NameString::new()` - Stack-allocated fixed buffer
- `IntrusiveList::<T>::new()` - No allocation, pointer-based

### `with_capacity(n)` - Preallocating Constructor

For types that allocate memory upfront:

```novus
// Returns Result with subsystem-specific error type
pub fn with_capacity(capacity: u32) -> Result<T, SubsystemError>
```

Examples:
- `Vec::<T>::with_capacity(n)` - Returns `Result<Vec<T>, ExecError>`
- `String::with_capacity(n)` - Returns `Result<String, StringError>`
- `HashMap::<K,V>::with_capacity(n)` - Returns `Result<HashMap<K,V>, ExecError>`

### Allocating `new()` Constructors

When `new()` allocates (e.g., with a default capacity), it should return `Result`:

```novus
// Allocates default capacity (e.g., 256 bytes)
pub fn new() -> Result<T, SubsystemError>
```

Examples:
- `StringBuilder::new()` - Returns `Result<StringBuilder, StringError>` (allocates 256)
- `HashMap::<K,V>::new()` - Returns `Result<HashMap<K,V>, ExecError>` (allocates 16 slots)
- `Formatter::new()` - Returns `Result<Formatter, StringError>` (allocates 256)

---

## Error Handling Patterns

### Error Type Taxonomy

Each subsystem has its own error enum:

| Subsystem | Error Type | Common Variants |
|-----------|------------|-----------------|
| Exec/Memory | `ExecError` | `NoMem`, `InvalidPtr` |
| Strings | `StringError` | `AllocationFailed`, `BufferFull`, `InvalidUtf8` |
| DOS/Files | `DosError` | `NotFound`, `AccessDenied`, `IoError` |
| Graphics | `GfxError` | `NoChips`, `ModeNotSupported` |
| Prefs | `PrefsError` | `NotFound`, `ParseError`, `VersionMismatch` |

### When to Use `Result<T, E>`

Use `Result` when the operation can fail due to:
- Memory allocation failure
- I/O errors
- Invalid input that should be rejected
- Resource exhaustion

```novus
// Allocation can fail
pub fn push(&var self, consuming value: T) -> Result<(), ExecError>

// Growth can fail
pub fn reserve(&var self, additional: u32) -> Result<(), ExecError>

// Insertion can fail (allocation or validation)
pub fn insert(&var self, consuming key: K, consuming value: V) -> Result<Option<V>, ExecError>
```

### When to Use `Option<T>`

Use `Option` when the operation might not find a value (not an error):

```novus
// Element might not exist
pub fn get(&self, index: u32) -> Option<&T>

// Collection might be empty
pub fn pop(&var self) -> Option<T>

// Key might not exist
pub fn find(&self, key: &K) -> Option<&V>

// Iterator might be exhausted
pub fn next(&var self) -> Option<T>
```

### Never Use `bool` for Fallible Operations

Old pattern (avoid):
```novus
// BAD - no error information
pub fn push(&var self, consuming value: T) -> bool
```

New pattern (use):
```novus
// GOOD - explicit error type
pub fn push(&var self, consuming value: T) -> Result<(), ExecError>
```

---

## Method Naming Conventions

### Mutability in Method Names

- Methods that modify `self` take `&var self`
- No special naming needed - mutability is in the signature
- Use `get` and `get_mut` pair for accessor variants

```novus
pub fn get(&self, index: u32) -> Option<&T>          // Immutable borrow
pub fn get_mut(&var self, index: u32) -> Option<&var T>  // Exclusive borrow
```

### Consuming Methods

Methods that consume `self` use `consuming self`:

```novus
pub fn finish(consuming self) -> String  // Consumes builder, returns String
pub fn into_vec(consuming self) -> Vec<T>  // Consumes, returns underlying Vec

// Fluent builder steps also consume and return the builder, preventing reuse
// of stale pre-step values.
pub fn title(consuming self, title: Str) -> WindowBuilder
```

### Conversion Methods

| Pattern | Meaning |
|---------|---------|
| `as_*` | Cheap view/reference (no allocation) |
| `to_*` | Potentially allocating copy |
| `into_*` | Consuming conversion |
| `from_*` | Static constructor from another type |

```novus
pub fn as_slice(&self) -> Slice<T>    // Owner-tied view into a collection
pub fn to_vec(&self) -> Result<Vec<T>, ExecError>  // Allocating copy
pub fn into_vec(consuming self) -> Vec<T>  // Consuming conversion
pub fn from_slice(s: Str) -> Result<String, StringError>  // Constructor
```

Owning wrappers use one vocabulary: `system()` borrows the next safe layer,
`as_raw()` borrows the native handle, `into_raw(consuming self)` transfers
ownership after disarming `Drop`, and validating `from_raw(...)` adopts
ownership. Raw pointers stay at `amiga::raw` and explicit escape hatches; safe collection
views and iterators store `&T` or `&var T` so the compiler can tie them to
their owner.

### Borrowed Views

Reference fields make the whole aggregate owner-tied. This applies recursively
through `Option`, `Result`, tuples, arrays, and generic containers.

```novus
pub struct View<T> { value: &T }

// Inferred from &self.
pub fn as_str(&self) -> Str

// Select one source when elision would be ambiguous.
@borrows(right)
pub fn suffix(left: Str, right: Str) -> Str

// Permanent literals and static storage have no runtime owner.
@borrows(static)
pub fn reset_code() -> Str { return "\x9b0m" }
```

Do not hide a borrow in `*T`. A constructor that asserts a raw pointer or span
is valid must be `@unsafe`; callers then opt into that boundary explicitly.
Safe APIs should accept `&T`, `&var T`, `Slice<T>`, or `MutSlice<T>`.

---

## Pattern Matching Conventions

### Matching on `Result`

Always use fully-qualified variants in pattern matches:

```novus
match vec.push(value) {
    Result::Ok(_) => { /* success */ },
    Result::Err(e) => { /* handle error */ },
}
```

### Ignoring Results

When you intentionally ignore a Result, use `let _ =`:

```novus
let _ = vec.push(value)  // Explicitly ignored
```

### Unit Results

Note: Novus parser doesn't support `Result::Ok(())` pattern. Use `Result::Ok(_)`:

```novus
// Correct
match operation() {
    Result::Ok(_) => { },
    Result::Err(e) => return Result::Err(e),
}

// Incorrect - parser error
match operation() {
    Result::Ok(()) => { },  // Won't parse!
    Result::Err(e) => return Result::Err(e),
}
```

---

## Collection API Patterns

### Standard Methods

Every collection should implement:

| Method | Signature | Description |
|--------|-----------|-------------|
| `new()` | `-> T` or `-> Result<T, E>` | Create empty/default |
| `with_capacity(n)` | `-> Result<T, E>` | Preallocate capacity |
| `len()` | `-> u32` | Number of elements |
| `capacity()` | `-> u32` | Allocated capacity |
| `is_empty()` | `-> bool` | Check if empty |
| `clear()` | `(&var self)` | Remove all elements |
| `drop()` | `(&var self)` | Free resources during scope cleanup |

### Sequence Collections (Vec, VecDeque, etc.)

| Method | Signature |
|--------|-----------|
| `push(consuming value)` | `-> Result<(), ExecError>` |
| `pop()` | `-> Option<T>` |
| `get(index)` | `-> Option<&T>` |
| `get_mut(index)` | `-> Option<&var T>` |
| `set(index, consuming value)` | `-> Result<(), ExecError>` |
| `insert(index, consuming value)` | `-> Result<(), ExecError>` |
| `remove(index)` | `-> Result<T, ExecError>` |
| `reserve(additional)` | `-> Result<(), ExecError>` |

### Map Collections (HashMap, etc.)

| Method | Signature |
|--------|-----------|
| `insert(consuming key, consuming value)` | `-> Result<Option<V>, ExecError>` |
| `get(&key)` | `-> Option<&V>` |
| `get_mut(&key)` | `-> Option<&var V>` |
| `remove(&key)` | `-> Option<V>` |
| `contains_key(&key)` | `-> bool` |

### Iteration

All collections that support iteration provide:

```novus
pub fn iter(&self) -> SomeIterator<T>
pub fn iter_mut(&var self) -> SomeMutIterator<T>  // If mutable iteration supported
```

Iterators implement:
```novus
pub fn next(&var self) -> Option<T>
```

Collection iterators instantiate `T` with an owner-tied reference or an
aggregate containing references; iterators that generate independent values
may return those values directly.

---

## RAII and Resource Management

### Drop Trait

Types that own resources implement `Drop`:

```novus
impl Drop for MyType {
    fn drop(&var self) {
        // Free resources
    }
}
```

### Explicit Cleanup

Explicit cleanup uses a mutable borrow and must leave the value disarmed so
the later automatic `Drop` is harmless:

```novus
pub fn drop(&var self) {
    // Resources freed when self goes out of scope
}
```

### Handle Types

For AmigaOS resources, use Handle wrappers:

```novus
pub struct ScreenHandle {
    ptr: *Screen,
}

impl Drop for ScreenHandle {
    fn drop(&var self) {
        if self.ptr != null {
            CloseScreen(self.ptr)
        }
    }
}
```

---

## Documentation Comments

Use `///` for public API documentation:

```novus
/// Creates a new vector with the specified capacity.
///
/// # Errors
/// Returns `ExecError::NoMem` if memory allocation fails.
///
/// # Examples
/// ```novus
/// let vec = Vec::<i32>::with_capacity(10)?
/// ```
pub fn with_capacity(capacity: u32) -> Result<Vec<T>, ExecError>
```

---

## Summary Checklist

When adding new types to the stdlib:

- [ ] Use `Result<T, E>` for allocating constructors
- [ ] Use `Result<(), E>` for fallible mutations (not `bool`)
- [ ] Use `Option<T>` for "not found" scenarios
- [ ] Follow naming: `new`, `with_capacity`, `as_*`, `to_*`, `into_*`, `from_*`
- [ ] Implement `Drop` if type owns resources
- [ ] Take ownership with `consuming`; otherwise use `&T` or `&var T`
- [ ] Return owner-tied `&T`/`&var T` views instead of raw pointers
- [ ] Use `@borrows(name)` only when return-source elision is ambiguous
- [ ] Mark raw pointer/span assertions `@unsafe`; keep safe alternatives available
- [ ] Provide `iter()` if collection is iterable
- [ ] Use subsystem-specific error types
- [ ] Document all public APIs with `///` comments
