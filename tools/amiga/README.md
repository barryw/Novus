# Amiga-side test tooling

Host-side tools live in `tools/`. These run *on the Amiga*, under FS-UAE.

## What to use when

| Target | Where to run it | Why |
|---|---|---|
| CLI programs (DOS/Exec only) | `vamos -C 68020 -- <prog>` on the host | ~1s, no emulator, no boot |
| `.library` / `.device` | FS-UAE | vamos has no library to open |
| Anything that opens a window | FS-UAE, via `wrun` | needs real Intuition |

`vamos` must be given `-C 68020`. Its default CPU is below Novus's minimum and
dies at an odd PC on our binaries, which looks alarmingly like a codegen bug and
is not one. Novus targets 68020 and newer.

## wrun

Launches a windowed program, verifies its window appeared, closes it, and hands
focus back to the shell.

```
wrun <program> [seconds]     launch, wait, report, close, restore focus
wrun -l                      list open windows
```

Exit codes: `0` window appeared and closed, `5` appeared but would not close,
`10` no window appeared, `20` setup failure.

This exists because of a bootstrapping problem. The moment a program opens a
window it takes focus, and every later command typed at the shell goes nowhere
— including any command that would restore focus. Recovering meant rebooting
(~2 minutes) after every single GUI test. `wrun` does the whole cycle from one
invocation issued while the shell still has focus, so GUI tests chain freely.

Build:

```sh
VBCC=vendor/vbcc vendor/vbcc/bin/vc +aos68k -c99 -cpu=68020 -O=1 -o wrun wrun.c
```

## Running a suite

For Novus conformance tests, use the MCP runner. It builds locally, boots one
A4000, uploads each executable through the exchange, and records structured
Guru/CPU-exception diagnostics without screenshots:

```sh
python3 tools/amiga/run_runtime_suite.py --layer foundation
python3 tools/amiga/run_runtime_suite.py --profile debug --profile release-o1 --profile release-o3 --layer foundation
python3 tools/amiga/run_runtime_suite.py --suite foundation-primitives
python3 tools/amiga/run_runtime_suite.py --suite foundation-aggregates --filter foundation_nested_arrays
python3 tools/amiga/run_runtime_suite.py --layer stdlib --benchmark
python3 tools/amiga/run_runtime_suite.py --layer stdlib --memory-check
python3 tools/amiga/run_runtime_suite.py --suite stdlib-tls-live --benchmark \
  --amissl-dir /path/to/extracted/AmiSSL
```

The default endpoint is `http://localhost:6800/mcp`; results are written to
`.novus-cache/amiga-runtime-suite/report.json`. The foundation layer compiles
its foundational tests into one executable per profile so unchanged reruns hit the
compiler's build stamp and stdlib is compiled once. The stdlib layer does the same:
one `stdlib-all` executable plus the redirected-input fixture. Every individual
`stdlib-*` suite remains selectable when isolating a failure. Three existing const-fn,
intrinsics, and fixed32 probes remain standalone; named suites remain available
for isolating crashes. Coverage includes primitives and numerics, control flow,
functions, aggregates, modules, generics/traits, errors/patterns, ownership/Drop,
strings, intrinsics, const functions, fixed point, inline assembly, native
unions, volatile memory, Amiga register callbacks, and interrupt entries. NDK,
Exec/DOS, GUI, ports, tasks, and other Amiga integrations remain separate.

`--memory-check` snapshots each test before and after execution. It fails on
outstanding Novus allocations and on raw AmigaOS memory that was not returned,
then reclaims tracked test allocations so one failure cannot poison later tests.
Owned values discarded with `let _ = value` are dropped normally. Lazily opened
runtime libraries are closed at test boundaries, and OS memory is retried against
a warmed baseline so one-time subsystem caches are not reported as leaks. The JSON
report also records memory immediately after the program exits and after its output
is fetched, which keeps guest-service overhead separate from test failures. A
whole-process drop larger than 256 bytes is rerun once on the same machine; a
second drop fails the suite as a repeatable teardown leak.

`stdlib-tls-live` is explicit because AmiSSL is a third-party extension. Point
`--amissl-dir` at the `AmiSSL/` directory extracted from the OS3 AmiSSL v5
archive. The runner installs the 68020 library and its master library only in
RAM, assigns them for that disposable machine, and runs a Novus TLS client and
server against each other with a generated one-day self-signed certificate.

The runner deliberately refuses to take over an already-running machine. A
failed command is reset or restarted; if the service retains an uncollected
command, the report records recovery failure instead of hiding it.

## Manual suites

There is no custom batch runner and none is needed — an AmigaDOS script plus one
`Execute` call covers it. Whole suite, one round trip:

```
FailAt 100
SYS:Barry/ptest/p_cli
echo "rc=$RC"
SYS:Barry/ptest/wrun SYS:Barry/ptest/p_gui-traditional 4
echo "rc=$RC"
```

```
Execute SYS:Barry/ptest/suite
```

`FailAt 100` matters: without it the shell aborts the script as soon as a test
returns >= 10, and legitimate non-zero results (the library example returns 36
by design) would end the run.

Redirect each test to its own file rather than a shared stream. Output from a
program launched asynchronously interleaves with the next test's output, which
breaks anything trying to parse the result.
