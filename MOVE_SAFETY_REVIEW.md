# Move Safety Implementation Review

## Executive Summary

The Phase 1 move safety implementation has a **critical bug** that allows use-after-move for regular function calls. The implementation successfully detects moves in method calls with consuming parameters, but completely misses moves in regular function calls. This makes the current implementation **unsafe and not ready for production**.

**Recommendation**: Do not ship until the missing move tracking for regular function calls is implemented and all edge cases are addressed.

---

## What's Currently Implemented

### 1. Grammar Support (GOOD)
**File**: `/Users/barry/RiderProjects/Novus/Novus.Core/Novus.g4`

```antlr
parameter: KW_CONSUMING? IDENTIFIER ':' type ;
selfParameter: ... | KW_CONSUMING? KW_SELF ;
```

- Added `consuming` keyword (line 77, 86, 396)
- Grammar correctly allows `consuming` on both regular parameters and self parameters
- **Status**: ✅ Complete and correct

### 2. Semantic Analysis Data Structures (GOOD)
**File**: `/Users/barry/RiderProjects/Novus/Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`

```csharp
// Line 56-63
private readonly Dictionary<string, MoveInfo> _movedVariables = new();

private class MoveInfo {
    public string VariableName { get; init; } = "";
    public SourceLocation MoveLocation { get; init; } = null!;
    public string Reason { get; init; } = "";
}
```

- Simple, efficient dictionary tracking
- Good error message support with location and reason
- **Status**: ✅ Adequate for Phase 1

### 3. Move Tracking for Method Calls (GOOD)
**File**: `/Users/barry/RiderProjects/Novus/Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`
**Location**: `HandleMethodCall` method (lines 5118-5131 for self, 5147-5160 for arguments)

```csharp
// Track consuming self parameter (line 5118-5131)
if (hasSelfParam && method.Parameters[0].IsConsuming) {
    var receiverVarName = ExtractVariableName(receiverExpr);
    if (receiverVarName != null) {
        _movedVariables[receiverVarName] = new MoveInfo { ... };
    }
}

// Track consuming regular parameters (line 5147-5160)
if (param.IsConsuming) {
    var argVarName = ExtractVariableName(arguments[i]);
    if (argVarName != null) {
        _movedVariables[argVarName] = new MoveInfo { ... };
    }
}
```

**Test Results**:
```bash
# This CORRECTLY errors:
let obj = MyStruct::new(42)
let val = obj.consume()  // consuming self
let v2 = obj.value       // ERROR: use of moved value
```

- **Status**: ✅ Works correctly for method calls

### 4. Use-After-Move Detection (GOOD)
**File**: `/Users/barry/RiderProjects/Novus/Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`
**Location**: `VisitIdentifierExpr` (lines 5489-5505)

```csharp
if (!name.Contains("::") && _movedVariables.ContainsKey(name)) {
    var moveInfo = _movedVariables[name];
    _diagnostics.ReportError("E0382", $"use of moved value: `{name}`", ...);
}
```

- Checked on every variable reference
- Good error message with move location and reason
- Correctly skips qualified names like `Result::Ok`
- **Status**: ✅ Works correctly

### 5. Scope Management (GOOD)
**File**: `/Users/barry/RiderProjects/Novus/Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`
**Location**: Function entry points (lines 1544, 1566)

```csharp
_movedVariables.Clear(); // Reset move tracking for new function
```

- Move tracking is per-function (cleared at function boundaries)
- **Status**: ✅ Correct for simple cases

---

## Critical Bug: Missing Move Tracking for Regular Function Calls

### The Problem

**Location**: `VisitCallExpr` method (around line 4847)

```csharp
// This is where regular function call arguments are visited
for (int i = 0; i < arguments.Length; i++) {
    var argType = Visit(arguments[i]);

    // BUG: No move tracking here!
    // Missing: check if function.Parameters[i].IsConsuming

    var paramType = function.Parameters[i].Type;
    if (argType != null && !TypesCompatible(paramType, argType)) {
        // error reporting...
    }
}
```

### Proof of Bug

**Test case**: `/Users/barry/RiderProjects/Novus/test_param_move.novus`

```novus
fn consume_struct(consuming s: MyStruct) { }

fn main() -> i32 {
    let obj = MyStruct { value: 42 }
    consume_struct(obj)  // Move happens here
    let v = obj.value    // BUG: NO ERROR - should fail!
    return v
}
```

**Result**: Compiles successfully with NO ERROR. The moved value is used unsafely.

### Why This Happens

The semantic analyzer has TWO code paths for function calls:

1. **Method calls** (`v.method()`) → `HandleMethodCall` → ✅ Has move tracking
2. **Regular calls** (`function(v)`) → `VisitCallExpr` → ❌ NO move tracking

The move tracking code was only added to path #1, not path #2.

---

## Missing Features

### 1. Conditional Moves (CRITICAL)

```novus
fn test_conditional(flag: bool) {
    let s = String::new()

    if flag {
        consume_string(s)  // s moved in this branch
    }

    // BUG: Should error - s is potentially moved
    let len = s.len()
}
```

**Problem**: Move tracking needs to be **flow-sensitive**. A variable should be marked as "potentially moved" if it's moved in any control flow path.

**What's needed**:
- Track moves per control-flow branch
- Merge move states at join points (end of if/match)
- Error if variable is used when it's "definitely moved" or "potentially moved"

### 2. Loop Moves (CRITICAL)

```novus
fn test_loop() {
    let s = String::new()

    while condition() {
        consume_string(s)  // BUG: s moved on FIRST iteration
        // Second iteration uses moved value!
    }
}
```

**Problem**: Loops require special handling. Any move inside a loop body that could execute multiple times must error.

**What's needed**:
- Detect moves inside loops
- Error on ANY move of a loop-external variable inside the loop (unless reassigned)
- Handle break/continue correctly

### 3. Assignment Moves (MISSING)

```novus
fn test_assignment() {
    let s1 = String::new()
    let s2 = s1  // This should MOVE s1 to s2
    let len = s1.len()  // Should error: s1 was moved
}
```

**Problem**: The Novus language spec implies move semantics, but moves are only tracked through `consuming` parameters, not through assignment.

**What's needed**:
- Define when assignments move vs copy
- Track moves through `let` and `var` assignments
- Consider: add explicit `Copy` trait to distinguish copy vs move types

### 4. Return Moves (MISSING)

```novus
fn return_value() -> String {
    let s = String::new()
    return s  // This should move s
    // Unreachable code after return, but if it weren't:
    // let len = s.len()  // Should error if reachable
}
```

**Problem**: Returning a value should move it.

**What's needed**:
- Track moves on `return` statements
- Integration with unreachable code analysis (already exists)

### 5. Partial Moves (COMPLEX)

```novus
struct Container {
    field1: String,
    field2: String
}

fn test_partial() {
    let c = Container { field1: ..., field2: ... }
    consume_string(c.field1)  // Move just field1
    let s = c.field2          // Should work - field2 not moved
    let s2 = c.field1         // Should error - field1 was moved
    let c2 = c                // Should error - c partially moved
}
```

**Problem**: Need to track which struct fields have been moved separately.

**What's needed**:
- Track moves per field path (`c.field1`, `c.field2`)
- Mark entire struct as "partially moved" after any field move
- Error on whole-struct use after partial move
- Complex but important for zero-cost abstractions

### 6. Pattern Matching Moves (MISSING)

```novus
match option {
    Some(value) => {
        consume(value)  // value is moved out of option
    },
    None => {}
}
// option is now moved/consumed
```

**Problem**: Pattern destructuring moves values out of enums/structs.

**What's needed**:
- Track moves in pattern bindings
- Mark matched variable as moved after match
- Handle references in patterns differently

---

## Edge Cases & Potential Issues

### 1. ExtractVariableName Limitations

**Current Implementation**:
```csharp
private string? ExtractVariableName(ParserRuleContext expr) {
    if (expr is NovusParser.IdentifierExprContext identExpr) {
        // Simple identifier like "x"
        if (identifierCtx.IDENTIFIER().Length == 1) {
            return identifierCtx.IDENTIFIER(0).GetText();
        }
    }
    return null;  // Complex expressions return null
}
```

**Limitation**: Only tracks simple variable names, not field accesses or complex expressions.

**Test case that SILENTLY FAILS to track**:
```novus
struct Wrapper { inner: String }

fn test() {
    let w = Wrapper { inner: String::new() }
    consume_string(w.inner)  // ExtractVariableName returns null!
    // BUG: No move tracked, no error on next line
    let len = w.inner.len()
}
```

**Impact**: Partial moves are completely untracked. This is a **correctness issue**.

### 2. Dictionary Lookup Performance

**Current**: `_movedVariables` is checked on EVERY variable reference.

```csharp
if (!name.Contains("::") && _movedVariables.ContainsKey(name)) { ... }
```

**Performance**: Dictionary lookups are O(1) amortized, so this is probably fine for Phase 1. The function-local scope keeps the dictionary small.

**Concern**: For very large functions with thousands of variable references, this could add up. Unlikely to be a real issue.

**Recommendation**: Keep as-is for Phase 1, profile if needed later.

### 3. Error Message Quality

**Current message**:
```
error[E0382]: use of moved value: `obj`
  note: value moved here
  help: value moved into consuming method 'consume'
```

**Good**:
- Clear error code
- Shows both locations (move and use)
- Explains why (consuming parameter)

**Missing**:
- No suggestion on how to fix (e.g., "consider cloning" or "use a reference")
- No explanation of what "consuming" means for new users
- Could mention if type implements Copy/Clone

**Recommendation**: Add helpful suggestions:
```
help: value moved into consuming method 'consume'
help: if you want to use 'obj' after this call, consider:
      - passing a reference: &obj
      - cloning first: obj.clone()  [if Clone is implemented]
```

### 4. Integration with IR Builder

**Current**: Move tracking is in semantic analysis only. The IR builder and code generator don't know about moves.

**Question**: Does the C code generator already null out moved values?

Let me check...

**Finding**: The C code generator has this comment in the requirements:
> "The compiler handles the complete pipeline: compile → assemble → link"

But I don't see explicit null-out logic for consumed parameters. The generated C code might not defend against use-after-move at runtime.

**Recommendation**: Either:
1. Add runtime defense (null out moved pointers in debug builds), OR
2. Document that move safety is compile-time only

### 5. Copy vs Move Semantics

**Current behavior**: ALL types have move semantics when passed to `consuming` parameters.

**Problem**: Some types should be implicitly copyable (i32, pointers, etc.).

```novus
fn consume_int(consuming x: i32) { }

fn test() {
    let x = 42
    consume_int(x)
    let y = x + 1  // Should this error?
}
```

**Design question**: Should `consuming` apply to copyable types?

**Options**:
1. **Allow implicit copy**: i32, pointers, etc. are always copied, `consuming` is ignored
2. **No implicit copy**: `consuming` moves everything; add `Copy` trait to opt-in
3. **Error on consuming Copy types**: Make it a compile error

**Rust's approach**: Uses the `Copy` trait. Copy types can be passed by-value without moving.

**Recommendation**: Define a `Copy` trait for Phase 2, but for Phase 1, allow primitives to be used after "move" (they're copied anyway).

---

## Architecture Concerns

### 1. Global vs Scoped Tracking

**Current**: `_movedVariables` is a single dictionary cleared at function boundaries.

**Problem**: What about nested scopes (blocks, if/match arms)?

```novus
fn test() {
    let s = String::new()
    {
        let s = String::new()  // Shadows outer s
        consume_string(s)       // Inner s moved
    }
    // Outer s should still be usable
    let len = s.len()  // Should work!
}
```

**Question**: Does the current implementation handle shadowing correctly?

**Test needed**: Create a test with shadowed variables.

**Likely behavior**: Dictionary uses simple string keys, so shadowing will INCORRECTLY mark the outer variable as moved.

**Fix needed**: Key by variable ID or scope+name, not just name.

### 2. Function Boundaries

**Current**: `_movedVariables.Clear()` at function entry.

**Good**: Keeps tracking simple, per-function only.

**Problem**: What about closures/lambdas in the future?

**Recommendation**: Sufficient for Phase 1. Closures will need capture analysis anyway.

### 3. Order of Checks

**Current**: Use-after-move check happens in `VisitIdentifierExpr`, BEFORE type checking.

**Good**: Errors are caught early.

**Problem**: Might miss some semantic context (e.g., is this a mutation or read?).

**Recommendation**: Current order is fine. Move semantics apply regardless of read/write.

---

## Testing Strategy

### Current Test Coverage

**Existing tests**:
- ✅ Method call with `consuming self`
- ✅ Method call with `consuming` parameter
- ❌ Regular function call with `consuming` parameter (FAILS - bug exists)
- ❌ Conditional moves
- ❌ Loop moves
- ❌ Assignment moves
- ❌ Return moves
- ❌ Partial moves
- ❌ Shadowed variables
- ❌ Complex expressions

### Recommended Test Suite

```novus
// Test 1: Regular function consuming (CURRENTLY BROKEN)
fn consume(consuming s: String) {}
fn test1() {
    let s = String::new()
    consume(s)
    s.len()  // ERROR
}

// Test 2: Double move
fn test2() {
    let s = String::new()
    consume(s)
    consume(s)  // ERROR
}

// Test 3: Conditional move (definitely moved)
fn test3(flag: bool) {
    let s = String::new()
    if flag {
        consume(s)
    } else {
        consume(s)
    }
    // Both branches move s
    s.len()  // ERROR
}

// Test 4: Conditional move (maybe moved)
fn test4(flag: bool) {
    let s = String::new()
    if flag {
        consume(s)
    }
    // s is maybe moved
    s.len()  // ERROR (conservative)
}

// Test 5: Loop move
fn test5() {
    let s = String::new()
    while condition() {
        consume(s)  // ERROR: can't move in loop
    }
}

// Test 6: Shadowing
fn test6() {
    let s = String::new()
    {
        let s = String::new()
        consume(s)
    }
    s.len()  // OK: inner s was moved, outer s is fine
}

// Test 7: Partial move
fn test7() {
    let c = Container { f1: String::new(), f2: String::new() }
    consume(c.f1)
    c.f2.len()  // OK: f2 not moved
    c.f1.len()  // ERROR: f1 moved
    let c2 = c  // ERROR: c partially moved
}

// Test 8: Return move
fn test8() -> String {
    let s = String::new()
    return s  // s moved
}

// Test 9: Pattern move
fn test9(opt: Option<String>) {
    match opt {
        Some(s) => consume(s),
        None => {}
    }
    // opt is moved
}

// Test 10: Move OK - last use
fn test10() {
    let s = String::new()
    consume(s)  // OK: last use of s
}
```

---

## Performance Analysis

### Compilation Performance

**Dictionary lookups**: O(1) per variable reference
**Memory overhead**: O(moved variables) per function
**Expected impact**: Negligible for typical functions

**Worst case**:
- Function with 1000 local variables
- Each used 100 times
- 100,000 dictionary lookups
- Still <1ms on modern hardware

**Conclusion**: Performance is not a concern for Phase 1.

### Runtime Performance

**Current**: Move tracking is compile-time only, zero runtime cost.

**Future**: If we add runtime checks (null out moved values), this will add cost.

**Recommendation**: Keep as compile-time only for release builds.

---

## Recommendations

### Must-Fix Before Shipping

1. **Fix regular function call move tracking** (CRITICAL)
   - Add the same move tracking logic from `HandleMethodCall` to `VisitCallExpr`
   - Test thoroughly with all parameter combinations

2. **Add comprehensive test suite** (CRITICAL)
   - All test cases listed in "Recommended Test Suite" section
   - Automated tests in CI/CD pipeline

3. **Fix shadowing bug** (IMPORTANT)
   - Use unique variable IDs or scope-qualified names as dictionary keys
   - Test nested scopes

### Should-Fix Before Shipping

4. **Improve error messages** (IMPORTANT)
   - Add "consider cloning" or "use a reference" suggestions
   - Explain what `consuming` means

5. **Define Copy semantics** (IMPORTANT)
   - Decide: are primitives Copy by default?
   - Document the behavior clearly

6. **Add conditional move detection** (IMPORTANT)
   - At minimum, error on "maybe moved" conservatively
   - Proper control flow analysis for Phase 2

### Can-Defer to Phase 2

7. **Loop move detection**
   - Complex but important
   - Can be conservative (error on any move in loop)

8. **Partial moves**
   - Complex implementation
   - Nice-to-have but not critical

9. **Pattern matching moves**
   - Depends on how pattern matching is implemented
   - Will need special handling

10. **Runtime move checking**
    - Debug-mode null checks
    - Helpful but not essential

---

## Alternative Approaches

### 1. Borrow Checker (Rust-style)

**Pros**:
- Complete safety (no use-after-free, no data races)
- Enables zero-cost abstractions
- Well-proven model

**Cons**:
- Very complex to implement (months of work)
- Steep learning curve for users
- May be overkill for Amiga development

**Recommendation**: Phase 3 or later

### 2. Reference Counting (Swift/Python-style)

**Pros**:
- Simple to use
- No borrow checker complexity

**Cons**:
- Runtime overhead (atomic refcount ops)
- Doesn't prevent cycles
- Not zero-cost

**Recommendation**: Not suitable for Amiga/68k target

### 3. Ownership + Manual Unsafe (Zig-style)

**Pros**:
- Middle ground: safe by default, escape hatches for experts
- Less complex than full borrow checker

**Cons**:
- Still requires good ownership tracking
- Can leak safety holes if unsafe is overused

**Recommendation**: Current approach is already similar to this

---

## Security Considerations

### Memory Safety Bugs Prevented

- ✅ Use-after-move (when implemented correctly)
- ❌ Use-after-free (not prevented - move tracking only)
- ❌ Double-free (not prevented)
- ❌ Null pointer dereference (not prevented)

### Memory Safety Bugs NOT Prevented

**Current implementation is NOT a complete memory safety solution**. It only prevents using moved values, not:

- Using freed memory
- Buffer overflows
- Type confusion
- Data races (if/when multithreading is added)

**Recommendation**: Document clearly that `consuming` is **not** a full memory safety solution, just a move tracker.

---

## Conclusion

### What's Good

✅ Grammar support is complete
✅ Basic move tracking works for method calls
✅ Error messages are clear and helpful
✅ Architecture is sound for Phase 1
✅ Performance impact is negligible

### What's Broken

❌ **CRITICAL**: Regular function calls don't track moves at all
❌ Shadowed variables will be incorrectly marked as moved
❌ No control flow sensitivity (if/match/while)
❌ No assignment moves
❌ No return moves
❌ No partial moves

### Ship or Don't Ship?

**DO NOT SHIP** in current state. The missing move tracking for regular function calls is a **critical correctness bug** that undermines the entire feature.

### Minimum Viable Fix

To make this shippable:

1. Add move tracking to `VisitCallExpr` (2-4 hours)
2. Fix shadowing bug (2-4 hours)
3. Add test suite (4-8 hours)
4. Document limitations (1-2 hours)

**Total**: 1-2 days of work to make this production-ready for Phase 1.

### Future Phases

**Phase 2**: Control flow sensitivity, assignment moves, Copy trait
**Phase 3**: Partial moves, pattern moves
**Phase 4**: Full borrow checker (if needed)

---

## Implementation Plan for Fix

### Step 1: Fix Regular Function Calls

**File**: `/Users/barry/RiderProjects/Novus/Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`
**Location**: `VisitCallExpr`, around line 4847

```csharp
for (int i = 0; i < arguments.Length; i++) {
    var argType = Visit(arguments[i]);

    // ADD THIS: Check for consuming parameter and track move
    var param = function.Parameters[i];
    if (param.IsConsuming) {
        var argVarName = ExtractVariableName(arguments[i]);
        if (argVarName != null) {
            var moveLocation = SourceLocationHelper.FromContext(arguments[i], _filePath, _sourceLines);
            _movedVariables[argVarName] = new MoveInfo {
                VariableName = argVarName,
                MoveLocation = moveLocation,
                Reason = $"value moved into consuming parameter '{param.Name}'"
            };
        }
    }

    // Continue with existing type checking...
}
```

### Step 2: Fix Shadowing

**Current**:
```csharp
private readonly Dictionary<string, MoveInfo> _movedVariables = new();
```

**Change to**:
```csharp
// Use variable symbol reference instead of string name
private readonly Dictionary<VariableSymbol, MoveInfo> _movedVariables = new();
```

This requires refactoring but ensures shadowed variables don't collide.

### Step 3: Add Tests

Create `/Users/barry/RiderProjects/Novus/Novus.Tests/MoveTrackingTests.cs`:

```csharp
[Test]
public void RegularFunctionConsuming_DetectsUseAfterMove() {
    var code = @"
        fn consume(consuming s: String) {}
        fn main() -> i32 {
            let s = String::new()
            consume(s)
            s.len()  // Should error
            return 0
        }
    ";
    var result = Compile(code);
    Assert.That(result.Errors, Has.Some.Matches<Diagnostic>(
        d => d.ErrorCode == "E0382" && d.Message.Contains("use of moved value")));
}
```

---

**End of Review**

**Author**: Claude (Sonnet 4.5)
**Date**: 2025-11-06
**Compiler Version**: Novus PoC (commit: 8a4a1e2)
