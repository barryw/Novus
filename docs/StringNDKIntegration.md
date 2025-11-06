# Seamless NDK String Integration for Novus

## Overview

This document outlines the design for seamless integration between Novus strings and AmigaOS NDK functions, allowing natural string passing without explicit conversions.

## Goals

1. Allow passing `Str`, `String`, and string literals directly to NDK functions expecting `*u8` (CSTR)
2. Allow passing `BStr` to NDK functions expecting `BPTR` (BSTR)
3. Maintain type safety and prevent common errors
4. Zero runtime overhead for conversions
5. Clear compile-time errors when incompatible string types are used

## Current State

### String Types in Novus

```novus
pub struct Str {
    ptr: *u8,  // Pointer to string data
    len: u32,  // Length in bytes
}

pub struct String {
    // Heap-allocated, null-terminated C string
    // Internally uses Vec<u8>
}

pub struct BStr {
    // BCPL string (length byte + data)
    // Used for AmigaDOS functions
}
```

### NDK Function Signatures

```novus
// Examples from dos.library
extern fn Open(name: *u8, access_mode: i32) -> i32
extern fn CreateDir(name: *u8) -> i32
extern fn SetComment(name: *u8, comment: *u8) -> bool

// Examples using BSTR (BPTR)
extern fn LoadSeg(name: BPTR) -> i32
extern fn Execute(command: BPTR, input: i32, output: i32) -> bool
```

## Design: Automatic String Coercion

### Phase 1: Implicit .as_ptr() Insertion

Add automatic coercion in the semantic analyzer when passing string types to `*u8` parameters:

```novus
// User writes:
let file = Open("data/test.txt", MODE_OLDFILE)

// Compiler transforms to:
let file = Open("data/test.txt".as_ptr(), MODE_OLDFILE)
```

```novus
// User writes:
fn process_file(path: Str) {
    let file = Open(path, MODE_OLDFILE)
}

// Compiler transforms to:
fn process_file(path: Str) {
    let file = Open(path.as_ptr(), MODE_OLDFILE)
}
```

### Phase 2: Safe String Literal Handling

String literals are always null-terminated and stored in CODE section, so they can safely coerce to `*u8`:

```novus
// This is SAFE because string literals are null-terminated
Open("filename.txt", MODE_OLDFILE)
  → Open("filename.txt".as_ptr(), MODE_OLDFILE)
```

### Phase 3: Warning for Sliced Strings

Sliced `Str` values may not be null-terminated. Add compile-time warnings:

```novus
let s = Str::from_cstr("hello world")?
let slice = s.slice(0, 5)?  // "hello" - NOT null-terminated

// This should generate a warning:
Open(slice, MODE_OLDFILE)
```

**Warning**: `Str` slice may not be null-terminated. Consider using `slice.to_cstr()` or adding explicit null terminator.

### Phase 4: BSTR Coercion

For functions expecting `BPTR`, automatically call `.as_bptr()`:

```novus
// User writes:
let bstr = BStr::new_from_str("LIBS:")?
let seg = LoadSeg(bstr)

// Compiler transforms to:
let bstr = BStr::new_from_str("LIBS:")?
let seg = LoadSeg(bstr.as_bptr())
```

## Implementation Plan

### Step 1: Add Coercion Infrastructure

**File**: `Novus/SemanticAnalyzer.cs`

Add method to `SemanticAnalyzer` class:

```csharp
private IrExpression CoerceStringToPointer(IrExpression expr, IrType targetType)
{
    // Check if target type is *u8
    if (targetType is not IrPointerType ptrType ||
        ptrType.ElementType is not IrPrimitiveType { Kind: IrPrimitiveKind.U8 })
    {
        return expr;
    }

    var exprType = expr.Type;

    // String literal → already null-terminated
    if (expr is IrStringLiteral)
    {
        return new IrMemberAccess(expr, "as_ptr");
    }

    // Str type → insert .as_ptr() call
    if (IsStrType(exprType))
    {
        return new IrMemberAccess(expr, "as_ptr");
    }

    // String type → insert .as_ptr() call
    if (IsStringType(exprType))
    {
        return new IrMemberAccess(expr, "as_ptr");
    }

    return expr;
}
```

### Step 2: Apply Coercion in Function Calls

**File**: `Novus/SemanticAnalyzer.cs` in `AnalyzeFunctionCall()`

```csharp
// When analyzing function call arguments
for (int i = 0; i < args.Count; i++)
{
    var arg = args[i];
    var paramType = functionType.Parameters[i].Type;

    // Apply automatic string coercion
    arg = CoerceStringToPointer(arg, paramType);

    analyzedArgs.Add(arg);
}
```

### Step 3: Add Type Checking Helper Methods

```csharp
private bool IsStrType(IrType type)
{
    return type is IrStructType st &&
           st.Name == "Str" &&
           st.Module == "std::strings";
}

private bool IsStringType(IrType type)
{
    return type is IrStructType st &&
           st.Name == "String" &&
           st.Module == "std::strings";
}

private bool IsBStrType(IrType type)
{
    return type is IrStructType st &&
           st.Name == "BStr" &&
           st.Module == "std::strings";
}
```

### Step 4: Add Warning for Potentially Unsafe Conversions

```csharp
private void CheckStringSliceSafety(IrExpression expr, string functionName)
{
    // Check if expr is result of .slice() or other operations that
    // might create non-null-terminated strings

    if (expr is IrMethodCall mc && mc.MethodName == "slice")
    {
        Warning($"Passing sliced string to '{functionName}' which expects " +
                $"null-terminated string. Consider using .to_cstr() for safety.");
    }
}
```

## Usage Examples

### Example 1: File Operations

```novus
from std::strings import Str
from std::ffi::dos import *

pub fn main() -> i32 {
    // String literal - seamless
    let file = Open("data/config.txt", MODE_OLDFILE)

    // Str parameter - seamless
    fn open_file(path: Str) -> i32 {
        return Open(path, MODE_OLDFILE)  // Automatic .as_ptr()
    }

    // String - seamless
    let filename = String::new_from_str("output.txt")?
    let outfile = Open(filename, MODE_NEWFILE)

    return 0
}
```

### Example 2: BSTR Functions

```novus
from std::strings import BStr
from std::ffi::dos import *

pub fn main() -> i32 {
    let bstr = BStr::new_from_str("SYS:System")?

    // Automatic .as_bptr() insertion
    let lock = Lock(bstr, ACCESS_READ)

    return 0
}
```

### Example 3: Safe Slice Warning

```novus
let path = Str::from_cstr("SYS:Utilities/More")?
let dir = path.slice(0, 14)?  // "SYS:Utilities"

// ⚠️ Compiler warning: slice may not be null-terminated
let lock = Lock(dir, ACCESS_READ)

// Better: use to_cstr() for safety
let dir_cstr = dir.to_cstr()?
let lock = Lock(dir_cstr, ACCESS_READ)
```

## Advanced Features

### Feature 1: Temporary String Allocation

For functions that need temporary null-terminated strings from slices:

```novus
impl Str {
    // Allocates temporary null-terminated copy
    // Returns scope-guard that auto-frees on drop
    pub fn with_cstr<F>(&self, f: F) -> Result<i32, StringError>
    where F: Fn(*u8) -> i32
    {
        let temp = self.to_cstr()?
        defer temp.drop()
        return Ok(f(temp.as_ptr()))
    }
}

// Usage:
let slice = full_path.slice(0, 10)?
slice.with_cstr(|cstr| {
    Open(cstr, MODE_OLDFILE)
})
```

### Feature 2: Format String Safety

Add compile-time checking for format strings:

```novus
// Check that format specifiers match argument types
write("Value: %ld\n", value)  // OK: %ld expects i32/u32
write("Value: %ld\n", str)    // ERROR: %ld doesn't accept Str
```

## Migration Strategy

### Phase 1: Opt-in (Current)
- Explicit `.as_ptr()` calls required
- No automatic coercion
- ✅ Already implemented

### Phase 2: Opt-in with warnings (Recommended)
- Add automatic coercion
- Emit warnings for potentially unsafe uses
- Easy to disable per-file or per-function

### Phase 3: Opt-out (Future)
- Automatic coercion enabled by default
- Can disable with `#[no_auto_string_coercion]` attribute

## Safety Considerations

### Safe Cases
1. ✅ String literals → always null-terminated
2. ✅ `String::as_ptr()` → always null-terminated
3. ✅ `Str::from_cstr()` → source was null-terminated
4. ✅ `BStr::as_bptr()` → BCPL format with length byte

### Unsafe Cases
1. ⚠️ `Str::slice()` → may not be null-terminated
2. ⚠️ `Str::from_raw()` → caller responsibility
3. ⚠️ Manual pointer arithmetic → caller responsibility

### Mitigation
- Compiler warnings for unsafe cases
- Runtime checks in debug builds
- Clear documentation in stdlib

## Performance Impact

### Zero-cost Abstractions
- Coercion happens at compile time
- No runtime overhead
- Same generated code as explicit `.as_ptr()` calls

### Memory
- No additional allocations for simple cases
- Temporary allocations only when `.to_cstr()` is needed for slices

## Compatibility

### Backward Compatibility
- Existing code with explicit `.as_ptr()` calls continues to work
- New code can omit `.as_ptr()` for cleaner syntax

### Forward Compatibility
- Design allows adding new string types in future
- Coercion system is extensible

## Testing Plan

1. Add tests for each coercion case
2. Test warning generation for unsafe cases
3. Benchmark to verify zero overhead
4. Test with real NDK function calls
5. Test error messages are clear and helpful

## Documentation

### User-facing Documentation
- Update stdlib docs with examples
- Add "String Best Practices" guide
- Document when `.to_cstr()` is needed

### Developer Documentation
- Add comments in SemanticAnalyzer
- Document coercion rules
- Add design rationale

## Summary

This design provides seamless NDK string integration while maintaining safety:

✅ Natural syntax: `Open("file.txt", MODE_OLDFILE)`
✅ Type safe: compiler ensures correct conversions
✅ Zero overhead: compile-time only
✅ Safe by default: warnings for potentially unsafe uses
✅ Extensible: easy to add new string types

Implementation effort: ~2-3 days for Phase 1-2.
