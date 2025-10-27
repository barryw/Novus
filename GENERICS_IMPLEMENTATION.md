# Generics Implementation Plan for Novus

## Goal
Enable proper generic types like `Result<T, E>`, `Option<T>`, and generic functions for a self-hosting Novus compiler on AmigaOS.

## Current State

✅ **Parsing**: Generic syntax is parsed correctly
```novus
pub enum Result<T, E> {
    Ok(T),
    Err(E),
}
```

❌ **Everything Else**: Type substitution, monomorphization, code generation all missing

## Implementation Strategy: Monomorphization

Following Rust/C++ approach (NOT Java-style type erasure):
- Generate separate code for each concrete type instantiation
- `Result<i32, DosError>` and `Result<String, ExecError>` are completely separate types
- No runtime type information needed
- Zero abstraction overhead

## Core Components Needed

### 1. Type Parameter Binding (SemanticAnalyzer.cs)

**What**: Track generic type parameters and their constraints

```csharp
// Track type parameters in scope
private Dictionary<string, GenericTypeParameter> _genericTypeParams = new();

public class GenericTypeParameter
{
    public string Name { get; set; }  // "T", "E"
    public List<IrType> Constraints { get; set; }  // e.g., T: Display
}
```

**Example**:
```novus
pub enum Result<T, E> {
    Ok(T),
    Err(E),
}

// When analyzing this, bind T and E as type parameters
```

### 2. Type Substitution (TypeResolver.cs - new file)

**What**: Replace generic parameters with concrete types

```csharp
public class TypeSubstitution
{
    private Dictionary<string, IrType> _bindings = new();

    // Result<i32, DosError> means: { "T" -> i32, "E" -> DosError }
    public IrType Substitute(IrType type)
    {
        if (type is IrGenericType generic)
        {
            if (_bindings.TryGetValue(generic.Name, out var concrete))
                return concrete;
        }
        // Recursively substitute in composite types
        // ...
    }
}
```

**Example**:
```novus
let result: Result<i32, DosError> = Result::Ok(42)
// Substitute: T=i32, E=DosError throughout Result definition
```

### 3. Monomorphization Pass (Monomorphizer.cs - new file)

**What**: Generate concrete instances for each generic instantiation

```csharp
public class Monomorphizer
{
    // Track which concrete types we've generated
    private HashSet<MonomorphizedType> _generated = new();

    public void MonomorphizeModule(IrModule module)
    {
        // Find all generic instantiations
        var instantiations = FindGenericInstantiations(module);

        // For each unique instantiation, generate concrete code
        foreach (var inst in instantiations)
        {
            GenerateConcreteType(inst);
        }
    }

    private void GenerateConcreteType(GenericInstantiation inst)
    {
        // Example: Result<i32, DosError> becomes Result_i32_DosError
        var concreteName = MangleName(inst);

        // Clone the generic definition
        var concreteType = CloneWithSubstitution(inst.GenericType, inst.TypeArgs);

        // Add to module
        module.Types.Add(concreteType);
    }
}

public record MonomorphizedType(string GenericName, List<IrType> TypeArgs);
```

**Example**:
```novus
// Source code uses:
Result<i32, DosError>::Ok(42)
Result<String, IoError>::Err(IoError::NotFound)

// Compiler generates:
enum Result_i32_DosError { ... }
enum Result_String_IoError { ... }
```

### 4. Type Inference (TypeInference.cs - new file)

**What**: Deduce generic type arguments from context

```csharp
public class TypeInference
{
    // Infer T from: Result::Ok(42)
    // We know: Ok(T), value is i32
    // Therefore: T = i32
    public Dictionary<string, IrType> InferTypeArgs(
        IrEnumType genericEnum,
        string variant,
        List<IrValue> args)
    {
        var bindings = new Dictionary<string, IrType>();

        // Match argument types against variant parameter types
        // Solve for type parameters
        // ...

        return bindings;  // { "T" -> i32 }
    }
}
```

**Example**:
```novus
// User writes:
let x = Result::Ok(42)

// Compiler infers:
// - Result::Ok has signature: Ok(T) -> Result<T, E>
// - 42 has type i32
// - Therefore T = i32
// - E cannot be inferred - ERROR! Must specify: Result<i32, DosError>::Ok(42)
```

### 5. Name Mangling (NameMangler.cs - new file)

**What**: Generate unique names for monomorphized types

```csharp
public class NameMangler
{
    public string MangleGenericType(string baseName, List<IrType> typeArgs)
    {
        var sb = new StringBuilder(baseName);
        sb.Append("_");

        foreach (var arg in typeArgs)
        {
            sb.Append(MangleTypeName(arg));
            sb.Append("_");
        }

        return sb.ToString();
    }

    private string MangleTypeName(IrType type)
    {
        return type switch
        {
            IrIntType it => $"{(it.IsSigned ? "i" : "u")}{it.BitWidth}",
            IrStructType st => st.Name,
            IrEnumType et => et.Name,
            // ...
        };
    }
}
```

**Example**:
```
Result<i32, DosError>     -> Result_i32_DosError
Option<String>            -> Option_String
Vec<Vec<u8>>             -> Vec_Vec_u8
```

### 6. IR Extensions (IrModule.cs)

**What**: Extend IR to track generic information

```csharp
// Mark types as generic or monomorphized
public interface IGenericType
{
    List<string> TypeParameters { get; }
    bool IsGeneric { get; }
}

public class IrEnumType : IrType, IGenericType
{
    public List<string> TypeParameters { get; } = new();
    public bool IsGeneric => TypeParameters.Count > 0;

    // If this is a monomorphized instance:
    public IrEnumType? GenericDefinition { get; set; }
    public Dictionary<string, IrType>? TypeArguments { get; set; }
}
```

### 7. Code Generation (M68kCodeGenerator.cs)

**What**: Generate assembly for monomorphized types

- Each monomorphized type gets its own assembly code
- Names are mangled to avoid conflicts
- Size calculations use concrete types

**Example Assembly**:
```asm
; Result_i32_DosError
Result_i32_DosError_Ok:
    ; tag = 0 (Ok variant)
    move.l  #0,d0
    ; store i32 value
    move.l  8(a6),d1
    ; ...

; Result_String_IoError
Result_String_IoError_Ok:
    ; tag = 0 (Ok variant)
    move.l  #0,d0
    ; store String value (8 bytes: ptr + len)
    move.l  8(a6),d1
    move.l  12(a6),d2
    ; ...
```

## Implementation Phases

### Phase 1: Foundation (8-12 hours)
- [ ] Create TypeSubstitution infrastructure
- [ ] Extend IrEnumType/IrStructType with generic tracking
- [ ] Basic name mangling for monomorphized types

### Phase 2: Monomorphization Engine (12-16 hours)
- [ ] Implement Monomorphizer pass
- [ ] Generic instantiation discovery (traverse IR, find all uses)
- [ ] Clone-and-substitute algorithm
- [ ] Integration into compilation pipeline

### Phase 3: Type Inference (10-15 hours)
- [ ] Implement basic type inference for enum construction
- [ ] Inference for function calls with generic arguments
- [ ] Constraint checking (if constraints are added)
- [ ] Error messages for failed inference

### Phase 4: Code Generation (8-12 hours)
- [ ] Generate code for monomorphized enums
- [ ] Generate code for monomorphized structs
- [ ] Generate code for generic functions
- [ ] Size calculations for generic types

### Phase 5: Testing & Polish (8-12 hours)
- [ ] Unit tests for all components
- [ ] Integration tests with real code
- [ ] Error message improvements
- [ ] Performance optimization

**Total Estimated Time: 46-67 hours of focused development**

## What Works With This Approach

✅ Generic enums: `Option<T>`, `Result<T, E>`
✅ Generic structs: `Vec<T>`, `HashMap<K, V>`
✅ Generic functions: `fn map<T, U>(opt: Option<T>, f: fn(T) -> U) -> Option<U>`
✅ Zero runtime overhead - all resolved at compile time
✅ Full type safety

## Limitations (Acceptable for v1.0)

⚠️ **No Higher-Kinded Types**: Can't have `Container<Container<T>>`
⚠️ **No Trait Bounds** (yet): Can't constrain `T: Display`
⚠️ **Limited Type Inference**: May need explicit type annotations
⚠️ **Code Bloat**: Each instantiation generates separate code (same as C++)

## Alternative: Simpler Approach for Initial Self-Hosting

For getting to self-hosting faster, could implement a **limited generic system**:

### Compiler-Magic Built-ins
```novus
// Only these specific generic types work (compiler knows about them)
pub enum Option<T> { /* compiler magic */ }
pub enum Result<T, E> { /* compiler magic */ }

// All other types: use concrete types
pub enum MyError { /* regular enum */ }
pub struct Vec_i32 { /* concrete struct */ }
```

**Benefits**:
- Much faster to implement (2-4 days vs 2+ weeks)
- Good enough for self-hosting compiler
- Can add full generics later as v2.0 feature

**Drawbacks**:
- Only works for Result<T, E> and Option<T>
- Can't define your own generic types
- Less elegant, more special-casing

## Recommendation for Self-Hosting Path

### Near-term (Self-Hosting v1.0):
1. **Implement enums with associated data** (tagged unions) - REQUIRED
2. **Use compiler-magic for Result/Option ONLY**
3. **Use concrete types everywhere else** (Vec_String, HashMap_String_i32, etc.)

### Long-term (Self-Hosting v2.0):
1. Full generic system with monomorphization
2. Type inference
3. Trait system for constraints
4. Generic specialization

This gets us to self-hosting much faster while keeping the door open for proper generics later.

## Next Steps

**Immediate** (for error system to work):
1. ✅ Remove associated data from enums (use simple enums only)
2. ✅ Implement error.novus with simple DosError/ExecError/etc enums
3. ⏳ Make Result<T, E> work as compiler magic

**Short-term** (for self-hosting v1.0):
1. Implement tagged unions (enums with associated data)
2. Implement Result<T, E> and Option<T> as special built-ins
3. Implement Vec<T> as compiler magic or use Vec_T naming

**Long-term** (for self-hosting v2.0):
1. Full monomorphization-based generics system
2. Type inference
3. Trait system
