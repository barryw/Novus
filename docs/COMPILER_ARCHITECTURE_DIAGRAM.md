# Novus Compiler Architecture - Visual Diagrams

## Current Architecture (As-Is)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           SOURCE CODE (.novus)                          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         ANTLR PARSER (Grammar)                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                 │
│  │    Lexer     │→ │    Parser    │→ │  Parse Tree  │                 │
│  │  Tokenize    │  │  Build AST   │  │   (Contexts) │                 │
│  └──────────────┘  └──────────────┘  └──────────────┘                 │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
            ┌───────────────────────┴───────────────────────┐
            │                                               │
            ▼                                               ▼
┌───────────────────────────────┐           ┌───────────────────────────────┐
│    SEMANTIC ANALYZER          │           │        IR BUILDER             │
│    (~11,500 lines)            │           │     (~12,000 lines)           │
├───────────────────────────────┤           ├───────────────────────────────┤
│ • Type checking               │           │ • 6-pass AST traversal        │
│ • Move semantics              │◀─ ─ ─ ─ ─│ • Type resolution             │
│ • Borrow checking             │  parallel │ • Generic instantiation       │
│ • Drop tracking               │           │ • Symbol tables               │
│ • Unsafe validation           │           │ • IR construction             │
│                               │           │ • Import resolution           │
│ PROBLEM: Runs too late!       │           │ PROBLEM: Does too much!       │
└───────────────────────────────┘           └───────────────────────────────┘
            │                                               │
            │                                               ▼
            │                               ┌───────────────────────────────┐
            │                               │      IR (IrModule)            │
            │                               │  ┌─────────────────────────┐  │
            │                               │  │ • Functions             │  │
            │                               │  │ • Basic Blocks          │  │
            │                               │  │ • Instructions          │  │
            │                               │  │ • Types (Structs/Enums) │  │
            │                               │  │ • Phi Functions (SSA)   │  │
            │                               │  └─────────────────────────┘  │
            │                               └───────────────────────────────┘
            │                                               │
            │                                               ▼
            │                               ┌───────────────────────────────┐
            │                               │   OPTIMIZATION PIPELINE       │
            │                               ├───────────────────────────────┤
            │                               │ 1. SSA Construction           │
            │                               │ 2. Constant Propagation       │
            │                               │ 3. Dead Code Elimination      │
            │                               │ 4. CSE                        │
            │                               │ 5. Strength Reduction         │
            │                               │ 6. SSA Destruction            │
            │                               │                               │
            │                               │ ✓ Clean, composable passes    │
            │                               └───────────────────────────────┘
            │                                               │
            │                                               ▼
            │                               ┌───────────────────────────────┐
            │                               │   C CODE GENERATOR            │
            │                               │     (~7,500 lines)            │
            │                               ├───────────────────────────────┤
            │                               │ • IR → C99 translation        │
            │                               │ • VBCC workarounds ⚠️         │
            │                               │ • Amiga ABI handling          │
            │                               │ • NDK integration             │
            │                               └───────────────────────────────┘
            │                                               │
            └───────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         C99 SOURCE CODE (.c)                            │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                            VBCC TOOLCHAIN                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                 │
│  │   vc (C99)   │→ │  vlink       │→ │   Amiga      │                 │
│  │  Compiler    │  │   Linker     │  │  Executable  │                 │
│  └──────────────┘  └──────────────┘  └──────────────┘                 │
└─────────────────────────────────────────────────────────────────────────┘
```

## Problem Spots

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    PROBLEM 1: God Object IrBuilder                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   IrBuilder (12,127 lines across 8 files)                              │
│                                                                         │
│   ┌───────────────────────────────────────────────────────────┐        │
│   │ IrBuilder.cs                  │ 1,180 │ Core              │        │
│   │ IrBuilder.Declarations.cs     │ 1,173 │ Types/Functions   │        │
│   │ IrBuilder.Expressions.cs      │ 5,984 │ ⚠️ TOO BIG!       │        │
│   │ IrBuilder.Statements.cs       │ 2,728 │ Statements        │        │
│   │ IrBuilder.Imports.cs          │ 1,551 │ Module imports    │        │
│   │ IrBuilder.PatternMatching.cs  │   675 │ Match expressions │        │
│   │ IrBuilder.TypeHelpers.cs      │   591 │ Type utilities    │        │
│   │ IrBuilder.DropHelpers.cs      │   261 │ RAII tracking     │        │
│   └───────────────────────────────────────────────────────────┘        │
│                                                                         │
│   Responsibilities (7 jobs):                                           │
│   1. AST traversal                                                     │
│   2. Type resolution                                                   │
│   3. Type inference                                                    │
│   4. Symbol management                                                 │
│   5. IR construction                                                   │
│   6. Import resolution                                                 │
│   7. Generic instantiation                                             │
│                                                                         │
│   Result:                                                              │
│   • Hard to test (integration tests only)                             │
│   • Hard to modify (change one thing, break many)                     │
│   • Hard to understand (deep call chains)                             │
│   • Hard to debug (interleaved concerns)                              │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│               PROBLEM 2: Multi-Pass Brittleness                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   Pass 0a:  Auto-import std::core                                      │
│   Pass 0b:  Process explicit imports                                   │
│   Pass 1:   Register constants                                         │
│   Pass 2a:  Register enum stubs                                        │
│   Pass 2a.5: Register struct stubs       ⚠️ Added to fix circular deps │
│   Pass 2b:  Fill enum variants                                         │
│   Pass 3:   Fill struct fields                                         │
│   Pass 3.1: Register static variables    ⚠️ Added for struct literals  │
│   Pass 3.25: Register traits             ⚠️ Added for trait resolution │
│   Pass 3.5: Register extern variables                                  │
│   Pass 4:   Collect function signatures                                │
│   Pass 4.5: Collect impl method signatures                             │
│   Pass 5:   Build function bodies                                      │
│   Pass 6:   Build impl method bodies                                   │
│                                                                         │
│   Problem: Passes keep getting inserted between other passes as new    │
│            dependencies are discovered. Phase structure is wrong!       │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│            PROBLEM 3: Semantic Analysis Runs Too Late                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   Current Order:                                                       │
│   ┌──────────┐    ┌──────────┐    ┌──────────┐                        │
│   │  Parse   │ →  │ Build IR │ →  │  Check   │                        │
│   │   AST    │    │ (IrBuilder) │  │  Types   │                        │
│   └──────────┘    └──────────┘    └──────────┘                        │
│                                       ▲                                 │
│                                       │                                 │
│                                  Problem: Too late!                     │
│                                  IR may be malformed                    │
│                                                                         │
│   Correct Order:                                                       │
│   ┌──────────┐    ┌──────────┐    ┌──────────┐                        │
│   │  Parse   │ →  │  Check   │ →  │ Build IR │                        │
│   │   AST    │    │  Types   │    │ (TypedAST)│                        │
│   └──────────┘    └──────────┘    └──────────┘                        │
│                        ▲                                                │
│                        │                                                │
│                   Right place!                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

## Proposed Architecture (To-Be)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           SOURCE CODE (.novus)                          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         ANTLR PARSER (Grammar)                          │
│                         (No changes needed)                             │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         RAW AST (Parse Tree)                            │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    TYPE RESOLUTION (NEW PHASE)                          │
├─────────────────────────────────────────────────────────────────────────┤
│  TypeResolver                                                           │
│  • Parse all type expressions                                           │
│  • Resolve type names to IrType                                         │
│  • Build complete type tables                                           │
│  • Handle forward references                                            │
│                                                                         │
│  Output: TypedAST                                                       │
│    - Dictionary<ParseTree, IrType> node types                          │
│    - Complete struct/enum/trait definitions                            │
│    - Symbol tables                                                     │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         TYPED AST (NEW DATA STRUCTURE)                  │
│                 All types resolved, ready for validation                │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    SEMANTIC ANALYSIS (IMPROVED)                         │
├─────────────────────────────────────────────────────────────────────────┤
│  SemanticAnalyzer (works on TypedAST, not raw AST)                     │
│                                                                         │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐     │
│  │  Type Checker    │  │  Move Checker    │  │  Borrow Checker  │     │
│  │  - Assignments   │  │  - Use-after-move│  │  - Aliasing      │     │
│  │  - Casts         │  │  - Partial moves │  │  - Lifetimes     │     │
│  │  - Calls         │  │  - Drop tracking │  │  - Mut conflicts │     │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘     │
│                                                                         │
│  ✓ Runs BEFORE IR building                                             │
│  ✓ All errors detected early                                           │
│  ✓ IR is always well-formed                                            │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                  VALIDATED TYPED AST (GUARANTEED CORRECT)               │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       HIR BUILDER (SIMPLIFIED)                          │
├─────────────────────────────────────────────────────────────────────────┤
│  Single Responsibility: Build High-Level IR from validated AST         │
│                                                                         │
│  ┌──────────────────────────────────────────────────────────┐          │
│  │ FunctionHirBuilder   │ Function bodies                   │          │
│  │ TypeHirBuilder       │ Struct/enum definitions          │          │
│  │ ExpressionHirBuilder │ Expression compilation           │          │
│  │ StatementHirBuilder  │ Statement compilation            │          │
│  └──────────────────────────────────────────────────────────┘          │
│                                                                         │
│  Features preserved as HIR:                                            │
│  • defer blocks                                                        │
│  • match expressions                                                   │
│  • method calls                                                        │
│  • Drop calls                                                          │
│  • Copper DSL                                                          │
│  • Blitter DSL                                                         │
│  • Async/await                                                         │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         HIR (HIGH-LEVEL IR)                             │
│               Novus-specific constructs preserved                       │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    HIR LOWERING PASS (NEW)                              │
├─────────────────────────────────────────────────────────────────────────┤
│  Lower Novus-specific features to generic operations:                  │
│                                                                         │
│  • defer → cleanup code insertion                                      │
│  • match → conditional branches                                        │
│  • method calls → static function calls                                │
│  • Drop calls → cleanup functions                                      │
│  • Copper DSL → chip RAM data                                          │
│  • Blitter DSL → register writes                                       │
│  • async/await → state machine                                         │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         LIR (LOW-LEVEL IR)                              │
│                 Generic operations, ready for optimization              │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    OPTIMIZATION PIPELINE (UNCHANGED)                    │
├─────────────────────────────────────────────────────────────────────────┤
│  1. SSA Construction                                                    │
│  2. Constant Propagation                                                │
│  3. Dead Code Elimination                                               │
│  4. Common Subexpression Elimination                                    │
│  5. Strength Reduction                                                  │
│  6. SSA Destruction                                                     │
│                                                                         │
│  ✓ Clean, composable passes                                            │
│  ✓ Works on well-formed LIR                                            │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      OPTIMIZED LIR                                      │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
          ┌─────────────────────────┴─────────────────────────┐
          │                                                   │
          ▼                                                   ▼
┌───────────────────────────┐                 ┌───────────────────────────┐
│   C99 CODE GENERATOR      │                 │  68K CODE GENERATOR       │
│   (CURRENT PATH)          │                 │  (FUTURE PATH)            │
├───────────────────────────┤                 ├───────────────────────────┤
│ • IR → C99 translation    │                 │ • Instruction selection   │
│ • VBCC integration        │                 │ • Register allocation     │
│ • Amiga ABI               │                 │ • Peephole optimization   │
│                           │                 │ • Direct Amiga ABI        │
│ ✓ Mature, debuggable      │                 │ ✓ Optimal 68k code        │
│ ⚠️ VBCC workarounds       │                 │ ✓ No C impedance          │
└───────────────────────────┘                 └───────────────────────────┘
          │                                                   │
          └─────────────────────────┬─────────────────────────┘
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         AMIGA EXECUTABLE (68k)                          │
└─────────────────────────────────────────────────────────────────────────┘
```

## Component Size Comparison

```
BEFORE (Current):
┌─────────────────────────────────────────────────────────────────┐
│ IrBuilder              │ 12,127 lines │ ████████████████████████ │
│ SemanticAnalyzer       │ 11,474 lines │ ███████████████████████  │
│ CCodeGenerator         │  7,584 lines │ ███████████████          │
│ Parser (ANTLR)         │  ~2,000 lines│ ████                     │
│ IR Types               │  1,956 lines │ ████                     │
│ Optimization Passes    │  ~1,500 lines│ ███                      │
└─────────────────────────────────────────────────────────────────┘

AFTER (Proposed):
┌─────────────────────────────────────────────────────────────────┐
│ SemanticAnalyzer       │  8,000 lines │ ████████████████         │
│ CCodeGenerator         │  7,584 lines │ ███████████████          │
│ TypeResolver (NEW)     │  3,000 lines │ ██████                   │
│ HirBuilder (NEW)       │  5,000 lines │ ██████████               │
│ HirLowering (NEW)      │  2,000 lines │ ████                     │
│ FunctionHirBuilder     │  2,000 lines │ ████                     │
│ ExpressionHirBuilder   │  3,000 lines │ ██████                   │
│ StatementHirBuilder    │  1,500 lines │ ███                      │
│ Parser (ANTLR)         │  ~2,000 lines│ ████                     │
│ IR Types               │  2,500 lines │ █████                    │
│ Optimization Passes    │  ~1,500 lines│ ███                      │
└─────────────────────────────────────────────────────────────────┘

Total: Similar line count, but:
  ✓ Better separation of concerns
  ✓ Each component < 5,000 lines (manageable)
  ✓ Independently testable
  ✓ Clear dependencies
```

## Data Flow: Type Information

```
BEFORE (Current):
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│   AST → IrBuilder → IR → SemanticAnalyzer → Check Types         │
│                    ▲                                             │
│                    │                                             │
│              Types computed here                                 │
│              (but IR already built!)                             │
│                                                                  │
│   Problem: If type checking fails, IR may be malformed          │
└──────────────────────────────────────────────────────────────────┘

AFTER (Proposed):
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│   AST → TypeResolver → TypedAST → SemanticAnalyzer → HirBuilder │
│              ▲                         ▲                         │
│              │                         │                         │
│        Types computed             Validated                      │
│        Types flow down ────────────────┘                         │
│                                                                  │
│   ✓ Types known before IR building                              │
│   ✓ Type errors caught early                                    │
│   ✓ IR is always well-formed                                    │
└──────────────────────────────────────────────────────────────────┘
```

## Testing Strategy

```
BEFORE (Current):
┌────────────────────────────────────────────────────────────────┐
│  Integration Tests Only                                        │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Full Pipeline Tests                                     │  │
│  │  Source → IR → C99 → Executable                          │  │
│  │                                                           │  │
│  │  Problem: Hard to isolate failures                       │  │
│  │  - Which phase broke?                                    │  │
│  │  - Can't test components in isolation                    │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────┘

AFTER (Proposed):
┌────────────────────────────────────────────────────────────────┐
│  Unit Tests Per Component                                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  TypeResolver Tests                                      │  │
│  │  • Parse primitive types                                 │  │
│  │  • Resolve struct references                             │  │
│  │  • Handle generics                                       │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  SemanticAnalyzer Tests                                  │  │
│  │  • Type checking rules                                   │  │
│  │  • Move semantics                                        │  │
│  │  • Borrow checking                                       │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  HirBuilder Tests                                        │  │
│  │  • Build functions from TypedAST                         │  │
│  │  • Handle expressions                                    │  │
│  │  • Compile statements                                    │  │
│  └──────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  HirLowering Tests                                       │  │
│  │  • Lower defer to cleanup                                │  │
│  │  • Lower match to branches                               │  │
│  │  • Lower async to state machine                          │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                │
│  + Integration Tests                                           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Full Pipeline Tests                                     │  │
│  │  Source → TypedAST → HIR → LIR → C99 → Executable       │  │
│  │                                                           │  │
│  │  ✓ Each phase independently testable                     │  │
│  │  ✓ Easy to isolate failures                              │  │
│  │  ✓ Fast unit tests + comprehensive integration tests     │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────┘
```

## Summary

**Key Changes:**
1. ✅ Extract TypeResolver from IrBuilder
2. ✅ Run semantic analysis BEFORE IR building
3. ✅ Split IrBuilder into focused components
4. ✅ Add HIR lowering pass
5. ✅ Make each component < 5,000 lines
6. ✅ Enable unit testing per component

**Benefits:**
- Faster development (better testing)
- Easier feature additions (clear boundaries)
- Better error messages (earlier detection)
- More maintainable (smaller components)

**Timeline:**
- Phase 1 (Decomposition): 1-2 weeks
- Phase 2 (Reordering): 1-2 weeks
- Phase 3 (HIR/LIR Split): 1-2 weeks
- Phase 4 (Testing): Ongoing

**Total Refactoring: 4-6 weeks**
