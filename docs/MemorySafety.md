# Memory safety in Novus

Novus makes the safe path the ordinary path. Owning values are move-only, borrowed views cannot outlive their owners, `Drop` performs cleanup, and fallible allocation returns `Result`.

## Application memory

Use the portable buffer and slice types when the Amiga memory class does not matter:

```novus
from std::core import Option, Result
from std::memory import Buffer, MemoryError, MutSlice

fn fill(size: u32) -> Result<Buffer, MemoryError> {
    let var buffer = Buffer::new(size)?
    let bytes: MutSlice<u8> = buffer.as_mut_bytes()
    match bytes.get_mut(0) {
        Option::Some(first) => { *first = 42 },
        Option::None => {},
    }
    return Result::Ok(buffer)
}
```

`Buffer` remembers its allocation size and frees itself. `Slice<T>` and `MutSlice<T>` carry a length and borrow their owner, so normal indexing is checked without exposing a raw pointer.

## Amiga memory classes

Use the systems layer when hardware or an NDK API requires chip, fast, or explicitly flagged memory:

```novus
from amiga::sys::exec::memory::allocation import MemHandle

let chip = MemHandle::new_chip(320 * 256)?
let raw = chip.ptr() // borrowed escape hatch for the NDK call
```

`MemHandle` owns the Exec allocation and calls `FreeVec` from `Drop`. Keep the owner alive while a borrowed pointer is in use.

## Raw NDK allocation

Only Tier-3 code should normally manage an allocation manually:

```novus
from amiga::raw::consts import MEMF_PUBLIC
from amiga::raw::exec import AllocMem, FreeMem

unsafe {
    let memory = AllocMem(1024, MEMF_PUBLIC)
    if memory != null {
        // The same pointer and size must be supplied exactly once.
        FreeMem(memory, 1024)
    }
}
```

Raw allocation is intentionally `unsafe`: the compiler cannot infer the allocation size, ownership, or whether Amiga hardware retains the pointer.

## Ownership rules

- Returning or passing an owning value with `consuming` transfers cleanup responsibility.
- `&T` and `&var T` borrow without transferring ownership.
- `as_raw()` borrows a native handle.
- `into_raw()` consumes and disarms the owner.
- `from_raw()` adopts one explicitly owned native resource.
- Prefer `Result` and `?` so cleanup still runs on every error path.
- Prefer slices over pointer/count pairs outside raw bindings.

Use `unsafe` only at the smallest NDK boundary. Application code should normally need neither `AllocMem` nor `FreeMem`.
