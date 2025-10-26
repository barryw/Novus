# Defer Statement for Novus

## Overview

`defer` schedules a closure to run at the end of the current scope, no matter how you exit (return, break, panic, etc.). Deferred closures execute in **reverse order** (LIFO - last deferred, first executed).

**Inspired by:** Swift, Go, Zig, Odin

---

## Basic Syntax

```novus
defer { /* cleanup code */ }
```

The closure captures variables from the enclosing scope.

---

## Basic Examples

### Example 1: Simple Cleanup

```novus
fn write_file(path: str, data: str) -> Result<(), Error> {
    let file = Open(path.as_ptr(), MODE_NEWFILE)
    if file == 0 {
        return Err(Error.CannotOpen)
    }
    defer { Close(file) }  // <-- Cleanup right next to allocation!

    Write(file, data.as_ptr(), data.len())?

    return Ok(())
}  // Close(file) called here automatically
```

### Example 2: Multiple Defers (LIFO order)

```novus
fn example() {
    println("Start")
    defer { println("Third") }   // Executed 3rd
    defer { println("Second") }  // Executed 2nd
    defer { println("First") }   // Executed 1st
    println("End")
}

// Output:
// Start
// End
// First
// Second
// Third
```

### Example 3: Capturing Variables

```novus
fn example() {
    let x = 10
    let mut y = 20

    defer {
        println("x = {}, y = {}", x, y)  // Captures x and y
        y = y + 1  // Can modify captured mutable variables
    }

    y = 30  // Modifies y

}  // defer executes: prints "x = 10, y = 30"
```

### Example 4: Working with NDK

```novus
use ffi::exec::*
use ffi::dos::*

fn load_and_process_file(path: str) -> Result<(), Error> {
    // Open dos.library
    let dos_base = OpenLibrary("dos.library", 0)
    if dos_base == null {
        return Err(Error.NoDosLibrary)
    }
    defer { CloseLibrary(dos_base) }  // Cleanup #3

    // Allocate buffer
    let buffer = AllocMem(4096, MEMF_PUBLIC | MEMF_CLEAR)
    if buffer == null {
        return Err(Error.OutOfMemory)
    }
    defer { FreeMem(buffer, 4096) }  // Cleanup #2

    // Open file
    let file = Open(path.as_ptr(), MODE_OLDFILE)
    if file == 0 {
        return Err(Error.CannotOpen)
    }
    defer { Close(file) }  // Cleanup #1

    // Process file...
    let bytes_read = Read(file, buffer, 4096)
    if bytes_read < 0 {
        return Err(Error.ReadFailed)
    }

    // Process buffer...

    return Ok(())
}
// Cleanup order (LIFO):
// 1. Close(file)
// 2. FreeMem(buffer, 4096)
// 3. CloseLibrary(dos_base)
```

**Notice:** Cleanup happens in **reverse order of allocation**. Last allocated, first freed. This is the natural cleanup order!

---

## Defer vs Box vs Rc

### When to use each:

| Scenario | Use | Why |
|----------|-----|-----|
| Simple owned heap allocation | `Box<T>` | Automatic, zero thought |
| Shared data | `Rc<T>` | Automatic ref counting |
| NDK resources (files, libraries, locks) | `defer` | Manual but safe |
| Complex cleanup logic | `defer` | Full control |
| Cleanup with captured variables | `defer` | Access to scope vars |

### Example: Mixing All Three

```novus
fn complex_example() -> Result<(), Error> {
    // Box: Simple heap allocation
    let buffer = Box.alloc(4096, MEMF_CHIP)?
    // Automatically freed at scope exit

    // defer: Manual resource cleanup
    let file = Open("data.txt", MODE_OLDFILE)
    if file == 0 {
        return Err(Error.CannotOpen)
    }
    defer { Close(file) }

    // Rc: Shared ownership
    let config = Rc.new(load_config()?);
    spawn_worker(config.clone())
    spawn_worker(config.clone())
    // Config freed when last Rc drops

    return Ok(())
}
// Cleanup order:
// 1. Close(file) - from defer
// 2. buffer dropped - from Box
// 3. config dropped - last Rc
```

---

## Multi-Statement Closures

```novus
fn example() {
    let x = 10
    let mut counter = 0

    defer {
        println("Cleanup starting")
        println("x = {}", x)
        counter = counter + 1
        println("Counter: {}", counter)
        println("Cleanup done")
    }

    // Do work...

}  // defer closure executes here
```

---

## Single Expression (Shorthand)

For simple cases, you can omit the braces:

```novus
defer Close(file)  // Equivalent to: defer { Close(file) }
```

But capturing variables requires explicit closure:

```novus
// These are equivalent:
defer { println("x = {}", x) }
defer println("x = {}", x)  // Shorthand - still captures x
```

---

## Defer in Loops

**Defers execute at end of scope, not each iteration!**

```novus
fn loop_example() {
    for i in 0..5 {
        defer { println("i = {}", i) }
        println("Loop {}", i)
    }
    println("Done")
}

// Output:
// Loop 0
// Loop 1
// Loop 2
// Loop 3
// Loop 4
// Done
// i = 4  <-- Only executed once at end!
// i = 3
// i = 2
// i = 1
// i = 0
```

**If you need per-iteration cleanup, use a nested scope:**

```novus
fn loop_with_per_iteration_cleanup() {
    for i in 0..5 {
        {  // Nested scope
            let temp = AllocMem(1024, MEMF_PUBLIC)
            defer { FreeMem(temp, 1024) }

            // Use temp...

        }  // defer executes here, each iteration!
    }
}
```

---

## Defer with Early Returns

**Defers execute on ALL exit paths!**

```novus
fn early_return_example(success: bool) -> Result<(), Error> {
    let resource = acquire_resource()
    defer { release_resource(resource) }

    if !success {
        return Err(Error.Failed)  // defer executes here!
    }

    // Do work...

    return Ok(())  // defer executes here too!
}
```

**Both return paths execute the defer!**

---

## Real-World Example: OpenWindow with defer

**Without defer:**

```novus
use ffi::intuition::*
use ffi::graphics::*

fn open_window_no_defer() -> i32 {
    let intuition_base = OpenLibrary("intuition.library", 0)
    if intuition_base == null {
        return -1
    }

    let graphics_base = OpenLibrary("graphics.library", 0)
    if graphics_base == null {
        CloseLibrary(intuition_base)  // Easy to forget!
        return -1
    }

    let window = OpenWindow(&NewWindow { /* ... */ })
    if window == null {
        CloseLibrary(graphics_base)
        CloseLibrary(intuition_base)
        return -1
    }

    // Use window...

    // Cleanup (error-prone!)
    CloseWindow(window)
    CloseLibrary(graphics_base)
    CloseLibrary(intuition_base)

    return 0
}
```

**With defer:**

```novus
use ffi::intuition::*
use ffi::graphics::*

fn open_window_with_defer() -> Result<(), Error> {
    let intuition_base = OpenLibrary("intuition.library", 0)
    if intuition_base == null {
        return Err(Error.LibraryNotFound)
    }
    defer { CloseLibrary(intuition_base) }  // Right next to allocation!

    let graphics_base = OpenLibrary("graphics.library", 0)
    if graphics_base == null {
        return Err(Error.LibraryNotFound)
    }
    defer { CloseLibrary(graphics_base) }  // Right next to allocation!

    let window = OpenWindow(&NewWindow { /* ... */ })
    if window == null {
        return Err(Error.CannotOpenWindow)
    }
    defer { CloseWindow(window) }  // Right next to allocation!

    // Use window...

    return Ok(())
}
// Automatic cleanup in correct order:
// 1. CloseWindow(window)
// 2. CloseLibrary(graphics_base)
// 3. CloseLibrary(intuition_base)
```

**Much cleaner! And it's impossible to forget cleanup!**

---

## Grammar Changes

```antlr
statement
    : varDeclaration
    | assignment
    | functionCall
    | returnStatement
    | ifStatement
    | whileStatement
    | forStatement
    | block
    | deferStatement        // NEW!
    | expression
    ;

deferStatement
    : 'defer' (expression | block)
    ;

// block is already defined:
// block: '{' statement* '}'
```

---

## Closure Capture Semantics

### Variables are Captured by Reference

```novus
fn capture_example() {
    let mut x = 10

    defer { println("x = {}", x) }  // Captures reference to x

    x = 20  // Modifies the captured variable

}  // Prints "x = 20" (not "x = 10")
```

**The defer closure sees the final value of x at the time the closure executes!**

### Moving Values into Closures

```novus
fn move_example() {
    let data = Box.alloc(1024, MEMF_CHIP)?

    // data is moved into the closure
    defer {
        process_final(data)  // data is still valid here
    }

    // data is no longer accessible here (moved)

}  // defer executes with ownership of data
```

---

## Implementation

### Phase 1: Parse defer statements

```csharp
// In Novus.g4
deferStatement
    : 'defer' (expression | block)
    ;

// In SemanticAnalyzer.cs and IrBuilder.cs

class Scope {
    List<IrClosure> DeferredClosures = new();
}

public override object? VisitDeferStatement(DeferStatementContext context) {
    // Parse the deferred closure
    var closure = ParseClosure(context)

    // Add to current scope's deferred list
    _currentScope.DeferredClosures.Add(closure)

    // Don't emit the closure now!
    return null
}
```

### Phase 2: Insert defers at scope exit

```csharp
void ExitScope() {
    var scope = _scopes.Pop()

    // First: Execute deferred closures (in reverse order)
    foreach (var deferredClosure in scope.DeferredClosures.Reverse()) {
        EmitClosureCall(deferredClosure)
    }

    // Then: Drop owned values (Box, etc.)
    foreach (var (name, type) in scope.OwnedValues.Reverse()) {
        if (type is IrBoxType boxType) {
            InsertBoxDrop(name, boxType)
        }
    }
}
```

### Phase 3: Handle early exits

```csharp
public override object? VisitReturnStatement(ReturnStatementContext context) {
    var returnValue = Visit(context.expression())

    // Execute ALL defers from all parent scopes (in reverse order)
    foreach (var scope in _scopes.Reverse()) {
        foreach (var deferredClosure in scope.DeferredClosures.Reverse()) {
            EmitClosureCall(deferredClosure)
        }
    }

    // Then return
    EmitReturn(returnValue)
}
```

### Phase 4: Closure Implementation

Closures need to capture variables. Two approaches:

**Approach 1: Inline expansion** (simpler, zero overhead)
```csharp
// defer { Close(file) }
// becomes:
// Close(file)  // Inlined at scope exit

// Captures are just variable references
```

**Approach 2: Closure struct** (if we need true closures later)
```csharp
// struct __defer_closure_1 {
//     file: i32  // Captured variable
// }
//
// let __closure = __defer_closure_1 { file: file }
// // At scope exit:
// Close(__closure.file)
```

For defer, **Approach 1** (inline expansion) is sufficient and has zero overhead!

---

## Execution Order Example

```novus
fn order_example() -> i32 {
    println("1")

    let box1 = Box.alloc(1024, MEMF_CHIP)?
    defer { println("2 (defer)") }

    {
        let box2 = Box.alloc(2048, MEMF_PUBLIC)?
        defer { println("3 (defer)") }

        if true {
            return 0  // Early return!
        }
    }

    println("4")  // Never executed
}

// Output:
// 1
// 3 (defer)       <- Inner defer executed first
// box2 dropped    <- Inner Box dropped
// 2 (defer)       <- Outer defer executed
// box1 dropped    <- Outer Box dropped
```

**Cleanup order:**
1. Deferred closures (reverse order)
2. Box/Rc drops (reverse order)
3. Both execute on all exit paths

---

## Edge Cases

### Defer in defer?

**Not allowed!**

```novus
defer { defer { println("No!") } }  // Compile error
```

**Rationale:** Too confusing. Use a single closure with multiple statements.

### Mutable captures

```novus
fn mutable_capture() {
    let mut counter = 0

    defer {
        counter = counter + 1
        println("Counter: {}", counter)
    }

    counter = 10

}  // Prints "Counter: 11"
```

### Panic in defer

```novus
fn panic_in_defer() {
    defer { panic("Uh oh") }

    println("This prints")

}  // panic happens here
```

**Subsequent defers still execute!**

```novus
fn multiple_panics() {
    defer { println("This prints") }
    defer { panic("Second panic") }
    defer { println("This prints too") }

    println("Start")
}

// Output:
// Start
// This prints too
// panic: Second panic
// This prints
```

---

## Benefits

✅ **Cleanup right next to allocation** - Easy to see what pairs with what
✅ **Can't forget cleanup** - Compiler guarantees it runs
✅ **Correct cleanup order** - LIFO (last allocated, first freed)
✅ **Works on all exit paths** - return, break, panic, etc.
✅ **Full flexibility** - Any code in the closure
✅ **Variable capture** - Access to scope variables
✅ **Zero runtime overhead** - Just inline code
✅ **Perfect for NDK** - Libraries, files, locks, etc.

---

## Common Patterns

**Pattern 1: Open/Close**
```novus
let file = Open(path, MODE_OLDFILE)
defer { Close(file) }
```

**Pattern 2: Lock/Unlock**
```novus
ObtainSemaphore(sem)
defer { ReleaseSemaphore(sem) }
```

**Pattern 3: BeginIO/AbortIO**
```novus
SendIO(io_request)
defer { AbortIO(io_request) }
```

**Pattern 4: OpenLibrary/CloseLibrary**
```novus
let lib = OpenLibrary("foo.library", 0)
defer { CloseLibrary(lib) }
```

**Pattern 5: AllocMem/FreeMem (when Box isn't suitable)**
```novus
let mem = AllocMem(size, flags)
defer { FreeMem(mem, size) }
```

**Pattern 6: Counter/Logging**
```novus
let mut operations = 0
defer { println("Total operations: {}", operations) }

// operations incremented throughout function
```

---

## Full Example: Complex Amiga Program

```novus
use ffi::exec::*
use ffi::dos::*
use ffi::intuition::*

fn run_application() -> Result<(), Error> {
    // Open libraries (defer for cleanup)
    let exec_base = OpenLibrary("exec.library", 0)
    if exec_base == null { return Err(Error.NoExec) }
    defer { CloseLibrary(exec_base) }

    let dos_base = OpenLibrary("dos.library", 0)
    if dos_base == null { return Err(Error.NoDos) }
    defer { CloseLibrary(dos_base) }

    let intuition_base = OpenLibrary("intuition.library", 0)
    if intuition_base == null { return Err(Error.NoIntuition) }
    defer { CloseLibrary(intuition_base) }

    // Allocate buffers (Box for automatic cleanup)
    let render_buffer = Box.alloc(64000, MEMF_CHIP | MEMF_CLEAR)?
    let work_buffer = Box.alloc(32000, MEMF_PUBLIC)?

    // Open window (defer for cleanup)
    let window = OpenWindow(&new_window_spec())
    if window == null { return Err(Error.NoWindow) }
    defer { CloseWindow(window) }

    // Create shared config (Rc for sharing)
    let config = Rc.new(AppConfig::load()?);

    // Track stats
    let mut frame_count = 0
    defer {
        println("Application ran for {} frames", frame_count)
    }

    // Run application loop
    run_event_loop(window, render_buffer, config, &mut frame_count)?

    return Ok(())
}
// Automatic cleanup order:
// 1. println("Application ran for {} frames", frame_count)
// 2. CloseWindow(window)
// 3. work_buffer.drop() (Box)
// 4. render_buffer.drop() (Box)
// 5. CloseLibrary(intuition_base)
// 6. CloseLibrary(dos_base)
// 7. CloseLibrary(exec_base)
// 8. config.drop() (Rc)
```

---

## Comparison with Other Languages

| Language | Feature | Syntax | Captures |
|----------|---------|--------|----------|
| Swift | `defer` | `defer { code }` | Yes |
| Go | `defer` | `defer function()` | Args evaluated immediately |
| Zig | `defer` | `defer statement` | No (not a closure) |
| Odin | `defer` | `defer statement` | Yes |
| Rust | No defer | Uses RAII/Drop | N/A |
| **Novus** | `defer` | `defer { code }` | **Yes** |

**Novus approach:** Closures with capture (like Swift/Odin), giving maximum flexibility.

---

## Implementation Checklist

### Phase 1: Grammar and Parsing
- [ ] Add `deferStatement` to grammar
- [ ] Parse `defer <expression>`
- [ ] Parse `defer <block>`

### Phase 2: Semantic Analysis
- [ ] Track deferred closures per scope
- [ ] Validate defer statements
- [ ] Prevent defer in defer
- [ ] Analyze captured variables

### Phase 3: IR Generation
- [ ] Store deferred closures in scope
- [ ] Insert defers at scope exit (inline expansion)
- [ ] Insert defers before returns
- [ ] Insert defers before breaks/continues

### Phase 4: Code Generation
- [ ] Generate deferred closures as inline code
- [ ] Ensure correct LIFO order
- [ ] Handle captured variables
- [ ] Test with nested scopes
- [ ] Test with early returns

### Phase 5: Testing
- [ ] Simple defer
- [ ] Multiple defers (LIFO order)
- [ ] Defer with captures
- [ ] Defer in loops
- [ ] Defer with early returns
- [ ] Defer with Box/Rc
- [ ] Defer with panics

---

## Summary

`defer` with closures is the perfect complement to Box/Rc:

- **Box/Rc** for automatic memory management
- **defer** for automatic resource management (files, libraries, locks, etc.)
- **Closures** allow capturing variables for complex cleanup logic
- **Raw pointers** when you need full manual control

All three work together seamlessly with the NDK FFI!

Ready to implement?
