# AmigaOS Calling Convention Quick Reference

## Library Initialization (SYSTEM-DEFINED)

```c
struct Library* LibInit(__reg("d0") struct MyLibraryBase* base,
                       __reg("a0") BPTR segList,
                       __reg("a6") struct ExecBase* sysBase)
```

| Register | Parameter | Type | Source |
|----------|-----------|------|--------|
| **D0** | Library Base | `struct Library*` | MakeLibrary (allocated) |
| **A0** | Segment List | `BPTR` | DOS LoadSeg |
| **A6** | ExecBase | `struct ExecBase*` | System (0x00000004) |

**Return:** D0 = library base pointer or NULL

**Source:** exec.library/MakeLibrary (hardcoded in ROM)

---

## Library Function Calls (APPLICATION-DEFINED)

```c
ReturnType MyFunction(__reg("a6") struct MyLibraryBase* base,
                     __reg("d0") Type param1,
                     __reg("d1") Type param2,
                     __reg("a0") Type param3,
                     __reg("a1") Type param4)
```

| Register | Usage | Notes |
|----------|-------|-------|
| **A6** | Library Base | Always (caller sets) |
| **D0, D1** | Parameters/Return | Volatile |
| **A0, A1** | Parameters | Volatile |
| Stack | Additional params | Right-to-left |

**Return:** D0 (or D0:D1 for 64-bit)

---

## Register Preservation

| Registers | Status | Responsibility |
|-----------|--------|----------------|
| **D2-D7, A2-A6** | Preserved | Callee must save/restore |
| **D0-D1, A0-A1** | Volatile | Caller must save if needed |
| **A7** | Stack Pointer | Always preserved |

---

## Device Initialization (SYSTEM-DEFINED)

Same as library initialization:

```c
struct Device* DevInit(__reg("d0") struct MyDeviceBase* base,
                      __reg("a0") BPTR segList,
                      __reg("a6") struct ExecBase* sysBase)
```

---

## Interrupt Handlers (SYSTEM-DEFINED)

```c
ULONG InterruptHandler(__reg("a0") APTR data,
                      __reg("a1") APTR code,
                      __reg("a5") struct Custom* custom,
                      __reg("a6") struct ExecBase* sysBase)
```

| Register | Parameter | Source |
|----------|-----------|--------|
| **A0** | is_Data | Interrupt structure |
| **A1** | is_Code | Interrupt structure |
| **A5** | Custom chips | 0xDFF000 |
| **A6** | ExecBase | System |

**Return:** D0 = 0 (not handled) or non-zero (handled)

---

## Return Value Conventions

| Type | Register(s) | Notes |
|------|-------------|-------|
| int8_t, uint8_t | D0 (low byte) | Upper bits undefined |
| int16_t, uint16_t | D0 (low word) | Upper word undefined |
| int32_t, uint32_t | D0 | Full register |
| int64_t, uint64_t | D0:D1 | D0=high, D1=low |
| Pointer | D0 | Any pointer type |
| float/double | FP0 | If FPU available |

**Structures:** Use pointer parameter, NOT by-value return

---

## Common Error Codes

| Code | Meaning | Common Cause |
|------|---------|--------------|
| 80000003 | Invalid library base | Wrong LibInit registers |
| 80000004 | Library not found | Missing from LIBS: |
| 80000005 | Version mismatch | OpenLibrary version too high |

---

## Critical Rules

### System-Defined Conventions (NON-NEGOTIABLE)

These are **hardcoded in ROM** and **cannot be changed**:

- Library/Device initialization (MakeLibrary)
- Interrupt handlers (AddIntServer)
- Exception handlers (SetExcept)
- DOS packet handlers

### Application-Defined Conventions (YOUR CHOICE)

You choose (but follow Amiga standards):

- Your library's public functions
- Your internal functions
- Your application code

### The Difference

```
INITIALIZATION (one time):          FUNCTION CALLS (many times):
┌───────────────────────┐          ┌───────────────────────┐
│ A6 = ExecBase         │          │ A6 = Library Base     │
│ D0 = Library Base     │          │ D0 = First Parameter  │
│ A0 = Segment List     │          │ D1 = Second Parameter │
└───────────────────────┘          └───────────────────────┘
  System-defined                     Application-defined
  (cannot change)                    (your choice)
```

---

## A6 Wrapper Pattern

**Why needed:** Library functions receive base in A6, but callers pass parameters normally.

**Function Table:**
```c
static APTR FuncTable[] = {
    (APTR)MyFunction_Wrapper,  // Points to wrapper, NOT C function
    (APTR)-1                    // Terminator
};
```

**Assembly Wrapper:**
```asm
_MyFunction_Wrapper:
    move.l  a6,-(sp)        ; Save library base
    ; Extract parameters from stack/registers
    ; A6 already has library base (from caller)
    jsr     _MyFunction     ; Call C function
    move.l  (sp)+,a6        ; Restore
    rts
```

**C Function:**
```c
int32_t MyFunction(__reg("a6") struct MyLibraryBase* base,
                  __reg("d0") int32_t param)
{
    // base is in A6 (from wrapper)
    // param is in D0 (from wrapper)
    return result;  // Returned in D0
}
```

---

## Verification Checklist

Before deploying library code:

- [ ] LibInit uses: D0=base, A0=segList, A6=ExecBase
- [ ] All public functions have A6 wrappers
- [ ] Function table uses wrapper addresses
- [ ] ROMTag structure is non-const
- [ ] Library base has struct Library first
- [ ] LibExpunge returns segment list
- [ ] Tested on actual/emulated AmigaOS

---

## When In Doubt

### DO:
✅ Check NDK documentation
✅ Compare with working examples
✅ Test on AmigaOS
✅ Ask for clarification

### DON'T:
❌ Trust assumptions
❌ Change working code without proof
❌ Skip testing system interfaces
❌ Guess at conventions

---

## References

### Primary Sources
- AmigaOS Developer CD 2.1
- ROM Kernel Reference Manual
- exec.library autodocs

### Working Examples
- PowerPCAmiga: https://github.com/Sakura-IT/PowerPCAmiga
- libopenurl: https://github.com/jens-maus/libopenurl
- NDK example.library

### Novus Documentation
- `docs/AmigaOS_ABI_Reference.md` (complete reference)
- `templates/library/REGISTER_CONVENTION_DIAGRAM.md` (visual diagrams)
- `templates/library/CODE_REVIEW_CHECKLIST.md` (review guide)

---

## The One Rule to Remember

**System-defined calling conventions are HARDCODED in ROM.**

If exec.library, dos.library, or intuition.library calls your function, you **MUST** match the system's register assignments.

You **CANNOT** change them. They are **NON-NEGOTIABLE**.

**Always verify against NDK documentation before changing system interface code.**

---

**AmigaOS LibInit Convention:**
```
D0 = base, A0 = segList, A6 = ExecBase
```

**This is the standard. Period.**

---

*Quick Reference Card - AmigaOS NDK 3.9*
*For Novus Compiler Development*
*Last Updated: November 2, 2025*
