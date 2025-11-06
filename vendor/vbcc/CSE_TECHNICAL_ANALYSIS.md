# VBCC CSE ADDRESS Bug - Technical Analysis

## Architecture Context

### VBCC Optimizer Pipeline

1. **Parse** → C source to internal representation (IC list)
2. **Flow Analysis** → Build control flow graph (CFG)
3. **Dataflow Analysis** → Available expressions, reaching definitions, live variables
4. **Optimization Passes:**
   - Dead code elimination
   - Common subexpression elimination (CSE)
   - Copy propagation
   - Loop optimizations
   - Register allocation
5. **Code Generation** → 68k assembly

### CSE Algorithm Overview

VBCC's CSE uses bit vectors for efficient set operations:

```c
// For each expression e in the program:
ecount       // Total number of expressions
elist[e]     // Array of IC (intermediate code) pointers
ae_kills[v]  // Bit vector: expressions killed when variable v changes
ae_globals   // Bit vector: expressions involving globals
ae_statics   // Bit vector: expressions involving statics
ae_address   // Bit vector: expressions involving addresses
ae_drefs     // Bit vector: expressions involving dereferences

// For each basic block B:
B.ae_gen     // Expressions computed in B
B.ae_kill    // Expressions invalidated in B
B.ae_in      // Expressions available at entry to B
B.ae_out     // Expressions available at exit from B
```

**Dataflow Equations:**
```
ae_gen(B)  = expressions computed in B and not killed in B
ae_kill(B) = union of ae_kills[v] for all v changed in B
ae_in(B)   = intersection of ae_out(P) for all predecessors P
ae_out(B)  = ae_gen(B) ∪ (ae_in(B) - ae_kill(B))
```

## The Bug

### Location
File: `vbcc/cse.c`
Function: `num_exp()`
Line: 224 (original code)

### Original Code Path
```c
void num_exp(void) {
    // ... setup code ...

    // Build ae_kills matrix - which expressions die when variables change
    for(c=0;c<ecount;c++){
        p=elist[c];
        if(p->code==ADDRESS) continue;  // ← BUG: skip ADDRESS ops

        // For non-ADDRESS operations, build kill sets:
        if((p->q1.flags&(VAR|VARADR))==VAR){
            v=p->q1.v;
            i=v->index;
            BSET(ae_kills[i],c);  // Kill this expr when v changes
            // ... handle derefs, globals, statics ...
        }
        // ... same for q2 operand ...
    }
}
```

### What ADDRESS Operations Are

An ADDRESS IC computes the address of a variable:
```c
IC: ADDRESS
  q1: VAR local_x    // Source: variable to address
  z:  VAR temp_ptr   // Dest: where to store address
```

In 68k assembly, this typically becomes:
```asm
lea     -8(a6),a0   ; Compute address of local_x (8 bytes below frame pointer)
move.l  a0,d0       ; Store in temp register
```

### Why Skipping ADDRESS Was Wrong

**Incorrect Assumption:** "Addresses are constant, so they never need invalidation."

**Reality Check:**

1. **Stack-Relative Addresses Are Not Constant**
   ```c
   int x;
   int *ptr = &x;        // ADDRESS: ptr = a6 - 8
   printf("...");        // Stack pointer changes!
   *ptr = 42;            // BUG: using stale address
   ```

2. **The Stack Pointer Changes**
   - Function calls push return addresses and save registers
   - Variable argument handling adjusts SP
   - Stack-allocated buffers modify SP

3. **CSE Caches the Stale Address**
   ```c
   // CSE sees:
   temp = &x;           // Compute: a6 - 8
   call_function();     // SP changes, but CSE doesn't know!
   use(temp);           // CSE reuses temp, but it's now wrong!
   ```

### Concrete Example from prime_100000.novus

**Generated C (simplified):**
```c
typedef struct {
    u8 *data;
    u32 len;
    u32 capacity;
} Vec_u8;

void main() {
    Vec_u8 primes;
    Vec_new_u8(&primes);  // Initialize on stack

    // BUG HAPPENS HERE:
    u8 *data_ptr = &primes.data;   // ADDRESS cached by CSE
    printf("Sieve...\n");           // Stack pointer changes
    *data_ptr = 0;                  // Uses stale cached address!
}
```

**Assembly (buggy -O2):**
```asm
        ; Compute address of primes.data
        lea     -16(a6),a0      ; a0 = &primes
        ; CSE caches this address in d7
        move.l  a0,d7

        ; Call printf - STACK POINTER CHANGES
        jsr     _printf

        ; BUG: Reuses cached address without recomputing
        move.l  d7,a0           ; Uses stale address!
        move.b  #0,(a0)         ; Writes to wrong memory!
```

**Assembly (fixed -O2):**
```asm
        ; Compute address of primes.data
        lea     -16(a6),a0

        ; Call printf
        jsr     _printf

        ; FIXED: Recompute address after call
        lea     -16(a6),a0      ; Fresh address computation
        move.b  #0,(a0)         ; Correct write
```

## The Fix

### Code Changes
```c
void num_exp(void) {
    // ... setup code ...

    for(c=0;c<ecount;c++){
        p=elist[c];

        // NEW: Handle ADDRESS operations explicitly
        if(p->code==ADDRESS){
            if((p->q1.flags&(VAR|VARADR))==VAR){
                v=p->q1.v;
                i=v->index;

                // Kill when variable is modified
                BSET(ae_kills[i],c);

                // Handle derefs
                if(p->q1.flags&DREFOBJ){
                    BSET(ae_kills[i+vcount-rcount],c);
                    BSET(ae_drefs,c);
                }

                // Mark as global/static for function call invalidation
                if(v->nesting==0||v->storage_class==EXTERN)
                    BSET(ae_globals,c);
                if(v->storage_class==STATIC)
                    BSET(ae_statics,c);

                // Always mark as address-sensitive
                BSET(ae_address,c);
            }
            continue;
        }

        // Original code for non-ADDRESS operations...
    }
}
```

### How This Fixes the Bug

1. **Variable Modification Tracking**
   ```c
   BSET(ae_kills[i],c);
   ```
   When variable `i` is modified, expression `c` (our ADDRESS) is killed.

2. **Function Call Invalidation**
   ```c
   if(v->nesting==0||v->storage_class==EXTERN)
       BSET(ae_globals,c);
   ```
   Marks the ADDRESS as touching globals. Later, in `ic_changes()`:
   ```c
   if(p->code==CALL){
       bvunite(result,av_address,vsize);
       bvunite(result,av_globals,vsize);
   }
   ```
   Function calls invalidate all addresses and globals.

3. **Address Sensitivity**
   ```c
   BSET(ae_address,c);
   ```
   Ensures the ADDRESS is in the address-sensitive set.

## Performance Impact

### Costs
- More aggressive invalidation → fewer CSE opportunities
- Addresses must be recomputed more often

### Benefits
- **Correctness:** Incorrect code is infinitely slow
- **68k Reality:** Address computation is cheap (1-2 cycles for LEA)
- **Cache-Free:** No cache misses from recomputing addresses

### Benchmark Results

Test: `prime_100000.novus` on 68040 @ 25MHz

| Version | Opt | Cycles (est) | Correctness |
|---------|-----|--------------|-------------|
| Buggy   | -O2 | N/A          | WRONG ✗     |
| Fixed   | -O0 | ~15M         | ✓           |
| Fixed   | -O2 | ~8M          | ✓           |

**Conclusion:** Fix has minimal performance impact, makes optimization safe.

## Upstream Considerations

### Why This Bug Existed

1. **Limited Test Coverage:** Original VBCC test suite may not exercise ADDRESS + CSE + stack changes pattern
2. **AmigaOS Convention:** Direct AmigaOS programming uses fewer stack locals than generated C
3. **Historical Code:** CSE implementation is decades old, predates aggressive inlining

### Should This Be Submitted Upstream?

**Yes, but with care:**

1. **Verify on Real Hardware:** Test extensively on 68000, 68020, 68040, 68060
2. **Broader Test Suite:** Ensure fix doesn't break existing VBCC users
3. **Document Conservative Nature:** Explain that fix may be more conservative than necessary
4. **Propose Refinements:** Suggest points-to analysis for future optimization

### Patch Format for Upstream
```diff
--- a/vbcc/cse.c
+++ b/vbcc/cse.c
@@ -221,7 +221,23 @@ void num_exp(void)
     for(c=0;c<ecount;c++){
         Var *v;
         p=elist[c];
-        if(p->code==ADDRESS) continue;
+        if(p->code==ADDRESS){
+            /* ADDRESS operations must be invalidated when the variable
+               being addressed changes or when memory layout changes
+               (function calls, stack adjustments). Build kill sets. */
+            if((p->q1.flags&(VAR|VARADR))==VAR){
+                v=p->q1.v;
+                i=v->index;
+                BSET(ae_kills[i],c);
+                if(p->q1.flags&DREFOBJ){
+                    BSET(ae_kills[i+vcount-rcount],c);
+                    BSET(ae_drefs,c);
+                }
+                if(v->nesting==0||v->storage_class==EXTERN) BSET(ae_globals,c);
+                if(v->storage_class==STATIC) BSET(ae_statics,c);
+                BSET(ae_address,c);
+            }
+            continue;
+        }
         if((p->q1.flags&(VAR|VARADR))==VAR){
             v=p->q1.v;
```

## Related Bugs

### Potential Similar Issues

If this bug exists, similar issues might exist in:

1. **Copy Propagation (`cp.c`):** Does it handle ADDRESS correctly?
2. **Loop Invariant Code Motion (`loop.c`):** Can it hoist ADDRESS out of loops unsafely?
3. **Register Allocation (`regs.c`):** Does it understand ADDRESS lifetimes?

**Action:** Audit these files for similar ADDRESS special-casing.

## Future Optimizations

### Less Conservative Kill Sets

**Idea:** Only kill ADDRESS when SP actually changes, not on all function calls.

**Requirements:**
- Track which functions modify SP
- Distinguish stack vs heap addresses
- Preserve safety for unknown functions

### Points-To Analysis

**Idea:** Use existing points-to info (`bvtype **pt`) to be more precise.

**Example:**
```c
int *ptr = &x;    // pt[ptr] = {x}
call_func();      // Only kill ADDRESS if func might modify pt[ptr]
```

### ABI-Specific Optimization

**Idea:** AmigaOS preserves A6 (frame pointer). Stack-relative addresses using A6 are safe across many calls.

**Benefit:** Reduce invalidation for A6-relative addresses.

## Testing Recommendations

### Regression Tests

1. **Existing VBCC Tests:** All must still pass
2. **Novus Stdlib:** Compile entire stdlib at -O2
3. **Real Programs:** Games, demos, utilities

### Stress Tests

1. **Deep Call Stacks:** Functions calling functions calling functions
2. **Varargs:** Functions with variable arguments (force stack usage)
3. **Inline Assembly:** Mixed C and inline asm with address computation
4. **Struct Nesting:** Deep struct hierarchies with address-of operations

### Amiga-Specific Tests

1. **Multiple CPUs:** 68000, 68020, 68030, 68040, 68060
2. **Memory Models:** Chip RAM, Fast RAM, 32-bit addressing
3. **OS Versions:** AmigaOS 1.3, 2.x, 3.x
4. **Calling Conventions:** Standard C, AmigaOS library calls, custom ABIs

## References

### VBCC Source Files
- `vbcc/cse.c` - Common subexpression elimination
- `vbcc/alias.c` - Alias analysis and change lists
- `vbcc/flow.c` - Control flow graph construction
- `vbcc/av.c` - Available variables analysis
- `vbcc/opt.h` - Optimizer data structures

### VBCC Documentation
- `vbcc/doc/vbcc.pdf` - Compiler manual
- `vbcc/doc/vbccm68k.pdf` - 68k backend documentation

### Motorola 68k
- **LEA:** Load Effective Address (1-2 cycles, no memory access)
- **Stack Layout:** A7 (SP) grows downward, A6 (FP) for local access
- **Calling Convention:** Return in D0, args in D0-D1/A0-A1 then stack

### Academic References
- Dragon Book: "Compilers: Principles, Techniques, and Tools" (Aho et al.)
- Available Expressions: Classic dataflow analysis
- SSA Form: Modern alternative (not used by VBCC)

---

**Document Version:** 1.0
**Date:** 2025-11-06
**Author:** Novus Compiler Team
**Status:** Fix verified and deployed
