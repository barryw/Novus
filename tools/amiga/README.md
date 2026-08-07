# Amiga-side test tooling

Host-side tools live in `tools/`. These run *on the Amiga*, under FS-UAE.

## What to use when

| Target | Where to run it | Why |
|---|---|---|
| CLI programs (DOS/Exec only) | `vamos -C 68020 -- <prog>` on the host | ~1s, no emulator, no boot |
| `.library` / `.device` | FS-UAE | vamos has no library to open |
| Anything that opens a window | FS-UAE, via `wrun` | needs real Intuition |

`vamos` must be given `-C 68020`. It defaults to 68000 and dies at an odd PC on
our binaries, which looks alarmingly like a codegen bug and is not one. Novus
targets 68020+; `--cpu 68000` is not supported and fails during assembly.

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
