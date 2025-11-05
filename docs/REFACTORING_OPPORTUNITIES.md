# Remaining Refactoring Opportunities

This document captures additional refactoring opportunities identified during the Phase 1-4 compiler refactoring effort.

## Completed Refactorings (Phases 1-4)

✅ **Phase 1: AST Helper Utilities** (~200 lines eliminated)
- Created `AstModifierHelper` and `AstExtensions`
- Eliminated 11+ duplicate modifier parsing loops

✅ **Phase 2: IR Visitor Pattern** (~1,113 net lines eliminated)
- Created `IrVisitor` and `IrRewriter` base classes
- Refactored 6 optimizer passes

✅ **Phase 3: Def-Use Analysis** (~519 lines added, ~35 duplication eliminated)
- Centralized def-use chain computation
- Foundation for future optimizations

✅ **Phase 4: Unified SymbolTable** (~650+ lines added, major consolidation)
- Replaced 11+ duplicate dictionaries across IrBuilder and SemanticAnalyzer
- Fixed critical generic enum parameter tracking bug

**Total Impact:** ~2,000+ lines of duplication eliminated

---

## Remaining Opportunities

### 1. ImportResolver Integration (Phase 5) - HIGH COMPLEXITY

**Location:** `IrBuilder.cs` and `SemanticAnalyzer.cs`

**Duplication:**
- `ProcessImport()` - nearly identical in both classes
- `ImportModule()` - 700+ lines in IrBuilder, similar in SemanticAnalyzer
- `ImportModuleSpecificSymbols()` - nearly identical

**Challenge:**
The `ImportModule` methods are extremely complex with:
- Multi-pass enum/struct registration with forward reference handling
- Dependency expansion for struct imports
- Generic method template storage
- Trait implementation handling
- Reexport processing
- Selective vs. wildcard imports

**Why Skipped:**
- Tight coupling with internal state (_module, _symbols, _genericMethodTemplates)
- Complex callback interface requirements
- Risk of introducing subtle import bugs
- Would require extensive testing of all module import scenarios

**Recommendation:**
- Keep existing `ImportResolver.cs` as-is (it's well-structured but unused)
- Future refactoring should create adapter classes that wrap existing registration logic
- Requires dedicated testing sprint to validate all import edge cases

**Estimated Effort:** 1-2 weeks of careful refactoring + testing

---

### 2. TypeParser Utility (Phase 6) - MEDIUM COMPLEXITY

**Location:**
- `IrBuilder.cs:6853` - `ParseType()` method (~400 lines)
- `SemanticAnalyzer.cs:6433` - `ParseType()` method (~400 lines)

**Duplication:**
Both classes have nearly identical type parsing logic:
- `ParseType()` - main dispatcher
- `ParseReferenceType()` - handles `&T` and `&mut T`
- `ParsePointerType()` - handles `*T`
- `ParseNamedType()` - handles struct/enum types with generic instantiation
- `ParseArrayTypeWithSize()` / `ParseArrayTypeInferred()`
- `ParseFunctionPointerType()`
- `ParsePrimitiveType()`

**Key Differences:**
- **Error Handling:** SemanticAnalyzer uses DiagnosticBag; IrBuilder throws exceptions
- **Generic Parameter Lookup:** Different internal structures
- **Monomorphization Caching:** Different dictionary structures
- **Validation:** SemanticAnalyzer validates constraints; IrBuilder doesn't

**Challenge:**
The monomorphization logic is nearly identical (~200 lines each) but differs in:
- Cache storage locations (_monomorphizedEnums vs _symbols.GetMonomorphizedEnum)
- Error reporting mechanisms
- Generic constraint validation (only in SemanticAnalyzer)

**Recommendation:**
Create a `TypeParser` utility with these approaches:

**Option A: Shared Core Logic**
```csharp
public class TypeParser
{
    // Shared monomorphization logic
    public static IrType MonomorphizeStruct(
        IrStructType baseStruct,
        List<IrType> typeArgs,
        Func<string, IrStructType?> cacheLooku p,
        Action<string, IrStructType> cacheStore)
    {
        // Shared logic here
    }

    public static IrType MonomorphizeEnum(
        IrEnumType baseEnum,
        List<IrType> typeArgs,
        Func<string, IrEnumType?> cacheLookup,
        Action<string, IrEnumType> cacheStore)
    {
        // Shared logic here
    }
}
```

**Option B: Strategy Pattern**
```csharp
public interface ITypeParsingContext
{
    IrType? LookupGenericParameter(string name);
    IrStructType? LookupStruct(string name);
    IrEnumType? LookupEnum(string name);
    IrStructType? LookupMonomorphizedStruct(string cacheKey);
    IrEnumType? LookupMonomorphizedEnum(string cacheKey);
    void CacheMonomorphizedStruct(string key, IrStructType type);
    void CacheMonomorphizedEnum(string key, IrEnumType type);
    void ReportError(string code, string message, SourceLocation? loc = null);
}

public class TypeParser
{
    private readonly ITypeParsingContext _context;
    public IrType ParseType(NovusParser.TypeContext context) { ... }
}
```

Both IrBuilder and SemanticAnalyzer would implement `ITypeParsingContext` and use the shared `TypeParser`.

**Estimated Effort:** 3-5 days

---

### 3. CCodeFileBuilder Utility - LOW PRIORITY

**Location:**
- `M68kCodeGenerator.cs` - C runtime function declarations
- Various stdlib wrappers

**Potential Duplication:**
C code generation for runtime functions and wrappers might have patterns that could be extracted, but analysis shows this is likely minimal duplication.

**Recommendation:**
- Low priority
- Only pursue if C code generation becomes more extensive
- Current approach is fine for now

---

## Prioritized Recommendations

1. **TypeParser Utility** (Phase 6) - Medium complexity, high value
   - Clear duplication (~800 lines)
   - Well-scoped problem
   - Strategy pattern approach is clean

2. **ImportResolver Integration** (Phase 5) - High complexity, high value
   - Massive duplication (~1,400+ lines)
   - High risk of breaking imports
   - Requires extensive testing
   - Best tackled after TypeParser

3. **CCodeFileBuilder** - Low priority
   - Minimal duplication found
   - Current approach is adequate

---

## Notes for Future Developers

**Testing Strategy:**
- Any refactoring of type parsing or imports MUST maintain 100% test pass rate
- Add specific regression tests for:
  - Generic type instantiation with nested generics
  - Selective imports with struct dependencies
  - Reexport chains
  - Circular import detection

**Incremental Approach:**
- Extract small utilities first (like we did with AstModifierHelper)
- Test thoroughly after each extraction
- Don't try to refactor everything at once

**When to Stop:**
- If a refactoring requires changing more than 500 lines across multiple files
- If test failures appear that are hard to diagnose
- If the abstraction feels forced or overly complex

The Phase 1-4 refactorings demonstrate the right level of ambition: clear wins with manageable risk.
