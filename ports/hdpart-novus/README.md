# HDPart Novus porting case study

This is a functional but not yet feature-complete port of Stefan Skotte's real AmigaOS
[HDPart](https://github.com/stefanskotte/hdpart) application. The source was
pinned at commit
[`d71ced9`](https://github.com/stefanskotte/hdpart/tree/d71ced9cc601db4909f6350c8dcfa68639eec46a)
on 2026-08-11. HDPart is MIT licensed, targets Kickstart 2.04+, uses GadTools,
and has no third-party GUI runtime. The upstream license is preserved in
[`LICENSE.upstream`](LICENSE.upstream).

HDPart is a useful test because it is neither a toy nor a game engine. Its
6,351 lines of C discover block devices, read and validate Rigid Disk Blocks,
edit partitions, prevent writes to mounted/boot media, install filesystems,
format volumes, and drive a 17-control GUI with custom drawing.

## Rule of the exercise

The port stops where an application developer would have to drop to raw FFI,
invent a reusable standard-library abstraction locally, or contort otherwise
clear Novus. A raw `IORequest`, `BPTR`, `TagItem`, cast, pointer, or `unsafe`
block can prove that a port is possible, but it does not close the gap. Those
escape hatches should remain available for advanced code, not be the normal
way to open a disk or populate a list view.

**C with different punctuation is a failed port.** A subsystem is complete
only when application code talks in its own concepts: disks, blocks,
partitions, mounted volumes, controls, and events. Mechanical Amiga concepts
such as message ports, request allocation, BCPL pointer conversion, tag lists,
message replies, and cleanup order must end at a reusable module boundary.

The review uses three grades:

- **Green:** application concepts, value types, iteration, ownership, and
  `Option`/`Result`; no platform call sequence is visible.
- **Amber:** memory-safe and pointer-free, but still mirrors C boilerplate or
  exposes numeric handles/IDs that can be mixed up.
- **Red:** requires raw FFI, `unsafe`, pointer arithmetic, address casts, or
  manual resource cleanup in application code.

This is progressive disclosure, not prohibition:

1. **Application Novus:** the default path covers the common 90% with owned
   resources, typed values, iterators, events, and `Result`.
2. **Systems Novus:** advanced modules may deliberately expose handles,
   buffers, lifetimes, and explicit `.as_raw()` / `.from_raw()` boundaries.
3. **Machine escape hatch:** `unsafe`, C FFI, inline assembly, and the existing
   hardware DSLs remain first-class for code that genuinely needs them.

Dropping a level must be visible in source and voluntary. A missing tier-one
API must never be excused merely because tier three can technically do it.

## What ports cleanly today

[`src/discovery.novus`](src/discovery.novus) ports HDPart's pure capacity and
geometry helpers. It replaces C output pointers with returned value types and
uses `Option<Capacity>` for the unsupported READ CAPACITY sentinel. The test suite
also checks driver-name filtering and the `trackdisk.device` exclusion.

```novus
match parse_read_capacity_10(&response) {
    Option::Some(capacity) => use_capacity(capacity),
    Option::None => try_device_geometry(),
}
```

The parser now uses `ByteReader::read_be_u32`; truncated responses return
`Option::None` without indexing or raw-pointer work in the application.

[`src/rdb.novus`](src/rdb.novus) shows the natural typed RDB model: partition
names are `NameString`, ranges are values, mutation is confined to methods,
and every operation that can fail returns `Result`. This removes caller-owned
output buffers, null sentinels, and integer error conventions from the C API.

## Subsystem scorecard

| HDPart subsystem | Source | Grade today | Port result |
|---|---|---:|---|
| Capacity parsing and synthetic geometry | [`discover.c`](https://github.com/stefanskotte/hdpart/blob/d71ced9cc601db4909f6350c8dcfa68639eec46a/src/discover.c) | Green | Bounds-checked decoding passes its 68020 tests. |
| In-memory partition model | [`rdb.c`](https://github.com/stefanskotte/hdpart/blob/d71ced9cc601db4909f6350c8dcfa68639eec46a/src/rdb.c) | Green | Compiles and passes its 68020 tests. |
| RDB block serialization | [`rdb.c`](https://github.com/stefanskotte/hdpart/blob/d71ced9cc601db4909f6350c8dcfa68639eec46a/src/rdb.c) | Green | RDSK, PART, FSHD, and LSEG offsets, checksums, links, round trips, corruption rejection, and byte-for-byte compatibility with the pinned C serializer pass. |
| Device discovery and block I/O | [`device.c`](https://github.com/stefanskotte/hdpart/blob/d71ced9cc601db4909f6350c8dcfa68639eec46a/src/device.c) | Amber | `DeviceRequest` owns the Exec lifecycle, `BlockDevice` validates whole-block reads and writes, and DOS discovery returns an owned bounded snapshot. A disposable HDF passed RDB write, independent readback, and cold-remount tests. Automatic discovery of the start-time HDF remains an emulator-configuration limitation. |
| Boot/mounted-media protection | [`safety.c`](https://github.com/stefanskotte/hdpart/blob/d71ced9cc601db4909f6350c8dcfa68639eec46a/src/safety.c) | Amber | Pure and live fail-safe classifiers are ported. `SYS:` resolution, bounded DOS traversal, and mounted-device detection pass on the A4000; an HDF-backed boot device remains necessary to prove the live `Boot` and `Clear` branches. |
| GadTools application UI | [`gui.c`](https://github.com/stefanskotte/hdpart/blob/d71ced9cc601db4909f6350c8dcfa68639eec46a/src/gui.c) | Amber | Cycle, list-view, string, text, button, menu, ownership, typed state, model replacement, and events pass on the A4000. Own-screen fallback and canvas remain. |
| Filesystem install and format | [`format.c`](https://github.com/stefanskotte/hdpart/blob/d71ced9cc601db4909f6350c8dcfa68639eec46a/src/format.c) | Amber | HUNK validation, `FileSystem.resource` discovery, embedded HUNK ownership, typed DOS environments, registration, inhibit-gated format, and cold remount pass. MCP input now drives ASL reliably, but the latest live `L:FastFileSystem` selection returned `Could not open the filesystem handler`; resolve that path/open failure before calling import complete. |
| CLI/Workbench startup and stack | [`main.c`](https://github.com/stefanskotte/hdpart/blob/d71ced9cc601db4909f6350c8dcfa68639eec46a/src/main.c) | Green | Novus already owns startup and supports `#[stack_size]`; do not port C's startup/StackSwap scaffolding. |

## Resolved compiler blocker

`RdbModel` needs a fixed 32-entry partition table. The natural initializer is:

```novus
partitions: [RdbPartition::empty(); 32]
```

**LANG-01 is fixed.** Runtime-valued arrays are initialized element by element;
only compile-time constant arrays use the static initializer. The unchanged RDB
test suite now links with VBCC and passes under 68020 emulation.

**DEV-01 is fixed.** `std::os::device::DeviceRequest` owns the reply port,
variable-sized I/O request, open device, and cleanup.
`std::os::block_device::BlockDevice` exposes typed geometry and bounded whole-
block reads and writes. Its A4000 tests cover the `trackdisk.device` no-media
path plus successful writes and reads on an explicitly disposable HDF.

**DOS-01 and DOS-02 are fixed.** `std::os::doslist::DosDeviceList` owns the
read lock, exposes normal iteration, and always unlocks through `Drop`.
`std::os::bptr::BPtr<T>` checks BCPL range and Amiga memory before any safe
wrapper dereferences it; DOS names, `FileSysStartupMsg`, and `DosEnvec` are
owner-tied views. The A4000 test traverses live devices and proves the list can
be reacquired after an early `Result::Err`. HDPart's pure safety rules now live
in `src/safety.novus` and its 68020 tests pass.

**STR-01 and LANG-05 are fixed.** Const-generic values now work in array
lengths and repeat initializers. `FixedString<N>`, `FixedCString<N>`, and the
owner-tied `CStr` view provide stack storage and guaranteed NUL termination.
`DeviceProcess` owns `GetDeviceProc`/`FreeDeviceProc`; the A4000 test resolves
`SYS:` and matches its handler without exposing a raw pointer.

**LANG-06 and COLL-01 are fixed.** `Some(&value)` and
`Some(&var value)` borrow enum payloads in `match` and `let ... else` without
copying them. `Option::as_ref/as_mut` and inline `ArrayVec<T, N>` build on that
syntax. The A4000 generics suite passes bounded push/get/get-mut and exact-once
drop tests. `src/discovery.novus` now snapshots unique mounted device identities
into owned fixed strings; the host-directory A4000 fixture correctly produces
an empty raw-device snapshot.

**BYTES-01 now has independent compatibility evidence.**
`tests/rdb_fixture_test.novus` embeds the exact RDSK, PART, FSHD, and LSEG blocks
emitted by the pinned C implementation. It validates checksums and chain links,
reconstructs the embedded HUNK, and requires Novus to reproduce every block
exactly. The test passes on the real A4000.

`RdbModel::from_image` now follows the complete bounded RDSK/PART/FSHD/LSEG
chain and rejects invalid links or truncated blocks. `src/rdb_io.novus::load_rdb`
locates the header through the owning `BlockDevice`, validates the declared
reserved area, and reads it into an owning `MemoryBlock` through bounded slice
views. Its no-media/live-device test passes on the A4000 without exposing an
`IORequest`, allocation pointer, or manual cleanup to application code.

An idiomatic pass now uses postfix guards, compound predicates, `let ... else`,
`?`, `ok_or`, ranges, and borrowed collection iteration throughout discovery,
safety, RDB, RDB I/O, and format planning. `ArrayVec`, `Slice`, and `MutSlice`
implement the existing `Iterable` contract, and `Str::as_bytes()` keeps its raw
representation inside the standard library. The intended application style is:

```novus
return false unless capacity.block_bytes == 512 && capacity.total_blocks == 2097152
```

The pass exposed a general array-return bug: large constant array literals
copied `sizeof(pointer)` bytes into the hidden result buffer. Code generation
now copies the array's full byte count, and the golden RDB fixture again passes
byte-for-byte under 68020 emulation.

**UI-01 now covers HDPart's control creation path.** `GadToolsBuilder` accepts
ordinary `Slice<Str>` cycle choices and list rows plus normal `Str` values for
string and text gadgets. It owns the native pointer tables, Exec list/nodes,
and copied strings until window teardown. String and integer gadgets also fit
the active screen font's native minimum height instead of failing mysteriously
on Workbench configurations with larger fonts. The HDPart-shaped controls test
opens and closes successfully on the A4000 through `wrun`.

Kind-checked `cycle`, `list_view`, `string`, and `text` handles prevent an ID
from being used through the wrong gadget API. They cover programmatic cycle and
list selection, owner-tied string reads/updates, and text updates. User cycle
and list selections keep using the typed `GadgetUp(id, code)` event;
the same A4000 test exercises these transitions.

Cycle choices and list rows now use one owned allocation per model. Replacing a
model installs the new native pointers before freeing the old allocation, while
invalid IDs, kinds, selections, and active indices leave the current model
untouched. The A4000 test performs 256 alternating cycle/list replacements per
window and five complete create/replace/drop sessions without a failure or Guru.

That probe also exposed a compiler defect: nested enum payload patterns such as
`Result::Err(IntuitionError::WindowOpenFailed)` checked only the outer `Err`
tag. Pattern lowering now recursively checks inner enum tags and literal or
constant payloads. A focused IR regression and the 12-test A4000 errors/patterns
suite pass; codegen cache contract 46 prevents reuse of affected objects.

**FS-01 is fixed.**
`std::os::filesystem::FileSystemRegistry` opens the permanent Exec resource,
traverses its variable-length entries under `CriticalSection`, reads optional
fields only when their patch bits are present, and returns copied handler
metadata with a typed `BPtr` segment. The A4000 test covers the stock resource's
valid no-loaded-handler path and proves every query restores the guest's existing
scheduler nest count.

That probe exposed another compiler defect: temporaries used by a struct nested
inside an enum payload were considered dead before the payload was built, so
same-typed fields could silently receive the final field's value. Liveness now
walks enum payloads recursively, a focused regression passes, and codegen cache
contract 47 prevents reuse of affected objects.

`std::os::dosnode::DosNodeDraft` now owns the `DeviceNode`, startup message,
environment, device name, and driver name without relying on the NDK's
allocation-only `MakeDosNode`. Failure and ordinary scope exit free everything;
successful `AddDosNode` registration transfers the complete graph to DOS. A live
A4000 stress test creates and drops 128 drafts without losing memory.

That stress test exposed a compiler ownership hole: a struct containing owned
fields was treated as non-droppable unless it declared `Drop` itself. Structural
drop now recursively cleans fields in reverse order while preserving explicit
`Drop` behavior. The DOS-node, ownership, and tuple-drop A4000 suites pass, and
codegen cache contract 48 prevents reuse of affected objects.

`MountedFileSystem` owns `GetDeviceProc`/`FreeDeviceProc`; `inhibit()` returns an
owner-tied guard whose `Drop` resumes the filesystem. The destructive `format()`
packet is deliberately available only through that guard, so application code
cannot accidentally send `ACTION_FORMAT` to an active filesystem. The A4000 test
resolves and reacquires `SYS:` without leaking the process handle. It does not
inhibit or format the live boot volume.

`std::os::segment::LoadedSegment` loads embedded HUNK data through
`InternalLoadSeg` with Novus-native register-ABI callbacks and always unloads it
through `Drop`. `DosNodeDraft::register_loaded` binds that segment to the node and
transfers both ownership graphs only after registration succeeds. A 64-cycle
load/unload stress test passes on the A4000 without losing memory.

This exposed a general consuming-call bug: small owned structs and owned fields
were copied into the callee without invalidating the caller's cleanup source.
Call lowering now deactivates whole-value cleanup or clears the original source
field for every consuming parameter. Focused host regressions and the DOS-node,
embedded-segment, and ownership A4000 suites pass; codegen cache contract 49
invalidates affected objects.

AmigaOS 3.2 compatibility is now explicit: a zero `fse_PatchFlags` value may
still carry the documented `fse_SegList`. The registry accepts that convention
while retaining patch-bit checks for non-zero flags. Its A4000 test now requires
an addressable DOS-family handler instead of accepting an empty snapshot.

`src/format.novus::environment_for` ports upstream `format_build_envec` into the
typed `DosEnvironment` used by `DosNodeDraft`; its 68020 test verifies geometry,
cylinder range, buffers, transfer limits, mask, boot priority, DOS type, and the
out-of-range error.

The same format plan now follows upstream's complete handler-selection order:
a compatible mounted handler first, `FileSystem.resource` second, then an owned
FSHD/LSEG HUNK from the RDB. `PreparedNode` keeps an embedded segment alive until
registration and transfers it only after `AddDosNode` succeeds. The A4000 test
passes both the shared-handler and forced embedded-handler branches.

`rdb_io::write_rdb` validates the complete model, reserved area, geometry, and
media capacity before its first write, then emits the pinned upstream
RDSK/PART/FSHD/LSEG layout. On the A4000, the destructive tests target only
`uaehf.device` unit 1 with the exact 131,072-block by 512-byte disposable
geometry. They write and independently reload an RDB, register and start NVT0,
inhibit and format it as DOS\3, resume it, and lock its root. A full emulator
stop/start followed by registration and root locking proves both the RDB and
filesystem survived a cold restart without a Guru.

This slice found four compiler faults: generic structural Drop calls used
unmangled names, fixed-array literal returns emitted invalid C, and persisted C
manifests cached a context-dependent generic-symbol winner. Repeated pattern
bindings could also resolve to an earlier C local; every later binding now gets
a unique IR name. Focused codegen tests pass, a second unchanged compile is an
immediate cache hit, and codegen contract 54 invalidates affected artifacts.

## Port blockers

The port closed these general language/tooling gaps on 2026-08-11:

| ID | Resolution |
|---|---|
| LANG-02 | A newline after any infix operator continues the expression. |
| LANG-03 | Array literals and repeats inherit their expected element type. |
| LANG-04 | Unsuffixed integer literals inherit declaration, return, argument, assignment, comparison, and arithmetic context. |
| LANG-07 | `for` accepts stateful `Iterator<T>` values directly, including `ui.events()`. |
| LANG-08 | Borrowed generic fields retain their concrete `Iterable<T>` element type. |
| TOOL-01 | Normal builds show application diagnostics while dependency and successful VBCC/vasm warnings require `--verbose`. |

| ID | Priority | Missing natural Novus facility | Why HDPart needs it |
|---|---:|---|---|
| UI-02 | P1 | Workbench-or-own-screen policy | A partitioner must still start when Workbench is unavailable. The current GadTools builder only locks the public screen. |
| UI-03 | P1 | Safe canvas plus mouse events | HDPart's disk map is custom-rendered and supports hit testing and dragging. Raw RastPort/window pointers should stay inside the UI module. |
| LAYER-01 | P0 | Uniform safe-to-raw interoperability | A developer must be able to borrow or transfer the underlying device, window, gadget, screen, buffer, DOS entry, or request without rebuilding the surrounding application. |

**UI-04 is fixed.** `FileRequesterHandle` owns its ASL request and returns an
owned `PathString`. The application validates the selected load file, detects
its version and DOS family, and embeds it in the in-memory RDB model without
exposing ASL tags, buffers, or cleanup.

## Binary-size audit

Measured from the same pinned sources and 68020 Novus target on 2026-08-12:

| Build | File bytes | CODE bytes |
|---|---:|---:|
| Pinned C (`-Os`) | 63,608 | 43,500 |
| Novus release `-O1` | 138,324 | 136,336 |
| Novus release `-O3` | 117,116 | 108,160 |
| Novus `-O3 --unsafe` | 110,912 | 102,708 |

The canonical Workbench package is 117,756 bytes; the table uses direct
CLI-startup builds so the safe/unsafe comparison has identical startup metadata.

The directly comparable Novus file is 1.85 times the C file. CODE alone is 2.50 times
larger, but that comparison overstates executable-code growth because VBCC
places about 15 KiB of Novus literals and aggregate templates in CODE while the
C toolchain uses DATA sections.

Safety checks account for 6,204 file bytes, about 5% of the safe Novus binary;
they are not the primary gap. Map and generated-C inspection instead finds the
largest excess in RDB serialization/parsing and UI setup. A single safe
`ByteWriter::write_be_u32(...)?` expands through `Result`, bounds, slice, and
four byte-write paths. The C version validates its 512-byte block once and then
uses direct stores. Novus IR optimization levels 2 and 3 are not yet usable as
a remedy: the inliner cannot clone general IR, and the level-2 passes can delete
still-live enum, try-propagation, and indexed-field temporaries. VBCC `-O3`
whole-program optimization is correct, reduces this application by 21,208
bytes, and an unchanged rebuild takes about 0.2 seconds.

The next size work should therefore fix and validate the general IR optimizer,
then teach checked byte access to eliminate dominated checks. Removing safety
or hand-writing unsafe RDB code would hide the compiler problem rather than fix
it.

## Cohesion rules

New functionality must extend Novus's existing shape rather than introduce an
Amiga-only dialect:

- Fallible operations return `Result`; absence and unsupported data use
  `Option`; cleanup uses `Drop`.
- Owned OS resources remain move-only handles. Ownership transfer uses the
  existing `consuming self` convention.
- Collections accept `Slice` and expose normal iterators rather than pointer
  plus count pairs.
- Simple common cases get constructors; optional configuration uses the
  existing fluent builder pattern; fixed UI descriptions use the existing
  static-data pattern.
- `From`, `Into`, `AsRef`, and `AsMut` provide ordinary value/view conversion
  where ownership is not involved.
- New syntax is reserved for a general language capability that libraries
  cannot express. HDPart alone is not justification for a one-off construct.

Accordingly, the speculative `window` DSL is rejected. GadTools should grow by
extending `StaticGadget`, `StaticGadToolsUi`, and `GadToolsBuilder`, while the
window they produce composes with the existing `WindowHandle`.

## Interoperability contract

Every owning wrapper should support the same three operations already used by
`WindowHandle`, `ScreenHandle`, `BitMapHandle`, `MsgPortHandle`, and DOS file
handles:

| Operation | Meaning | Ownership after the call |
|---|---|---|
| `handle()` | Borrow the lower-layer/native handle for an advanced call | High-level owner remains valid and continues cleanup. |
| `into_raw(consuming self)` | Transfer the complete lower-layer state out | High-level owner is moved and cannot clean up or be reused. |
| `from_raw(...)` | Adopt explicitly owned lower-layer state | High-level wrapper becomes responsible for cleanup. |

For objects whose state is larger than one pointer, `into_raw` returns a typed
raw-state value, not an unstructured tuple. For example, `RawBlockDevice`
retains the request, port, open state, and sector size needed to reconstruct a
`BlockDevice` safely. Borrowing must be the common fallback so one special
command does not force the rest of the program down to FFI.

Interop is required at every adjacent boundary, not just at the bottom:

| Boundary | Borrow downward without changing ownership |
|---|---|
| `BlockDevice` → `DeviceRequest` | `request()` |
| `DeviceRequest` → `*IORequest` | `handle()` |
| `GadToolsWindow` → `WindowHandle` | `window()` |
| `WindowHandle` → `*Window` | `handle()` |
| typed gadget → `*Gadget` | `handle()` |

Ownership-transfer APIs are added only when a real lower-level API consumes
the resource. If a wrapper owns several cooperating native resources, its
`into_raw` returns one named raw-state value that preserves them all, and
`from_raw` validates that state with `Result`. We should not manufacture a
second family of `parts` or adapter objects merely for theoretical symmetry.
Existing methods with different names should receive compatibility aliases;
normalizing the layers must not break applications already using them.

```novus
let var disk = BlockDevice::open("scsi.device", 0)?
let geometry = disk.geometry()?

// One controller-specific operation at the systems layer.
custom_scsi_command(disk.request().handle(), command)?

// The same high-level object is still owned and usable.
let rdb = disk.read_rdb()?
```

The same rule applies upward as well: `GadToolsWindow` should expose its
`WindowHandle`; a typed gadget should expose its `Gadget` handle; `Block<N>`
should provide `Slice<u8>` / mutable views; and DOS device entries should
provide typed `BPtr<T>` views. Layers share types instead of copying data into
parallel, incompatible object models.

Interop tests must prove that a borrowed raw call can return to the high-level
API, and that an `into_raw` / `from_raw` round trip neither leaks nor double
frees the resource.

## Libraries to add or extend

This work belongs primarily in reusable libraries:

| Module | Responsibility |
|---|---|
| `std::os::device` | Generic owning Exec device/request lifecycle and typed command execution. |
| `std::os::block_device` | Geometry, readiness, block I/O, SCSI passthrough, and discovery built on `std::os::device`. |
| `std::os::doslist` | Locked DOS-list iterator and typed mounted-device/startup views. |
| `std::os::bptr` | Typed BCPL pointers and checked BSTR/native conversions. |
| `std::memory::bytes` | Bounds-checked endian readers/writers over owner-tied slices. |
| `std::collections::arrayvec` | Inline bounded collection using existing collection/iterator conventions. |
| `std::strings::fixed` | Generic `FixedString<N>` and `FixedCString<N>` replacing capacity-specific copies. |
| `std::ui::gadtools` | More control definitions, typed state handles, events, own/public-screen selection, and access to the underlying `WindowHandle`. |
| `std::ui::asl` | Owning requesters returning existing string/path types. |

The device and DOS libraries are Amiga-specific and therefore stay under
`std::os`; byte, collection, and string facilities are general-purpose. The
existing raw bindings remain in `std::ffi` as the bottom layer.

## The application-facing API we should make possible

This intended end state follows the same static description and ordinary event
loop already used by Novus's idiomatic GadTools example. The current application
implements the workflow with the dynamic builder; the remaining blockers above
prevent this final static form and the upstream custom disk map.

```novus
static GADGETS: [StaticGadget] = [
    StaticGadget::cycle(DISK, "Device", (78, 22, 228, 14)),
    StaticGadget::list_view(PARTITIONS, (8, 70, 304, 82)),
    StaticGadget::string(NAME, "Name", (54, 158, 92, 14), max: 31),
    StaticGadget::integer(SIZE, "Size MB", (220, 158, 62, 14), 0, 6),
    StaticGadget::button(SAVE, "Save changes", (192, 178, 120, 14)),
]

static UI = StaticGadToolsUi::new(
    "HDPart",
    (0, 11, 320, 200),
    GADGETS,
    MENUS,
)

fn run() -> Result<(), HdPartError> {
    let disks = BlockDevices::discover()?
    var ui = UI.open(ScreenTarget::WorkbenchOrOwn)?

    for event in ui.events() {
        match event? {
            HdPartEvent::SelectDisk(index) => {
                let var device = disks.open(index)?
                let model = device.read_rdb()?
                ui.show_disk(device.info(), model)?
            },
            HdPartEvent::Save(model, disk) => {
                disk.require_unmounted()?
                disk.write_rdb(model)?
            },
            HdPartEvent::ImportFilesystem(path) => ui.import_filesystem(path)?,
            HdPartEvent::Quit => break,
            _ => {},
        }
    }
    return Result::Ok(())
}
```

There is no `*`, `&`, numeric `BPTR`, tag list, manual message reply, or cleanup
sequence in the application. Advanced users can still reach the existing FFI.
The wrappers should lower to those same calls and remain zero-cost after
link-time dead-code elimination.

## Proposed order, with acceptance tests

1. Fix and validate higher-level IR optimization, then remove dominated checked
   byte-access paths and remeasure the complete application.
2. Add **UI-02** and **UI-03** to reproduce own-screen startup and the upstream
   custom disk map without exposing raw drawing or input APIs.

## Reproduce

HDPart uses the Novus test runner. The project command runs the fast,
non-destructive suite; live A4000 and destructive suites are explicit.

```sh
novus test . --release --run
novus test tests/a4000 --release
novus test destructive-tests/a4000/rdb_write_test.novus --release
novus test destructive-tests/a4000/rdb_lifecycle_test.novus --release
# Cold-restart the disposable A4000 before running rdb_remount_test.novus.
```

The destructive suite refuses disks that are not the exact 64 MiB, 512-byte
block disposable HDF. Build the application separately with `novus build --release`.
