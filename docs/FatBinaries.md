# CPU-targeted and fat binaries

Novus requires a Motorola 68020 or newer CPU. `--cpu auto` uses a 68020-safe
baseline and may select optimized 68040/68060 paths at runtime. A thin build can
target one supported processor explicitly:

```sh
novus compile app.novus --cpu 68020
novus compile app.novus --cpu 68040
novus compile app.novus --cpu 68060
```

Supported targets are `auto`, `68020`, `68030`, `68040`, `68060`, and `68080`.
Project files use the same names in `build.target_cpu`.

Use `M68020_PLUS`, `M68030_PLUS`, `M68040_PLUS`, and `M68060_PLUS` for source-level
conditional compilation. The exact-target constants are `M68020`, `M68030`,
`M68040`, `M68060`, and `M68080`.

The 68020 baseline includes native 32-bit multiply/divide and the full addressing
modes Novus code generation depends on. All variants retain the Amiga 68k ABI.
