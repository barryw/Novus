# `if let` Syntax Design for Novus

**Status:** Design Document
**Date:** 2025-11-01

---

## 🎯 Goal

Add Swift-style `if let` syntax to Novus for elegant null pointer checking and value binding.

---

## 📋 Motivation

### Current (Verbose):

```novus
pub fn OpenWindow(tags_ptr: *TagItem, count: u32) -> Result<*Window, NovusError> {
    let tag_list = make_tags(tags_ptr, count)
    let window = OpenWindowTagList(null_new_window, tag_list.as_ptr())

    // Verbose null check with cast
    if (u32)window == 0 {
        return Result::Err(novus_error_from_intuition(IntuitionError::WindowOpenFailed))
    }

    return Result::Ok(window)
}
```

### Desired (Clean):

```novus
pub fn OpenWindow(tags_ptr: *TagItem, count: u32) -> Result<*Window, NovusError> {
    let tag_list = make_tags(tags_ptr, count)
    let window = OpenWindowTagList(null_new_window, tag_list.as_ptr())

    // Clean if let with implicit non-null check
    if let win = window {
        return Result::Ok(win)
    }

    return Result::Err(novus_error_from_intuition(IntuitionError::WindowOpenFailed))
}
```

---

## 🎨 Syntax

```novus
if let <identifier> = <expression> {
    // <identifier> is bound to non-null value
    // This block executes if expression is non-null
}
```

### With else:

```novus
if let <identifier> = <expression> {
    // Non-null path
} else {
    // Null path
}
```

---

## 📐 Semantics

### For Pointer Types

```novus
let ptr: *Window = get_window()

if let p = ptr {
    // p is bound to ptr (which is non-null)
    // p has type *Window
    // This block executes if ptr != 0
    use_window(p)
} else {
    // ptr was null
}
```

**Desugars to:**

```novus
let ptr: *Window = get_window()

if (u32)ptr != 0 {
    let p = ptr
    use_window(p)
} else {
    // ptr was null
}
```

### For Integer Types (with cast)

```novus
let ptr: *Window = get_window()

if let ptr_int = (u32)ptr {
    // ptr_int is bound to the u32 value (non-zero)
    // This block executes if ptr != 0
    // ptr_int has type u32
    return Result::Ok(ptr_int)
}
```

**Desugars to:**

```novus
let ptr: *Window = get_window()
let ptr_int = (u32)ptr

if ptr_int != 0 {
    return Result::Ok(ptr_int)
}
```

---

## 🔧 Grammar Changes

### Current if statement:

```antlr
ifStatement
    : 'if' expression block ('else' (ifStatement | block))?
    ;
```

### New if let statement:

```antlr
ifStatement
    : 'if' 'let' IDENTIFIER '=' expression block ('else' (ifStatement | block))?  // if let
    | 'if' expression block ('else' (ifStatement | block))?                        // regular if
    ;
```

---

## 💡 Use Cases

### Use Case 1: Pointer Null Check

```novus
let window = OpenWindowTagList(...)

if let w = window {
    // w is non-null window pointer
    return Result::Ok(w)
}
return Result::Err(WindowOpenFailed)
```

### Use Case 2: Cast + Null Check

```novus
let ptr: *u8 = AllocMem(...)

if let addr = (u32)ptr {
    // addr is non-zero u32
    FreeMem((i32)addr, size)
}
```

### Use Case 3: Optional Unwrapping (Future)

```novus
let opt: Option<i32> = get_value()

if let value = opt {
    // value is the unwrapped i32 from Option::Some
    println("Got value: {}", value)
} else {
    // opt was Option::None
    println("No value")
}
```

---

## 🎯 Type Rules

### Rule 1: Pointer Types

For pointer types, the condition is `(u32)expr != 0`:

```novus
let ptr: *T = ...

if let p = ptr {
    // Condition: (u32)ptr != 0
    // Binding: p has type *T
}
```

### Rule 2: Integer Types

For integer types, the condition is `expr != 0`:

```novus
let x: u32 = ...

if let y = x {
    // Condition: x != 0
    // Binding: y has type u32
}
```

### Rule 3: Boolean Types

For bool, the condition is `expr == true`:

```novus
let flag: bool = ...

if let f = flag {
    // Condition: flag == true
    // Binding: f has type bool
}
```

### Rule 4: Option Types (Future)

For `Option<T>`, the condition is pattern matching:

```novus
let opt: Option<T> = ...

if let value = opt {
    // Condition: opt is Option::Some(value)
    // Binding: value has type T
}
```

---

## 🔨 Implementation Steps

### Phase 1: Grammar

1. Add `if let` alternative to grammar
2. Parse `if let IDENTIFIER = expression`
3. Create AST node with binding variable

### Phase 2: Semantic Analysis

1. Check that binding variable doesn't shadow existing variable (or allow it?)
2. Determine type of binding variable (same as expression type)
3. Validate expression type is checkable (pointer, int, bool, Option)
4. Generate synthetic condition based on type

### Phase 3: IR Generation

1. Evaluate expression
2. Generate null/zero check based on type
3. Create binding variable in if-block scope
4. Assign expression value to binding variable

### Phase 4: Code Generation

Should automatically work since IR is lowered to existing constructs.

---

## 📝 Examples

### Example 1: Window Opening (Clean)

**Before:**
```novus
pub fn OpenWindow(tags_ptr: *TagItem, count: u32) -> Result<*Window, NovusError> {
    let tag_list = make_tags(tags_ptr, count)
    let window = OpenWindowTagList(null_new_window, tag_list.as_ptr())

    if (u32)window == 0 {
        return Result::Err(novus_error_from_intuition(IntuitionError::WindowOpenFailed))
    }

    return Result::Ok(window)
}
```

**After:**
```novus
pub fn OpenWindow(tags_ptr: *TagItem, count: u32) -> Result<*Window, NovusError> {
    let tag_list = make_tags(tags_ptr, count)
    let window = OpenWindowTagList(null_new_window, tag_list.as_ptr())

    if let win = window {
        return Result::Ok(win)
    }

    return Result::Err(novus_error_from_intuition(IntuitionError::WindowOpenFailed))
}
```

### Example 2: Memory Allocation

**Before:**
```novus
pub fn CoreAlloc(byteSize: u32, attributes: u32) -> Option<*u8> {
    let ptr: *u8 = SafeAllocMem(byteSize, attributes)

    if (u32)ptr == 0 {
        return Option::None
    }

    return Option::Some(ptr)
}
```

**After:**
```novus
pub fn CoreAlloc(byteSize: u32, attributes: u32) -> Option<*u8> {
    let ptr: *u8 = SafeAllocMem(byteSize, attributes)

    if let p = ptr {
        return Option::Some(p)
    }

    return Option::None
}
```

### Example 3: With Else Block

```novus
if let window = OpenWindow(...) {
    // Use window
    DoStuffWith(window)
    CloseWindow(window)
} else {
    // Window open failed
    println("Failed to open window")
}
```

---

## 🚨 Edge Cases

### Shadow Existing Variable?

```novus
let x: *Window = ...

if let x = get_window() {
    // Should this be allowed?
    // Option A: Error (variable already exists)
    // Option B: Shadow x within if block (like Swift)
}
```

**Decision:** Allow shadowing within the if block (like Swift/Rust).

### Multiple Bindings?

```novus
// Future enhancement
if let a = expr1, let b = expr2 {
    // Both non-null
}
```

**Decision:** Not in Phase 1. Can add later.

---

## ✅ Success Criteria

1. `if let x = ptr` works for pointer types
2. `if let x = (u32)ptr` works for casted values
3. Binding variable is scoped to if block
4. Else block works correctly
5. All existing tests pass
6. New tests for `if let` pass

---

**End of Document**

## Next Steps

1. Update grammar with `if let` alternative
2. Update parser to handle `if let` syntax
3. Add semantic analysis for `if let`
4. Add IR generation for `if let`
5. Write tests
6. Update standard library to use `if let`
