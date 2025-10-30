# Traits Design for Novus

## Overview

This document describes the design and implementation of Rust-style traits for Novus. Traits provide compile-time polymorphism through static dispatch (monomorphization), similar to Rust's trait system but without lifetime parameters.

## Goals

1. **Compile-time polymorphism** - No runtime overhead, full monomorphization
2. **Type safety** - Trait bounds checked at compile time
3. **Ergonomic iteration** - Support for `for x in collection` style loops
4. **Generic constraints** - `fn process<T: Display>(item: T)` syntax
5. **Coherence** - One implementation per type-trait pair
6. **No vtables** - Pure static dispatch (dynamic dispatch can be added later)

## Non-Goals

1. No object safety or `dyn Trait` (for now)
2. No associated types (initially - can add later)
3. No default implementations (initially - can add later)
4. No trait objects or fat pointers
5. No lifetime parameters (Novus doesn't have lifetimes yet)

---

## Syntax

### Trait Definition

```novus
trait TraitName<T> {
    fn method_name(&self, param: Type) -> ReturnType;
    fn another_method(&mut self);
    fn static_method(param: Type) -> ReturnType;
}
```

**Examples:**

```novus
trait Display {
    fn display(&self) -> String;
}

trait Iterable<T> {
    fn iter(&self) -> Iterator<T>;
}

trait Iterator<T> {
    fn next(&mut self) -> Option<T>;
    fn has_next(&self) -> bool;
}

trait Comparable<T> {
    fn compare(&self, other: &T) -> i32;
}

trait Drop {
    fn drop(&mut self);
}
```

### Trait Implementation

```novus
impl TraitName<TypeArgs> for TargetType {
    fn method_name(&self, param: Type) -> ReturnType {
        // implementation
    }
}
```

**Examples:**

```novus
// Implement Display for i32
impl Display for i32 {
    fn display(&self) -> String {
        // Convert i32 to String
        return int_to_string(*self);
    }
}

// Implement Iterable for Vec<T>
impl<T> Iterable<T> for Vec<T> {
    fn iter(&self) -> VecIterator<T> {
        return VecIterator<T> {
            vec: self,
            index: 0
        };
    }
}

// Implement Iterator for VecIterator<T>
impl<T> Iterator<T> for VecIterator<T> {
    fn next(&mut self) -> Option<T> {
        if self.index >= self.vec.len() {
            return Option::None;
        }
        let value = self.vec.get(self.index);
        self.index += 1;
        return Option::Some(value);
    }

    fn has_next(&self) -> bool {
        return self.index < self.vec.len();
    }
}
```

### Generic Bounds (Trait Constraints)

```novus
// Function with trait bound
fn print_all<T: Display>(items: Vec<T>) {
    let iter = items.iter();
    while iter.has_next() {
        let item = iter.next();
        match item {
            Option::Some(val) => println(val.display()),
            Option::None => break
        }
    }
}

// Multiple bounds
fn process<T: Display + Comparable<T>>(a: T, b: T) {
    println(a.display());
    if a.compare(&b) > 0 {
        println("a is greater");
    }
}

// Struct with trait bounds
struct Container<T: Display> {
    value: T
}

impl<T: Display> Container<T> {
    fn show(&self) {
        println(self.value.display());
    }
}
```

### Where Clauses (Future)

```novus
// For complex bounds (future enhancement)
fn complex<T, U>(a: T, b: U) -> bool
where
    T: Display + Comparable<T>,
    U: Iterator<T>
{
    // implementation
}
```

---

## Grammar Changes

### Add Trait Keyword

```antlr
KW_TRAIT : 'trait';
KW_WHERE : 'where';  // for future where clauses
```

### Update compilationUnit

```antlr
compilationUnit
    : importDeclaration*
      reexportDeclaration*
      (constDeclaration
       | globalVariableDeclaration
       | structDeclaration
       | enumDeclaration
       | traitDeclaration     // NEW
       | implDeclaration
       | functionDeclaration)*
      EOF
    ;
```

### Add traitDeclaration

```antlr
traitDeclaration
    : attribute* KW_PUB? KW_TRAIT IDENTIFIER genericParams? '{' traitItem* '}'
    ;

traitItem
    : functionSignature   // function declaration without body
    ;

functionSignature
    : attribute* KW_FN IDENTIFIER '(' parameterList? ')' ('->' type)?
    ;
```

### Update implDeclaration

```antlr
implDeclaration
    : attribute* KW_IMPL genericParams? (traitRef KW_FOR)? typeName genericTypeArgs? '{' implItem* '}'
    ;

traitRef
    : typeName genericTypeArgs?
    ;
```

### Update genericParams to support bounds

```antlr
genericParams
    : '<' genericParam (',' genericParam)* '>'
    ;

genericParam
    : IDENTIFIER (':' traitBounds)?
    ;

traitBounds
    : traitBound ('+' traitBound)*
    ;

traitBound
    : typeName genericTypeArgs?
    ;
```

---

## AST Nodes

### TraitDeclaration

```csharp
public class TraitDeclaration
{
    public string Name { get; set; }
    public List<string> GenericParameters { get; set; }
    public List<TraitMethod> Methods { get; set; }
    public List<AttributeNode> Attributes { get; set; }
    public bool IsPublic { get; set; }
    public SourceLocation Location { get; set; }
}

public class TraitMethod
{
    public string Name { get; set; }
    public List<ParameterNode> Parameters { get; set; }
    public TypeNode? ReturnType { get; set; }
    public List<AttributeNode> Attributes { get; set; }
    public SourceLocation Location { get; set; }
}
```

### TraitImplementation

```csharp
public class TraitImplementation
{
    public string TraitName { get; set; }
    public List<TypeNode> TraitTypeArgs { get; set; }
    public TypeNode TargetType { get; set; }
    public List<string> GenericParameters { get; set; }
    public List<FunctionDeclaration> Methods { get; set; }
    public SourceLocation Location { get; set; }
}
```

### TraitBound

```csharp
public class TraitBound
{
    public string TraitName { get; set; }
    public List<TypeNode> TypeArgs { get; set; }
    public SourceLocation Location { get; set; }
}

public class GenericParameter
{
    public string Name { get; set; }
    public List<TraitBound> Bounds { get; set; }  // NEW
    public SourceLocation Location { get; set; }
}
```

---

## Semantic Analysis

### Phase 1: Register Traits (New Pass)

Add a new semantic analysis pass between "Register Enums" and "Register Structs":

**Pass 2b: Register Traits**

For each trait declaration:
1. Check for duplicate trait names in current scope
2. Register trait with name, generic parameters, and method signatures
3. Validate method signatures (no bodies allowed)
4. Store in `TraitRegistry`

```csharp
public class TraitRegistry
{
    private Dictionary<string, TraitInfo> _traits = new();

    public void RegisterTrait(string name, TraitInfo info);
    public TraitInfo? LookupTrait(string name);
    public bool HasTrait(string name);
}

public class TraitInfo
{
    public string Name;
    public List<string> GenericParameters;
    public Dictionary<string, MethodSignature> Methods;
    public bool IsPublic;
}

public class MethodSignature
{
    public string Name;
    public List<(string Name, NovusType Type)> Parameters;
    public NovusType? ReturnType;
    public bool HasSelfParam;
    public bool IsMutSelf;
}
```

### Phase 2: Register Trait Implementations (Extend Existing Pass)

Update **Pass 5: Register impl methods** to handle trait implementations:

For each `impl Trait for Type`:
1. Resolve trait name (check TraitRegistry)
2. Resolve target type
3. Validate all trait methods are implemented
4. Check method signatures match trait definition
5. Check for duplicate implementations (coherence)
6. Store in `TraitImplementationRegistry`

```csharp
public class TraitImplementationRegistry
{
    // Key: (TraitName, TargetTypeName) -> Implementation
    private Dictionary<(string, string), TraitImplementationInfo> _impls = new();

    public void RegisterImpl(string traitName, string targetType, TraitImplementationInfo info);
    public TraitImplementationInfo? LookupImpl(string traitName, string targetType);
    public bool HasImpl(string traitName, string targetType);
}

public class TraitImplementationInfo
{
    public string TraitName;
    public string TargetType;
    public List<string> GenericParameters;
    public Dictionary<string, FunctionDeclaration> Methods;
}
```

### Phase 3: Validate Trait Bounds (New Validation)

When analyzing generic functions/structs:
1. Parse trait bounds from generic parameters
2. For each generic type parameter with bounds:
   - Check that all required traits exist
   - Check that all uses satisfy the bounds
3. At call sites with generic parameters:
   - Resolve concrete types
   - Check that concrete types implement required traits

```csharp
public class TraitBoundChecker
{
    public void ValidateBounds(
        List<GenericParameter> genericParams,
        Dictionary<string, NovusType> concreteTypes,
        TraitRegistry traitRegistry,
        TraitImplementationRegistry implRegistry);
}
```

### Phase 4: Method Resolution

When resolving method calls:
1. If the type has a trait bound, look up method in trait definition
2. Resolve to the concrete implementation for the type
3. During monomorphization, substitute with the concrete method

---

## Type System Changes

### NovusType Extensions

Add trait bound information to generic types:

```csharp
public class GenericType : NovusType
{
    public string Name { get; set; }
    public List<TraitBound> Bounds { get; set; }  // NEW
}
```

### Type Checking with Trait Bounds

```csharp
public class TypeChecker
{
    public bool SatisfiesBounds(
        NovusType type,
        List<TraitBound> bounds,
        TraitImplementationRegistry implRegistry)
    {
        foreach (var bound in bounds)
        {
            if (!implRegistry.HasImpl(bound.TraitName, type.Name))
            {
                return false;
            }
        }
        return true;
    }
}
```

---

## Monomorphization

### Trait Method Calls

When monomorphizing generic code with trait bounds:

1. **Identify trait method calls**
   ```novus
   fn print<T: Display>(item: T) {
       item.display();  // trait method call
   }
   ```

2. **At call site with concrete type**
   ```novus
   print(42);  // T = i32
   ```

3. **Resolve implementation**
   - Look up `impl Display for i32`
   - Find concrete method `i32_display`

4. **Generate monomorphized version**
   ```novus
   fn print_i32(item: i32) {
       i32_display(&item);  // direct call to concrete impl
   }
   ```

### Monomorphization Algorithm

```csharp
public class TraitMonomorphizer
{
    public IrFunction Monomorphize(
        FunctionDeclaration genericFunc,
        Dictionary<string, NovusType> typeSubstitutions,
        TraitImplementationRegistry implRegistry)
    {
        // 1. Substitute type parameters
        var concreteFunc = SubstituteTypes(genericFunc, typeSubstitutions);

        // 2. Resolve trait method calls
        foreach (var call in concreteFunc.GetMethodCalls())
        {
            if (IsTraitMethod(call))
            {
                var traitName = GetTraitName(call);
                var targetType = GetReceiverType(call);
                var impl = implRegistry.LookupImpl(traitName, targetType.Name);

                // Replace with concrete method call
                ReplaceWithConcreteCall(call, impl);
            }
        }

        return GenerateIR(concreteFunc);
    }
}
```

---

## Code Generation

Since Novus uses C backend, trait methods become regular C functions:

### Example: Display trait

**Novus trait:**
```novus
trait Display {
    fn display(&self) -> String;
}
```

**No C code generated** (traits are compile-time only)

### Example: Implementation

**Novus impl:**
```novus
impl Display for i32 {
    fn display(&self) -> String {
        return int_to_string(*self);
    }
}
```

**Generated C:**
```c
// novus_generated_Display_i32.c
String Display_i32_display(i32* self) {
    return int_to_string(*self);
}
```

### Example: Generic function with trait bound

**Novus code:**
```novus
fn print_item<T: Display>(item: T) {
    println(item.display());
}

fn main() {
    print_item(42);      // T = i32
    print_item(true);    // T = bool
}
```

**Generated C (after monomorphization):**
```c
// novus_generated_print_item_i32.c
void print_item_i32(i32 item) {
    String s = Display_i32_display(&item);
    println(s);
}

// novus_generated_print_item_bool.c
void print_item_bool(bool item) {
    String s = Display_bool_display(&item);
    println(s);
}

// novus_generated_main.c
void main() {
    print_item_i32(42);
    print_item_bool(true);
}
```

---

## Iterator Pattern Implementation

### Core Traits

```novus
// std/core.novus

trait Iterator<T> {
    fn next(&mut self) -> Option<T>;
    fn has_next(&self) -> bool;
}

trait Iterable<T> {
    fn iter(&self) -> Iterator<T>;
}
```

### Vec Iterator

```novus
// std/collections.novus

pub struct VecIterator<T> {
    vec: &Vec<T>,
    index: i32
}

impl<T> Iterator<T> for VecIterator<T> {
    fn next(&mut self) -> Option<T> {
        if self.index >= self.vec.len() {
            return Option::None;
        }
        let value = self.vec.get(self.index);
        self.index += 1;
        return Option::Some(value);
    }

    fn has_next(&self) -> bool {
        return self.index < self.vec.len();
    }
}

impl<T> Iterable<T> for Vec<T> {
    fn iter(&self) -> VecIterator<T> {
        return VecIterator<T> {
            vec: self,
            index: 0
        };
    }
}
```

### Usage Example

```novus
fn main() {
    var numbers = Vec::<i32>::new();
    numbers.push(10);
    numbers.push(20);
    numbers.push(30);

    // Manual iteration
    var iter = numbers.iter();
    while iter.has_next() {
        let num = iter.next();
        match num {
            Option::Some(n) => println(n),
            Option::None => break
        }
    }

    // Using generic function with trait bound
    print_all(numbers);
}

fn print_all<T: Display>(items: Vec<T>) {
    var iter = items.iter();
    while iter.has_next() {
        match iter.next() {
            Option::Some(item) => println(item.display()),
            Option::None => break
        }
    }
}
```

---

## Implementation Phases

### Phase 1: Grammar & AST (Week 1)
- [ ] Add `KW_TRAIT` keyword
- [ ] Add `traitDeclaration` grammar rule
- [ ] Update `implDeclaration` for trait impls
- [ ] Add `genericParam` with bounds
- [ ] Add AST nodes: `TraitDeclaration`, `TraitImplementation`, `TraitBound`
- [ ] Update parser to construct new AST nodes

### Phase 2: Semantic Analysis (Week 2)
- [ ] Create `TraitRegistry` and `TraitImplementationRegistry`
- [ ] Add Pass 2b: Register Traits
- [ ] Update Pass 5: Handle trait implementations
- [ ] Implement trait bound validation
- [ ] Implement method resolution with traits
- [ ] Add error diagnostics for:
  - Duplicate trait definitions
  - Missing trait implementations
  - Method signature mismatches
  - Unsatisfied trait bounds
  - Duplicate implementations (coherence)

### Phase 3: Type System (Week 3)
- [ ] Add trait bounds to `GenericType`
- [ ] Update type checker to validate bounds
- [ ] Update type inference to handle trait constraints
- [ ] Implement `SatisfiesBounds` checker

### Phase 4: Monomorphization (Week 4)
- [ ] Identify trait method calls in IR
- [ ] Resolve trait implementations during monomorphization
- [ ] Replace trait method calls with concrete calls
- [ ] Update monomorphization cache to include trait info

### Phase 5: Code Generation (Week 5)
- [ ] Generate mangled names for trait methods
- [ ] Update C code generator for trait method calls
- [ ] Ensure proper includes for trait implementations

### Phase 6: Standard Library (Week 6)
- [ ] Define `Iterator<T>` trait in std::core
- [ ] Define `Iterable<T>` trait in std::core
- [ ] Create `VecIterator<T>` struct in std::collections
- [ ] Implement `Iterator<T>` for `VecIterator<T>`
- [ ] Implement `Iterable<T>` for `Vec<T>`

### Phase 7: Testing (Week 7)
- [ ] Test basic trait definitions
- [ ] Test trait implementations
- [ ] Test generic functions with trait bounds
- [ ] Test method resolution
- [ ] Test monomorphization
- [ ] Test Vec iteration
- [ ] Test multiple trait bounds
- [ ] Test coherence (duplicate impl detection)
- [ ] Add ~20 test cases covering all scenarios

---

## Error Messages

### Good Error Messages Examples

```
error: trait `Display` not found
  --> test.novus:10:15
   |
10 | fn print<T: Display>(item: T) {
   |               ^^^^^^^ trait not found in this scope
   |
help: you might be missing an import
   |
   | from std::core import Display
```

```
error: type `i32` does not implement trait `Display`
  --> test.novus:15:5
   |
15 |     print(42);
   |     ^^^^^ the trait `Display` is not implemented for `i32`
   |
help: consider implementing the trait
   |
   | impl Display for i32 {
   |     fn display(&self) -> String { ... }
   | }
```

```
error: missing method `display` in implementation of trait `Display`
  --> test.novus:20:1
   |
20 | impl Display for MyType {
   | ^^^^^^^^^^^^^^^^^^^^^^^^ missing `display` in trait impl
   |
note: `display` is required by trait `Display`
  --> std/core.novus:5:5
   |
5  |     fn display(&self) -> String;
   |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^
```

```
error: method `display` has an incompatible signature
  --> test.novus:25:5
   |
25 |     fn display(&self) -> i32 {
   |     ^^^^^^^^^^^^^^^^^^^^^^^^ expected `String`, found `i32`
   |
note: trait requires this signature
  --> std/core.novus:5:5
   |
5  |     fn display(&self) -> String;
   |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^
```

---

## Open Questions

1. **Associated types?** Not in initial version, but worth planning for
   - Example: `trait Iterator { type Item; fn next(&mut self) -> Option<Self::Item>; }`

2. **Default implementations?** Not initially, but valuable
   - Example: Provide default `is_empty()` if `len()` is implemented

3. **Trait inheritance?** (e.g., `trait Ord: Eq`)
   - Could add later with `trait Ord: Eq + Display`

4. **Automatic trait derivation?** (`#[derive(Display, Clone)]`)
   - Very valuable, worth implementing after basic traits

5. **For-in syntax?** Should we add `for item in collection {}`?
   - Requires desugaring to iterator manually
   - Should use `Iterable` trait under the hood

---

## Future Enhancements

### Phase 8: For-In Loop Syntax
```novus
for item in numbers {
    println(item);
}

// Desugars to:
{
    var _iter = numbers.iter();
    while _iter.has_next() {
        match _iter.next() {
            Option::Some(item) => {
                println(item);
            },
            Option::None => break
        }
    }
}
```

### Phase 9: Common Standard Traits
- `Clone` - Deep copying
- `Copy` - Shallow copying (marker trait)
- `Drop` - Custom cleanup (already have defer, but trait would be explicit)
- `Eq` / `PartialEq` - Equality comparison
- `Ord` / `PartialOrd` - Ordering comparison
- `Default` - Default value constructor
- `Debug` - Debug formatting
- `Hash` - Hashing for hash tables

### Phase 10: Derive Macros
```novus
#[derive(Display, Clone, Debug)]
struct Point {
    x: i32,
    y: i32
}
```

---

## References

- Rust Book Chapter on Traits: https://doc.rust-lang.org/book/ch10-02-traits.html
- Rust Reference on Trait Implementations: https://doc.rust-lang.org/reference/items/implementations.html
- Swift Protocols: https://docs.swift.org/swift-book/LanguageGuide/Protocols.html
