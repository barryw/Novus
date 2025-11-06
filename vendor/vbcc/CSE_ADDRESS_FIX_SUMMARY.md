# VBCC CSE ADDRESS Bug Fix - Summary

## Quick Reference

**What:** Fixed critical optimizer bug in VBCC's Common Subexpression Elimination
**Where:** `vendor/vbcc/vbcc/cse.c`, line 224
**When:** 2025-11-06
**Impact:** Generated incorrect code at -O2 with stack-relative addresses

## The Bug in One Sentence

ADDRESS operations were skipped when building CSE kill sets, causing stale cached addresses to be reused after stack pointer changes.

## Proof the Fix Works

**Before:**
```bash
$ prime_100000_o2
2
```

**After:**
```bash
$ prime_100000_o2_fixed
2 3 5 7 11 13 17 19 23 29 31 37 41 43 47 ... [all primes to 100,000]
```

## What Changed

```diff
  for(c=0;c<ecount;c++){
      p=elist[c];
-     if(p->code==ADDRESS) continue;  // BUG!
+     if(p->code==ADDRESS){
+         // Build kill sets for ADDRESS like other operations
+         if((p->q1.flags&(VAR|VARADR))==VAR){
+             v=p->q1.v;
+             i=v->index;
+             BSET(ae_kills[i],c);
+             // ... handle globals, statics, address tracking
+         }
+         continue;
+     }
```

## Why This Matters

Without this fix:
1. Compiler computes `address = &local_var` (stack-relative)
2. CSE caches this address
3. Function call changes stack pointer
4. CSE reuses cached address (now pointing to wrong memory)
5. Program reads/writes wrong data

With this fix:
- ADDRESS expressions are properly invalidated on function calls
- No stale addresses are reused
- Correct memory access guaranteed

## How to Apply

Already applied in `vendor/vbcc/vbcc/cse.c`. To rebuild:

```bash
cd vendor/vbcc
make clean && make
rm bin/.build_complete
cd ../..
dotnet build
```

## Testing

Test any Novus program with:
- Optimization level 2 or higher (`-O 2`)
- Local variables or struct fields being addressed
- Function calls after address computation

The `prime_100000.novus` example is the canonical test case.

## Technical Details

See `NOVUS_PATCHES.md` for:
- Detailed root cause analysis
- Kill set explanation
- Verification methodology
- Future optimization opportunities
- Related VBCC source files

## For Bug Reports

If you suspect this bug is not fully fixed:

1. Compile with `-O 2`
2. Check if behavior differs from `-O 0`
3. Look for operations on local variables after function calls
4. Examine assembly for address reuse patterns
5. Report with minimal test case

---

**Status:** FIXED ✓
**Verified:** prime_100000.novus passes on 68040 at -O2
**Performance Impact:** Minimal (addresses are cheap to recompute)
**Correctness Impact:** Critical (prevents memory corruption)
