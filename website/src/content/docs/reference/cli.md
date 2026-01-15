---
title: CLI Reference
description: Complete reference for the Novus compiler command-line interface
---

The Novus compiler (`novus`) provides a comprehensive command-line interface for compiling, testing, and managing Novus projects.

## Basic Usage

```bash
novus [command] [options] <input>
```

When no command is specified, `compile` is used by default.

## Commands

### compile (default)

Compile a single Novus source file to an Amiga executable.

```bash
novus compile [options] <file.novus>
novus [options] <file.novus>  # compile is default
```

**Options:**
- `-o, --output <file>` - Output file name (default: `a.out`)
- `--emit-asm` - Emit assembly only, don't assemble/link
- `--emit-ir` - Emit IR (intermediate representation) to stdout
- `-v, --verbose` - Verbose output

### build

Build a project or workspace using `project.toml` configuration.

```bash
novus build [options]
```

**Options:**
- `-p, --project <path>` - Path to project directory or `project.toml` file (default: current directory)
- `--release` - Build in release mode (optimization level 2, no debug symbols)
- `--debug` - Build in debug mode (no optimization, debug symbols, bounds checking) - this is the default
- `-v, --verbose` - Verbose output

### test

Build and run tests for Novus projects. Discovers `@test` attributed functions and generates a test runner.

```bash
novus test [options] [path]
```

**Options:**
- `-o, --output <dir>` - Output directory for test runner executable (default: `./tests/`)
- `-v, --verbose` - Verbose output showing compilation details
- `--release` - Build test runner in release mode (default: debug)
- `--filter <pattern>` - Only run tests matching this pattern (e.g., `test_math_*`)
- `--list` - List discovered tests without building
- `-b, --benchmark` - Enable timing for each test, showing duration in microseconds

### bench

Build and run benchmarks for Novus projects. Discovers `@bench` attributed functions.

```bash
novus bench [options] [path]
```

**Options:**
- `-o, --output <dir>` - Output directory for benchmark runner executable (default: `./bench/`)
- `-v, --verbose` - Verbose output showing compilation details
- `--release` - Build benchmark runner in release mode (default: true for accurate timing)
- `--filter <pattern>` - Only run benchmarks matching this pattern (e.g., `bench_math_*`)
- `--list` - List discovered benchmarks without building
- `--iterations <n>` - Fixed iteration count for all benchmarks (0 = auto-detect)

### new

Create a new Novus project from a template.

```bash
novus new [options] <project-name>
```

**Options:**
- `-t, --type <type>` - Project type: `bin` (executable), `lib` (library), `device` (AmigaOS device), `library` (AmigaOS shared library)
- `-a, --author <name>` - Author name
- `-l, --license <license>` - License (e.g., MIT, Apache-2.0, GPL-3.0)
- `-d, --description <text>` - Project description

### fmt

Format Novus source code according to style guidelines.

```bash
novus fmt [options] [files...]
```

**Options:**
- `-c, --check` - Check if formatting is needed without modifying files
- `-v, --verbose` - Verbose output
- `--indent <n>` - Number of spaces per indentation level (default: 4)
- `--max-width <n>` - Maximum line width (default: 100)

### clean

Clean all cached artifacts to force fresh rebuilds.

```bash
novus clean [options]
```

**Options:**
- `-v, --verbose` - Verbose output showing what's being deleted
- `--keep-stdlib-cache` - Don't delete stdlib precompiled cache
- `--keep-bin-std` - Don't delete `bin/std` copy
- `--keep-user-cache` - Don't delete user cache (`~/.novus-cache`)
- `--keep-temp-dirs` - Don't delete temp build directories

### stdlib-build

Pre-compile the standard library for faster linking.

```bash
novus stdlib-build [options]
```

**Options:**
- `--cpu <target>` - Build for specific CPU (`68020`, `68040`, `68060`) or `all` (default: all)
- `--mode <mode>` - Build mode: `debug`, `release`, or `both` (default: both)
- `-v, --verbose` - Verbose output

### generate-stubs

Generate Amiga library stubs and Novus FFI bindings from NDK 3.9 SFD files.

```bash
novus generate-stubs [options]
```

**Options:**
- `--ndk-path <path>` - Path to NDK installation (default: auto-detect from `NDK_PATH` env var)
- `-o, --output <dir>` - Output directory (default: current directory)

## Target Options

These options control code generation for specific Amiga hardware configurations.

### CPU Targets

Use `--cpu <target>` to specify the target CPU:

| Target | Description | Instruction Set |
|--------|-------------|-----------------|
| `68020` | 68020/68030 | 32-bit operations, bitfields, PC-relative addressing |
| `68040` | 68040 | Cache-aware, avoids trappy operations |
| `68060` | 68060 | Optimized, strict operation selection |
| `auto` | Fat binary | Runtime CPU detection with multiple code paths |

**Default:** `auto`

**Examples:**
```bash
novus compile --cpu 68020 main.novus
novus build --cpu 68040
```

### FPU Targets

Use `--fpu <mode>` to specify floating-point unit configuration:

| Mode | Description |
|------|-------------|
| `none` / `soft` | Software floating-point emulation |
| `68881` | 68881/68882 FPU coprocessor |
| `68040` | 68040 built-in FPU |
| `68060` | 68060 built-in FPU |
| `auto` | Runtime FPU detection |

**Default:** `auto`

### Chipset Targets

Use `--chipset <target>` to specify the Amiga chipset:

| Target | Description | Features |
|--------|-------------|----------|
| `OCS` | Original Chip Set | A1000/A500/A2000 |
| `ECS` | Enhanced Chip Set | A500+/A600/A3000 |
| `AGA` | Advanced Graphics Architecture | A1200/A4000 |
| `auto` | Runtime detection | Widest common subset |

**Default:** `auto`

## Optimization Options

Use `-O <level>` or `--optimize <level>` to control optimization:

| Level | Description | Use Case |
|-------|-------------|----------|
| `-O 0` | No optimization | Development, debugging |
| `-O 1` | Basic optimization | Fast compile times, some optimization |
| `-O 2` | Standard optimization | Recommended for release builds |
| `-O 3` | Aggressive optimization | Maximum performance, slower compilation |

**Default:** `-O 2`

## Safety Options

Control runtime safety checks and validation:

- `--safety-level <level>` - Safety level (0-3):
  - `0` - Unsafe: no runtime checks
  - `1` - Basic: essential checks only
  - `2` - Full: comprehensive checks (default for debug)
  - `3` - Paranoid: maximum validation
- `--unsafe` - Disable all safety checks (equivalent to `--safety-level 0`)

**Defaults:**
- Debug mode: `--safety-level 2`
- Release mode: `--safety-level 1`

## Build Mode Options

- `--release` - Release mode: optimization level 2, no debug symbols
- `--debug` - Debug mode: no optimization, debug symbols, bounds checking (default)

## Debugging Options

- `-v, --verbose` - Enable verbose output showing compilation details
- `--emit-ir` - Emit intermediate representation to stdout for inspection
- `--emit-asm` - Emit assembly code without assembling/linking

## Cache Options

The Novus compiler implements incremental compilation with multiple cache layers:

- `--use-stdlib-cache` - Use cached stdlib if available (default: rebuild fresh)
- `--rebuild-stdlib-cache` - Rebuild stdlib and cache it for future use
- `--no-cache` - Disable incremental compilation cache (force full rebuild)
- `--cache-stats` - Display cache hit/miss statistics after compilation

## Toolchain Options

- `--vbcc-path <path>` - Path to VBCC installation (default: auto-detect from `$VBCC` or vendored VBCC)
- `--ndk-path <path>` - Path to NDK installation (default: auto-detect from `$NDK_PATH` or `~/amiga-cc/NDK3.9`)
- `--backend <backend>` - Code generation backend: `c` (VBCC, stable) or `m68k` (experimental direct assembly)

## Profile-Guided Optimization

- `--pgo-generate` - Generate instrumented code for profile collection
- `--pgo-use <file.pgo>` - Use profile data file for profile-guided optimization

## Environment Variables

The Novus compiler respects the following environment variables:

| Variable | Description | Example |
|----------|-------------|---------|
| `VBCC` | Path to VBCC installation | `/opt/vbcc` |
| `NDK_PATH` | Path to Amiga NDK | `~/amiga-cc/NDK3.9` |

## Exit Codes

- `0` - Success
- `1` - Compilation error or invalid arguments

## Examples

### Compile a simple program

```bash
novus compile hello.novus
./a.out
```

### Compile with custom output name

```bash
novus compile -o hello hello.novus
```

### Build for 68020 with optimization

```bash
novus compile --cpu 68020 -O 3 -o myapp myapp.novus
```

### Emit assembly for inspection

```bash
novus compile --emit-asm --cpu 68040 graphics.novus > graphics.s
```

### Build a project in release mode

```bash
cd myproject
novus build --release
```

### Run tests with verbose output

```bash
novus test --verbose --filter "test_parser_*"
```

### Create a new binary project

```bash
novus new --type bin --author "Your Name" --license MIT my-app
cd my-app
novus build
```

### Format all source files

```bash
novus fmt src/**/*.novus
```

### Profile-guided optimization workflow

```bash
# Step 1: Generate instrumented build
novus compile --pgo-generate -o myapp myapp.novus

# Step 2: Run with representative workload
./myapp < typical-input.dat

# Step 3: Rebuild with profile data
novus compile --pgo-use myapp.pgo -o myapp myapp.novus
```
