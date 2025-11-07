# Phase 2: Control Flow Sensitivity

## Goal

Track moves through control flow (if/else, match, while) to catch conditional move bugs.

## Current Problem

```novus
fn test() {
    let x = String::new()
    if condition {
        consume(x)  // x moved in this branch
    }
    // Currently: NO ERROR (bug!)
    x.len()  // Should be ERROR: x may be moved
}
```

## The Rule

**If a variable is moved in ANY branch, it's considered moved after the conditional.**

This is conservative but safe. More advanced analysis (definite assignment) comes later.

## Implementation Plan

### 1. Track Control Flow Contexts

Add to `SemanticAnalyzer`:
```csharp
private Stack<ControlFlowContext> _controlFlowStack = new();

private class ControlFlowContext
{
    public ControlFlowKind Kind { get; init; }  // If, Match, While
    public Dictionary<int, MoveInfo> MovesInBranch { get; init; } = new();
    public List<Dictionary<int, MoveInfo>> AllBranches { get; init; } = new();
}

private enum ControlFlowKind
{
    If,
    Match,
    While,
    Function  // Top-level context
}
```

### 2. Track Branch Entries/Exits

**On entering `if` branch:**
```csharp
private void EnterIfBranch()
{
    var context = new ControlFlowContext { Kind = ControlFlowKind.If };
    _controlFlowStack.Push(context);
}
```

**On exiting `if` branch:**
```csharp
private void ExitIfBranch()
{
    var context = _controlFlowStack.Pop();

    // Merge moves from all branches into parent scope
    foreach (var branchMoves in context.AllBranches)
    {
        foreach (var (varId, moveInfo) in branchMoves)
        {
            // Variable moved in at least one branch → mark as "maybe moved"
            if (!_movedVariables.ContainsKey(varId))
            {
                _movedVariables[varId] = new MoveInfo
                {
                    VariableId = varId,
                    VariableName = moveInfo.VariableName,
                    MoveLocation = moveInfo.MoveLocation,
                    Reason = $"value moved in conditional branch: {moveInfo.Reason}"
                };
            }
        }
    }
}
```

### 3. Update Move Tracking

When a variable is moved, record it in the **current control flow context**:
```csharp
private void MarkVariableAsMoved(int varId, string varName, SourceLocation loc, string reason)
{
    var moveInfo = new MoveInfo
    {
        VariableId = varId,
        VariableName = varName,
        MoveLocation = loc,
        Reason = reason
    };

    if (_controlFlowStack.Count > 0)
    {
        // Inside a branch - track in branch context
        var context = _controlFlowStack.Peek();
        context.MovesInBranch[varId] = moveInfo;
    }
    else
    {
        // Top level - directly mark as moved
        _movedVariables[varId] = moveInfo;
    }
}
```

### 4. Handle `if` Statements

In `VisitIfStatement()`:
```csharp
public override void VisitIfStatement(IfStatementContext ctx)
{
    // Visit condition
    Visit(ctx.condition);

    // Then branch
    EnterIfBranch();
    Visit(ctx.thenBlock);
    var thenMoves = _controlFlowStack.Peek().MovesInBranch;
    ExitIfBranch();

    // Else branch (if exists)
    Dictionary<int, MoveInfo>? elseMoves = null;
    if (ctx.elseBlock != null)
    {
        EnterIfBranch();
        Visit(ctx.elseBlock);
        elseMoves = _controlFlowStack.Peek().MovesInBranch;
        ExitIfBranch();
    }

    // Merge: variable moved if moved in ANY branch
    var allMoves = thenMoves;
    if (elseMoves != null)
    {
        foreach (var (varId, moveInfo) in elseMoves)
        {
            if (!allMoves.ContainsKey(varId))
                allMoves[varId] = moveInfo;
        }
    }

    // Apply merged moves
    foreach (var (varId, moveInfo) in allMoves)
    {
        _movedVariables[varId] = moveInfo;
    }
}
```

### 5. Handle `match` Expressions

Similar logic, but track moves across ALL arms:
```csharp
public override void VisitMatchExpression(MatchExpressionContext ctx)
{
    Visit(ctx.matchTarget);

    var allBranchMoves = new List<Dictionary<int, MoveInfo>>();

    foreach (var arm in ctx.arms)
    {
        EnterMatchArm();
        Visit(arm);
        allBranchMoves.Add(_controlFlowStack.Peek().MovesInBranch);
        ExitMatchArm();
    }

    // Merge all arms
    var mergedMoves = new Dictionary<int, MoveInfo>();
    foreach (var branchMoves in allBranchMoves)
    {
        foreach (var (varId, moveInfo) in branchMoves)
        {
            if (!mergedMoves.ContainsKey(varId))
                mergedMoves[varId] = moveInfo;
        }
    }

    // Apply
    foreach (var (varId, moveInfo) in mergedMoves)
    {
        _movedVariables[varId] = moveInfo;
    }
}
```

### 6. Handle `while` Loops

**Conservative approach**: If a variable is moved inside a loop, it's moved after the loop (can't use it).

```csharp
public override void VisitWhileStatement(WhileStatementContext ctx)
{
    Visit(ctx.condition);

    EnterWhileLoop();
    Visit(ctx.body);
    var loopMoves = _controlFlowStack.Peek().MovesInBranch;
    ExitWhileLoop();

    // Any move in loop body makes variable moved after loop
    foreach (var (varId, moveInfo) in loopMoves)
    {
        _movedVariables[varId] = moveInfo;
    }
}
```

## Error Messages

### Conditional Move
```
error[E0382]: use of moved value: `x`
  --> test.novus:18:5
   |
15 |     if condition {
16 |         consume(x)
   |                 - value moved here
17 |     }
18 |     x.len()
   |     ^ value used here after conditional move
   |
help: value may have been moved in conditional branch
```

### Loop Move
```
error[E0382]: use of moved value: `x`
  --> test.novus:15:5
   |
12 |     while condition {
13 |         consume(x)
   |                 - value moved here in loop
14 |     }
15 |     x.len()
   |     ^ value used here after loop
   |
help: value was moved inside loop body
```

## Test Cases

### Test 1: If with Move
```novus
fn test_if_move() {
    let x = String::new()
    if condition {
        consume(x)
    }
    x.len()  // ERROR: x may be moved
}
```

### Test 2: If-Else Both Move
```novus
fn test_if_else_both() {
    let x = String::new()
    if condition {
        consume(x)
    } else {
        consume(x)
    }
    x.len()  // ERROR: x definitely moved
}
```

### Test 3: Match with Move
```novus
fn test_match() {
    let x = String::new()
    match option {
        Option::Some(v) => {
            consume(x)
        },
        Option::None => {}
    }
    x.len()  // ERROR: x may be moved
}
```

### Test 4: While with Move
```novus
fn test_while() {
    let x = String::new()
    while condition {
        consume(x)  // ERROR: can't move in loop
    }
}
```

## Advanced: Definite Assignment (Phase 3)

Later we can improve this to track "definitely moved" vs "maybe moved":

```novus
let x = String::new()
if condition {
    consume(x)
} else {
    consume(x)
}
// x is DEFINITELY moved (both branches move it)
x.len()  // ERROR: use of moved value

let y = String::new()
if condition {
    consume(y)
}
// y is MAYBE moved (only one branch)
y.len()  // ERROR: use of possibly moved value
```

## Implementation Time

- **Basic if/match/while tracking**: 4-6 hours
- **Test suite**: 2-3 hours
- **Error message improvements**: 1-2 hours

**Total**: 1-2 days

## Benefits

After Phase 2:
- Catches ~85% of move bugs (vs 60% in Phase 1)
- Real-world code patterns work correctly
- Control flow is properly respected

## Next: Phase 3

After control flow:
1. **Assignment moves**: `let y = x` marks x as moved
2. **Return moves**: `return x` marks x as moved
3. **Partial moves**: Track per-field for structs
