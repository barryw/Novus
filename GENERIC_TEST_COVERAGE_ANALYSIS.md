# Novus Generic Programming Analysis - Test Coverage Gaps

## Executive Summary

The Novus compiler has a **skeleton generic system** with 16 tests covering basic scenarios, but significant gaps in:
1. **Generic function constraints** (not supported)
2. **Recursive/circular generic types**
3. **Generic function pointers** (not tested)
4. **Generic functions with varargs** (not supported)
5. **Multiple instantiation sites** (limited testing)
6. **Monomorphization edge cases**
7. **Generic const/static declarations** (not tested)
8. **Generic specialization** (not supported)

## Current Test Coverage (16 tests in GenericFunctionTests.cs)

### What IS Tested:
✅ Single type parameter generic functions
✅ Multiple type parameters (2-3 params)
✅ Generic functions with generic struct parameters
✅ Generic functions returning generic types
✅ Generic function calling other generic functions
✅ Generic functions with enum types
✅ Type inference for generic calls
✅ Multiple call sites with same generic
✅ Generic functions with pointer types
✅ Generic functions in impl blocks

### What is NOT Tested:
❌ Generic constraints (trait bounds)
❌ Circular/recursive generic types
❌ Generic function pointers
❌ Varargs with generics
❌ Generic const declarations
❌ Generic static variables
❌ Specialization with constraints
❌ Unused type parameters
❌ Generic mutual recursion
❌ Generic code in different modules
❌ Conflicting constraints
❌ Higher-ranked types

## Critical Gaps with Examples

### GAP 1: Generic Constraints (Trait Bounds)
**Status:** ❌ NOT SUPPORTED - No grammar support
**Severity:** HIGH - Essential for error handling, collections APIs

**Missing Test:**
```novus
// Example of what should work but doesn't
trait Display {
    fn display(&self) -> String
}

fn print_value<T: Display>(val: T) {
    let s = val.display()
    // print(s)
}

pub fn main() -> i32 {
    // This would need String to implement Display
    print_value("hello")
    return 0
}
```

**Why it matters:**
- Novus needs `Result<T, E>` to require `E: Error` or similar
- Collections (Vec, HashMap) need element type constraints
- Without this, error handling becomes unsafe
- Major blocker for self-hosting compiler

**Implementation Status:**
- Grammar: No `where` clause support
- SemanticAnalyzer: No constraint tracking
- IrBuilder: No constraint checking
- CodeGen: N/A

---

### GAP 2: Recursive Generic Types
**Status:** ⚠️ PARTIALLY SUPPORTED - Parsing works, but edge cases untested
**Severity:** MEDIUM - Important for linked lists, trees

**Missing Tests:**
```novus
// Test 1: Self-referential generic struct (linked list)
struct Node<T> {
    value: T,
    next: *Node<T>  // Self reference with generic
}

fn create_list<T>(val: T) -> *Node<T> {
    return 0 as *Node<T>
}

pub fn main() -> i32 {
    let _n: *Node<i32> = create_list(42)
    return 0
}

// Test 2: Mutually recursive generics
struct A<T> {
    b: *B<T>
}

struct B<T> {
    a: *A<T>
}

pub fn main() -> i32 {
    return 0
}

// Test 3: Deep recursion with multiple types
struct Tree<T> {
    value: T,
    left: *Tree<T>,
    right: *Tree<T>,
    children: *Tree<T>  // Multiple recursive refs
}

pub fn main() -> i32 {
    return 0
}
```

**Why it matters:**
- Data structure implementation (Vec, HashMap, linked lists)
- Size calculations must handle recursive types correctly
- Self-hosting compiler needs BTreeMap, graph structures

**Testing Needed:**
- [ ] Size calculation for recursive generics
- [ ] Multiple recursive references in same type
- [ ] Mutual recursion between generic types
- [ ] Deep nesting levels (3+ levels)

---

### GAP 3: Generic Function Pointers
**Status:** ❌ NOT TESTED - Grammar supports fn pointers, generics separate
**Severity:** MEDIUM - Needed for callbacks, functional patterns

**Missing Tests:**
```novus
// Test 1: Generic function pointer as parameter
fn apply<T, U>(f: fn(T) -> U, val: T) -> U {
    return f(val)
}

fn double(x: i32) -> i32 {
    return x + x
}

pub fn main() -> i32 {
    let f: fn(i32) -> i32 = double
    let result = apply(f, 21)
    return result  // Should be 42
}

// Test 2: Storing generic function pointer in struct
struct Mapper<T, U> {
    func: fn(T) -> U
}

fn add_one(x: i32) -> i32 {
    return x + 1
}

pub fn main() -> i32 {
    let mapper = Mapper {
        func: add_one
    }
    return mapper.func(41)  // Should be 42
}

// Test 3: Array of generic function pointers
fn call_with_42<T>(funcs: *fn(i32) -> T, count: i32) -> T {
    return funcs(42)
}

pub fn main() -> i32 {
    let f: fn(i32) -> i32 = double
    return call_with_42(&f, 1)
}
```

**Implementation Challenge:**
- Function pointer type unification with generics
- Name mangling for generic functions passed as pointers
- Tracking which monomorphized versions are needed
- Testing combinations of generic callbacks

---

### GAP 4: Generic Functions with Varargs
**Status:** ❌ NOT SUPPORTED - No varargs syntax in grammar
**Severity:** LOW-MEDIUM - Useful for formatting, printf-style APIs

**Missing Tests:**
```novus
// Not currently supported in Novus grammar
// fn printf(format: *u8, ...args: i32) -> i32 { ... }

// Even simpler: generic with variable number of same type
fn sum_three<T>(a: T, b: T, c: T) -> T {
    // This is currently the workaround
    return a + b + c
}

pub fn main() -> i32 {
    return sum_three(10, 20, 12)  // 42
}
```

**Why it's needed:**
- Printf-style functions for debugging on Amiga
- Flexible collection constructors
- Reduce overloading burden

---

### GAP 5: Generic Methods in Generic Structs with Generic Return Types
**Status:** ⚠️ PARTIALLY SUPPORTED - Simple cases work, complex cases untested
**Severity:** MEDIUM - Standard pattern in modern systems languages

**Missing Tests:**
```novus
// Test 1: Generic method with different return type than struct
struct Box<T> {
    value: T
}

impl<T> Box<T> {
    fn map<U>(self, f: fn(T) -> U) -> Box<U> {
        // Return different generic type
        return Box { value: f(self.value) }
    }
}

pub fn main() -> i32 {
    let b = Box { value: 21 }
    let doubled: Box<i32> = b.map(fn(x: i32) -> i32 { return x + x })
    return doubled.value  // Should be 42
}

// Test 2: Method with multiple generic parameters
impl<T> Box<T> {
    fn pair<U>(self, other: U) -> Box<(T, U)> {
        // Return tuple of two different types
        // Note: tuples in Novus not fully supported yet
        return Box { value: (self.value, other) }
    }
}

// Test 3: Generic method returning unrelated type
impl<T> Box<T> {
    fn extract<U>(self) -> U {
        // Extract to completely different type
        return 0 as U  // Unsafe cast - needs explicit generic
    }
}
```

**Implementation Gaps:**
- [ ] Method instantiation with multiple generic parameters
- [ ] Return type inference across generic method calls
- [ ] Tuple type support in generics
- [ ] Generic method chaining (Box<T>::map returns Box<U>)

---

### GAP 6: Generic Const/Static Declarations
**Status:** ❌ NOT TESTED - Syntax exists but no test coverage
**Severity:** LOW - Not critical for MVP but important for library code

**Missing Tests:**
```novus
// Test 1: Generic const doesn't really make sense
// const MAGIC<T>: T = ...  // Can't initialize without concrete type

// Test 2: Generic static is questionable
// static<T> INSTANCE: T = ...  // Would need per-instantiation storage

// Better: Static factory methods (already work mostly)
struct Container<T> {
    value: T
}

impl<T> Container<T> {
    fn empty() -> Container<T> {
        // This is the pattern Rust uses
        return Container { value: 0 as T }
    }
}

pub fn main() -> i32 {
    let c: Container<i32> = Container::empty()
    return 0
}
```

**Note:** This is a language design question, not a compiler bug. May not be needed for v1.0.

---

### GAP 7: Specialization with Conflicting Constraints
**Status:** ❌ NOT SUPPORTED - No specialization system
**Severity:** MEDIUM - Advanced feature, nice-to-have for optimization

**Missing Tests:**
```novus
// This requires a trait system first
// Example of what C++ allows:

// General template:
fn process<T>(val: T) -> i32 {
    return 0  // generic implementation
}

// Specialization for pointers:
fn process<T>(val: *T) -> i32 {
    return 1  // pointer-specific implementation
}

// Specialization for i32:
fn process(val: i32) -> i32 {
    return 2  // type-specific implementation
}

pub fn main() -> i32 {
    let x: i32 = 42
    let result = process(x)
    return result  // Should dispatch to i32 specialization
}
```

**Not needed for MVP** - specialization can come in v2.0

---

### GAP 8: Monomorphization Edge Cases

#### 8a: Same Generic Type Instantiated Multiple Ways
**Status:** ⚠️ LIKELY WORKS but not thoroughly tested
**Severity:** MEDIUM - Cache correctness critical

**Missing Test:**
```novus
fn identity<T>(x: T) -> T {
    return x
}

fn process<T>(val: T) -> T {
    let x = identity(val)
    return x
}

pub fn main() -> i32 {
    // Call identity with i32 directly
    let a = identity(42)

    // Call identity through generic function
    let b = process(42)

    // Call identity with bool
    let c = identity(true)

    // Call process with bool through generic wrapper
    let d = process(true)

    // These should generate exactly 3 monomorphized versions:
    // - identity<i32>
    // - identity<bool>
    // - process<i32>
    // - process<bool>

    return a
}
```

**What could go wrong:**
- Duplicate monomorphized functions generated
- Cache key collisions
- Type equality not working correctly (TypeInterner bug)

---

#### 8b: Unused Type Parameters
**Status:** ❌ NOT TESTED
**Severity:** LOW - Weird edge case but should still compile

**Missing Test:**
```novus
fn ignore_parameter<T, U>(x: T) -> T {
    // U is unused - should still compile
    return x
}

pub fn main() -> i32 {
    // Type inference must figure out U somehow
    // This is ambiguous without explicit type annotation
    // let result = ignore_parameter(42)  // ERROR: ambiguous U

    // With explicit type:
    let result: i32 = ignore_parameter<i32, bool>(42)
    return result
}
```

---

#### 8c: Generic Function Called from Multiple Modules
**Status:** ⚠️ UNTESTED - Import handling unclear
**Severity:** MEDIUM - Self-hosting requires cross-module generics

**Missing Test Structure:**
```
File: math.novus
---
fn max<T>(a: T, b: T) -> T {
    if a > b { return a }
    return b
}

File: main.novus
---
import math

pub fn main() -> i32 {
    let x = math::max(10, 20)
    let y = math::max(3.14f, 2.71f)  // Hypothetically if floats exist
    return x
}
```

**Questions that need testing:**
- [ ] Are monomorphized functions correctly included from imported modules?
- [ ] Does cache work across module boundaries?
- [ ] Symbol visibility correct for monomorphized types?
- [ ] Object file linking handles duplicate monomorphizations?

---

## Monomorphization Implementation Status

### Currently Implemented:
✅ Generic enum type tracking (`IrEnumType.GenericParameters`)
✅ Generic struct type tracking (`IrStructType.GenericParameters`)
✅ Generic function templates storage (`_genericFunctionTemplates`)
✅ Generic method templates storage (`_genericMethodTemplates`)
✅ Basic instantiation detection (`_instantiatedGenericFunctions`, `_instantiatedMethods`)
✅ Type substitution in method calls (`_currentTypeSubstitutions`)
✅ Monomorphization cache (`_monomorphizedEnums`, `_monomorphizedStructs`)

### NOT Implemented:
❌ `GenericMonomorphizationPass` - Empty skeleton, returns `false`
❌ Type inference for ambiguous generics
❌ Constraint checking
❌ Name mangling verification
❌ Cross-module monomorphization coordination
❌ Dead code elimination of unused monomorphizations

---

## Test Statistics

| Category | Count | Status |
|----------|-------|--------|
| **Total Generic Tests** | 16 | Basic coverage only |
| **Generic Functions** | 13 | ✅ Basic scenarios |
| **Generic with Enums** | 7 (in EnumTests.cs) | ✅ Basic scenarios |
| **Generic with Structs** | 1 (in GenericFunctionTests.cs) | ⚠️ Limited |
| **Constraints/Bounds** | 0 | ❌ Not supported |
| **Recursive Types** | 0 | ❌ Not tested |
| **Function Pointers** | 0 | ❌ Not tested |
| **Varargs** | 0 | ❌ Not supported |
| **Cross-module** | 0 | ❌ Not tested |
| **Specialization** | 0 | ❌ Not supported |
| **Const/Static Generic** | 0 | ❌ Not tested |

---

## Recommended Priority for Test Coverage

### TIER 1 (CRITICAL - blocking self-hosting):
1. **Generic constraints/trait bounds** - Needed for `Error` trait
2. **Recursive generic types** - Needed for Vec, HashMap
3. **Cross-module generics** - Needed for std library usage
4. **Type inference edge cases** - Prevent silent monomorphization bugs

### TIER 2 (HIGH - important features):
5. Generic function pointers
6. Generic methods with multiple type parameters
7. Monomorphization correctness (deduplication)
8. Unused type parameter detection

### TIER 3 (MEDIUM - nice to have):
9. Generic const declarations
10. Specialization patterns
11. Varargs support
12. Circular dependency detection

---

## Code Generation Gaps

### What Works:
✅ Monomorphized enum code generation
✅ Monomorphized struct code generation
✅ Basic method dispatch on generic types

### What Doesn't Work:
❌ Constraint checking at codegen
❌ Name mangling for constraints
❌ Function pointer codegen with monomorphization
❌ Symbol visibility for monomorphized types
❌ RTTI for generic types (not needed for v1.0)

---

## Semantic Analysis Gaps

### What Works:
✅ Generic type parameter tracking (`_genericParams`)
✅ Monomorphization detection in IrBuilder

### What Doesn't Work:
❌ Constraint checking in SemanticAnalyzer
❌ Ambiguity detection (unused type params)
❌ Forward reference handling for recursive generics
❌ Cross-module generic resolution

---

## Summary Table: Which Features Exist vs. Tested

| Feature | Parsed | In IR | In Codegen | Tested | Works |
|---------|--------|-------|-----------|--------|-------|
| Generic functions | ✅ | ✅ | ⚠️ | ✅ | ✅ |
| Generic structs | ✅ | ✅ | ✅ | ✅ | ✅ |
| Generic enums | ✅ | ✅ | ✅ | ✅ | ✅ |
| Generic impl blocks | ✅ | ✅ | ✅ | ✅ | ✅ |
| Generic constraints | ❌ | ❌ | ❌ | ❌ | ❌ |
| Recursive generics | ✅ | ✅ | ⚠️ | ❌ | ? |
| Generic fn pointers | ✅ | ⚠️ | ❌ | ❌ | ❌ |
| Varargs generics | ❌ | ❌ | ❌ | ❌ | ❌ |
| Generic consts | ✅ | ⚠️ | ❌ | ❌ | ❌ |
| Cross-module generics | ✅ | ✅ | ⚠️ | ❌ | ? |
| Specialization | ❌ | ❌ | ❌ | ❌ | ❌ |

Legend: ✅ = Implemented, ⚠️ = Partially implemented, ❌ = Not implemented, ? = Unknown
