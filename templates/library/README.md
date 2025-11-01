# example.library - AmigaOS Shared Library Template

This is a "Hello World" skeleton for creating AmigaOS shared libraries (.library files) in Novus. It demonstrates the complete library lifecycle and provides a working foundation you can build upon.

## What This Template Provides

- ✅ Complete working library that compiles and runs
- ✅ ROMTag structure for library identification
- ✅ Open/Close/Expunge/Reserved lifecycle functions
- ✅ Two example functions (GetVersion, Multiply)
- ✅ Test program demonstrating library usage
- ✅ Extensive documentation throughout the code
- ✅ Step-by-step guide for adding new functions

## Project Structure

```
example-library/
├── novus.toml              # Library project configuration
├── src/
│   ├── lib.novus           # Main library code (Novus)
│   ├── library_base.s      # ROMTag and AutoInit (Assembly)
│   └── wrappers.s          # A6 calling convention wrappers (Assembly)
├── examples/
│   ├── test_example.novus  # Test program
│   └── novus.toml          # Test project configuration
└── README.md               # This file
```

## Quick Start

### 1. Build the Library

```bash
# Compile Novus code to object file
novusc build

# Assemble the assembly files
vasmm68k_mot -Fhunk -o build/library_base.o src/library_base.s
vasmm68k_mot -Fhunk -o build/wrappers.o src/wrappers.s

# Link everything together
vlink -bamigahunk -o build/example.library \
    build/library_base.o \
    build/wrappers.o \
    build/lib.o
```

### 2. Install the Library

```bash
# Copy to LIBS: on your Amiga (or shared folder)
cp build/example.library /Users/barry/Emulation/Amiga/A4000-DH0/Barry/LIBS/
```

### 3. Test the Library

```bash
cd examples
novusc build
./test_example
```

Expected output:
```
example.library opened successfully!
Multiply(7, 6) called successfully
Library closed. Test complete!
```

## Understanding the Code

### Three-Layer Architecture

This library uses a three-layer architecture to separate concerns:

```
┌──────────────────────────────┐
│  lib.novus (Business Logic)  │  ← Your library functions
│  - Type-safe Novus code      │  ← Easy to write and maintain
│  - Modern language features  │  ← Option, Result, match, etc.
└──────────────────────────────┘
             ↓ Called by
┌──────────────────────────────┐
│  wrappers.s (Translation)    │  ← Calling convention glue
│  - Register → Stack params   │  ← Mechanical translation
│  - A6 base handling          │  ← One wrapper per function
└──────────────────────────────┘
             ↓ Pointed to by
┌──────────────────────────────┐
│  library_base.s (Bootstrap)  │  ← System interface
│  - ROMTag structure          │  ← Exec scans for this
│  - Function vector table     │  ← Jump table for functions
└──────────────────────────────┘
```

### The Library Lifecycle

AmigaOS libraries go through a well-defined lifecycle:

```
1. LOAD (exec.library scans LIBS:)
   ↓
2. INIT (LibInit called, library base allocated)
   ↓
3. READY (library added to system library list)
   ↓
4. OPEN (program calls OpenLibrary)
   ↓
5. IN USE (program calls library functions)
   ↓
6. CLOSE (program calls CloseLibrary)
   ↓
7. EXPUNGE (library removed from memory)
```

### Required Functions

Every AmigaOS library MUST provide these four functions:

| Function | Offset | When Called | Purpose |
|----------|--------|-------------|---------|
| Open | -6 | OpenLibrary() | Increment open count, return base |
| Close | -12 | CloseLibrary() | Decrement open count |
| Expunge | -18 | System cleanup | Remove library from memory |
| Reserved | -24 | Never | Reserved for future use |

### Example Functions

This template includes two example functions:

| Function | Offset | Parameters | Returns | Purpose |
|----------|--------|------------|---------|---------|
| GetVersion | -30 | (none) | u32 | Return library version |
| Multiply | -36 | i32 a, i32 b | i32 | Return a * b |

## Adding New Functions

Here's how to add a new function to your library. Let's add an `Add` function:

### Step 1: Add Novus Function (lib.novus)

```novus
/// Add - Add two numbers
///
/// VECTOR OFFSET: -42
pub fn Add(base: *ExampleLibraryBase, a: i32, b: i32) -> i32 {
    return a + b
}
```

### Step 2: Add Assembly Wrapper (wrappers.s)

```asm
    XDEF    _Add                 ; Export wrapper
    XREF    _novus_Add           ; Import Novus function

_Add:
    movem.l d2-d7/a2-a6,-(sp)    ; Save registers
    move.l  a6,-(sp)             ; Push base
    move.l  d0,-(sp)             ; Push a
    move.l  d1,-(sp)             ; Push b
    jsr     _novus_Add           ; Call Novus function
    lea     12(sp),sp            ; Clean up 3 parameters
    movem.l (sp)+,d2-d7/a2-a6    ; Restore registers
    rts                          ; Return
```

### Step 3: Add to Vector Table (library_base.s)

```asm
FuncTable:
    dc.l    _LibOpen
    dc.l    _LibClose
    dc.l    _LibExpunge
    dc.l    _LibReserved
    dc.l    _GetVersion
    dc.l    _Multiply
    dc.l    _Add             ; Add your new function here
    dc.l    -1               ; Terminator
```

### Step 4: Update NegSize (library_base.s)

```asm
; Old: 6 functions * 6 = 36
NegSize equ 42            ; New: 7 functions * 6 = 42
```

### Step 5: Rebuild and Test

```bash
novusc build
vasmm68k_mot -Fhunk -o build/library_base.o src/library_base.s
vasmm68k_mot -Fhunk -o build/wrappers.o src/wrappers.s
vlink -bamigahunk -o build/example.library \
    build/library_base.o \
    build/wrappers.o \
    build/lib.o
```

## Calling Library Functions from Other Programs

To call library functions from another Novus program, you need to:

1. **Open the library**:
```novus
let base = OpenLibrary("example.library", 0)
```

2. **Set up registers and call** (currently requires assembly):
```asm
move.l  base,a6          ; Load library base into A6
move.l  #7,d0            ; First parameter
move.l  #6,d1            ; Second parameter
jsr     -36(a6)          ; Call Multiply (offset -36)
; Result in D0
```

3. **Close the library**:
```novus
CloseLibrary(base)
```

**Note**: Future versions of Novus will provide easier FFI declarations to call library functions directly from Novus code.

## Memory Layout

### Library Base in Memory

```
+0:  Library header (34 bytes)
     ├─ ln_Succ, ln_Pred (Node links)
     ├─ ln_Type (NT_LIBRARY = 9)
     ├─ ln_Pri (priority)
     ├─ ln_Name (pointer to name string)
     ├─ lib_Flags
     ├─ lib_pad
     ├─ lib_NegSize (size of function vectors)
     ├─ lib_PosSize (size of library base)
     ├─ lib_Version
     ├─ lib_Revision
     ├─ lib_IdString (pointer to ID string)
     ├─ lib_Sum (checksum)
     └─ lib_OpenCnt (open count)
+34: seglist (4 bytes) - BPTR to code segment
+38: open_count (4 bytes) - Custom open counter
Total: 42 bytes
```

### Function Vector Table in Memory

```
Base-4:  -> Start of vector table (for safety)
Base-6:  -> Open function
Base-12: -> Close function
Base-18: -> Expunge function
Base-24: -> Reserved function
Base-30: -> GetVersion function
Base-36: -> Multiply function
Base-42: -> (next function would go here)
```

## Important Notes

### Register Usage

AmigaOS libraries use a special calling convention:

- **A6**: Library base pointer (ALWAYS)
- **D0-D1, A0-A1**: Function parameters (left to right)
- **Stack**: Additional parameters (right to left push)
- **D0**: Return value
- **D2-D7, A2-A6**: Must be preserved

### Memory Allocation

The library base is allocated with:
- `MEMF_PUBLIC`: Accessible from all tasks
- `MEMF_CLEAR`: Zeroed on allocation

Always free memory in LibExpunge!

### Open Count

The library maintains two open counts:
- `lib_OpenCnt` in Library header (managed by exec.library)
- `open_count` in ExampleLibraryBase (custom counter)

Use the custom counter for your own logic.

### Expunge Handling

Never expunge a library with open_count > 0!

Set `LIBF_DELEXP` flag and wait for the last Close:
```novus
if (*base).open_count > 0 {
    let LIBF_DELEXP: u16 = 0x0001
    (*lib_ptr).lib_Flags = (*lib_ptr).lib_Flags | LIBF_DELEXP
    return 0
}
```

## Common Pitfalls

### ❌ Forgetting to Update NegSize

When adding functions, you MUST update `NegSize` in `library_base.s`:
```asm
; Each function adds 6 bytes
NegSize equ (number_of_functions * 6)
```

### ❌ Wrong Parameter Order in Wrappers

AmigaOS passes parameters in D0, D1, A0, A1, but C expects stack order.
Always push base first, then parameters in order!

### ❌ Not Preserving Registers

Always save/restore D2-D7 and A2-A6:
```asm
movem.l d2-d7/a2-a6,-(sp)   ; Save at start
; ... your code ...
movem.l (sp)+,d2-d7/a2-a6   ; Restore at end
```

### ❌ Incorrect Structure Size

`ExampleLibraryBase_SIZEOF` in `library_base.s` MUST match the actual struct size from `lib.novus`. If you add fields, update both!

## Future Language Improvements

When Novus implements the planned attributes (`@resident`, `@libvec`, `@autoinit`), library creation will become much simpler:

```novus
@resident(name="example.library", version=1)
@autoinit

@libvec(-30)
pub fn GetVersion(base: *ExampleLibraryBase) -> u32 {
    return 0x00010000
}
```

The compiler will generate all the assembly glue code automatically.

## Troubleshooting

### Library Won't Load

Check:
- ✅ ROMTag MatchWord is 0x4AFC
- ✅ Library file is in LIBS: directory
- ✅ Filename ends with ".library"
- ✅ ExampleLibraryBase_SIZEOF matches actual size

### Function Calls Crash

Check:
- ✅ Vector offsets are correct (multiples of 6)
- ✅ A6 contains library base before JSR
- ✅ Registers are preserved in wrappers
- ✅ Stack cleanup is correct (param_count * 4)

### Expunge Problems

Check:
- ✅ open_count is decremented in LibClose
- ✅ LIBF_DELEXP is set when expunge is delayed
- ✅ Memory is freed in LibExpunge
- ✅ Seglist is returned for code unload

## Resources

- [AmigaOS Library Creation Guide](http://amigadev.elowar.com/read/ADCD_2.1/Libraries_Manual_guide/node0001.html)
- [Exec Library Functions](http://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node0200.html)
- [ROMTag (Resident) Structure](http://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node05DA.html)
- [MakeLibrary Function](http://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node0262.html)

## License

This template is provided as-is for use with the Novus programming language.
Use it as a foundation for your own AmigaOS libraries!

---

**Happy library development! 🚀**
