---
title: Functions
description: Learn how to define and use functions in Novus
---

Functions are the primary way to organize and reuse code in Novus. This guide covers function syntax, parameters, return values, and best practices.

## Function Basics

A function is defined using the `fn` keyword, followed by a name, parameters in parentheses, an optional return type, and a body in curly braces.

### Basic Function Syntax

```novus
fn greet() {
    // Function with no parameters or return value
}

fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn main() -> i32 {
    let result = add(5, 7)
    return result
}
```

Key components:
- `fn` keyword declares a function
- Parameters: `name: Type`
- Return type: `-> Type` (optional)
- Body: `{ ... }` contains statements

## Parameters

Functions can take zero or more parameters:

```novus
// No parameters
fn say_hello() {
    // ...
}

// Single parameter
fn square(x: i32) -> i32 {
    return x * x
}

// Multiple parameters
fn multiply(a: i32, b: i32) -> i32 {
    return a * b
}

// Different types
fn display_message(count: u32, message: str) {
    // ...
}
```

Each parameter must have an explicit type - no type inference for function parameters.

### Passing Values Without Ownership Transfer

An ordinary parameter does not take ownership. Use this form for primitive,
`Copy`, and small view values:

```novus
fn increment(x: i32) -> i32 {
    return x + 1
}

fn main() -> i32 {
    let value = 10
    let result = increment(value)
    // value is still 10 (unchanged)
    // result is 11
    return 0
}
```

Use `consuming` when the function takes ownership of a non-`Copy` value:

```novus
fn enqueue(consuming job: Job) {
    // job is owned here and cleaned up unless ownership moves again
}

fn main() {
    let job = Job::new()
    enqueue(job)
    // job cannot be used here
}
```

Use `&T` rather than an ordinary value parameter to inspect an owning resource.
The compiler rejects forwarding a non-consuming owner into a consuming call.

### Passing by Reference

To pass data without copying or to allow mutation, use references:

```novus
// Immutable reference (read-only)
fn read_value(x: &i32) -> i32 {
    return *x  // Dereference to read
}

// Mutable reference (can modify)
fn increment_in_place(x: &var i32) {
    *x = *x + 1  // Modify the referenced value
}

fn main() -> i32 {
    var value = 10
    increment_in_place(&var value)
    // value is now 11
    return 0
}
```

Reference parameters:
- `&T` - immutable reference (cannot modify)
- `&var T` - mutable reference (can modify)

## Return Values

Functions can return values using the `return` keyword:

```novus
fn get_answer() -> i32 {
    return 42
}

fn max(a: i32, b: i32) -> i32 {
    if a > b {
        return a
    } else {
        return b
    }
}
```

### Early Returns

You can return early from a function:

```novus
fn check_range(value: i32) -> bool {
    if value < 0 {
        return false  // Early return
    }
    if value > 100 {
        return false  // Early return
    }
    return true
}
```

### Functions Without Return Values

Functions that don't return a value have no return type annotation:

```novus
fn print_greeting(name: str) {
    // No return value
    // Implicitly returns "void"
}

fn do_work() {
    // No return statement needed
}
```

Note: Even though these functions don't return a value, you can still use `return` to exit early:

```novus
fn process(flag: bool) {
    if !flag {
        return  // Early exit
    }
    // Continue processing...
}
```

## Function Visibility

By default, functions are private to their module. Use `pub` to make them public:

```novus
// Public function - visible to other modules
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}

// Private function - only visible in this module
fn helper() {
    // ...
}

// Internal function - visible within the same project
internal fn project_helper() {
    // ...
}
```

## The Main Function

Every executable Novus program must have a `main` function:

```novus
pub fn main() -> i32 {
    // Program entry point
    return 0  // Exit code
}
```

The `main` function:
- Must be `pub`
- Returns `i32` (exit code)
- `0` typically indicates success
- Non-zero values indicate errors

## Function Examples

### Simple Calculator Functions

```novus
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

fn subtract(a: i32, b: i32) -> i32 {
    return a - b
}

fn multiply(a: i32, b: i32) -> i32 {
    return a * b
}

fn divide(a: i32, b: i32) -> i32 {
    if b == 0 {
        return 0  // Avoid division by zero
    }
    return a / b
}

pub fn main() -> i32 {
    let sum = add(10, 20)
    let diff = subtract(30, 15)
    let product = multiply(4, 5)
    let quotient = divide(100, 4)

    return 0
}
```

### Functions with Multiple Parameters

```novus
fn calculate_area(width: u16, height: u16) -> u32 {
    return (u32)width * (u32)height
}

fn in_range(value: i32, min: i32, max: i32) -> bool {
    return value >= min && value <= max
}

fn format_color(r: u8, g: u8, b: u8) -> u32 {
    return ((u32)r << 16) | ((u32)g << 8) | (u32)b
}
```

### Functions with References

```novus
struct Point {
    x: i32,
    y: i32,
}

// Read-only access
fn distance_from_origin(p: &Point) -> i32 {
    let dx = p.x
    let dy = p.y
    return dx * dx + dy * dy  // Simplified distance
}

// Modify in place
fn move_point(p: &var Point, dx: i32, dy: i32) {
    p.x = p.x + dx
    p.y = p.y + dy
}

pub fn main() -> i32 {
    var point = Point { x: 10, y: 20 }

    let dist = distance_from_origin(&point)
    move_point(&var point, 5, -3)
    // point is now (15, 17)

    return 0
}
```

### Conditional Logic in Functions

```novus
fn absolute(x: i32) -> i32 {
    if x < 0 {
        return -x
    } else {
        return x
    }
}

fn clamp(value: i32, min: i32, max: i32) -> i32 {
    if value < min {
        return min
    }
    if value > max {
        return max
    }
    return value
}

fn sign(x: i32) -> i32 {
    if x > 0 {
        return 1
    } else {
        if x < 0 {
            return -1
        } else {
            return 0
        }
    }
}
```

## Function Composition

Functions can call other functions:

```novus
fn double(x: i32) -> i32 {
    return x * 2
}

fn triple(x: i32) -> i32 {
    return x * 3
}

fn complex_calculation(x: i32) -> i32 {
    let doubled = double(x)
    let tripled = triple(x)
    return doubled + tripled  // 5x
}

pub fn main() -> i32 {
    let result = complex_calculation(10)  // 50
    return 0
}
```

## Recursion

Functions can call themselves recursively:

```novus
fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}

fn fibonacci(n: i32) -> i32 {
    if n <= 1 {
        return n
    }
    return fibonacci(n - 1) + fibonacci(n - 2)
}

pub fn main() -> i32 {
    let fact5 = factorial(5)    // 120
    let fib7 = fibonacci(7)     // 13
    return 0
}
```

Be careful with recursion on limited stack space (especially on Amiga 68k).

## Working with Structs

Functions commonly work with struct types:

```novus
struct Rectangle {
    width: u16,
    height: u16,
}

fn create_rectangle(w: u16, h: u16) -> Rectangle {
    return Rectangle { width: w, height: h }
}

fn area(rect: &Rectangle) -> u32 {
    return (u32)rect.width * (u32)rect.height
}

fn perimeter(rect: &Rectangle) -> u32 {
    return 2u32 * ((u32)rect.width + (u32)rect.height)
}

fn is_square(rect: &Rectangle) -> bool {
    return rect.width == rect.height
}

pub fn main() -> i32 {
    let rect = create_rectangle(320, 200)
    let a = area(&rect)
    let p = perimeter(&rect)
    let square = is_square(&rect)

    return 0
}
```

## Generic Functions (Preview)

Novus supports generic functions that work with multiple types:

```novus
fn max<T>(consuming a: T, consuming b: T) -> T where T: Ord {
    if a > b {
        return a
    } else {
        return b
    }
}

pub fn main() -> i32 {
    let max_int = max(10, 20)
    let max_byte = max(100u8, 200u8)
    return 0
}
```

Generics allow writing code once that works with many types. We'll cover this in depth in the Advanced chapter.

## Best Practices

1. **Keep functions small and focused**: Each function should do one thing well
2. **Use descriptive names**: Function names should describe what they do
3. **Prefer immutable parameters**: Use `&T` instead of `&var T` when possible
4. **Avoid side effects**: Pure functions are easier to test and reason about
5. **Document complex functions**: Add comments explaining what the function does
6. **Return early for error cases**: Handle edge cases at the start of the function
7. **Make ownership visible**: Borrow with `&T`/`&var T`; transfer with `consuming`

## Common Patterns

### Factory Functions

```novus
struct Color {
    r: u8,
    g: u8,
    b: u8,
}

fn red() -> Color {
    return Color { r: 255, g: 0, b: 0 }
}

fn green() -> Color {
    return Color { r: 0, g: 255, b: 0 }
}

fn blue() -> Color {
    return Color { r: 0, g: 0, b: 255 }
}
```

### Builder/Modifier Functions

```novus
fn with_alpha(c: Color, a: u8) -> Color {
    // Returns a new color with alpha
    return Color { r: c.r, g: c.g, b: c.b }
}

fn darken(c: Color) -> Color {
    return Color {
        r: c.r / 2,
        g: c.g / 2,
        b: c.b / 2,
    }
}
```

### Validation Functions

```novus
fn is_valid_coordinate(x: i32, y: i32, width: u16, height: u16) -> bool {
    if x < 0 || y < 0 {
        return false
    }
    if x >= (i32)width || y >= (i32)height {
        return false
    }
    return true
}
```

## Coming from C

Key differences from C:

| C | Novus |
|---|-------|
| `int add(int a, int b)` | `fn add(a: i32, b: i32) -> i32` |
| `void foo(void)` | `fn foo()` |
| `int* ptr` parameter | `ptr: *i32` parameter |
| Pass by pointer: `func(&x)` | Shared borrow: `func(&x)` |
| Ownership implied by convention | Ownership transfer: `fn take(consuming value: T)` |
| `return;` (void function) | `return` or no return |
| Function pointers | Function pointers (similar syntax) |

Key points:
- Use `fn` keyword, not return type before name
- Types come after parameter names: `a: i32`
- Return type comes after arrow: `-> i32`
- No `void` keyword - just omit return type
- References (`&T`) are safer than C pointers
- `&var T` is the only mutable borrow; call it with `&var value`
- `consuming` makes ownership transfer explicit in the signature
