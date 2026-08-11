# Novus compiler review

**Updated:** 2026-08-09
**Supported processors:** 68020, 68030, 68040, 68060, 68080

This is the current review baseline. Older line-by-line reviews described code and
target modes that no longer exist; git history remains the source for those snapshots.

## Current architecture

Novus parses source into a typed IR, applies target-aware optimization, emits C or
direct m68k code, and links Amiga Hunk binaries. The production path uses the vendored
VBCC toolchain. Successful-build stamps and the object cache make an unchanged rebuild
a no-op before parsing or invoking VBCC.

The language provides the ordinary systems-programming surface required by Amiga code:
strong integer and fixed-point types, aggregates and native unions, enums and pattern
matching, generics and traits, ownership and deterministic `Drop`, pointers and unsafe
blocks, closures, async functions, inline assembly, volatile memory, and function
pointers with explicit Amiga register bindings.

The Amiga project generators own the boilerplate that is easy to get subtly wrong:

- libraries: resident/autoinit data, vectors, A6 thunks, lifecycle, delayed expunge;
- devices: lifecycle, units, quick and deferred BeginIO, AbortIO ownership;
- resources: resident/autoinit data, permanent state, vectors, `OpenResource` binding;
- handlers: DOS startup and packet ownership with exactly-once replies.

## Closed audit gaps

| Area | Current result | Regression coverage |
|---|---|---|
| Exec/NDK callbacks | Native `amiga fn(... in a0) -> ... in d0` syntax | parser, semantic, C codegen, A4000 |
| Overlaid ABI records | Native `union`; one active initializer; unsafe field access | layout, diagnostics, C, A4000 |
| MMIO/shared memory | `read_volatile`, `write_volatile`, `memory_fence` | safety diagnostics, C, A4000 |
| Interrupt entry | `@interrupt` for Exec RTS entries; `@interrupt_vector` for raw RTE vectors | validation, C attributes, A4000 RTS path |
| Shared components | Native library, device, resource, and handler projects | generator tests and real template builds |
| NDK versioning | Reached FFI functions select the required OpenLibrary version | SFD metadata tests |
| Processor floor | 68020 is the only baseline; pre-68020 selections are rejected | CLI, project schema, preprocessor, backend tests |

## Remaining boundaries

- Copper, blitter, sprite, and asset DSL sections are design work, not required language
  primitives. Current applications can express the same operations with typed structs,
  volatile memory, NDK calls, and inline assembly.
- The direct m68k backend is not yet the default production backend; VBCC remains the
  compatibility reference.
- Native code can still corrupt memory inside `unsafe`. Emulator diagnostics catch CPU
  exceptions and alerts, but no language runtime can convert arbitrary native corruption
  into a guaranteed recoverable Novus dialog.

## Verification baseline

The host solution tests cover syntax, type checking, lowering, generators, toolchain
configuration, and templates. `tools/amiga/run_runtime_suite.py` runs the foundational
language aggregate on an A4000 in debug, release O1, and release O3 profiles, with
structured emulator diagnostics for failures, alerts, and Guru Meditations.
