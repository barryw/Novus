# Novus Compiler Architecture Review

**Reviewer:** Senior Compiler Architect
**Date:** 2025-12-05
**Scope:** Complete compilation pipeline from source to Amiga binary

---

## Executive Summary

The Novus compiler implements a **multi-phase, transformation-based architecture** that compiles a modern systems programming language to C99, which is then compiled by VBCC for AmigaOS/68k targets. The architecture is **fundamentally sound** with clean phase boundaries, but faces **scalability challenges** as language features grow in complexity.

**Key Strengths:**
- Clean IR representation with SSA support
- Proper separation between semantic analysis and code generation
- Excellent generics implementation with monomorphization caching
- Strong memory safety features (move semantics, Drop tracking)
- Good optimization infrastructure

**Critical Issues:**
- **IrBuilder is a 12,000+ line monolith** split across 8 files - needs decomposition
- **Semantic analysis is tightly coupled to IR building** - should be independent
- **Type checking happens too late** - should occur before/during IR building
- **C as an intermediate target creates impedance mismatches** (VBCC workarounds)
- **HIR (High-level IR) is underutilized** - should handle more Novus-specific features

---

## Pipeline Architecture

### Current Flow

```
Source Code (.novus)
    ↓
┌─────────────────────────────────────────────────────────────┐
│ ANTLR Parser (NovusParser.g4)                               │
│ - Lexer: Tokenization                                       │
│ - Parser: AST construction                                  │
└─────────────────────────────────────────────────────────────┘
    ↓
    AST (Parse Tree Context objects)
    ↓
┌─────────────────────────────────────────────────────────────┐
│ SemanticAnalyzer (~11k LOC)                                 │
│ - Type checking (post-IR building)                          │
│ - Error reporting                                            │
│ - Symbol table management                                    │
│ - Move/borrow checking                                       │
│ Problem: Runs AFTER IR building, making errors late         │
└─────────────────────────────────────────────────────────────┘
    ↓ (parallel to)
    ↓
┌─────────────────────────────────────────────────────────────┐
│ IrBuilder (~12k LOC across 8 files)                         │
│ - Multi-pass AST traversal (6 passes)                       │
│ - Type resolution                                            │
│ - Generic instantiation                                      │
│ - IR construction                                            │
│ - Import resolution                                          │
│ Problem: God object doing too much                           │
└─────────────────────────────────────────────────────────────┘
    ↓
    IR (Low-level, SSA-ready)
    ↓
┌─────────────────────────────────────────────────────────────┐
│ IrOptimizationPipeline                                      │
│ - SSA construction                                           │
│ - Constant propagation                                       │
│ - Dead code elimination                                      │
│ - Common subexpression elimination                           │
│ - Strength reduction                                         │
│ - SSA destruction                                            │
│ Strength: Clean, composable passes                           │
└─────────────────────────────────────────────────────────────┘
    ↓
    IR (Optimized)
    ↓
┌─────────────────────────────────────────────────────────────┐
│ CCodeGenerator (~7.5k LOC)                                  │
│ - IR → C99 translation                                      │
│ - VBCC-specific workarounds                                 │
│ - Amiga ABI handling                                        │
│ Problem: Impedance mismatch with VBCC                        │
└─────────────────────────────────────────────────────────────┘
    ↓
    C99 source (.c)
    ↓
┌─────────────────────────────────────────────────────────────┐
│ VBCC Toolchain                                              │
│ - vc (VBCC C compiler)                                      │
│ - vlink (linker)                                            │
│ - Amiga NDK headers                                         │
└─────────────────────────────────────────────────────────────┘
    ↓
    Amiga executable (68k machine code)
```

### Phase Boundaries

**Good:**
- Clear separation between optimization passes
- IR is the central representation (not AST)
- Code generation is isolated from semantic analysis

**Bad:**
- **Semantic analysis runs in parallel with IR building** - should precede it
- **Type information computed during IR building** - should be part of semantic pass
- **No clear HIR → LIR lowering boundary** - HIR exists but is barely used

---

## 1. IR Design Analysis

### Current Structure

**Location:** `Novus.Core/IR/IrModule.cs` (1,956 lines)

```csharp
// Core IR types
IrModule
  ├── Functions: List<IrFunction>
  ├── Enums: List<IrEnumType>
  ├── Structs: List<IrStructType>
  ├── Traits: List<IrTrait>
  ├── TraitImpls: List<IrTraitImpl>
  ├── Constants: Dictionary<string, (Visibility, IrType, object)>
  ├── StaticVariables: List<IrStaticVariable>
  └── ExternalVariables: List<IrExternalVariable>

IrFunction
  ├── Parameters: List<IrParameter>
  ├── LocalVariables: List<IrLocalVariable>
  ├── BasicBlocks: List<IrBasicBlock>
  ├── DeferredBlocks: List<IrBasicBlock>  // Novus-specific
  ├── GenericParameters: List<string>
  └── Attributes: AttributeCollection

IrBasicBlock
  ├── PhiFunctions: List<IrPhi>  // SSA support
  └── Instructions: List<IrInstruction>
```

### Strengths

1. **Clean SSA representation**
   - Phi functions properly separated from instructions
   - SSA construction/destruction passes work well
   - Supports iterative optimization

2. **Rich type system**
   - Proper generics representation with `IrGenericType`
   - Monomorphization caching via `CacheKey`
   - Type interning for efficient equality

3. **Amiga-specific features**
   - Memory sections (Chip/Fast RAM)
   - Copper list data
   - Blitter operation data
   - Well-designed for target

4. **Good instruction set**
   - Covers all necessary operations
   - Clean value/instruction separation
   - Proper control flow (branches, phis)

### Weaknesses

1. **Flat instruction namespace**
   - All 30+ instruction types in one file
   - No grouping by category (memory ops, control flow, arithmetic)
   - Hard to extend with new instructions

2. **Type representation duplication**
   ```csharp
   // Multiple pointer-like types
   IrPointerType       // Can be null
   IrReferenceType     // Never null, immutable
   IrMutReferenceType  // Never null, mutable

   // Problem: Code must check all three variants
   ```

3. **Missing high-level constructs**
   - No representation for `defer` blocks (compiled away too early)
   - No representation for pattern matches (lowered in IrBuilder)
   - No representation for async/await (HIR exists but unused)

4. **Optimization metadata underutilized**
   ```csharp
   public Dictionary<string, object>? Metadata { get; set; }
   ```
   - Present but rarely used
   - No structured PGO data
   - No loop annotations

### Recommendations

**Priority 1: Introduce proper HIR**

```csharp
// High-level IR for Novus-specific features
HirInstruction
  ├── HirDefer           // defer blocks (with scope tracking)
  ├── HirPatternMatch    // match expressions (before lowering)
  ├── HirMethodCall      // method calls (before devirtualization)
  ├── HirDropCall        // explicit Drop calls
  ├── HirCopperList      // Copper DSL (already exists!)
  ├── HirBlitterJob      // Blitter DSL (already exists!)
  └── HirAsyncFunction   // async/await (already exists!)
```

**Priority 2: Organize instruction types**

```csharp
// Group instructions by category
namespace Novus.IR.Instructions
{
    public abstract class MemoryInstruction : IrInstruction { }
    public abstract class ControlFlowInstruction : IrInstruction { }
    public abstract class ArithmeticInstruction : IrInstruction { }
}
```

**Priority 3: Structured metadata**

```csharp
public class IrMetadata
{
    public ProfileGuidedInfo? PGO { get; set; }
    public LoopInfo? Loop { get; set; }
    public InliningHints? Inline { get; set; }
}
```

---

## 2. Frontend Architecture

### IrBuilder Analysis

**Location:** `Novus.Core/Frontend/IrBuilder*.cs` (8 files, ~12,000 lines total)

```
IrBuilder.cs                  (1,180 lines) - Core infrastructure
IrBuilder.Declarations.cs     (1,173 lines) - Type/function registration
IrBuilder.Expressions.cs      (5,984 lines) - Expression compilation ⚠️
IrBuilder.Statements.cs       (2,728 lines) - Statement compilation
IrBuilder.Imports.cs          (1,551 lines) - Module imports
IrBuilder.PatternMatching.cs    (675 lines) - Match expressions
IrBuilder.TypeHelpers.cs        (591 lines) - Type utilities
IrBuilder.DropHelpers.cs        (261 lines) - RAII tracking
```

### The Problem

**IrBuilder is doing 7 jobs:**

1. **AST traversal** - Visitor pattern over parse tree
2. **Type resolution** - Resolving type names to IrType
3. **Type inference** - Inferring generic type arguments
4. **Symbol management** - Tracking functions, variables, types
5. **IR construction** - Building IrModule, IrFunction, IrInstruction
6. **Import resolution** - Loading and merging external modules
7. **Generic instantiation** - Monomorphizing generic types/functions

**Result:** A 12,000-line god object that is:
- Hard to test (integration tests only)
- Hard to modify (change one thing, break many)
- Hard to understand (deep call chains)
- Hard to debug (interleaved concerns)

### Specific Issues

**1. Multi-pass architecture is brittle**

```csharp
// In BuildModule():
// Pass 0a: Auto-import std::core
// Pass 0b: Process explicit imports
// Pass 1:  Register constants
// Pass 2a: Register enum stubs
// Pass 2a.5: Register struct stubs ⚠️ Added later to fix circular deps
// Pass 2b: Fill enum variants
// Pass 3:  Fill struct fields
// Pass 3.1: Register static variables ⚠️ Added later for struct literals
// Pass 3.25: Register traits ⚠️ Added later for trait resolution
// Pass 3.5: Register extern variables
// Pass 4:  Collect function signatures
// Pass 4.5: Collect impl method signatures
// Pass 5:  Build function bodies
// Pass 6:  Build impl method bodies
```

**Problem:** Passes keep getting inserted between other passes as new dependencies are discovered. This is a sign that the phase structure is wrong.

**2. Type resolution is ad-hoc**

```csharp
// IrBuilder has multiple type resolution methods:
ParseType(typeContext)           // Parse AST type to IrType
ResolveType(typeName)            // Lookup type by name
GetEffectiveType(value)          // Get type of IR value
InferGenericArguments(...)       // Infer type arguments from call
```

**Problem:** No single source of truth for "what is the type of X". Type information is computed in multiple places.

**3. Error recovery is poor**

```csharp
// Throughout IrBuilder:
if (enumType == null)
{
    return null; // Silent failure - error already reported
}
```

**Problem:** Errors are reported via DiagnosticBag but control flow continues. Later passes can crash on null values. Need proper error recovery with sentinel values.

### Recommendations

**Priority 1: Extract type resolution into separate phase**

```csharp
public class TypeResolver
{
    // Single responsibility: resolve all types in AST
    public TypedAST Resolve(ParseTree ast);

    // Returns annotated AST with type information
    public class TypedAST
    {
        public Dictionary<IParseTree, IrType> NodeTypes;
        public Dictionary<string, IrStructType> Structs;
        public Dictionary<string, IrEnumType> Enums;
    }
}
```

**Priority 2: Split IrBuilder by concern**

```csharp
// Separate builders for different AST node types
public class FunctionIrBuilder { }    // Function bodies only
public class TypeIrBuilder { }        // Struct/enum definitions
public class ExpressionIrBuilder { }  // Expression compilation
public class StatementIrBuilder { }   // Statement compilation

// Coordinator
public class ModuleIrBuilder
{
    private readonly TypeResolver _typeResolver;
    private readonly FunctionIrBuilder _functionBuilder;
    // ...

    public IrModule Build(ParseTree ast)
    {
        // Single pass: everything is resolved beforehand
        var typedAst = _typeResolver.Resolve(ast);
        var module = new IrModule();

        foreach (var structDecl in typedAst.Structs)
            _typeBuilder.BuildStruct(structDecl, module);

        foreach (var funcDecl in typedAst.Functions)
            _functionBuilder.BuildFunction(funcDecl, module);

        return module;
    }
}
```

**Priority 3: Use proper error recovery**

```csharp
// Instead of null checks everywhere:
public class ErrorType : IrType
{
    public static readonly ErrorType Instance = new();
    public override string Name => "<error>";
}

// Return ErrorType when resolution fails
if (structType == null)
{
    _diagnostics.ReportError(...);
    return ErrorType.Instance;  // Allows compilation to continue
}
```

---

## 3. Semantic Analysis

### Current Architecture

**Location:** `Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs` (~11,500 lines)

**The Good:**

1. **Comprehensive checks**
   - Type checking
   - Move semantics validation
   - Borrow checking
   - Drop tracking
   - Unsafe block validation

2. **Good error messages**
   - Source location tracking
   - Helpful diagnostic codes
   - Context-aware suggestions

3. **LSP support**
   - Symbol location tracking
   - Documentation comment extraction
   - Type information for hover

**The Bad:**

1. **Runs AFTER IR building**
   ```csharp
   // In Compiler.cs workflow:
   var builder = new IrBuilder();
   var module = builder.BuildModule(ast);  // IR built first

   var analyzer = new SemanticAnalyzer();
   var valid = analyzer.Analyze(ast);      // Then validated
   ```

   **Problem:** By the time semantic analysis runs, the IR is already built. If there are type errors, the IR may be malformed.

2. **Duplicates work from IrBuilder**

   Both IrBuilder and SemanticAnalyzer:
   - Parse types from AST
   - Resolve type names
   - Track generic parameters
   - Handle imports
   - Manage symbol tables

   **Result:** Code duplication, inconsistencies, wasted work.

3. **Type checking is split**
   - Basic type checking in SemanticAnalyzer
   - Generic type inference in IrBuilder
   - Cast validation in IrBuilder
   - Method resolution in IrBuilder

   **Problem:** No single source of truth for "is this expression well-typed?"

### Recommendations

**Priority 1: Make semantic analysis precede IR building**

```
Source → Parse → Semantic Analysis → IR Building → Optimization → Codegen
                       ↓
                 Typed AST with full type information
```

**Priority 2: Unified type checking**

```csharp
public class TypeChecker
{
    // Check an expression and return its type
    public IrType CheckExpr(ExprContext expr, IrType? expected = null);

    // Check type compatibility
    public bool IsAssignable(IrType from, IrType to);

    // Resolve method call
    public (string MangledName, IrFunction Signature) ResolveMethod(
        IrType receiver, string methodName, List<IrType> argTypes);

    // Infer generic arguments
    public Dictionary<string, IrType> InferTypeArguments(
        List<string> typeParams, List<IrType> paramTypes, List<IrType> argTypes);
}
```

**Priority 3: Separate move checking**

```csharp
// Move checking is orthogonal to type checking
public class MoveChecker
{
    public void CheckFunction(FunctionSymbol func, TypedAST ast);
    // Reports errors for:
    // - Use after move
    // - Multiple mutable borrows
    // - Move out of borrowed value
}
```

---

## 4. Code Generation

### Current Approach: C99 as Intermediate

**Location:** `Novus/Codegen/CCodeGenerator.cs` (~7,500 lines)

**Why C99?**
- Leverage VBCC's mature 68k code generator
- Easier to debug (can inspect C output)
- Amiga NDK integration (headers, libraries)

**The Cost:**

1. **Impedance mismatches require workarounds**

   ```csharp
   // VBCC bug: moves stack cleanup between comparison and branch
   // Workaround: inline comparisons into if statements
   private Dictionary<string, string> _inlineableComparisons = new();
   ```

2. **68k-specific optimizations are lost**
   - Can't leverage addressing modes directly
   - Can't control register allocation
   - Can't use MOVEM for struct copies
   - Can't use DBRA for tight loops

3. **Struct passing requires workarounds**
   ```c
   // Novus: fn foo(s: MyStruct) -> MyStruct
   // C99:   MyStruct foo(MyStruct s)  // Doesn't compile on VBCC!
   // C99:   void foo(MyStruct* result, const MyStruct* s)  // Actual signature
   ```

4. **Limited control over layout**
   - C struct layout may not match Novus expectations
   - Amiga 68k ABI word-alignment requirements
   - No control over field order (matters for chip RAM structs)

### The Alternative: Direct 68k Generation

**What we'd gain:**

1. **Optimal instruction selection**
   ```asm
   ; IrIndexedFieldAccess → single LEA + offset
   lea   array(a0),a1
   move.w  4(a1,d0.w*8),d1   ; array[index].field
   ```

2. **Register allocation control**
   ```asm
   ; Keep pointers in A-regs, data in D-regs
   move.l  (a0)+,d0     ; Amiga ABI preferences
   ```

3. **No VBCC workarounds**
   - Direct control over code generation
   - No reliance on C undefined behavior

4. **Better 68k utilization**
   - MOVEM for bulk save/restore
   - DBRA for efficient loops
   - Addressing modes for complex loads

**What we'd lose:**

1. **VBCC's optimizer** - actually quite good for 68k
2. **C ecosystem** - easier debugging, familiar output
3. **Development time** - need full 68k backend

### Recommendation

**Short term:** Keep C99 generation but clean it up
- Remove VBCC workarounds by reporting bugs upstream
- Generate cleaner C (less complex expressions)
- Add option to dump IR for debugging

**Medium term:** Prototype direct 68k generation
- Start with simple functions (no calls, no spills)
- Use as proof-of-concept for performance comparison
- Keep C99 path as fallback

**Long term:** Full 68k backend
- Register allocator (linear scan + graph coloring)
- Instruction selection via pattern matching
- Peephole optimization
- Amiga ABI codegen

---

## 5. Scalability Assessment

### How Hard to Add...?

**Generic Closures:** 🔴 **Very Hard**

```novus
fn map<T, U>(items: &[T], f: fn(T) -> U) -> Vec<U>
```

**Problems:**
- Need higher-kinded types in IR
- Generic function pointers not well represented
- IrBuilder's generic instantiation would need major rework

**Recommendation:** Add `IrGenericFunctionPointer` type, extend monomorphization to handle closure contexts.

---

**Async/Await:** 🟡 **Medium**

```novus
async fn fetch(url: &str) -> Result<Data, Error>
```

**Positives:**
- HIR already has `HirAsyncFunction` (unused)
- State machine lowering is well-understood

**Problems:**
- No coroutine representation in IR
- Would need major IrBuilder changes to thread async context
- Integration with AmigaOS signals is complex

**Recommendation:** Implement HIR → LIR lowering pass for async, keep IrBuilder simple.

---

**Pattern Matching Extensions:** 🟢 **Easy**

```novus
match value {
    Some(x) if x > 10 => ...,  // Guards
    Some(x @ 1..=5) => ...,     // Range patterns
    other => ...
}
```

**Why easy:**
- Pattern matching already in IrBuilder.PatternMatching.cs
- Just extend AST traversal
- IR supports it (conditional branches)

---

**Trait Objects (Dynamic Dispatch):** 🔴 **Very Hard**

```novus
fn process(items: &[&dyn Drawable])
```

**Problems:**
- No vtable representation in IR
- No runtime type information (RTTI) system
- Fat pointers not represented (ptr + vtable)
- Would require pervasive IR changes

**Recommendation:** Major redesign needed. Add:
- `IrTraitObject` type with vtable pointer
- `IrVTable` type definition in module
- `IrIndirectMethodCall` instruction

---

**Const Generics:** 🟡 **Medium**

```novus
struct Array<T, const N: usize>
```

**Positives:**
- Generic system is already well-designed
- Monomorphization cache can handle it

**Problems:**
- Need constant expression evaluation in type system
- Type unification needs to handle constant parameters
- IrBuilder type resolution would need extension

---

## 6. Critical Recommendations

### Must Fix Now

1. **Decompose IrBuilder**
   - 12,000 lines is unmaintainable
   - Extract type resolution, symbol management
   - Make it testable

2. **Fix semantic analysis ordering**
   - Run type checking BEFORE IR building
   - Use typed AST as bridge
   - Eliminate duplicate work

3. **Clarify HIR vs LIR boundary**
   - HIR for Novus-specific features (defer, match, async)
   - LIR for generic operations (loads, stores, arithmetic)
   - Add HIR lowering pass

### Should Fix Soon

4. **Better error recovery**
   - Use `ErrorType` instead of null checks
   - Allow compilation to continue after errors
   - Validate IR even when errors occur

5. **Organize IR instruction types**
   - Group by category (memory, control flow, arithmetic)
   - Separate files per category
   - Make extension easier

6. **Add structured metadata**
   - PGO information
   - Loop annotations
   - Inlining hints

### Consider for Future

7. **Direct 68k code generation**
   - Prototype simple backend
   - Compare performance with C99 path
   - Keep C99 as fallback

8. **Trait object support**
   - Design vtable representation
   - Implement dynamic dispatch
   - Add fat pointer support

9. **Incremental compilation**
   - Cache type-checked modules
   - Reuse monomorphizations across modules
   - Speed up rebuilds

---

## 7. Comparison with Other Compilers

### Rust (rustc)

**What they do better:**
- **HIR → MIR → LLVM IR** (three IRs, clear boundaries)
- Type checking completely independent of IR building
- Trait resolution in separate pass
- Better error recovery (can check multiple functions with errors)

**What we do better:**
- Simpler generics (C++ template style, not Rust's complex trait system)
- Less abstraction overhead (no borrow checker formalism)
- Faster compilation (no LLVM overhead)

### Zig

**What they do better:**
- **AST is first-class** (can be analyzed, modified)
- Comptime evaluation at IR level
- Direct LLVM IR generation (no C step)
- Incremental compilation

**What we do better:**
- Better move semantics tracking
- Cleaner generic instantiation
- More type safety guarantees

### C3 (C successor)

**What they do better:**
- **Semantic analysis before IR** (correct order)
- Simple, linear pipeline
- No multi-pass hacks

**What we do better:**
- Richer type system (generics, traits)
- Better memory safety (move semantics)
- More powerful optimization passes

---

## 8. Actionable Recommendations Summary

### Phase 1: Decomposition (1-2 weeks)

```
[ ] Extract TypeResolver from IrBuilder
    - Parse all types in single pass
    - Produce TypedAST

[ ] Extract SymbolTable to top level
    - Shared between TypeResolver and IrBuilder
    - Single source of truth

[ ] Split IrBuilder.Expressions.cs
    - 5,984 lines is too big for one file
    - Group by expression kind
```

### Phase 2: Reordering (1-2 weeks)

```
[ ] Move semantic analysis before IR building
    - TypedAST bridge between phases
    - SemanticAnalyzer reads TypedAST, not raw AST

[ ] Eliminate duplicate work
    - IrBuilder uses TypedAST types directly
    - No re-parsing, no re-resolution
```

### Phase 3: HIR/LIR Split (1-2 weeks)

```
[ ] Define clear HIR instruction set
    - HirDefer, HirPatternMatch, HirMethodCall

[ ] Add HIR lowering pass
    - HIR → LIR transformation
    - Runs before optimization

[ ] Update IrBuilder to emit HIR
    - High-level constructs preserved longer
    - Better for optimization, error reporting
```

### Phase 4: Testing & Validation (ongoing)

```
[ ] Unit tests for each component
    - TypeResolver (without IrBuilder)
    - SemanticAnalyzer (with mocked types)
    - Each IrBuilder subcomponent

[ ] Integration tests for full pipeline
    - Source → TypedAST → IR → C99
    - Verify each phase independently
```

---

## 9. Long-Term Vision

**Where the architecture should be in 12 months:**

```
Source Code
    ↓
  Parser (ANTLR)
    ↓
  Raw AST
    ↓
┌──────────────────────┐
│  Type Resolution     │  ← NEW: Separate phase
│  - Resolve all types │
│  - Build type tables │
└──────────────────────┘
    ↓
  Typed AST
    ↓
┌──────────────────────┐
│  Semantic Analysis   │  ← IMPROVED: Works on typed AST
│  - Type checking     │
│  - Move checking     │
│  - Borrow checking   │
└──────────────────────┘
    ↓
  Validated Typed AST
    ↓
┌──────────────────────┐
│  HIR Builder         │  ← RENAMED: Simpler, focused
│  - High-level IR     │
│  - Novus constructs  │
└──────────────────────┘
    ↓
  HIR (High-level IR)
    ↓
┌──────────────────────┐
│  HIR Lowering        │  ← NEW: Lower Novus features
│  - defer → cleanup   │
│  - match → branches  │
│  - async → state FSM │
└──────────────────────┘
    ↓
  LIR (Low-level IR)
    ↓
┌──────────────────────┐
│  Optimization        │  ← IMPROVED: Richer metadata
│  - SSA form          │
│  - CFG analysis      │
│  - Scalar opts       │
└──────────────────────┘
    ↓
  Optimized LIR
    ↓
┌──────────────────────┐
│  68k Code Generator  │  ← FUTURE: Direct backend
│  - Instruction sel   │     (or keep C99 path)
│  - Register alloc    │
│  - Peephole opts     │
└──────────────────────┘
    ↓
  68k Assembly (or C99)
```

**Key Principles:**

1. **Each phase has ONE job**
2. **Phases communicate via well-defined IR**
3. **Type information flows downward** (never recomputed)
4. **Errors detected early** (type checking before IR building)
5. **Testing is easy** (each phase independently testable)

---

## Conclusion

The Novus compiler has a **solid foundation** but suffers from **architectural debt** accumulated during rapid development. The core IR design is sound, the optimization infrastructure is good, and the Amiga-specific features are well thought out.

**The three critical issues are:**

1. **IrBuilder is a 12,000-line monolith** - Needs decomposition ASAP
2. **Semantic analysis happens too late** - Should precede IR building
3. **HIR is underutilized** - Should handle Novus-specific features better

**Fixing these will:**
- Make the compiler more maintainable
- Enable easier feature additions (closures, async, trait objects)
- Improve error messages (catch errors earlier)
- Speed up development (better testing, clearer boundaries)

**The path forward is clear:** Extract type resolution, reorder phases, and leverage HIR properly. This is 4-6 weeks of refactoring work that will pay off for years to come.

**Overall Grade: B** (Good bones, needs refinement)

- Architecture: B+
- Scalability: C+
- IR Design: A-
- Error Reporting: B
- Code Organization: C
- Testing: B-
- Documentation: B+

With the recommended changes, this could easily be an A- compiler architecture.
