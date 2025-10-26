# Automatic Memory Management for Novus

## Goal
Eliminate manual AllocMem/FreeMem calls while maintaining deterministic, efficient memory management suitable for the Amiga platform.

## Design Philosophy

**Inspired by:**
- Swift's ARC (automatic reference counting)
- Rust's RAII and ownership system
- Zig's explicit allocation + defer
- C++ smart pointers and destructors

**Key Principles:**
1. **Zero-cost abstraction** - No runtime overhead compared to manual management
2. **Deterministic** - You know exactly when memory is freed
3. **Explicit when needed** - No hidden allocations
4. **Safe by default** - Compiler prevents leaks and double-frees

---

## Three-Tier Approach

### Tier 1: Stack Allocation (Default)
Structs and basic types live on the stack by default. Zero allocation overhead.

```novus
fn example() {
    var point = Point { x: 10, y: 20 }  // Stack allocated
    var array = [1, 2, 3, 4, 5]          // Stack allocated

    // ... use them ...

}  // Automatically cleaned up (stack pop)
```

**Generated code:** Just stack frame management, no alloc/free.

---

### Tier 2: Box<T> - Owned Heap Allocation
When you need heap memory with single ownership. Compiler tracks lifetime.

```novus
fn load_large_data() -> Box<Buffer> {
    var buf = Box.new(Buffer.alloc(65536))  // AllocMem called here

    // Process buf...

    return buf  // Ownership transferred to caller
}

fn main() {
    var data = load_large_data()

    // Use data...

}  // Box destructor calls FreeMem here automatically
```

**Generated code:**
```asm
_load_large_data:
    ; Call AllocMem
    move.l  #65536,d0
    move.l  d0,-(sp)
    move.l  #MEMF_PUBLIC,d0
    move.l  d0,-(sp)
    jsr     _AllocMem
    ; Store pointer...
    rts

_main:
    bsr     _load_large_data
    ; Use data...

    ; Compiler inserts cleanup:
    move.l  _data_ptr,-(sp)
    move.l  #65536,-(sp)
    jsr     _FreeMem
    rts
```

---

### Tier 3: Rc<T> - Reference Counted Heap Allocation
When you need shared ownership (multiple references to same data).

```novus
struct Rc<T> {
    ptr: *T              // Pointer to actual data
    ref_count: *u32      // Pointer to ref count
}

impl Rc<T> {
    // Create new Rc with ref count = 1
    fn new(value: T) -> Rc<T> {
        var layout = sizeof(T) + sizeof(u32)
        var mem = AllocMem(layout, MEMF_PUBLIC | MEMF_CLEAR)

        var ref_ptr = mem as *u32
        *ref_ptr = 1  // Initial ref count

        var data_ptr = (mem + 4) as *T
        *data_ptr = value

        return Rc { ptr: data_ptr, ref_count: ref_ptr }
    }

    // Clone increments ref count
    fn clone(self: &Rc<T>) -> Rc<T> {
        *self.ref_count = *self.ref_count + 1
        return Rc { ptr: self.ptr, ref_count: self.ref_count }
    }

    // Drop decrements ref count, frees if zero
    fn drop(self: &mut Rc<T>) {
        *self.ref_count = *self.ref_count - 1
        if *self.ref_count == 0 {
            FreeMem(self.ref_count as *u8, sizeof(T) + sizeof(u32))
        }
    }
}

// Usage:
fn example() {
    var shared = Rc.new(ExpensiveData { ... })

    {
        var copy = shared.clone()  // ref_count = 2
        process(copy)
    }  // copy.drop() called, ref_count = 1

}  // shared.drop() called, ref_count = 0, memory freed
```

---

## Compiler Implementation

### Phase 1: Add Drop Tracking to IR

New IR instructions:
```csharp
// Track variables that need cleanup
class IrDrop : IrInstruction
{
    public string VariableName { get; }
    public IrType Type { get; }

    // Calls the type's drop function (or FreeMem for Box)
}

// Marks a value as moved (transferred ownership)
class IrMove : IrInstruction
{
    public string FromVar { get; }
    public string ToVar { get; }

    // Prevents FromVar's drop from being called
}
```

### Phase 2: Lifetime Analysis

In `SemanticAnalyzer.cs`:
```csharp
class LifetimeTracker
{
    Dictionary<string, VariableLifetime> _lifetimes = new();

    void TrackVariable(string name, IrType type, bool isOwned)
    {
        _lifetimes[name] = new VariableLifetime {
            Name = name,
            Type = type,
            IsOwned = isOwned,  // Does this var own its memory?
            ScopeEnd = null
        };
    }

    void OnScopeExit()
    {
        // Insert drop calls for all owned variables
        foreach (var (name, lifetime) in _lifetimes)
        {
            if (lifetime.IsOwned && !lifetime.Moved)
            {
                InsertDropCall(name, lifetime.Type);
            }
        }
    }
}
```

### Phase 3: Insert Cleanup Code

In `IrBuilder.cs`:
```csharp
public override object? VisitBlock(BlockContext context)
{
    EnterScope();

    // Visit all statements...
    foreach (var stmt in context.statement())
    {
        Visit(stmt);
    }

    // Before exiting scope, insert drop calls
    InsertScopeCleanup();

    ExitScope();
}

private void InsertScopeCleanup()
{
    var currentScope = _scopeStack.Peek();

    // Insert drops in reverse order (LIFO - last allocated, first freed)
    foreach (var varName in currentScope.OwnedVariables.Reverse())
    {
        var varInfo = currentScope.Variables[varName];

        if (varInfo.Type is IrBoxType boxType)
        {
            // Generate FreeMem call for Box<T>
            InsertBoxDrop(varName, boxType);
        }
        else if (varInfo.Type is IrRcType rcType)
        {
            // Generate Rc.drop call
            InsertRcDrop(varName, rcType);
        }
        else if (HasDropMethod(varInfo.Type))
        {
            // Call custom drop method
            InsertCustomDrop(varName, varInfo.Type);
        }
    }
}
```

### Phase 4: Code Generation

In `M68kCodeGenerator.cs`:
```csharp
private void GenerateDrop(IrDrop drop)
{
    if (drop.Type is IrBoxType boxType)
    {
        // Load pointer from variable
        Emit($"\tmove.l\t{GetVariableLocation(drop.VariableName)},-(sp)");

        // Load size
        Emit($"\tmove.l\t#{boxType.InnerType.SizeInBytes},-(sp)");

        // Call FreeMem
        Emit($"\tjsr\t_FreeMem");
        Emit($"\tlea\t8(sp),sp");
    }
}
```

---

## Examples

### Example 1: Simple Box Allocation

**Novus code:**
```novus
fn test_box() -> i32 {
    var buf = Box.new<u8>(1024, MEMF_CHIP)

    // Use buffer...
    buf[0] = 42

    return 0
}  // buf automatically freed here
```

**Generated assembly:**
```asm
_test_box:
    link    a6,#-4

    ; Allocate memory
    move.l  #2,d0           ; MEMF_CHIP
    move.l  d0,-(sp)
    move.l  #1024,d0
    move.l  d0,-(sp)
    jsr     _AllocMem
    lea     8(sp),sp
    move.l  d0,-4(a6)       ; Store pointer in buf

    ; Use buffer...
    move.l  -4(a6),a0
    move.b  #42,(a0)

    ; Cleanup: Free buffer
    move.l  #1024,d0
    move.l  d0,-(sp)
    move.l  -4(a6),d0
    move.l  d0,-(sp)
    jsr     _FreeMem
    lea     8(sp),sp

    moveq   #0,d0
    unlk    a6
    rts
```

### Example 2: Ownership Transfer

**Novus code:**
```novus
fn allocate_buffer() -> Box<Buffer> {
    var buf = Box.new<Buffer>(4096, MEMF_PUBLIC)
    return buf  // Ownership transferred, no drop here
}

fn main() {
    var my_buf = allocate_buffer()
    // Use my_buf...
}  // my_buf dropped here (in main, not in allocate_buffer)
```

### Example 3: Early Drop

**Novus code:**
```novus
fn example() {
    var buf1 = Box.new<u8>(1024, MEMF_CHIP)

    {
        var buf2 = Box.new<u8>(2048, MEMF_CHIP)
        // Use buf2...
    }  // buf2 dropped here

    // buf1 still valid here

}  // buf1 dropped here
```

---

## Benefits

1. **No manual FreeMem needed** - Compiler handles it
2. **No leaks** - Compiler ensures all allocations are freed
3. **No double-frees** - Ownership system prevents this
4. **Zero overhead** - Same assembly as manual management
5. **Explicit when needed** - Box/Rc make heap allocation visible
6. **Works great on Amiga** - No GC runtime, deterministic timing

---

## Implementation Phases

### Phase 1: Foundation (Week 1)
- [ ] Add Box<T> type to type system
- [ ] Add IrDrop instruction
- [ ] Add IrMove instruction for ownership transfer
- [ ] Implement basic scope tracking

### Phase 2: Lifetime Analysis (Week 2)
- [ ] Build ownership tracking in SemanticAnalyzer
- [ ] Detect when variables go out of scope
- [ ] Insert drop calls at scope boundaries
- [ ] Handle ownership transfer (moves)

### Phase 3: Code Generation (Week 3)
- [ ] Generate FreeMem calls for Box drops
- [ ] Handle nested scopes
- [ ] Handle early returns (drop everything on path)
- [ ] Handle break/continue in loops

### Phase 4: Advanced Features (Week 4)
- [ ] Implement Rc<T> in standard library
- [ ] Add weak references for cycle breaking
- [ ] Custom drop methods for user types
- [ ] Optimization: elide unnecessary drops

---

## Future Enhancements

1. **Borrow checker** (Rust-style) - Prevent use-after-free at compile time
2. **Arena allocators** - Allocate many objects, free all at once
3. **Pool allocators** - Reuse fixed-size objects
4. **defer keyword** (Zig-style) - Explicit cleanup at scope end

---

## Comparison with Other Languages

| Feature | C | C++ | Rust | Swift | Novus (Proposed) |
|---------|---|-----|------|-------|------------------|
| Manual alloc/free | ✅ | ✅ | ❌ | ❌ | ❌ |
| RAII | ❌ | ✅ | ✅ | ❌ | ✅ |
| Reference counting | ❌ | ✅ (shared_ptr) | ✅ (Rc) | ✅ (ARC) | ✅ (Rc) |
| Borrow checker | ❌ | ❌ | ✅ | ❌ | 🔮 Future |
| Zero overhead | ✅ | ✅ | ✅ | ❌ | ✅ |
| Works on 68k | ✅ | ✅ | ✅ | ❌ | ✅ |

---

## Questions for Discussion

1. Should Box<T> be implicit or explicit?
   - Option A: `let buf = alloc(1024)` → compiler wraps in Box automatically
   - Option B: `let buf = Box.new(1024)` → explicit allocation

2. Should we implement borrow checking (Rust-style)?
   - Pros: Prevents many bugs at compile time
   - Cons: More complex, steeper learning curve

3. How to handle FFI calls that return pointers?
   - Option A: Wrap in Box, assume we own it
   - Option B: Return raw pointer, user must explicitly wrap

4. Should strings be Box<str> by default?
