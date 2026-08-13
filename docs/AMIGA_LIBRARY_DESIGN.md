# Novus Amiga Library Design

## Purpose

The Amiga library surface had accumulated multiple abstraction levels under the same namespaces. The result was that application code, safe NDK wrappers, and raw NDK bindings were too easy to mix accidentally.

The desired model is simple:

1. A high-level application layer for the 90% case.
2. A systems layer for programmers who know the NDK and need control without giving up ownership and type safety.
3. A raw layer that exposes the NDK directly.

The goal is progressive disclosure, not wrapper proliferation.

---

# Core architecture

## Tier 1: application API

This is for developers who want to write Amiga applications without manually managing NDK mechanics.

Recommended namespace:

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

The application layer should own resources, use `Drop`, return `Result`, use `Option` for absence, and accept slices/iterables instead of pointer/count pairs.

### Example: block device

```novus
let var disk = BlockDevice::open("scsi.device", 0)?
let geometry = disk.geometry()

var block = [0; 512]
disk.read_blocks(0, &var block)?
```

The caller should not need to allocate an `IORequest`, create a message port, set request fields, call `DoIO`, or close anything manually.

---

## Tier 2: systems API

This layer is for developers who understand AmigaOS and want direct control while retaining Novus ownership, lifetime, error, and type-safety conventions.

Recommended namespace:

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

It should improve the dangerous mechanics:

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

But it should not hide the underlying Amiga model when that model is the point of the API.

The current `DeviceRequest` design is the right kind of Tier-2 abstraction: it owns the message port, request allocation, open device state, cleanup, and synchronous command execution while still exposing the underlying request when required.

---

## Tier 3: raw NDK API

This is the direct Amiga NDK surface.

Recommended namespace:

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

Most of the existing `std::ffi` Amiga bindings belong here.

`ffi` describes implementation technique, not user intent. These files are the raw Amiga API and should be named accordingly.

Generic language FFI support, if any, may remain under a portable `std::ffi` namespace.

---

# Dependency rule

Dependencies point only downward:

```text
Application API
      ↓
Systems API
      ↓
Raw NDK
```

Examples:

```text
amiga::storage         → amiga::sys::device
amiga::sys::device     → amiga::raw::exec
```

Application code importing `amiga::raw::*` is a strong signal that a higher-level capability is missing or the caller has explicitly chosen to leave the high-level layer.

High-level library code should also avoid importing raw NDK constants when a systems-level semantic type can represent them.

---

# Portable stdlib versus Amiga platform library

Portable facilities should remain under `std`:

```text
std::core
std::collections
std::string
std::memory
std::io
std::math
std::net
```

Amiga-specific facilities should move out of the generic `std` root and under the Amiga platform namespace.

Current top-level areas such as `std::ui`, `std::graphics`, `std::hardware`, `std::prefs`, and much of `std::os` are platform-specific and should be classified into one of the three Amiga tiers.

---

# Organize Tier 1 by developer intent

Tier 1 should not mirror NDK library names.

A normal application developer thinks in terms of:

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

Therefore:

```text
amiga::ui
```

should expose application concepts such as:

```text
Window
Screen
Button
TextField
IntegerField
Cycle
ListView
Menu
Dialog
FileRequester
Event
Canvas
```

The implementation may use Intuition, GadTools, ASL, or another subsystem.

By contrast, the systems layer should preserve subsystem identities:

```text
amiga::sys::intuition
amiga::sys::gadtools
amiga::sys::asl
amiga::sys::reaction
amiga::sys::mui
```

This allows the developer to choose altitude deliberately.

---

# Progressive disclosure and interop

There must be no abstraction cliff.

A developer using Tier 1 should be able to borrow the next layer down for one unusual operation and then continue using the high-level object.

Standardize interop for owning wrappers.

Where applicable, every wrapper should support these concepts:

```text
borrow next layer down
borrow raw/native handle
transfer ownership downward
adopt ownership upward
```

Recommended naming convention:

```novus
system()      // borrow the next safe systems-layer object
as_raw()      // borrow the native/raw handle
into_raw()    // transfer ownership downward
from_raw()    // adopt explicitly owned raw state
```

Domain-specific aliases may exist when they improve readability, but behavior must remain consistent.

For multi-resource owners, `into_raw()` must return a named typed state object rather than an unstructured tuple.

Example:

```novus
let var disk = BlockDevice::open("scsi.device", 0)?

disk.read_blocks(0, &var block)?

custom_controller_command(disk.system(), command)?

disk.write_blocks(1, &block)?
```

Borrowing downward must not transfer ownership.

---

# Avoid duplicate object models

Do not create a new wrapper type at every minor abstraction step.

A bad direction would produce families such as:

```text
RawWindow
WindowHandle
SafeWindow
ManagedWindow
GadToolsWindow
BufferedWindow
ApplicationWindow
```

with mostly overlapping state and ownership.

Prefer one clear object per meaningful layer:

```text
amiga::raw::intuition::Window   // ABI structure
amiga::sys::intuition::Window   // safe owner / NDK-level wrapper
amiga::ui::Window               // application composition
```

Three meanings, three levels.

Layers should share types and borrow views where possible instead of copying information into parallel object graphs.

---

# Ownership rules

All owning wrappers should follow the same principles:

- resources are move-only where duplicate ownership would be unsafe
- cleanup is performed by `Drop`
- fallible construction returns `Result`
- absence uses `Option`
- ownership transfer uses `consuming self`
- borrowed handles do not alter ownership
- `from_raw` validates state when validation is possible
- ownership-transfer APIs are added only when real lower-level APIs require transfer

Do not manufacture ownership-transfer APIs solely for symmetry.

---

# Collections and views

NDK pointer/count APIs should generally become slices or iterables in Tier 1 and Tier 2.

Prefer:

```novus
fn replace_items(items: Slice<Str>)
```

or an iterable abstraction over:

```text
STRPTR *items, ULONG count
```

Tier 1 application APIs should not force callers to create parallel storage and pointer/view arrays solely to satisfy an NDK ABI.

The wrapper should build transient native tables internally when needed.

This is especially important for GadTools cycle/list-view content and similar APIs.

---

# Strings

Portable string types should have one obvious home:

```text
std::string::Str
std::string::FixedString<N>
std::string::FixedCString<N>
```

Avoid exposing implementation-history paths such as `std::string::core` or multiple competing homes for fixed strings.

Amiga APIs should convert at the boundary:

- application APIs accept ordinary Novus string views or fixed strings
- systems APIs may expose explicit C-string requirements when the NDK semantics matter
- raw APIs expose the native pointer representation

High-level code should not normally manipulate `STRPTR`.

---

# Memory

Tier 1 should express intent, not Exec allocation flags.

Example:

```novus
let buffer = Buffer::new(byte_count)?
```

When memory class matters, Tier 2 may expose semantic allocation controls:

```novus
let memory = amiga::sys::exec::Memory::alloc(
    byte_count,
    MemoryFlags::PUBLIC,
)?
```

Tier 3 exposes `AllocMem`/raw flags directly.

High-level application code importing `MEMF_PUBLIC` should be treated as a layering smell.

---

# Errors

Errors should belong to the layer and domain they describe.

Prefer:

```text
amiga::storage::StorageError
amiga::ui::UiError
amiga::sys::device::DeviceError
amiga::sys::exec::ExecError
```

Do not use a generic platform-error dumping ground such as `amiga::sys::errors`.

Portable `std::error` should contain the `Error` trait and generic error infrastructure.

Error conversion should follow normal `From`/`Into` conventions between adjacent layers.

Tier 1 errors should generally collapse implementation-specific detail unless that detail is useful to the caller.

---

# Candidate migration map

The exact names may change during implementation, but the abstraction classification should remain.

| Legacy source | Canonical destination | Tier |
|---|---|---|
| `std::os::block_device` | `amiga::storage::BlockDevice` | Application |
| `std::os::device` | `amiga::sys::device::DeviceRequest` | Systems |
| `std::os::bptr` | `amiga::sys::dos::BPtr` | Systems |
| `std::os::{dos,filesystem,handler,doslist,dosnode,segment}` | `amiga::sys::dos::*` | Systems |
| `std::os::{exec,process,task}` | `amiga::sys::exec::*` | Systems |
| `std::ui::{gadtools,menu}` | `amiga::sys::gadtools::*` | Systems |
| `std::ui::{window,screen,dialog}` | `amiga::sys::intuition::*` and `amiga::ui::*` | Both |
| `std::ui::asl` | `amiga::sys::asl` and `amiga::ui::FileRequester` | Both |
| Amiga contents of `std::ffi::*` | `amiga::raw::*` | Raw |
| `std::memory::bytes` | portable `std::memory` | Portable |
| `std::collections::ArrayVec` | portable `std::collections` | Portable |
| `FixedString`, `FixedCString`, `Str` | portable `std::string` | Portable |

---

# High-level filesystem responsibilities

HDPart shows that application code currently knows too much about Amiga filesystem activation.

Mechanics such as these should not normally be implemented by an application:

1. search mounted handlers
2. inspect `FileSystem.resource`
3. inspect RDB FSHD/LSEG entries
4. fall back to `L:FastFileSystem`
5. create a DeviceNode
6. transfer segment ownership
7. launch the handler
8. wait for mount
9. inhibit the volume
10. format
11. resume the volume

These are platform mechanics.

Tier 1 should expose intent, for example:

```novus
let filesystem = FileSystem::for_dos_type(partition.dos_type)?
let volume = filesystem.mount(device, partition, name)?
volume.format(volume_name)?
```

The exact API should be designed from real use cases, but the application must not be forced to reproduce the NDK algorithm merely to perform a normal operation.

Tier 2 retains the individual primitives for specialized software.

---

# High-level storage discovery

Tier 2 may expose the real DOS list safely:

```novus
let list = DosList::lock()?
for entry in list {
    ...
}
```

Tier 1 should normally return owned snapshots:

```novus
let devices = storage::devices()?
for device in devices {
    ...
}
```

The common caller should not need to understand DOS-list locking or owner-tied views.

---

# UI layering

Current UI modules span application conveniences and specific toolkit wrappers. Split these roles explicitly.

## Tier 1

```text
amiga::ui
```

Provide common application concepts and consistent events.

The caller should not manually create tag lists, reply Intuition messages, manage native gadget linked lists, or maintain duplicate text-pointer arrays.

## Tier 2

```text
amiga::sys::intuition
amiga::sys::gadtools
amiga::sys::asl
amiga::sys::reaction
amiga::sys::mui
```

Expose toolkit/subsystem semantics safely.

## Tier 3

```text
amiga::raw::intuition
amiga::raw::gadtools
...
```

Expose native calls, structs, tags, and constants.

---

# API review litmus test

Every new Amiga library API must answer:

> Who is this API for?

If the answer is:

> A developer trying to make a normal Amiga application.

It belongs in `amiga::*`.

If the answer is:

> A developer who knows the NDK and needs direct control, but still wants Novus ownership and type safety.

It belongs in `amiga::sys::*`.

If the answer is:

> A developer who wants the actual NDK interface.

It belongs in `amiga::raw::*`.

If the intended user is unclear, the API should not be added until its abstraction level is understood.

---

# Migration rules

1. Freeze ad-hoc additions to the current Amiga stdlib layout while classification is underway.
2. Inventory every Amiga-facing module and classify each public API as application, systems, raw, portable, duplicate, or obsolete.
3. Move raw NDK bindings first because their destination is the least ambiguous.
4. Move clear Tier-2 wrappers next.
5. Build Tier-1 APIs from real application use cases rather than wrapping every Tier-2 type automatically.
6. Add compatibility aliases temporarily when useful, but document the canonical path.
7. Do not duplicate implementation merely to support old and new namespaces.
8. Delete obsolete wrappers once migration coverage is complete.
9. Keep tests at each boundary.

---

# Interop tests

Every adjacent abstraction boundary should have tests proving:

- borrowing downward does not transfer ownership
- a raw/system call can return to the higher-level API safely
- `into_raw` prevents the moved high-level owner from cleaning up
- `from_raw` adopts exactly one ownership responsibility
- `into_raw` / `from_raw` round trips do not leak
- no double-free occurs
- owner-tied views cannot outlive their owner

Representative boundaries include:

```text
BlockDevice → DeviceRequest
DeviceRequest → IORequest
UI Window → Intuition Window
Intuition Window → raw Window pointer
typed gadget → raw Gadget pointer
DOS-list entry → BPTR/native structures
```

---

# HDPart acceptance target

HDPart should be used as the primary migration proof.

The main application should eventually import approximately this level of API:

```novus
from std::collections import ArrayVec
from std::string import FixedString, Str

from amiga::storage import BlockDevice, StorageDevice
from amiga::dos import FileSystem, Volume
from amiga::ui import Window, Dialog, FileRequester
```

Deep RDB/filesystem code may legitimately use a small number of `amiga::sys::*` imports.

The normal application layer should have zero `amiga::raw::*` imports.

A successful migration should reduce:

- direct NDK constants in application code
- raw pointers
- manual ownership transfer
- manual cleanup
- tag-list construction
- DOS-list mechanics
- handler startup mechanics
- parallel ABI-only arrays

without reducing functionality or making generated 68k code materially worse.

---

# Implementation order

Recommended sequence:

1. Define namespace policy and API classification rules.
2. Inventory current `std::os`, `std::ui`, Amiga `std::ffi`, `std::graphics`, `std::hardware`, and related modules.
3. Establish `amiga::raw::*` and move/alias generated NDK bindings.
4. Establish `amiga::sys::*` and move/alias clear NDK-safe wrappers.
5. Normalize interop naming and ownership contracts across Tier 2.
6. Normalize portable homes for strings, collections, memory, and error traits.
7. Design Tier-1 `amiga::storage` from HDPart storage/discovery needs.
8. Design Tier-1 `amiga::dos` from HDPart mount/filesystem/format needs.
9. Design Tier-1 `amiga::ui` from HDPart and existing idiomatic examples.
10. Refactor HDPart to use Tier 1 by default and Tier 2 only where its job genuinely requires NDK-level control.
11. Remove duplicate object models and obsolete compatibility paths.

---

# Final principle

The library should offer a staircase, not a cliff and not a maze.

A beginner or application developer starts with semantic Amiga concepts.

An experienced Amiga developer can step down one level and work safely with recognizable NDK concepts.

A systems programmer can step down again and get the real NDK.

Each level should be internally coherent, ownership-compatible with adjacent levels, and obvious from its namespace.

---

## Implementation status (2026-08-13)

The staircase is now active rather than only proposed:

- `amiga::raw::*` resolves directly to the generated NDK bindings, including the `devices::*` and `resources::*` families. No bindings are duplicated.
- `amiga::sys::{exec,dos,device,intuition,gadtools,asl,graphics,hardware,resources,timer,utility,workbench}` exposes owning NDK-level wrappers under canonical systems paths. Systems modules depend only on other systems modules, raw bindings, and portable facilities.
- `amiga::storage` owns block-device discovery and returns copied `StorageDevice` snapshots; DOS-list locks and owner-tied entries no longer escape into application code.
- `amiga::dos::File` hides DOS modes and pointer/count I/O. `FileSystem::resolve` and `mount` own handler lookup, embedded HUNK loading, DOS-node construction, handler startup, and mount waiting. `Volume::format` owns inhibit/format/resume.
- `amiga::ui` owns application windows, builders, events, dialogs, screens, and requesters. Window-parented alerts, confirmation, choices, and ASL requests no longer expose raw Intuition window handles.
- `amiga::{audio,graphics,input,timer,workbench}` expose application terminology while specialist device, RastPort, hardware, and toolkit APIs remain in `amiga::sys`.
- `std::{collections,string,memory,io,error}` are canonical portable façades. `Buffer` hides Exec allocation flags and exposes only bounded slices.
- Owning wrappers use `system()`, `as_raw()`, `into_raw()`, and validating `from_raw()` where the corresponding transition is real and safe. Obsolete `handle()`, `request()`, and `raw()` compatibility names have been removed.

HDPart is the migration proof. Its main application imports only portable façades and `amiga::{storage,ui,workbench}`. Deep format/safety modules use `amiga::sys::dos`; the complete `src` tree has no raw imports or legacy paths.

The old Amiga-specific `std::{ffi,os,ui,graphics,hardware,audio,args,prefs,strings}` implementation trees are removed. Boundary tests reject legacy trees, upward systems dependencies, Tier-1 raw dependencies, noncanonical HDPart imports, and the former platform-error dumping ground.
