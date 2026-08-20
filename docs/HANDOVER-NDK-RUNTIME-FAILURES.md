# Resolved — the 8 remaining Amiga runtime failures

All eight suites named in the previous handover now pass on the live A4000, with
`--benchmark --memory-check`. This document records what each failure actually was, because
three of them were compiler bugs that had nothing to do with the suites they showed up in.

Everything below was measured on the live machine, not inferred.

---

## Verified environment

| Fact | Value |
| --- | --- |
| Machine | A4000/040, headless FS-UAE via MCP at `http://localhost:6800/mcp` |
| Exec | `exec.library 47.13` |
| Boot volume | `SYS:` → `/Users/barry/Emulation/Amiga/A4000-DH0` (host directory, **not** a real FFS partition) |
| Disposable partition | `/private/tmp/novus-ndk-destructive.hdf`, 64 MB, attached as DH1, volume `NOVUSNDK` |

The disposable partition persists between runs. Its volume label is `NOVUSNDK`, which matters —
see the assign-name collision below. Two suites failed only when it was attached, so run the
Amiga layer with `--hdf` or those failures stay hidden.

## Reproducing the run

```sh
python3 tools/amiga/run_runtime_suite.py \
  --configuration A4000 \
  --build-dir .novus-cache/ndk-triage \
  --suite ndk-battclock-resource --suite ndk-nonvolatile --suite ndk-ramdrive \
  --suite ndk-dos-device-proc --suite ndk-dos-filesystem-control \
  --suite filesystem-registry --suite hdpart-format-handler --suite hdpart-ui-controls \
  --profile release-o1 --benchmark --memory-check \
  --hdf /private/tmp/novus-ndk-destructive.hdf --hdf-drive 1 --nonvolatile-volume NDK0
```

→ **16 passed records, 0 failed.**

The whole layer is clean too:

```sh
python3 tools/amiga/run_runtime_suite.py \
  --configuration A4000 --layer amiga \
  --build-dir .novus-cache/ndk-layer \
  --profile release-o1 --benchmark --memory-check \
  --hdf /private/tmp/novus-ndk-destructive.hdf --hdf-drive 1 --nonvolatile-volume NDK0
```

→ **292 passed records, 0 failed** (previously 279/292).

The runner refuses to take over a running emulator. Shut one down before starting a run.

---

# Compiler bugs

These three were found through the failing suites but are general defects. Each was isolated
with a throwaway probe suite that measured `AvailMem` around one construct at a time, then
reproduced on the host by reading the generated C — which is far faster than reasoning about it.

## 1. `let ... else` discarded a wildcard payload without dropping it

```novus
let Result::Ok(_) = load() else { return }   // leaked the payload, every time
```

`BindPatternData` in `IrBuilder.Statements.cs` had a wildcard arm whose entire body was the
comment *"Wildcard — don't bind anything"*. The payload still moves out of the enum, so with no
owner it was simply abandoned. `match` handled this correctly (`unboundDropPayloads`); only
`let ... else` did not.

Measured: 30 472 bytes leaked per call — exactly the size of `L:FastFileSystem`, the segment
`hdpart-format-handler` was resolving.

Fixed by giving the payload a temporary owner and injecting the automatic drop, which is what
the `match` path already does. Borrowed enums are excluded, so `let Some(_) = &option` still
drops nothing.

## 2. Imported private enums arrived with no variants, so structural drops vanished

A public type whose cleanup is purely structural — no explicit `Drop`, just droppable fields —
was never dropped when used from another module.

```novus
pub struct FileSystem { source: FileSystemSource }   // FileSystemSource is private
```

`FillEnumVariantsForImport` filled the symbol-table entry for every enum in an imported module,
but skipped registration entirely when the entry was already filled. A nested import could fill
the shared symbol while leaving *this* module's enum table without a copy. Drop analysis
resolves layouts through the module, so `FileSystemSource` read back with zero variants —
indistinguishable from an enum with nothing to clean up.

Two independent gaps, both fixed:

- **The import**: registration now also runs when the module table has no usable copy, not only
  when the symbol is unfilled.
- **The analysis**: `IrModule` gained `ResolveStructLayout`/`ResolveEnumLayout`, mirroring the
  pattern the C backend already used for structs. `TypeImplementsDrop`, `EnumNeedsDrop`,
  `EnsureDropMethodInstantiated` and the backend's enum walks now resolve a layout before
  reading it, so a stale stub captured before its declaration was filled can no longer be
  mistaken for a type with no fields.

The second fix is the load-bearing one: an empty layout must never be read as "nothing to drop".

## 3. Moving a consuming parameter into a struct field did not end its ownership

This is the `hdpart-ui-controls` double free — `AN_FreeTwice` / `AN_MemCorrupt`, alert codes
varying between runs because it was heap corruption.

```novus
fn build_static(&var self, ..., consuming menu_strip: GadToolsMenuStrip) -> ... {
    let result = GadToolsWindow { ..., menu_strip: Option::Some(menu_strip) }
    return Result::Ok(result)
}
```

The C backend deactivates a moved variable's drop only when scanning the **return** expression.
`menu_strip` was moved earlier, into a local aggregate, so nothing marked it moved:

```c
result.menu_strip = (Option_GadToolsMenuStrip){ ... ._0 = *menu_strip };
/* no deactivation */
...
if (_defer_1_active) GadToolsMenuStrip_Drop_drop(menu_strip);   /* caller owns this now */
```

The window returned to the caller held the freed menu, visual info and label allocations; the
caller's own `Drop` freed them a second time.

The move-tracking machinery already existed (`EmitMovedSourceZero`, `DeactivateMovedVariableDrop`)
— the struct-literal path just never reached it, because a field initialised with an *enum*
literal was emitted as one compound literal. Fixed by emitting such a field field-by-field, and
by having the nested-enum helper zero/deactivate its moved payload source like its sibling
branches already did. The generated C now clears `_defer_1_active` immediately after the
transfer, on every path.

**Note for future triage:** the loop in `ui_controls_test.novus` is a red herring. Cutting it
from 256 iterations to 0 still crashed; the fault was in construction and teardown.

---

# Machine behaviour, recorded rather than asserted away

## `ndk-battclock-resource`

FS-UAE serves `ReadBattClock` from the host clock and **discards writes**. Measured: writing
`original - 86400` and reading straight back returns `original` unchanged, and `ResetBattClock`
leaves the same live value instead of the 01-Jan-1978 epoch.

Following the `ndk-exec-avl` precedent, the tests now assert what this machine actually does —
the entry points are callable and leave the clock intact — and the bindings carry a `# Bugs`
section recording that emulated battery-clock writes are not observable. Real hardware stores
both; that difference is stated in the note.

## `ndk-nonvolatile`

`GetNVInfo` describes a *hardware* nonvolatile medium, and this machine has none. It returns a
valid owned `NVInfo` whose `nvi_MaxStorage` and `nvi_FreeStorage` both read zero, for either
`killRequesters` value. Every other entry point works, because the library falls back to the
NVDISK location in `ENV:Sys/nv_location`, which the runner provisions on `NDK0:`.

The test now asserts that response — an owned block reporting no medium, with the
free-within-capacity invariant still checked — and the binding records it under `# Bugs`.

## `filesystem-registry` — this one was a real stdlib bug

The previous handover guessed the registry had no `DOS\0` entry. It does. Enumerating
`FileSystem.resource` on the live machine shows 9 entries: `DOS\0` through `DOS\7` plus
`UNI\1`.

`registry.find($444F5300)` returned `None` because `BPtr::is_addressable` decides addressability
with `TypeOfMem`, which only answers for memory on Exec's free lists. The `DOS\0` handler's
seglist lives at `$00F9FB8C` — in Kickstart ROM — so a perfectly readable ROM address was
reported as unaddressable and `snapshot()` discarded the entry.

Fixed in `BPtr::is_addressable`: ROM is readable. The ranges used are the ones exec's own
documentation names for ROMTags, `$F80000-$FFFFFF` and `$F00000-$F7FFFF`. Without this, the
stdlib could not see any ROM-resident filesystem handler.

---

# Test and fixture defects

## `ndk-dos-filesystem-control` and `ndk-ramdrive` — `Inhibit` is refused while a handler drains

`Inhibit` works fine on this machine. What the tests hit is that a handler which has just
serviced a write still has outstanding work, and `ACTION_INHIBIT` is refused with
`ERROR_OBJECT_IN_USE` (202) until it settles. Measured: refused immediately after a write,
accepted after a short `Delay`. Both suites wrote a file and inhibited in the next breath.

Both now use an `inhibit_when_idle` helper that retries only on `ERROR_OBJECT_IN_USE`, with a
bounded number of short delays, and reports every other error unchanged. The assertions are
untouched — inhibited still has to block a new root lock, and resuming still has to restore it.

The stdlib's `MountedFileSystem::inhibit()` was deliberately left alone: it returns
`InhibitFailed(IoErr())`, so a caller can already see this condition and decide. Burying a retry
inside it would hide a real state from applications.

`dos_format`'s failure was the same cause seen through `--memory-check`: the first run passed and
the rerun hit the busy handler.

## `ndk-ramdrive` — one unit cannot serve two destructive tests

`KillRAD` and `KillRAD0` both tear a unit down permanently. Measured: after `KillRAD(0)`, `RAD:`
is gone for good in that session — `Lock` and `Open` return `ERROR_NO_DISK` (225), and
`C:Mount RAD:` is refused because the DOS device node survives. So the second test could never
get a live unit.

The stock `Storage:DOSDrivers/RAD` entry documents extra units as copies carrying a different
`Unit` value. The runner now builds that copy on the guest and mounts it as `RAD1:`, so
`KillRAD` owns unit one and `KillRAD0` owns unit zero. Each returns its own exact device name —
verified: `"RAD1:"` and `"RAD:"`.

The fixture also touches both volumes after mounting. `Mount` only adds the DOS entry; the unit
claims its ~880 KB on first access. Without the touch that allocation landed inside the measured
window, the test looked like it leaked a unit's worth of RAM, and `--memory-check` reran it
against a unit it had already killed.

## `ndk-dos-device-proc` and `ndk-dos-assigns` — the assign name collided with the volume

Both suites built assigns named `NOVUSNDK`. That is also the volume label of the disposable NDK0
partition, so the assign was refused whenever that partition was attached: each suite passed
without `--hdf` and failed with it. This is why the previous handover recorded `ndk-dos-assigns`
as fixed — it had been measured without the partition.

Renamed to `NOVUSDEVPROC` and `NOVUSASSIGN`, each with a comment naming the collision. The
remaining `NOVUSNDK` references in `ndk_dos_filesystem_control.novus` are correct: that suite
owns the label.

---

## Gates — all green after the changes

`Novus.Tests/DropOwnershipRegressionTests.cs` locks in the three compiler fixes by asserting on
the generated C, since every one of them produced wrong cleanup rather than a compile error.

```sh
dotnet test Novus.Tests/Novus.Tests.csproj -c Debug     # 3225 passed, 0 failed, 3 skipped
dotnet Novus/bin/Debug/net10.0/Novus.dll verify-ndk     # 9563 symbols, 112 interfaces
python3 -m unittest discover -s tools -p 'test_ndk_*.py' # 10 tests, OK
python3 tools/verify_stdlib_tests.py --allow-missing     # rc 0
python3 tools/verify_amiga_tiers.py                      # rc 0
```

## Technique worth keeping

For a leak or a double free, do not reason about the stdlib — measure one construct at a time.
A throwaway suite of single-purpose `@test`s, each sampling `AvailMem` around one expression,
localised two compiler bugs in minutes. Once a case is isolated, reproduce it on the host with
`novus compile` and read the generated C under `.novus-cache/build/<name>/`; the drop calls and
the `_defer_N_active` flags are all right there, and the loop is seconds instead of a machine
boot. Run individual tests with `--filter <fn_name>` — output is lost when a run gurus, so
bisect by running one test per binary rather than relying on prints.
