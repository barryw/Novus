# Drop Trait and RAII for Novus

## Overview

Implement automatic resource cleanup through the `Drop` trait, similar to Rust's RAII (Resource Acquisition Is Initialization) pattern.

## Goals

1. **Automatic cleanup**: Values are automatically cleaned up when they go out of scope
2. **Memory safety**: Prevent memory leaks in stdlib (MemoryBlock, Vec, String, etc.)
3. **Deterministic destruction**: Destructors run at a precise, predictable point
4. **Zero runtime overhead**: All destructor calls inserted at compile-time
5. **Explicit control**: Users can still call `drop()` manually via `defer` if needed

## The Drop Trait

```novus
// In std::core
pub trait Drop {
    fn drop(&mut self)
}
```

**Semantics**:
- Called automatically when a value goes out of scope
- Takes `&mut self` to allow cleanup of owned resources
- Cannot be called manually (compiler enforces this)
- Only called if value wasn't moved

## When Drop is Called

### 1. End of Block Scope
```novus
fn test() {
    let x = MemoryBlock::alloc(1024, MEMF_FAST)?
    // ... use x ...
}  // ← x.drop() called here automatically
```

### 2. Variable Shadowing
```novus
let x = MemoryBlock::alloc(1024, MEMF_FAST)?
let x = MemoryBlock::alloc(2048, MEMF_FAST)?  // ← old x.drop() called here
```

### 3. Reassignment
```novus
var x = MemoryBlock::alloc(1024, MEMF_FAST)?
x = MemoryBlock::alloc(2048, MEMF_FAST)?  // ← old x.drop() called before assignment
```

### 4. Early Return
```novus
fn test() -> Result<i32, ExecError> {
    let x = MemoryBlock::alloc(1024, MEMF_FAST)?
    if some_condition {
        return Err(ExecError::NoMem)  // ← x.drop() called before return
    }
    // ... use x ...
}  // ← x.drop() called here too (if we didn't return early)
```

### 5. Loop Exit (break/continue)
```novus
while true {
    let x = MemoryBlock::alloc(1024, MEMF_FAST)?
    if done {
        break  // ← x.drop() called before break
    }
}
```

## When Drop is NOT Called

### 1. Value Was Moved
```novus
let x = MemoryBlock::alloc(1024, MEMF_FAST)?
let y = x  // x is moved to y
// x.drop() NOT called (x is no longer valid)
// y.drop() will be called when y goes out of scope
```

### 2. Field Was Partially Moved
```novus
struct Pair {
    first: MemoryBlock,
    second: MemoryBlock,
}

let p = Pair { ... }
consume(p.first)  // p.first is moved
// p.first.drop() NOT called
// p.second.drop() IS called when p goes out of scope
```

### 3. Inside `unsafe` Block with Manual Management
```novus
unsafe {
    let ptr = AllocMem(1024, MEMF_FAST)
    // Manual management - no Drop called
    FreeMem(ptr, 1024)
}
```

## Drop Order

Destructors are called in **reverse order of construction** (like Rust):

```novus
let a = MemoryBlock::alloc(1024, MEMF_FAST)?
let b = MemoryBlock::alloc(2048, MEMF_FAST)?
let c = MemoryBlock::alloc(4096, MEMF_FAST)?
// Drop order: c.drop(), then b.drop(), then a.drop()
```

**Rationale**: Later values may depend on earlier values, so destroy in reverse.

## Struct Fields Drop Order

Fields are dropped in **declaration order**:

```novus
struct Container {
    header: MemoryBlock,
    data: Vec<u8>,
    footer: MemoryBlock,
}

// Drop order: header.drop(), data.drop(), footer.drop()
```

## Implementation Plan

### Phase 1: IR and Type System (2-3 days)

**1. Add Drop trait to core.novus**
```novus
pub trait Drop {
    fn drop(&mut self)
}
```

**2. Track Drop implementations in IR**
- Add `HasDrop` flag to `IrStructType`
- Check if type implements `Drop` trait during semantic analysis

**3. Track move state per variable**
- Extend existing move tracking to include "needs_drop" flag
- If moved, clear the "needs_drop" flag

### Phase 2: Semantic Analysis (3-4 days)

**1. Insert drop calls at scope exit**

In `SemanticAnalyzer`, track:
```csharp
private class ScopeInfo {
    public List<VariableInfo> LiveVariables { get; set; } = new();
    public SourceLocation EndLocation { get; set; }
}

private Stack<ScopeInfo> _scopeStack = new();
```

**2. At end of block/function**:
```csharp
// In VisitBlock() or VisitFunctionDeclaration()
foreach (var variable in currentScope.LiveVariables.Reverse()) {
    if (variable.NeedsDrop && !variable.WasMoved) {
        EmitDropCall(variable);
    }
}
```

**3. Before early returns**:
```csharp
// In VisitReturnStatement()
foreach (var scope in _scopeStack.Reverse()) {
    foreach (var variable in scope.LiveVariables.Reverse()) {
        if (variable.NeedsDrop && !variable.WasMoved) {
            EmitDropCall(variable);
        }
    }
}
```

**4. Before reassignment**:
```csharp
// In VisitAssignment()
if (targetVariable.NeedsDrop && !targetVariable.WasMoved) {
    EmitDropCall(targetVariable);
}
// Then do the assignment
```

### Phase 3: Code Generation (1-2 days)

**1. Emit drop calls in C code**:
```csharp
// In CCodeGenerator
private void EmitDropCall(IrVariable variable) {
    var typeName = GetTypeName(variable.Type);
    _output.AppendLine($"    {typeName}_drop(&{variable.Name});");
}
```

**2. Handle drop in generated C**:
```c
// For MemoryBlock
void MemoryBlock_drop(MemoryBlock* self) {
    if (self->size > 0) {
        FreeMem(self->ptr, self->size);
        self->ptr = (uint8_t*)0;
        self->size = 0;
    }
}
```

### Phase 4: Stdlib Implementation (1 day)

**1. Implement Drop for MemoryBlock**:
```novus
impl Drop for MemoryBlock {
    fn drop(&mut self) {
        self.free()  // Delegates to existing free() method
    }
}
```

**2. Implement Drop for Allocation<T>**:
```novus
impl<T> Drop for Allocation<T> {
    fn drop(&mut self) {
        self.block.drop()  // Compiler automatically calls this
        self.count = 0
    }
}
```

**3. Implement Drop for Box<T>**:
```novus
impl<T> Drop for Box<T> {
    fn drop(&mut self) {
        self.alloc.drop()  // Compiler automatically calls this
    }
}
```

**4. Implement Drop for Vec<T>**:
```novus
impl<T> Drop for Vec<T> {
    fn drop(&mut self) {
        if self.capacity > 0 {
            let element_size: u32 = @sizeof(T)
            let bytes: u32 = self.capacity * element_size
            let mem: *u8 = (*u8)(self.ptr)
            unsafe { FreeMem(mem, bytes) }
            self.ptr = 0
            self.len = 0
            self.capacity = 0
        }
    }
}
```

**5. Implement Drop for String**:
```novus
impl Drop for String {
    fn drop(&mut self) {
        self.vec.drop()  // Compiler automatically calls this
    }
}
```

### Phase 5: Testing (1-2 days)

**Test Cases**:

1. **Basic drop**:
```novus
fn test_basic_drop() {
    let x = MemoryBlock::alloc(1024, MEMF_FAST)?
    // x.drop() called automatically
}
```

2. **Drop order**:
```novus
fn test_drop_order() {
    let a = MemoryBlock::alloc(1024, MEMF_FAST)?
    let b = MemoryBlock::alloc(2048, MEMF_FAST)?
    // Verify: b.drop() called first, then a.drop()
}
```

3. **No drop after move**:
```novus
fn test_no_drop_after_move() {
    let x = MemoryBlock::alloc(1024, MEMF_FAST)?
    let y = x  // x is moved
    // Only y.drop() called, not x.drop()
}
```

4. **Drop on early return**:
```novus
fn test_early_return() -> Result<i32, ExecError> {
    let x = MemoryBlock::alloc(1024, MEMF_FAST)?
    if some_condition {
        return Err(ExecError::NoMem)  // x.drop() called here
    }
    Ok(42)  // x.drop() called here too
}
```

5. **Partial move**:
```novus
fn test_partial_move() {
    struct Pair {
        first: MemoryBlock,
        second: MemoryBlock,
    }
    let p = Pair { ... }
    consume(p.first)  // p.first moved
    // Only p.second.drop() called
}
```

## Interaction with Existing Features

### With `defer` Blocks

`defer` runs **before** automatic drop:

```novus
fn test() {
    let x = MemoryBlock::alloc(1024, MEMF_FAST)?
    defer {
        // Custom cleanup here
        log("Cleaning up x")
    }
    // ... use x ...
}
// Order: defer block runs first, then x.drop()
```

### With Move Semantics

Drop respects move tracking:

```novus
let x = String::new()
let y = x  // x moved to y
// x.drop() NOT called (x is moved)
// y.drop() IS called
```

### With Partial Moves

Only non-moved fields are dropped:

```novus
struct Pair {
    first: String,
    second: String,
}
let p = Pair { ... }
consume(p.first)  // Move p.first
// p.second.drop() called, but NOT p.first.drop()
```

## Edge Cases

### 1. Cyclic References

**Problem**: Two objects reference each other
```novus
struct Node {
    next: Option<Box<Node>>,
}
```

**Solution**: Use weak references (future feature) or manual cleanup

### 2. Panics During Drop

**Problem**: What if drop() panics?

**Solution**:
- Drop should be no-fail (`Result` not allowed)
- Use `unsafe` for operations that can't fail
- Document that drop must not panic

### 3. Drop in Generics

**Problem**: Generic type might or might not have Drop

**Solution**: Compiler tracks at monomorphization time
```novus
struct Container<T> {
    value: T,
}

impl<T> Drop for Container<T> {
    fn drop(&mut self) {
        // Compiler automatically calls T's drop if T implements Drop
    }
}
```

## Benefits

1. ✅ **Zero memory leaks** in well-written code
2. ✅ **No manual cleanup** needed for common cases
3. ✅ **Predictable performance** - all cleanup at compile-time determined points
4. ✅ **Composable** - drop works with move semantics and generics
5. ✅ **Safe** - compiler prevents double-drop and use-after-drop

## Timeline

- **Phase 1**: IR and Type System (2-3 days)
- **Phase 2**: Semantic Analysis (3-4 days)
- **Phase 3**: Code Generation (1-2 days)
- **Phase 4**: Stdlib Implementation (1 day)
- **Phase 5**: Testing (1-2 days)

**Total**: 8-12 days (1.5 - 2.5 weeks)

## Next Steps

1. Add Drop trait to std::core
2. Implement drop tracking in SemanticAnalyzer
3. Extend IR to support drop calls
4. Update code generator to emit drop calls
5. Implement Drop for stdlib types
6. Write comprehensive tests
