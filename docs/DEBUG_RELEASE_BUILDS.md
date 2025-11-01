# Debug vs. Release Builds in Novus

**Status:** Design Document
**Date:** 2025-11-01

---

## 🎯 Overview

Novus needs clear debug and release build modes for Amiga 68k development. These modes should follow industry best practices while respecting the constraints of 68k hardware.

---

## 📊 Build Mode Comparison

| Feature | Debug | Release |
|---------|-------|---------|
| **Optimization** | `-O0` (none) | `-O2` or `-O3` |
| **Debug Symbols** | Yes (HUNK debug) | No |
| **Bounds Checking** | Yes (runtime) | No (removed) |
| **Assertions** | Enabled | Disabled |
| **Panic Messages** | Verbose | Minimal/stripped |
| **Dead Code Elimination** | Minimal | Aggressive |
| **Inline Functions** | Never | Aggressive |
| **Register Allocation** | Simple | Optimal |
| **Code Size** | Larger | Smaller |
| **Runtime Speed** | Slower | Faster |
| **Binary Size** | Larger | Smaller |
| **Debuggability** | Easy | Hard |

---

## 🔍 Debug Mode (`--debug` or default)

### Purpose
- **Development workflow**
- **Finding bugs quickly**
- **Understanding runtime behavior**
- **Debugging with MonAm/vamos**

### Characteristics

1. **No Optimization (`-O0`)**
   - Every statement generates distinct code
   - Variables not optimized away
   - Easy to match source to assembly
   - Slower execution, but predictable

2. **Debug Symbols (HUNK Debug Sections)**
   - Line number information
   - Variable names preserved
   - Function names preserved
   - Works with MonAm, vamos, WinUAE debugger

3. **Runtime Bounds Checking**
   ```novus
   let arr: [i32; 10] = [...]
   let x = arr[i]  // Runtime check: if i >= 10, abort()
   ```
   - Array access checked
   - Slice access checked
   - Pointer dereference safety (where possible)

4. **Assertions Enabled**
   ```novus
   assert!(x > 0, "x must be positive")
   // Compiled into runtime check + panic on failure
   ```

5. **Verbose Panics**
   ```novus
   panic!("Array index out of bounds: index={}, len={}", index, len)
   // Full message included in binary
   ```

6. **Stack Frame Preservation**
   - All functions get stack frames (even small ones)
   - Frame pointer (a6) always set up
   - Easy to walk stack in debugger

7. **Dead Code Kept**
   - Unused functions still compiled
   - Easier to call from debugger
   - Helps with incremental development

---

## 🚀 Release Mode (`--release`)

### Purpose
- **Production deployment**
- **Maximum performance**
- **Minimum binary size**
- **Distribution to users**

### Characteristics

1. **Aggressive Optimization (`-O2` or `-O3`)**
   - Constant folding
   - Dead code elimination
   - Loop unrolling
   - Register allocation optimization
   - Peephole optimization
   - Instruction scheduling for CPU pipeline

2. **No Debug Symbols**
   - Line numbers stripped
   - Variable names gone
   - Smaller binary
   - Harder to debug, but that's the trade-off

3. **No Bounds Checking**
   ```novus
   let arr: [i32; 10] = [...]
   let x = arr[i]  // No check! Assumes i < 10
   ```
   - Faster, but unsafe if index is wrong
   - Assumes developer tested in debug mode first

4. **Assertions Removed**
   ```novus
   assert!(x > 0, "x must be positive")
   // Compiled to nothing! Completely removed.
   ```

5. **Minimal Panics**
   ```novus
   panic!("Array index out of bounds")
   // Message might be stripped to just error code
   // Or just call abort() with no message
   ```

6. **Aggressive Inlining**
   - Small functions inlined
   - Reduces function call overhead
   - Faster but larger code (if many call sites)

7. **Dead Code Elimination**
   - Unused functions completely removed
   - Unreachable code removed
   - Smaller binary

8. **Link-Time Optimization (LTO)**
   - Whole-program optimization
   - Cross-module inlining
   - Global dead code elimination

---

## ⚙️ Implementation in Novus

### Build Modes

```bash
# Debug mode (default)
novusc build
novusc build --debug

# Release mode
novusc build --release
```

### Configuration in project.toml

```toml
[build]
# Default build mode
mode = "debug"  # or "release"

# Debug-specific settings
[build.debug]
optimization_level = 0
emit_debug_symbols = true
bounds_checking = true
assertions = true
verbose_panics = true
inline = "never"

# Release-specific settings
[build.release]
optimization_level = 2
emit_debug_symbols = false
bounds_checking = false
assertions = false
verbose_panics = false
inline = "aggressive"
link_time_optimization = true
```

### Command-Line Overrides

```bash
# Override optimization level
novusc build --debug -O2          # Debug with -O2
novusc build --release -O0        # Release with -O0 (weird but allowed)

# Override specific features
novusc build --release --bounds-check     # Release with bounds checking
novusc build --debug --no-bounds-check    # Debug without bounds checking
```

---

## 🔧 What Changes in Generated Code

### Example: Array Access

**Source:**
```novus
pub fn get_item(arr: [i32; 10], index: i32) -> i32 {
    return arr[index]
}
```

**Debug Build (`-O0 --debug`):**
```c
// Generated C code
int32_t get_item(int32_t* arr, int32_t index) {
    // Bounds check
    if ((uint32_t)index >= 10) {
        fprintf(stderr, "PANIC: Array index out of bounds (index=%d, len=10)\n", index);
        abort();
    }

    return arr[index];
}
```

**Release Build (`-O2 --release`):**
```c
// Generated C code
int32_t get_item(int32_t* arr, int32_t index) {
    return arr[index];  // No check!
}
```

Or even better, with optimization:
```asm
; 68020 assembly (inlined)
move.l  (a0, d0.l*4), d0    ; arr[index] in one instruction
```

---

### Example: Assertions

**Source:**
```novus
pub fn divide(a: i32, b: i32) -> i32 {
    assert!(b != 0, "division by zero")
    return a / b
}
```

**Debug Build:**
```c
int32_t divide(int32_t a, int32_t b) {
    if (!(b != 0)) {
        fprintf(stderr, "PANIC: Assertion failed: division by zero\n");
        fprintf(stderr, "  at src/math.novus:42\n");
        abort();
    }
    return a / b;
}
```

**Release Build:**
```c
int32_t divide(int32_t a, int32_t b) {
    return a / b;  // Assertion completely removed
}
```

---

### Example: Panic Messages

**Source:**
```novus
pub fn validate(x: i32) {
    if x < 0 {
        panic!("Invalid value: {} (must be >= 0)", x)
    }
}
```

**Debug Build:**
```c
void validate(int32_t x) {
    if (x < 0) {
        fprintf(stderr, "PANIC: Invalid value: %d (must be >= 0)\n", x);
        fprintf(stderr, "  at src/validate.novus:12\n");
        abort();
    }
}
```

**Release Build (Option 1: Stripped Messages):**
```c
void validate(int32_t x) {
    if (x < 0) {
        abort();  // No message, just crash
    }
}
```

**Release Build (Option 2: Error Codes):**
```c
void validate(int32_t x) {
    if (x < 0) {
        _panic_with_code(0x1001);  // Lookup table maps to message
    }
}
```

---

## 🎯 Optimization Levels

| Level | Name | Use Case | Characteristics |
|-------|------|----------|-----------------|
| `-O0` | None | Debug | No optimization, fast compile |
| `-O1` | Basic | Quick build | Basic optimizations, reasonable speed |
| `-O2` | Standard | Release | Full optimization, standard for production |
| `-O3` | Aggressive | Performance | Aggressive optimization, may increase size |
| `-Os` | Size | Embedded | Optimize for size over speed |

### Implementation

```bash
novusc build --debug          # -O0
novusc build --debug -O1      # -O1 (debug with basic opts)
novusc build --release        # -O2 (default release)
novusc build --release -O3    # -O3 (max performance)
novusc build --release -Os    # Minimize size
```

---

## 📏 Debug Symbols (HUNK Format)

AmigaOS uses the **HUNK format** for executables. Debug symbols are stored in:

- **HUNK_DEBUG** - Debug information
- **HUNK_SYMBOL** - Symbol names

### What to Include

**Debug Build:**
```
HUNK_DEBUG sections:
  - Line number table (source line → address)
  - Variable names (name → stack offset)
  - Function names (name → address)

HUNK_SYMBOL sections:
  - Global function symbols
  - Global variable symbols
```

**Release Build:**
```
No HUNK_DEBUG sections
HUNK_SYMBOL only for exported symbols (libraries)
```

### Tools That Use Debug Symbols

- **MonAm** - Amiga machine-language monitor
- **vamos** - Amiga emulator with debugger
- **WinUAE** - Debugger in WinUAE
- **GDB** - Can work with HUNK debug info (with converter)

---

## 🧪 Testing Strategy

### Debug Builds: Catch Bugs Early

```novus
// This bug is caught in debug mode
let arr: [i32; 5] = [1, 2, 3, 4, 5]
let x = arr[10]  // PANIC: Array index out of bounds (index=10, len=5)
```

### Release Builds: Assume Correctness

```novus
// This bug is NOT caught in release mode (undefined behavior!)
let arr: [i32; 5] = [1, 2, 3, 4, 5]
let x = arr[10]  // Reads random memory! 💥
```

**Best Practice:** Always test in debug mode first, then build release.

---

## 🔄 Workflow

### Development Cycle

```bash
# 1. Develop with debug builds
novusc build --debug
./build/myapp              # Fast rebuild, easy debugging

# 2. Test thoroughly
novusc test --debug

# 3. Profile (if needed)
novusc build --debug -O1   # Some optimization, still debuggable

# 4. Final release build
novusc build --release
./build/myapp              # Optimized, stripped, fast

# 5. Verify release build works
# (catches any bugs that only show up without bounds checks)
```

---

## 🎨 Profile-Based Builds

Future enhancement: Allow custom build profiles

```toml
[profiles.debug]
optimization_level = 0
debug_symbols = true
bounds_checking = true

[profiles.release]
optimization_level = 2
debug_symbols = false
bounds_checking = false

[profiles.release-with-symbols]
optimization_level = 2
debug_symbols = true      # For profiling
bounds_checking = false

[profiles.size]
optimization_level = "s"  # Optimize for size
debug_symbols = false
bounds_checking = false
link_time_optimization = true
```

Usage:
```bash
novusc build --profile release-with-symbols
```

---

## 🚦 Default Behavior

```bash
# No options → debug build
novusc build
# Equivalent to:
novusc build --debug -O0

# Release flag → release build
novusc build --release
# Equivalent to:
novusc build --release -O2
```

---

## 📝 Compiler Flags Summary

| Flag | Debug | Release |
|------|-------|---------|
| `-O` level | 0 | 2 |
| `-g` debug info | Yes | No |
| `-DNDEBUG` | No | Yes |
| Bounds checks | Yes | No |
| Assertions | Yes | No |
| Frame pointers | Yes | Optional |
| Dead code elim | No | Yes |
| Inlining | Never | Aggressive |

---

## 🎯 Implementation Plan

### Phase 1: Basic Debug/Release

1. Add `--debug` and `--release` flags to BuildOptions
2. Pass flags through to VBCC compiler
3. Add conditional bounds checking in CCodeGenerator
4. Add `#ifdef NDEBUG` for assertions

### Phase 2: Debug Symbols

1. Generate HUNK_DEBUG sections
2. Line number table generation
3. Symbol name preservation
4. Integration with vasm/vlink

### Phase 3: Advanced Optimizations

1. Link-time optimization
2. Profile-guided optimization
3. Custom build profiles
4. Per-function optimization control

---

## 💡 User Experience

### Before:
```bash
novusc build              # Who knows what optimization level?
novusc build -O2          # Have to remember to specify
```

### After:
```bash
# Clear, explicit modes
novusc build              # Debug mode (safe, slow)
novusc build --release    # Release mode (fast, small)

# Or configure in project.toml
[build]
mode = "release"          # Default to release mode
```

---

## 🎉 Benefits

1. **Safety** - Debug mode catches bugs early
2. **Performance** - Release mode runs fast
3. **Size** - Release mode produces small binaries
4. **Clarity** - Explicit `--debug` and `--release` flags
5. **Flexibility** - Can override individual settings
6. **Industry Standard** - Follows Rust/C++/etc. conventions

---

**End of Document**

## Next Steps

1. Implement `--debug` and `--release` flags
2. Wire up VBCC optimization flags
3. Add conditional bounds checking
4. Add assertion support
5. Add panic message stripping
6. Document in user guide
