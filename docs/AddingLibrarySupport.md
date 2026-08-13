# Adding New Amiga Library Support to Novus

This guide documents the complete process for adding support for new Amiga libraries to Novus. We'll use GadTools as a working example, showing every file that needs to be modified.

## Overview

Adding a new library to Novus involves:
1. Adding the library base storage to `library_bases.s`
2. **Adding library initialization to `novus_startup.s`** (CRITICAL!)
3. Creating FFI bindings in the stdlib
4. Creating high-level wrapper APIs (optional but recommended)

**Key Insight:** Novus uses its own custom startup code (`novus_startup.s`), NOT VBCC's auto-library-opening. This means you MUST manually add `OpenLibrary()` and `CloseLibrary()` calls to the startup code for each library you want to use.

**Why not use VBCC's auto-library-opening?**
- We define the library base symbols ourselves in `library_bases.s`
- This prevents vlink from pulling in libauto's init code
- Our startup code gives us explicit control over initialization order and error handling

## Step-by-Step Process

### Step 1: Add Library Base Storage

**File:** `/Users/barry/RiderProjects/Novus/Novus/stubs/library_bases.s`

Add your library base to the existing library_bases.s file:

```assembly
; ============================================================================
; Exported Library Bases
; ============================================================================
	xdef	_SysBase		; exec.library base
	xdef	_DOSBase		; dos.library base
	xdef	_IntuitionBase	; intuition.library base
	xdef	_GadToolsBase	; gadtools.library base  ← ADD THIS

; ============================================================================
; Storage (initialized to 0 by loader)
; ============================================================================
_SysBase:
	ds.l	1		; Reserve 1 longword for SysBase

_DOSBase:
	ds.l	1		; Reserve 1 longword for DOSBase

_IntuitionBase:
	ds.l	1		; Reserve 1 longword for IntuitionBase

_GadToolsBase:
	ds.l	1		; Reserve 1 longword for GadToolsBase  ← ADD THIS
```

**Why:** This creates BSS storage for the library base pointer. The startup code will populate this when opening the library.

**Important:** All commonly-used libraries should go in `library_bases.s` rather than separate files. This keeps them together and ensures they're always available.

### Step 2: Add Library Initialization to Startup Code (CRITICAL!)

**File:** `/Users/barry/RiderProjects/Novus/Novus/stubs/novus_startup.s`

This is the most critical step. You MUST add code to open your library at startup and close it at exit.

**1. Add external reference for the library base:**

```assembly
; External References
	xref	_main
	xref	_SysBase
	xref	_DOSBase
	xref	_IntuitionBase
	xref	_GadToolsBase		; ← ADD THIS
	xref	___dos_init
	xref	___dos_cleanup
```

**2. Add OpenLibrary call after existing library opens:**

```assembly
	; Open gadtools.library v37
	movea.l	_SysBase,a6		; Get exec.library base
	lea	.gadtools_name(pc),a1	; Library name
	moveq	#37,d0			; Minimum version (v37 = AmigaOS 2.0+)
	jsr	-552(a6)		; OpenLibrary()
	move.l	d0,_GadToolsBase	; Save the base
	beq.s	.no_gadtools		; If NULL, skip gadtools cleanup
```

**3. Add CloseLibrary call before existing library closes:**

```assembly
	; Close gadtools.library
	move.l	d0,-(sp)		; Save return code
	movea.l	_SysBase,a6		; Get exec.library base
	movea.l	_GadToolsBase,a1	; Library to close
	jsr	-414(a6)		; CloseLibrary()
	move.l	(sp)+,d0		; Restore return code

.no_gadtools:
	; Continue with closing other libraries...
```

**4. Add library name string:**

```assembly
.gadtools_name:
	dc.b	'gadtools.library',0
	even
```

**Library Opening Order:**
- Open in order of dependencies (DOS first, then higher-level libraries)
- Close in reverse order (close GadTools before Intuition before DOS)
- Use minimum version numbers appropriate for your library (v37 = AmigaOS 2.0+, v33 = AmigaOS 1.2+)

**Why This is Critical:**
- Without this step, `_GadToolsBase` will be NULL (just zero-initialized BSS)
- Any library function call will crash with Guru Meditation 80000006 (address error)
- The library base is passed to every library function via A6 register
- We discovered this the hard way - windows worked (Intuition was opened) but menus crashed (GadTools wasn't opened)

### Step 3: Create FFI Bindings

**File:** `/Users/barry/RiderProjects/Novus/Novus/std/amiga/raw/gadtools.novus`

Create FFI bindings for the library's functions. These are typically generated from SFD files using the stub generator, but can be written manually:

```novus
// gadtools.novus - Low-level FFI bindings for gadtools.library
// Namespace: amiga::raw::gadtools
//
// These are direct 1:1 mappings to the C functions in gadtools.library.
// For high-level, ergonomic APIs, see amiga::sys::graphics::menus

from amiga::raw::structs import Menu, NewMenu, VisualInfo, Screen, TagItem, Gadget

// ============================================================================
// Menu Functions
// ============================================================================

/// Create menus from NewMenu array
/// Returns: Menu pointer or NULL on failure
extern fn CreateMenusA(newmenu: *NewMenu, tags: *TagItem) -> *Menu

/// Layout menus on screen
/// Returns: TRUE on success, FALSE on failure
extern fn LayoutMenusA(menu: *Menu, vi: *VisualInfo, tags: *TagItem) -> i32

/// Free menus created with CreateMenusA
extern fn FreeMenus(menu: *Menu)

/// Get visual info for screen (needed for LayoutMenusA)
/// Returns: VisualInfo pointer or NULL on failure
extern fn GetVisualInfoA(screen: *Screen, tags: *TagItem) -> *VisualInfo

/// Free visual info
extern fn FreeVisualInfo(vi: *VisualInfo)

// ============================================================================
// Gadget Functions
// ============================================================================

/// Create a GadTools gadget
extern fn CreateGadgetA(
    kind: i32,
    prev: *Gadget,
    ng: *u8,  // NewGadget pointer
    tags: *TagItem
) -> *Gadget

/// Free gadgets created with CreateGadgetA
extern fn FreeGadgets(gadget: *Gadget)
```

**Key Points:**
- Use `extern fn` for all library functions
- Match C signatures exactly (pointer types, return types)
- Add documentation comments explaining what each function does
- Group related functions with section headers

**How VBCC Finds These:**
- VBCC looks for `#include <clib/gadtools_protos.h>` in generated C code
- The NDK protos define the function signatures
- VBCC's auto-library-opening generates code that:
  1. Opens the library on first use
  2. Stores the base in `_GadToolsBase`
  3. Calls the function via the library vector table
  4. Closes the library at program exit

### Step 3: Define Necessary Structs

**File:** `/Users/barry/RiderProjects/Novus/Novus/std/amiga/raw/structs.novus`

Add any structs the library uses (if not already present):

```novus
// NewMenu - Menu template structure for GadTools
pub struct NewMenu {
    nm_Type: u8,           // Menu type (NM_TITLE, NM_ITEM, NM_SUB, NM_END)
    nm_Label: *u8,         // Menu label string
    nm_CommKey: *u8,       // Command key (or NULL)
    nm_Flags: u16,         // Menu flags
    nm_MutualExclude: i32, // Mutual exclude mask
    nm_UserData: *u8       // User data pointer
}

pub struct VisualInfo {
    // Opaque structure - never directly accessed
    // Created by GetVisualInfoA(), freed by FreeVisualInfo()
}
```

**CRITICAL: NDK Struct Redefinition Prevention**

When you add NDK structs to Novus, you MUST also update the NDK struct skip list in the C code generator to prevent redefinition errors:

**File:** `/Users/barry/RiderProjects/Novus/Novus/Codegen/CCodeGenerator.cs`

1. **Add struct to skip list** (around line 338):
```csharp
private static readonly HashSet<string> ndkStructs = new HashSet<string>
{
    // ... existing structs ...
    "NewMenu",        // ← ADD YOUR STRUCT HERE
    "VisualInfo",     // ← ADD YOUR STRUCT HERE
    // ...
};
```

2. **Add typedef to novus_types.h generation** (around line 295-298):
```csharp
sb.AppendLine("typedef struct NewMenu NewMenu;");      // ← ADD THIS
sb.AppendLine("typedef struct VisualInfo VisualInfo;"); // ← ADD THIS
```

**Why This is Critical:**
- The Novus struct definition is used for TYPE CHECKING ONLY
- The C code generator MUST NOT emit struct definitions for NDK types
- The NDK headers (`<proto/gadtools.h>`) provide the actual definition
- This prevents redefinition errors from VBCC
- **The skip list ensures VBCC's struct layout (with padding) is used, not Novus's**
- This is essential for correct `sizeof()` calculations

**How It Works:**
1. Novus sees the struct fields for semantic analysis and field access
2. Code generator skips emitting the struct definition (it's in the skip list)
3. Code generator emits only a typedef: `typedef struct NewMenu NewMenu;`
4. VBCC uses the NDK's struct definition with correct padding
5. `@sizeof(NewMenu)` emits `sizeof(NewMenu)` in C, which VBCC evaluates correctly

**Important:** Match the C struct field names and types exactly:
- Field order must match
- Field names must match (for semantic analysis)
- Field types must match (u8, u16, i32, pointers)
- Alignment and padding are handled by VBCC using the NDK definition

### Step 4: Define Constants

**File:** `/Users/barry/RiderProjects/Novus/Novus/std/amiga/raw/consts.novus`

Add any constants the library defines:

```novus
// GadTools NewMenu types
pub const NM_TITLE: i32 = 1      // Menu title
pub const NM_ITEM: i32 = 2       // Menu item
pub const NM_SUB: i32 = 3        // Sub-item
pub const NM_END: i32 = 0        // End of menu array

// GadTools menu flags
pub const NM_ITEMDISABLED: u16 = 0x0001
pub const NM_MENUDISABLED: u16 = 0x0002
pub const NM_COMMANDSTRING: u16 = 0x0004

// GadTools gadget kinds
pub const BUTTON_KIND: i32 = 1
pub const CHECKBOX_KIND: i32 = 2
pub const INTEGER_KIND: i32 = 3
pub const LISTVIEW_KIND: i32 = 4
pub const MX_KIND: i32 = 5
```

### Step 5: Create High-Level Wrapper API (Optional)

**File:** `/Users/barry/RiderProjects/Novus/Novus/std/graphics/menus.novus`

Create ergonomic, type-safe wrappers around the FFI:

```novus
// Menu System - Hierarchical Builder API for GadTools menus
//
// This provides an ergonomic, type-safe menu builder that translates
// to the Amiga Way™ using GadTools NewMenu arrays.

from std::core import Result, Vec
from std::error::core import IntuitionError
from amiga::raw::structs import Menu, NewMenu, Screen, VisualInfo, Window
from amiga::raw::consts import *
from amiga::raw::gadtools import CreateMenusA, LayoutMenusA, FreeMenus, GetVisualInfoA, FreeVisualInfo
from amiga::raw::intuition import SetMenuStrip, ClearMenuStrip
from amiga::raw::exec import AllocVec, FreeVec, CopyMem, MEMF_PUBLIC, MEMF_CLEAR
from std::string::core import Str, String

pub struct GadToolsMenuBuilder {
    entries_bytes: Vec<u8>  // Raw menu entry data
    entry_count: u32
    screen: *Screen
}

impl GadToolsMenuBuilder {
    pub fn new(screen: *Screen) -> GadToolsMenuBuilder {
        return GadToolsMenuBuilder {
            entries_bytes: Vec::<u8>::new(),
            entry_count: 0,
            screen: screen
        }
    }

    pub fn add_menu(&mut self, title: Str) -> MenuHandle {
        // Create menu title entry
        let label = String::new_from_str(title).unwrap_or(String::new())
        let entry = MenuEntry::new_title(label.as_ptr())
        self.push_entry(entry)

        return MenuHandle { builder: self }
    }

    pub fn build(&mut self) -> Result<GadToolsMenuStrip, IntuitionError> {
        // Add terminator
        self.push_entry(MenuEntry::new_end())

        unsafe {
            // Allocate NewMenu array
            let size = self.entry_count * 14
            let menu_array = (*NewMenu)AllocVec(size, MEMF_PUBLIC | MEMF_CLEAR)
            if !menu_array {
                return Result::Err(IntuitionError::MenuCreateFailed)
            }

            // Copy entries to array
            CopyMem(self.entries_bytes.as_ptr(), (*u8)menu_array, size)

            // Create menus (VBCC auto-opens gadtools.library here!)
            let menu = CreateMenusA(menu_array, (*TagItem)0)
            if !menu {
                FreeVec((*u8)menu_array)
                return Result::Err(IntuitionError::MenuCreateFailed)
            }

            // Get visual info and layout
            let vi = GetVisualInfoA(self.screen, (*TagItem)0)
            if !vi {
                FreeMenus(menu)
                FreeVec((*u8)menu_array)
                return Result::Err(IntuitionError::MenuCreateFailed)
            }

            LayoutMenusA(menu, vi, (*TagItem)0)

            return Result::Ok(GadToolsMenuStrip {
                menu: menu,
                visual_info: vi,
                menu_array: menu_array
            })
        }
    }
}
```

**Design Principles:**
- Hide unsafe FFI behind safe Rust-like APIs
- Use RAII for resource management (Drop trait)
- Return Result types for operations that can fail
- Builder pattern for complex object construction
- Type safety (MenuHandle, not raw pointers)

### Step 6: Add Error Types

**File:** `/Users/barry/RiderProjects/Novus/Novus/std/error/core.novus`

Add error variants for library-specific failures:

```novus
pub enum IntuitionError {
    WindowOpenFailed,
    ScreenOpenFailed,
    GadgetCreateFailed,
    MenuCreateFailed,       // ← Existing
    LibraryOpenFailed,      // ← Added for GadTools
    InvalidWindow,
    InvalidScreen,
    NoIDCMP,
    BadDisplayMode,
    ModifyIDCMPFailed,
}

// Update error code conversion
pub fn intuition_error_to_code(err: IntuitionError) -> i32 {
    match err {
        IntuitionError::WindowOpenFailed => return -200,
        IntuitionError::ScreenOpenFailed => return -201,
        IntuitionError::GadgetCreateFailed => return -202,
        IntuitionError::MenuCreateFailed => return -203,
        IntuitionError::InvalidWindow => return -204,
        IntuitionError::InvalidScreen => return -205,
        IntuitionError::NoIDCMP => return -206,
        IntuitionError::BadDisplayMode => return -207,
        IntuitionError::ModifyIDCMPFailed => return -208,
        IntuitionError::LibraryOpenFailed => return -209,  // ← ADD THIS
    }
}
```

### Step 7: Rebuild Projects

After making all changes:

```bash
# 1. Rebuild the Novus compiler (copies stubs to bin/)
dotnet build -c Release

# 2. Rebuild the standard library
dotnet bin/Release/net9.0/Novus.dll stdlib-build

# 3. Test compilation
dotnet bin/Release/net9.0/Novus.dll compile your_test.novus -o output
```

## Complete File Checklist

When adding a new library, you'll typically modify these files:

- [ ] `/Novus/stubs/library_bases.s` - Add library base storage
- [ ] **`/Novus/stubs/novus_startup.s`** - **CRITICAL: Add OpenLibrary/CloseLibrary calls**
- [ ] `/Novus/std/amiga/raw/yourlib.novus` - FFI function bindings
- [ ] `/Novus/std/amiga/raw/structs.novus` - Add structs (if needed)
- [ ] `/Novus/std/amiga/raw/consts.novus` - Add constants (if needed)
- [ ] **`/Novus/Codegen/CCodeGenerator.cs`** - **CRITICAL: Add NDK structs to skip list AND typedef generation**
- [ ] `/Novus/std/yourmodule/api.novus` - High-level wrappers (optional)
- [ ] `/Novus/std/error/core.novus` - Error types (if needed)

**⚠️ CRITICAL:** The two most common mistakes:
1. Forgetting to update `novus_startup.s` - causes Guru Meditation 80000006 crashes
2. Forgetting to update `CCodeGenerator.cs` - causes struct redefinition errors and wrong sizeof

## How Library Initialization Works in Novus

When you call a library function like `CreateMenusA()`:

1. **At Program Startup (`novus_startup.s`):**
   - `_start` is the entry point
   - SysBase is loaded from absolute address 4
   - DOS library is opened via `___dos_init`
   - Each additional library is opened via `OpenLibrary()` calls
   - Library bases are stored in `library_bases.s` BSS storage
   - If a library fails to open, initialization skips calling `main()`

2. **During Program Execution:**
   - Library functions use the pre-loaded library base from BSS
   - The base is passed in A6 register for each library call
   - VBCC's generated code loads the base: `movea.l _GadToolsBase,a6`

3. **At Program Exit:**
   - Libraries are closed in reverse order via `CloseLibrary()`
   - Return code from `main()` is preserved through cleanup
   - Program returns to AmigaOS CLI

**Why Novus Uses Custom Startup Instead of libauto:**
- Full control over initialization order
- Explicit error handling for each library
- No dependency on VBCC's auto-open mechanism
- We define the library base symbols ourselves, which prevents libauto from working anyway

**The Critical Lesson:**
If you add storage for a library base but forget to add the `OpenLibrary()` call in `novus_startup.s`, the base will be NULL and your program will crash with Guru Meditation 80000006 (address error) when calling any function from that library.

## Critical: How @sizeof Works with NDK Structs

**Background:** C compilers add padding to structs for alignment. The actual sizes depend on the struct layout and compiler. For example, `NewMenu` in the NDK is 20 bytes with proper padding for field alignment.

**The Problem:**
- If Novus tried to calculate sizeof at compile time without VBCC, it might get the wrong size
- Memory allocations could be too small
- Struct copying could copy the wrong number of bytes
- Result: Guru Meditation crashes on Amiga

**The Solution:**
Novus's `@sizeof` operator emits C's `sizeof()` in the generated code, NOT a hardcoded number:

```novus
let size = @sizeof(NewMenu)  // In Novus code
```

Generates:

```c
uint32_t size = sizeof(NewMenu);  // In C code - VBCC evaluates this
```

**Why This Works:**
1. Novus emits `sizeof(NewMenu)` as a C expression
2. VBCC evaluates `sizeof(NewMenu)` using the NDK's struct definition (with proper padding)
3. Result: Correct size from VBCC/NDK, not a potentially wrong hardcoded value from Novus

**Critical Requirements:**
- The struct MUST be in the NDK skip list
- The typedef MUST be emitted in novus_types.h
- The NDK header MUST be included (via proto headers)

If any of these are missing, VBCC will use Novus's struct definition and calculate the wrong size.

**Verification:**
Look at the generated C code. You should see:
```c
// novus_types.h
typedef struct NewMenu NewMenu;  // ← Just typedef, no definition

// In function:
uint32_t size = sizeof(NewMenu);  // ← VBCC evaluates this
```

NOT:
```c
uint32_t size = 20;  // ← Wrong! Hardcoded by Novus
```

## Common Pitfalls

### 1. Forgetting to Update Startup Code (MOST COMMON!)

**Problem:** Guru Meditation 80000006 (address error) when calling any library function.

**Symptom:** Your program crashes immediately when calling functions from the new library, but other libraries (like Intuition) work fine.

**Root Cause:** You added storage for `_YourLibBase` in `library_bases.s` but forgot to add `OpenLibrary()` and `CloseLibrary()` calls in `novus_startup.s`. The base pointer is NULL.

**Solution:** Add the library initialization to `novus_startup.s`:
```assembly
; Add xref
xref _YourLibBase

; Add OpenLibrary call
movea.l	_SysBase,a6
lea	.yourlib_name(pc),a1
moveq	#37,d0			; Minimum version
jsr	-552(a6)		; OpenLibrary()
move.l	d0,_YourLibBase
beq.s	.no_yourlib

; Add CloseLibrary call (before closing other libs)
move.l	d0,-(sp)
movea.l	_SysBase,a6
movea.l	_YourLibBase,a1
jsr	-414(a6)		; CloseLibrary()
move.l	(sp)+,d0

; Add name string
.yourlib_name:
	dc.b	'yourlib.library',0
	even
```

**This is the #1 mistake when adding new library support!**

### 2. Forgetting to Update NDK Struct Skip List

**Problem:** VBCC error: `redefinition of struct NewMenu` or similar.

**Solution:** You MUST add the struct to BOTH places in CCodeGenerator.cs:
1. The `ndkStructs` skip list (line ~338)
2. The typedef generation (line ~295-298)

**Why:** Novus uses the struct for type checking, but VBCC must use the NDK's definition for correct padding.

### 3. Forgetting to Rebuild After Stub Changes

**Problem:** You add `_GadToolsBase` to `library_bases.s` but get linker errors.

**Solution:** You must rebuild the Novus project to copy the updated stub:
```bash
dotnet build -c Release
```

The stdlib build (`stdlib-build`) only rebuilds `.novus` files, NOT assembly stubs.

### 4. Wrong Struct Layout / sizeof Mismatch

**Problem:** Crashes, Guru Meditation, or garbage data when passing structs to library. Common symptom: `sizeof(YourStruct)` in Novus doesn't match C's sizeof.

**Root Cause:** Struct isn't in the NDK skip list, so the compiler emits its own struct definition which may have different padding.

**Solution:**
- **FIRST**: Add the struct to the NDK skip list (see Step 4 above)
- This ensures VBCC calculates sizeof using the NDK definition with correct padding
- Verify in generated C code: should see `typedef struct YourStruct YourStruct;` but NO `struct YourStruct { ... }` definition
- Test: `@sizeof(YourStruct)` should emit `sizeof(YourStruct)` in C code, not a hardcoded number

### 5. Missing Library Base Definition

**Problem:** `Error 21: Reference to undefined symbol _YourLibBase`

**Solution:** Add the library base to `library_bases.s`:
```assembly
xdef _YourLibBase
_YourLibBase:
    ds.l 1
```

**Remember:** You also need to add the OpenLibrary/CloseLibrary calls in `novus_startup.s`!

### 6. Using Wrong Pointer Types

**Problem:** Type errors or crashes when calling library functions.

**Solution:**
- `*u8` for char pointers and opaque pointers
- `*YourStruct` for struct pointers
- Match the C signatures exactly

### 7. Not Handling NULL Returns

**Problem:** Crashes when library functions fail.

**Solution:** Always check for NULL and return Result:
```novus
let menu = CreateMenusA(array, (*TagItem)0)
if !menu {
    return Result::Err(IntuitionError::MenuCreateFailed)
}
```

## Testing Your Library Support

Create a minimal test program:

```novus
// test_yourlib.novus
from amiga::raw::yourlib import SomeFunction
from std::io::core import write

pub fn main() -> i32 {
    unsafe {
        let result = SomeFunction((*YourType)0)
        if result {
            write("Library function worked!\n")
            return 0
        } else {
            write("Library function failed!\n")
            return 20
        }
    }
}
```

Compile and test:
```bash
dotnet bin/Release/net9.0/Novus.dll compile test_yourlib.novus -o test
# Copy to Amiga and run
```

## GadTools Case Study Summary

For GadTools, we:

1. ✅ Added `_GadToolsBase` to `library_bases.s`
2. ✅ **Added OpenLibrary/CloseLibrary calls to `novus_startup.s`** (CRITICAL!)
3. ✅ Created `std/amiga/raw/gadtools.novus` with function bindings
4. ✅ Added `NewMenu` and `VisualInfo` structs to `amiga_structs.novus`
5. ✅ **Added `NewMenu` and `VisualInfo` to NDK skip list in `CCodeGenerator.cs`** (CRITICAL!)
6. ✅ **Added typedefs for `NewMenu` and `VisualInfo` in `CCodeGenerator.cs`** (CRITICAL!)
7. ✅ Added `NM_*` constants to `amiga_consts.novus`
8. ✅ Created `std/graphics/menus.novus` with hierarchical builder API
9. ✅ Added `LibraryOpenFailed` to `IntuitionError` enum
10. ✅ Rebuilt compiler and stdlib

**Result:** Full GadTools menu support with ergonomic Rust-like API, type safety, and correct struct layout from NDK headers.

**Key Insight:** Steps 2, 5, and 6 are CRITICAL:
- Without Step 2: Guru Meditation 80000006 crash (NULL library base)
- Without Steps 5-6: VBCC redefinition errors and incorrect sizeof calculations

**The Bug We Fixed:**
We had storage for `_GadToolsBase` but forgot to open the library in `novus_startup.s`. Windows worked (Intuition was opened), but calling `CreateMenusA()` crashed because `_GadToolsBase` was NULL. Adding the OpenLibrary/CloseLibrary calls fixed it immediately.

## Advanced: Library Versioning and Error Handling

The startup code opens libraries with minimum version requirements:

```assembly
moveq	#37,d0			; v37 = AmigaOS 2.0+ (for GadTools)
moveq	#33,d0			; v33 = AmigaOS 1.2+ (for basic Intuition)
```

**Common Library Versions:**
- **v33** - AmigaOS 1.2 (basic functionality)
- **v36** - AmigaOS 2.0 beta
- **v37** - AmigaOS 2.0 (GadTools, ASL, etc.)
- **v39** - AmigaOS 3.0 (enhanced features)

**Error Handling:**
Currently, if a library fails to open, the startup code skips calling `main()` and exits. For better error handling, you could:
- Display an error message before exiting
- Fall back to a reduced feature set
- Open optional libraries only when needed (lazy loading)

**Future Enhancement:**
Consider adding a callback system or error codes so programs can know which library failed to open and why.

## Conclusion

Adding library support to Novus requires these essential steps:
1. Add base storage to `library_bases.s`
2. **Add OpenLibrary/CloseLibrary calls to `novus_startup.s`** (don't skip this!)
3. Create FFI bindings
4. Update `CCodeGenerator.cs` for any NDK structs

The hard work is creating ergonomic high-level APIs - but that's optional and can be done incrementally.

**Key takeaway:** Novus uses custom startup code, NOT VBCC's auto-library-opening. You MUST add the OpenLibrary/CloseLibrary calls manually, or your program will crash with Guru Meditation 80000006 when calling library functions. This is the most common mistake when adding new library support.
