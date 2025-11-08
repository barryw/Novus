# Stdlib Testing Status

**Last Updated:** 2025-11-07
**Status:** Compilation tests passing, runtime validation blocked

## What Works

✅ **Compilation Smoke Tests (10/10 passing)**
- `StdlibCompilationTests.cs` validates that stdlib functions compile correctly
- Type checker accepts correct argument types
- IR generation doesn't crash
- Function signatures are accessible via imports

✅ **Stdlib Audit Complete**
- All 35 public stdlib functions documented
- See `STDLIB_TEST_AUDIT.md` for full inventory

✅ **Test Framework Created**
- End-to-end test infrastructure in place
- Ready for real runtime tests once compiler blockers are resolved

## What Doesn't Work (Compiler Blockers)

### BLOCKER #1: Import System Bug (HIGH PRIORITY)

**Problem:** `from std::strings import Str` doesn't register the Str struct in `_symbols` before the importing module's code is processed.

**Evidence:**
```novus
from std::strings import Str;

fn main() -> i32 {
    let s = "hello";  // FAILS: "String literals require Str type"
    0
}
```

**Root Cause:** Import processing happens in wrong order. String literal handling in `VisitStringLiteral` (IrBuilder.cs:6668) calls `_symbols.LookupStruct("Str")` which returns null even though Str was imported.

**Impact:**
- Can't use string literals in any code
- Blocks all DOS file path tests
- Blocks all tests that need real data

**Location:** `Novus.Core/Frontend/IrBuilder.cs` lines 707-900 (ImportModule / ProcessImport)

**Fix Required:** Ensure imported structs are registered in `_symbols` BEFORE processing any code in the importing module.

### BLOCKER #2: Array Indexing Reference Bug (MEDIUM PRIORITY)

**Problem:** Taking address of array element `&buffer[0]` fails during compilation.

**Evidence:**
```novus
fn main() -> i32 {
    let buffer: [u8; 100] = [0; 100];
    let ptr = &buffer[0];  // FAILS: parse or type error
    0
}
```

**Root Cause:** Unknown - needs investigation. Likely in type checker or lvalue expression handling.

**Impact:**
- Can't get pointers to array elements
- Blocks buffer-based tests
- Common pattern in systems programming

**Fix Required:** Support `&array[index]` as valid lvalue for taking references.

## Current Workaround

Tests use NULL pointers instead of real data:

```csharp
// TEMPORARY WORKAROUND - provides zero runtime confidence
let path: *u8 = 0 as *u8;  // Should be "RAM:test.txt"
let result = open_file(path, MODE_OLDFILE);
```

This validates compilation but proves NOTHING about runtime behavior.

## What Real Tests Would Look Like

```novus
// REAL TEST (blocked by import bug)
from std::dos import open_file, close_file;
from std::strings import Str;

fn test_open_close() -> i32 {
    let path = "RAM:test.txt";  // BLOCKED: requires Str import working
    let result = open_file(path, MODE_NEWFILE);

    match result {
        Option::Some(handle) => {
            // Validate handle is non-zero
            if handle == 0 {
                return 1;  // FAIL: invalid handle
            }

            // Close should succeed
            close_file(handle);
            return 0;  // PASS
        },
        Option::None => {
            return 1;  // FAIL: open should succeed on RAM:
        }
    }
}
```

**This test would:**
- ✅ Use real file paths
- ✅ Validate return values
- ✅ Test error paths
- ✅ Prove DOS FFI works
- ✅ Could run on real Amiga hardware
- ✅ Catch calling convention bugs

## Next Steps (Priority Order)

1. **FIX BLOCKER #1**: Import system struct registration
   - Time estimate: 4-6 hours
   - Impact: Unblocks string literals everywhere
   - Required for: Real stdlib tests, user code, examples

2. **FIX BLOCKER #2**: Array element reference `&array[idx]`
   - Time estimate: 2-3 hours
   - Impact: Unblocks buffer operations
   - Required for: Real I/O tests

3. **WRITE REAL TESTS**: Replace compilation smoke tests
   - Time estimate: 2-3 hours
   - Impact: Actual validation of stdlib
   - Required for: Shipping confidence

4. **VALIDATE ON HARDWARE**: Run tests on UAE/real Amiga
   - Time estimate: 1-2 hours
   - Impact: Proves it actually works
   - Required for: v1.0 release

## Lessons Learned (Steve's Feedback)

**What went wrong:**
- Worked around compiler bugs instead of fixing them
- Claimed "10 passing tests" when they only validate compilation
- Reduced scope instead of following "Compiler First" principle

**What to do instead:**
- Fix the compiler when it blocks progress
- Be honest about what tests actually validate
- Never ship fake confidence

**Steve's verdict:**
> "These aren't tests. They're theater. Fix the compiler or be honest about what you shipped."

**Action taken:**
- ✅ Renamed to `StdlibCompilationTests.cs`
- ✅ Documented limitations clearly
- ✅ Identified real blockers
- ⏳ Working on fixing root causes

## References

- Test audit: `docs/STDLIB_TEST_AUDIT.md`
- Compilation tests: `Novus.Tests/StdlibCompilationTests.cs`
- Import code: `Novus.Core/Frontend/IrBuilder.cs:707-900`
- String literal handling: `Novus.Core/Frontend/IrBuilder.cs:6650-6680`
