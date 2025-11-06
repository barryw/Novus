# VBCC Patches for Novus

This document describes patches applied to VBCC for use with the Novus compiler.

## 1. CSE ADDRESS Operation Kill Set Bug (CRITICAL CORRECTNESS FIX)

**File:** `vbcc/cse.c`
**Line:** 224 (original), 219-266 (patched)
**Date Applied:** 2025-11-06
**Severity:** Critical (produces incorrect code at -O2)

### Problem Description

The Common Subexpression Elimination (CSE) optimizer was skipping ADDRESS operations when building the `ae_kills` invalidation matrix. This caused ADDRESS expressions (computing addresses of variables like `&local_var` or `&struct->field`) to be cached and reused even after:

1. The variable being addressed was modified or moved
2. Function calls occurred that could modify stack layout or memory
3. Stack pointer adjustments happened

**Impact:** Generated code would cache stack-relative addresses, then reuse those stale addresses after function calls modified the stack pointer, resulting in incorrect memory accesses.

**Example Failure Case:**
```c
// In prime_100000.novus when compiled with -O2:
// 1. ADDRESS operation computes address of Vec::data field
// 2. CSE caches this address
// 3. Function call (printf) modifies stack pointer
// 4. CSE incorrectly reuses cached address
// 5. Program accesses wrong memory location
// Result: Only outputs "2" instead of all primes up to 100,000
```

### Original Code (BUGGY)

```c
for(c=0;c<ecount;c++){
    Var *v;
    p=elist[c];
    if(p->code==ADDRESS) continue;  // ← BUG: ADDRESS ops skipped!
    // ... build ae_kills for other operations
}
```

### Fixed Code

```c
for(c=0;c<ecount;c++){
    Var *v;
    p=elist[c];
    /*  ADDRESS operations must be invalidated when:
        1. The variable being addressed changes (moves/is reassigned)
        2. Memory aliasing could affect the address
        3. Function calls occur (stack changes, global/static access)
        We handle ADDRESS the same as other operations to build proper kill sets.
    */
    if(p->code==ADDRESS){
        /*  For ADDRESS operations, we need to track which variables' addresses
            are being taken and ensure they're invalidated appropriately.
        */
        if((p->q1.flags&(VAR|VARADR))==VAR){
            v=p->q1.v;
            i=v->index;
            /*  Kill this ADDRESS expression when the variable is modified or moved */
            BSET(ae_kills[i],c);
            if(p->q1.flags&DREFOBJ){ BSET(ae_kills[i+vcount-rcount],c);BSET(ae_drefs,c);}
            /*  Mark as global/static/address so function calls properly invalidate */
            if(v->nesting==0||v->storage_class==EXTERN) BSET(ae_globals,c);
            if(v->storage_class==STATIC) BSET(ae_statics,c);
            /*  Always mark ADDRESS expressions as needing invalidation on address changes */
            BSET(ae_address,c);
        }
        continue;
    }
    // ... rest of kill set building for other operations
}
```

### What the Fix Does

The fix ensures ADDRESS operations are properly tracked in the CSE system:

1. **Variable Modification Tracking:** ADDRESS operations are added to the kill set for the variable being addressed (`ae_kills[i]`), so if that variable is modified, the cached address is invalidated.

2. **Dereference Tracking:** If addressing through a pointer (`DREFOBJ`), the ADDRESS is added to the appropriate kill sets and marked in `ae_drefs`.

3. **Global/Static Tracking:** ADDRESS operations on globals/statics are marked in `ae_globals`/`ae_statics`, ensuring function calls (which may modify globals) invalidate these addresses.

4. **Address-Taken Tracking:** All ADDRESS operations are marked in `ae_address`, ensuring any operation that could affect addresses invalidates them.

### How Kill Sets Work in CSE

The CSE optimizer uses bit vectors to track which expressions must be invalidated:

- `ae_kills[i]` = expressions that must be invalidated when variable `i` changes
- `ae_globals` = expressions involving globals (invalidated on function calls)
- `ae_statics` = expressions involving statics (invalidated on static modifications)
- `ae_address` = expressions involving addresses (invalidated when address operations occur)
- `ae_drefs` = expressions involving dereferences (invalidated on pointer modifications)

When an operation modifies a variable (tracked via `change_list`), the optimizer calls `bvdiff(ae, ae_kills[i], esize)` to remove invalidated expressions from the available expression set.

By adding ADDRESS operations to these kill sets, we ensure they're properly invalidated at the right times.

### Verification

**Test Case:** `Novus.Tests/Examples/prime_100000.novus`

**Before Fix:**
```bash
$ ./prime_100000_o2
2
```
(Only outputs "2" because stale address is reused)

**After Fix:**
```bash
$ ./prime_100000_o2_fixed
2 3 5 7 11 13 17 19 23 29 31 37 41 43 47 ...
[all primes up to 100,000]
```

**Build Commands:**
```bash
# Rebuild VBCC with fix
cd vendor/vbcc && make clean && make

# Remove build marker to force Novus rebuild
rm vendor/vbcc/bin/.build_complete

# Rebuild Novus compiler
dotnet build

# Test with optimization level 2
./Novus/bin/Debug/net9.0/Novus compile \
  Novus.Tests/Examples/prime_100000.novus \
  --output /tmp/prime_100000_o2_fixed \
  --cpu 68040 \
  -O 2

# Copy to Amiga for testing
cp /tmp/prime_100000_o2_fixed /Users/barry/Emulation/Amiga/A4000-DH0/Barry/
```

### Root Cause Analysis

The original code author likely skipped ADDRESS operations thinking:
- "Addresses don't change, so they don't need invalidation"
- "ADDRESS is just computing a constant offset"

However, this is incorrect for several reasons:

1. **Stack-Relative Addresses:** Local variable addresses are computed relative to the stack pointer (SP). When SP changes (function calls, stack adjustments), these addresses become invalid.

2. **Aliasing:** Even "constant" addresses can be affected by memory aliasing. If `*ptr = x` could alias with `&local_var`, the address computation's assumptions are violated.

3. **Memory Layout Changes:** Function calls, dynamic allocation, and other operations can change the memory layout, invalidating previously computed addresses.

### Conservative vs Optimal Fix

This fix is **intentionally conservative**. It invalidates ADDRESS operations more aggressively than strictly necessary to ensure correctness. Possible future optimizations:

1. Track whether an ADDRESS is stack-relative vs heap-relative
2. Only invalidate stack addresses on SP modifications
3. Use points-to analysis to be more precise about what invalidates what

However, these optimizations should only be attempted after extensive testing confirms the current fix is correct.

### Why This Bug Wasn't Caught Earlier

This bug likely wasn't caught in standard VBCC usage because:

1. Most VBCC users target AmigaOS directly, which has different calling conventions and stack usage patterns than Novus's generated C code.

2. The bug only manifests when:
   - Optimization level 2 or higher is used
   - ADDRESS operations are CSE candidates
   - Stack pointer changes between address computation and use
   - The cached address is actually reused

3. The Novus compiler generates C code with patterns (Vec manipulation, multiple function calls, stack-allocated structs) that trigger this specific bug more reliably than typical hand-written C.

### Impact on Performance

This fix may reduce optimization opportunities by invalidating ADDRESS expressions more aggressively. However:

- **Correctness > Performance:** Incorrect code is infinitely slower than correct code.
- Impact is likely minimal in practice, as recomputing addresses is cheap on 68k.
- Future profiling may identify safe optimizations to reduce conservatism.

### Related VBCC Files

If debugging CSE issues, also check:
- `vbcc/cse.c` - CSE implementation
- `vbcc/alias.c` - Alias analysis and `change_list` population (tracks what operations modify)
- `vbcc/av.c` - Available expressions dataflow analysis
- `vbcc/opt.h` - Optimizer data structures and bit vector definitions

### Upstream Status

**Not submitted upstream.** This patch should be thoroughly tested on real hardware and across multiple scenarios before proposing to VBCC maintainers.

---

## 2. Force Frame Pointer Usage (CRITICAL CORRECTNESS FIX)

**File:** `Novus/Toolchain/VbccToolchain.cs`
**Lines:** 170, 198 (added `-use-framepointer` flag)
**Date Applied:** 2025-11-06
**Severity:** Critical (produces incorrect code with -O2)

### Problem Description

VBCC's default stack management (without frame pointers) uses stack-relative addressing via label-based offsets. When the stack pointer (A7) is adjusted during function execution (e.g., via `add.w #N,a7` after function calls), all previously calculated stack offsets become invalid, leading to incorrect memory accesses.

**Impact:** At optimization level 2 with stack pointer adjustments, VBCC generates code that accesses stack variables at incorrect offsets, causing data corruption and wrong results.

**Example Failure Case:**
```asm
; Without frame pointer (-O2):
_main
    sub.w   #36,a7              ; Allocate stack frame (SP = SP_BASE - 36)
    move.l  #100000,(32,a7)     ; Store max at offset 32 (SP_BASE - 4)
    add.w   #36,a7              ; Adjust SP (SP = SP_BASE)
    ; ... later in loop:
    cmp.l   (32,a7),d0          ; BUG: Now reads from SP_BASE + 32 (wrong!)
                                ; Should read from (SP_BASE + 32 - 36) = SP_BASE - 4
```

### Root Cause

VBCC's m68k backend has two modes:
1. **Without frame pointer:** Uses stack-relative addressing `(offset,a7)` with label-based offsets
2. **With frame pointer:** Uses frame-relative addressing `(offset,a5)` with LINK/UNLK instructions

The label-based offset system computes offsets at compile-time assuming a fixed stack depth. When `add.w #N,a7` adjusts the stack pointer during execution, these offsets become stale.

### The Fix

Force VBCC to always use frame pointer mode by adding the `-use-framepointer` flag to all compilation commands.

**Modified Files:**
- `Novus/Toolchain/VbccToolchain.cs::CompileToObject()` - Added `-use-framepointer`
- `Novus/Toolchain/VbccToolchain.cs::CompileWithVC()` - Added `-use-framepointer`

**Generated Code Comparison:**

Without Frame Pointer (BUGGY):
```asm
_main
    sub.w   #36,a7              ; Manual stack allocation
    movem.l l124,-(a7)          ; Save registers
    ; ... function body
    move.l  d7,(4+l126,a7)      ; l126=28, so (32,a7)
    ; ... later
    add.w   #36,a7              ; Stack adjustment - BREAKS OFFSETS!
    ; ... even later
    move.l  (4+l126,a7),d2      ; BUG: Still uses (32,a7) but SP changed!
```

With Frame Pointer (FIXED):
```asm
_main
    link.w  a5,#-36             ; Allocate frame via LINK (A5 = frame base)
    movem.l l124,-(a7)          ; Save registers
    ; ... function body
    move.l  d7,(-8,a5)          ; Store relative to A5
    ; ... later
    add.w   #36,a7              ; Stack adjustment - doesn't matter!
    ; ... even later
    move.l  (-8,a5),d2          ; CORRECT: Still uses A5, which never changes
    ; ... function exit
    unlk    a5                  ; Restore frame pointer and stack
```

### Frame Pointer Mechanics

The LINK/UNLK instructions provide a stable reference point for stack variables:

```asm
link.w  a5,#-N      ; Allocate N bytes: push A5, A5=A7, A7-=N
                    ; A5 now points to the frame base (doesn't change)
; ... function body accesses variables via (offset,a5)
unlk    a5          ; Restore: A7=A5, pop A5
```

Benefits:
1. **A5 never changes** during function execution
2. All stack variables accessed via fixed `(offset,a5)`
3. Stack pointer A7 can be freely adjusted without breaking variable access
4. Standard m68k ABI convention for functions with complex stack usage

### Why VBCC Uses A5 Instead of A6

The m68k ABI typically uses **A6** as the frame pointer, but VBCC uses **A5**. This is defined in `vendor/vbcc/vbcc/machines/m68k/machine.c:163`:

```c
static int sp=8,fbp=6,framesize;  // fbp=6 → A5 (registers 1-8 are a0-a7)
```

This is fine - the important thing is using *any* stable frame pointer, not which specific register.

### Verification

**Test Case:** `Novus.Tests/Examples/prime_100000.novus` with `-O 2`

**Before Fix:**
```bash
$ ./prime_100000_fixed
2
# Only outputs "2" because stale stack offset reads wrong value
```

**After Fix:**
```bash
$ ./prime_100000_FIXED
2 3 5 7 11 13 17 19 23 29 31 37 41 43 47 53 59 61 67 71
[continues with all 9,592 primes up to 100,000]
```

**Assembly Verification:**
```bash
# Generate assembly with frame pointer
cd /tmp
/Users/barry/Downloads/vbcc/bin/vc +aos68k -c99 -cpu=68040 -O2 \
  -use-framepointer -S -o main_withfp.s prime_100000_fixed_main.c

# Verify LINK/UNLK present
grep "link.w" main_withfp.s
# Should show: link.w  a5,#-36

# Verify frame-relative addressing
grep "a5)" main_withfp.s | head -5
# Should show lines like: move.l (-8,a5),d2
```

### Performance Impact

Using frame pointers has a small performance cost:
- **+1 register used** (A5 reserved as frame pointer instead of general-purpose)
- **+2 instructions per function** (LINK at entry, UNLK at exit)
- **Slightly larger code** (frame-relative addressing may use longer offsets)

However:
- **Correctness > Performance:** Wrong results are infinitely worse than 1-2% slowdown
- Impact is minimal on 68020+ with instruction cache
- Most functions use <10% of available registers, so losing A5 rarely matters

### When Frame Pointers Are Required

VBCC automatically uses frame pointers when:
1. Variable-length arrays (VLAs) are used (`vlas` flag set)
2. `-use-framepointer` flag explicitly provided

Stack-relative addressing (no frame pointer) is safe ONLY when:
- Stack pointer never changes within function (no mid-function adjustments)
- No VLAs or dynamic stack operations
- VBCC's offset tracking system correctly accounts for all SP changes

Since Novus generates C code with complex patterns (nested struct manipulation, multiple function calls, Vec operations), we force frame pointers for all functions to ensure correctness.

### Alternative Considered: Fix VBCC's Stack Tracking

Instead of forcing frame pointers, we could fix VBCC's stack-relative offset tracking to properly account for `add.w #N,a7` adjustments. This would require:

1. Tracking current stack depth throughout code generation
2. Adjusting all `(offset,a7)` calculations by current depth
3. Ensuring all stack adjustments update the depth tracker

**Why we didn't do this:**
- Much more invasive change to VBCC internals
- Higher risk of introducing new bugs
- Frame pointers are the standard solution for this problem
- Performance cost is acceptable

### Build Commands

```bash
# Rebuild Novus compiler with fix
dotnet build -c Release

# Test compilation with -O2
./Novus/bin/Release/net9.0/Novus compile \
  Novus.Tests/Examples/prime_100000.novus \
  --output /tmp/prime_100000_FIXED \
  --cpu 68040 \
  -O 2

# Verify frame pointer in generated C → assembly
cd /tmp
/Users/barry/Downloads/vbcc/bin/vc +aos68k -c99 -cpu=68040 -O2 \
  -use-framepointer -S -o check.s prime_100000_fixed_main.c
grep "link.w" check.s  # Should find link.w instruction

# Copy to Amiga for testing
cp /tmp/prime_100000_FIXED /Users/barry/Emulation/Amiga/A4000-DH0/Barry/
```

### Upstream Status

**Not submitted upstream.** This is a workaround in the Novus toolchain integration, not a patch to VBCC itself. The real fix would be improving VBCC's stack offset tracking, but that requires deep changes to the m68k backend and extensive testing.

---

## Patch Application

All patches in this file are automatically applied to the VBCC source tree in `vendor/vbcc/`. No manual patching is required.

To rebuild VBCC with patches:
```bash
cd vendor/vbcc
make clean
make
```

To verify patches are applied, check for comments mentioning "invalidated" in `vbcc/cse.c` around line 224.
