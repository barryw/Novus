# Critical String Implementation Fixes

## Overview

This document outlines the critical fixes needed for the Novus string implementation based on the Amiga developer review.

## Priority 1: Critical Issues (Must Fix Before Production)

### Issue #1: BStr Memory Leak

**Problem**: `BStr::drop()` must be called manually, leading to memory leaks.

**Current Code** (strings.novus:761-769):
```novus
pub fn drop(&self) {
    let ptr: *u8 = self.as_ptr()
    let len: u32 = self.len()
    let total_bytes: u32 = 1 + len

    unsafe {
        FreeMem(ptr, total_bytes)  // ❌ Manual cleanup required
    }
}
```

**Solution**: Implement automatic cleanup using `defer` blocks in the compiler.

**Implementation Plan**:

1. **Add defer semantics to compiler** (if not already present)
2. **Track resource ownership** in type system
3. **Auto-insert drop calls** at scope exit

**Alternative (Short-term)**: Refactor BStr to use MemoryBlock internally:

```novus
pub struct BStr {
    block: MemoryBlock,  // Owns the memory
}

impl BStr {
    pub fn new_from_str(s: Str) -> Result<BStr, StringError> {
        if s.len() > 255 {
            return Result::Err(StringError::TooLong(s.len()))
        }

        let total_bytes: u32 = 1 + s.len()
        let block = MemoryBlock::alloc(total_bytes, MEMF_PUBLIC)?

        let ptr: *u8 = block.ptr()
        ptr[0] = (u8)s.len()  // Length byte

        // Copy data
        var i: u32 = 0
        while i < s.len() {
            ptr[1 + i] = s.ptr[i]
            i = i + 1
        }

        return Result::Ok(BStr { block: block })
    }

    pub fn as_ptr(&self) -> *u8 {
        return self.block.ptr()
    }

    pub fn as_bptr(&self) -> BPTR {
        let addr: u32 = (u32)self.block.ptr()
        return (BPTR)(addr >> 2)  // BPTR = address / 4
    }

    // drop() is automatic when MemoryBlock goes out of scope
}
```

---

### Issue #2: Chip RAM Support

**Problem**: All strings allocate from ANY memory (Fast or Chip). Hardware access requires Chip RAM.

**Impact**: System crashes when passing strings to Copper, Blitter, or audio hardware.

**Solution**: Add Chip RAM allocation variants.

**Implementation**:

```novus
// Add MEMF_CHIP constant
from std::ffi::exec import MEMF_CHIP

impl String {
    /// Allocate string in Chip RAM (for hardware access)
    pub fn with_capacity_chip(capacity: u32) -> Result<String, StringError> {
        let block = MemoryBlock::alloc(capacity, MEMF_CHIP | MEMF_CLEAR)?
        return Result::Ok(String {
            data: Vec::<u8> { block: block, len: 0, capacity: capacity }
        })
    }

    /// Create string from Str in Chip RAM
    pub fn from_str_chip(s: Str) -> Result<String, StringError> {
        let mut string = String::with_capacity_chip(s.len() + 1)?
        string.push_str(s)
        string.push_byte(0)  // Null terminator
        return Result::Ok(string)
    }
}

impl BStr {
    /// Create BSTR in Chip RAM (for hardware access)
    pub fn new_from_str_chip(s: Str) -> Result<BStr, StringError> {
        if s.len() > 255 {
            return Result::Err(StringError::TooLong(s.len()))
        }

        let total_bytes: u32 = 1 + s.len()
        let block = MemoryBlock::alloc(total_bytes, MEMF_CHIP)?

        let ptr: *u8 = block.ptr()
        ptr[0] = (u8)s.len()

        var i: u32 = 0
        while i < s.len() {
            ptr[1 + i] = s.ptr[i]
            i = i + 1
        }

        return Result::Ok(BStr { block: block })
    }
}
```

**Documentation**: Add clear guidelines about when Chip RAM is needed:

```novus
/// When to use Chip RAM strings:
///
/// ✅ Required:
/// - Screen titles accessed by Copper
/// - Gadget text rendered by system (not RastPort text)
/// - Audio sample names displayed in GUI
/// - Custom chip DMA operations
///
/// ❌ Not needed:
/// - File paths (dos.library)
/// - Window titles (copied by Intuition)
/// - Text rendered via Text() RastPort function
/// - Most AmigaOS API calls (library code uses Fast RAM)
```

---

### Issue #3: Unsafe as_cstr()

**Problem**: `Str::as_cstr()` assumes null termination, which is unsafe for slices.

**Current Code** (strings.novus:351-356):
```novus
pub fn as_cstr(&self) -> *u8 {
    return self.ptr  // ❌ Assumes null termination
}
```

**Solution**: Make safety explicit in API.

**Implementation**:

```novus
impl Str {
    /// Get pointer to string data
    ///
    /// ⚠️ SAFETY: Only safe if string is null-terminated.
    /// String literals are always null-terminated.
    /// Sliced strings are NOT null-terminated.
    ///
    /// For safety, use `to_cstr()` which allocates a null-terminated copy.
    pub unsafe fn as_cstr_unchecked(&self) -> *u8 {
        return self.ptr
    }

    /// Create null-terminated copy for passing to C functions
    ///
    /// Always safe but allocates memory if string is not null-terminated.
    /// Consider using `with_cstr()` for automatic cleanup.
    pub fn to_cstr(&self) -> Result<String, StringError> {
        let mut s = String::with_capacity(self.len + 1)?
        s.push_str(*self)
        s.push_byte(0)  // Null terminator
        return Result::Ok(s)
    }

    /// Execute function with null-terminated string
    ///
    /// Automatically allocates and frees temporary null-terminated copy.
    /// Use this for passing slices to C functions.
    ///
    /// Example:
    /// ```novus
    /// let slice = path.slice(0, 10)?
    /// slice.with_cstr(|cstr| {
    ///     Open(cstr, MODE_OLDFILE)
    /// })
    /// ```
    pub fn with_cstr<F>(&self, f: F) -> Result<i32, StringError>
    where F: Fn(*u8) -> i32
    {
        let temp = self.to_cstr()?
        defer temp.drop()  // Auto-cleanup
        return Result::Ok(f(temp.as_ptr()))
    }
}

// String is always null-terminated
impl String {
    pub fn as_cstr(&self) -> *u8 {
        return self.data.as_ptr()
    }
}
```

---

## Priority 2: Important Fixes

### Issue #4: BPTR Alignment Validation

**Problem**: BCPL strings should be LONG-aligned (4-byte boundary).

**Solution**:

```novus
impl BStr {
    pub fn new_from_str(s: Str) -> Result<BStr, StringError> {
        // ... existing code ...

        let ptr: *u8 = block.ptr()
        let addr: u32 = (u32)ptr

        // Validate alignment
        if addr % 4 != 0 {
            // Free and reallocate with alignment
            block.free()
            let aligned_block = MemoryBlock::alloc_aligned(total_bytes, MEMF_PUBLIC, 4)?
            ptr = aligned_block.ptr()
        }

        // ... rest of implementation ...
    }
}
```

---

### Issue #5: Null Pointer Checks in Slice Operations

**Current Code** (strings.novus:126-140):
```novus
pub fn slice(&self, start: u32, end: u32) -> Result<Str, StringError> {
    if start > end {
        return Result::Err(StringError::OutOfBounds(start, self.len))
    }
    if end > self.len {
        return Result::Err(StringError::OutOfBounds(end, self.len))
    }

    let new_len: u32 = end - start
    let new_ptr: *u8 = self.ptr + start

    return Result::Ok(Str { ptr: new_ptr, len: new_len })
}
```

**Solution**:

```novus
pub fn slice(&self, start: u32, end: u32) -> Result<Str, StringError> {
    // Check for null pointer
    if (u32)self.ptr == 0 {
        return Result::Err(StringError::NullPointer)
    }

    if start > end {
        return Result::Err(StringError::OutOfBounds(start, self.len))
    }
    if end > self.len {
        return Result::Err(StringError::OutOfBounds(end, self.len))
    }

    let new_len: u32 = end - start
    let new_ptr: *u8 = self.ptr + start

    return Result::Ok(Str { ptr: new_ptr, len: new_len })
}
```

---

## Priority 3: Performance Optimizations

### Issue #6: Use memcmp for String Comparisons

**Current Code** (byte-by-byte loops):
```novus
pub fn equals(&self, other: Str) -> bool {
    if self.len != other.len {
        return false
    }

    var i: u32 = 0
    while i < self.len {
        if self.ptr[i] != other.ptr[i] {
            return false
        }
        i = i + 1
    }
    return true
}
```

**Optimized**:
```novus
pub fn equals(&self, other: Str) -> bool {
    if self.len != other.len {
        return false
    }
    if self.len == 0 {
        return true
    }
    unsafe {
        return memcmp(self.ptr, other.ptr, self.len) == 0
    }
}

pub fn compare(&self, other: Str) -> i32 {
    let min_len: u32 = if self.len < other.len { self.len } else { other.len }
    unsafe {
        let cmp: i32 = memcmp(self.ptr, other.ptr, min_len)
        if cmp != 0 {
            return cmp
        }
    }
    // If common prefix equal, shorter string is "less"
    if self.len < other.len {
        return -1
    } else if self.len > other.len {
        return 1
    }
    return 0
}
```

---

## Testing Plan

### Test #1: BStr Automatic Cleanup
```novus
fn test_bstr_cleanup() {
    let initial_mem = get_avail_mem(MEMF_PUBLIC)

    // Create and drop many BStr objects
    var i: u32 = 0
    while i < 1000 {
        let bstr = BStr::new_from_str("test")?
        // BStr should auto-drop here
        i = i + 1
    }

    let final_mem = get_avail_mem(MEMF_PUBLIC)
    assert!(initial_mem == final_mem, "Memory leaked!")
}
```

### Test #2: Chip RAM Allocation
```novus
fn test_chip_ram() {
    let chip_str = String::from_str_chip("Hello")?
    let ptr = chip_str.as_ptr()
    let addr = (u32)ptr

    // Check if in Chip RAM range (0x000000 - 0x200000 on most Amigas)
    assert!(addr < 0x200000, "String not in Chip RAM!")
}
```

### Test #3: Slice Safety
```novus
fn test_slice_safety() {
    let s = Str::from_cstr("hello world")?
    let slice = s.slice(0, 5)?  // "hello"

    // Should work with with_cstr
    let result = slice.with_cstr(|cstr| {
        strlen(cstr)
    })
    assert!(result == 5, "Slice length mismatch")
}
```

---

## Migration Guide

### For Existing Code Using BStr

**Before**:
```novus
let bstr = BStr::new_from_str("path")?
// ... use bstr ...
bstr.drop()  // Manual cleanup required
```

**After** (with MemoryBlock refactor):
```novus
let bstr = BStr::new_from_str("path")?
// ... use bstr ...
// Automatic cleanup at scope exit
```

### For Code Needing Chip RAM

**Before**:
```novus
let title = String::new_from_str("My Window")?
// Might crash if allocated in Fast RAM
```

**After**:
```novus
let title = String::from_str_chip("My Window")?
// Guaranteed Chip RAM for hardware access
```

---

## Summary

These fixes address all critical issues found in the review:

✅ **BStr memory leak** - automatic cleanup via MemoryBlock
✅ **Chip RAM support** - explicit _chip variants
✅ **as_cstr() safety** - split into unsafe and safe versions
✅ **BPTR alignment** - validation and aligned allocation
✅ **Null pointer checks** - added to slice operations
✅ **Performance** - optimized comparisons using memcmp

Estimated implementation: 3-4 days of focused work.
