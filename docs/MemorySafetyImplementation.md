# Memory Safety Implementation Plan

## IMMEDIATE GOAL

Stop all memory bugs at **compile time**. No more runtime hangs, crashes, or corruption.

## Phase 1: Core Ownership Tracking (THIS SPRINT)

### 1. Add `consuming` Keyword to Grammar

**File**: `Novus/Parsing/NovusLexer.g4`

Add token:
```antlr
CONSUMING: 'consuming';
```

**File**: `Novus/Parsing/NovusParser.g4`

Update method parameters:
```antlr
methodParameter
    : CONSUMING? MUT? IDENTIFIER ':' type
    ;
```

### 2. AST Representation

**File**: `Novus/AST/AstParameter.cs`

Add property:
```csharp
public bool IsConsuming { get; set; }
```

### 3. Semantic Analysis - Move Tracking

**File**: `Novus/SemanticAnalysis/SemanticAnalyzer.cs`

Add to class:
```csharp
// Track which variables have been moved
private Dictionary<string, MoveInfo> _movedVariables = new();

private class MoveInfo
{
    public string VariableName { get; set; }
    public SourceLocation MoveLocation { get; set; }
    public string Reason { get; set; } // e.g., "passed to consuming parameter"
}
```

### 4. Detect Moves

When analyzing a method call where a parameter is `consuming`:
1. Mark the argument variable as moved
2. Store where it was moved and why

When analyzing a variable use:
1. Check if it's in `_movedVariables`
2. If yes → **COMPILE ERROR** with helpful message

### 5. Error Messages

```
error[E0382]: use of moved value: `formatter`
  --> test.novus:18:5
   |
15 |     let greeting = formatter.finish()
   |                    --------- value moved here (finish consumes self)
   |
18 |     formatter.write_str("x")
   |     ^^^^^^^^^ value used after move
   |
help: if you want to use formatter after finish(), consider cloning it first
   |
15 |     let greeting = formatter.clone().finish()
   |                    +++++++++
```

### 6. Update Standard Library

Mark all consuming methods:
```novus
impl Formatter {
    pub fn finish(consuming self) -> String {  // ← Add 'consuming'
        self.buffer
    }
}
```

### 7. Flow-Sensitive Analysis

Track moves through control flow:
```novus
let x = ...
if condition {
    consume(x)  // x moved in this branch
} else {
    // x still valid here
}
// ERROR: x may or may not be moved (use after conditional move)
```

**Rule**: If a variable is moved in ANY branch, it's considered moved after the conditional.

### 8. Add `move` Keyword (Explicit Moves)

**Grammar**:
```antlr
MOVE: 'move';

primaryExpression
    : MOVE expression  // explicit move
    | ...
    ;
```

**Usage**:
```novus
let s = move formatter.finish()  // Explicit: we're moving formatter
```

**Benefit**: Makes ownership transfer visible in code.

## Phase 2: Borrow Checking (NEXT SPRINT)

### 1. Track References

```csharp
private Dictionary<string, BorrowInfo> _borrows = new();

private class BorrowInfo
{
    public bool IsMutable { get; set; }
    public List<string> BorrowedBy { get; set; }  // What variables borrow this
}
```

### 2. Borrow Rules

- Multiple immutable borrows: **OK**
- One mutable borrow XOR any immutable borrows: **ENFORCED**

### 3. Error Messages

```
error[E0502]: cannot borrow `data` as mutable because it is also borrowed as immutable
  --> test.novus:12:5
   |
10 |     let ref = &data
   |               ----- immutable borrow occurs here
11 |
12 |     modify(&mut data)
   |            ^^^^^^^^^ mutable borrow occurs here
13 |
14 |     print(ref)
   |           --- immutable borrow later used here
```

## Implementation Order

### Day 1: Grammar & Parsing
- [ ] Add `consuming` keyword to lexer
- [ ] Update parser for method parameters
- [ ] Update AST classes
- [ ] Write parser tests

### Day 2: Semantic Analysis - Basic Moves
- [ ] Add move tracking to SemanticAnalyzer
- [ ] Detect when variables are moved (consuming params)
- [ ] Error on use-after-move
- [ ] Write semantic analysis tests

### Day 3: Flow-Sensitive Analysis
- [ ] Track moves through if/else
- [ ] Track moves through loops
- [ ] Track moves through match expressions
- [ ] Write control flow tests

### Day 4: Standard Library Updates
- [ ] Mark all consuming methods in stdlib
- [ ] Fix any resulting compilation errors
- [ ] Add tests for common patterns

### Day 5: Explicit `move` Keyword
- [ ] Add to grammar
- [ ] Implement in semantic analyzer
- [ ] Update error messages
- [ ] Write tests

## Testing Strategy

### Unit Tests

```novus
// test_move_simple.novus
fn test_basic_move() {
    let x = String::new()
    consume(x)  // move
    print(x)    // ERROR: use after move
}

// test_move_conditional.novus
fn test_conditional_move() {
    let x = String::new()
    if condition {
        consume(x)
    }
    print(x)  // ERROR: x may be moved
}

// test_move_field.novus
fn test_field_move() {
    let f = Formatter::new()
    let s = f.finish()  // moves f
    f.write_str("x")    // ERROR: use after move
}
```

### Integration Tests

Real-world scenarios:
- String building with Formatter
- Vec operations
- File handles (must be closed exactly once)
- Option/Result unwrapping

## Code Generator Changes

Once semantic analysis enforces move semantics, the C codegen can:

1. **Trust** that moved values won't be used again
2. Skip null-checking moved-from values
3. Generate cleaner, faster code

The null-out behavior becomes a **safety net**, not the primary mechanism.

## Success Criteria

After Phase 1:
- [ ] All use-after-move bugs caught at compile time
- [ ] No runtime memory corruption from moved values
- [ ] Clear, helpful error messages
- [ ] Standard library fully annotated
- [ ] F-string example compiles and runs correctly

## Migration Path

1. **Week 1**: Implement system, mark it as **warnings only**
2. **Week 2**: Fix all warnings in stdlib
3. **Week 3**: Promote to **errors**, ship it

## Why This Matters

**Current state**: Writing Novus is more dangerous than C because bugs hide until runtime.

**After Phase 1**: Writing Novus is safer than C because the compiler prevents entire classes of bugs.

**The Promise**: "If it compiles, it won't crash from memory bugs."

---

## Next Steps

1. Start with grammar changes (15 minutes)
2. Update AST (15 minutes)
3. Implement basic move tracking (2 hours)
4. Write tests (1 hour)
5. Mark stdlib methods (1 hour)
6. Test on f-string example

**Total estimate**: 1 day to working prototype

Let's fucking do this.
