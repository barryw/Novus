# Amiga Tier 1 and Tier 2 verification

This is the reproducible verification record for the Novus-authored Amiga
application layer (`amiga::`, Tier 1) and systems layer (`amiga::sys`, Tier 2).
The raw NDK bindings are recorded separately in
[NDK_VERIFICATION.md](NDK_VERIFICATION.md).

## Current result

| Evidence | A1200 | A4000 |
| --- | ---: | ---: |
| Public callables | 1,597 | 1,597 |
| Documented | 1,597 | 1,597 |
| Behavior mapped | 1,597 | 1,597 |
| Compile/link verified | 1,597 | 1,597 |
| Runtime verified | 1,597 | 1,597 |
| Size measured | 1,597 | 1,597 |
| Leak verified | 1,510 | 1,510 |

Tier 1 is complete across all six evidence dimensions: 240/240. Tier 2 has
1,270/1,357 complete callables. The remaining 87 are the MUI module: all 87
compile, link, execute, and have size and benchmark evidence on both machines,
but the installed MUI 5 runtime retains AmigaOS memory after application and
requester disposal. That unresolved platform/runtime result is detailed below;
it is not counted as leak verified.

## Machines and execution

Both configurations ran through the FS-UAE MCP service at
`http://localhost:6800/mcp` using `release-o1` binaries with guest benchmarks.
All non-MUI suites were allocation- and AmigaOS-memory checked. MUI behavior
was run separately because its leak gate is intentionally still red.

| Configuration | CPU | Chipset | Result |
| --- | --- | --- | --- |
| A1200 | 68020 | AGA | all behavior passed; 1,510/1,597 leak verified |
| A4000 | 68040 | AGA | all behavior passed; 1,510/1,597 leak verified |

The A1200 image originally lacked MUI. It now uses the same installed MUI
21.227 (2021-08-31) files as A4000, with `MUI:` assigned to its MUI directory
and `MUI:Libs` added to `LIBS:`. This is required for the external MUI classes;
copying only `muimaster.library` produces an AmigaDOS “insert volume MUI”
requester rather than the intended modal MUI test.

## Speed and size

Every one of the 210 behavior tests mapped to Tier 1/2 callables emits a guest
benchmark. Of those callables, 73 have an exclusive one-callable test and an
individually attributable speed value. The rest execute in grouped contract
tests; their test timings are retained without being mislabeled as isolated
microbenchmarks.

| Guest benchmark metric | A1200 | A4000 |
| --- | ---: | ---: |
| Mapped tests | 210 | 210 |
| Guest time total | 117.771 s | 40.432 s |
| Test median | 7,568 µs | 1,811 µs |
| Test range | 218 µs–27.626 s | 0 µs–7.834 s |
| Exclusively timed callables | 73 | 73 |

Callable size probes link one callable at a time and subtract a baseline that
imports the same module without calling it. The initial full-campaign snapshot
measured all 1,597 callables: 32–29,456 bytes, median 576 bytes, with aggregate
deltas of 3,518,880 bytes. These deltas deliberately double-count shared code
and are useful for finding dependency fan-out; they are not library file sizes.
The targeted final probes below supersede that snapshot for the optimized
modules.

### Optimization audit (2026-08-22)

The Tier 1–3 size and guest-timing evidence was ranked before changing code.
Tier 3 needs no general size rewrite: its 1,397 raw NDK thunks have a 132-byte
median and a 1,368-byte maximum. The longest guest timings in all tiers belong
to modal requesters, viewers, filesystem operations, and deliberate waits;
those measurements describe OS service time rather than wrapper CPU overhead.

The actionable bloat was in `ModPlayer`, `AudioChannel`, `ChipCache`, and
`ChipPool`. Both hardware destructors referenced the complete cache and pool
implementation. `ModPlayer` now links cache cleanup only when `init_asset`
installs it. `AudioChannel::play_asset` owns its one required chip allocation
directly instead of generating a cache key that could never be reused. Cache
entries no longer retain unused source pointers, debug names, or redundant
allocation flags, and each Exec pool is created only when its size class is
first used.

All 132 affected callable probes compile and link. The current probes compare
as follows; the cache/pool “before” values are the original campaign reference:

| Module | Improved callables | Regressed | Probe-delta sum before | After |
| --- | ---: | ---: | ---: | ---: |
| `hardware::ptplayer` | 33 / 45 | 0 | 808,656 B | 356,884 B |
| `hardware::audio` | 19 / 66 | 0 | 357,396 B | 76,440 B |
| `memory::chip_cache` | 12 / 12 | 0 | 141,496 B | 71,144 B |
| `memory::chip_pool` | 5 / 9 (4 unchanged) | 0 | 19,396 B | 13,136 B |

The sums compare independently linked probes and are not a combined-library
file size. Representative deltas are `ModPlayer::new` 24,196→9,844 bytes,
`ModPlayer::play` 24,724→10,212, `AudioChannel::acquire` 15,224→672,
`AudioChannel::play_asset` 20,868→1,892, and `AudioChannel::stop`
15,252→700. Cache-aware entry points retain the implementation they need.

Actual final executable sizes provide the non-additive view:

| Program | Bytes |
| --- | ---: |
| Minimal `ModPlayer::new` program | 10,688 |
| Minimal `AudioChannel::acquire` program | 1,516 |
| Generated program calling all 45 PTPlayer APIs once | 45,508 |
| Generated program calling all 66 hardware-audio APIs once | 25,912 |
| 20-second headed MOD demo | 432,388 |
| Embedded `GSLINGER.MOD` inside that demo | 406,354 |
| Demo executable excluding the embedded MOD bytes | 26,034 |

The final `release-o1` runtime gates pass with guest benchmarks and memory
checks on both machines. The three PTPlayer tests total 1.143 ms on A4000 and
9.136 ms on A1200; the five hardware-audio tests total 3.246 ms and 15.780 ms
respectively. The same 432,388-byte demo played the complete MOD audibly for
20 seconds on headed A1200 and A4000 sessions, sequentially, and exited cleanly
on both; playback was also confirmed by the listener. The optimization removes
link-time bloat without claiming a CPU speedup for unchanged playback and OS
service paths.

## AGA audit

The campaign added and ran the AGA paths for 16/32/64-pixel regular and
attached sprites, 10-bit sprite vertical positions, copper bitplanes 6 and 7,
8-plane Intuition screens, and live 8-bit palette entry 255. Both targets pass
these checks, including invalid width, depth, plane, and expanded-row cases.
The full capability table and guards are in
[AGA_CAPABILITIES.md](AGA_CAPABILITIES.md).

## Open MUI leak gate

The public MUI contract is functionally green on both machines: object trees,
notifications, the event loop, attributes, list operations, popups, tabs,
menus, progress controls, and a real modal requester all execute. The wrapper
calls `MUI_DisposeObject` on the application, which is the cleanup required by
the [MUI primary documentation](https://github.com/amiga-mui/muidev/blob/master/files/muimaster.txt),
and frees every Novus-owned tracking allocation.

The guest OS-memory gate nevertheless observes repeatable retained memory:

- A4000: 200 bytes for a minimal application, 4,256 bytes for the complete
  object tree, and 1,136 bytes after `MUI_RequestA`.
- A1200: the independent confirmation run retains 3,632 bytes for the complete
  object tree; `MUI_RequestA` retains 1,136 bytes.
- A standalone MUI Text object and Group tree both dispose without a loss,
  isolating the behavior to `Application.mui` and requester/class state.
- Disabling the optional A4000 `MUI:PatchASL` resident did not change the
  result, so that startup patch was restored.

No allowance or tolerance is applied. The 87 MUI callables remain visibly red
for leak evidence until the installed MUI behavior can be eliminated or a
different verified MUI build is selected.

## Evidence files

The combined per-callable reports are generated at:

- `.novus-cache/tier12-evidence-a1200.json`
- `.novus-cache/tier12-evidence-a4000.json`

Runtime reports retain guest output, test timings, binary sizes, memory deltas,
and MCP diagnostics. Callable compile/size reports are under
`.novus-cache/amiga-tier-size`, `.novus-cache/tier12-size-impacted`, and
`.novus-cache/tier12-size-final`.
