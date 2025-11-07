# Memory Safety Roadmap for Novus

## The Problem

The f-string implementation exposed a critical gap in Novus's safety guarantees: **move semantics are not enforced by the compiler**. This led to:

1. A method `finish(self)` consuming/moving `self` by value
2. The caller still having access to the moved-from value
3. Silent memory corruption (double-free) only caught at runtime on hardware
4. The kind of subtle bug that should be **impossible in a modern systems language**

## Core Issue: No Borrow Checker

Novus currently has **no compile-time enforcement** of:
- Move semantics (use-after-move)
- Borrow rules (aliasing vs mutation)
- Lifetime tracking (dangling references)
- Resource ownership

**Result**: Writing Novus code is currently more dangerous than C because the language *looks* safe but has hidden landmines.

## What Modern Systems Languages Do Right

### Rust's Approach (The Gold Standard)
```rust
let formatter = Formatter::new();
formatter.write_str("hello");
let s = formatter.finish();  // Consumes formatter
// formatter.write_str("x");  // ❌ COMPILE ERROR: use of moved value
```

**Key insight**: The compiler **prevents** use-after-move at compile time.

### Zig's Approach (Explicit is Better)
```zig
var formatter = Formatter.init();
formatter.writeStr("hello");
const s = formatter.finish();  // Consumes formatter
// No borrow checker, but conventions + tooling make ownership clear
```

### Swift's Approach (Consuming/Borrowing)
```swift
consuming func finish(self) -> String {
    return self.buffer  // Compiler enforces: self is consumed
}
```

## Novus Safety Levels: A Pragmatic Approach

We need a **multi-tier safety system** that respects the Amiga's constraints while preventing footguns:

### Level 1: Immediate (Before 1.0)
**Goal**: Make move semantics visible and catchable

#### 1.1 Explicit Move Syntax
```novus
let formatter = Formatter::new()
formatter.write_str("hello")
let s = move formatter.finish()  // 'move' keyword required

// Attempt to use moved value
formatter.write_str("x")  // ❌ COMPILE ERROR: use of moved value 'formatter'
```

**Implementation**:
- Track moved variables in semantic analyzer
- Error on any use after move
- Require explicit `move` keyword for consuming calls

#### 1.2 Ownership Attributes
```novus
pub struct Formatter {
    buffer: String  // Implicitly owns heap data
}

impl Formatter {
    // Explicitly consumes self
    pub fn finish(consuming self) -> String {
        self.buffer
    }

    // Borrows self immutably
    pub fn as_str(&self) -> Str {
        self.buffer.as_str()
    }

    // Borrows self mutably
    pub fn write_str(&mut self, s: Str) -> bool {
        self.buffer.push_str(s)
    }
}
```

**Implementation**:
- Add `consuming` keyword for methods that take ownership
- Enforce at call sites that value is moved
- Track moved state through control flow

#### 1.3 Use-After-Move Detection
```novus
let s = String::new()
let s2 = move s  // s is now moved

if condition {
    print(s)  // ❌ ERROR: use of moved value 's'
}
```

**Implementation**:
- Semantic analyzer tracks "moved" state per variable
- Flow-sensitive analysis (like Rust's)
- Error on any path that uses moved variable

### Level 2: Enhanced Safety (v1.1)

#### 2.1 Lifetime Annotations (Subset of Rust)
```novus
// Return a reference that lives as long as the input
pub fn first_word<'a>(s: &'a Str) -> &'a Str {
    // ... implementation
}
```

**Use cases**:
- Slices into strings/arrays
- References into structs
- Iterator lifetimes

**Non-goals**:
- Full Rust complexity (no HKT, no HRTB)
- Just enough to catch dangling references

#### 2.2 Affine Types (Linear-ish)
```novus
// Must be consumed exactly once
pub struct [[must_use]] FileHandle {
    fd: i32
}

impl FileHandle {
    pub fn close(consuming self) {
        // ...
    }
}

fn test() {
    let f = open_file("test.txt")
    // Forgot to call f.close()
}  // ❌ WARNING: FileHandle must be consumed (call .close())
```

#### 2.3 Ownership Visualization
```bash
$ novusc check --explain-moves main.novus

main.novus:15:5
  |
15|     formatter.write_str("x")
  |     ^^^^^^^^^ value used here after move
  |
note: value moved here
  |
12|     let s = formatter.finish()
  |             ^^^^^^^^^ moved due to consuming method call
  |
help: if you want to use formatter after finish(), clone it first
  |
12|     let s = formatter.clone().finish()
  |             ++++++++
```

### Level 3: Advanced Safety (v2.0+)

#### 3.1 Full Borrow Checker (Rust-style)
```novus
// Multiple immutable borrows OK
let s = String::new()
let r1 = &s
let r2 = &s  // OK

// Mutable + immutable = ERROR
let mut s = String::new()
let r1 = &s
let r2 = &mut s  // ❌ ERROR: cannot borrow as mutable while immutably borrowed
```

#### 3.2 Async Ownership
```novus
async fn process(data: String) -> Result<(), Error> {
    // Ownership transferred across await points
    let result = await fetch_data().await
    await send_data(move data).await  // OK: ownership moved into async call
}
```

#### 3.3 Safe Concurrency (When we add multitasking)
```novus
// Send + Sync traits (like Rust)
pub trait Send {}  // Can be sent to another task
pub trait Sync {}  // Can be shared between tasks
```

## Implementation Plan

### Phase 1: Foundation (Current Sprint)
- [x] Fix C codegen move semantics (null out source pointers)
- [ ] Add `consuming` keyword to grammar
- [ ] Semantic analysis: track moved variables
- [ ] Error on use-after-move

### Phase 2: Ownership Annotations (Next Sprint)
- [ ] Implement `&self`, `&mut self`, `consuming self` in methods
- [ ] Require explicit annotations on all methods
- [ ] Flow-sensitive move tracking
- [ ] Add `move` keyword for explicit moves

### Phase 3: Borrowing Rules (v1.1)
- [ ] Track borrowed references
- [ ] Enforce exclusivity (mut XOR shared)
- [ ] Lifetime inference (simple cases)
- [ ] Explicit lifetime annotations for complex cases

### Phase 4: Advanced Features (v2.0)
- [ ] Full borrow checker
- [ ] Affine types (`must_use` enforcement)
- [ ] Ownership visualizations in compiler errors
- [ ] Integration with IDE (show moved/borrowed state)

## Design Principles

1. **Explicit over implicit**: Ownership transfer should be visible in code (`move`, `consuming`)
2. **Progressive disclosure**: Start simple (moves), add complexity as needed (lifetimes)
3. **Escape hatches**: `unsafe` blocks for when you know better than the compiler
4. **Great error messages**: Rust-quality diagnostics with suggestions
5. **Zero runtime cost**: All checking at compile time
6. **Amiga-first**: Don't add features that hurt 68k performance

## Comparison with Other Languages

| Feature | C | C++ | Rust | Zig | Swift | **Novus (Proposed)** |
|---------|---|-----|------|-----|-------|---------------------|
| Move semantics | ❌ | ⚠️ Manual | ✅ Auto | ⚠️ Convention | ✅ Auto | ✅ Explicit |
| Borrow checking | ❌ | ❌ | ✅ Full | ❌ | ⚠️ ARC | ✅ Compile-time |
| Use-after-move detection | ❌ | ⚠️ Runtime | ✅ Compile | ❌ | ✅ Compile | ✅ Compile |
| Lifetime tracking | ❌ | ❌ | ✅ Full | ❌ | ⚠️ ARC | ✅ Subset |
| Overhead | 0 | 0 | 0 | 0 | Runtime | **0** |
| Learning curve | Easy | Hard | Very Hard | Medium | Easy | **Medium** |

## Why This Matters for Amiga Development

1. **No debugger luxury**: Amiga debugging is painful. Catching bugs at compile time is essential.
2. **Limited memory**: Memory leaks/corruption are fatal on 512KB-2MB systems.
3. **No OS protection**: One bad pointer crashes the whole system.
4. **Long iteration cycles**: Compile → copy to floppy → reboot → test is slow.

**Conclusion**: Amiga development *needs* strong compile-time safety even more than modern platforms.

## References

- [Rust Ownership System](https://doc.rust-lang.org/book/ch04-00-understanding-ownership.html)
- [Swift Ownership Manifesto](https://github.com/apple/swift/blob/main/docs/OwnershipManifesto.md)
- [Zig Memory Management](https://ziglang.org/documentation/master/#Memory)
- [Vale Region Borrow Checking](https://vale.dev/guide/regions)

## Next Steps

1. Implement `consuming` keyword and use-after-move detection (2-3 days)
2. Add extensive tests for move semantics
3. Update stdlib to use `consuming self` where appropriate
4. Document ownership rules in language guide
5. Add compiler flag for gradual opt-in: `--strict-ownership`

---

**TL;DR**: Novus needs Rust-style ownership tracking at compile time. The current "looks safe, actually footgun" situation is unacceptable for a language claiming to be better than C.
