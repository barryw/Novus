# Panic! Statement Smoke Testing Guide

## Test Binaries

Three test binaries have been compiled and copied to the Amiga shared drive:

**Location**: `/Users/barry/Emulation/Amiga/A4000-DH0/Barry/`

### 1. panic_pass_example

**Purpose**: Verify panic! is compiled in but doesn't trigger

**Expected behavior**:
- No panic occurs
- Program exits cleanly with return code 5
- No GUI dialog appears

**Source**: `Novus.Tests/Examples/panic_pass_example.novus`

### 2. panic_fail_example

**Purpose**: Trigger a panic! to see the GUI dialog

**Expected behavior**:
- AmigaOS EasyRequest dialog appears:
  - **Title**: "Novus Runtime Error"
  - **Message**: "PANIC: Division by zero!"
  - **Location**: "File: ./Novus.Tests/Examples/panic_fail_example.novus, Line: 16, Column: 8"
  - **Button**: "OK"
- After clicking OK, **program exits cleanly** with error code 1
- Shell prompt returns normally (no hang!)

**Source**: `Novus.Tests/Examples/panic_fail_example.novus`

### 3. panic_defer_example

**Purpose**: Verify defer blocks execute before panic halts

**Expected behavior**:
- Defer block executes (sets cleanup_flag = 1)
- AmigaOS EasyRequest dialog appears:
  - **Title**: "Novus Runtime Error"
  - **Message**: "PANIC: Negative value not allowed!"
  - **Location**: File/line/column information
  - **Button**: "OK"
- After clicking OK, **program exits cleanly** with error code 1
- Shell prompt returns normally
- Defer cleanup ran before showing dialog (though not observable externally)

**Source**: `Novus.Tests/Examples/panic_defer_example.novus`

---

## Testing Instructions

### On WinUAE (Recommended)

1. Boot WinUAE with A4000 configuration
2. Open a shell window
3. Navigate to Barry:
   ```
   cd Barry:
   ```
4. Run the test binaries:
   ```
   panic_pass_example
   panic_fail_example
   panic_defer_example
   ```

### On Real Amiga Hardware

Same steps as WinUAE above.

---

## What to Look For

### ✅ Success Criteria

1. **panic_pass_example**: Exits normally (no dialog)
2. **panic_fail_example**: Shows GUI requester with proper message
3. **panic_defer_example**: Shows GUI requester (defer ran invisibly)
4. All dialogs use native AmigaOS EasyRequest style
5. Dialogs show file, line, and column information
6. "OK" button dismisses the dialog
7. Program hangs after dialog (doesn't crash)

### ❌ Failure Indicators

- No dialog appears (panic! not working)
- Dialog shows wrong message
- Dialog missing file/line/column info
- Program crashes instead of showing dialog
- Memory corruption or Guru Meditation

---

## Generated C Code

Example from `panic_fail_example`:

```c
int32_t divide(int32_t a, int32_t b) {
    bool _t0 = b == 0;
    if (_t0) goto if_then_0;
    goto if_end_0;
if_then_0:;
    __novus_panic("Division by zero!", "./Novus.Tests/Examples/panic_fail_example.novus", 16, 8);
    return 1;  // Unreachable (panic never returns)
if_end_0:;
    int32_t _t1 = a / b;
    return _t1;
}
```

**Key points**:
- `__novus_panic()` is called with message + location
- Never elided (even in release mode)
- Executes defer blocks before showing dialog
- Loops forever after dialog

---

## Rebuilding Test Binaries

If you modify the examples and want to rebuild:

```bash
cd /Users/barry/RiderProjects/Novus
./build_panic_tests.sh
```

This will:
1. Compile all three panic examples
2. Copy binaries to the Amiga shared drive
3. Show status for each build

---

## Comparison with assert!

| Feature | `assert!(cond, msg)` | `panic!(msg)` |
|---------|---------------------|---------------|
| **Debug Mode** | Shows GUI | Shows GUI |
| **Release Mode** | **ELIDED** | **ALWAYS PRESENT** |
| **Purpose** | Debug-time checks | Runtime errors |
| **Example** | `assert!(x > 0)` | `panic!("Out of memory")` |

---

## Implementation Details

### Runtime Function (`runtime/novus_runtime.c`)

```c
void __novus_panic(const char* message, const char* file,
                   int32_t line, int32_t col)
{
    // Opens intuition.library
    // Builds error message: "PANIC: {message}\n\nFile: {file}\nLine: {line}, Column: {col}"
    // Shows EasyRequest dialog
    // Closes library
    // Loops forever (while (1) {})
}
```

### Key Behavior

1. **Opens intuition.library** - Native AmigaOS GUI
2. **Shows EasyRequest** - Standard Amiga requester style
3. **Executes defer blocks** - Resource cleanup before halting
4. **Loops forever** - No C runtime `exit()` dependency
5. **Never elided** - Present in both debug and release builds

---

## Questions to Answer During Testing

1. ✅ Does the GUI dialog appear?
2. ✅ Is the message correct?
3. ✅ Does it show file/line/column?
4. ✅ Does the "OK" button work?
5. ✅ Does panic_pass_example exit normally?
6. ✅ Is the dialog style native AmigaOS?
7. ✅ Does it work on both Kickstart 2.0 and 3.x?

---

## See Also

- **Implementation**: `docs/PANIC_ANALYSIS.md`
- **Design**: `docs/DEBUG_RELEASE_BUILDS.md`
- **Runtime**: `runtime/novus_runtime.c`
- **Tests**: `Novus.Tests/AssertTests.cs` (7 panic tests)
- **Examples**: `Novus.Tests/Examples/panic_*.novus`

---

**Status**: Ready for smoke testing! 🚀
