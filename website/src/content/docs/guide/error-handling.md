---
title: Error Handling
description: Learn how to handle errors in Novus using Result, Option, and the ? operator
---

Novus uses explicit error handling through types rather than exceptions. This makes errors visible in function signatures and forces you to handle them intentionally.

## The Problem with Null

In many languages, `null` or `NULL` represents the absence of a value. This leads to null pointer errors, which are a major source of bugs:

```c
// C example - dangerous!
char* find_user(int id) {
    // Returns NULL if not found
    return NULL;
}

// Easy to forget null check!
char* user = find_user(42);
printf("%s\n", user);  // CRASH if NULL!
```

Novus solves this by making absence explicit in the type system.

## Option[T]: Representing Optional Values

`Option[T]` represents a value that may or may not be present:

```novus
enum Option<T> {
    Some(T),    // Contains a value
    None,       // No value present
}
```

### Creating Option Values

```novus
let has_value: Option<i32> = Option::Some(42)
let no_value: Option<i32> = Option::None
```

### Using Option with Pattern Matching

The safest way to use `Option` is with pattern matching:

```novus
fn process_value(opt: Option<i32>) -> i32 {
    match opt {
        Option::Some(value) => {
            // We have a value
            return value * 2
        },
        Option::None => {
            // No value
            return 0
        }
    }
}
```

### Example: Safe Array Access

```novus
fn get_element(arr: [i32; 10], index: u32) -> Option<i32> {
    if index >= 10 {
        return Option::None
    }
    return Option::Some(arr[index])
}

pub fn main() -> i32 {
    let numbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

    let result = get_element(numbers, 5)
    match result {
        Option::Some(value) => return value,
        Option::None => return -1
    }
}
```

## Result[T, E]: Representing Success or Failure

`Result[T, E]` represents an operation that can succeed with a value or fail with an error:

```novus
enum Result<T, E> {
    Ok(T),      // Success with value
    Err(E),     // Failure with error
}
```

### Creating Result Values

```novus
fn divide(a: i32, b: i32) -> Result<i32, str> {
    if b == 0 {
        return Result::Err("Division by zero")
    }
    return Result::Ok(a / b)
}
```

### Using Result with Pattern Matching

```novus
pub fn main() -> i32 {
    let result = divide(10, 2)

    match result {
        Result::Ok(value) => {
            // Success - value is 5
            return value
        },
        Result::Err(msg) => {
            // Error - handle it
            return -1
        }
    }
}
```

## The ? Operator: Error Propagation

The `?` operator automatically propagates errors up the call stack, making error handling concise:

```novus
from std::core import Result
from std::error::errors import DosError

fn open_file(valid: bool) -> Result<i32, DosError> {
    if !valid {
        return Result::Err(DosError::NotFound)
    }
    return Result::Ok(42)  // File handle
}

fn read_file(handle: i32) -> Result<i32, DosError> {
    if handle <= 0 {
        return Result::Err(DosError::NoFreeStore)
    }
    return Result::Ok(1024)  // Bytes read
}

fn close_file(handle: i32) -> Result<i32, DosError> {
    if handle <= 0 {
        return Result::Err(DosError::InvalidInput)
    }
    return Result::Ok(0)
}
```

### Without the ? Operator (Verbose)

```novus
fn process_file_verbose(is_valid: bool) -> Result<i32, DosError> {
    let handle_result = open_file(is_valid)
    let handle = match handle_result {
        Result::Ok(h) => h,
        Result::Err(e) => return Result::Err(e)
    }

    let bytes_result = read_file(handle)
    let bytes = match bytes_result {
        Result::Ok(b) => b,
        Result::Err(e) => return Result::Err(e)
    }

    let close_result = close_file(handle)
    match close_result {
        Result::Ok(_) => {},
        Result::Err(e) => return Result::Err(e)
    }

    return Result::Ok(bytes)
}
```

### With the ? Operator (Concise)

```novus
fn process_file(is_valid: bool) -> Result<i32, DosError> {
    // ? automatically returns on error
    let handle = open_file(is_valid)?
    let bytes = read_file(handle)?
    let _close_status = close_file(handle)?

    return Result::Ok(bytes)
}
```

The `?` operator:
1. Unwraps the value if `Ok`
2. Returns immediately with `Err` if an error occurs
3. Only works in functions that return `Result`

### Using ? in Expressions

```novus
fn get_file_size(is_valid: bool) -> Result<i32, DosError> {
    let handle = open_file(is_valid)?
    let bytes = read_file(handle)?

    // Can use ? in expressions
    return Result::Ok(bytes + 100)
}
```

## Unwrapping: Use Sparingly

`unwrap()` extracts the value from `Option` or `Result`, but panics if there's no value or an error:

```novus
let value: Option<i32> = Option::Some(42)
let x = value.unwrap()  // x = 42

let nothing: Option<i32> = Option::None
let y = nothing.unwrap()  // PANIC! Program crashes
```

**Only use `unwrap()` when:**
- You're absolutely certain the value exists
- You're prototyping and will add proper error handling later
- In test code where panic is acceptable

## Common Error Handling Patterns

### Guard Clauses with Early Return

```novus
fn process(value: Option<i32>) -> i32 {
    // Early return on None
    if value == Option::None {
        return -1
    }

    // Continue with Some case
    match value {
        Option::Some(v) => return v * 2,
        Option::None => return -1  // Won't reach here
    }
}
```

### Chaining Operations

```novus
fn validate_age(age: i32) -> Result<i32, str> {
    if age < 0 {
        return Result::Err("Age cannot be negative")
    }
    if age > 150 {
        return Result::Err("Age too large")
    }
    return Result::Ok(age)
}

fn calculate_discount(age: i32) -> Result<i32, str> {
    let validated_age = validate_age(age)?

    if validated_age < 18 {
        return Result::Ok(50)  // 50% discount for children
    }
    if validated_age >= 65 {
        return Result::Ok(30)  // 30% discount for seniors
    }
    return Result::Ok(0)  // No discount
}
```

### Providing Default Values

```novus
fn get_config_value(key: str) -> Option<i32> {
    // Simulate config lookup
    return Option::None
}

pub fn main() -> i32 {
    let timeout = get_config_value("timeout")

    // Provide default if None
    let actual_timeout = match timeout {
        Option::Some(t) => t,
        Option::None => 30  // Default: 30 seconds
    }

    return actual_timeout
}
```

### Converting Between Option and Result

```novus
fn option_to_result(opt: Option<i32>) -> Result<i32, str> {
    match opt {
        Option::Some(value) => return Result::Ok(value),
        Option::None => return Result::Err("No value")
    }
}

fn result_to_option(res: Result<i32, str>) -> Option<i32> {
    match res {
        Result::Ok(value) => return Option::Some(value),
        Result::Err(_) => return Option::None
    }
}
```

## Working with AmigaOS Errors

Novus standard library wraps AmigaOS calls in `Result` types:

```novus
from std::core import Result
from std::error::errors import DosError

fn read_config_file(path: str) -> Result<i32, DosError> {
    // DOS Open() returns Result<FileHandle, DosError>
    let file = dos::Open(path, MODE_OLDFILE)?

    // ReadLine() returns Result<String, DosError>
    let line = dos::ReadLine(file)?

    // Close() returns Result<(), DosError>
    dos::Close(file)?

    return Result::Ok(0)
}
```

Common AmigaOS error types:
- `DosError::NotFound` - File not found
- `DosError::NoFreeStore` - Out of memory
- `DosError::InvalidInput` - Invalid parameter
- `ExecError::NoMemory` - Exec memory allocation failed
- `GraphicsError::NoColorMap` - Graphics error

## Best Practices

1. **Use `Result` for recoverable errors**: Operations that can fail should return `Result`
2. **Use `Option` for absence**: When a value might not exist, use `Option`
3. **Prefer pattern matching**: It's explicit and exhaustive
4. **Use `?` for propagation**: Simplifies error handling in function chains
5. **Avoid `unwrap()` in production**: Only use when you're certain or for prototyping
6. **Make errors descriptive**: Error messages should help debugging
7. **Don't ignore errors**: Every `Result` should be handled

## Complete Example

```novus
from std::core import Result, Option
from std::error::errors import DosError

struct Config {
    width: u16,
    height: u16,
    depth: u8,
}

fn parse_number(s: str) -> Option<u16> {
    // Simplified parser
    if s == "320" {
        return Option::Some(320)
    }
    if s == "640" {
        return Option::Some(640)
    }
    return Option::None
}

fn load_config(path: str) -> Result<Config, DosError> {
    // Simulate file I/O
    let width_str = "320"
    let height_str = "200"

    // Parse values
    let width = match parse_number(width_str) {
        Option::Some(w) => w,
        Option::None => return Result::Err(DosError::InvalidInput)
    }

    let height = match parse_number(height_str) {
        Option::Some(h) => h,
        Option::None => return Result::Err(DosError::InvalidInput)
    }

    return Result::Ok(Config {
        width: width,
        height: height,
        depth: 5,
    })
}

pub fn main() -> i32 {
    let config_result = load_config("S:screen.config")

    match config_result {
        Result::Ok(config) => {
            // Successfully loaded configuration
            return 0
        },
        Result::Err(err) => {
            // Handle error
            return 1
        }
    }
}
```

## Common Pitfalls

### Ignoring Errors

```novus
// BAD - ignoring Result
let _ = open_file(true)  // Error is lost!

// GOOD - handle or propagate
let file = open_file(true)?
```

### Over-using unwrap()

```novus
// BAD - will panic on error
let value = might_fail().unwrap()

// GOOD - handle explicitly
let value = match might_fail() {
    Result::Ok(v) => v,
    Result::Err(_) => return -1
}
```

### Not propagating errors

```novus
// BAD - converting errors to magic values
fn read_number() -> i32 {
    match parse_number("text") {
        Option::Some(n) => return n,
        Option::None => return -1  // Loses error information
    }
}

// GOOD - propagate as Result
fn read_number() -> Result<i32, str> {
    match parse_number("text") {
        Option::Some(n) => return Result::Ok(n),
        Option::None => return Result::Err("Invalid number")
    }
}
```

## Coming from C

C error handling typically uses:
- Return codes (0 = success, negative = error)
- Global `errno` variable
- NULL pointers for failure
- Out parameters for values

Novus error handling:
- Uses types (`Result`, `Option`)
- Errors are values, not codes
- No NULL pointers
- Compiler forces error handling

| C | Novus |
|---|-------|
| `int fd = open(...); if (fd < 0) { ... }` | `let fd = Open(...)? // Result` |
| `char* p = malloc(...); if (!p) { ... }` | `let p = alloc()? // Result` |
| `if (errno == ENOENT) { ... }` | `match err { DosError::NotFound => ... }` |
| Function returns value or NULL | Function returns `Option<T>` |
| Function returns -1 on error | Function returns `Result<T, E>` |

Key advantages:
- Errors are visible in function signatures
- Compiler ensures errors are handled
- No forgotten error checks
- No NULL pointer crashes
