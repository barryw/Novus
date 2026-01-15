---
title: Installation
description: Set up the Novus compiler and toolchain on your system
---

# Installing Novus

This guide will walk you through setting up the Novus compiler and its dependencies on your development machine.

## Prerequisites

Before installing Novus, you'll need the following tools:

### 1. .NET 9.0 SDK

The Novus compiler is written in C# and requires the .NET 9.0 SDK.

**Download:** [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

**Verify installation:**
```bash
dotnet --version
# Should show 9.0.x or higher
```

### 2. VBCC Toolchain

Novus uses the VBCC toolchain (`vasm` and `vlink`) to assemble and link 68k code.

**Download:** [http://www.compilers.de/vbcc.html](http://www.compilers.de/vbcc.html)

You'll need:
- `vasm` - The assembler (specifically the Motorola syntax version: `vasmm68k_mot`)
- `vlink` - The linker

**macOS/Linux Installation:**

```bash
# Download and extract VBCC
wget http://www.compilers.de/vbcc_bin_mac.tar.gz
tar xzf vbcc_bin_mac.tar.gz
sudo mv vbcc /opt/vbcc

# Add to PATH in ~/.bashrc or ~/.zshrc
export VBCC=/opt/vbcc
export PATH=$VBCC/bin:$PATH
```

**Windows Installation:**

1. Download the Windows VBCC package
2. Extract to `C:\vbcc`
3. Add `C:\vbcc\bin` to your PATH environment variable

**Verify installation:**
```bash
vasmm68k_mot -help
vlink -h
```

### 3. Amiga NDK 3.9

The Amiga Native Development Kit provides headers and libraries for AmigaOS development.

**Download:** [http://www.amigadev.com](http://www.amigadev.com) or search for "NDK 3.9"

Extract the NDK and note its location. You'll reference this path when compiling Novus programs.

**Typical installation locations:**
- macOS/Linux: `/opt/amiga/ndk39`
- Windows: `C:\amiga\ndk39`

## Installing Novus

### Option 1: Build from Source (Recommended)

Clone the Novus repository and build the compiler:

```bash
# Clone the repository
git clone https://github.com/barryw/novus.git
cd novus

# Build the compiler
dotnet build

# Run the test suite to verify
dotnet test
```

The compiled binary will be located at:
```
Novus/bin/Debug/net9.0/Novus.dll
```

### Option 2: Install Release Binary (Coming Soon)

Pre-built releases will be available on GitHub once the compiler reaches stable status.

## Configuration

### Environment Variables

Set these environment variables for convenience:

**~/.bashrc or ~/.zshrc (macOS/Linux):**
```bash
# VBCC toolchain
export VBCC=/opt/vbcc
export PATH=$VBCC/bin:$PATH

# Amiga NDK
export NDK=/opt/amiga/ndk39

# Optional: Alias for running Novus compiler
alias novus="dotnet run --project ~/novus/Novus/Novus.csproj --"
```

**Windows (System Environment Variables):**
```
VBCC=C:\vbcc
NDK=C:\amiga\ndk39
Path=%Path%;%VBCC%\bin
```

### Compiler Configuration File (Optional)

You can create a `novus.config` file in your home directory to set default compiler options:

```json
{
  "vbccPath": "/opt/vbcc",
  "ndkPath": "/opt/amiga/ndk39",
  "defaultCpu": "68020",
  "defaultOptLevel": 2
}
```

## Platform-Specific Notes

### macOS

**Apple Silicon (M1/M2/M3):**

The .NET SDK and VBCC both work on Apple Silicon Macs via Rosetta 2. No special configuration needed.

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

### 1. Check .NET
```bash
dotnet --version
# Expected: 9.0.x
```

### 2. Check VBCC
```bash
vasmm68k_mot -help | head -5
# Expected: vasm 1.9x (or similar)

vlink -h | head -5
# Expected: vlink 0.x (or similar)
```

### 3. Check Novus Compiler
```bash
cd ~/novus  # Or wherever you cloned it
dotnet run --project Novus/Novus.csproj -- --version
# Expected: Novus compiler version x.x.x
```

### 4. Run Test Suite
```bash
cd ~/novus
dotnet test
# Expected: All tests passing (77+ tests)
```

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

### "vasmm68k_mot: command not found"

VBCC is not installed or not in your PATH. Verify:
```bash
echo $VBCC
ls $VBCC/bin
```

Make sure the VBCC bin directory is in your PATH.

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
