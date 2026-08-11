# Novus Preprocessor Design

**Status:** Design + Implementation
**Date:** 2025-11-01

---

## 🎯 Overview

Novus needs a simple preprocessor to handle conditional compilation based on build mode and other compile-time constants.

---

## 📋 Preprocessor Directives

### Phase 1: Build Mode Conditionals

```novus
#if DEBUG
    // Debug-only code
    println("Debug: value = {}", x)
#endif

#if RELEASE
    // Release-only code
    // Optimized fast path
#endif

#if DEBUG
    assert!(x > 0, "x must be positive")
#else
    // No assertion in release
#endif
```

### Syntax

```
#if <CONSTANT>
    <code>
#endif

#if <CONSTANT>
    <code>
#else
    <code>
#endif

#if <CONSTANT>
    <code>
#elif <CONSTANT>
    <code>
#else
    <code>
#endif
```

---

## 🔧 Built-in Constants

### Always Available

| Constant | Value | Description |
|----------|-------|-------------|
| `DEBUG` | `true` or `false` | Debug build mode |
| `RELEASE` | `true` or `false` | Release build mode (opposite of DEBUG) |

### Future: Target Constants

```novus
#if M68020
    // 68020 baseline code
#endif

#if TARGET_CPU_68020
    // 68020-specific code (bitfields, etc.)
#endif

#if TARGET_CPU_68040
    // 68040-specific code
#endif

#if TARGET_AMIGA
    // Amiga-specific code
#endif

#if TARGET_OS_AMIGAOS
    // AmigaOS-specific code
#endif
```

### Future: Feature Flags

```novus
#if FEATURE_BOUNDS_CHECKING
    // Runtime bounds checks
#endif

#if FEATURE_ASSERTIONS
    // Assertions enabled
#endif
```

---

## 🎨 Use Cases

### 1. Debug Logging

```novus
pub fn process_data(data: Vec[i32]) -> Result[i32, Error> {
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
```novus
pub fn process_data(data: Vec[i32]) -> Result[i32, Error> {
    println("Processing {} items", data.len())
    let result = expensive_computation(data)
    println("Result: {}", result)
    return Result::Ok(result)
}
```

**Release Build:**
```novus
pub fn process_data(data: Vec[i32]) -> Result[i32, Error> {
    let result = expensive_computation(data)
    return Result::Ok(result)
}
```

---

### 2. Assertions

```novus
pub fn divide(a: i32, b: i32) -> i32 {
    #if DEBUG
        assert!(b != 0, "division by zero")
    #endif

    return a / b
}
```

---

### 3. Different Implementations

```novus
pub fn fast_sqrt(x: i32) -> i32 {
    #if DEBUG
        // Slow but safe implementation for debugging
        return safe_sqrt(x)
    #else
        // Fast but unsafe approximation for release
        return fast_approximate_sqrt(x)
    #endif
}
```

---

### 4. Debug-only Functions

```novus
#if DEBUG
    pub fn dump_state() {
        println("State: ...")
    }
#endif

pub fn main() -> i32 {
    #if DEBUG
        dump_state()
    #endif

    return 0
}
```

---

## 🔨 Implementation Approach

### Option 1: Lexer-Level (Before Parsing)

**Pros:**
- Simple to implement
- Works like C preprocessor
- No changes to grammar

**Cons:**
- Harder to report good error messages
- Can create invalid token streams

### Option 2: Parser-Level (During Parsing)

**Pros:**
- Better error messages
- Can validate syntax inside blocks
- AST-aware

**Cons:**
- More complex
- Needs grammar changes

### **Decision: Lexer-Level** (simpler, matches C/C++ expectations)

---

## 📐 Implementation Details

### Preprocessor Class

```csharp
public class Preprocessor
{
    private readonly Dictionary<string, bool> _constants;

    public Preprocessor(Dictionary<string, bool> constants)
    {
        _constants = constants;
    }

    public string Process(string source, string filePath)
    {
        // Process directives and return modified source
        // Remove lines that are inside false #if blocks
    }
}
```

### Integration into Compilation Pipeline

```csharp
// In Program.cs RunCompiler():

// 1. Read source
var source = await File.ReadAllTextAsync(inputFile);

// 2. Set up preprocessor constants
var constants = new Dictionary<string, bool>
{
    ["DEBUG"] = options.BuildMode == BuildMode.Debug,
    ["RELEASE"] = options.BuildMode == BuildMode.Release
};

// 3. Run preprocessor
var preprocessor = new Preprocessor(constants);
source = preprocessor.Process(source, inputFile);

// 4. Continue with lexing, parsing, etc.
var inputStream = new AntlrInputStream(source);
// ...
```

---

## 🎯 Directive Syntax

### Valid Directives

```
#if <CONSTANT>
#elif <CONSTANT>
#else
#endif
```

### Rules

1. Directives must be on their own line
2. Directives start with `#` at the beginning of the line (whitespace before `#` is allowed)
3. Constants are uppercase identifiers
4. Nesting is allowed

**Example:**
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

## 🚨 Error Handling

### Undefined Constant

```novus
#if UNKNOWN_CONSTANT
    // Error: Undefined preprocessor constant 'UNKNOWN_CONSTANT'
#endif
```

**Error Message:**
```
error: undefined preprocessor constant 'UNKNOWN_CONSTANT'
  --> src/main.novus:10:5
   |
10 | #if UNKNOWN_CONSTANT
   |     ^^^^^^^^^^^^^^^^
   |
  help: available constants: DEBUG, RELEASE
```

### Mismatched Directives

```novus
#if DEBUG
    println("Debug")
// Missing #endif
```

**Error Message:**
```
error: unmatched #if directive
  --> src/main.novus:5:1
   |
5  | #if DEBUG
   | ^^^^^^^^^
   |
  help: expected #endif before end of file
```

---

## 📊 Processing Algorithm

```
1. Initialize stack for nested #if tracking
2. Initialize "active" flag (true = keep code, false = skip code)

3. For each line:
   a. If line starts with '#':
      - Parse directive (#if, #elif, #else, #endif)
      - Update stack and active flag
      - Remove this line from output

   b. Else:
      - If active flag is true:
        - Keep this line
      - Else:
        - Remove this line

4. Check stack is empty (all #if matched with #endif)
5. Return processed source
```

---

## 🧪 Test Cases

### Test 1: Simple #if DEBUG

**Input:**
```novus
pub fn main() -> i32 {
    #if DEBUG
        println("Debug mode")
    #endif
    return 0
}
```

**Debug Build Output:**
```novus
pub fn main() -> i32 {
    println("Debug mode")
    return 0
}
```

**Release Build Output:**
```novus
pub fn main() -> i32 {
    return 0
}
```

---

### Test 2: #if/#else

**Input:**
```novus
pub fn main() -> i32 {
    #if DEBUG
        println("Debug")
    #else
        println("Release")
    #endif
    return 0
}
```

**Debug Build Output:**
```novus
pub fn main() -> i32 {
    println("Debug")
    return 0
}
```

**Release Build Output:**
```novus
pub fn main() -> i32 {
    println("Release")
    return 0
}
```

---

### Test 3: Nested #if

**Input:**
```novus
#if DEBUG
    #if BOUNDS_CHECKING
        println("Debug with bounds checking")
    #endif
#endif
```

**Debug Build (with BOUNDS_CHECKING=true):**
```novus
println("Debug with bounds checking")
```

**Debug Build (with BOUNDS_CHECKING=false):**
```novus
// (empty, both blocks removed)
```

---

## 🎨 Line Number Preservation

**Problem:** Removing lines breaks error reporting (line numbers won't match source file)

**Solution:** Replace removed lines with blank lines

**Example:**

**Source:**
```novus
1: pub fn main() -> i32 {
2:     #if RELEASE
3:         println("Release")
4:     #endif
5:     return 0
6: }
```

**Debug Build (naive removal):**
```novus
1: pub fn main() -> i32 {
2:     return 0
3: }
```
❌ Line numbers are wrong!

**Debug Build (with blank lines):**
```novus
1: pub fn main() -> i32 {
2:
3:
4:
5:     return 0
6: }
```
✅ Line numbers match source file!

---

## 🔄 Future: User-Defined Constants

```bash
# Command line
novusc build --define FEATURE_X=true --define MAX_SIZE=1024

# In code
#if FEATURE_X
    println("Feature X enabled")
#endif
```

```toml
# In project.toml
[build.defines]
FEATURE_X = true
MAX_SIZE = 1024
```

---

## 📝 Implementation Checklist

### Phase 1: Basic Preprocessor

- [ ] Create `Preprocessor.cs` class
- [ ] Implement directive parsing (#if, #elif, #else, #endif)
- [ ] Implement conditional block skipping
- [ ] Preserve line numbers (blank lines)
- [ ] Error handling (undefined constants, mismatched directives)
- [ ] Integration into compilation pipeline

### Phase 2: Build Mode Integration

- [ ] Add `BuildMode` enum (Debug, Release)
- [ ] Add `--debug` and `--release` flags to BuildOptions
- [ ] Set DEBUG and RELEASE constants based on build mode
- [ ] Update project.toml to support default build mode

### Phase 3: Testing

- [ ] Unit tests for preprocessor
- [ ] Integration tests (compile with #if DEBUG)
- [ ] Error message tests

---

## 🎯 Success Criteria

1. ✅ `#if DEBUG` blocks work
2. ✅ `#if RELEASE` blocks work
3. ✅ Nested `#if` works
4. ✅ Error messages show correct line numbers
5. ✅ Undefined constants produce clear errors
6. ✅ Mismatched directives produce clear errors

---

**End of Document**

## Summary

We're adding a **simple, C-style preprocessor** to Novus:
- Directives: `#if`, `#elif`, `#else`, `#endif`
- Built-in constants: `DEBUG`, `RELEASE`
- Lexer-level processing (before parsing)
- Line number preservation for error messages
- Clear error messages for undefined/mismatched directives

Next: Implementation!
