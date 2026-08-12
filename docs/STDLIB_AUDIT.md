# Novus standard-library error contract

**Updated:** 2026-08-11
**Target:** AmigaOS 3.1/3.2 on 68020 or newer

Novus uses `Result<T, E>` when an operation can fail and the caller needs the reason.
`Option<T>` is reserved for ordinary absence: an empty iterator, a missing map key, or
a nonblocking receive with no message ready. Raw `std::ffi` declarations preserve the
Amiga ABI; safe wrappers translate its null pointers, negative values, and status codes
at that boundary.

## Enforced rules

- A `Result` expression cannot be ignored. Handle it, propagate it with `?`, or discard
  it explicitly with `let _ = ...`.
- `?` is valid only inside a function returning `Result` and runs normal `Drop` cleanup
  before returning `Err`.
- Matching an owned `Result` transfers each owned payload exactly once. Matching a
  borrowed enum with owned payloads is rejected until Novus has borrow-pattern syntax.
- `main() -> Result<(), E>` is supported when `E` implements `Error`. `Err` displays the
  Novus program-failure requester and returns AmigaDOS failure code 20.
- Every public standard-library type named `*Error` implements `std::core::Error`.

The compiler and language server report the same diagnostics for these rules.

## Audited fallible surfaces

| Area | Result contract |
|---|---|
| DOS files | open, read, write, and seek return `DosError` |
| Heap memory | blocks, allocations, boxes, and `MemHandle` return `ExecError` |
| Chip memory | buffers and pools return `ChipCacheError` |
| Collections | allocation, capacity, and bounds failures return typed errors |
| Channels | setup, send, handoff, and one-shot state failures return `ChannelError` |
| Async timers | setup and device failures return `TimerError`; failure is never readiness |
| FFP setup | library-open failures return `FfpError` |
| Networking and prefs | `AddrParseError`, `NetError`, and `PrefsError` implement `Error` |

## Verification

Regression coverage includes compiler and LSP negative tests, `Drop` on `?`, generated
`Result` entry-point handling, deterministic Amiga failure paths, public error-contract
scanning, and a 3,072-byte release gate for the idiomatic GUI example. The runtime
failure suites are executed on the A4000 FS-UAE target, where structured guest
diagnostics also detect alerts, CPU exceptions, and Guru Meditations.

## Review checklist

When adding a wrapper:

1. Keep ABI sentinels inside `std::ffi` or the wrapper implementation.
2. Return the narrowest existing error enum; add a variant only when callers can act on
   the distinction.
3. Use `Option` only if absence is a successful state.
4. Add one success-path and one failure-path test; use the A4000 suite for behavior that
   depends on AmigaOS.
5. Reuse the existing ownership shape: `handle()` borrows, `into_raw(consuming self)`
   transfers, and `from_raw(...)` adopts. Do not invent synonymous methods.
6. Expose a borrowed path to the next lower API layer when advanced operations are
   plausible, and test that callers can return to the high-level API afterward.
