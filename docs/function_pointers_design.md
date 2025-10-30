# Function Pointers & Higher-Order Functions for Novus

## Current Status: 80% Done!

Great news - most of the infrastructure for function pointers **already exists**! Here's what we have:

### ✅ Already Implemented

1. **Grammar support** - `fn(params) -> return` type syntax ✅
2. **Type system** - `IrFunctionPointerType` class ✅
3. **Type interning** - Function pointer type caching ✅
4. **Type checking** - Parameter/return type validation ✅
5. **IR support** - `IrFunctionAddress` and `IrIndirectCall` ✅
6. **Semantic analysis** - Function pointer call validation ✅

### ❌ Missing Pieces

1. **C code generation** - Need to emit C function pointer syntax
2. **Taking function addresses** - Need `&function_name` or similar syntax
3. **Test coverage** - No test files exist yet

**Bottom line: We're like 80% there!** Just need codegen and some syntax decisions.

---

## Design Goals

1. **Swift-style ergonomics** - Pass functions as easily as data
2. **Type safety** - Full compile-time checking of signatures
3. **Zero abstraction cost** - Direct function pointers, no closures/captures (yet)
4. **Familiar syntax** - Feel natural to C/Swift/Rust developers

---

## Proposed Syntax

### 1. Function Pointer Types

Already supported in grammar:

```novus
// Type annotation for function pointers
let callback: fn(i32, i32) -> i32;
let printer: fn(String);           // No return type = void
let factory: fn() -> Vec<i32>;     // No params
```

### 2. Taking Function Addresses

**Option A: Automatic (implicit, like Swift)**
```novus
fn add(a: i32, b: i32) -> i32 {
    return a + b;
}

let operation: fn(i32, i32) -> i32 = add;  // Implicit address-of
call_with_numbers(add);                     // Pass directly
```

**Option B: Explicit with & (like Rust)**
```novus
let operation = &add;               // Explicit &function_name
call_with_numbers(&add);
```

**Option C: Explicit with @ (unique to Novus)**
```novus
let operation = @add;               // @ for "address of function"
call_with_numbers(@add);
```

**My Recommendation: Option A (implicit)**
- Most ergonomic (Swift-style)
- Already works in type system (function name resolves to address)
- Less noisy syntax
- Can always add explicit & later if needed

### 3. Passing Functions as Parameters

```novus
fn apply_operation(a: i32, b: i32, op: fn(i32, i32) -> i32) -> i32 {
    return op(a, b);  // Indirect call through function pointer
}

fn add(a: i32, b: i32) -> i32 { return a + b; }
fn mul(a: i32, b: i32) -> i32 { return a * b; }

fn main() {
    let sum = apply_operation(5, 3, add);      // Pass add function
    let product = apply_operation(5, 3, mul);  // Pass mul function
    println(sum);      // 8
    println(product);  // 15
}
```

### 4. Storing Functions in Variables

```novus
fn greet(name: String) {
    println("Hello, " + name);
}

fn farewell(name: String) {
    println("Goodbye, " + name);
}

fn main() {
    var say: fn(String) = greet;
    say("Alice");           // "Hello, Alice"

    say = farewell;         // Reassign
    say("Bob");            // "Goodbye, Bob"
}
```

### 5. Arrays of Functions (Dispatch Tables)

```novus
fn op_add(a: i32, b: i32) -> i32 { return a + b; }
fn op_sub(a: i32, b: i32) -> i32 { return a - b; }
fn op_mul(a: i32, b: i32) -> i32 { return a * b; }
fn op_div(a: i32, b: i32) -> i32 { return a / b; }

fn main() {
    let operations: [4]fn(i32, i32) -> i32 = [op_add, op_sub, op_mul, op_div];

    let a = 10;
    let b = 5;

    for i in 0..4 {
        let result = operations[i](a, b);
        println(result);
    }
}
```

### 6. Function Pointers in Structs (Callbacks)

```novus
struct EventHandler {
    on_click: fn(i32, i32),      // Mouse click at x, y
    on_keypress: fn(u8)          // Key code
}

fn handle_click(x: i32, y: i32) {
    println("Clicked at: " + int_to_string(x) + ", " + int_to_string(y));
}

fn handle_key(code: u8) {
    println("Key pressed: " + u8_to_string(code));
}

fn main() {
    let handler = EventHandler {
        on_click: handle_click,
        on_keypress: handle_key
    };

    handler.on_click(100, 200);
    handler.on_keypress(65);  // 'A'
}
```

---

## Real-World Use Cases

### 1. Higher-Order Vec Methods (Immediate Win!)

```novus
impl<T> Vec<T> {
    // Map: transform each element
    fn map<U>(&self, f: fn(&T) -> U) -> Vec<U> {
        var result = Vec::<U>::new();
        for i in 0..self.len() {
            result.push(f(&self.get(i)));
        }
        return result;
    }

    // Filter: keep elements matching predicate
    fn filter(&self, predicate: fn(&T) -> bool) -> Vec<T> {
        var result = Vec::<T>::new();
        for i in 0..self.len() {
            let item = self.get(i);
            if predicate(&item) {
                result.push(item);
            }
        }
        return result;
    }

    // ForEach: execute function for each element
    fn each(&self, f: fn(&T)) {
        for i in 0..self.len() {
            f(&self.get(i));
        }
    }

    // Reduce: fold elements into single value
    fn reduce<U>(&self, initial: U, f: fn(U, &T) -> U) -> U {
        var acc = initial;
        for i in 0..self.len() {
            acc = f(acc, &self.get(i));
        }
        return acc;
    }
}
```

**Usage:**

```novus
fn main() {
    var numbers = Vec::<i32>::new();
    numbers.push(1);
    numbers.push(2);
    numbers.push(3);
    numbers.push(4);
    numbers.push(5);

    // Double all numbers
    fn double(n: &i32) -> i32 { return *n * 2; }
    let doubled = numbers.map(double);
    // [2, 4, 6, 8, 10]

    // Keep only even numbers
    fn is_even(n: &i32) -> bool { return *n % 2 == 0; }
    let evens = numbers.filter(is_even);
    // [2, 4]

    // Print each number
    fn print_num(n: &i32) { println(*n); }
    numbers.each(print_num);

    // Sum all numbers
    fn add(acc: i32, n: &i32) -> i32 { return acc + *n; }
    let sum = numbers.reduce(0, add);
    // 15
}
```

### 2. Sort with Custom Comparator

```novus
impl<T> Vec<T> {
    fn sort(&mut self, compare: fn(&T, &T) -> i32) {
        // Bubble sort (simple for now)
        for i in 0..self.len() {
            for j in 0..(self.len() - i - 1) {
                let a = self.get(j);
                let b = self.get(j + 1);
                if compare(&a, &b) > 0 {
                    // Swap
                    self.set(j, b);
                    self.set(j + 1, a);
                }
            }
        }
    }
}

fn compare_ascending(a: &i32, b: &i32) -> i32 {
    return *a - *b;
}

fn compare_descending(a: &i32, b: &i32) -> i32 {
    return *b - *a;
}

fn main() {
    var nums = Vec::<i32>::new();
    nums.push(5);
    nums.push(2);
    nums.push(8);
    nums.push(1);

    nums.sort(compare_ascending);
    // [1, 2, 5, 8]

    nums.sort(compare_descending);
    // [8, 5, 2, 1]
}
```

### 3. Event System (Game/Demo Coding)

```novus
struct EventSystem {
    handlers: Vec<fn()>,
    max_handlers: i32
}

impl EventSystem {
    fn new() -> EventSystem {
        return EventSystem {
            handlers: Vec::new(),
            max_handlers: 16
        };
    }

    fn register(&mut self, handler: fn()) {
        if self.handlers.len() < self.max_handlers {
            self.handlers.push(handler);
        }
    }

    fn trigger(&self) {
        for i in 0..self.handlers.len() {
            let handler = self.handlers.get(i);
            handler();  // Call the registered function
        }
    }
}

fn on_vblank() {
    println("VBlank happened!");
}

fn update_sprites() {
    println("Updating sprites...");
}

fn play_sound() {
    println("Playing sound effect...");
}

fn main() {
    var events = EventSystem::new();
    events.register(on_vblank);
    events.register(update_sprites);
    events.register(play_sound);

    // Simulate frame
    events.trigger();
}
```

### 4. State Machine with Function Pointers

```novus
enum GameState {
    Menu,
    Playing,
    Paused,
    GameOver
}

struct Game {
    state: GameState,
    update_fn: fn(&mut Game),
    render_fn: fn(&Game)
}

fn update_menu(game: &mut Game) {
    println("Menu update");
}

fn update_playing(game: &mut Game) {
    println("Game update");
}

fn render_menu(game: &Game) {
    println("Render menu");
}

fn render_playing(game: &Game) {
    println("Render game");
}

fn main() {
    var game = Game {
        state: GameState::Menu,
        update_fn: update_menu,
        render_fn: render_menu
    };

    // Game loop (simplified)
    for frame in 0..10 {
        game.update_fn(&mut game);
        game.render_fn(&game);

        // Change state after 5 frames
        if frame == 5 {
            game.state = GameState::Playing;
            game.update_fn = update_playing;
            game.render_fn = render_playing;
        }
    }
}
```

---

## Implementation Plan

### Phase 1: Semantic Analysis Enhancement (Already 90% Done!)

The semantic analyzer already handles:
- ✅ Parsing `fn(params) -> return` type syntax
- ✅ Validating function pointer types match
- ✅ Checking indirect calls through function pointers

**What's needed:**
- ✅ Create `IrFunctionAddress` value when referencing function name

**File:** `/Users/barry/RiderProjects/Novus/Novus/SemanticAnalysis/SemanticAnalyzer.cs`

**Changes needed:** When we see a function name used as a value (not a call), create an `IrFunctionAddress`:

```csharp
// In VisitIdentifierExpression or similar:
if (_functions.ContainsKey(identifier) && !isBeingCalled)
{
    var funcDecl = _functions[identifier];
    var funcType = _typeInterner.GetFunctionPointerType(
        funcDecl.Parameters.Select(p => p.Type).ToList(),
        funcDecl.ReturnType
    );
    return new IrFunctionAddress(identifier, funcType);
}
```

### Phase 2: IR Builder Enhancement (Already Done!)

The IR already has:
- ✅ `IrFunctionAddress` value type (line 758-764 in IrModule.cs)
- ✅ `IrIndirectCall` instruction

**Nothing to do here!**

### Phase 3: C Code Generator (Main Work Item)

**File:** `/Users/barry/RiderProjects/Novus/Novus/Codegen/CCodeGenerator.cs`

**Add function pointer type generation:**

```csharp
private string GenerateType(IrType type)
{
    // ... existing cases ...

    if (type is IrFunctionPointerType fpType)
    {
        // Generate C function pointer type
        var returnType = GenerateType(fpType.ReturnType);
        var paramTypes = fpType.ParameterTypes.Count > 0
            ? string.Join(", ", fpType.ParameterTypes.Select(GenerateType))
            : "void";
        return $"{returnType} (*){{{paramTypes}}}";
    }

    // ... rest ...
}
```

**Add indirect call generation:**

```csharp
private void GenerateInstruction(IrIndirectCall indirectCall, StringBuilder sb)
{
    var funcPtr = GenerateValue(indirectCall.FunctionPointer);
    var args = indirectCall.Arguments.Count > 0
        ? string.Join(", ", indirectCall.Arguments.Select(GenerateValue))
        : "";

    if (indirectCall.Result != null)
    {
        sb.AppendLine($"    {indirectCall.Result} = ({funcPtr})({args});");
    }
    else
    {
        sb.AppendLine($"    ({funcPtr})({args});");
    }
}
```

**Add function address generation:**

```csharp
private string GenerateValue(IrValue value)
{
    // ... existing cases ...

    if (value is IrFunctionAddress funcAddr)
    {
        return funcAddr.FunctionName;  // In C, function name IS its address
    }

    // ... rest ...
}
```

### Phase 4: Testing

Create test files:

**`Novus.Tests/Examples/function_pointer_basic.novus`**
```novus
fn add(a: i32, b: i32) -> i32 {
    return a + b;
}

fn apply(a: i32, b: i32, op: fn(i32, i32) -> i32) -> i32 {
    return op(a, b);
}

fn main() -> i32 {
    let result = apply(5, 3, add);
    return result;  // Should return 8
}
```

**`Novus.Tests/Examples/function_pointer_array.novus`**
```novus
fn add(a: i32, b: i32) -> i32 { return a + b; }
fn sub(a: i32, b: i32) -> i32 { return a - b; }

fn main() -> i32 {
    let ops: [2]fn(i32, i32) -> i32 = [add, sub];
    let sum = ops[0](10, 5);     // 15
    let diff = ops[1](10, 5);    // 5
    return sum + diff;            // 20
}
```

**`Novus.Tests/Examples/function_pointer_struct.novus`**
```novus
struct Calculator {
    operation: fn(i32, i32) -> i32
}

fn multiply(a: i32, b: i32) -> i32 {
    return a * b;
}

fn main() -> i32 {
    let calc = Calculator { operation: multiply };
    return calc.operation(6, 7);  // 42
}
```

**`Novus.Tests/Examples/vec_map.novus`**
```novus
// Requires Vec<T>::map implementation
fn double(n: &i32) -> i32 {
    return *n * 2;
}

fn main() -> i32 {
    var nums = Vec::<i32>::new();
    nums.push(1);
    nums.push(2);
    nums.push(3);

    let doubled = nums.map(double);
    return doubled.get(2);  // Should return 6
}
```

---

## Effort Estimate

### Time Required: **4-6 hours total**

**Breakdown:**
1. **Semantic analysis tweaks** - 1 hour
   - Handle function name as value (not call)
   - Create IrFunctionAddress in the right places

2. **C code generator** - 2-3 hours
   - Add IrFunctionPointerType C type generation
   - Add IrIndirectCall code generation
   - Add IrFunctionAddress value generation
   - Handle edge cases (null checks, etc.)

3. **Testing** - 1-2 hours
   - Write 5-6 test cases
   - Debug any issues
   - Verify generated C code is correct

### Complexity: **Low-Medium**

- ✅ Type system already done
- ✅ IR already done
- ✅ Semantic analysis mostly done
- ❌ Code generator needs work (but straightforward)

**Risk: Very Low** - This is well-understood compiler territory. C function pointers are simple.

---

## After This Works

### Immediate Benefits:
1. **Vec methods** - map, filter, each, reduce, sort
2. **Event systems** - Register callbacks for VBlank, input, etc.
3. **State machines** - Function pointer dispatch tables
4. **Custom comparators** - Sort with any comparison function

### Future Enhancements (Optional):

#### 1. Closures (Captured Variables)
```novus
fn make_adder(x: i32) -> fn(i32) -> i32 {
    // NOT supported yet - would need heap allocation for captures
    return fn(y: i32) -> i32 { return x + y; };
}
```

**Lift:** Medium-High (need closure conversion, capture environments)

#### 2. Anonymous Functions (Lambdas)
```novus
let doubled = numbers.map(fn(n: &i32) -> i32 { return *n * 2; });
```

**Lift:** Low-Medium (just syntax sugar for named functions)

#### 3. Method Pointers (Bound Methods)
```novus
struct Counter {
    value: i32
}

impl Counter {
    fn increment(&mut self) { self.value += 1; }
}

let c = Counter { value: 0 };
let callback = c.increment;  // Bound method pointer
```

**Lift:** Medium (need to pass implicit self parameter)

---

## C Code Generation Examples

### Input Novus:
```novus
fn add(a: i32, b: i32) -> i32 {
    return a + b;
}

fn apply(a: i32, b: i32, op: fn(i32, i32) -> i32) -> i32 {
    return op(a, b);
}

fn main() -> i32 {
    return apply(5, 3, add);
}
```

### Generated C:
```c
// novus_types.h
typedef i32 (*fn_i32_i32_to_i32)(i32, i32);

// novus_generated_add.c
i32 add(i32 a, i32 b) {
    return a + b;
}

// novus_generated_apply.c
i32 apply(i32 a, i32 b, fn_i32_i32_to_i32 op) {
    return op(a, b);
}

// novus_generated_main.c
i32 main() {
    return apply(5, 3, add);
}
```

---

## Recommendation

**Start with this BEFORE traits!** Here's why:

1. **Simpler** - Only codegen work needed
2. **Immediately useful** - Vec methods, callbacks, event systems
3. **Foundation for traits** - Traits will need function pointers internally anyway
4. **Swift-style ergonomics** - Matches your goal without the complexity

### Suggested Order:

1. ✅ **Function pointers** (4-6 hours) - Do this first!
2. Add Vec methods (map, filter, each) using function pointers
3. Write game/demo examples with event callbacks
4. *Then* consider traits when you have multiple collection types

---

## Questions for You

1. **Syntax preference:** Implicit (Swift-style) or explicit `&function_name`?
   - My vote: **Implicit** (simpler, cleaner)

2. **Priority:** Should I implement this right away?
   - This is the **highest ROI feature** you could add right now

3. **Scope:** Just basic function pointers, or also add Vec methods immediately?
   - Suggestion: Do both - basic pointers + Vec.map/filter/each

Would you like me to start implementing this?
