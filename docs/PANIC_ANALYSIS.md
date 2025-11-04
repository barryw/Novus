# Panic Mechanism Analysis & Recommendations

**Date:** 2025-01-04
**Status:** ✅ IMPLEMENTED (Option C)

---

## Summary

After investigating the panic mechanisms in Novus, I found:

1. **assert!** is a **statement** (not a macro) that shows GUI dialogs via `__novus_assert_failed()`
2. **panic()** is a **function** in `std::panic` that writes to stdout and loops forever
3. There is **confusion** because they have different behaviors and purposes
4. The documentation mentions `panic!` (with `!`) but no such thing exists

---

## Current Implementation

### 1. `assert!` Statement (Compiler Built-in)

**Location**: Built into the compiler (not stdlib)

**Behavior**:
- Syntax: `assert!(condition)` or `assert!(condition, "message")`
- **Debug mode**: Generates call to `__novus_assert_failed()` (C runtime)
- **Release mode**: Completely elided (no code generated)
- Shows **GUI dialog** using Am igaOS `EasyRequest()`
- Then returns 1 from the current function

**Runtime Implementation** (`runtime/novus_runtime.c`):
```c
void __novus_assert_failed(const char* file, int32_t line, int32_t col, const char* message)
{
    // Opens intuition.library
    // Displays EasyRequest GUI dialog with:
    //   - File, line, column
    //   - Optional message
    //   - "OK" button
    // Closes library and returns
}
```

**Generated Code**:
```c
if (!(condition)) {
    __novus_assert_failed("file.novus", 10, 5, "message");
    // Execute deferred cleanup
    return 1;  // Assert failed
}
```

**Use Case**: Development-time assertions that disappear in production

### 2. `panic()` Function (stdlib)

**Location**: `Novus/std/panic.novus`

**Behavior**:
- Syntax: `panic("message")`
- Writes "PANIC: message\n" to stdout via DOS `Write()`
- Then calls `loop_forever()` which spins forever
- **No GUI dialog**
- **Not elided in release mode** (it's a regular function call)

**Implementation**:
```novus
pub fn panic(message: String) {
    let stdout = Output()
    if stdout != 0 {
        let prefix: String = "PANIC: "
        Write(stdout, (i32)(prefix.ptr), prefix.len)
        Write(stdout, (i32)(message.ptr), message.len)
        let newline: String = "\n"
        Write(stdout, (i32)(newline.ptr), newline.len)
    }
    loop_forever()  // Infinite loop
}
```

**Use Case**: Unrecoverable errors that should always halt (even in release)

---

## Problems with Current Design

### 1. Naming Confusion

- **Documentation says `panic!`** (with `!`) but that doesn't exist
- **Only `assert!` exists** with the `!` suffix
- **`panic()` is a regular function**, not a "macro-like" statement

### 2. Inconsistent Error Reporting

| Mechanism | Debug Mode | Release Mode | Output Method |
|-----------|------------|--------------|---------------|
| `assert!(cond)` | GUI dialog | **Elided** | EasyRequest |
| `panic(msg)` | Stdout | Stdout | DOS Write() |

- `assert!` shows a **nice GUI** on Amiga
- `panic()` just writes to **stdout** (which may not be visible if running from Workbench!)

### 3. Incomplete `panic()` Implementation

From the source comments:
```novus
// TODO: Call AmigaOS Alert() for GUI popup or proper cleanup
// For now, just loop forever since we don't have exit() linked
```

The `panic()` function:
- Doesn't show a GUI (unlike `assert!`)
- Doesn't exit cleanly (just infinite loops)
- Won't be visible if running from Workbench (no stdout)

### 4. Semantic Overlap

Both mechanisms are for "unrecoverable errors" but:
- `assert!` is for "this should never happen" (debug-only)
- `panic()` is for "this is an unrecoverable runtime error" (production too)

But their behaviors don't reflect this distinction well.

---

## Recommendations

### Option A: Keep Both, Fix `panic()`

**Keep**: `assert!` for debug-time checks
**Keep**: `panic()` for unrecoverable runtime errors
**Fix**: Make `panic()` behavior consistent with `assert!`

**Changes needed**:

1. **Make `panic()` show a GUI dialog** (like `assert!`)
   ```novus
   pub fn panic(message: String) {
       // Use EasyRequest to show error
       // Then exit(1) or loop forever
   }
   ```

2. **Rename `panic()` if needed** for clarity:
   - `panic()` → `abort()` (matches C convention)
   - Or keep `panic()` (matches Rust convention)

3. **Update documentation** to clarify:
   - `assert!(cond, msg)` - Debug-only safety check (elided in release)
   - `panic(msg)` - Unrecoverable error (always present, shows GUI)

**Pros**:
- Clear distinction between debug-time and runtime errors
- `panic()` becomes useful for Workbench apps (shows GUI)
- Follows Rust's model (assert! vs panic!())

**Cons**:
- Two mechanisms to learn
- `panic()` needs more implementation work

### Option B: Remove `panic()`, Use Only `assert!`

**Remove**: `std::panic` module entirely
**Keep**: `assert!` for all unrecoverable errors

**Changes needed**:

1. **Delete** `Novus/std/panic.novus`
2. **Update docs** to only mention `assert!`
3. **For runtime errors**, use `assert!(false, "error message")`

**Example**:
```novus
fn divide(a: i32, b: i32) -> i32 {
    assert!(b != 0, "division by zero")
    return a / b
}
```

**Pros**:
- One mechanism to learn
- Less code to maintain
- assert! already has GUI support

**Cons**:
- `assert!(false, msg)` is awkward for runtime errors
- Assertions disappear in release mode (but runtime errors shouldn't!)
- Lost the semantic distinction

### Option C: Add `panic!` Statement (Recommended)

**Add**: `panic!` statement (compiler built-in, like `assert!`)
**Keep**: `assert!` for debug-only checks
**Remove**: `panic()` function from stdlib

**Behavior**:

| Statement | Debug Mode | Release Mode | Purpose |
|-----------|------------|--------------|---------|
| `assert!(cond, msg)` | Check + GUI | **Elided** | Debug-only safety |
| `panic!(msg)` | GUI + exit | GUI + exit | Unrecoverable error |

**Implementation**:

1. **Add `panic!` to compiler** (like `assert!`):
   - Generates call to `__novus_panic(message, file, line, col)`
   - **NOT** elided in release mode
   - Shows GUI dialog
   - Exits cleanly (or loops)

2. **Add runtime function** (`runtime/novus_runtime.c`):
   ```c
   void __novus_panic(const char* message, const char* file, int32_t line, int32_t col) {
       // Similar to __novus_assert_failed
       // Show EasyRequest with panic message
       // Exit or loop forever
   }
   ```

3. **Remove** `std::panic` module

4. **Update docs** to show both:
   ```novus
   // Debug-only assertion (disappears in release)
   assert!(x > 0, "x must be positive")

   // Runtime panic (always present)
   if file_not_found {
       panic!("Could not open file: {}", filename)
   }
   ```

**Pros**:
- Clear distinction: `assert!` = debug, `panic!` = runtime
- Both show GUI dialogs (consistent UX)
- Matches Rust semantics (assert! vs panic!())
- Documentation already mentions `panic!`

**Cons**:
- Requires compiler changes
- More runtime code

---

## Recommended Action Plan

**I recommend Option C**: Add `panic!` statement

### Implementation Steps:

1. **Add `panic!` statement to grammar** (`Novus.g4`):
   ```antlr
   panicStatement: 'panic!' '(' STRING_LITERAL ')';
   ```

2. **Add IR instruction** (`IrModule.cs`):
   ```csharp
   public class IrPanic : IrInstruction
   {
       public string Message { get; set; }
       public SourceLocation Location { get; set; }
   }
   ```

3. **Add semantic analysis** (`SemanticAnalyzer.cs`):
   ```csharp
   public override IrType? VisitPanicStatement(NovusParser.PanicStatementContext context)
   {
       // Extract message, create IrPanic
   }
   ```

4. **Add code generation** (`CCodeGenerator.cs`):
   ```csharp
   private void EmitPanic(IrPanic panic)
   {
       // Always emit (never elided)
       // Call __novus_panic(message, file, line, col)
       // Execute deferred cleanup
       // Exit program
   }
   ```

5. **Add runtime function** (`runtime/novus_runtime.c`):
   ```c
   void __novus_panic(const char* message, const char* file,
                      int32_t line, int32_t col);
   ```

6. **Remove** `Novus/std/panic.novus`

7. **Update documentation**:
   - `DEBUG_RELEASE_BUILDS.md`: Document both `assert!` and `panic!`
   - `LanguageDesignDoc.md`: Add `panic!` to language features

8. **Add tests**:
   - `Novus.Tests/PanicTests.cs`: Test panic! statement
   - Test that panic! is NOT elided in release mode
   - Test runtime behavior

---

## Alternative: Keep Current Design

If you want to **minimize changes** for now:

### Quick Fixes:

1. **Improve `panic()` to show GUI**:
   ```novus
   pub fn panic(message: String) {
       // Use Intuition FFI to show EasyRequest
       // (Similar to __novus_assert_failed)
       show_error_dialog(message)
       loop_forever()
   }
   ```

2. **Document the distinction**:
   - `assert!(cond, msg)` - Development-time checks (elided in release)
   - `panic(msg)` - Production runtime errors (always present)

3. **Update doc references** from `panic!` to `panic()`

**This is the minimal-effort option** but doesn't fix the naming confusion or semantic overlap.

---

## Conclusion

**Current state**:
- `assert!` works great for debug-time checks with GUI dialogs
- `panic()` is incomplete and confusing (stdout-only, loops forever, not a macro)
- Documentation mentions `panic!` which doesn't exist

**Recommended**:
- Add `panic!` statement (compiler built-in)
- Keep `assert!` for debug-only checks
- Remove `panic()` function
- Both show GUI dialogs for good Amiga UX

**Effort**:
- Medium (compiler changes + runtime function)
- Clear payoff (clean semantics, good UX)

**If low effort preferred**:
- Keep `panic()` function
- Fix it to show GUI dialog
- Update docs to say `panic()` not `panic!`

---

## Questions to Resolve

1. **Should `panic!` allow formatted strings?**
   - Like: `panic!("File not found: {}", filename)`
   - Or just: `panic!("File not found")`
   - Formatted strings require string formatting runtime

2. **Should `panic!` clean up resources (defer blocks)?**
   - `assert!` currently executes deferred cleanup
   - `panic!` should probably do the same

3. **Should we have `exit()` or just loop forever?**
   - Proper exit requires C runtime linkage
   - Looping forever is the current approach
   - Could call `Alert()` and then loop

---

---

## ✅ IMPLEMENTATION COMPLETE

**Date:** 2025-01-04

### What Was Implemented

✅ Added `panic!` statement to grammar (Novus.g4)
✅ Added `IrPanic` instruction to IR
✅ Added semantic analysis for `panic!` statement
✅ Added IR building for `panic!` statement
✅ Added code generation for `panic!` statement (C backend)
✅ Added `__novus_panic()` runtime function to `runtime/novus_runtime.c`
✅ Removed `std::panic` module (no longer needed)
✅ Added comprehensive unit tests (7 tests, all passing)
✅ Created example program in `Novus.Tests/Examples/test_panic.novus`

### Implementation Details

**Behavior:**

| Statement | Debug Mode | Release Mode | Purpose |
|-----------|------------|--------------|---------|
| `assert!(cond, msg)` | Check + GUI | **Elided** | Debug-only safety checks |
| `panic!(msg)` | GUI + halt | GUI + halt | Unrecoverable runtime errors |

**Runtime:**
- `panic!` displays AmigaOS EasyRequest dialog with:
  - Title: "Novus Runtime Error"
  - Message: "PANIC: {message}"
  - File, line, column location
- Executes deferred cleanup (defer blocks) before halting
- Loops forever after showing dialog (no C runtime `exit()` dependency)

**Example Usage:**

```novus
pub fn divide(a: i32, b: i32) -> i32 {
    if b == 0 {
        panic!("Division by zero!")
    }
    return a / b
}
```

### Test Results

All 7 panic tests passing:

1. `BuildIr_Panic_SimpleMessage_Compiles` ✅
2. `BuildIr_Panic_InIfBlock_Compiles` ✅
3. `BuildIr_Panic_WithDefer_Compiles` ✅
4. `BuildIr_Panic_InFunction_Compiles` ✅
5. `CodeGen_Panic_InDebugMode_GeneratesPanicCode` ✅
6. `CodeGen_Panic_InReleaseMode_KeepsPanicCode` ✅
7. `CodeGen_Panic_VersusAssert_BehaviorDifference` ✅

### Files Modified

- `Novus.Core/Novus.g4` - Added panicStatement grammar rule
- `Novus.Core/IR/IrModule.cs` - Added IrPanic class
- `Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs` - Added VisitPanicStatement
- `Novus.Core/Frontend/IrBuilder.cs` - Added VisitPanicStatement
- `Novus/Codegen/CCodeGenerator.cs` - Added EmitPanic method
- `runtime/novus_runtime.c` - Added __novus_panic function
- `Novus/std/panic.novus` - **DELETED** (no longer needed)
- `Novus.Tests/AssertTests.cs` - Added 7 panic tests
- `Novus.Tests/Examples/test_panic.novus` - Added example

### Next Steps (Future Work)

The foundation is complete. Future enhancements could include:

1. **Automatic Safety Checks** (Phase 2):
   - Array bounds checking with auto-inserted `panic!`
   - Division-by-zero checks
   - Null pointer dereference checks
   - Integer overflow detection

2. **Format String Support** (Phase 3):
   - `panic!("Value: {}", x)` with string formatting
   - Requires string formatting runtime

3. **Stack Trace** (Phase 4):
   - Show call stack in panic dialog
   - Requires debug symbol support

---

## See Also

- `docs/DEBUG_RELEASE_BUILDS.md` - Debug vs Release mode design
- ~~`Novus/std/panic.novus`~~ - **REMOVED** (replaced by `panic!` statement)
- `runtime/novus_runtime.c` - Panic and assert runtime handlers
- `Novus.Tests/AssertTests.cs` - Panic tests
- Rust's panic design: https://doc.rust-lang.org/book/ch09-01-unrecoverable-errors-with-panic.html
