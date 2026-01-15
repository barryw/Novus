---
title: Memory Safety
description: How Novus prevents memory bugs at compile time
---

Novus prevents common memory bugs at compile time. No garbage collector, no runtime overhead—just the compiler catching mistakes before they become Guru Meditations.

The philosophy is **power with guardrails**: safe by default, with explicit escape hatches when you need direct hardware control.

## Ownership

Every value in Novus has exactly one owner—the variable that holds it. When the owner goes out of scope, the value is automatically cleaned up:

```novus
fn example() {
    let screen = ScreenHandle::lores("Demo", 5)?
    // use screen...
}  // screen automatically closed here
```

No manual cleanup, no memory leaks. See the [Programmer's Guide](/guide) Chapter 5 for details on move semantics and the Drop trait.

## References vs Pointers

Novus has two ways to access data without taking ownership:

| | References (`&T`) | Raw Pointers (`*T`) |
|---|---|---|
| Null | Never null | Can be null |
| Lifetimes | Compiler-tracked | No tracking |
| Safety | Safe by default | Requires `unsafe` |
| Use for | Normal code | FFI, hardware |

```novus
let x: i32 = 42

// Reference - compiler ensures it stays valid
let r: &i32 = &x
let value = *r  // Safe

// Raw pointer - you manage validity
let p: *i32 = unsafe { (*i32)&x }
unsafe {
    let value = *p  // Your responsibility
}
```

**Guideline:** Use references by default. Use raw pointers only for FFI calls and direct hardware access.

## What the Compiler Catches

The compiler prevents these common bugs:

**Dangling references** - using a reference after its source is dropped:

```novus
fn bad() {
    let r: &i32
    {
        let x: i32 = 42
        r = &x
    }  // x dropped
    let y = *r  // ERROR: x does not live long enough
}
```

**Returning local references** - functions can't return references to their local variables:

```novus
fn bad() -> &i32 {
    let x: i32 = 42
    return &x  // ERROR: cannot return reference to local
}
```

**Accidental lifetime escape** - converting a reference to a raw pointer requires explicit `unsafe`:

```novus
let r: &i32 = &x
let p: *i32 = (*i32)r  // ERROR: requires unsafe block
```

## The Amiga Context

On modern systems, memory bugs often just crash one process. On the Amiga, there's no memory protection—a dangling pointer can corrupt any memory in the system, leading to a Guru Meditation or corrupted data.

Novus catches these bugs at compile time, before your code ever runs. You get the low-level control Amiga programming demands, with safety guarantees that prevent the crashes.

For the complete reference on ownership, borrowing, Drop, and RAII patterns, see Chapter 5 of the [Programmer's Guide](/guide).
