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
| **Assertions (`assert!`)** | Enabled | **Elided** |
| **Panics (`panic!`)** | Enabled | **Enabled** (never elided) |
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
   // Compiled into runtime check
   // Shows GUI dialog on failure (AmigaOS EasyRequest)
   // Then executes defer blocks and exits
   ```

5. **Panics Always Present**
   ```novus
   panic!("Division by zero!")
   // panic! is NEVER elided (even in release mode)
   // Shows GUI dialog with message + file/line/column
   // Executes defer blocks before halting
   // Note: Format strings not yet implemented
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
   // Compiled to nothing! Completely elided in release mode.
   ```

5. **Panics Remain (Not Elided!)**
   ```novus
   panic!("Division by zero!")
   // panic! is NEVER elided, even in release mode
   // Still shows GUI dialog with full message
   // This is for truly unrecoverable errors
   // Use assert! for debug-only checks instead
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
pub fn divide(a: i32, b: i32) -> i32 {
    if b == 0 {
        panic!("Division by zero!")
    }
    return a / b
}
```

**Both Debug AND Release Build (panic! is never elided):**
```c
int32_t divide(int32_t a, int32_t b) {
    if (b == 0) {
        __novus_panic("Division by zero!", "src/math.novus", 3, 9);
        return 1;  // Unreachable
    }
    return a / b;
}
```

**Runtime Behavior:**
- Opens `intuition.library`
- Displays AmigaOS EasyRequest dialog:
  - Title: "Novus Runtime Error"
  - Message: "PANIC: Division by zero!"
  - Location: "File: src/math.novus, Line: 3, Column: 9"
  - Button: "OK"
- Executes any defer blocks in the current function
- Loops forever after user clicks OK

**Note:** Format strings (e.g., `panic!("Value: {}", x)`) are not yet implemented.

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

## 📦 Binary Size Optimization

The Novus compiler implements several automatic optimizations to minimize binary size, especially important for Amiga systems with limited memory.

### Automatic Size Optimizations

#### 1. Zero-Initialized Array Compression

Large arrays initialized to zero are emitted using C99's `{0}` syntax instead of listing every element:

```novus
// Novus source
static data: [u8; 2048] = [0; 2048]
```

**Before optimization:**
```c
// Would generate 2048 explicit zeros
const uint8_t data[2048] = { 0, 0, 0, 0, ... }; // ~20KB in source
```

**After optimization:**
```c
// C99 guarantees {0} zeros the entire array
const uint8_t data[2048] = {0};  // A few bytes
```

This applies to arrays, nested arrays, and structs with all-zero fields.

#### 2. Symbol Stripping (Release Mode)

Release builds automatically strip debug symbols using vlink's `-s` flag:

- **Debug mode:** Preserves symbols and map file for debugging
- **Release mode:** Strips all symbols for smaller binaries

Typical savings: 20-40% reduction in binary size.

#### 3. Dead Code Elimination

The compiler uses vlink's `-gc-all` (garbage collection) to remove unused functions:

```bash
# Linker flags for executables
-gc-all   # Trace from entry point, remove unreachable code
-e _start # Entry point for tracing
```

Each function is compiled to its own `.o` file, enabling fine-grained DCE.

#### 4. Section Merging

vlink's `-sc` and `-sd` flags merge sections to avoid duplicate symbol errors from monomorphized generics:

```bash
-sc  # Merge all code sections
-sd  # Merge all data/bss sections
```

### Size Comparison Example

For a chip RAM cache test binary:

| Build Mode | Binary Size | Notes |
|------------|-------------|-------|
| Debug (before opts) | 340 KB | Full symbols, unoptimized |
| Release (before opts) | 62 KB | Stripped, optimized |
| Release (with opts) | 59 KB | + zero-init compression |

### Manual Size Reduction Tips

If you need even smaller binaries:

1. **Use `--release` mode** - Always use release for distribution
2. **Minimize string literals** - Each string adds to binary size
3. **Avoid unused imports** - Unused functions may still be pulled in transitively
4. **Use `write!` sparingly** - Printf formatting adds ~5KB
5. **Consider `-Os`** - Optimize for size over speed (when implemented)

### Future Size Optimizations

Planned improvements:
- `-Os` optimization level for size-optimized code
- String deduplication for repeated literals
- More aggressive inlining thresholds
- LTO (Link-Time Optimization) across modules

---

**End of Document**

## Next Steps

1. ~~Implement `--debug` and `--release` flags~~ ✓
2. ~~Wire up VBCC optimization flags~~ ✓
3. Add conditional bounds checking
4. Add assertion support
5. Add panic message stripping
6. Document in user guide
