# Memory Safety Status

Updated: 2026-08-11

Novus enforces moves and lexical borrowing at compile time. The standard
library follows the same ownership vocabulary as the language.

## Current guarantees

- `consuming` is required when a call takes ownership. The caller is marked
  moved, the generated code clears the source, and the callee cleans up its
  owned parameter on every exit path.
- Assignments, returns, branches, matches, loops, shadowed locals, and partial
  field moves participate in move tracking.
- `&T` is a shared borrow and `&var T` is an exclusive mutable borrow. Novus
  permits many shared borrows or one mutable borrow, never both (`E0499` and
  `E0502`).
- Borrow lifetimes end with their lexical scope. References cannot outlive a
  local source.
- Structs, enums, tuples, arrays, and instantiated generic containers that
  contain references are owner-tied views. Returning `Option<&T>`,
  `Vec<Str>`, `Slice<T>`, iterators, guards, or another aggregate containing
  references keeps the source borrowed.
- Return lifetime elision uses `&self` or one unambiguous borrowed input.
  `@borrows(input)` selects among multiple inputs and `@borrows(static)` marks
  views backed by permanent program data. Function bodies are checked against
  that declared source.
- Moving or consuming an owner while one of its views is live is rejected.
- Mutation through an owned value requires `var`; mutation through a borrow
  requires `&var`.
- Non-consuming parameters cannot forward owned data into a consuming call.
  `Copy` implementations are rejected for resource owners, non-Copy fields,
  and mutable references.
- `Drop` receives `&var self`. Its custom cleanup runs before owned fields are
  dropped automatically in reverse declaration order. Moved fields are
  disarmed before cleanup, so ownership is released exactly once.

## Standard-library contract

```novus
pub fn get(&self, index: u32) -> Option<&T>
pub fn get_mut(&var self, index: u32) -> Option<&var T>
pub fn push(&var self, consuming value: T) -> Result<(), ExecError>
pub fn into_raw(consuming self) -> *Resource
```

Collections, slices, iterators, semaphore/data guards, channels, boxes,
futures, and owning Amiga handles use these forms consistently. `system()`
borrows the next safe layer and `as_raw()`/`as_ptr()` are explicit native
escape hatches; validating `from_raw()` adopts ownership and `into_raw()`
transfers ownership after disarming automatic cleanup.

`Str`, `Slice`, `DrawContext`, `AreaContext`, Workbench arguments, and nested
generic views store checked references rather than erasing their owners into
raw pointer fields. Constructors that assert a raw span or pointer lifetime are
marked `@unsafe`; calling them requires an `unsafe` block.

## Remaining limitations

- Raw pointers intentionally bypass lifetime and alias checking. They remain
  necessary for FFI, intrusive collections, allocators, and advanced code.
- `@borrows(raw_pointer)` establishes a lexical relationship but cannot prove
  the pointee is valid; it is therefore permitted only on `@unsafe` functions.
- AmigaOS and hardware can retain pointers after a function returns. Those
  asynchronous contracts still require a scoped owning handle or an explicit
  raw/unsafe boundary; ordinary lexical borrowing cannot model hardware DMA by
  itself.
- Borrows currently end at lexical block boundaries rather than at their last
  use. A smaller nested block can release one earlier.
- `unsafe` permits explicitly marked operations but does not disable move,
  lifetime, alias, or lexical mutability checks.

## Verification

The compiler tests cover consuming calls and cleanup, assignment/return/field
moves, lexical scopes, shared-versus-exclusive conflicts, aggregate views, and
C code generation. Tests also cover explicit/static return sources, unsafe raw
view construction, mutation while borrowed, and generic containers of views.
A separate stdlib contract suite checks foundational
collection access, consuming insertion, owner-tied iterators/guards, mutable
`Drop`, raw ownership transfer conventions, and moves while borrowed. The
language-server sweep is clean for all 195 standard-library files.
