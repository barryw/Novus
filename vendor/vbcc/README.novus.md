# VBCC Toolchain - Vendored for Novus

This is the complete VBCC toolchain (compiler, assembler, linker) vendored for the Novus language project.

## Source

Forked from: https://github.com/bebbo/amiga-gcc (VBCC components)
Original upstream: http://www.compilers.de/vbcc.html

This version includes:
- **vbcc**: The C compiler frontend and m68k code generator
- **vasm**: The m68k assembler (vasmm68k_mot)
- **vlink**: The linker for Amiga HUNK format

**Note**: The Amiga NDK (Native Development Kit) is NOT included due to copyright restrictions. You must obtain it separately (see below).

## Why Vendored?

1. **Bug fixes**: We maintain fixes for VBCC optimizer bugs
2. **Stability**: Known-good version that works with Novus
3. **Simplicity**: Single source tree, easy to build and modify
4. **Control**: No external dependencies for core toolchain

## Required: Amiga NDK

The NDK contains headers and libraries for AmigaOS development. It is copyrighted and cannot be redistributed with Novus.

**Where to get it:**
- NDK 3.9: Download from Haage&Partner or other authorized distributors
- Install to: `~/amiga-cc/NDK3.9` (or set `$NDK` environment variable)
- Alternative: Set `--ndk-path` flag when building Novus programs

## Building

From the Novus root directory:

```bash
cd vendor/vbcc
make
```

Binaries will be in: `bin/` (or as configured in Makefile)

The Novus compiler automatically uses these vendored binaries.

## Novus Modifications

See `NOVUS_PATCHES.md` for all modifications made for Novus.

**Current patches**:
- Partial fix for stack-relative address caching bug in m68k code generator

**Known issues**:
- -O2 optimizer still has bugs with ADDRESS instructions
- Novus defaults to -O0 for correctness

## Updating

To update from upstream:

```bash
cd ~/Git/vbcc
git pull
cd /Users/barry/RiderProjects/Novus
rsync -av --exclude='.git' ~/Git/vbcc/ vendor/vbcc/
# Re-apply Novus patches if needed
```

## License

VBCC components are freeware for non-commercial use.  
See individual component licenses in their directories.

Novus-specific modifications follow Novus licensing.
