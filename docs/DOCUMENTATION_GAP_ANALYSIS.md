# Novus Language Documentation Gap Analysis

**Date:** 2025-12-17
**Scope:** LanguageDesignDoc.md vs. Implementation
**Status:** Comprehensive review of documented features vs. actual implementation

---

## Executive Summary

The Novus project has made **substantial progress** in implementing core language features, with strong foundations in place for a modern systems language targeting AmigaOS. However, there are significant gaps between the aspirational design documentation and current implementation status.

**Key Findings:**
- ✅ **Strong foundation**: Core language features (structs, enums, pattern matching, generics) are implemented
- ✅ **Memory safety**: Result/Option types, RAII/Drop, defer blocks fully implemented
- ✅ **Async runtime**: Stackless coroutines with signal-based futures working
- ⚠️ **Hardware DSLs**: Documented extensively but **not yet implemented** (copper/blitter)
- ⚠️ **Library/Device support**: Attributes designed but **not fully implemented**
- ⚠️ **Stdlib**: Good coverage but incomplete for documented Amiga-specific APIs

---

## 1. Core Language Features

### 1.1 Syntax & Basic Constructs ✅ IMPLEMENTED

| Feature | Documented | Parser | Type Checker | Codegen | Status |
|---------|-----------|--------|--------------|---------|--------|
| Function declarations | ✅ | ✅ | ✅ | ✅ | **Complete** |
| Struct declarations | ✅ | ✅ | ✅ | ✅ | **Complete** |
| Enum declarations | ✅ | ✅ | ✅ | ✅ | **Complete** |
| Trait declarations | ✅ | ✅ | ✅ | ⚠️ | **Partial** (trait system exists, monomorphization WIP) |
| Impl blocks | ✅ | ✅ | ✅ | ✅ | **Complete** |
| Pattern matching | ✅ | ✅ | ✅ | ✅ | **Complete** |
| Generics | ✅ | ✅ | ✅ | ✅ | **Complete** (monomorphization-based) |
| Module system | ✅ | ✅ | ✅ | ✅ | **Complete** (from/import) |

**Evidence:**
- Parser grammar (NovusParser.g4) includes all documented syntax
- 48+ defer usage examples in test suite
- Generic tests show full support for `Result<T,E>` and `Option<T>`
- Extensive enum pattern matching tests

### 1.2 Type System ✅ IMPLEMENTED

| Type Category | Documented | Implemented | Notes |
|--------------|-----------|-------------|-------|
| Primitive integers | ✅ u8/u16/u32/u64, i8/i16/i32/i64 | ✅ | Full support |
| Booleans | ✅ bool | ✅ | Complete |
| Floating-point | ✅ f32/f64 | ✅ | Present in lexer/parser |
| Fixed-point | ✅ fixed16/fixed32 | ⚠️ | **Tokens exist, semantics not implemented** |
| Arrays | ✅ [T; N] and [T] | ✅ | Static and dynamic |
| Slices | ✅ []T | ✅ | With bounds checking |
| Tuples | ✅ (T, U, V) | ✅ | Full support |
| Pointers | ✅ *T, &T, &var T | ✅ | Raw and safe references |
| Function pointers | ✅ fn(args) -> ret | ✅ | Implemented |
| Closures | ✅ closure(args) -> ret | ⚠️ | **Documented but not implemented** |

**Gap: Fixed-Point Math** ⚠️
- **Documented** (§6.4, §27.7): `fixed16` (8.8), `fixed32` (16.16) with intrinsics
- **Implementation**: Lexer tokens exist (`KW_FIXED16`, `KW_FIXED32` in NovusLexer.g4:82-83)
- **Missing**: No semantic analysis, no codegen for fixed-point arithmetic
- **Priority**: Medium (important for 68k performance but workarounds exist)

**Gap: Closures** ⚠️
- **Documented** (§17 v1.5, §23.2.2): First-class closures for async and callbacks
- **Implementation**: `KW_CLOSURE` token exists, grammar has `closureExpression` rule
- **Missing**: Capture analysis, closure struct generation, upvalue management
- **Priority**: Medium (planned for v1.5, not blocking v1.0)

---

## 2. Memory Management ✅ MOSTLY COMPLETE

### 2.1 RAII & Resource Safety ✅ IMPLEMENTED

| Feature | Documented | Implemented | Status |
|---------|-----------|-------------|--------|
| `defer` blocks | ✅ §17, §22.1 | ✅ | **Complete** - 48+ examples |
| Drop trait | ✅ §22 | ✅ | **Complete** - Full Drop implementation |
| Handles (RAII) | ✅ §22.1, §23 | ✅ | **Complete** - WindowHandle, ScreenHandle, etc. |
| `using` syntax | ✅ §22.1 | ✅ | **Complete** - Parser + semantic support |
| Move semantics | ✅ §22 | ✅ | **Complete** - Move checker implemented |

**Evidence:**
- `Drop-COMPLETE.md` documents full RAII implementation
- `AmigaResourceSafety-COMPLETE.md` shows Handle wrappers
- Test files show extensive defer usage

### 2.2 Allocators ⚠️ PARTIAL

| Feature | Documented | Implemented | Status |
|---------|-----------|-------------|--------|
| Global allocators | ✅ §22.2 | ✅ | Fast/Chip mem allocation |
| Arena allocators | ✅ §22.2, §22.5 | ⚠️ | **Partially implemented** |
| Pool allocators | ✅ §22.2, §22.5 | ⚠️ | **Design exists, incomplete** |
| Slab allocators | ✅ §22.2, §22.5 | ⚠️ | **Documented but not implemented** |
| Custom allocator param | ✅ §22.2 | ❌ | **Not implemented in APIs** |

**Gap: Advanced Allocators** ⚠️
- **Documented** (§22.2, §22.5): Arena/pool/slab allocators with custom allocator parameters
- **Implementation**: Basic chip/fast allocation works, advanced allocators incomplete
- **Priority**: Medium-High (needed for performance-critical code)

---

## 3. Error Handling ✅ COMPLETE

| Feature | Documented | Implemented | Status |
|---------|-----------|-------------|--------|
| `Result<T, E>` | ✅ §17, §13.7 | ✅ | **Complete** - Core type |
| `Option<T>` | ✅ §17, §13.7 | ✅ | **Complete** - Core type |
| `try` operator `?` | ✅ §27.9 | ✅ | **Complete** - Parser + codegen |
| Pattern matching on Result/Option | ✅ | ✅ | **Complete** |
| Mandatory Result in stdlib | ✅ §13.7 | ✅ | **Complete** - All FFI returns Result |

**Evidence:**
- `std/core.novus` implements full Result/Option types with impl blocks
- `ResultOptimizationPass.cs` shows compiler optimization for Result
- Test suite has extensive Result/Option coverage

---

## 4. Async/Await ✅ IMPLEMENTED

| Feature | Documented | Implemented | Status |
|---------|-----------|-------------|--------|
| `async fn` | ✅ §17, §23.6 | ✅ | **Complete** |
| `await` expressions | ✅ | ✅ | **Complete** |
| State machine lowering | ✅ §27.10 | ✅ | **Complete** - AsyncLoweringPass |
| Signal-based futures | ✅ §17, §23.6 | ✅ | **Complete** - Exec signal integration |
| VBlank/timer async | ✅ §23.6 | ✅ | **Complete** - stdlib helpers |
| Channels | ✅ §17 v1.5 | ✅ | **Complete** - channel_multiprocess_test.novus |

**Evidence:**
- `AsyncLoweringPass.cs` (100+ lines) implements state machine transformation
- `async_simple_test.novus` and `channel_multiprocess_test.novus` in test suite
- `std/async/` directory with runtime support

---

## 5. Hardware DSLs ❌ NOT IMPLEMENTED

### 5.1 Copper DSL ❌ MISSING

**Documented:** §23.2 (extensive specification)
```novus
copper {
    move(COLOR00, RGB(255,0,0))
    wait(scan(64))
    move(COLOR00, RGB(0,0,255))
}
```

**Implementation Status:**
- ✅ Parser: `copperList` and `copperOperation` rules exist (NovusParser.g4:489-499)
- ✅ Lexer: `KW_COPPER` token exists (line 52)
- ❌ Semantic analysis: No copper instruction validation
- ❌ Codegen: No copper word generation
- ❌ Stdlib: No `hw.copper` module (only register definitions in `std/hardware/registers.novus`)

**Gap Impact:** **HIGH**
- Copper lists are a core Amiga feature prominently documented
- Parser support suggests this was planned but abandoned
- Documentation promises compile-time validation and `copperviz` tool

### 5.2 Blitter DSL ❌ MISSING

**Documented:** §23.3 (extensive specification)
```novus
blitter {
    op      = CopyMasked(src, mask = src.mask)
    target  = dst.at(x,y)
    size    = pixels(width:32, height:32)
}
```

**Implementation Status:**
- ✅ Parser: `blitterJob` and `blitterField` rules exist (NovusParser.g4:501-507)
- ✅ Lexer: `KW_BLITTER` token exists (line 53)
- ❌ Semantic analysis: No blitter job validation
- ❌ Codegen: No BLTCONx register generation
- ❌ Stdlib: No `hw.blit` module

**Gap Impact:** **HIGH**
- Blitter is critical for Amiga graphics performance
- Documentation promises auto-computed minterm, modulo, safety checks
- Extensively documented but completely unimplemented

### 5.3 Paula Audio ⚠️ PARTIAL

**Documented:** §23.4 (channel management, async support)

**Implementation Status:**
- ⚠️ Basic audio support exists in `std/hardware/paula.novus`
- ❌ Full async audio API not implemented
- ⚠️ Channel management partial

**Gap Impact:** **MEDIUM**

---

## 6. AmigaOS Integration

### 6.1 Library/Device Attributes ❌ MOSTLY MISSING

**Documented:** §13 (building libraries and devices)

| Attribute | Documented | Parser | Semantic | Codegen | Status |
|-----------|-----------|--------|----------|---------|--------|
| `@resident` | ✅ §13.1 | ✅ | ❌ | ❌ | **Not implemented** |
| `@autoinit` | ✅ §13.1 | ✅ | ❌ | ❌ | **Not implemented** |
| `@libvec` | ✅ §13.1 | ✅ | ❌ | ❌ | **Not implemented** |
| `@devicevec` | ✅ §13.1 | ✅ | ❌ | ❌ | **Not implemented** |
| `@interrupt` | ✅ §13.6 | ✅ | ❌ | ❌ | **Not implemented** |
| `@packed` | ✅ §13.1 | ✅ | ✅ | ✅ | **Complete** |
| `@align(N)` | ✅ §13.1 | ✅ | ✅ | ✅ | **Complete** |

**Evidence:**
- Grammar supports attribute syntax: `attribute` rule (line 27-30)
- `ATTRIBUTE_SYSTEM_COMPLETE.md` documents attribute parsing
- **Missing**: ROMTag generation, vector table emission, AutoInit blocks

**Gap Impact:** **HIGH**
- Building libraries/devices is a documented v1.0 goal (§13)
- `BuildingAmigaDevices.md` and `AddingLibrarySupport.md` exist but implementation incomplete

### 6.2 FFI Layer ✅ COMPLETE

| Feature | Documented | Implemented | Status |
|---------|-----------|-------------|--------|
| `extern` declarations | ✅ §24.2 | ✅ | **Complete** |
| Amiga ABI | ✅ §24.2, §27.1 | ✅ | **Complete** - d0 return, register args |
| Result-based wrappers | ✅ §24.3 | ✅ | **Complete** - All stdlib uses Result |
| Exec/Intuition/Graphics APIs | ✅ §24 | ✅ | **Complete** - `std/ffi/` |

**Evidence:**
- `std/ffi/` contains exec, dos, graphics, intuition, gadtools
- `AmigaOS_ABI_Reference.md` documents calling convention
- Test suite shows extensive FFI usage

---

## 7. Inline Assembly ✅ COMPLETE

**Documented:** §28 (external assembly only for v1.0)

**Implementation Status:**
- ✅ Parser: Full assembly block support with ASM_MODE lexer
- ✅ Register binding syntax (`x in d0`, `out d0`)
- ✅ Use clause for constants (`sizeof`, `offsetof`)
- ✅ Clobbers and volatile support
- ✅ Multi-return syntax for multiple registers

**Evidence:**
- NovusLexer.g4 has complete `ASM_MODE` (lines 269-546)
- NovusParser.g4 has `asmStatement` and `asmExpression` (lines 509-620)
- `ASSEMBLY_INTEGRATION_GUIDE.md` documents usage

**Status:** **Complete** ✅

---

## 8. Graphics & Hardware Assets ❌ NOT IMPLEMENTED

### 8.1 Sprites DSL ❌ MISSING

**Documented:** §25.2 (hardware sprites, 16px wide)
```novus
const SHIP = spr.bank {
    depth: 2,
    sprite Idle { "..112211..2211.." }
}
```

**Implementation:** **None**
**Gap Impact:** **HIGH** - Core Amiga graphics feature

### 8.2 BOBs (Blitter Objects) ❌ MISSING

**Documented:** §25.3 (arbitrary-sized with masks)

**Implementation:** **None**
**Gap Impact:** **HIGH**

### 8.3 Bitmap Fonts ❌ MISSING

**Documented:** §25.5 (monospace & variable width)

**Implementation:** **None**
**Gap Impact:** **MEDIUM**

**Note:** §25 promises "one-liners to draw" and compile-time validation. Completely absent from implementation.

---

## 9. Target Profiles & Fat Binaries ⚠️ PARTIAL

**Documented:** §26 (CPU profiles, chipset profiles, multi-version dispatch)

**Implementation Status:**
- ✅ CPU target selection (`--cpu 68000`, `--cpu 68020`, etc.)
- ❌ Chipset profiles (`--chipset OCS|ECS|AGA|auto`) **not implemented**
- ❌ `@cpu(min=...)` attribute not enforced
- ❌ Fat binaries (`--cpu fat:000,020,060`) **not implemented**
- ❌ `@multiversion` attribute **not implemented**

**Gap Impact:** **MEDIUM**
- Basic CPU targeting works
- Advanced features documented but unimplemented

---

## 10. Stdlib Coverage

### 10.1 Core Types ✅ COMPLETE

| Module | Documented | Implemented | Status |
|--------|-----------|-------------|--------|
| Result/Option | ✅ | ✅ | **Complete** |
| String | ✅ | ✅ | **Complete** |
| Vec | ✅ | ✅ | **Complete** |
| Box | ✅ | ✅ | **Complete** |

### 10.2 AmigaOS Wrappers ✅ MOSTLY COMPLETE

| Module | Documented | Implemented | Coverage |
|--------|-----------|-------------|----------|
| exec | ✅ | ✅ | ~90% - Core APIs |
| dos | ✅ | ✅ | ~80% - File I/O |
| intuition | ✅ | ✅ | ~70% - Windows/screens |
| graphics | ✅ | ✅ | ~60% - Basic drawing |
| gadtools | ✅ | ✅ | ~50% - Menus/gadgets |

**Evidence:**
- `std/ffi/` has comprehensive extern declarations
- `std/ui/`, `std/io/`, `std/os/` provide safe wrappers
- All APIs return Result types as documented

### 10.3 Hardware Modules ❌ INCOMPLETE

| Module | Documented | Exists | Status |
|--------|-----------|--------|--------|
| hw.copper | ✅ §23.2 | ❌ | **Missing** |
| hw.blit | ✅ §23.3 | ❌ | **Missing** |
| hw.paula | ✅ §23.4 | ⚠️ | **Partial** |
| hw.chipset | ✅ | ✅ | **Exists** (detection only) |
| hw.registers | ✅ | ✅ | **Complete** (register definitions) |

---

## 11. Tooling

### 11.1 Compiler Commands ⚠️ PARTIAL

**Documented:** §19.8, §28.10

| Tool | Documented | Implemented | Status |
|------|-----------|-------------|--------|
| `novusc build` | ✅ | ✅ | **Complete** |
| `novusc compile` | ✅ | ✅ | **Complete** |
| `novusc fmt` | ✅ | ❌ | **Not implemented** |
| `novusc inspect` | ✅ | ⚠️ | **Partial** (symbols only) |
| `novusc run` | ✅ | ❌ | **Not implemented** (UAE integration) |
| `novusc trace` | ✅ | ❌ | **Not implemented** |
| `novusc package` | ✅ | ❌ | **Not implemented** |
| `novusc copperviz` | ✅ | ❌ | **Not implemented** |
| `novusc blitviz` | ✅ | ❌ | **Not implemented** |

### 11.2 LSP & Editor Support ✅ COMPLETE

**Evidence:**
- `LSP_COMPLETE.md` documents full LSP implementation
- `RIDER_LSP_SETUP.md` for IDE integration

---

## 12. Features Implemented But Not Documented

### 12.1 Advanced Type System Features

| Feature | Implemented | Documented | Notes |
|---------|-------------|-----------|-------|
| Where clauses | ✅ | ⚠️ | Parser + semantic support, barely mentioned in docs |
| Trait bounds | ✅ | ⚠️ | Working but under-documented |
| Turbofish (`::<T>`) | ✅ | ❌ | Not mentioned in design doc |
| `if let` / `let else` | ✅ | ❌ | Not in design doc |
| While var syntax | ✅ | ❌ | Not documented |
| Postfix conditions | ✅ | ❌ | Not documented |

### 12.2 Debug Features

| Feature | Implemented | Documented | Notes |
|---------|-------------|-----------|-------|
| `dbg!()` macro | ✅ | ❌ | Lexer token exists (KW_DBG) |
| `unreachable!()` | ✅ | ❌ | Lexer token exists |
| `matches!()` | ✅ | ❌ | Pattern matching helper |
| `assert!()` | ✅ | ❌ | Not in design doc |

### 12.3 Memory Safety Features

| Feature | Implemented | Documented | Notes |
|---------|-------------|-----------|-------|
| Move checker | ✅ | ⚠️ | Complete but under-documented |
| Drop analysis | ✅ | ⚠️ | Full Drop implementation, minimal docs |
| Lifetime tracking | ✅ | ❌ | Basic implementation exists |
| `consuming` parameter | ✅ | ❌ | Parser support, not documented |

### 12.4 Build System

| Feature | Implemented | Documented | Notes |
|---------|-------------|-----------|-------|
| Workspaces | ✅ | ❌ | `WORKSPACE_DESIGN.md` but not in main doc |
| Project templates | ✅ | ❌ | `PROJECT_TEMPLATES_DESIGN.md` |
| novus.toml | ✅ | ⚠️ | Mentioned briefly, needs detail |
| Multi-file projects | ✅ | ❌ | Works but not explained |

---

## 13. Priority Recommendations

### 13.1 HIGH Priority - Should Implement for v1.0

1. **Complete Library/Device Support** (§13)
   - Implement `@resident`, `@autoinit`, `@libvec` attributes
   - Generate ROMTags and vector tables
   - **Impact:** This is a documented v1.0 core goal
   - **Effort:** Medium (3-4 weeks)

2. **Document Implemented Features**
   - Add sections for `if let`, `while var`, turbofish, etc.
   - Document move semantics and Drop system properly
   - **Impact:** Users can't use features they don't know exist
   - **Effort:** Low (1-2 weeks)

3. **Advanced Allocators** (§22.2)
   - Implement arena/pool/slab allocators
   - Add allocator parameter to APIs
   - **Impact:** Performance-critical for games/demos
   - **Effort:** Medium (2-3 weeks)

### 13.2 MEDIUM Priority - Defer to v1.5

1. **Hardware DSLs** (§23)
   - Copper list generation
   - Blitter job compilation
   - **Impact:** Heavily documented but not blocking
   - **Effort:** High (6-8 weeks)
   - **Recommendation:** Document as v1.5 feature, or implement for v1.0 if resources allow

2. **Graphics Asset DSLs** (§25)
   - Sprite bank compilation
   - BOB/bitmap authoring
   - Font packing
   - **Impact:** Nice-to-have, workarounds exist
   - **Effort:** High (4-6 weeks)

3. **Fat Binaries** (§26)
   - Multi-CPU dispatch
   - `@multiversion` attribute
   - **Impact:** Advanced optimization
   - **Effort:** Medium (3-4 weeks)

### 13.3 LOW Priority - Post-v1.5

1. **Closures** (§17 v1.5)
   - Capture analysis
   - Upvalue management
   - **Impact:** Not critical for v1.0
   - **Effort:** High (4-6 weeks)

2. **Tooling** (§19.8)
   - `novusc fmt`, `novusc run`, `novusc trace`
   - Visualization tools
   - **Impact:** Quality-of-life improvements
   - **Effort:** Medium (each tool 1-2 weeks)

3. **Fixed-Point Math** (§6.4)
   - Implement fixed16/fixed32 semantics
   - Intrinsics for mul/div
   - **Impact:** Nice-to-have, not critical
   - **Effort:** Low-Medium (1-2 weeks)

---

## 14. Documentation Update Recommendations

### 14.1 Clarify Implementation Status

**Recommendation:** Add implementation status markers to `LanguageDesignDoc.md`:
- ✅ **Implemented** (v1.0)
- 🚧 **In Progress** (partial implementation)
- 📅 **Planned** (v1.5+)
- ❌ **Not Planned** (cut features)

### 14.2 Create User-Facing Documentation

**Recommendation:** Split documentation into:
1. **Language Reference** (what exists now)
2. **Design Roadmap** (what's planned)
3. **Tutorial** (how to use implemented features)
4. **Stdlib Reference** (API documentation)

### 14.3 Update Inconsistent Sections

**Examples of inconsistencies:**
- §17 table says `Result/Option` "✅ Core" but §13.7 says "mandatory in all std APIs" (actually implemented)
- §17 says `async/await` is "🚧 MVP" but it's actually complete
- §23 extensively documents copper/blitter DSLs but they don't exist

---

## 15. Conclusion

### What's Working Well ✅

1. **Core language is solid** - Parser, type system, pattern matching all work
2. **Memory safety is production-ready** - Drop/RAII, Result/Option, defer all complete
3. **Async is surprisingly mature** - Full state machine transformation works
4. **FFI is comprehensive** - AmigaOS integration is well-designed and working
5. **Good test coverage** - 150+ example files show real usage

### Critical Gaps ⚠️

1. **Hardware DSLs are vaporware** - Extensively documented but completely missing
2. **Library/device attributes incomplete** - Core v1.0 goal not finished
3. **Documentation misleading** - Features marked "Core" that don't exist
4. **Graphics assets DSL missing** - §25 promises features that aren't there

### Path Forward 🎯

**For v1.0 Release:**
1. Complete library/device attribute implementation
2. Update LanguageDesignDoc.md with accurate implementation status
3. Document all implemented-but-undocumented features
4. Add advanced allocators

**For v1.5 Release:**
5. Implement copper/blitter DSLs (or remove from v1.0 docs)
6. Add graphics asset DSLs
7. Implement fat binaries
8. Add closures if needed

**Overall Assessment:** The Novus compiler is **more capable than documented** in some areas (move semantics, Drop, async) but **less capable than documented** in others (hardware DSLs, library support). The project should either:
- Downgrade documentation to match reality (mark §23, §25 as v1.5+)
- OR implement the missing hardware features for v1.0

---

**Document Version:** 1.0
**Next Review:** After v1.0 feature freeze
