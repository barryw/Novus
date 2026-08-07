---
title: Installation
description: Set up the Novus compiler and toolchain on your system
---

# Installing Novus

This guide will walk you through setting up the Novus compiler and its dependencies on your development machine.

## Prerequisites

Before installing Novus, you'll need the following tools:

### Amiga NDK 3.9

This is the only thing you need to supply yourself. The NDK contains the AmigaOS
system headers (`exec/`, `dos/`, `intuition/`, `proto/` and friends). Its licence
does not allow us to redistribute it, so Novus ships everything **except** the NDK
and you point it at your own copy.

**Download:** [http://www.amigadev.com](http://www.amigadev.com) or search for "NDK 3.9"

Extract it anywhere you like, then tell Novus where it is (see
[Configuration](#configuration) below).

:::note[What you do *not* need]
You no longer need to install VBCC, vasm or vlink. Novus bundles a patched VBCC
toolchain, and it is used in preference to any system install. A stock VBCC lacks
the `aos68k_fpu` target config and the optimizer fixes Novus depends on, so using
one would fail or miscompile.

You also do not need the .NET SDK unless you are building the compiler from source.
:::

## Installing Novus

### Option 1: Download a Release (Recommended)

Grab the archive for your platform from the
[releases page](https://github.com/barryw/novus/releases), extract it, and run it:

```bash
tar xzf novus-macos-arm64.tar.gz -C ~/novus
~/novus/novus --version
```

Add `~/novus` to your `PATH` to invoke it as `novus` from anywhere.

:::caution[Keep the extracted tree together]
The compiler loads its standard library, runtime and bundled VBCC toolchain from
directories beside its own binary. Copying just the executable somewhere else stops
it compiling, with errors like `module 'std::core' not found`.
:::

### Option 2: Build from Source

Building the compiler needs the [.NET 10 SDK](https://dotnet.microsoft.com/download),
plus `make` and a C compiler to build the vendored VBCC toolchain.

```bash
git clone https://github.com/barryw/novus.git
cd novus
dotnet build
dotnet test
```

## Configuration

Tell Novus where your NDK is, once:

```bash
novus config set ndk-path /path/to/NDK3.9
```

This is written to `~/.novus/config.toml`. Check it at any time:

```bash
novus config show
```

```
config file: /Users/you/.novus/config.toml
ndk-path   : /path/to/NDK3.9
```

The path is validated when you set it, so a typo is reported immediately rather
than turning into a confusing missing-header error on your next build.

If you prefer not to use the config file, Novus also accepts `--ndk-path` per
invocation, or an `NDK` environment variable. Precedence is `--ndk-path`, then
`NDK`, then the config file, then the usual install locations.

## Platform-Specific Notes

### macOS

**Apple Silicon (M1/M2/M3):**

Novus ships a native arm64 build with a matching arm64 VBCC toolchain. No Rosetta needed.

**Homebrew users:**

You can use Homebrew to install .NET:
```bash
brew install dotnet
```

### Linux

**Ubuntu/Debian:**
```bash
# Install .NET 9.0
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install dotnet-sdk-9.0
```

**Arch Linux:**
```bash
sudo pacman -S dotnet-sdk
```

### Windows

**WSL2 (Recommended):**

For the best development experience on Windows, consider using WSL2 (Windows Subsystem for Linux) and follow the Linux installation instructions.

**Native Windows:**

All tools work natively on Windows. Use PowerShell or Command Prompt for commands.

## Verification

Let's verify your installation is working correctly:

### 1. Check the compiler
```bash
novus --version
# Expected: novus 0.4.0 (or later)
```

### 2. Check the NDK is configured
```bash
novus config show
# Expected: ndk-path pointing at your NDK 3.9 directory
```

### 3. Compile something

```bash
novus compile hello.novus -o hello
```

If that produces an executable, the compiler, its bundled toolchain and your NDK
are all wired up correctly.

## Running on Amiga

To run your compiled Novus programs, you'll need either:

### Option 1: Amiga Emulator (Recommended for Development)

**WinUAE (Windows/macOS/Linux via Wine):**
- Download: [http://www.winuae.net](http://www.winuae.net)
- Configure with Kickstart ROM and AmigaOS
- Set up a shared folder to transfer executables

**FS-UAE (Cross-platform):**
- Download: [https://fs-uae.net](https://fs-uae.net)
- More modern UI, easier setup
- Good for testing on multiple Amiga configurations

**Recommended Emulator Configuration:**
- CPU: 68020 or higher (68040 recommended)
- RAM: 8MB Fast RAM minimum
- Chipset: AGA for best compatibility
- AmigaOS 3.1 or 3.2

### Option 2: Real Amiga Hardware

**Transferring Files:**

- **CF/SD card adapter** - Most reliable method
- **Serial cable** - Use Amiga Serial Tool or similar
- **Network (if you have a network card)** - FTP/HTTP transfer
- **Floppy disk** - Traditional but slow

**Minimum Hardware Requirements:**
- Any Amiga with 68020+ CPU (A1200, A3000, A4000, or accelerated A500/A2000)
- 2MB RAM minimum (4MB+ recommended)
- AmigaOS 2.0+ (3.1 recommended)

### Option 3: Vampire/Apollo Accelerators

If you have a Vampire V2/V4 or Apollo accelerator:
- Full 68k compatibility
- Fast execution
- Can run 68080-optimized code (future feature)

## Troubleshooting

### "dotnet: command not found"

The .NET SDK is not installed or not in your PATH. Verify installation:
```bash
which dotnet
```

If not found, reinstall .NET SDK and ensure it's in your PATH.

### "Amiga NDK not found"

Novus cannot bundle the NDK, so it has to be told where yours is:
```bash
novus config set ndk-path /path/to/NDK3.9
```

If that reports the directory "does not look like an NDK 3.9 tree", check it
contains `Include/include_h/exec/types.h`.

### Compilation Errors: "NDK headers not found"

Set the NDK path explicitly:
```bash
dotnet run --project Novus/Novus.csproj -- input.novus -o output --ndk-path /path/to/ndk39
```

Or set the NDK environment variable (see Configuration above).

### "Permission denied" when running binary

Make the binary executable:
```bash
chmod +x output
```

### Tests Failing

This indicates a problem with your installation or the Novus compiler itself. Try:
1. Pull the latest changes: `git pull`
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Run tests with verbose output: `dotnet test --logger "console;verbosity=detailed"`

If tests still fail, please report an issue on GitHub with the test output.

## Next Steps

Now that you have Novus installed, you're ready to write your first program!

Continue to: **[Your First Program](/getting-started/first-program/)**

## Additional Resources

- **[GitHub Repository](https://github.com/barryw/novus)** - Source code and issue tracker
- **[Language Reference](/reference/syntax/)** - Complete language documentation
- **[Standard Library](/stdlib/overview/)** - API documentation
- **[VBCC Documentation](http://www.compilers.de/vbcc.html)** - Assembler and linker reference

---

**Having issues?** Please report installation problems on [GitHub Issues](https://github.com/barryw/novus/issues).
