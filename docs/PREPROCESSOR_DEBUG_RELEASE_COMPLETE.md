# Novus Preprocessor & Debug/Release Builds - COMPLETE! 🎉

**Date:** 2025-11-01
**Status:** ✅ WORKING - Ready to use!
**Test Status:** 960/960 passing (100%)

---

## 🎯 What We Built

1. **Preprocessor System** - Conditional compilation with `#if`, `#elif`, `#else`, `#endif`
2. **Debug/Release Modes** - `--debug` and `--release` build flags
3. **Automatic Constant Injection** - `DEBUG` and `RELEASE` constants based on build mode
4. **Optimization Control** - Different optimization levels for debug vs. release

---

## 🚀 Usage

### Debug Build (Default)

```bash
novusc build                # Debug mode (default)
novusc build --debug        # Explicit debug mode
```

**Characteristics:**
- `DEBUG = true`, `RELEASE = false`
- Optimization level: `-O0` (no optimization)
- Debug symbols included
- Bounds checking enabled (future)
- Assertions enabled (future)

### Release Build

```bash
novusc build --release      # Release mode
```

**Characteristics:**
- `DEBUG = false`, `RELEASE = true`
- Optimization level: `-O2` (full optimization)
- No debug symbols
- Bounds checking disabled (future)
- Assertions stripped (future)

---

## 📝 Preprocessor Syntax

### Basic #if

```novus
#if DEBUG
    // Debug-only code
    println("Debug mode active")
#endif
```

### #if / #else

```novus
#if DEBUG
    // Debug code
    let verbosity = 3
#else
    // Release code
    let verbosity = 0
#endif
```

### #if / #elif / #else

```novus
#if DEBUG
    println("Debug")
#elif RELEASE
    println("Release")
#else
    println("Unknown")
#endif
```

### Nested #if

```novus
#if DEBUG
    #if M68020
        println("Debug on the 68020 baseline")
    #else
        println("Debug on other CPU")
    #endif
#endif
```

---

## 💡 Real-World Examples

### Example 1: Debug Logging

```novus
pub fn process_data(data: Vec[i32]) -> Result[i32, Error] {
    #if DEBUG
        println("Processing {} items", data.len())
    #endif

    let result = expensive_computation(data)

    #if DEBUG
        println("Result: {}", result)
    #endif

    return Result::Ok(result)
}
```

**Debug Build:**
- Includes both println statements
- Helps debugging

**Release Build:**
- Both println statements removed
- No runtime overhead

---

### Example 2: Assertions

```novus
pub fn divide(a: i32, b: i32) -> i32 {
    #if DEBUG
        assert!(b != 0, "division by zero")
    #endif

    return a / b
}
```

**Debug Build:**
- Runtime assertion check
- Catches bugs early

**Release Build:**
- No assertion
- Faster execution

---

### Example 3: Different Implementations

```novus
pub fn fast_sqrt(x: i32) -> i32 {
    #if DEBUG
        // Slow but safe implementation
        return safe_sqrt(x)
    #else
        // Fast approximation
        return fast_approximate_sqrt(x)
    #endif
}
```

---

### Example 4: Debug-Only Functions

```novus
#if DEBUG
    pub fn dump_state() {
        println("State: ...")
        println("Memory: ...")
    }
#endif

pub fn main() -> i32 {
    #if DEBUG
        dump_state()
    #endif

    return 0
}
```

**Debug Build:**
- `dump_state()` function exists
- Can be called

**Release Build:**
- `dump_state()` function doesn't exist
- No code bloat

---

## 🔧 Built-in Constants

| Constant | Debug Value | Release Value |
|----------|-------------|---------------|
| `DEBUG` | `true` | `false` |
| `RELEASE` | `false` | `true` |

---

## ⚙️ Implementation Details

### Files Created

1. **`Novus/Preprocessing/Preprocessor.cs`** (NEW)
   - Lexer-level preprocessor
   - Processes directives before parsing
   - Preserves line numbers (blank lines)
   - Error handling for undefined constants and mismatched directives

2. **`Novus/BuildMode.cs`** (NEW)
   - `BuildMode` enum (Debug, Release)

### Files Modified

3. **`Novus/BuildOptions.cs`**
   - Added `--debug` flag
   - Added `--release` flag

4. **`Novus/CompilerOptions.cs`**
   - Added `BuildMode` property

5. **`Novus/Commands/BuildCommand.cs`**
   - Determine build mode from flags
   - Set optimization level based on mode (Debug=0, Release=2)
   - Pass `BuildMode` to `CompilerOptions`

6. **`Novus/Program.cs`**
   - Create preprocessor constants dictionary
   - Run preprocessor before parsing
   - Check for preprocessor errors

---

## 📊 Preprocessing Algorithm

```
1. Read source file
2. Create constants dictionary:
   - DEBUG = (buildMode == Debug)
   - RELEASE = (buildMode == Release)
3. Run preprocessor:
   - Parse directives (#if, #elif, #else, #endif)
   - Track active/inactive blocks with stack
   - Keep lines in active blocks
   - Replace lines in inactive blocks with blank lines
4. Check for errors (undefined constants, mismatched directives)
5. Continue with lexing/parsing
```

---

## 🎨 Preprocessing Example

### Source Code

```novus
pub fn main() -> i32 {
    #if DEBUG
        return 1
    #endif

    #if RELEASE
        return 2
    #endif

    return 0
}
```

### Debug Build (Processed Source)

```novus
pub fn main() -> i32 {

        return 1



    return 0
}
```

Note: `#if RELEASE` block replaced with blank lines to preserve line numbers.

### Release Build (Processed Source)

```novus
pub fn main() -> i32 {



        return 2


    return 0
}
```

Note: `#if DEBUG` block replaced with blank lines.

---

## 🚨 Error Handling

### Undefined Constant

```novus
#if UNKNOWN_CONSTANT
    println("test")
#endif
```

**Error:**
```
error[E_PREPROC_010]: undefined preprocessor constant 'UNKNOWN_CONSTANT'
  --> src/main.novus:5:5
   |
 5 | #if UNKNOWN_CONSTANT
   |     ^^^^^^^^^^^^^^^^
   |
  help: Available constants: DEBUG, RELEASE
```

### Mismatched #if

```novus
#if DEBUG
    println("test")
// Missing #endif
```

**Error:**
```
error[E_PREPROC_004]: unmatched #if directive (1 unclosed block)
  --> src/main.novus:10
```

### #else without #if

```novus
#else
    println("test")
#endif
```

**Error:**
```
error[E_PREPROC_002]: #else without matching #if
  --> src/main.novus:5:1
```

---

## 🧪 Testing

### Test File: `preprocessor_test.novus`

```novus
pub fn main() -> i32 {
    #if DEBUG
        // Debug mode - return 1
        return 1
    #endif

    #if RELEASE
        // Release mode - return 2
        return 2
    #endif

    // Should never reach here
    return 0
}
```

**Debug Build:**
- Compiles to: `return 1`
- Exit code: 1

**Release Build:**
- Compiles to: `return 2`
- Exit code: 2

### Test Results

```bash
dotnet test
# Result: 960/960 tests passing (100%)
```

All tests pass, including the new preprocessor test!

---

## 🎯 Build Mode Behavior

### Debug Mode (`--debug` or default)

| Feature | Setting |
|---------|---------|
| Optimization | `-O0` (none) |
| DEBUG constant | `true` |
| RELEASE constant | `false` |
| VBCC flags | `-O=0` |

### Release Mode (`--release`)

| Feature | Setting |
|---------|---------|
| Optimization | `-O2` (full) |
| DEBUG constant | `false` |
| RELEASE constant | `true` |
| VBCC flags | `-O=1023` (max) |

---

## 📐 Line Number Preservation

The preprocessor replaces removed lines with **blank lines** instead of deleting them.

**Why?**
- Preserves line numbers for error messages
- Compiler errors point to correct source lines
- Debuggers show correct source locations

**Example:**

**Original:**
```novus
1: pub fn main() -> i32 {
2:     #if RELEASE
3:         println("Release")
4:     #endif
5:     return 0
6: }
```

**Debug Build (processed):**
```novus
1: pub fn main() -> i32 {
2:
3:
4:
5:     return 0
6: }
```

Error on line 5 still points to line 5 in the original source! ✅

---

## 🔄 Workflow

### Development Workflow

```bash
# 1. Develop with debug build
novusc build

# 2. Test (with debug assertions and logging)
./build/myapp

# 3. Profile (if needed)
novusc build -O1   # Some optimization, still debuggable

# 4. Release build
novusc build --release

# 5. Verify release build works
./build/myapp
```

---

## 🎨 Future Enhancements

### Phase 2: User-Defined Constants

```bash
novusc build --define FEATURE_X=true --define MAX_SIZE=1024
```

```toml
# project.toml
[build.defines]
FEATURE_X = true
MAX_SIZE = 1024
```

### Phase 3: Target Constants

```novus
#if M68020
    // 68020 baseline code
#endif

#if TARGET_CPU_68040
    // 68040-specific code
#endif

#if TARGET_AMIGA
    // Amiga-specific code
#endif
```

### Phase 4: Feature Flags

```novus
#if FEATURE_BOUNDS_CHECKING
    // Runtime bounds checks
#endif

#if FEATURE_ASSERTIONS
    // Assertions enabled
#endif
```

---

## 📊 Success Metrics

| Metric | Value |
|--------|-------|
| **Lines of Code Added** | ~300 lines |
| **New Files** | 2 (Preprocessor.cs, BuildMode.cs) |
| **Modified Files** | 4 (BuildOptions.cs, CompilerOptions.cs, BuildCommand.cs, Program.cs) |
| **Features** | Preprocessor + Debug/Release modes |
| **Test Status** | 960/960 passing (100%) |
| **Regressions** | 0 |

---

## 💡 Benefits

1. **Conditional Compilation** - Include/exclude code based on build mode
2. **Debug Logging** - Verbose output in debug, silent in release
3. **Performance** - No runtime overhead for debug code in release builds
4. **Safety** - Debug assertions without release performance hit
5. **Code Clarity** - Explicit `#if DEBUG` blocks
6. **Error Messages** - Clear errors for undefined constants and mismatched directives
7. **Line Numbers** - Preserved for accurate error reporting

---

## 🎉 Highlights

1. **Simple Syntax** - C-style `#if`/`#elif`/`#else`/`#endif`
2. **Automatic Constants** - `DEBUG` and `RELEASE` set based on build mode
3. **Line Preservation** - Error messages show correct line numbers
4. **Nested Support** - Full support for nested `#if` blocks
5. **Clear Errors** - Helpful error messages for common mistakes
6. **100% Tests** - All 960 tests passing
7. **Zero Regressions** - No existing functionality broken

---

## 🎯 User Experience

### Before

```novus
// No way to have debug-only code
pub fn process(data: Vec[i32]) -> i32 {
    // println("Processing...") // Have to comment out for release!
    let result = compute(data)
    return result
}
```

### After

```novus
// Clean debug/release separation
pub fn process(data: Vec[i32]) -> i32 {
    #if DEBUG
        println("Processing {} items", data.len())
    #endif

    let result = compute(data)

    #if DEBUG
        println("Result: {}", result)
    #endif

    return result
}
```

**Debug Build:** Full logging
**Release Build:** Zero logging overhead

---

## 📚 Documentation

- `/docs/PREPROCESSOR_DESIGN.md` - Preprocessor design document
- `/docs/DEBUG_RELEASE_BUILDS.md` - Debug vs. Release builds guide
- `/docs/PREPROCESSOR_DEBUG_RELEASE_COMPLETE.md` - This document

---

**End of Report**

## Summary

We successfully implemented:

1. **Preprocessor system** with `#if`, `#elif`, `#else`, `#endif` directives
2. **Debug and Release build modes** with `--debug` and `--release` flags
3. **Automatic constant injection** - `DEBUG` and `RELEASE` based on build mode
4. **Optimization control** - `-O0` for debug, `-O2` for release
5. **Line number preservation** - Blank lines maintain accurate error locations
6. **Error handling** - Clear messages for undefined constants and mismatched directives

**All 960 tests passing!** ✅

**Ready for production!** 🚀
