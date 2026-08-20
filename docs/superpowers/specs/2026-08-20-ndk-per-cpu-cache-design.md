# Per-CPU NDK build cache

Build the full Novus NDK — tiers 1, 2 and 3 — once per target CPU into a persistent
user-level cache, and reuse it when compiling applications for that CPU.

## Problem

A per-CPU stdlib cache already exists and already works. It is keyed correctly:

```
<compilerDir>/stdlib/<cpu>/<mode>/<fpu>-O<opt>-S<safety>/<abi-hash>/
```

Two things are wrong with it.

**It does not stay put.** `<compilerDir>` is `AppContext.BaseDirectory`, which is
`Novus/bin/Debug/net10.0/`. A `dotnet build` can delete it, and the cache key embeds the
compiler assembly's `ModuleVersionId`, which changes on *every* rebuild even when the source is
identical. Rebuilding the compiler to run a unit test therefore throws away every cached NDK.
Measured during the runtime-failure work: a warm Amiga layer sweep builds 146 suites at ~0.3 s
each; the same sweep after a compiler rebuild takes ~7 s each, turning a 15-minute run into
nearly an hour.

**There is no front door.** The cache is only ever populated lazily, as a side effect of
compiling an application, so it holds whatever that application happened to import. There is no
way to say "build me a complete 68040 NDK", no way to force a rebuild, and no scoped purge.
`stdlib-build` looks like that command but is a stub: it writes an empty manifest, copies stdlib
sources into the compiler directory, and compiles nothing. Its own source says so —
*"we'll just create an empty manifest to mark that this target has been built"*.

## Goals

- One command builds the complete NDK (everything under `Novus/std`, all three tiers) for a
  given CPU.
- Built objects survive compiler rebuilds and are shared across every project on the machine.
- Application compiles for that CPU reuse the build automatically.
- Explicit overwrite and scoped purge.
- A stale cache is never linked.

## Non-goals

- Prebuilding every variant combination. `fpu × opt × safety × mode` is ~128 per CPU; the
  command builds the two that matter and leaves the rest to the existing lazy path.
- Breaking the stdlib's circular imports.
- Migrating the old cache. It lives in `bin/` and is disposable.

## Design

### Cache location

Root moves to a user-level home, key structure unchanged:

```
~/.novus/ndk/<cpu>/<mode>/<fpu>-O<opt>-S<safety>/<abi-hash>/
    *.o
    manifest.json
    novus_types.h.hash
```

`NOVUS_NDK_CACHE` overrides the root so CI and tests never touch a developer's real cache.
Both the explicit build command and the lazy compile path write here, so there is exactly one
cache.

### Command surface

`stdlib-build` is rewritten in place rather than adding a parallel command:

```
novus stdlib-build [--cpu 68020|68030|68040|68060|all] [--mode debug|release|both]
                   [--overwrite] [--purge] [-v]
```

Defaults stay `--cpu all --mode both`. Each `(cpu, mode)` pair produces exactly the variant a
plain `novus compile` uses for that mode. Today's compiler defaults are `--fpu auto`,
`-O 1`, and `--safety-level` 2 for debug / 1 for release, so the two variant directories are:

| Mode | Variant directory |
| --- | --- |
| debug | `auto-O1-S2` |
| release | `auto-O1-S1` |

These are read from the same defaults `compile` uses rather than hardcoded, so the two stay in
step if a default changes. Anything else — `-O3`, `--safety-level 3`, an explicit `--fpu` —
still falls back to the lazy path and caches itself on first use.

`--overwrite` rebuilds even when the cache is valid. `--purge` deletes the entries matching the
`--cpu`/`--mode` selection and exits.

### How a build runs

For each `(cpu, mode)`:

1. Generate a synthetic root program that imports every module under `Novus/std`.
2. Compile it through the normal pipeline, stopping after object generation — no link, since
   only the `.o` files are wanted.
3. Harvest the stdlib objects into the cache directory and write the manifest.

Circular imports between stdlib modules are irrelevant here because the compiler resolves the
whole graph in a single pass, exactly as it does for any application today. This is also why
per-module compilation is not an option: `error.novus` imports `dos.novus` and back again, and
the existing code documents that individual module compilation does not work.

A module is classified as stdlib by `sourcePath.Contains("/std/")`, which already covers all
three tiers, so no new classification is needed. Importing any symbol from a module causes the
whole module's functions to be emitted — that is how 637 objects appear from a program that
calls two functions.

**Known risk.** Novus has only `from <path> import <list>`; there is no bare module import. A
root that imports every module therefore brings hundreds of names into one scope and will
collide. The generator emits one import line per module naming a single public symbol, and
splits into multiple roots when the compiler reports a collision. The first run is expected to
surface genuine collisions in the stdlib. Those are findings to report, not to paper over.

### Reuse and invalidation

The lazy path is unchanged apart from the new root. On every compile it resolves the key —
`cpu` (`auto` → `68020`), mode, `<fpu>-O<opt>-S<safety>`, ABI hash — and links cached objects
only when the manifest's `codegenVersion` matches the running compiler **and** the stored
`novus_types.h.hash` matches. Otherwise it rebuilds and repopulates.

A stale entry is ignored, never repaired and never linked. This is deliberate and it is the one
place the implementation does not follow "cached until purged" literally: objects built by a
different codegen or against a different struct layout would link cleanly and fail at runtime.
The compiler-drop defects fixed on 2026-08-19 were exactly that class of silent wrong-code bug,
and they cost a day to find.

`--rebuild-stdlib-cache` and `--no-cache` on `compile` keep their current meanings;
`--overwrite` on `stdlib-build` is the batch equivalent.

### Purge

`stdlib-build --purge` deletes entries matching the `--cpu`/`--mode` selection, so `--purge`
with default selection clears the root. `clean` gains the new root while keeping its removal of
the old `<compilerDir>/stdlib` path, so an upgrade does not strand an orphan.

Purge only deletes inside the resolved root, does not follow symlinks, and refuses to run when
the root is neither under `$HOME/.novus` nor explicitly set through `NOVUS_NDK_CACHE`. A
mistyped environment variable must not hand a recursive delete an arbitrary directory.

## Testing

Unit tests, in `Novus.Tests`:

- Key derivation: `auto` resolves to `68020`; the variant string composes as
  `<fpu>-O<opt>-S<safety>`; `NOVUS_NDK_CACHE` overrides the root; purge selection matches the
  right subset for each `--cpu`/`--mode` combination.
- Purge guard: refuses a root that is neither under `$HOME/.novus` nor explicitly configured.

Integration tests, each pointing `NOVUS_NDK_CACHE` at a temporary directory:

- `stdlib-build --cpu 68020 --mode release` produces objects and a manifest.
- A compile using that key links from cache — asserted by the absence of the
  "Compiling stdlib modules" path, not merely by exit status.
- A compile with a deliberately different key (`--safety-level 3`) does **not** reuse the cache.
- `--overwrite` regenerates; `--purge` empties.

## Sequencing

The synthetic-root collision risk is the only real unknown, so it is settled first: generate the
root, compile it, report what breaks. If the stdlib cannot be imported wholesale, the build
mechanism changes and that must be known before the command is built around it.

1. Spike the synthetic root; report collisions.
2. Re-root the cache and teach `clean` about it.
3. Rewrite `stdlib-build` to compile and harvest.
4. Add `--overwrite` and `--purge` with the safety guard.
5. Tests.
