# Novus naming conventions

Novus uses names to distinguish language concepts while namespaces distinguish abstraction layers.

## Identifiers

- `SCREAMING_SNAKE_CASE` for constants: `MEMF_FAST`, `MAX_DEVICES`
- `PascalCase` for structs, enums, traits, and aliases: `BlockDevice`, `StorageError`
- `snake_case` for functions, methods, variables, parameters, fields, and modules: `read_blocks`, `block_bytes`
- Raw Amiga functions keep their NDK spelling: `OpenDevice`, `DoIO`, `FindTask`

## Amiga API layers

The namespace—not capitalization—is the authoritative safety and abstraction signal:

```novus
from amiga::storage import BlockDevice          // application intent
from amiga::sys::device import DeviceRequest    // safe NDK model
from amiga::raw::exec import OpenDevice          // native NDK call
```

Portable facilities remain under `std`:

```novus
from std::collections import ArrayVec
from std::memory import Buffer, Slice
from std::string import FixedString, Str
```

## Ownership and interop

Owning wrappers use one consistent vocabulary where the operation exists:

- `system()` borrows the next safe layer down
- `as_raw()` borrows the native handle
- `into_raw()` consumes the owner and transfers native ownership
- `from_raw()` adopts explicitly owned native state
- `into_system()` and `from_system()` transfer between application and systems owners

Do not add transfer methods merely for symmetry. Borrowed views should name their owner in the type system instead of using `Raw`, `Safe`, or `Managed` prefixes.

## Examples

```novus
from amiga::storage import BlockDevice

let disk = BlockDevice::open("scsi.device", 0)?
let request = disk.system()
custom_command(request)?
disk.read_blocks(0, &var block)?
```

```novus
from amiga::raw::exec import AllocMem, FreeMem

unsafe {
    let memory = AllocMem(size, flags)
    FreeMem(memory, size)
}
```

The golden rule is simple: use ordinary Novus names in safe code, preserve NDK names at the raw boundary, and choose the namespace that matches the developer's intent.
