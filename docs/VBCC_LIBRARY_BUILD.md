# VBCC AmigaOS Library Build Requirements

## Critical Build Flags for Shared Libraries

When building AmigaOS shared libraries (.library, .device) with VBCC/vlink, specific flags are REQUIRED for correct operation.

### ❌ WRONG (causes Guru #80000003 crashes)

```bash
vlink -bamigahunk -x -Bstatic -Cvbcc -gc-all -o greeting.library greeting.o wrappers.o
```

**Problems:**
1. `-Bstatic` creates a non-relocatable executable (libraries MUST be relocatable)
2. `-gc-all` removes wrapper functions that appear "unreferenced" (they're called via function table)
3. ROMTag self-references fail without relocations

### ✅ CORRECT

```bash
vlink -bamigahunk -x -Cvbcc -o greeting.library greeting.o wrappers.o
```

**Why this works:**
- NO `-Bstatic` → HUNK relocations are preserved
- NO `-gc-all` → wrapper functions are kept
- `-Cvbcc` → correct calling convention support

## Code Structure Requirements

### ROMTag and Tables MUST NOT Be `const`

```c
// ❌ WRONG - VBCC puts const data in non-relocated section
const struct Resident RomTag = { ... };
static const APTR FuncTable[] = { ... };
static const ULONG InitTable[] = { ... };
```

```c
// ✅ CORRECT - allows relocations to be applied
struct Resident RomTag = { ... };
static APTR FuncTable[] = { ... };
static ULONG InitTable[] = { ... };
```

**Why:** VBCC generates relocations only for writable data sections. The `const` qualifier moves data to a read-only section that doesn't get HUNK_RELOC32 entries.

## How AmigaOS Library Loading Works

1. **LoadSeg()** loads the HUNK file into memory
2. **HUNK_RELOC32** entries are applied, converting file offsets to RAM addresses
3. **RomTag.rt_MatchTag** gets relocated to point to its actual RAM location
4. **InitTable** and **FuncTable** pointers get relocated
5. **MakeLibrary()** uses the relocated pointers to build the jump table

### Without Correct Relocations

```
File offset: RomTag @ 0x1A0, rt_MatchTag = 0x1A0 (file offset)
Loaded to:   RAM @ 0x08004000
Result:      rt_MatchTag still = 0x1A0 (INVALID, causes Guru!)
```

### With Correct Relocations

```
File offset: RomTag @ 0x1A0, rt_MatchTag = 0x00000000 (placeholder)
HUNK_RELOC32: offset 0x1A4 → HUNK 0
Loaded to:   RAM @ 0x08004000
Result:      rt_MatchTag = 0x080041A0 (VALID, points to itself)
```

## Verification

Check your library has relocations:

```bash
od -A x -t x4 greeting.library | grep "ec030000"
```

You should see `000003ec` (HUNK_RELOC32) followed by relocation counts and offsets.

## Novus Compiler Implementation

The Novus compiler automatically applies these rules:

```csharp
// VbccToolchain.cs
if (isLibrary)
{
    args.Add("-Cvbcc");  // VBCC calling convention
    // NO -Bstatic
    // NO -gc-all
}
else
{
    args.Add("-Bstatic");  // Static linking for executables
    args.Add("-Cvbcc");
    args.Add("-gc-all");   // Dead code elimination safe for executables
}
```

## References

- AmigaOS ROM Kernel Reference Manual: Libraries
- VBCC documentation: vlink HUNK format
- exec.library/MakeLibrary() autodocs

## Common Errors

### Guru Meditation #80000003

**Cause:** Missing or incorrect relocations in library
**Solution:** Remove `-Bstatic`, remove `const` qualifiers

### Library functions crash immediately

**Cause:** Wrapper functions stripped by `-gc-all`
**Solution:** Don't use `-gc-all` for libraries

### "Version" command works but library crashes

**Cause:** ROMTag structure found, but function vectors have wrong addresses
**Solution:** Check FuncTable has relocations applied
