# Novus Type System Test Coverage Analysis

## Executive Summary

The Novus type system has **basic** test coverage but with **significant gaps** in edge case handling, advanced type combinations, and error conditions. Most tests verify that code compiles successfully but don't test:
- Runtime behavior (values, bounds, limits)
- Error handling and validation
- Edge cases and boundary conditions
- Complex type interactions

---

## Current Test Coverage

### 1. NumericTypeTests.cs (245 lines)

**What's tested:**
- Basic literal support: `f32`, `f64`, `fixed16`, `fixed32`, `u64`, `i64`
- Basic arithmetic: addition for all numeric types
- Multiplication for `u64`
- Type casting: `f32↔i32`, `i32→f32`

**What's NOT tested:**
- Overflow/underflow behavior
- Floating point precision loss
- Floating point edge cases (NaN, Inf, -0.0)
- Division operations
- Modulo operations
- Subtraction operations
- Negative number handling
- Type limits (MIN/MAX values)
- Fixed-point specific operations
- Fixed-point precision loss

### 2. TypeCastTests.cs (442 lines)

**What's tested:**
- 15+ signedness-preserving casts (i8→i16, i32→i64, etc.)
- 7+ narrowing casts (i64→i32, u16→u8, etc.)
- 5+ signed→unsigned casts
- 5+ unsigned→signed casts
- 4+ cross-size signed/unsigned casts
- 2 bool casts (bool↔i32)
- 1 chained cast test

**What's NOT tested:**
- **Behavior of narrowing casts**: What happens when casting 256 to u8? No validation tests
- **Behavior of sign-bit changes**: Casting -1i8 to u8 produces different bits - no verification
- **Casts involving floats**: Only 2 tests (f32→i32, i32→f32), no i64↔f32, f64→i8, etc.
- **Casts from/to fixed-point types**: Completely untested
- **Invalid cast errors**: Are there casts that should fail? No error tests
- **Loss of precision**: Casting large integers to floats
- **Chained narrowing**: e.g., i64→i16→i8
- **Casting with actual values**: All tests use literal values; semantic meaning not verified

### 3. PointerTests.cs (324 lines)

**What's tested:**
- Basic pointer types: `*i32`, `*u8`, `*struct`
- Null pointer comparison: `(u32)ptr == 0`
- Pointer parameters and return types
- Pointer-to-pointer: `**i32`
- Pointers in structs (linked list node)
- Pointer casts: `u32→*i32`, `*i32→u32`
- Pointers in enums
- Dereference read/write: `*ptr`, `*ptr = value`
- Pointer arithmetic (manual via u32 cast)
- Function pointers and function pointer calls
- Function pointers as parameters
- Pointers in loops
- Null pointer checks
- Pointer equality

**What's NOT tested:**
- **Null dereference**: No test for `*null_ptr` (should be caught or undefined)
- **Pointer overflow**: Arithmetic overflow when advancing pointers
- **Misaligned pointers**: Creating pointers to misaligned addresses
- **Double pointers aliasing**: `**p` points to location of `*p`; no aliasing tests
- **Casting between incompatible pointer types**: `*i32` cast to `*f32` then dereferenced
- **Pointer to array**: `*[10]i32` - array pointers not tested
- **Function pointer type mismatches**: Calling fn(i32,i32) as fn(i32)?
- **Dangling pointers**: Pointer outliving referenced value (though scope-based, worth testing)
- **Pointer to different sizes**: `*u8` vs `*u32` arithmetic differences not tested
- **Volatile vs non-volatile semantics**: Hardware register access patterns

### 4. ReferenceTests.cs (254 lines)

**What's tested:**
- Immutable references: `&x`
- Mutable references: `&mut x`
- Reference dereference (read): `*r`
- Reference dereference (write): `*r = value`
- References as parameters (immutable and mutable)
- References as return types
- References to structs
- References to arrays
- References in loops
- Multiple immutable references to same value
- Self-references in methods (`&self`, `&mut self`)
- Compound assignment through reference: `*r += 5`
- Reference chaining: `let r2 = r1` (copying reference)

**What's NOT tested:**
- **Lifetime validation**: References outliving their target (compile-time catch, but no test)
- **Mutable alias detection**: `let r1 = &mut x; let r2 = &mut x` (should fail)
- **Mutable+immutable conflict**: `let r1 = &x; let r2 = &mut x` (should fail)
- **Reference to temporary**: `let r = &(x + y)` (should fail)
- **Return dangling reference**: Function returning `&` to local (should fail)
- **Reference rebinding**: Treating different reference types as equivalent
- **Casting references**: `&i32` cast to `&u32`?
- **Reference to reference**: `&&x` or `&mut &x`
- **References in enums**: No test for `enum E { Ref(&i32) }`
- **References in generic types**: No test for `Option<&T>`

---

## Missing Edge Cases by Category

### A. Null/Void Handling

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| Null pointer dereference | Crash | HIGH |
| Null pointer comparison beyond u32 cast | Behavior varies | HIGH |
| Void returns (edge cases) | Codegen error | MEDIUM |
| Null function pointer call | Crash | HIGH |
| Pointer to void? | Type system | MEDIUM |

**Suggested tests:**
```novus
// Test 1: Null dereference (should compile but runtime trap)
fn dereference_null() -> i32 {
    let ptr: *i32 = 0 as *i32
    return *ptr  // Dereference null - undefined behavior
}

// Test 2: Null function pointer
fn call_null_fp() -> i32 {
    let fp: fn() -> i32 = 0 as fn() -> i32
    return fp()  // Call null function pointer
}

// Test 3: Safe null checks with different sizes
fn null_check_variants() -> bool {
    let ptr8: *u8 = 0 as *u8
    let ptr32: *i32 = 0 as *i32
    let ptr64: *u64 = 0 as *u64
    return (ptr8 as u32 == 0u32) && (ptr32 as u32 == 0u32) && (ptr64 as u32 == 0u32)
}
```

### B. Integer Overflow/Underflow

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| i8 addition: 127 + 1 | Wraps to -128 | HIGH |
| u8 subtraction: 0 - 1 | Wraps to 255 | HIGH |
| i32 multiplication: 2B * 2B | Overflows | HIGH |
| i64 multiplication: edge near i64::MAX | Overflows | HIGH |
| Negative number casting to unsigned | Sign bit becomes high bit | MEDIUM |
| Mixed width arithmetic: i32 + i64 | Type promotion rules | MEDIUM |

**Suggested tests:**
```novus
// Test 1: Signed overflow
fn signed_byte_overflow() -> i8 {
    let max: i8 = 127i8
    return max + 1i8  // Wraps to -128
}

// Test 2: Unsigned underflow
fn unsigned_byte_underflow() -> u8 {
    let zero: u8 = 0u8
    return zero - 1u8  // Wraps to 255
}

// Test 3: Signed multiplication overflow
fn signed_mult_overflow() -> i32 {
    let a: i32 = 100000
    let b: i32 = 100000
    return a * b  // Overflows i32
}

// Test 4: Mixed width arithmetic
fn mixed_width_add(a: i32, b: i64) -> i64 {
    return (a as i64) + b
}

// Test 5: Negative to unsigned cast semantics
fn neg_to_unsigned() -> u32 {
    let neg: i32 = -1
    return neg as u32  // Should be 0xFFFFFFFF
}
```

### C. Division and Modulo

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| Integer division by zero | ERROR caught at compile-time | TESTED |
| Floating division by zero | Inf or NaN | UNTESTED |
| Modulo by zero | ERROR caught at compile-time | UNTESTED (partially) |
| Modulo with negatives: -7 % 3 | Platform-dependent | UNTESTED |
| Division with truncation: -7 / 2 | Should truncate toward zero | UNTESTED |

**Suggested tests:**
```novus
// Test 1: Float division by zero (compile-time unknown)
fn float_div_zero() -> f32 {
    let x: f32 = 1.0f32
    let y: f32 = 0.0f32
    return x / y  // Should be Inf or NaN
}

// Test 2: Modulo with negatives
fn mod_negative() -> i32 {
    return (-7i32) % 3i32  // -1 or -1? (implementation dependent)
}

// Test 3: Division truncation
fn div_truncate() -> i32 {
    return (-7i32) / 2i32  // -3 or -4? (toward zero = -3)
}
```

### D. Type Limits (MIN/MAX Values)

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| i8::MIN (-128) operations | Boundary case | HIGH |
| i8::MAX (127) operations | Boundary case | HIGH |
| u8::MAX (255) operations | Boundary case | HIGH |
| i32::MIN/-MAX operations | 68k specific | HIGH |
| i64::MIN/-MAX operations | Large number handling | MEDIUM |
| f32 precision loss at boundaries | Float semantics | MEDIUM |
| fixed16/fixed32 overflow | Fixed-point semantics | MEDIUM |

**Suggested tests:**
```novus
// Test 1: All signed integer type boundaries
fn i8_boundaries() -> (i8, i8) {
    return (-128i8, 127i8)  // i8::MIN, i8::MAX
}

fn i16_boundaries() -> (i16, i16) {
    return (-32768i16, 32767i16)  // i16::MIN, i16::MAX
}

fn i32_boundaries() -> (i32, i32) {
    return (-2147483648i32, 2147483647i32)  // i32::MIN, i32::MAX
}

fn i64_boundaries() -> (i64, i64) {
    return (-9223372036854775808i64, 9223372036854775807i64)  // i64::MIN, i64::MAX
}

// Test 2: All unsigned integer type boundaries
fn u8_boundary() -> u8 {
    return 255u8  // u8::MAX
}

fn u16_boundary() -> u16 {
    return 65535u16  // u16::MAX
}

fn u32_boundary() -> u32 {
    return 4294967295u32  // u32::MAX
}

fn u64_boundary() -> u64 {
    return 18446744073709551615u64  // u64::MAX
}

// Test 3: Operations at boundaries
fn boundary_ops() -> bool {
    let max_i8 = 127i8
    let min_i8 = -128i8
    return (max_i8 + 1i8 == min_i8) && (min_i8 - 1i8 == max_i8)
}
```

### E. Floating-Point Edge Cases

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| NaN comparison: NaN == NaN | False (IEEE 754) | MEDIUM |
| Inf arithmetic: Inf + 1.0 | Still Inf | MEDIUM |
| -0.0 vs 0.0 | Different bits, same value | LOW |
| Float to int conversion of Inf | Undefined | MEDIUM |
| Float to int conversion of NaN | Undefined | MEDIUM |
| Precision loss: large float | Rounding errors | MEDIUM |
| Subnormal numbers | Implementation-dependent | LOW |

**Suggested tests:**
```novus
// Test 1: NaN semantics
fn nan_comparison() -> bool {
    let nan: f32 = 0.0f32 / 0.0f32
    return nan == nan  // Should be false (IEEE 754)
}

// Test 2: Infinity arithmetic
fn inf_arithmetic() -> f32 {
    let inf: f32 = 1.0f32 / 0.0f32
    return inf + 1.0f32  // Should still be Inf
}

// Test 3: Float to int with Inf/NaN
fn float_to_int_inf() -> i32 {
    let inf: f32 = 1.0f32 / 0.0f32
    return (i32)inf  // Undefined, likely INT_MAX or INT_MIN
}

// Test 4: Large float precision loss
fn float_precision() -> bool {
    let large: f32 = 16777216.0f32  // 2^24
    let large_plus_1: f32 = large + 1.0f32
    return large == large_plus_1  // May be true due to precision loss
}
```

### F. Invalid/Impossible Casts

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| Cast struct to i32 | Type error | UNTESTED |
| Cast function to i32 | Type error | UNTESTED |
| Cast array to scalar | Type error | UNTESTED |
| Cast reference to pointer | Implicit vs explicit? | UNTESTED |
| Cast reference to int | Safe or unsafe? | UNTESTED |

**Suggested tests:**
```novus
// Test 1: Invalid type casts (should be compile errors)
fn invalid_struct_cast() -> i32 {
    struct Point { x: i32, y: i32 }
    let p = Point { x: 1, y: 2 }
    return (i32)p  // ERROR: cannot cast struct to i32
}

// Test 2: Invalid function cast
fn invalid_fn_cast() -> i32 {
    fn foo() -> i32 { return 42 }
    return (i32)foo  // ERROR: cannot cast function to i32
}

// Test 3: Reference to pointer conversion
fn ref_to_ptr() -> *i32 {
    let x = 42
    let r: &i32 = &x
    return (r as *i32)  // Valid? Both are 32-bit addresses
}

// Test 4: Reference to integer conversion
fn ref_to_int() -> u32 {
    let x = 42
    let r: &i32 = &x
    return (r as u32)  // Valid? Reference is address
}
```

### G. Pointer Arithmetic Edge Cases

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| Pointer overflow: max_addr + 1 | Wraps on 68k | HIGH |
| Pointer to array bounds | Out of bounds access | HIGH |
| Pointer type size mismatch: `*u8` vs `*i32` | Arithmetic scaling | MEDIUM |
| Pointer subtraction: `ptr2 - ptr1` | Unsupported or pointer diff | MEDIUM |
| Null pointer arithmetic: `null + 4` | Undefined behavior | MEDIUM |

**Suggested tests:**
```novus
// Test 1: Pointer arithmetic with different type sizes
fn ptr_arithmetic_sizes() -> (u32, u32) {
    let base: u32 = 0x1000u32

    // Advance *u8 pointer by 4
    let ptr_u8: *u8 = base as *u8
    let addr_u8 = (ptr_u8 as u32) + 4u32  // +4 bytes

    // Advance *i32 pointer by 4 (should scale?)
    let ptr_i32: *i32 = base as *i32
    let addr_i32_scaled = (ptr_i32 as u32) + (4u32 * 4u32)  // +16 bytes? Or +4?

    return (addr_u8, addr_i32_scaled)
}

// Test 2: Pointer to array subscripting
fn ptr_to_array() -> i32 {
    let arr = {10, 20, 30, 40, 50}
    let ptr: *i32 = &arr[0] as *i32
    // Can we do pointer arithmetic safely?
    let next_addr = (ptr as u32) + 4u32
    let next_ptr: *i32 = next_addr as *i32
    return *next_ptr  // Should be 20
}

// Test 3: Pointer overflow wrapping
fn ptr_overflow_wrap() -> u32 {
    let ptr: *i32 = 0xFFFFFFFCu32 as *i32  // Near max
    let overflow_addr = (ptr as u32) + 4u32
    return overflow_addr  // Wraps to 0x00000000
}
```

### H. Reference Aliasing and Lifetime Issues

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| Mutable reference created during immutable borrow | Compile error | UNTESTED |
| Reference outliving referent | Compile error (scope-based) | UNTESTED |
| Multiple mutable references to same value | Compile error | UNTESTED |
| Mutable reference after immutable use | Compile error | UNTESTED |

**Suggested tests:**
```novus
// Test 1: Multiple mutable references (should fail)
fn multiple_mut_refs() {
    var x = 42
    let r1: &mut i32 = &mut x
    let r2: &mut i32 = &mut x  // ERROR: x already borrowed mutably
}

// Test 2: Mutable and immutable (should fail)
fn mix_mut_immut() {
    var x = 42
    let r1: &i32 = &x
    let r2: &mut i32 = &mut x  // ERROR: x already borrowed immutably
}

// Test 3: Reference to temporary (should fail)
fn ref_to_temp() -> &i32 {
    let temp = 42
    return &temp  // ERROR: reference to local variable outlives it
}

// Test 4: Mutable borrow after immutable use (should succeed - borrow ends)
fn borrow_reuse() -> i32 {
    var x = 42
    let r1: &i32 = &x
    let _val = *r1  // Use immutable reference
    // r1 borrow should end here
    let r2: &mut i32 = &mut x  // Should be OK now
    *r2 = 100
    return x
}
```

### I. Array/Slice Combinations with Pointers/References

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| Array of pointers: `[10]*i32` | Type system | UNTESTED |
| Pointer to array: `*[10]i32` | Type system | UNTESTED |
| Array of references: `[10]&i32` | Type system (problematic) | UNTESTED |
| Reference to array: `&[10]i32` | TESTED but limited | TESTED |
| Slice of pointer array | Bounds checking | UNTESTED |

**Suggested tests:**
```novus
// Test 1: Array of pointers
fn array_of_ptrs() -> i32 {
    let arr: [3]*i32 = {
        0x1000u32 as *i32,
        0x2000u32 as *i32,
        0x3000u32 as *i32
    }
    return (arr[0] as u32) == 0x1000u32 ? 1 : 0
}

// Test 2: Pointer to array
fn ptr_to_array_type() -> i32 {
    let arr = {10, 20, 30}
    let ptr: *[3]i32 = &arr as *[3]i32
    // How do we dereference and access elements?
    // (*ptr)[0] should be 10
    return 0
}

// Test 3: Array of mutable references (problematic)
fn array_of_mut_refs() {
    var a = 1
    var b = 2
    var c = 3
    let arr: [3]&mut i32 = {&mut a, &mut b, &mut c}
    *arr[0] = 10
    // Lifetime issue: arr holds references to local variables
}

// Test 4: Reference to array (existing test but expand scenarios)
fn ref_to_array_multidim() -> i32 {
    let arr: [[3]i32] = {{1,2,3}, {4,5,6}}
    let r: &[[3]i32] = &arr
    return 0
}
```

### J. Enum/Generic Type Combinations

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| Enum with pointer variant | Type system | TESTED |
| Enum with reference variant | Type system | UNTESTED |
| Option<*T> nesting | Pointer + generic | UNTESTED |
| Result<&T, E> lifetime issues | Reference + generic | UNTESTED |
| Generic struct with pointer field | Monomorphization | UNTESTED |

**Suggested tests:**
```novus
// Test 1: Enum with reference variant
enum RefValue {
    Ref(&i32),
    Int(i32)
}

fn enum_with_ref() -> i32 {
    let x = 42
    let rv = RefValue::Ref(&x)
    // Pattern match and extract reference
    match rv {
        RefValue::Ref(r) => return *r
        RefValue::Int(i) => return i
    }
}

// Test 2: Generic struct with pointer field
struct Buffer<T> {
    ptr: *T,
    len: u32
}

fn generic_with_ptr() -> i32 {
    let addr: u32 = 0x1000u32
    let buf: Buffer<i32> = Buffer {
        ptr: addr as *i32,
        len: 10u32
    }
    return 0
}

// Test 3: Nested generics with pointers
fn option_ptr_nesting() -> i32 {
    let ptr: *i32 = 0x1000u32 as *i32
    // Option<*i32> - how does None vs Some differ?
    return 0
}
```

### K. Sign Extension and Bit Width Edge Cases

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| Sign-extend i8 to i32 | Arithmetic correctness | UNTESTED |
| Zero-extend u8 to u32 | Unsigned semantics | UNTESTED |
| Casting preserves bit pattern vs. semantic value | Implementation | UNTESTED |
| Mixed-width comparison: i8 vs i32 | Type coercion | UNTESTED |

**Suggested tests:**
```novus
// Test 1: Sign extension semantics
fn sign_extend_test() -> i32 {
    let neg: i8 = -1i8  // 0xFF in bits
    let extended: i32 = (i32)neg  // Should be 0xFFFFFFFF (-1)
    return extended
}

// Test 2: Zero extension semantics
fn zero_extend_test() -> u32 {
    let val: u8 = 255u8  // 0xFF
    let extended: u32 = (u32)val  // Should be 0x000000FF (255)
    return extended
}

// Test 3: Bit pattern preservation
fn bit_pattern_preservation() -> u32 {
    let neg_i32: i32 = -1i32  // 0xFFFFFFFF
    let as_u32: u32 = (u32)neg_i32  // Should preserve bits = 0xFFFFFFFF
    return as_u32
}

// Test 4: Mixed width comparison
fn mixed_width_compare() -> bool {
    let a: i8 = -1i8
    let b: i32 = -1i32
    // Are these equal? Requires type coercion
    return ((i32)a) == b
}
```

### L. Fixed-Point Arithmetic

| Edge Case | Impact | Priority |
|-----------|--------|----------|
| fixed16 overflow | 8.8 format | UNTESTED |
| fixed32 overflow | 16.16 format | UNTESTED |
| fixed16 + fixed32 | Type mismatch | UNTESTED |
| Conversion: int to fixed | Scaling | UNTESTED |
| Conversion: fixed to int | Truncation | UNTESTED |
| Fixed-point precision | Rounding | UNTESTED |

**Suggested tests:**
```novus
// Test 1: Fixed-point type boundaries
fn fixed16_bounds() -> (fixed16, fixed16) {
    // fixed16 is 8.8 format: -128 to 127.99609375
    let min: fixed16 = -128.0fixed16
    let max: fixed16 = 127.99609375fixed16
    return (min, max)
}

fn fixed32_bounds() -> (fixed32, fixed32) {
    // fixed32 is 16.16 format: -32768 to 32767.9999...
    let min: fixed32 = -32768.0fixed32
    let max: fixed32 = 32767.9999fixed32
    return (min, max)
}

// Test 2: Fixed-point overflow
fn fixed16_overflow() -> fixed16 {
    let near_max: fixed16 = 127.0fixed16
    return near_max + 1.0fixed16  // Overflows in 8.8 format
}

// Test 3: Fixed-point precision
fn fixed_precision() -> bool {
    let a: fixed16 = 1.5fixed16
    let b: fixed16 = 1.5fixed16
    return a == b
}

// Test 4: Fixed to int conversion
fn fixed_to_int() -> i32 {
    let f: fixed16 = 42.75fixed16
    return (i32)f  // Truncates to 42
}

// Test 5: Int to fixed conversion
fn int_to_fixed() -> fixed16 {
    let i: i32 = 42
    return (fixed16)i  // Becomes 42.0 in fixed16
}
```

---

## Missing Test Files

### Suggested New Test Files

1. **EdgeCaseNumericTests.cs** - Integer/float edge cases
   - Overflow/underflow scenarios
   - Type boundary values
   - Signed/unsigned bit pattern conversions
   - Mixed-width operations

2. **AdvancedPointerTests.cs** - Complex pointer scenarios
   - Pointer-to-pointer aliasing
   - Pointer to array with bounds
   - Pointer arithmetic edge cases
   - Function pointer type mismatches

3. **AdvancedReferenceTests.cs** - Lifetime and aliasing
   - Multiple mutable borrows (should fail)
   - Reference to temporary (should fail)
   - Mixed borrow conflicts
   - Borrow reuse after scope end

4. **ComplexTypeCompositionTests.cs** - Combinations
   - Array of pointers
   - Pointer to array
   - Enum with references
   - Generic structs with pointers
   - References in generics

5. **FloatingPointEdgeCaseTests.cs** - IEEE 754 semantics
   - NaN, Inf, -0.0
   - Precision loss
   - Float-to-int conversion edge cases
   - Division by zero with floats

6. **FixedPointTests.cs** - Fixed-point arithmetic
   - Type boundaries
   - Overflow/underflow
   - Precision and rounding
   - Conversion to/from int and float

---

## Summary Table: Missing Coverage

| Category | Tested | Partial | Missing | Priority |
|----------|--------|---------|---------|----------|
| Basic Types | ✓ | | | HIGH |
| Numeric Casting | ✓ | Float/Fixed | 70% | MEDIUM |
| Pointer Basics | ✓ | Arithmetic | 60% | HIGH |
| Reference Basics | ✓ | Aliasing | 50% | HIGH |
| Overflow/Underflow | | ✓ | 90% | HIGH |
| Division/Modulo | Div-by-0 | | Float, Modulo | HIGH |
| Type Limits | | | 100% | MEDIUM |
| Float Semantics | | | NaN, Inf | MEDIUM |
| Pointer Arithmetic | ✓ | Edge cases | 70% | MEDIUM |
| Pointer-Array | | | 100% | MEDIUM |
| Reference Lifetime | | | 100% | HIGH |
| Fixed-Point | | | 100% | MEDIUM |
| Enum+Complex | ✓ | References | 60% | MEDIUM |
| Generic+Complex | | | 100% | LOW |

**Overall Coverage: ~35%** (Basic functionality works, edge cases missing)

---

## Implementation Priority

### Phase 1 (Critical Safety)
1. Integer overflow/underflow tests
2. Null pointer dereference behavior
3. Type limit boundary tests
4. Reference aliasing conflicts (compile-time validation)

### Phase 2 (Correctness)
5. Float edge cases (NaN, Inf)
6. Modulo/division remainder semantics
7. Pointer arithmetic edge cases
8. Fixed-point type boundaries

### Phase 3 (Completeness)
9. Complex type combinations (arrays of pointers, etc.)
10. Generic type with complex members
11. All type cast combinations
12. Edge case combinations

---

## Recommendations

1. **Add runtime value tests**: Current tests only verify compilation. Add tests that check actual computed values.

2. **Add error/diagnostic tests**: Test that invalid operations are caught with appropriate error messages.

3. **Add boundary value tests**: Test MIN/MAX for all integer types, edge values for floats/fixed-point.

4. **Clarify semantics**: Document behavior for:
   - Pointer arithmetic (scaled or byte-based?)
   - Overflow wrapping (defined or undefined?)
   - Float operations (IEEE 754 compliant?)
   - Fixed-point precision (rounding strategy?)

5. **Add compiler documentation**: Document which type operations are allowed/disallowed and why.

6. **Consider safety attributes**: Mark unsafe operations and verify they require `unsafe` blocks.
