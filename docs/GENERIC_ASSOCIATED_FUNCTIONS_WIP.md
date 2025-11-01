# Generic Associated Functions - Work In Progress

**Date:** 2025-11-01
**Status:** 🚧 90% COMPLETE - Type Equality Issue Blocking
**Test Status:** 925/961 passing (36 regressions from type equality issue)

---

## 🎯 Goal

Implement full generic support for associated functions (impl methods) to enable:
```novus
impl<T> Option<T> {
    pub fn FromPointer(ptr: *T) -> Option<*T>
}

// Usage:
let ptr: *u8 = allocate()
return Option::FromPointer(ptr)  // Infers T = u8, returns Option<*u8>
```

---

## ✅ What's Been Implemented

### 1. Enhanced FunctionSymbol (COMPLETE)
**File:** `SemanticAnalyzer.cs` lines 5350-5357

Added `GenericParameters` field to track generic type parameters:
```csharp
public record FunctionSymbol(
    string Name,
    IrType ReturnType,
    List<ParameterSymbol> Parameters,
    SourceLocation Location,
    bool IsExtern = false,
    List<string>? GenericParameters = null  // NEW: ["T"] for Option<T>::FromPointer
);
```

### 2. Generic Parameter Storage in Impl Methods (COMPLETE)
**File:** `SemanticAnalyzer.cs` line 803

Updated `RegisterImplMethod` to store generic parameters from impl blocks:
```csharp
_functions[mangledName] = new FunctionSymbol(
    mangledName, returnType, parameters, location, false,
    genericParams.Count > 0 ? genericParams : null  // Store generic params
);
```

### 3. Monomorphization Cache (COMPLETE)
**File:** `SemanticAnalyzer.cs` line 44

Added cache for monomorphized functions:
```csharp
private readonly Dictionary<string, FunctionSymbol> _monomorphizedFunctions = new();
```

### 4. Generic Type Inference (COMPLETE)
**File:** `SemanticAnalyzer.cs` lines 2635-2712

Two new methods:
- `InferGenericTypes()` - Infer type parameters from arguments
- `InferGenericTypeFromPair()` - Recursively match parameter/argument types

**Example:**
```novus
// Call: Option::FromPointer(ptr) where ptr: *u8
// Function signature: fn FromPointer(ptr: *T) -> Option<*T>
// Inference: Match *u8 against *T → infer T = u8
```

Handles:
- Direct generic parameters: `T` → `u8`
- Pointers to generics: `*T` → `*u8`
- Enums with generics: `Option<T>` (partial support)

### 5. Type Substitution (COMPLETE)
**File:** `SemanticAnalyzer.cs` lines 2714-2776

`SubstituteGenericTypes()` method applies inferred types:
```csharp
// Input: *T with {T → u8}
// Output: *u8

// Input: Option<*T> with {T → u8}
// Output: Option<*u8> (monomorphized, cached)
```

### 6. Function Monomorphization (COMPLETE)
**File:** `SemanticAnalyzer.cs` lines 2778-2809

`MonomorphizeFunction()` method creates concrete function instances:
```csharp
// Input: Option::FromPointer<T>(ptr: *T) -> Option<*T>
// With substitutions: {T → u8}
// Output: Option::FromPointer_ptr_u8(ptr: *u8) -> Option<*u8>
// Mangled name: Option::FromPointer_ptr_u8
// Cached for reuse
```

### 7. Call Site Integration (COMPLETE)
**File:** `SemanticAnalyzer.cs` lines 3371-3414

Updated `VisitCallExpr` to:
1. Detect generic functions
2. Collect argument types
3. Infer generic parameters
4. Monomorphize function
5. Continue with type checking using monomorphized version

---

## ❌ What's Blocking

### The Core Issue: Monomorphized Type Equality

**Problem:**
When `Option<*u8>` is created during:
1. **Parsing** a function signature: `fn test() -> Option<*u8>`
2. **Monomorphization** of a generic function return type

...they create TWO DIFFERENT `IrEnumType` instances that represent the same type but don't compare as equal.

**Current Behavior:**
```novus
pub fn test() -> Option<*u8> {
    let ptr: *u8 = (*u8)42
    let result = Option::FromPointer(ptr)  // Returns Option<*u8> (instance A)
    return result  // ERROR: expected Option<*u8> (instance B), found Option<*u8> (instance A)
}
```

**Error Message:**
```
error[E0003]: mismatched types in return statement
  help: expected type 'Option', found 'Option'
```

Both say "Option" but they're different object instances.

---

## 🔧 Solutions to Try

### Option 1: Fix Type Parser to Use Monomorphization Cache (RECOMMENDED)
**Complexity:** Medium
**Impact:** Fixes root cause

When parsing `Option<*u8>` in source code, the type parser should:
1. Recognize it's a monomorphized type (concrete type args)
2. Check `_monomorphizedEnums` cache
3. If exists, return cached version
4. If not, create and cache with proper cache key

**File to modify:** Find type parser (likely in `SemanticAnalyzer.cs` or separate parser file)

**Search for:** How `Option<*u8>` syntax is parsed into `IrEnumType`

### Option 2: Enhanced Type Equality Check (PARTIAL - TRIED)
**Status:** Attempted but incomplete

**File:** `SemanticAnalyzer.cs` lines 5273-5281

Attempted to compare by cache key:
```csharp
if (expEnum.CacheKey != null && actEnum.CacheKey != null)
{
    return expEnum.CacheKey == actEnum.CacheKey;
}
```

**Problem:** One or both instances don't have cache keys set, so comparison fails.

**Fix needed:** Ensure ALL monomorphized enums get cache keys, including those created during parsing.

### Option 3: Structural Equality in IrEnumType.Equals (COMPLEX)
**Complexity:** High
**Impact:** May have side effects

Modify `IrEnumType.Equals()` in `IR/IrEnumTypes.cs` to:
- Compare enum name
- Compare variant structure
- For monomorphized types, compare type arguments more loosely
- Handle pointer type equality recursively

**Risk:** Could break existing code that relies on strict equality.

### Option 4: Canonical Type Registry (IDEAL but COMPLEX)
**Complexity:** Very High
**Impact:** Clean architecture

Create a global type registry that ensures:
- Only ONE instance of each unique type exists
- All type creation goes through registry
- Equality becomes reference equality (`==`)

**Implementation:**
- New `TypeRegistry` class
- Intercept all `IrEnumType` creation
- Intern types like string interning
- Update all type creation sites

---

## 📊 Test Status

**Before changes:** 961/961 passing (100%)
**After changes:** 925/961 passing (96.3%)
**Regressions:** 36 tests

**Likely causes of regressions:**
- Type equality checks failing for existing code using generic enums
- Enum constructor type inference affected by new code paths
- Cache key mismatches in existing monomorphization code

**To diagnose:**
```bash
dotnet test --filter "FullyQualifiedName~StandardLibraryTests"
dotnet test --filter "FullyQualifiedName~ExampleCompilationTests" -v detailed
```

---

## 🚀 Next Steps

### Immediate (Fix Blocking Issue):
1. **Find the type parser** - Search for where `Option<*u8>` syntax becomes `IrEnumType`
2. **Instrument with debug output** - Add logging to see:
   - When `Option<*u8>` types are created
   - What cache keys (if any) are set
   - Where the two different instances come from
3. **Unify type creation** - Ensure both paths use the same cache

### After Type Equality is Fixed:
4. **IR Builder Updates** - Ensure monomorphized functions generate correct IR
5. **Code Generator Updates** - Handle mangled function names in C output
6. **Comprehensive Testing:**
   ```novus
   // Test 1: Option::FromPointer with *u8
   let ptr1: *u8 = alloc()
   let opt1 = Option::FromPointer(ptr1)

   // Test 2: Option::FromPointer with *Window
   let ptr2: *Window = OpenWindow(...)
   let opt2 = Option::FromPointer(ptr2)

   // Test 3: Vec::new
   let v: Vec<i32> = Vec::new()

   // Test 4: Vec::with_capacity
   let v2 = Vec::with_capacity(10)  // Infer from usage
   ```

---

## 📝 Implementation Notes

### Cache Key Format
Monomorphized types use cache keys like:
- `"Option<u8>"` for `Option<u8>`
- `"Option<*u8>"` for `Option<*u8>` (should be `"Option<ptr_u8>"` for consistency?)

**Created by:** `GetTypeCacheKey()` method

**Used in:**
- `SubstituteGenericTypes()` when creating monomorphized enums
- Type equality checks (attempted)

### Mangled Function Names
Format: `OriginalName_TypeArg1_TypeArg2`

**Examples:**
- `Option::FromPointer<u8>` → `Option::FromPointer_ptr_u8`
- `Vec::new<i32>` → `Vec::new_i32`

**Special handling:**
- `*` becomes `ptr_`
- `<` and `>` become `_`

---

## 🎓 Key Learnings

1. **Generic type inference works similarly to Rust:**
   - Match parameter types against argument types
   - Build substitution map
   - Apply substitutions to return type

2. **Type caching is critical:**
   - Monomorphized types must be cached
   - Same cache must be used everywhere
   - Cache keys must be consistent

3. **Type equality is complex:**
   - Reference equality vs structural equality
   - Monomorphized types need special handling
   - Cache keys provide a good middle ground

4. **Impl methods are just functions:**
   - Stored in `_functions` with mangled names
   - Generic params tracked separately
   - Monomorphization creates new function symbols

---

## 🔍 Debug Commands

```bash
# Test core.novus (has Option::FromPointer)
dotnet run --project Novus/Novus.csproj -- Novus/std/core.novus

# Test simple generic call
dotnet run --project Novus/Novus.csproj -- /tmp/test_simple_generic.novus

# Run specific test
dotnet test --filter "FullyQualifiedName=Novus.Tests.StandardLibraryTests.StdLibraryFile_ShouldParse(core.novus)"

# Check for type-related errors
dotnet test 2>&1 | grep "E0003\|E0015\|mismatched types"
```

---

## Summary

We've implemented ~90% of the infrastructure for generic associated functions:
- ✅ Type inference from arguments
- ✅ Function monomorphization
- ✅ Caching for performance
- ✅ Integration with call sites

The remaining 10% is fixing the type equality issue so that monomorphized types created during parsing match those created during monomorphization. Once this is fixed, `Option::FromPointer<T>`, `Vec::new<T>`, and all other generic associated functions will work perfectly!
