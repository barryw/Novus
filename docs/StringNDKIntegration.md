# Seamless NDK String Integration for Novus

## Overview

This document outlines the design for seamless integration between Novus strings and AmigaOS NDK functions, allowing natural string passing without explicit conversions.

## Goals

1. Allow passing `Str`, `String`, and string literals directly to NDK functions expecting `*u8` (CSTR)
2. Allow passing `BStr` to NDK functions expecting `BPTR` (BSTR)
3. **Automatically convert NDK string return values to Novus string types**
4. Maintain type safety and prevent common errors
5. Zero runtime overhead for conversions (where possible)
6. Clear compile-time errors when incompatible string types are used

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

## Bidirectional Conversion: NDK Returns → Novus Strings

### The Challenge

Many NDK functions return string pointers that need to be converted to Novus string types for safe manipulation:

```novus
// Current: Returns raw pointer
extern fn GetVar(name: *u8, buffer: *u8, size: u32, flags: u32) -> i32

// User must manually wrap:
let mut buffer: [u8; 256]
let len = GetVar("PATH", &buffer[0], 256, 0)
let path = Str::from_raw(&buffer[0], (u32)len)  // ❌ Manual, error-prone
```

**Problems with current approach:**
1. Manual length tracking required
2. Easy to create dangling pointers
3. Lifetime management unclear
4. No safety checks

### Solution: Automatic Return Value Wrapping

#### Approach 1: Wrapper Functions in stdlib

Create safe wrapper functions that return `Str` or `Option<Str>`:

```novus
// In std::ffi::dos module
pub fn get_var(name: Str) -> Option<Str> {
    let mut buffer: [u8; 256]
    let len = unsafe {
        GetVar(name.as_ptr(), &buffer[0], 256, 0)
    }

    if len <= 0 {
        return Option::None
    }

    // Safe: buffer is on stack, caller must copy if needed
    return Option::Some(Str::from_raw(&buffer[0], (u32)len))
}

// Usage:
let path = get_var("PATH")?
println!("Path: {}", path)
```

#### Approach 2: Smart Return Types

For functions that return pointers to system-managed memory:

```novus
// FilePart returns pointer into existing string (no allocation)
extern fn FilePart(path: *u8) -> *u8

// Wrapper returns Str that borrows from original
pub fn file_part(path: Str) -> Option<Str> {
    let result_ptr = unsafe { FilePart(path.as_ptr()) }

    if (u32)result_ptr == 0 {
        return Option::None
    }

    // Calculate length from result_ptr to end of path
    let offset = (u32)result_ptr - (u32)path.as_ptr()
    let remaining_len = path.len() - offset

    return Option::Some(Str::from_raw(result_ptr, remaining_len))
}

// Usage:
let path = Str::from_cstr("SYS:Utilities/More")?
let filename = file_part(path)?  // "More"
```

#### Approach 3: Owned String Returns

For functions that allocate memory the caller must free:

```novus
// AllocVec returns memory caller must free
extern fn AllocVec(size: u32, flags: u32) -> *u8
extern fn FreeVec(ptr: *u8)

// Wrapper returns owned String with automatic cleanup
pub fn read_file_to_string(path: Str) -> Result<String, DosError> {
    let file = Open(path, MODE_OLDFILE)?
    defer Close(file)

    // Get file size
    Seek(file, 0, OFFSET_END)
    let size = Seek(file, 0, OFFSET_BEGINNING)
    Seek(file, 0, OFFSET_BEGINNING)

    // Allocate buffer
    let buffer = AllocVec(size + 1, MEMF_PUBLIC)?
    defer FreeVec(buffer)  // Auto-free on any error path

    // Read file
    let bytes_read = Read(file, buffer, size)
    if bytes_read != size {
        return Result::Err(DosError::ReadError)
    }

    // Null terminate
    buffer[size] = 0

    // Transfer ownership to String
    return Result::Ok(String::from_raw_parts(buffer, (u32)size, (u32)size + 1))
}
```

### Common NDK String Return Patterns

#### Pattern 1: Fill Buffer Functions

**Functions**: `GetVar`, `NameFromLock`, `DeviceProc`

**Strategy**: Stack buffer + Str wrapper

```novus
pub fn name_from_lock(lock: i32) -> Option<Str> {
    let mut buffer: [u8; 256]
    let success = unsafe {
        NameFromLock(lock, &buffer[0], 256)
    }

    if !success {
        return Option::None
    }

    // Find null terminator
    let len = strlen(&buffer[0])
    return Option::Some(Str::from_raw(&buffer[0], len))
}
```

#### Pattern 2: Pointer Into Existing String

**Functions**: `FilePart`, `PathPart`, `strchr`

**Strategy**: Return borrowed Str (slice)

```novus
pub fn path_part(full_path: Str) -> Option<Str> {
    let path_ptr = unsafe { PathPart(full_path.as_ptr()) }

    if (u32)path_ptr == 0 {
        return Option::None
    }

    // PathPart returns pointer to start of path component
    let offset = (u32)path_ptr - (u32)full_path.as_ptr()
    return Option::Some(full_path.slice_from(offset))
}
```

#### Pattern 3: System-Owned Strings

**Functions**: `FindTask(NULL)->tc_Node.ln_Name`, process names, device names

**Strategy**: Copy to owned String for safety

```novus
pub fn current_task_name() -> String {
    let task_ptr = unsafe { FindTask(0) }
    let name_ptr = unsafe {
        let node_ptr = task_ptr as *u8  // tc_Node is first field
        let ln_name_offset = 10  // Offset to ln_Name in Node struct
        *((node_ptr + ln_name_offset) as **u8)
    }

    // System owns this memory - MUST copy
    let name_str = Str::from_cstr(name_ptr)?
    return String::from_str(name_str)?
}
```

#### Pattern 4: BCPL Strings (BSTR/BPTR)

**Functions**: `DupLock`, BCPL command line parsing

**Strategy**: Wrap in BStr or convert to Str

```novus
// Convert BPTR to Str (read-only view)
pub fn bstr_to_str(bptr: BPTR) -> Option<Str> {
    if bptr == 0 {
        return Option::None
    }

    let addr: u32 = ((u32)bptr) << 2  // BPTR to address
    let ptr: *u8 = (addr as *u8)
    let len: u32 = (u32)ptr[0]  // Length byte

    if len == 0 {
        return Option::Some(Str::from_raw(ptr + 1, 0))
    }

    return Option::Some(Str::from_raw(ptr + 1, len))
}
```

### Implementation Strategy

#### Phase 1: Manual Wrapper Functions (Current)

Write safe wrapper functions in `std::ffi::*` modules:

```novus
// std::ffi::dos
pub fn get_var(name: Str) -> Option<String>
pub fn name_from_lock(lock: i32) -> Option<String>
pub fn file_part(path: Str) -> Option<Str>

// std::ffi::intuition
pub fn get_screen_title(screen: *Screen) -> Option<Str>
pub fn get_window_title(window: *Window) -> Option<Str>
```

**Pros:**
- ✅ Works immediately
- ✅ Full control over safety
- ✅ Can document ownership and lifetime

**Cons:**
- ❌ Must write wrapper for every function
- ❌ Verbose for simple cases

#### Phase 2: Attribute-Based Generation (Future)

Add attributes to extern declarations for automatic wrapper generation:

```novus
#[string_return(buffer_size = 256)]
extern fn GetVar(name: *u8, buffer: *u8, size: u32, flags: u32) -> i32

// Compiler auto-generates:
pub fn get_var(name: Str) -> Option<String> {
    let mut buffer: [u8; 256]
    let len = unsafe { GetVar(name.as_ptr(), &buffer[0], 256, 0) }
    if len <= 0 { return Option::None }
    return Option::Some(String::from_raw(&buffer[0], (u32)len))
}
```

**Attributes:**
- `#[string_return(buffer_size = N)]` - Fill buffer pattern
- `#[string_return(borrowed)]` - Pointer into existing string
- `#[string_return(owned)]` - Caller must free
- `#[string_return(system_owned)]` - System owns, must copy
- `#[bstr_return]` - BCPL string return

### Usage Examples

#### Example 1: Environment Variables

```novus
from std::ffi::dos import get_var

pub fn main() -> i32 {
    // Seamless: no manual buffer management
    let path = get_var("PATH")
        .unwrap_or(String::from_str("SYS:"))

    println!("PATH: {}", path)

    // Can split, manipulate, etc.
    let dirs = path.split(':')
    for dir in dirs {
        println!("  - {}", dir)
    }

    return 0
}
```

#### Example 2: File Name Parsing

```novus
from std::ffi::dos import file_part, path_part

pub fn process_file(full_path: Str) {
    // Get just the filename
    let filename = file_part(full_path)?
    println!("File: {}", filename)

    // Get just the path
    let path = path_part(full_path)?
    println!("Path: {}", path)

    // Both are slices of original - zero copy
}
```

#### Example 3: Lock Information

```novus
from std::ffi::dos import name_from_lock, Lock, UnLock

pub fn print_directory_name(path: Str) -> Result<(), DosError> {
    let lock = Lock(path, ACCESS_READ)?
    defer UnLock(lock)

    // Get full path from lock
    let full_path = name_from_lock(lock)?
    println!("Full path: {}", full_path)

    return Result::Ok(())
}
```

### Safety Considerations

#### Lifetime Safety

```novus
// ❌ UNSAFE: Str borrows from stack buffer
pub fn unsafe_example() -> Str {
    let mut buffer: [u8; 256]
    GetVar("PATH", &buffer[0], 256, 0)
    return Str::from_raw(&buffer[0], 256)  // ❌ Dangling pointer!
}

// ✅ SAFE: Return owned String
pub fn safe_example() -> Option<String> {
    let mut buffer: [u8; 256]
    let len = GetVar("PATH", &buffer[0], 256, 0)
    if len <= 0 { return Option::None }

    // Copy to owned String before returning
    return Option::Some(String::from_raw(&buffer[0], (u32)len))
}
```

#### Null Safety

```novus
// Always check for null before wrapping
pub fn wrap_ndk_string(ptr: *u8) -> Option<Str> {
    if (u32)ptr == 0 {
        return Option::None  // ✅ Safe
    }

    let len = strlen(ptr)
    return Option::Some(Str::from_raw(ptr, len))
}
```

#### Ownership Clarity

Use type system to document ownership:

```novus
// Borrowed - caller owns memory
pub fn file_part(path: Str) -> Option<Str>

// Owned - callee allocates, caller must free
pub fn read_file(path: Str) -> Result<String, Error>

// System owned - copy before returning
pub fn current_task_name() -> String
```

### Standard Library Additions

Add comprehensive wrappers to `std::ffi::dos`:

```novus
// Environment variables
pub fn get_var(name: Str) -> Option<String>
pub fn set_var(name: Str, value: Str) -> bool

// Path manipulation
pub fn file_part(path: Str) -> Option<Str>
pub fn path_part(path: Str) -> Option<Str>
pub fn add_part(path: &mut String, file: Str) -> bool

// Lock operations
pub fn name_from_lock(lock: i32) -> Option<String>
pub fn parent_dir(lock: i32) -> i32

// Device operations
pub fn device_name(device: *DeviceNode) -> Option<Str>
pub fn volume_name(info: *InfoData) -> Option<Str>
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
