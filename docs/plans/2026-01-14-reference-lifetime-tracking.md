# Reference Lifetime Tracking Design

**Date:** 2026-01-14
**Status:** Approved
**Goal:** Prevent use-after-free bugs by tracking reference lifetimes at compile time

## Problem

Currently, methods like `ScreenHandle::rastport()` return raw pointers (`*RastPort`). The compiler doesn't track that this pointer is only valid while the `ScreenHandle` is alive:

```novus
let rp: *RastPort
{
    let screen = ScreenHandle::lores("Demo", 5)?
    rp = screen.rastport()
} // screen dropped here!
SetAPen(rp, 2) // USE AFTER FREE - not caught!
```

## Solution

Implement scope-based lifetime tracking for references (`&T` and `&var T`). The compiler builds a borrow graph and validates that references don't outlive their sources.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Lifetime inference | Implicit (Rust-style elision) | Less syntax burden, common case just works |
| Multiple ref params | `&self` wins, else single param, else error | Matches Rust, predictable rules |
| Error messages | Detailed with spans and hints | Errors should teach, not just reject |
| Borrow tracking | Graph-based | Handles transitive borrows, better errors |
| Reference → pointer | Requires `unsafe` block | Escaping safety should be deliberate |
| References in structs | Disallowed for v1 | Avoids lifetime parameter complexity |

## Implementation

### 1. Core Data Structures

Add to `BorrowChecker.cs`:

```csharp
public class BorrowEdge
{
    public int SourceVariableId { get; init; }
    public int BorrowingVariableId { get; init; }
    public SourceLocation BorrowLocation { get; init; }
    public bool IsMutable { get; init; }
}

public class VariableScope
{
    public int VariableId { get; init; }
    public int ScopeDepth { get; init; }
    public SourceLocation DeclLocation { get; init; }
    public SourceLocation? DropLocation { get; set; }
}

public class BorrowGraph
{
    private readonly Dictionary<int, List<BorrowEdge>> _borrowsFrom = new();
    private readonly Dictionary<int, VariableScope> _scopes = new();

    public void AddBorrow(int borrower, int source, SourceLocation loc, bool mutable);
    public List<int> GetBorrowChain(int variableId);
    public bool OutlivesSource(int borrowerId, int sourceId);
    public void RemoveVariable(int variableId);  // For unsafe escape
}
```

### 2. Lifetime Inference Rules

In `SemanticAnalyzer.cs`, when analyzing method returns:

1. If `&self` present → return lifetime ties to self
2. If no `&self` but exactly one `&param` → return ties to that param
3. If multiple `&params` and no `&self` → compiler error

```csharp
public int? InferReturnLifetime(IrFunction method)
{
    var returnType = method.ReturnType;
    if (returnType is not IrReferenceType)
        return null;

    var refParams = method.Parameters
        .Where(p => p.Type is IrReferenceType or IrMutReferenceType)
        .ToList();

    // Rule 1: &self always wins
    var selfParam = refParams.FirstOrDefault(p => p.Name == "self");
    if (selfParam != null)
        return selfParam.Id;

    // Rule 2: Exactly one reference param
    if (refParams.Count == 1)
        return refParams[0].Id;

    // Rule 3: Ambiguous
    if (refParams.Count > 1)
    {
        EmitError("E0106", "cannot infer lifetime: multiple reference parameters", ...);
        return null;
    }

    // Rule 4: No reference params
    EmitError("E0106", "method returns reference but has no reference parameters", ...);
    return null;
}
```

### 3. Violation Detection

**At scope exit:**
```csharp
public void ValidateScopeExit(int scopeDepth, SourceLocation dropLocation)
{
    var droppedVars = _scopes.Values
        .Where(s => s.ScopeDepth == scopeDepth)
        .ToList();

    foreach (var dropped in droppedVars)
    {
        dropped.DropLocation = dropLocation;

        var danglingBorrows = _borrowsFrom
            .Where(kvp => kvp.Value.Any(e => e.SourceVariableId == dropped.VariableId))
            .Where(kvp => _scopes[kvp.Key].ScopeDepth < scopeDepth)
            .ToList();

        foreach (var (borrowerId, edges) in danglingBorrows)
        {
            EmitLifetimeError(dropped, _scopes[borrowerId], edges.First());
        }
    }
}
```

**At assignment:**
```csharp
public void ValidateAssignment(int targetVarId, int sourceVarId, SourceLocation loc)
{
    var targetScope = _scopes[targetVarId];
    var sourceScope = _scopes[sourceVarId];

    if (targetScope.ScopeDepth < sourceScope.ScopeDepth)
    {
        var chain = _borrowGraph.GetBorrowChain(sourceVarId);
        foreach (var sourceInChain in chain)
        {
            if (_scopes[sourceInChain].ScopeDepth > targetScope.ScopeDepth)
            {
                EmitEscapingReferenceError(targetVarId, sourceInChain, loc);
            }
        }
    }
}
```

### 4. Error Messages

Detailed, multi-span errors with actionable hints:

```
error[E0597]: `screen` does not live long enough
  --> example.novus:5:5
   |
 3 |     let rp: &RastPort
   |         -- borrow later used here
 4 |     {
 5 |         let screen = ScreenHandle::lores("Demo", 5)?
   |             ^^^^^^ borrowed value
 6 |         rp = screen.rastport()
   |              ----------------- borrow occurs here
 7 |     }
   |     ^ `screen` dropped here while still borrowed
   |
   = consider one of these fixes:
     - move `rp` into the same scope as `screen`
     - use a raw pointer in an `unsafe` block if you can guarantee validity
     - restructure to avoid storing the reference
```

### 5. Unsafe Escape Hatch

Converting `&T` to `*T` requires `unsafe`:

```novus
let rp: &RastPort = screen.rastport()

// ERROR
let raw: *RastPort = (*RastPort)rp

// OK
let raw: *RastPort = unsafe { (*RastPort)rp }
```

### 6. Struct Field Restriction

References cannot be stored in struct fields (v1 limitation):

```
error[E0106]: struct `RenderContext` cannot contain reference field `rp`
  --> example.novus:2:5
   |
 2 |     rp: &RastPort
   |     ^^^^^^^^^^^^^ reference type not allowed in struct field
   |
   = help: references have lifetimes that cannot be expressed in struct fields yet
   = consider these alternatives:
     - use a raw pointer: `rp: *RastPort`
     - use an owned type instead of a reference
     - pass the reference as a function parameter instead of storing it
```

## Files to Modify

1. `Novus.Core/SemanticAnalysis/BorrowChecker.cs` - Add BorrowGraph, lifetime tracking
2. `Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs` - Integrate lifetime inference and validation
3. `Novus.Core/Frontend/IrBuilder.Expressions.cs` - Handle unsafe ref→ptr conversion
4. `Novus.Core/Diagnostics/` - Add new error codes E0597, E0106, E0133
5. `Novus/std/ui/screen.novus` - Change `rastport()` to return `&RastPort`

## Migration

1. Change stdlib methods from `*T` returns to `&T` returns
2. Existing code using raw pointers continues to work
3. New code using references gets lifetime safety

## Future Work

- Lifetime parameters on structs (`struct Foo<'a>`)
- Mutable borrow exclusivity (one `&var` OR many `&`)
- Named lifetimes for complex cases
