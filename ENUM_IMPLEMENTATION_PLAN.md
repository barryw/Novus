# Enum and Generics Implementation Plan

## ✅ Phase 1: Grammar and IR Types (COMPLETED)

- [x] Added enum declaration grammar
- [x] Added generic type parameter grammar
- [x] Added match statement and pattern grammar
- [x] Added `::` path operator for enum variants
- [x] Created `IrEnumType`, `IrEnumVariant`, `IrEnumValue`
- [x] Created `IrMatch`, `IrMatchArm`, `IrPattern` types
- [x] Created `IrGenericType` and `IrMonomorphizedType`
- [x] Updated `IrModule` to track enums

## 🔨 Phase 2: Semantic Analysis (IN PROGRESS)

Need to add to SemanticAnalyzer:

### 2.1 Enum Declaration Analysis
- [ ] Visit `enumDeclaration` nodes
- [ ] Validate variant names are unique
- [ ] Track enum types in symbol table
- [ ] Handle generic type parameters on enums
- [ ] Validate associated data types for variants

### 2.2 Enum Value Construction
- [ ] Handle `Path` expressions (e.g., `Option::Some`)
- [ ] Validate variant exists in enum
- [ ] Type-check associated values against variant signature
- [ ] Support generic type inference (e.g., `Some(42)` infers `Option<i32>`)

### 2.3 Match Expression Analysis
- [ ] Visit `matchStatement` nodes
- [ ] Type-check match value
- [ ] Analyze each match arm pattern
- [ ] Check pattern exhaustiveness
- [ ] Bind pattern variables to correct types
- [ ] Ensure all arms return compatible types (if expression)

### 2.4 Pattern Analysis
- [ ] Validate variant patterns against enum type
- [ ] Check pattern binding variables
- [ ] Validate literal patterns
- [ ] Handle wildcard patterns

## 🏗️ Phase 3: IR Building (PENDING)

Need to update IrBuilder:

### 3.1 Enum Declaration IR
- [ ] Build `IrEnumType` from AST enum declaration
- [ ] Create `IrEnumVariant` instances
- [ ] Track enum in module
- [ ] Handle generic parameters

### 3.2 Enum Value Construction IR
- [ ] Generate `IrEnumValue` for variant construction
- [ ] Pack associated values into enum structure
- [ ] Set discriminant tag

### 3.3 Match Expression IR
- [ ] Generate `IrMatch` instruction
- [ ] Create basic blocks for each match arm
- [ ] Generate `IrExtractTag` to get discriminant
- [ ] Generate `IrExtractVariantData` for pattern bindings
- [ ] Generate conditional branches based on tag
- [ ] Handle wildcard fallthrough

### 3.4 Monomorphization
- [ ] Detect generic instantiations (e.g., `Option<i32>`)
- [ ] Create monomorphized versions of generic enums
- [ ] Mangle names for unique types (`Option_i32`, `Result_i32_IoError`)
- [ ] Track monomorphized instances in module

## ⚙️ Phase 4: Code Generation (PENDING)

Need to update M68kCodeGenerator:

### 4.1 Enum Memory Layout
```
Offset 0-3:  Tag (i32 discriminant)
Offset 4+:   Associated data (union of all variant data)
```

### 4.2 Enum Value Construction
```assembly
; Option::Some(42)
move.l  #0,-(sp)         ; Tag for Some = 0
move.l  #42,-(sp)        ; Associated value
; Result is 8-byte value on stack
```

### 4.3 Pattern Matching Code Gen
- [ ] Generate switch/jump table based on tag
- [ ] Extract tag from enum value
- [ ] Jump to appropriate arm label
- [ ] Extract associated data for bound variables
- [ ] Generate code for each arm body

Example:
```assembly
; match some_value { ... }
move.l  (a6),d0          ; Load enum (tag is first 4 bytes)
cmp.l   #0,d0            ; Compare tag with 0 (Some)
beq     .match_arm_0
cmp.l   #1,d0            ; Compare tag with 1 (None)
beq     .match_arm_1
bra     .match_end

.match_arm_0:  ; Some(x)
move.l  4(a6),d0         ; Extract associated value
; ... arm code ...
bra     .match_end

.match_arm_1:  ; None
; ... arm code ...

.match_end:
```

### 4.4 Generic Monomorphization in Codegen
- [ ] Generate code for each monomorphized instance
- [ ] Use mangled names for type-specific code
- [ ] Inline simple enums where possible

## 🧪 Phase 5: Testing (PENDING)

### 5.1 Basic Enum Tests
- [ ] Simple enums without data
- [ ] Enums with associated data
- [ ] Match with all variants
- [ ] Match with wildcard

### 5.2 Generic Enum Tests
- [ ] `Option<i32>` basic usage
- [ ] `Result<i32, IoError>` usage
- [ ] Multiple monomorphizations in same program
- [ ] Nested generics (`Option<Option<i32>>`)

### 5.3 Integration Tests
- [ ] Compile and run on Amiga emulator
- [ ] Test 14_enums.novus example
- [ ] Verify memory layout with debugger

## 📚 Phase 6: Standard Library (PENDING)

### 6.1 Core Types (std/core.novus)
```novus
pub enum Option<T> {
    Some(T),
    None,
}

pub enum Result<T, E> {
    Ok(T),
    Err(E),
}
```

### 6.2 Helper Methods (requires impl blocks)
```novus
impl<T> Option<T> {
    pub fn is_some(&self) -> bool { ... }
    pub fn is_none(&self) -> bool { ... }
    pub fn unwrap(self) -> T { ... }
}
```

## 📝 Estimated Complexity

- **Lines of Code**: ~2000-3000 lines
- **Files Modified**: 5-7 files
- **Time Estimate**: 10-15 hours of focused work
- **Risk**: Medium-High (complex feature with many edge cases)

## 🎯 Suggested Approach

1. **Start Simple**: Implement non-generic enums first
2. **Test Incrementally**: Get basic match working before adding generics
3. **Add Generics**: Once basic enums work, add generic support
4. **Optimize Later**: Focus on correctness first, optimize codegen later

## 🚀 Quick Win Alternative

If full implementation is too large, we could:
1. Implement **only** `Option<T>` and `Result<T, E>` as built-in compiler magic
2. Hard-code their behavior in the compiler
3. Add general enum support later

This would give us 80% of the value (safe error handling) with 20% of the effort!
