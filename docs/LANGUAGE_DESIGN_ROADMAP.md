# Novus Language Design Roadmap

## Purpose

Novus is a systems language for 68k Amiga development. Its language design should make safe code concise and readable while preserving predictable lowering to efficient 68020+ machine code.

The guiding principle is:

> Make high-level source easy to read while keeping the generated work obvious to a systems programmer.

HDPart is the primary real-world acceptance corpus for this work. Language changes should remove recurring ceremony in HDPart without introducing hidden allocation, hidden dynamic dispatch, exceptions, garbage collection, or runtime machinery that is difficult to predict.

## Design goals

1. Safe by default.
2. Zero-cost or near-zero-cost abstractions.
3. Predictable 68k lowering.
4. Minimal syntax ceremony.
5. Strong static typing without forcing redundant type annotations.
6. Efficient fixed-capacity and stack-oriented programming.
7. Raw control remains available when needed.
8. New syntax must solve a general language problem, not one Amiga API problem.

## Non-goals

Novus should not become Rust with different punctuation, nor a managed application language.

Avoid making these default language directions:

- garbage collection
- exceptions
- reflection
- hidden heap allocation
- pervasive dynamic dispatch
- LINQ-style iterator pipelines
- async/await unless a compelling Amiga-native model emerges
- application-specific DSLs
- macro systems introduced only to work around missing core language features

---

# Priority 0: immediate ergonomics

## 1. Contextual integer literals

### Problem

Current Novus code is saturated with redundant suffixes:

```novus
for index in 0u32..summed_longs {
    sum = sum + reader.read_be_u32(index * 4u32)?
}

return false unless high_reserved_block < $FFFFFFFFu32 / BLOCK_BYTES
```

The compiler usually already knows the required type.

### Target

```novus
for index in 0..summed_longs {
    sum += reader.read_be_u32(index * 4)?
}

return false unless high_reserved_block < $FFFFFFFF / BLOCK_BYTES
```

### Rules

- Unsuffixed integer literals begin as an abstract integer value.
- Assignment, parameter, return, comparison, arithmetic, range, array-element, and pattern context may determine the concrete type.
- Compilation fails when the value does not fit the inferred type.
- Explicit suffixes remain available when a programmer wants to force a type.
- Ambiguous expressions must produce a useful diagnostic rather than selecting a surprising type.

### Code generation

No runtime cost. Literal typing is compile-time only.

---

## 2. Compound assignment

Add:

```text
+= -= *= /= %= &= |= ^= <<= >>=
```

Target:

```novus
sum += value
current += 1 + segment_count
flags |= FLAG_DIRTY
```

These should lower exactly as their expanded assignment forms.

---

## 3. First-class safe indexing and slicing

### Target syntax

```novus
let value = slice[index]
slice[index] = value

let middle = slice[start..end]
let tail = slice[start..]
let head = slice[..end]
```

Indexing and slicing are bounds-checked by default.

### Optimization requirement

The compiler must eliminate dominated and provably redundant bounds checks.

Example:

```novus
for index in 0..buffer.len {
    consume(buffer[index])
}
```

The generated loop should not perform a bounds check on every iteration when the compiler can prove the range is valid.

### Explicit unchecked access

Unchecked access should require an explicit unsafe form. Exact syntax may be chosen during implementation, but it must be visibly unsafe.

Example candidate:

```novus
unsafe {
    let value = slice[index]
}
```

Do not introduce a second ordinary-looking indexing operator whose safety is unclear.

---

## 4. Contextual Option binding

### Problem

Current code repeatedly exposes enum structure when the programmer only wants to unwrap or take an alternate control-flow path:

```novus
let Option::Some(startup) = entry.startup() else { continue }
let Option::Some(driver) = startup.device_name() else { continue }
```

### Target

```novus
let startup = entry.startup() else continue
let driver = startup.device_name() else continue
```

### Semantics

When the right-hand side has type `Option<T>` and the left-hand side expects `T`:

- `Some(value)` binds `value`.
- `None` executes the `else` branch.
- The `else` branch must diverge from the binding site (`continue`, `break`, `return`, etc.).

Pattern binding remains available when the enum shape itself matters.

### Result handling

Do not automatically generalize this rule to `Result<T, E>` until error propagation semantics are explicitly designed. `?` already expresses normal Result propagation well.

---

## 5. Uniform iteration

### Goal

Normal code should almost never need to write an explicit `.next()` loop.

Current pattern:

```novus
var entries = devices.iter()
forever {
    let Option::Some(entry) = entries.next() else { break }
    ...
}
```

Target:

```novus
for entry in devices {
    ...
}
```

and, when an iterator object is explicitly constructed:

```novus
for entry in devices.iter() {
    ...
}
```

### Requirement

This must lower to direct iterator initialization plus `next`/done branching. No allocation and no mandatory virtual dispatch.

---

# Priority 1: common systems-programming improvements

## 6. Native index types

Introduce or standardize:

```novus
usize
isize
```

For 68k targets:

```text
usize = u32
isize = i32
```

Collection lengths and indices should use the native index type consistently unless an external ABI requires another type.

This should eliminate conversions such as:

```novus
types[index as u32]
```

inside ordinary collection code.

---

## 7. `enumerate`

Support straightforward index + value iteration:

```novus
for (index, item) in items.enumerate() {
    ...
}
```

This must remain a static, allocation-free iterator transformation.

---

## 8. Byte literals and byte strings

Systems code frequently works with signatures and binary formats.

Support:

```novus
b'P'
b"PFS"
```

A byte character literal is `u8`.

A byte string literal is a compile-time fixed byte sequence.

Example:

```novus
match bytes[0..3] {
    b"PFS" => pfs++
    b"PDS" => pds++
    b"SFS" => sfs++
    _ => {}
}
```

No heap allocation is permitted.

---

## 9. FourCC literals

Binary Amiga formats contain many 32-bit ASCII identifiers. Hex constants are unreadable:

```novus
$5244534B
$50415254
$46534844
```

Add a compile-time FourCC literal form. Recommended syntax:

```novus
fourcc"RDSK"
fourcc"PART"
fourcc"FSHD"
```

The literal must have explicit, documented byte-order semantics and lower to a constant `u32`.

Example:

```novus
const ID_RDSK = fourcc"RDSK"
```

---

## 10. Fixed-capacity formatting support

This may be primarily a standard-library feature, but the language must support it efficiently enough that formatting does not require heap strings, varargs, reflection, or dynamic dispatch.

Target source should be able to look approximately like:

```novus
let line = FixedString<80>::format(
    "unit {}  {} MB",
    disk.unit,
    disk.total_blocks / 2048,
)?
```

A future interpolation syntax may be considered only if it remains compile-time analyzable and stack/fixed-buffer friendly.

The initial implementation should prefer a library facility over new syntax.

---

## 11. Result error remapping without closures

HDPart often needs to intentionally collapse one error domain into another.

Instead of verbose matches, provide a zero-cost library operation such as:

```novus
operation().or_error(FormatError::FormatFailed)?
```

This should be preferred over adding general closure machinery solely for `map_err` ergonomics.

---

# Priority 2: structural improvements

## 12. Slice equality and bulk copy

Portable stdlib operations should make hand-written byte loops unnecessary:

```novus
if left == right { ... }

target.copy_from(source)?
```

For byte slices, the compiler/runtime may lower these to optimized longword loops after checking sizes once.

Safety checks should occur at the operation boundary, not for every copied byte.

---

## 13. Uniform `fill`

Arrays and mutable slices should expose a consistent operation:

```novus
buffer.fill(0)
```

The implementation should be optimized for primitive element types.

---

## 14. Property-style read-only accessors

Consider allowing trivial getter methods to be exposed as read-only properties:

Current:

```novus
geometry.block_bytes()
partition.low_cylinder()
```

Possible target:

```novus
geometry.block_bytes
partition.low_cylinder
```

This is lower priority than literal inference, indexing, Option binding, and iterator cleanup. Do not introduce property syntax until the rules for fields, methods, traits, mutability, and ABI behavior are precise.

---

## 15. Derived value traits

Simple value types should be able to derive common traits such as equality where semantics are structural.

Exact attribute syntax can follow the existing Novus attribute system.

Do not derive equality automatically for types whose equality is domain-specific.

---

## 16. Enum representation and discriminant ergonomics

Support strongly typed ABI-transparent IDs:

```novus
enum GadgetId: u16 {
    Device = 1
    Partitions
    Name
    Size
    Save
}
```

Requirements:

- explicit underlying representation
- auto-incrementing discriminants
- zero-cost conversion at compatible ABI boundaries when explicitly permitted
- no implicit conversion from arbitrary integers into the enum

Use this for gadget IDs, menu IDs, command IDs, and similar domains instead of unrelated integer constants.

---

# Test ergonomics

Tests are part of the language usability story. The test stdlib should provide:

```novus
expect_ok(value)
expect_err(value)
expect_some(value)
expect_none(value)
expect_eq(left, right)
expect_ne(left, right)
```

Test failures should report useful values and source locations when practical.

A test should not need to turn a `Result` into a giant hand-written `match` just to return `false`.

---

# Compiler requirements exposed by HDPart

Language ergonomics are not enough if safe abstractions expand into poor code.

HDPart currently demonstrates that checked byte access can expand excessively through Result handling, slicing, and repeated per-byte checks. The correct fix is compiler optimization, not rewriting application code as unsafe.

Required compiler work includes:

1. Correct general IR inlining.
2. Correct optimization of enum/Result temporaries.
3. Dominated bounds-check elimination.
4. Range analysis for loops and slices.
5. Inlining of small stdlib abstractions.
6. Bulk-operation recognition for copy/fill/equality where useful.
7. Dead-code elimination across unused high-level wrappers.

Safety should be cheap because the compiler understands it, not because the programmer disables it.

---

# Acceptance criteria

Use HDPart as a continuing benchmark.

For each language feature:

1. Refactor representative HDPart code to use the feature.
2. Measure source-code reduction and readability.
3. Confirm no new heap allocation.
4. Confirm no new unsafe requirement.
5. Compare generated code size.
6. Compare generated 68k assembly for hot paths.
7. Confirm bounds checks and Result machinery disappear where statically redundant.
8. Keep or improve test coverage.

A feature is successful when the source becomes easier to understand without making runtime behavior harder to predict.

---

# Recommended implementation order

1. Contextual integer literals.
2. Compound assignment.
3. Safe indexing and slicing syntax.
4. Bounds-check elimination and range analysis.
5. Contextual Option binding.
6. Uniform `for` iteration.
7. `usize` / `isize` cleanup for collections.
8. `enumerate`.
9. Byte literals and byte strings.
10. FourCC literals.
11. Fixed-capacity formatting support.
12. Slice copy/equality/fill optimization.
13. Test assertion helpers.
14. Error remapping helpers.
15. Property-style accessors, only after the higher-value work is complete.

## Principle for future proposals

Before adding a language feature, ask:

> Can this be expressed cleanly as a library feature with the same safety and generated code?

If yes, prefer the library.

If no, and the problem is general rather than Amiga-specific, consider language syntax.

The ideal Novus statement should let an experienced 68k developer form a reasonable mental model of the instructions and calls it will produce.
