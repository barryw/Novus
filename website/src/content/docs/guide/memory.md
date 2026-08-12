---
title: Ownership and Memory Safety
description: The rules for ownership, consuming values, borrowing, views, and raw Amiga access in Novus
---

Novus makes resource ownership visible in function signatures and checks it at
compile time. There is no garbage collector and no hidden reference counting.
Files, windows, screens, memory, ports, and ordinary values all follow the same
small set of rules.

## The model in one table

| Signature | Meaning | Caller may use the value afterward? |
|---|---|---|
| `value: T` | Pass without transferring ownership; use for `Copy` data and small views | Yes |
| `consuming value: T` | Transfer ownership | No |
| `value: &T` | Shared, read-only borrow | Yes, but not mutably while borrowed |
| `value: &var T` | Exclusive, mutable borrow | Yes, after the borrow's scope ends |
| `value: *T` | Unchecked raw address | The compiler cannot protect the pointee |

The practical rule is simple: borrow by default, consume when ownership really
moves, and use raw pointers only at an FFI or hardware boundary.

## Owned values

Every non-`Copy` value has one owner. When that owner leaves scope, Novus runs
its `Drop` implementation automatically, including on early return and error
paths.

```novus
fn show_demo() -> Result<(), IntuitionError> {
    let screen = ScreenHandle::lores("Demo", 5)?
    // screen owns the Intuition screen
    return Result::Ok(())
} // ScreenHandle closes it here
```

Primitive numbers, booleans, and types implementing `Copy` are copied instead
of moved. The compiler permits `Copy` only for resource-free types whose fields
are also safe to copy; resource handles, mutable views, and owning containers
cannot opt out of move checking.

## Consuming transfers ownership

Use `consuming` only when the callee keeps, destroys, or returns ownership of a
value.

```novus
fn queue(consuming job: Job) {
    // this function now owns job
}

fn run() {
    let job = Job::new()
    queue(job)
    // job is moved; using it here is a compile error
}
```

The compiler invalidates the caller's value, clears the generated storage, and
makes the callee responsible for cleanup. A moved value cannot be dropped
twice. It is also an error to consume an owner while any view of it is live,
or to forward an owning non-consuming parameter into a consuming call.

Methods use the same spelling:

```novus
pub fn finish(consuming self) -> String
pub fn push(&var self, consuming value: T) -> Result<(), ExecError>
```

`finish` consumes the builder. `push` mutates the collection and transfers the
element into it.

## Shared borrows: `&T`

A shared borrow provides read-only access without transferring ownership.
Many shared borrows may exist at once.

```novus
fn area(rect: &Rectangle) -> i32 {
    return rect.width * rect.height
}

fn measure() -> i32 {
    let rect = Rectangle { width: 20, height: 10 }
    let first = &rect
    let second = &rect
    return area(first) + area(second)
}
```

While a shared borrow is live, the owner cannot be changed, moved, consumed, or
mutably borrowed. This prevents a pointer from silently becoming stale after a
container resize or resource close.

## Exclusive borrows: `&var T`

An exclusive borrow permits mutation. There may be exactly one exclusive
borrow, and no shared borrows, for the same owner.

```novus
fn move_right(point: &var Point) {
    point.x = point.x + 1
}

fn update() {
    var point = Point { x: 10, y: 20 }
    move_right(&var point)
}
```

State-changing methods always use `&var self`; read-only methods use `&self`.
Collection APIs therefore have predictable pairs:

```novus
pub fn get(&self, index: u32) -> Option<&T>
pub fn get_mut(&var self, index: u32) -> Option<&var T>
```

The owner must be declared with `var` before it can be mutably borrowed.

## Borrow scope

Borrows currently last to the end of their lexical block. Use a small block
when a view should end before the surrounding function does.

```novus
fn edit() -> Result<(), ExecError> {
    var values = Vec<i32>::new()
    values.push(10)?

    {
        let first = values.get(0)
        // read first here
    } // the shared borrow ends

    values.push(20)? // mutation is legal again
    return Result::Ok(())
}
```

This rule is intentionally visible and deterministic. Novus does not make the
lifetime depend on an optimizer deciding where the last use occurred.

## Views and iterators

`Str`, `Slice<T>`, `MutSlice<T>`, collection iterators, drawing contexts, and
guards are views. They do not own the underlying resource. Their fields use
checked references, so the compiler keeps their owner alive and prevents
incompatible mutation for as long as the view exists.

```novus
fn inspect(values: &Vec<i32>) {
    let slice = values.as_slice()
    let first = slice.get(0)
    // values cannot be moved or mutated while slice/first are live
}
```

This relationship is recursive. `Option<&T>`, `Result<Str, E>`, a struct
containing a reference, and even `Vec<Str>` remain tied to their source.

Use `Slice<T>` for a shared contiguous range and `MutSlice<T>` for an exclusive
mutable range. Do not represent a safe view with a `*T` field; that erases the
relationship the checker needs.

## Returning borrowed data

Novus normally infers the source of a returned view from `&self` or from the
single borrowed parameter.

```novus
fn first(values: Slice<i32>) -> Option<&i32> {
    return values.get(0)
}
```

If multiple inputs could supply the view, name the source with `@borrows`:

```novus
@borrows(right)
fn choose(left: Str, right: Str) -> Str {
    return right
}
```

For literals or permanent program storage, use `@borrows(static)`:

```novus
@borrows(static)
fn clear_sequence() -> Str {
    return "\x9b0m"
}
```

The compiler checks the function's return expressions against this declaration;
`@borrows` is not permission to lie about a lifetime.

## `Result` for operations that can fail

Allocation and AmigaOS operations can fail, so safe APIs return `Result<T, E>`
instead of a null pointer or half-initialized object.

```novus
fn open_demo() -> Result<(), IntuitionError> {
    let screen = ScreenHandle::lores("Demo", 5)?
    return Result::Ok(())
}
```

`?` returns the error while normal scope cleanup still runs. Use `Option<T>`
only when absence is expected and needs no error detail.

## Raw pointers and `unsafe`

Raw pointers are necessary for NDK calls, custom-chip registers, DMA, and
advanced data structures. They can be null, dangling, misaligned, or aliased;
the compiler cannot prove otherwise.

```novus
unsafe {
    let custom = (*u16)$DFF000
    let dmacon = custom[75]
}
```

Keep unsafe regions small. Convert to a checked owner or view immediately, then
return to safe code. A public function that asserts a raw pointer's validity
must be marked `@unsafe`, which forces callers to acknowledge the boundary.

The standard library uses consistent raw names:

| API | Contract |
|---|---|
| `handle()` / `as_ptr()` | Borrow a raw handle; ownership stays put |
| `borrow_raw(...)` | Build a non-owning unsafe view |
| `from_raw(...)` | Adopt ownership of a raw resource |
| `into_raw(consuming self)` | Transfer ownership out and disable automatic cleanup |

After `into_raw`, the caller must eventually pass the resource to another
owner or release it manually. Calling `from_raw` twice for one resource creates
two owners and is an unsafe programming error.

## AmigaOS and hardware-retained pointers

Some APIs keep a pointer after the call returns: message ports, asynchronous
I/O, audio samples, sprites, Copper lists, and Blitter/DMA operations. A normal
lexical borrow is not enough unless a safe wrapper owns the backing storage for
the entire operation.

Prefer a scoped library handle such as a request, guard, player, or channel.
It should own every retained buffer, wait or abort before dropping it, and
expose only checked methods. If no wrapper exists, raw pointers and `unsafe`
are required, and the programmer must keep the memory alive until the OS or
hardware is provably finished.

## Reading an API signature

You should be able to understand resource behavior without reading its body:

```novus
fn draw(context: &DrawContext)                 // reads a live context
fn configure(context: &var DrawContext)        // mutates it exclusively
fn install(consuming menu: MenuStrip)           // takes ownership
fn rastport(&self) -> &RastPort                 // owner-tied view
fn open(...) -> Result<WindowHandle, Error>     // new owned resource or error
fn handle(&self) -> *Window                     // raw borrowed escape hatch
fn into_raw(consuming self) -> *Window          // ownership leaves Novus
```

If a library signature does not make ownership this clear, treat that as an API
bug rather than tribal knowledge developers are expected to memorize.

## Library-author rules

1. Return an owning handle for acquired resources and implement `Drop` with
   `fn drop(&var self)`.
2. Use `&self` for observation, `&var self` for mutation, and `consuming self`
   for transformations or ownership transfer.
3. Mark stored parameters `consuming`; borrow parameters used only during the
   call.
4. Return `Result` for failure and leave no partially owned resource on an
   error path.
5. Represent non-owning views with references, never hidden raw pointer fields.
6. Keep raw constructors `@unsafe` and follow the raw naming table above.
7. For OS/hardware-retained pointers, make the safe wrapper own the backing
   memory and finish or cancel the operation before `Drop` releases it.

## What this prevents

Safe Novus code prevents use-after-move, double cleanup, returning references
to locals, mutation while borrowed, competing mutable aliases, and unchecked
failure represented as a null owner. Those protections remove several major
sources of Guru Meditations.

`unsafe`, incorrect FFI declarations, out-of-spec hardware access, stack
overflow, and AmigaOS bugs can still crash the machine. The goal is that normal
application code never needs those powers; when it does, the dangerous region
is explicit, small, and reviewable.
