# Classic AmigaOS NDK verification

This is the reproducible verification record for the pinned classic 68k NDK
surface described in [NDK_COVERAGE.md](NDK_COVERAGE.md). It covers all 1,397
raw callable bindings with real AmigaOS execution, allocation-leak checking,
compile/link probes, and size and speed measurements.

## Verified machines

Both configurations ran through the FS-UAE MCP service at
`http://localhost:6800/mcp` with a disposable writable HDF mounted as `NDK0:`.

| Configuration | CPU/emulator settings | Suites | Benchmarked tests | Result |
| --- | --- | ---: | ---: | --- |
| A1200 | A1200/020, 2 MiB chip, 16 MiB Zorro III, JIT enabled | 146 | 1,543 | all passed |
| A4000 | A4000/040, 2 MiB chip, 8 MiB fast, 68040 MMU, JIT disabled | 146 | 1,543 | all passed |

The A4000 main sweep passed 145 runtime suites. The successful guest command
for `ndk-graphics-chip-revision` lost its MCP control channel during teardown;
an isolated benchmarked, leak-checked retry passed. The evidence verifier
combines that retry with the 145 uninterrupted results.

`host_integration = 1` is required for MCP guest control. The A4000 also uses
`jit_compiler = 0`: FS-UAE's JIT and MMU modes cannot be enabled together.

## Coverage result

The per-machine verifier reports the following for both A1200 and A4000:

| Evidence | Result |
| --- | ---: |
| Documented callables | 1,397 / 1,397 |
| Behavior-mapped callables | 1,397 / 1,397 |
| Side effects documented | 1,397 / 1,397 |
| Runtime verified | 1,397 / 1,397 |
| Leak verified | 1,397 / 1,397 |
| Compile/link verified | 1,397 / 1,397 |
| Size measured | 1,397 / 1,397 |

Verified operating-system and pinned-header deviations are preserved by the
raw tier and recorded in [NDK_BUGS.md](NDK_BUGS.md); the tests assert those
measured contracts rather than hiding them as binding failures.

Every one of the 1,543 tests emits a guest-side benchmark measurement. Of the
1,397 callables, 1,007 have an exclusive one-callable test and therefore an
individually attributable speed value. The remaining 390 are runtime verified
inside tests that intentionally cover related calls together; their test and
suite timings are retained, but are not mislabeled as isolated microbenchmarks.

## Measurements

All binaries use `release-o1`. Guest timings are the values emitted by the
Novus test runner; wall time includes MCP command overhead. The same 146
binaries are used on both machines.

| Metric | A1200 | A4000 |
| --- | ---: | ---: |
| Guest benchmark total | 255.675 s | 72.695 s |
| Guest test median | 1,209 µs | 317 µs |
| Guest test range | 218 µs–25.429 s | 0 µs–5.716 s |
| Runtime wall total | 1,923.363 s | 1,732.468 s |
| Runtime wall median per suite | 11.082 s | 11.063 s |
| Runtime wall range per suite | 10.865–148.887 s | 10.768–34.244 s |
| Binary-size total | 4,208,164 bytes | 4,208,164 bytes |
| Binary-size median | 21,812 bytes | 21,812 bytes |
| Binary-size range | 8,724–120,632 bytes | 8,724–120,632 bytes |

The callable-size probe links each call separately and subtracts a baseline
that imports the same module without making a call. Across 78 modules, all
1,397 deltas are positive: 20–1,368 bytes, median 132 bytes, with aggregate
deltas of 171,936 bytes.

## A1200 fixes verified by the gate

- Expected failed DOS locks now suppress system requesters only around the
  failing call. This prevents missing assigns, inhibited volumes, and killed
  RAM drives from opening a modal requester that blocks the MCP command.
- AmigaGuide lifecycle tests now use a real `input.device` pointer move and
  close-gadget click, bounded signal polling, and a short post-close settle.
  This removes the forged IDCMP message crash and the datatype teardown race.
- ASL cancellation activates the requester before injecting Escape and uses a
  bounded `AbortAslRequest` fallback where the emulator does not deliver
  synthetic input to the modal requester.

## Host gates

The same revision also passes the 3,225-test .NET suite, all 11 NDK tooling
tests, the 9,563-symbol/112-interface NDK inventory verifier, and the generated
API documentation check against the pinned NDK 3.9 installation.

## Reproduce

Run each machine independently so a transient failure cannot overwrite the
other machine's report:

```sh
python3 tools/amiga/run_ndk_dual_machine_gate.py \
  --configuration A1200 --build-dir .novus-cache/ndk-dual \
  --evidence-dir .novus-cache/ndk-evidence --require-complete \
  --layer amiga --profile release-o1 --benchmark --memory-check \
  --hdf /private/tmp/novus-ndk-destructive.hdf --hdf-drive 1 \
  --nonvolatile-volume NDK0
```

Repeat with `--configuration A4000`. Generate fresh callable size reports in
parallel, then pass all reports to `tools/verify_ndk_tests.py`:

```sh
for shard in 0 1 2 3 4 5 6 7; do
  python3 tools/verify_ndk_compile_probes.py --compile --measure-size \
    --shard-index "$shard" --shard-count 8 \
    --output ".novus-cache/ndk-size/shard-$shard" \
    --report ".novus-cache/ndk-size/report-$shard.json" &
done
wait
```

The detailed callable evidence is written to
`Novus/std/amiga/verify-ndk-dual/runtime-a1200.json` and
`runtime-a4000.json`. Raw runtime and size reports live under `.novus-cache`
and retain individual outputs, durations, memory deltas, binary sizes, and
per-callable size deltas.
