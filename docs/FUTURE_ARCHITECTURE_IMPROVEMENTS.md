# Future Architecture Improvements

This document captures architectural improvements identified during code review that would benefit the codebase but require significant refactoring effort.

## 1. Unified AST Traversal

**Status:** Documented (not yet implemented)

**Problem:**
Currently, both `SemanticAnalyzer` and `IrBuilder` traverse the ANTLR parse tree independently:
- `SemanticAnalyzer` (in `Novus.Core/SemanticAnalysis/`) performs type checking
- `IrBuilder` (in `Novus.Core/Frontend/`) generates IR

This leads to:
- Duplicated traversal logic
- Duplicated type parsing
- Duplicated symbol tracking
- Potential for semantic analysis and IR to get out of sync

**Proposed Solution:**
Implement a two-phase approach:

### Phase 1: SemanticAnalyzer produces type-annotated AST
```csharp
// SemanticAnalyzer annotates nodes with types
public class TypedAstNode
{
    public IrType Type { get; set; }
    public SourceLocation Location { get; set; }
    public IParseTree ParseTree { get; set; }
}
```

### Phase 2: IrBuilder consumes annotated AST
```csharp
// IrBuilder only deals with already-typed nodes
public class IrBuilder
{
    public IrModule Build(TypedCompilationUnit typedAst) { ... }
}
```

**Benefits:**
- Single source of truth for types
- IR is always type-correct by construction
- Better error recovery (semantic analysis can continue past errors)
- Cleaner separation of concerns

**Estimated Effort:** Large (2-4 weeks)

---

## 2. ScopeManager Integration

**Status:** Class created, integration pending

**Problem:**
Scope management (defer blocks, drops, loop labels) is scattered across IrBuilder fields.

**Current State:**
- `ScopeManager` class created at `Novus.Core/Frontend/ScopeManager.cs`
- Contains unified API for scope operations
- Not yet integrated into IrBuilder

**To Complete:**
1. Replace individual fields in IrBuilder with ScopeManager instance
2. Update all usages in partial class files
3. Add tests for scope management

**Estimated Effort:** Medium (1-2 days)

---

## 3. SSA-by-Default at O2+

**Status:** Documented

**Problem:**
SSA (Static Single Assignment) form is optional even at O2+, but many optimizations work better with SSA.

**Current Code:**
```csharp
// In IrOptimizationPipeline.cs
if (_options.UseSSA && !inSSA)
{
    var ssaConstructor = new SsaConstructor(_function);
    ssaConstructor.ConstructSsa();
    inSSA = true;
}
```

**Proposed Change:**
Make SSA default-on for O2+ optimizations:
```csharp
if (_level >= OptimizationLevel.O2 && !inSSA)
{
    var ssaConstructor = new SsaConstructor(_function);
    ssaConstructor.ConstructSsa();
    inSSA = true;
    stats.SSAConstructed = true;
}
```

**Estimated Effort:** Small (1-2 hours + testing)

---

## 4. IR Validator Pass

**Status:** Documented

**Problem:**
Malformed IR can cause cryptic errors in code generation. No validation pass exists to catch issues early.

**Proposed Validator Checks:**
- All referenced variables are defined
- Type mismatches in operations
- Missing basic block terminators
- Unreachable code detection
- Phi node consistency (SSA mode)

**Implementation Sketch:**
```csharp
public class IrValidator
{
    public List<ValidationError> Validate(IrModule module)
    {
        var errors = new List<ValidationError>();

        foreach (var function in module.Functions)
        {
            ValidateFunction(function, errors);
        }

        return errors;
    }

    private void ValidateFunction(IrFunction function, List<ValidationError> errors)
    {
        var definedVars = new HashSet<string>();

        foreach (var block in function.BasicBlocks)
        {
            ValidateBlock(block, definedVars, errors);
        }
    }
}
```

**Estimated Effort:** Medium (2-3 days)

---

## 5. Result-Based Error Handling in TypeParser

**Status:** Documented

**Problem:**
TypeParser uses exceptions for error handling:
```csharp
throw new Exception($"unknown type '{typeName}'");
```

This makes error recovery difficult and can cause cascade failures.

**Proposed Change:**
Use Result type for parsing:
```csharp
public Result<IrType, TypeError> ParseType(string typeString)
{
    if (!TryParse(typeString, out var type, out var error))
    {
        return Result.Err(error);
    }
    return Result.Ok(type);
}
```

**Benefits:**
- Better error messages with source locations
- Can recover and continue parsing
- Collect all errors instead of failing on first

**Estimated Effort:** Medium (1-2 days)

---

## Priority Order

1. **SSA at O2+** - Small change, big optimization impact
2. **IR Validator** - Catches bugs early, improves debugging
3. **ScopeManager Integration** - Already created, just needs wiring
4. **Result-Based Errors** - Better error experience
5. **Unified AST Traversal** - Major refactor, defer until needed
