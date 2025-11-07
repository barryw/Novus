# Drop Trait - Phase 2 Implementation Plan

## Status: Phase 1 Complete ✅

**Completed:**
- ✅ Added `Drop` trait to `std::core`
- ✅ Added `ImplementsDrop` property to `IrStructType`
- ✅ Added `TypeImplementsDrop()` helper methods to `IrModule`
- ✅ Modified `AddTraitImpl()` to set `ImplementsDrop = true` automatically
- ✅ Compiler builds successfully

## Phase 2: Semantic Analysis - Drop Call Insertion

### Goal
Automatically insert drop calls when variables go out of scope, respecting move semantics.

### Key Data Structures

**Current (in SemanticAnalyzer.cs:8346)**:
```csharp
public record VariableSymbol(
    string Name,
    IrType Type,
    bool IsMutable,
    SourceLocation Location,
    AttributeCollection? Attributes = null,
    int Id = 0  // Unique ID for shadowed variables
);
```

**Need to Add:**
```csharp
// Track drop state per variable
private class DropInfo
{
    public int VariableId { get; init; }
    public string VariableName { get; init; }
    public IrType VariableType { get; init; }
    public SourceLocation DeclLocation { get; init; }
    public bool WasMoved { get; set; }  // Don't drop if moved
    public HashSet<string>? MovedFields { get; set; }  // For partial moves
}

// Track variables that need dropping per scope
private class ScopeDropInfo
{
    public List<DropInfo> VariablesToDrop { get; } = new();
    public SourceLocation ScopeEnd { get; set; }
}

private Stack<ScopeDropInfo> _dropScopes = new();
private Dictionary<int, DropInfo> _dropInfo = new();  // VariableId -> DropInfo
```

### Implementation Steps

#### Step 1: Track Variables That Need Dropping

**When variable is declared** (in `VisitVariableDeclaration()`):
```csharp
// After adding variable to symbol table
if (module.TypeImplementsDrop(variable.Type) && !IsCopyType(variable.Type))
{
    var dropInfo = new DropInfo
    {
        VariableId = variable.Id,
        VariableName = variable.Name,
        VariableType = variable.Type,
        DeclLocation = variable.Location,
        WasMoved = false,
        MovedFields = null
    };

    _dropInfo[variable.Id] = dropInfo;
    _dropScopes.Peek().VariablesToDrop.Add(dropInfo);
}
```

#### Step 2: Mark Variables as Moved

**When variable is moved** (already exists in `RecordMove()`):
```csharp
// In RecordMove() - add this:
if (_dropInfo.TryGetValue(variableId, out var dropInfo))
{
    if (fieldName == null)
    {
        // Whole value moved
        dropInfo.WasMoved = true;
    }
    else
    {
        // Partial move
        dropInfo.MovedFields ??= new HashSet<string>();
        dropInfo.MovedFields.Add(fieldName);
    }
}
```

#### Step 3: Insert Drop Calls at Scope Exit

**At end of block** (in `VisitBlock()`):
```csharp
// Before exiting scope
EmitDropCallsForScope(_dropScopes.Peek());
_dropScopes.Pop();
```

**Helper method**:
```csharp
private void EmitDropCallsForScope(ScopeDropInfo scopeInfo)
{
    // Drop in reverse order of declaration
    foreach (var dropInfo in scopeInfo.VariablesToDrop.AsEnumerable().Reverse())
    {
        if (!dropInfo.WasMoved)
        {
            EmitDropCall(dropInfo);
        }
        else if (dropInfo.MovedFields != null)
        {
            // Partial move - drop non-moved fields
            EmitPartialDrop(dropInfo);
        }
    }
}

private void EmitDropCall(DropInfo dropInfo)
{
    // Generate IR instruction to call drop method
    var structType = (IrStructType)dropInfo.VariableType;
    var dropMethodName = $"{structType.StructName}_drop";

    // Create call: StructName_drop(&variable)
    var dropCall = new IrCall(dropMethodName, new IrVoidType());
    dropCall.Arguments.Add(new IrAddressOf(dropInfo.VariableName, dropInfo.VariableType));

    _currentBasicBlock.Instructions.Add(dropCall);
}
```

#### Step 4: Handle Early Returns

**Before return statement** (in `VisitReturnStatement()`):
```csharp
// Drop all variables in all scopes (in reverse scope order)
foreach (var scopeInfo in _dropScopes.Reverse())
{
    EmitDropCallsForScope(scopeInfo);
}

// Then emit the return
```

#### Step 5: Handle Reassignment

**Before assignment** (in `VisitAssignment()`):
```csharp
// If assigning to existing variable that needs drop
var targetVar = LookupVariable(targetName);
if (targetVar != null && _dropInfo.TryGetValue(targetVar.Id, out var dropInfo))
{
    if (!dropInfo.WasMoved)
    {
        // Drop old value before overwriting
        EmitDropCall(dropInfo);
    }
    // Reset move state since we're assigning new value
    dropInfo.WasMoved = false;
    dropInfo.MovedFields = null;
}
```

#### Step 6: Handle Variable Shadowing

**When new variable shadows old one** (in `VisitVariableDeclaration()`):
```csharp
// Check if shadowing existing variable
var existingVar = LookupVariable(varName);
if (existingVar != null && _dropInfo.TryGetValue(existingVar.Id, out var oldDropInfo))
{
    if (!oldDropInfo.WasMoved)
    {
        // Drop shadowed variable
        EmitDropCall(oldDropInfo);
    }
}
```

#### Step 7: Handle Loops

**Before break/continue** (in `VisitBreakStatement()` / `VisitContinueStatement()`):
```csharp
// Drop variables declared in current loop iteration
var loopScope = _dropScopes.Peek();
EmitDropCallsForScope(loopScope);

// Then emit break/continue
```

### Files to Modify

1. **Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs**
   - Add `DropInfo` class
   - Add `ScopeDropInfo` class
   - Add `_dropScopes` and `_dropInfo` fields
   - Modify `VisitBlock()` to push/pop drop scopes
   - Modify `VisitVariableDeclaration()` to track droppable variables
   - Modify `RecordMove()` to mark variables as moved
   - Add `EmitDropCall()` and `EmitDropCallsForScope()` methods
   - Modify `VisitReturnStatement()` to drop before return
   - Modify `VisitAssignment()` to drop before reassign
   - Modify `VisitBreakStatement()` and `VisitContinueStatement()`

### Testing Strategy

Create test cases in `Novus.Tests/DropTests.cs`:

1. **Basic drop**: Variable drops at end of scope
2. **No drop after move**: Moved variable doesn't drop
3. **Drop on early return**: Drop before return statement
4. **Drop on reassignment**: Old value drops before new assignment
5. **Drop on shadowing**: Shadowed variable drops
6. **Partial move**: Only moved fields don't drop
7. **Drop order**: Variables drop in reverse declaration order
8. **Loop break**: Variables drop before break
9. **Nested scopes**: Inner scope drops before outer scope

### Integration with Existing Features

**Defer blocks** (already implemented):
- Defer blocks run **before** automatic drops
- No changes needed to defer implementation

**Move semantics** (Phase 3b - already complete):
- Use existing `RecordMove()` infrastructure
- Just add drop tracking to existing move tracking

### Estimated Time

- **Step 1-2**: Add drop tracking (2-3 hours)
- **Step 3**: Scope exit drops (2-3 hours)
- **Step 4-7**: Special cases (3-4 hours)
- **Testing**: Write comprehensive tests (2-3 hours)

**Total**: 9-13 hours (1-2 days)

### Next Session TODO

1. Add `DropInfo` and `ScopeDropInfo` classes to SemanticAnalyzer.cs
2. Add `_dropScopes` stack and `_dropInfo` dictionary
3. Modify `VisitBlock()` to manage drop scopes
4. Implement `EmitDropCall()` method
5. Modify `VisitVariableDeclaration()` to track droppable variables
6. Update `RecordMove()` to mark as moved in drop info
7. Test with simple example

---

## After Phase 2

Once Phase 2 is complete, we can:
- Implement Drop for stdlib types (Phase 4)
- Fix the memory leaks in mem.novus and collections.novus
- Test thoroughly
- Document the feature

This will bring Novus to **true RAII memory safety**! 🎉
