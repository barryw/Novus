# Novus Amiga Library Design

> **STATUS: NOT COMPLETE**
>
> The namespace migration to `amiga::*`, `amiga::sys::*`, and `amiga::raw::*` is only the first half of this redesign. The library work is **not complete** until the abstraction boundaries described below are enforced by real API types and signatures, the portable collection/index cleanup is finished, and HDPart can be written primarily against Tier 1 without seeing Tier-2 mechanics.
>
> **Do not mark this design complete merely because imports moved. Do not fix the language server against the current library surface. Finish the language/library shape first.**

## Purpose

The Amiga library surface had accumulated multiple abstraction levels under the same namespaces. The result was that application code, safe NDK wrappers, and raw NDK bindings were too easy to mix accidentally.

The desired model is deliberately simple:

1. A high-level application layer for the 90% case.
2. A systems layer for programmers who know the NDK and need control without giving up ownership and type safety.
3. A raw layer that exposes the NDK directly.

The goal is **progressive disclosure, not wrapper proliferation**.

This document is an implementation contract. When an existing API conflicts with the rules below, the API must change; the document is not merely descriptive.

---

# Non-negotiable design rules

## Rule 1: Tier 1 must be a real abstraction boundary

A Tier-1 type must **not** normally be a type alias for a Tier-2 handle.

This is insufficient:

```novus
pub type Window = GadToolsWindow
pub type Screen = ScreenHandle
pub type FileRequester = FileRequesterHandle
pub type Directory = DirLockHandle
pub type Volume = MountedFileSystem
```

A type alias leaks the entire systems-layer method surface into Tier 1 and makes the namespace look cleaner without actually creating an abstraction boundary.

Tier 1 should instead use thin owning or borrowing wrappers/newtypes where control of the public API is required:

```novus
pub struct Window {
    system: amiga::sys::gadtools::GadToolsWindow,
}

impl Window {
    pub fn system(&self) -> &GadToolsWindow {
        return &self.system
    }
}
```

The wrapper may be representation-transparent or trivially optimized away. The purpose is API control, not runtime indirection.

**Requirement:** application code importing a Tier-1 type must not automatically gain every Tier-2 method merely because the type is an alias.

---

## Rule 2: Tier-1 public signatures must not expose Tier-2 mechanics

A Tier-1 API is not high-level if its parameters or return types require the caller to understand NDK implementation structures.

Examples of types that should normally not appear in Tier-1 public signatures:

```text
DosEnvironment
DosNodeDraft
FileSystemHandler
LoadedSegment
MountedFileSystem
DirLockHandle
FileRequesterHandle
ScreenHandle
GadToolsWindow
IntuitionError
AslError
ExecError
BPTR
IORequest
TagItem
```

Current code such as:

```novus
FileSystem::mount(
    name: Str,
    driver: Str,
    unit: u32,
    environment: &DosEnvironment,
)
```

is **not the desired final Tier-1 API**. `DosEnvironment` is a systems-layer concept and must be constructed below the Tier-1 boundary.

Tier 1 should accept semantic application data instead, for example:

```novus
let volume = filesystem.mount(MountSpec {
    name: "DH0",
    device: device,
    partition: partition,
})?
```

or another design that expresses the same intent without exposing the NDK environment block.

The exact shape may evolve, but the layering rule is mandatory.

---

## Rule 3: Tier-1 errors stay at Tier 1

Tier-1 operations must return Tier-1/domain errors unless the caller deliberately steps down to the systems layer.

For example, this is not an acceptable final Tier-1 signature:

```novus
FileRequester::choose(...) -> Result<Option<PathString>, AslError>
```

The Tier-1 API should return:

```novus
Result<Option<PathString>, UiError>
```

Similarly:

- `amiga::storage` should return storage/application errors, not `DeviceError` or `ExecError`.
- `amiga::ui` should return `UiError`, not `IntuitionError` or `AslError`.
- `amiga::dos` should return DOS/application-domain errors appropriate to the operation, not expose handler-launch details by default.

Lower-level errors remain available through Tier 2 when the programmer chooses that layer.

---

## Rule 4: Tier 1 expresses intent; Tier 2 expresses Amiga mechanics

If an operation is normal application behavior, Tier 1 should own the Amiga-specific procedure required to accomplish it.

An HD partition editor should not have to reimplement the platform algorithm for:

1. finding a live filesystem handler
2. checking `FileSystem.resource`
3. loading an RDB FSHD/LSEG image
4. falling back to `L:FastFileSystem`
5. creating a DeviceNode
6. transferring segment ownership
7. starting the handler
8. waiting for the DOS device to appear
9. inhibiting the volume
10. formatting it
11. resuming it

Those operations are platform mechanics and belong below a semantic Tier-1 API.

Tier 2 must still expose the individual primitives for software that genuinely needs to control the procedure.

---

## Rule 5: Tier 1 is organized by what the developer wants to do

Tier 1 should not mirror NDK library names.

Application developers think in terms of:

```text
windows
controls
files
volumes
disks
sound
input
screens
requesters
```

not:

```text
intuition.library
gadtools.library
asl.library
exec.library
dos.library
```

Therefore `amiga::ui`, `amiga::storage`, `amiga::dos`, etc. are intentional semantic domains.

Tier 2 should preserve recognizable NDK subsystem names because at that level the NDK model is the point of the API.

---

## Rule 6: dependencies point downward only

```text
Tier 1 application API
          ↓
Tier 2 systems API
          ↓
Tier 3 raw NDK API
```

Examples:

```text
amiga::storage       → amiga::sys::device
amiga::sys::device   → amiga::raw::exec
```

A Tier-1 implementation may internally use Tier 2. A Tier-2 implementation may internally use Tier 3.

The reverse is forbidden.

Application code importing `amiga::raw::*` is a strong signal that either:

1. the caller deliberately chose raw control, or
2. the high-level API is missing an operation.

Do not normalize raw imports in ordinary application code.

---

# Core architecture

## Tier 1: application API

Namespace:

```text
amiga::
    dos
    storage
    ui
    graphics
    audio
    input
    timer
    workbench
```

Examples:

```novus
from amiga::storage import BlockDevice
from amiga::ui import Window, FileRequester
from amiga::dos import File, Directory
```

Tier 1 should expose domain concepts, not implementation mechanisms.

Application code should normally not see:

```text
IORequest
MsgPort
BPTR
DosList
FileSysStartupMsg
TagItem
LibraryBase
Resident
STRPTR
MEMF_PUBLIC
raw pointers
manual cleanup sequences
```

Tier 1 should:

- own resources
- use `Drop`
- return `Result`
- use `Option` for absence
- accept slices/iterables instead of pointer/count pairs
- expose semantic configuration instead of tag lists
- collapse subsystem-specific errors into domain errors
- provide controlled `system()` escape hatches where advanced access is useful

### Example: block device

```novus
let var disk = BlockDevice::open("scsi.device", 0)?
let geometry = disk.geometry

var block = [0; 512]
disk.read_blocks(0, &var block)?
```

The caller should not need to allocate an `IORequest`, create a message port, set request fields, call `DoIO`, or close anything manually.

---

## Tier 2: systems API

Namespace:

```text
amiga::sys::
    exec
    dos
    device
    intuition
    graphics
    gadtools
    asl
    utility
    resources
```

Representative types:

```text
amiga::sys::exec::MessagePort
amiga::sys::exec::Task
amiga::sys::device::DeviceRequest
amiga::sys::dos::BPtr<T>
amiga::sys::dos::DosList
amiga::sys::dos::DeviceNode
amiga::sys::intuition::Window
amiga::sys::utility::TagList
```

Tier 2 should map closely enough to NDK concepts that an experienced Amiga programmer recognizes the model immediately.

It improves the dangerous mechanics:

- ownership
- cleanup
- lifetimes
- move semantics
- typed handles
- strings
- slices
- `Result`
- `Option`
- resource locking

It does **not** hide the underlying Amiga model when that model is the reason the API exists.

The current `DeviceRequest` concept is the right kind of Tier-2 abstraction: it owns the reply port, request allocation, open-device state and cleanup while still allowing a programmer to get at the underlying request when needed.

---

## Tier 3: raw NDK API

Namespace:

```text
amiga::raw::
    exec
    dos
    intuition
    graphics
    gadtools
    asl
    utility
    devices
    resources
    structs
    consts
```

Examples:

```novus
OpenDevice(...)
DoIO(...)
FindName(...)
MEMF_PUBLIC
CMD_READ
TD_GETGEOMETRY
*IORequest
*Window
```

This is the direct Amiga NDK surface.

The old Amiga contents of `std::ffi` belong here. `ffi` describes implementation technique; `amiga::raw` describes what the API actually is.

Generic language FFI support, if any, may remain under portable `std::ffi`.

---

# Required Tier-1 wrapper work

The namespace migration is **not sufficient**. The following categories must be reviewed and converted from aliases/facades into controlled Tier-1 APIs where necessary.

## UI

Current Tier-1 aliases such as these are transitional, not final:

```novus
pub type Window = GadToolsWindow
pub type WindowBuilder = GadToolsBuilder
pub type Event = GadToolsEvent
pub type FileRequester = FileRequesterHandle
pub type Screen = ScreenHandle
```

Required end state:

- `amiga::ui::Window` owns or wraps the relevant systems-layer window state.
- only application-appropriate methods are exposed directly.
- `Window::system()` provides controlled access to the systems-layer object.
- `FileRequester` returns `UiError`, not `AslError`.
- `Screen` does not expose every Intuition systems method by aliasing the systems handle.
- application event types should be semantic and stable even if the underlying toolkit changes.

Do not create unnecessary wrapper chains. One meaningful Tier-1 object over one meaningful Tier-2 object is enough.

## DOS

Transitional aliases such as:

```novus
pub type Directory = DirLockHandle
pub type Volume = MountedFileSystem
```

must be reviewed for leakage.

Required end state:

- application `Directory` exposes normal directory operations, not the entire DOS lock API.
- application `Volume` exposes volume operations, not every mounted-filesystem systems primitive.
- both may provide `system()` when an advanced caller needs the lower-level handle.

## Storage

`BlockDevice` is already close to a legitimate Tier-1 abstraction because it presents geometry and block operations while owning the request mechanics.

Required cleanup:

- expose application properties idiomatically
- keep device/request internals below the boundary
- return Tier-1 storage errors
- use native collection/index types
- keep `system()` as the advanced escape hatch

---

# High-level filesystem requirements

## `FileSystem::mount` must stop requiring `DosEnvironment`

A Tier-1 mount call must not require an application to construct the NDK environment array/structure.

Instead, define an application-level specification from semantic storage/partition information.

Possible direction:

```novus
pub struct MountSpec {
    name: Str,
    driver: Str,
    unit: u32,
    partition: PartitionGeometry,
    dos_type: u32,
}
```

or another equivalent semantic shape.

Tier 1 translates that into `DosEnvironment` internally.

The exact final structure should be driven by HDPart and other real use cases, but **passing `&DosEnvironment` through Tier 1 is explicitly transitional and must be removed**.

## Filesystem images must not expose handler-launch mechanics unnecessarily

The current Tier-1 `FileSystemImage` shape includes concepts such as:

```text
global_vec
stack_size
priority
```

These are implementation/handler-launch details.

They may exist internally or inside a systems-layer metadata object, but the Tier-1 API should expose a semantic embedded filesystem object where possible.

If HDPart needs to provide RDB-derived metadata, that conversion should be centralized so normal applications do not learn the launch mechanics.

---

# High-level storage discovery

Tier 2 may expose the real DOS-list lock and owner-tied views:

```novus
let list = DosList::lock()?
for entry in list {
    ...
}
```

Tier 1 should return owned snapshots:

```novus
let devices = storage::devices()?
for device in devices {
    ...
}
```

No DOS-list lock or borrowed DOS entry should escape Tier 1.

The current `StorageDevice` owned-snapshot direction is correct.

However, the implementation itself must be rewritten using idiomatic current Novus rather than retaining pre-0.10 iterator and Option boilerplate.

Preferred style:

```novus
for entry in list {
    let startup = entry.startup() else continue
    let driver = startup.device_name() else continue
    ...
}
```

The stdlib must dogfood the language.

---

# Portable stdlib cleanup required by the Amiga redesign

The platform design depends on portable containers and views having one coherent indexing model.

## Finish the `usize` / `isize` migration

The language now has native index types. The portable libraries must use them.

`ArrayVec`, `Slice`, `MutSlice`, `Iterable`, arrays, and similar generic collection/view APIs should use `usize` for lengths, capacities, and indices unless an external ABI specifically requires another type.

Target shape:

```novus
pub struct ArrayVec<T, const N: usize> {
    ...
    len: usize,
}

pub fn len(&self) -> usize
pub fn capacity(&self) -> usize
pub fn get(&self, index: usize) -> Option<&T>
pub fn get_mut(&var self, index: usize) -> Option<&var T>
```

On supported 68k targets, `usize` is 32-bit, so this should not impose runtime overhead.

Do not leave a half-migrated world where the language has `usize` but core collections continue to encode indexing semantics as `u32`.

---

# Canonical portable namespaces

Portable types must have one obvious public home.

Use canonical paths such as:

```text
std::string::Str
std::string::FixedString<N>
std::string::FixedCString<N>
std::collections::ArrayVec
std::memory::Slice
std::memory::MutSlice
std::error::Error
```

Legacy/internal paths such as:

```text
std::string::core
std::collections::arrayvec
std::memory::slice
```

may exist as implementation files, but library/application source should use the canonical exported path wherever possible.

Do not preserve implementation-history namespaces as the normal public API.

---

# Interop between tiers

There must be no abstraction cliff.

Standardize the concepts below for owning wrappers where they apply:

```novus
system()      // borrow the next safe systems-layer object
as_raw()      // borrow the native/raw handle
into_raw()    // transfer ownership downward
from_raw()    // adopt explicitly owned raw state
```

Additional rules:

- borrowing downward does not alter ownership
- `into_raw()` consumes the owner
- multi-resource `into_raw()` returns a named typed raw-state object, not an unstructured tuple
- `from_raw()` validates state when validation is possible
- ownership-transfer methods exist because a real API needs them, not merely for symmetry

Example:

```novus
let var disk = BlockDevice::open("scsi.device", 0)?

disk.read_blocks(0, &var block)?

custom_controller_command(disk.system(), command)?

disk.write_blocks(1, &block)?
```

The same high-level object remains usable after a borrowed systems-level operation.

---

# Avoid duplicate object models

Do not create a new wrapper for every tiny semantic step.

Avoid families such as:

```text
RawWindow
WindowHandle
SafeWindow
ManagedWindow
GadToolsWindow
BufferedWindow
ApplicationWindow
```

with overlapping ownership/state.

Prefer one meaningful type per meaningful layer:

```text
amiga::raw::intuition::Window   // ABI structure
amiga::sys::intuition::Window   // safe NDK-level owner
amiga::ui::Window               // application abstraction
```

Three meanings, three levels.

Layers should share views/borrowed references where possible instead of copying data into parallel incompatible graphs.

---

# Collections and ABI views

NDK pointer/count APIs should generally become slices or iterables at Tier 1 and Tier 2.

Prefer:

```novus
fn replace_items(items: Slice<Str>)
```

or an iterable abstraction over:

```text
STRPTR *items, ULONG count
```

Tier-1 application APIs must not force callers to create parallel storage and pointer/view arrays solely to satisfy an NDK ABI.

The wrapper should construct transient native tables internally.

This requirement applies especially to GadTools cycle/list-view content and similar APIs.

---

# Memory layering

Tier 1 expresses intent:

```novus
let buffer = Buffer::new(byte_count)?
```

When memory class matters, Tier 2 exposes semantic Exec allocation controls:

```novus
let memory = amiga::sys::exec::Memory::alloc(
    byte_count,
    MemoryFlags::PUBLIC,
)?
```

Tier 3 exposes raw `AllocMem` and flags directly.

A normal Tier-1 application importing `MEMF_PUBLIC` is a layering smell.

---

# API classification test

Every public Amiga API must answer:

> Who is this API for?

If the answer is:

> A developer trying to make a normal Amiga application.

It belongs in `amiga::*`.

If the answer is:

> A developer who knows the NDK and needs direct control while retaining Novus safety/ownership.

It belongs in `amiga::sys::*`.

If the answer is:

> A developer who wants the actual NDK interface.

It belongs in `amiga::raw::*`.

If the intended user is unclear, stop and classify the API before adding it.

---

# Current migration assessment

## Completed foundation

The following work is considered foundationally complete:

- `amiga::*`, `amiga::sys::*`, and `amiga::raw::*` namespace resolution exists.
- raw Amiga APIs have a dedicated raw tier.
- major systems wrappers have moved under the systems tier.
- initial semantic Tier-1 modules (`storage`, `dos`, `ui`, etc.) exist.
- HDPart imports primarily from the new platform namespaces.

## Explicitly NOT complete

The library redesign remains incomplete until all of the following are true:

- Tier-1 aliases that leak Tier-2 method surfaces have been replaced where API control is needed.
- Tier-1 public signatures no longer expose `DosEnvironment`, systems handles, subsystem-specific errors, and similar implementation mechanics.
- Tier-1 filesystem mount/format APIs accept semantic application data rather than NDK structures.
- `FileSystemImage` or its replacement no longer makes ordinary callers reason about handler-launch details unnecessarily.
- `ArrayVec`, `Slice`, `MutSlice`, `Iterable`, and related portable collection APIs use `usize` coherently.
- canonical portable namespace imports are used throughout stdlib/platform code.
- Tier-1 errors do not leak `AslError`, `IntuitionError`, `ExecError`, etc.
- stdlib/platform implementations use idiomatic Novus 0.10 features rather than old Option/iterator/cast boilerplate.
- HDPart has been refactored to use the final Tier-1 surfaces and modern language idioms.
- generated 68k for HDPart has been reviewed to ensure safety abstractions remain cheap.

**Until these criteria are met, this document must remain marked NOT COMPLETE.**

---

# HDPart acceptance target

HDPart is the primary migration proof.

Its normal application modules should eventually import roughly at this level:

```novus
from std::collections import ArrayVec
from std::string import FixedString, Str

from amiga::storage import BlockDevice, StorageDevice
from amiga::dos import FileSystem, Volume
from amiga::ui import Window, Dialog, FileRequester
```

Deep RDB/filesystem code may legitimately use a small number of `amiga::sys::*` imports where its actual job requires NDK-level control.

Normal application code should have zero `amiga::raw::*` imports.

HDPart should also be rewritten to exercise the language features now available:

- contextual numeric literals
- compound assignment
- direct safe indexing/slicing
- contextual `Option` binding
- uniform `for` iteration
- `enumerate()` where appropriate
- byte strings / FourCC literals
- fixed-capacity interpolation/formatting
- represented enums for gadget/menu IDs
- slice copy/equality/fill helpers
- test expectation helpers

Examples of pre-0.10 patterns that should disappear where the new language makes them unnecessary:

```novus
let Option::Some(value) = values.get(index) else { continue }

var iterator = values.iter()
forever {
    let Option::Some(value) = iterator.next() else { break }
}

const DEVICE: u16 = 1
const PARTITIONS: u16 = 2
```

Preferred direction:

```novus
for value in values {
    ...
}

enum GadgetId: u16 {
    Device = 1
    Partitions
    ...
}
```

A successful migration reduces:

- raw pointers
- manual ownership transfer
- manual cleanup
- tag-list construction
- DOS-list mechanics
- handler-startup mechanics
- parallel ABI-only arrays
- redundant casts/type annotations
- manual iterator loops
- manual Option destructuring

without reducing functionality or making generated 68k materially worse.

---

# Required implementation order

The agent implementing this redesign should follow this order unless a dependency forces a small adjustment:

1. **Finish portable `usize` migration** for collections, slices, iterables, lengths, capacities, and indices.
2. **Replace Tier-1 aliases with controlled wrappers/newtypes** where aliases expose Tier-2 APIs.
3. **Remove Tier-2 types from Tier-1 public signatures**, starting with `DosEnvironment`, systems handles, and subsystem errors.
4. **Redesign Tier-1 filesystem mount/format APIs** around semantic application data.
5. **Normalize Tier-1 error domains** (`UiError`, storage errors, DOS/filesystem errors).
6. **Normalize canonical portable imports** (`std::string`, `std::collections`, `std::memory`, etc.).
7. **Refactor stdlib and Amiga libraries to idiomatic Novus 0.10 syntax**.
8. **Refactor all HDPart source and tests** to the final Tier-1 surfaces and current language idioms.
9. **Inspect generated 68k and binary size** for representative HDPart hot paths and safe byte operations.
10. **Only after language/library APIs are stable, repair the language server** against the final syntax and namespaces.

Do not skip directly to the language server.

Do not declare the library redesign complete after step 2 or after namespace cleanup alone.

---

# Completion checklist

This design may be changed from **NOT COMPLETE** to **COMPLETE** only when all boxes are true:

- [ ] Portable collection/view APIs use `usize`/`isize` consistently.
- [ ] Tier-1 application types have controlled public surfaces rather than leaking Tier-2 aliases where that matters.
- [ ] Tier-1 public APIs expose no avoidable NDK mechanics.
- [ ] Tier-1 errors do not leak subsystem-specific systems errors.
- [ ] Filesystem mount/format is semantic at Tier 1.
- [ ] Storage discovery returns owned snapshots and hides DOS-list mechanics.
- [ ] UI APIs hide toolkit pointer-array/tag/message plumbing.
- [ ] Canonical portable namespaces are used consistently.
- [ ] `system()` / `as_raw()` / ownership-transfer conventions are consistent across adjacent layers.
- [ ] No duplicate wrapper/object-model families remain without a clear semantic reason.
- [ ] Amiga stdlib code itself uses idiomatic Novus 0.10 constructs.
- [ ] HDPart application code primarily uses Tier 1.
- [ ] HDPart tests use the final APIs and modern test helpers.
- [ ] Representative generated 68k has been reviewed for unnecessary abstraction overhead.
- [ ] Language server work has not forced compatibility hacks back into the language/library design.

---

# Final principle

The library should offer **a staircase, not a cliff and not a maze**.

A normal developer starts with semantic Amiga concepts.

An experienced Amiga developer steps down once and sees familiar NDK concepts made safe.

A systems programmer steps down again and gets the real NDK.

Each level must be internally coherent, ownership-compatible with adjacent levels, and obvious from its namespace.

A namespace rename alone does not satisfy this design. The public type surfaces and signatures must enforce it.
