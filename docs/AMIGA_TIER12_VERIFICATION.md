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
imports the same module without calling it. All 1,597 callables have a positive
delta: 32–29,456 bytes, median 576 bytes, with aggregate deltas of 3,518,880
bytes. The larger deltas are expected for high-level entry points that pull in
owned error handling and lower-tier implementation code.

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
