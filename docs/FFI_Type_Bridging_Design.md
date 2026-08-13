# FFI Type Bridging Design

## Philosophy

**"Simplicity is Genius"** — The compiler should handle FFI type conversions automatically, making it ergonomic to call C functions without verbose casts and conversions. The developer burden should be minimal.

**Goal**: Make FFI calls feel natural while maintaining type safety and zero-cost abstractions.

## Current vs. Desired Syntax

### Current (Verbose)
```novus
// String literals need manual conversion
let template_str = "FROM/A,TO/A,VERBOSE/S"
let rdargs = ReadArgs(template_str.as_cstr(), (*u8)&arg_results[0], (*RDArgs)0)

// Null pointer checks are awkward
let rdargs_addr: u32 = (u32)rdargs
if rdargs_addr == 0 {
    return 1
}

// String conversions everywhere
let name = "test.txt"
let file = Open(name.as_cstr(), MODE_OLDFILE)
```

### Desired (Ergonomic)
```novus
// String literals auto-convert to *u8
let rdargs = ReadArgs("FROM/A,TO/A,VERBOSE/S", &arg_results, null)

// Null comparisons just work
if rdargs == null {
    return 1
}

// String literals work directly
let file = Open("test.txt", MODE_OLDFILE)
```

## Automatic Conversions

### 1. String Literal → *u8

**When**: Passing a string literal to an `extern` function expecting `*u8`

**How**:
- String literals in Novus are already null-terminated `Str` types
- Compiler auto-inserts `.as_cstr()` call when passing to extern functions
- Only for **literals** and **Str variables**, not arbitrary expressions

**Examples**:
```novus
// These all work:
extern pub fn Open(name: *u8, mode: i32) -> i32

let file1 = Open("test.txt", MODE_OLDFILE)           // literal → auto .as_cstr()
let filename: Str = "data.bin"
let file2 = Open(filename, MODE_NEWFILE)             // Str var → auto .as_cstr()

// This doesn't auto-convert (needs explicit .as_cstr()):
let result = some_function_returning_str()
let file3 = Open(result, MODE_OLDFILE)               // ERROR: need explicit .as_cstr()
```

**Safety**: Only auto-convert when the Str is guaranteed to be null-terminated (literals and Str variables).

### 2. null Keyword for Pointer Types

**When**: Passing null pointers to extern functions or comparing pointers to null

**How**:
- Add `null` keyword to lexer/parser
- Type-checks as any pointer type `*T`
- Compiles to `(*T)0` in IR
- Works in comparisons: `ptr == null`, `ptr != null`

**Examples**:
```novus
// Pass null pointers
let rdargs = ReadArgs(template, &results, null)      // null → (*RDArgs)0

// Null checks
if rdargs == null {                                  // null → (*RDArgs)0 for comparison
    return 1
}

// Works with any pointer type
let port: *MsgPort = GetPort("MyPort")
if port != null {
    // use port
}
```

**Implementation**:
- Lexer: Add `null` as keyword
- Parser: Parse `null` as NullLiteralExpr
- TypeChecker: Infer pointer type from context (parameter type or comparison operand)
- IR: Generate appropriate `(*T)0` cast

### 3. Array Reference → Pointer Simplification

**When**: Passing `&array` to extern function expecting `*u8` (or any `*T`)

**Current**:
```novus
var results: [u32; 3] = [0, 0, 0]
ReadArgs(template, (*u8)&results[0], null)           // verbose cast
```

**Desired**:
```novus
var results: [u32; 3] = [0, 0, 0]
ReadArgs(template, &results, null)                   // auto-cast to *u8
```

**How**:
- When passing `&array` to extern function expecting `*T`:
  - Auto-convert `&array` → `(*T)&array[0]`
  - Type-check that array element type matches or is compatible with T

**Safety Consideration**:
This is **potentially dangerous** because it bypasses type safety:
```novus
var data: [i32; 10] = [1, 2, 3, ...]
ReadArgs(template, &data, null)  // &data is *i32, but ReadArgs expects *u8!
```

**Solution**: Only allow when target type is `*u8` (generic byte pointer) OR when element types match exactly.

**Revised Rule**:
- `&array[T]` → `*u8`: Always allowed (treating as byte buffer)
- `&array[T]` → `*T`: Always allowed (type matches)
- `&array[T]` → `*U` where T≠U: **ERROR** (type mismatch)

### 4. Pointer Null Comparisons

**When**: Comparing pointer to null or checking truthiness

**Current**:
```novus
let rdargs_addr: u32 = (u32)rdargs
if rdargs_addr == 0 {
    return 1
}
```

**Desired**:
```novus
if rdargs == null {
    return 1
}

// Or even:
if !rdargs {  // pointer as boolean
    return 1
}
```

**How**:
- Support `ptr == null` and `ptr != null` (implemented via null keyword above)
- Optionally support pointer-to-bool coercion in conditions: `if ptr { ... }`
  - null = false, non-null = true
  - Compile to: `(u32)ptr != 0`

**User Choice**: Do we want implicit pointer-to-bool? Or require explicit `ptr != null`?

**Recommendation**: Support both for ergonomics, but `ptr != null` is clearer.

## Implementation Plan

### Phase 1: null Keyword (High Priority)

**Files to Modify**:
1. `Novus.Core/Frontend/Lexer.cs`
   - Add `null` to keyword list
   - Token type: `TokenType.Null`

2. `Novus.Core/Frontend/Parser.cs`
   - Parse `null` as primary expression
   - Create `NullLiteralExpr` AST node

3. `Novus.Core/Frontend/AST/Expressions.cs`
   - Add `NullLiteralExpr : Expr` class

4. `Novus.Core/Middleend/SemanticAnalyzer.cs`
   - Type-check `NullLiteralExpr` based on context:
     - In assignment: infer from target type
     - In function call: infer from parameter type
     - In comparison: infer from other operand
   - If type cannot be inferred, ERROR

5. `Novus.Core/Backend/IrBuilder.cs`
   - Generate cast: `(*T)0` where T is the inferred pointer type

**Test Cases**:
```novus
// Assignment
let ptr: *i32 = null

// Function parameter
extern pub fn Foo(x: *u8)
Foo(null)

// Comparison
let p: *i32 = &x
if p == null { ... }
if null != p { ... }

// Should error (ambiguous type):
let x = null  // ERROR: cannot infer pointer type
```

### Phase 2: String Literal Auto-Conversion (High Priority)

**Files to Modify**:
1. `Novus.Core/Middleend/SemanticAnalyzer.cs`
   - In `VisitCallExpr`, detect if:
     - Function is `extern`
     - Argument is `Str` (literal or variable)
     - Parameter type is `*u8`
   - Auto-insert `.as_cstr()` call in AST

2. Alternative: Handle in IrBuilder
   - Detect same conditions during IR generation
   - Emit IR for `.as_cstr()` call automatically

**Implementation Choice**:
- **AST transformation** (in SemanticAnalyzer): Cleaner, easier to debug, explicit in AST
- **IR generation** (in IrBuilder): Keeps AST unchanged, but less visible

**Recommendation**: AST transformation for visibility and debuggability.

**Algorithm**:
```csharp
// In SemanticAnalyzer.VisitCallExpr
if (funcType.IsExtern)
{
    for (int i = 0; i < call.Arguments.Count; i++)
    {
        var arg = call.Arguments[i];
        var paramType = funcType.Parameters[i].Type;

        // Check: arg is Str, param is *u8
        if (arg.Type is IrStringType && paramType is IrPointerType ptrType && ptrType.PointeeType is IrIntType intType && intType.Width == 8)
        {
            // Transform: arg → arg.as_cstr()
            var asCstrCall = new MemberCallExpr(arg, "as_cstr", new List<Expr>());
            call.Arguments[i] = asCstrCall;
        }
    }
}
```

**Test Cases**:
```novus
extern pub fn Open(name: *u8, mode: i32) -> i32

let f1 = Open("test.txt", MODE_OLDFILE)  // auto .as_cstr()

let name: Str = "data.bin"
let f2 = Open(name, MODE_NEWFILE)        // auto .as_cstr()

let s = get_string()
let f3 = Open(s.as_cstr(), MODE_OLDFILE) // explicit (expression, not var)
```

### Phase 3: Array → Pointer Auto-Conversion (Medium Priority)

**Files to Modify**:
1. `Novus.Core/Middleend/SemanticAnalyzer.cs`
   - In `VisitCallExpr`, detect if:
     - Function is `extern`
     - Argument is `&array` (AddressOfExpr where target is array)
     - Parameter type is `*u8` or `*T` where T matches element type
   - Transform `&array` → `(*T)&array[0]`

**Safety Rules**:
- `&array[T]` → `*u8`: Always allowed
- `&array[T]` → `*T`: Always allowed
- `&array[T]` → `*U` where T≠U and U≠u8: **ERROR**

**Test Cases**:
```novus
extern pub fn ReadArgs(template: *u8, array: *u8, args: *RDArgs) -> *RDArgs

var results: [u32; 3] = [0, 0, 0]
ReadArgs(template, &results, null)  // OK: &[u32] → *u8

extern pub fn ProcessInts(data: *i32, len: i32)
var nums: [i32; 10] = [...]
ProcessInts(&nums, 10)              // OK: &[i32] → *i32

extern pub fn BadFunc(x: *i16)
var data: [i32; 5] = [...]
BadFunc(&data)                      // ERROR: cannot convert &[i32] to *i16
```

### Phase 4: Pointer-to-Bool Coercion (Low Priority, Optional)

**Files to Modify**:
1. `Novus.Core/Middleend/SemanticAnalyzer.cs`
   - In `VisitIfStmt`, detect if condition is pointer type
   - Auto-convert to: `(u32)ptr != 0`

**Debate**: Is this too implicit? User preference?

**Examples**:
```novus
let ptr: *i32 = GetPtr()

if ptr {        // auto: (u32)ptr != 0
    // use ptr
}

if !ptr {       // auto: (u32)ptr == 0
    return
}
```

**Alternative**: Require explicit `!= null`:
```novus
if ptr != null {
    // use ptr
}
```

**User Decision Needed**: Which style do we prefer?

## Edge Cases and Safety

### String Lifetime Safety
**Problem**: Auto-converting string literals is safe, but what about temporary strings?

```novus
fn get_name() -> Str {
    return "temp.txt"
}

let file = Open(get_name(), MODE_OLDFILE)  // DANGER: Str destroyed after call!
```

**Solution**: Only auto-convert:
1. String literals (lifetime = 'static)
2. Str variables (developer manages lifetime)
3. **NOT** function return values or complex expressions

**Rule**: Auto `.as_cstr()` only for:
- `StringLiteralExpr`
- `IdentifierExpr` with type `Str`

### Null Type Inference
**Problem**: Cannot infer type of standalone `null`

```novus
let x = null  // ERROR: what type is x?
```

**Solution**: Require explicit type in declarations:
```novus
let x: *i32 = null  // OK
```

### Array Conversion Safety
**Problem**: Casting arrays to wrong pointer types

```novus
var data: [i32; 10] = [...]
extern pub fn TakesShorts(x: *i16)
TakesShorts(&data)  // Type mismatch!
```

**Solution**: Strict type checking as described in Phase 3 rules.

## Summary of Features

| Feature | Priority | User Impact | Safety |
|---------|----------|-------------|--------|
| `null` keyword | **High** | Clean null checks and parameters | Safe with type inference |
| String literal → `*u8` | **High** | Eliminates `.as_cstr()` boilerplate | Safe for literals/vars only |
| Array → Pointer | Medium | Cleaner array passing | Safe with type rules |
| Pointer → Bool | Low | Shorter conditions | Debate: too implicit? |

## Implementation Order

1. **null keyword** (Phase 1) — Biggest ergonomic win, foundational
2. **String auto-conversion** (Phase 2) — Massive reduction in boilerplate
3. **Array auto-conversion** (Phase 3) — Nice-to-have, less common
4. **Pointer-to-bool** (Phase 4) — Optional, user decision needed

## Testing Strategy

Each phase requires:
1. Unit tests in `Novus.Tests/ExampleCompilationTests.cs`
2. Integration tests with actual AmigaOS FFI (dos.library, graphics.library)
3. Template updates (cli, gui, library templates should use new syntax)
4. Documentation in language guide

## Open Questions for User

1. **Pointer-to-bool**: Do we want `if ptr { ... }` or require `if ptr != null { ... }`?
2. **String expressions**: Should we allow `Open(get_name(), mode)` or require assignment first?
3. **Scope**: Are there other common FFI patterns we should auto-convert?

---

**Next Step**: Get user feedback, then implement Phase 1 (null keyword).
