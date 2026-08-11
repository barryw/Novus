# Amiga application language audit

**Updated:** 2026-08-10
**Target:** AmigaOS 3.1/3.2 on 68020 or newer

Novus can express the language and ABI mechanisms needed to build Amiga applications,
libraries, devices, resources, handlers, callback-driven APIs, and interrupt code. The
compiler generates platform boilerplate from Novus declarations instead of exposing a
second C-shaped language inside Novus.

## Matched GUI comparison

The reference programs use the same V36 Intuition/GadTools API surface to implement
the same application: identical window bounds and flags, gadget kinds and bounds,
initial values, IDCMP classes, menu structure, command key, and exit behavior. V36 is
older than AmigaOS 3.1, so every used call is present on both 3.1 and 3.2.

- [idiomatic_gui.novus](../Novus.Tests/Examples/idiomatic_gui.novus) uses only safe
  Novus APIs. It has no `std::ffi` import, raw pointer, `unsafe`, `extern`, C, or
  assembly.
- [idiomatic_gui.c](../Novus.Tests/Examples/idiomatic_gui.c) calls those V36 APIs
  directly. It does not use ReAction or a newer convenience toolkit.

| Measure | Novus | C |
|---|---:|---:|
| Nonblank, noncomment source lines | 28 | 117 |
| Application-level cleanup calls | 0 | 8 |
| Explicit failure branches before the event loop | 0 (`?` propagates them) | 8 |
| Raw message dequeue/reply protocol | hidden by typed API | manual |
| Raw pointers or pointer casts in application code | 0 | required throughout |

That is 70% less application code, but the stronger argument is what is absent. The
Novus program cannot forget to unlock its public screen, detach/free its menu, dispose
its gadgets, or reply to an Intuition message on an early return. Ownership and `Drop`
handle those obligations, `Result` and `?` preserve failure paths, and the event loop
matches named variants instead of coordinating class bits, message lifetimes, gadget
casts, and encoded menu numbers.

The first comparison incorrectly used ReAction on the Novus side. That was not a valid
comparison and would have imposed an AmigaOS 3.5 requirement. The corrected work closed
the actual gap in the shared UI/compiler layers:

- `StaticGadToolsUi` keeps literal descriptors and labels in static data and returns a
  zero-allocation owner for the public-screen lock, VisualInfo, gadgets, window, and
  menu. `GadToolsBuilder` remains available when controls are assembled at runtime.
- controls use `CreateContext`/`CreateGadgetA`; events use
  `GT_GetIMsg`/`GT_ReplyIMsg`, including copying the gadget ID before replying.
- the static and dynamic owners both release every partially acquired resource on an
  early return and drain or reply to Intuition messages before teardown.
- `GadToolsEvent` exposes only the close, menu, and gadget events enabled by the fixed
  GadTools IDCMP mask; it cannot carry unrelated refresh guards or keyboard events.
- `MenuSelection::matches()` keeps encoded menu-number details out of applications.
- selective wildcard imports now include same-module constant dependencies, so NDK
  constants such as `WA_*` cannot be emitted without their base constants.

Both references compile with the same vendored VBCC 68020 toolchain and were exercised
back-to-back on the same A4000/040 FS-UAE instance. Each window appeared, accepted the
injected close event, and exited with code 0. Guest diagnostics remained healthy. The
Novus dependency graph contains only `exec.library`, `dos.library`,
`intuition.library`, and `gadtools.library`; no ReAction class libraries are linked.

The default release binary is currently **6,228 bytes**, down from 13,264 bytes for
the original dynamic-builder version; the direct C reference is 2,444 bytes. This is
still above the 3,072-byte acceptance gate. The remaining dominant block is generic
compiler work: specialize immutable static arguments across module boundaries, fold
their enum matches and bounded loops, and share RAII cleanup epilogues instead of
emitting flattened copies. The dynamic builder will not be removed or weakened to
meet the static-program size target.

## Capability matrix

| Requirement | Novus surface | Verification |
|---|---|---|
| Data layout | structs, `@packed`, alignment, `sizeof`, `offsetof`, native `union` | semantic/layout/codegen tests; A4000 union overlay |
| Raw memory | pointers, references, unsafe blocks, volatile reads/writes, memory fence | diagnostics/codegen tests; A4000 round trip |
| Amiga callbacks | `amiga fn` parameters bound with `in d0`…`in a3`, return bound with `in d0` | parser/type/codegen tests; A4000 indirect call |
| Interrupts | `@interrupt` for Exec interrupt servers; `@interrupt_vector` for raw vectors | safety/codegen tests; A4000 Exec entry call |
| NDK APIs | generated SFD bindings, nested C aggregates, caller-supplied bases, reached-function minimum versions | FFI generator and metadata tests |
| AmigaOS 3.1 UI | owning V36 Intuition/GadTools windows, controls, menus, and typed events | matched C/Novus programs; A4000 run |
| Libraries | `@library`, public impl methods, `@libinit/open/close/expunge` | generator tests; library and client template build |
| Devices | `@device`, `@devicecmd`, `@deviceinit/open/close/expunge`, `@abortio` | generator tests; device and client template build |
| Deferred I/O | `@devicecmd(... deferred = true)` transfers request ownership until reply or abort | generator tests and documented template path |
| Resources | `@resource`, `@resourcefunc`, `@resourceinit` | generator tests and real resource link |
| DOS handlers | normal `main` plus owning `Packet` wrapper | handler template build; exactly-once reply behavior |
| Concurrency | async functions, futures, tasks, ports, messages, signals, channels | host tests and foundational A4000 aggregate |
| Core language | integers, fixed point, arrays, tuples, enums, patterns, generics, traits, closures, ownership, Drop, errors, modules | foundational A4000 aggregate in debug/O1/O3 |
| Hardware escape hatch | typed registers, volatile memory, inline assembly and external assembly | host tests and A4000 inline-assembly cases |

## Native syntax examples

```novus
pub union MessageWord {
    raw: u32,
    halves: [u16; 2],
}

amiga fn hook_entry(hook: *Hook in a0, object: *u8 in a2,
                    message: *u8 in a1) -> u32 in d0 {
    return 0
}

type HookEntry = amiga fn(*Hook in a0, *u8 in a2, *u8 in a1) -> u32 in d0
```

```novus
unsafe {
    write_volatile(custom_register, value)
    memory_fence()
    let observed = read_volatile(custom_register)
}
```

Project-specific lifecycle syntax is shown by `novus new library`, `device`,
`resource`, and `handler`. Those templates are built in regression tests.

## Deliberate boundaries

- Copper/blitter/asset DSLs remain optional ergonomics. Typed memory, NDK calls, and
  assembly already expose the underlying machine without blocking applications.
- `unsafe` permits native memory corruption by design. The emulator harness reports
  structured CPU exceptions and alerts, but recovery from arbitrary corruption is not a
  language guarantee.
- All output targets are 68020+. Compatibility modes for earlier processors do not
  exist in compiler options, project schemas, preprocessors, or runtime selection.
