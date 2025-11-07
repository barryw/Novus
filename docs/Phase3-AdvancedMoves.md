# Phase 3: Advanced Move Tracking

## Goal

Catch more complex move patterns: assignments, returns, and partial moves.

## Current Gaps

### Gap 1: Assignment Moves
```novus
let x = String::new()
let y = x  // Currently: NO ERROR (should mark x as moved)
x.len()    // Currently: NO ERROR (should be ERROR)
```

### Gap 2: Return Moves
```novus
fn get_string() -> String {
    let s = String::new()
    return s  // Currently: NO ERROR (should mark s as moved)
    // s.len()  // If we added this, should be ERROR
}
```

### Gap 3: Partial Moves
```novus
struct Pair { first: String, second: String }

let p = Pair { ... }
consume(p.first)   // Currently: NO ERROR (should mark p.first as moved)
let x = p.first    // Currently: NO ERROR (should be ERROR)
let y = p.second   // Currently: OK (second is still valid)
```

## Implementation Plan

### 1. Assignment Moves

Track when non-Copy values are assigned/passed by value.

**In `VisitVariableDeclaration()`:**
```csharp
// If initializer is a variable reference (not a function call/constructor)
if (ctx.initializer is IdentifierContext idCtx)
{
    var sourceVarName = idCtx.GetText();
    var sourceVar = LookupVariable(sourceVarName);

    if (sourceVar != null && !IsCopyType(sourceVar.Type))
    {
        // Non-Copy type assigned by value → move
        RecordMove(
            sourceVar.Id,
            sourceVarName,
            GetSourceLocation(idCtx),
            $"value moved by assignment to `{ctx.name.GetText()}`"
        );
    }
}
```

**Error message:**
```
error[E0382]: use of moved value: `x`
  --> test.novus:15:5
   |
13 |     let y = x
   |             - value moved here by assignment
15 |     x.len()
   |     ^ value used after move
   |
help: if you need to use `x` after assignment, clone it first:
   |
13 |     let y = x.clone()
   |             ++++++++
```

### 2. Return Moves

Track when values are returned.

**In `VisitReturnStatement()`:**
```csharp
if (ctx.expression is IdentifierContext idCtx)
{
    var varName = idCtx.GetText();
    var variable = LookupVariable(varName);

    if (variable != null && !IsCopyType(variable.Type))
    {
        RecordMove(
            variable.Id,
            varName,
            GetSourceLocation(idCtx),
            "value moved by return statement"
        );
    }
}
```

**Note**: This only matters if there's code after the return (which is usually unreachable, but we should still track it).

**Error message:**
```
error[E0382]: use of moved value: `s`
  --> test.novus:18:5
   |
16 |     return s
   |            - value moved here by return
17 |     // unreachable code warning
18 |     s.len()
   |     ^ value used after move
   |
help: code after return is unreachable
```

### 3. Partial Moves (Complex)

Track which fields of a struct have been moved.

**New tracking structure:**
```csharp
private class MoveInfo
{
    public int VariableId { get; init; }
    public string VariableName { get; init; }
    public SourceLocation MoveLocation { get; init; }
    public string Reason { get; init; }
    public HashSet<string>? MovedFields { get; init; }  // null = whole value moved
}
```

**When moving a field:**
```csharp
if (ctx.expression is MemberAccessContext memberCtx)
{
    var objectName = GetObjectName(memberCtx.obj);
    var fieldName = memberCtx.member.GetText();
    var variable = LookupVariable(objectName);

    if (variable != null)
    {
        // Check if already moved
        if (_movedVariables.TryGetValue(variable.Id, out var existing))
        {
            if (existing.MovedFields == null)
            {
                // Whole struct already moved
                EmitError($"use of moved value: `{objectName}`");
            }
            else if (existing.MovedFields.Contains(fieldName))
            {
                // This field already moved
                EmitError($"use of moved field: `{objectName}.{fieldName}`");
            }
            else
            {
                // Mark this field as moved
                existing.MovedFields.Add(fieldName);
            }
        }
        else
        {
            // First field move
            _movedVariables[variable.Id] = new MoveInfo
            {
                VariableId = variable.Id,
                VariableName = objectName,
                MoveLocation = GetSourceLocation(memberCtx),
                Reason = $"field `{fieldName}` moved",
                MovedFields = new HashSet<string> { fieldName }
            };
        }
    }
}
```

**When using a field:**
```csharp
if (_movedVariables.TryGetValue(variable.Id, out var moveInfo))
{
    if (moveInfo.MovedFields == null)
    {
        // Whole struct moved
        EmitError($"use of moved value: `{varName}`");
    }
    else if (moveInfo.MovedFields.Contains(fieldName))
    {
        // This specific field moved
        EmitError($"use of moved field: `{varName}.{fieldName}`");
    }
    // else: field is still valid
}
```

**Error message:**
```
error[E0382]: use of moved field: `pair.first`
  --> test.novus:18:13
   |
15 |     consume(pair.first)
   |             ---------- field moved here
18 |     let x = pair.first
   |             ^^^^^^^^^^ field used after move
   |
help: field `pair.second` is still valid and can be used
```

### 4. Copy Types

Some types should be automatically copyable (primitives, simple structs).

**Add `Copy` trait:**
```novus
// In std::core
pub trait Copy {}

// Implement for primitives
impl Copy for i32 {}
impl Copy for u32 {}
impl Copy for bool {}
// etc.
```

**Check in semantic analyzer:**
```csharp
private bool IsCopyType(IrType type)
{
    // Primitives are always Copy
    if (type is IrPrimitiveType)
        return true;

    // Check if struct implements Copy trait
    if (type is IrStructType structType)
    {
        return structType.Implements("Copy");
    }

    return false;
}
```

**Usage:**
```novus
let x: i32 = 42
let y = x  // OK: i32 is Copy, so x is copied not moved
x + 1      // OK: x is still valid
```

## Test Cases

### Test 1: Assignment Move
```novus
fn test_assignment_move() {
    let x = String::new()
    let y = x        // x is moved
    x.len()          // ERROR: use after move
}
```

### Test 2: Return Move
```novus
fn test_return_move() -> String {
    let s = String::new()
    return s         // s is moved
    // s.len()       // ERROR: use after move (unreachable)
}
```

### Test 3: Partial Move
```novus
struct Pair {
    first: String,
    second: String
}

fn test_partial_move() {
    let p = Pair { ... }
    consume(p.first)   // p.first is moved
    p.first.len()      // ERROR: use of moved field
    p.second.len()     // OK: second is still valid
}
```

### Test 4: Copy Type
```novus
fn test_copy_type() {
    let x: i32 = 42
    let y = x          // Copy, not move
    x + y              // OK: both valid
}
```

## Implementation Priority

**High Priority (Phase 3a):**
1. Assignment moves (4-6 hours)
2. Return moves (2-3 hours)
3. Copy types for primitives (2-3 hours)

**Medium Priority (Phase 3b):**
4. Partial moves for struct fields (8-12 hours - complex!)

## Error Message Templates

### Assignment Move
```
error[E0382]: use of moved value: `{varName}`
  --> {location}
   |
{moveLocation} |     let {newVar} = {varName}
   |                    {underline} value moved here
{useLocation}  |     {varName}.method()
   |             {underline} value used here after move
   |
help: if you need to use `{varName}` after assignment, clone it:
   |
{moveLocation} |     let {newVar} = {varName}.clone()
   |                    {spaces}       ++++++++
```

### Return Move
```
error[E0382]: use of moved value: `{varName}`
  --> {location}
   |
{moveLocation} |     return {varName}
   |                    {underline} value moved here
{useLocation}  |     {varName}.method()
   |             {underline} value used here after move
   |
note: code after return is unreachable
```

### Partial Move
```
error[E0382]: use of moved field: `{structName}.{fieldName}`
  --> {location}
   |
{moveLocation} |     consume({structName}.{fieldName})
   |                    {underline} field moved here
{useLocation}  |     {structName}.{fieldName}.method()
   |             {underline} field used here after move
   |
note: other fields of `{structName}` are still valid:
   |     {listOfValidFields}
```

## Success Criteria

After Phase 3a:
- ✅ Assignment moves tracked
- ✅ Return moves tracked
- ✅ Copy types for primitives
- ✅ ~92% of move bugs caught (vs 85% in Phase 2)

After Phase 3b:
- ✅ Partial moves for struct fields
- ✅ ~95% of move bugs caught
- ✅ Near Rust-level safety

## Next: Phase 4 (Future)

- Borrow checker (`&` vs `&mut` exclusivity)
- Lifetime annotations
- Advanced patterns (destructuring, etc.)
- Full Rust compatibility

## Time Estimate

- **Phase 3a**: 1-2 days (assignment + return + Copy)
- **Phase 3b**: 2-3 days (partial moves - complex!)
- **Total**: 3-5 days for complete Phase 3
