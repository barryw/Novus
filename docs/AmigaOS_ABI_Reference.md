# AmigaOS ABI Reference for Novus Compiler

## Critical: System-Defined vs. Application-Defined

AmigaOS has **system-defined** calling conventions that are hardcoded in exec.library and **cannot be changed**. These are different from **application-defined** conventions used in your own code.

### System-Defined (NON-NEGOTIABLE)

These conventions are **implemented by the operating system**. You must match them exactly:

1. Library Initialization (RTF_AUTOINIT)
2. Device Initialization
3. Interrupt Handlers
4. Exception Handlers
5. DOS Packet Handlers

### Application-Defined (FLEXIBLE)

These conventions are **your choice** (but follow Amiga standards for compatibility):

1. Your library's public functions
2. Your application's internal functions
3. Your device command handlers (internal implementation)

---

## 1. Library Initialization (RTF_AUTOINIT)

### Register Convention

**SYSTEM-DEFINED BY exec.library - CANNOT BE CHANGED**

```c
struct Library* LibInit(__reg("d0") struct MyLibraryBase* base,
                       __reg("a0") BPTR segList,
                       __reg("a6") struct ExecBase* sysBase)
```

| Register | Contains | Type | Description |
|----------|----------|------|-------------|
| **D0** | Library Base | `struct Library*` | Newly allocated library base pointer |
| **A0** | Segment List | `BPTR` | DOS segment list for library code |
| **A6** | ExecBase | `struct ExecBase*` | System library base (absolute address 4) |

### Why These Registers?

This is hardcoded in **exec.library/MakeLibrary**:

1. MakeLibrary allocates library base
2. MakeLibrary puts library base pointer in **D0**
3. MakeLibrary puts segment list (from InitResident) in **A0**
4. MakeLibrary puts its own base (ExecBase) in **A6**
5. MakeLibrary calls your InitFunc

**You cannot change this.** It's system behavior.

### Return Value

| Register | Contains | Description |
|----------|----------|-------------|
| **D0** | Library Base or NULL | Return your library base (success) or NULL (failure) |

### Complete Example

```c
struct Library* LibInit(__reg("d0") struct MyLibraryBase* base,
                       __reg("a0") BPTR segList,
                       __reg("a6") struct ExecBase* sysBase)
{
    // Initialize library base fields
    base->lib.lib_Node.ln_Type = NT_LIBRARY;
    base->lib.lib_Node.ln_Name = (char*)LibName;
    base->lib.lib_Flags = LIBF_CHANGED;
    base->lib.lib_Version = VERSION;
    base->lib.lib_Revision = REVISION;

    // Store segment list for later expunge
    base->lib_SegList = segList;

    // Return library base (or NULL if init failed)
    return &base->lib;
}
```

### Common Mistakes

❌ **WRONG:**
```c
// THIS WILL CRASH!
LibInit(__reg("a0") struct MyLibraryBase* base,  // Wrong register!
        __reg("d0") BPTR segList,                // Wrong register!
        __reg("a6") struct ExecBase* sysBase)
```

✅ **CORRECT:**
```c
LibInit(__reg("d0") struct MyLibraryBase* base,  // D0 = library base
        __reg("a0") BPTR segList,                // A0 = segment list
        __reg("a6") struct ExecBase* sysBase)    // A6 = ExecBase
```

---

## 2. Device Initialization

**Same convention as library initialization:**

```c
struct Device* DevInit(__reg("d0") struct MyDeviceBase* base,
                      __reg("a0") BPTR segList,
                      __reg("a6") struct ExecBase* sysBase)
```

---

## 3. Library Function Calls

### Standard Amiga Convention

**APPLICATION-DEFINED** - but follow these standards:

```c
ReturnType FunctionName(__reg("a6") struct MyLibraryBase* base,
                       __reg("d0") Type param1,
                       __reg("d1") Type param2,
                       __reg("a0") Type param3,
                       __reg("a1") Type param4,
                       // ... stack for additional params
                       )
```

### Register Usage Rules

| Register | Usage | Preserved? |
|----------|-------|------------|
| **D0-D1** | Parameters, return value | No (volatile) |
| **D2-D7** | Local variables | Yes (callee-saved) |
| **A0-A1** | Parameters | No (volatile) |
| **A2-A5** | Local variables | Yes (callee-saved) |
| **A6** | **Always library base** | Yes (caller sets) |
| **A7** | Stack pointer | Yes |

### A6 Wrapper Pattern

For **68k ABI compatibility**, library functions use A6 wrappers:

**User's call:**
```c
result = MyFunction(LibBase, param1, param2);
```

**Gets compiled to:**
```asm
move.l  param2,-(sp)    ; Push params
move.l  param1,-(sp)
move.l  LibBase,a6      ; Library base in A6
jsr     -30(a6)         ; Call via library vector
addq.l  #8,sp           ; Clean up stack
```

**A6 Wrapper (in library):**
```asm
MyFunction_Wrapper:
    move.l  a6,-(sp)        ; Preserve A6
    move.l  4(sp),d0        ; Get param1 from stack
    move.l  8(sp),d1        ; Get param2 from stack
    ; A6 already has library base
    jsr     _MyFunction     ; Call C function
    move.l  (sp)+,a6        ; Restore A6
    rts
```

**C Implementation:**
```c
int32_t MyFunction(__reg("a6") struct MyLibraryBase* base,
                  __reg("d0") int32_t param1,
                  __reg("d1") int32_t param2)
{
    // Implementation
    return result;  // Returned in D0
}
```

---

## 4. Interrupt Handlers

**SYSTEM-DEFINED** by exec.library:

```c
ULONG InterruptHandler(__reg("a0") APTR data,
                      __reg("a1") APTR code,
                      __reg("a5") struct Custom* custom,
                      __reg("a6") struct ExecBase* sysBase)
```

### Register Convention

| Register | Contains | Description |
|----------|----------|-------------|
| **A0** | is_Data | Pointer to your data (from Interrupt structure) |
| **A1** | is_Code | Pointer to your code (from Interrupt structure) |
| **A5** | Custom chips | Pointer to $DFF000 (custom chip base) |
| **A6** | ExecBase | System library base |

### Return Value

| Register | Contains | Description |
|----------|----------|-------------|
| **D0** | 0 or non-zero | 0 = not handled, non-zero = handled |

---

## 5. Exception Handlers

**SYSTEM-DEFINED** by exec.library:

```c
void ExceptionHandler(__reg("a0") struct Task* task,
                     __reg("a1") struct ExecBase* sysBase)
```

---

## 6. DOS Packet Handlers

**SYSTEM-DEFINED** by dos.library:

DOS handlers receive packets via message ports. The packet format is system-defined.

---

## 7. Function Return Values

### Scalar Returns

| Type | Register(s) | Notes |
|------|-------------|-------|
| int8_t, uint8_t | D0 (low byte) | Upper bits undefined |
| int16_t, uint16_t | D0 (low word) | Upper word undefined |
| int32_t, uint32_t | D0 | Full register |
| int64_t, uint64_t | D0:D1 | D0=high, D1=low |
| pointer | D0 | Any pointer type |
| float | FP0 | If FPU available |
| double | FP0 | If FPU available |

### Structure Returns

**AVOID** returning structures by value in library APIs. Use pointer parameters instead:

❌ **WRONG (causes VBCC issues):**
```c
struct Result GetResult(__reg("a6") struct MyLibraryBase* base);
```

✅ **CORRECT:**
```c
void GetResult(__reg("a6") struct MyLibraryBase* base,
              __reg("a0") struct Result* result);
```

---

## 8. Calling Convention Summary

### Library Initialization (System)
- D0 = library base
- A0 = segment list
- A6 = ExecBase
- Return: D0 = library base or NULL

### Library Functions (Application)
- A6 = library base (always)
- D0, D1, A0, A1 = parameters (caller's choice)
- Additional params on stack
- Return: D0 (or D0:D1 for 64-bit)

### Interrupts (System)
- A0 = is_Data
- A1 = is_Code
- A5 = Custom
- A6 = ExecBase
- Return: D0 = 0 or non-zero

### Preserved Registers
- **Callee must preserve:** D2-D7, A2-A6
- **Caller must preserve:** (none required, but save what you need)
- **Volatile (scratch):** D0-D1, A0-A1

---

## 9. Novus Compiler Implementation Notes

### For @library Attribute

When generating library code, Novus must:

1. **Generate LibInit with EXACT registers:**
   ```c
   __reg("d0") base, __reg("a0") segList, __reg("a6") sysBase
   ```

2. **Generate A6 wrappers** for all public functions

3. **Place A6 wrappers in function table**, not C functions

4. **C functions receive A6** as library base parameter

### Validation Checklist

- [ ] LibInit uses D0, A0, A6 (in that order)
- [ ] LibInit returns library base in D0
- [ ] All public functions have A6 wrappers
- [ ] Function table contains wrapper addresses
- [ ] C implementations receive library base in A6
- [ ] Structures returned via pointer parameters
- [ ] No hidden parameters (check VBCC behavior)

---

## 10. Error Messages

If library fails to load with error **80000003**:
- Check LibInit register assignments
- Verify ROMTag structure alignment
- Check function table terminator (-1)
- Verify segment list handling

If functions crash when called:
- Check A6 wrapper implementation
- Verify function table offsets
- Check register preservation (D2-D7, A2-A6)

---

## References

1. AmigaOS Developer CD 2.1 - Libraries Manual
2. exec.library/MakeLibrary autodoc
3. exec.library/InitResident autodoc
4. ROM Kernel Reference Manual: Libraries
5. Working examples: libopenurl, PowerPCAmiga, AROS libraries

---

## Quick Reference Card

```
LIBRARY INITIALIZATION (System-Defined)
========================================
LibInit(D0=base, A0=segList, A6=ExecBase) -> D0=base

LIBRARY FUNCTIONS (Application-Defined)
========================================
Function(A6=libBase, D0/D1/A0/A1=params...) -> D0=result

INTERRUPTS (System-Defined)
========================================
Handler(A0=data, A1=code, A5=custom, A6=ExecBase) -> D0=handled

PRESERVED REGISTERS
========================================
Must preserve: D2-D7, A2-A6
Volatile: D0-D1, A0-A1
```

---

**REMEMBER:** System-defined conventions are **non-negotiable**. You must match exec.library's expectations exactly, or your library/device will fail to initialize.
