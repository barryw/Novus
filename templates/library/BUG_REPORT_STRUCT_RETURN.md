# CRITICAL BUG REPORT: Struct Return Convention Violation

## Error Symptom
Program crashes with error 80000006 (illegal instruction / address error / invalid memory access)

## Root Cause #1: Struct Return Convention Mismatch

### Problem
The library function `GreetingLibrary_GetLibraryVersion()` returns a struct by value:

**File:** `/Users/barry/RiderProjects/Novus/templates/library/target/debug/libs/greeting.c:208-214`
```c
struct LibraryVersion GreetingLibrary_GetLibraryVersion(struct GreetingLibraryBase* base) {
    struct LibraryVersion ver;
    ver.major = base->lib.lib_Version;
    ver.minor = base->lib.lib_Revision;
    ver.patch = base->lib_Patch;
    return ver;  // ← STRUCT RETURN BY VALUE
}
```

### The 68k Struct Return Convention (VBCC)

When a struct is returned by value on 68k:

1. **Caller responsibilities:**
   - Allocate space on stack for return value
   - Pass hidden pointer to this space (typically in A0 or A1)
   - After call, struct data is at the address that was passed

2. **Callee responsibilities:**
   - Receive hidden pointer in register (A0/A1)
   - Write struct fields to the address pointed to by hidden pointer
   - Return normally (no struct data in registers)

### What's Actually Happening

**The wrapper** (`greeting_wrappers.s:68-75`) assumes struct is in registers:
```asm
_GreetingLibrary_GetLibraryVersion_Wrapper:
    move.l  a6,-(sp)        ; Push library base as parameter
    jsr     _GreetingLibrary_GetLibraryVersion ; Call C function
    addq.l  #4,sp           ; Clean up stack
    rts                     ; ← Assumes result in D0/D1 - WRONG!
```

**The caller** (`greeting_calls.s:24-38`) also assumes registers:
```asm
; Comment says: "Result is returned in registers: d0.w = major, d1.w = minor, d2.w = patch"
_call_GreetingLibrary_GetLibraryVersion:
    jsr     OpenLib
    move.l  GreetingLibraryBase.l,a6
    cmp.l   #0,a6
    beq.s   .fail
    jsr     -42(a6)         ; Call GetLibraryVersion
    rts                     ; ← Expects result in D0/D1 - WRONG!
```

### The Crash Sequence

1. `greeting-example` calls `call_GreetingLibrary_GetLibraryVersion()`
2. Assembly wrapper calls C function without passing hidden pointer
3. C function writes struct to **random memory location** (or registers)
4. Caller tries to use D0/D1 as struct fields (but they contain garbage)
5. These garbage values are passed to `write()` as the version numbers
6. `write()` uses these as format arguments in RawDoFmt
7. RawDoFmt accesses invalid memory → **CRASH with error 80000006**

---

## Root Cause #2: Taking Address of Register Variable

**File:** `/Users/barry/RiderProjects/Novus/Novus/runtime/novus_io.c:12-15`
```c
__reg("d0") static void putch_to_handle(__reg("d0") uint8_t ch, __reg("a3") BPTR handle) {
    // Write single character to the file handle
    Write(handle, &ch, 1);  // ← ILLEGAL: Taking address of register variable!
}
```

### The Problem
- `ch` is declared as `__reg("d0")` - it lives in a register
- Registers **do not have memory addresses**
- The code takes `&ch` and passes it to `Write()`
- This is **undefined behavior** - may compile but will not work correctly

### The Fix
Use a local stack variable:
```c
static void putch_to_handle(__reg("d0") uint8_t ch, __reg("a3") BPTR handle) {
    uint8_t buf = ch;  // Copy to stack
    Write(handle, &buf, 1);  // Pass address of stack variable
}
```

Or better yet, use RawDoFmt correctly with a proper buffer:
```c
// PutChProc for RawDoFmt - stores chars to buffer
void __asm putch_to_buffer(
    register __d0 char ch,
    register __a3 char **bufPtr
) {
    *(*bufPtr)++ = ch;
}
```

---

## Solution #1: Return Struct via Pointer Parameter

Change the C function signature to return via pointer:

```c
void GreetingLibrary_GetLibraryVersion(struct GreetingLibraryBase* base, struct LibraryVersion* result) {
    result->major = base->lib.lib_Version;
    result->minor = base->lib.lib_Revision;
    result->patch = base->lib_Patch;
}
```

Update the wrapper:
```asm
_GreetingLibrary_GetLibraryVersion_Wrapper:
    ; Parameters: A6 = library base, A0 = pointer to result struct
    move.l  a0,-(sp)        ; Push result pointer
    move.l  a6,-(sp)        ; Push library base
    jsr     _GreetingLibrary_GetLibraryVersion
    addq.l  #8,sp           ; Clean up stack (2 params × 4 bytes)
    rts
```

Update the caller:
```asm
_call_GreetingLibrary_GetLibraryVersion:
    jsr     OpenLib
    move.l  GreetingLibraryBase.l,a6
    cmp.l   #0,a6
    beq.s   .fail

    ; Allocate 6 bytes on stack for result (aligned to even boundary)
    sub.l   #8,sp           ; Allocate 8 bytes (6 for struct + 2 padding)
    move.l  sp,a0           ; A0 = pointer to result space
    jsr     -42(a6)         ; Call GetLibraryVersion

    ; Load result from stack into registers for C caller
    move.w  (sp),d0         ; d0.w = major
    move.w  2(sp),d1        ; d1.w = minor
    move.w  4(sp),d2        ; d2.w = patch (if needed)
    add.l   #8,sp           ; Clean up stack
    rts
.fail:
    moveq   #0,d0
    moveq   #0,d1
    moveq   #0,d2
    rts
```

---

## Solution #2: Return Small Structs in Registers (Better)

For small structs (6 bytes), manually pack into registers:

```c
// Return value packed into D0 (bits 31-16: major, bits 15-0: minor) and D1 (bits 15-0: patch)
uint32_t GreetingLibrary_GetLibraryVersion_Packed(struct GreetingLibraryBase* base) {
    // Pack major (16 bits) and minor (16 bits) into D0
    uint32_t result = ((uint32_t)base->lib.lib_Version << 16) | (uint32_t)base->lib.lib_Revision;
    // Patch goes in D1 (caller must handle this)
    // NOTE: This requires inline assembly or multiple return values
    return result;
}
```

But this is ugly. Better solution: **Always use pointer parameter for struct returns**.

---

## Solution #3: Fix RawDoFmt Callback

```c
// PutCh callback for RawDoFmt - NO register attributes
static void putch_to_handle(void) {
    // Use inline assembly to access registers directly
    __asm volatile (
        "move.b d0,%0\n\t"        // Get character from D0
        "move.l a3,%1\n\t"        // Get handle from A3
        : "=m" (ch_var), "=r" (handle_var)
    );

    uint8_t buf = ch_var;
    Write(handle_var, &buf, 1);
}
```

Or use a proper buffered approach with RawDoFmt.

---

## Recommended Fix Order

1. **Fix struct return** in `LibraryGenerator.cs` to generate void functions with pointer parameters
2. **Fix wrapper generation** to pass result pointer in A0
3. **Fix RawDoFmt callback** to use stack variable instead of register variable address
4. **Rebuild and test**

---

## Files Affected

- `/Users/barry/RiderProjects/Novus/Novus/Codegen/LibraryGenerator.cs` - generates library C code
- `/Users/barry/RiderProjects/Novus/Novus/Codegen/X86CodeGenerator.cs` - generates wrappers
- `/Users/barry/RiderProjects/Novus/Novus/runtime/novus_io.c` - RawDoFmt callback
- `/Users/barry/RiderProjects/Novus/templates/library/example/greeting_calls.s` - manual caller

---

## Testing After Fix

1. Rebuild library with corrected struct return
2. Copy to `/Users/barry/Emulation/Amiga/A4000-DH0/LIBS/`
3. Rebuild example program
4. Copy to `/Users/barry/Emulation/Amiga/A4000-DH0/Barry/`
5. Run on A4000 UAE instance
6. Should print: `greeting.library version: 1.0.0`
