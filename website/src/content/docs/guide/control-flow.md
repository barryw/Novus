---
title: Control Flow
description: Learn about control flow constructs in Novus including conditionals, loops, and pattern matching
---

Control flow determines the order in which code executes. Novus provides several constructs for branching and looping, including if/else, while, for, loop, and pattern matching.

## Conditional Statements

### If Expressions

The `if` statement executes code conditionally:

```novus
let x = 10

if x > 5 {
    // This block executes
}

if x < 0 {
    // This block doesn't execute
}
```

Conditions must be boolean expressions - no implicit conversions from integers.

### If-Else

Add an `else` clause for alternative execution:

```novus
let age = 25

if age >= 18 {
    // Adult path
} else {
    // Minor path
}
```

### Else-If Chains

Chain multiple conditions:

```novus
let score = 85

if score >= 90 {
    // Grade A
} else {
    if score >= 80 {
        // Grade B
    } else {
        if score >= 70 {
            // Grade C
        } else {
            // Below C
        }
    }
}
```

### If as an Expression

In Novus, `if` can be used as an expression that returns a value:

```novus
let x = 10
let result = if x > 5 {
    return 100
} else {
    return 200
}
// result is 100
```

When using `if` as an expression, both branches must return the same type.

### If-Let for Pattern Matching

The `if let` syntax tests if a value matches a pattern:

```novus
let ptr: *u8 = (*u8)100

if let p = ptr {
    // p is bound to ptr (non-null)
    // This block executes
} else {
    // Null case
}

// With integers (non-zero check)
let x: u32 = 42
if let y = x {
    // y is bound to x (non-zero)
} else {
    // Zero case
}
```

## Loops

Novus provides several loop constructs:

### While Loops

Execute code while a condition is true:

```novus
var count = 0
while count < 10 {
    count++
}
// count is now 10
```

The condition is checked before each iteration:

```novus
var x = 5
while x > 0 {
    x--
}
// x is now 0
```

### Forever Loops

Use `forever` for infinite loops (must break explicitly):

```novus
var count = 0
forever {
    count++
    if count >= 10 {
        break
    }
}
```

The `forever` keyword creates an infinite loop that must use `break` to exit.

### For Loops (While-Based)

Novus uses while loops for iteration. Here's the idiomatic pattern:

```novus
// Sum numbers 0 to 9
var sum = 0
var i = 0
while i < 10 {
    sum = sum + i
    i++
}
// sum is 45
```

With non-unit increment:

```novus
var sum = 0
var i = 0
while i < 10 {
    sum = sum + i
    i = i + 2  // Increment by 2
}
// sum is 20 (0+2+4+6+8)
```

Counting down:

```novus
var countdown = 0
var i = 10
while i > 0 {
    countdown = countdown + i
    i--
}
// countdown is 55 (10+9+8+...+1)
```

### Break Statement

Exit a loop early:

```novus
var i = 0
while i < 100 {
    if i == 10 {
        break  // Exit when i reaches 10
    }
    i++
}
// i is 10
```

### Continue Statement

Skip to the next iteration:

```novus
var sum = 0
var i = 0
while i < 10 {
    i++
    if i % 2 == 0 {
        continue  // Skip even numbers
    }
    sum = sum + i
}
// sum is 25 (1+3+5+7+9)
```

### Nested Loops

Loops can be nested:

```novus
var total = 0
var x = 0
while x < 3 {
    var y = 0
    while y < 3 {
        total++
        y++
    }
    x++
}
// total is 9 (3×3)
```

### Labeled Loops

Break from specific loops using labels:

```novus
'outer: forever {
    'inner: forever {
        break 'outer  // Breaks from outer loop
    }
}
```

Labels use single quotes and can be applied to any loop.

## Pattern Matching

Pattern matching is a powerful way to handle different cases:

### Match Expressions

The `match` expression compares a value against patterns:

```novus
enum Status {
    Success,
    Pending,
    Error(i32),
}

fn get_code(status: Status) -> i32 {
    match status {
        Status::Success => return 0,
        Status::Pending => return 1,
        Status::Error(code) => return code
    }
}
```

Each arm of the match has:
- A pattern (left of `=>`)
- An expression or block (right of `=>`)

### Match with Block Bodies

Arms can have block bodies:

```novus
fn process(status: Status) -> i32 {
    match status {
        Status::Success => {
            // Multiple statements
            return 10
        },
        Status::Pending => {
            return 5
        },
        Status::Error(code) => {
            return 100 + code
        }
    }
}
```

### Matching Integer Values

Match on integer literals:

```novus
fn day_name(day: i32) -> str {
    match day {
        1 => return "Monday",
        2 => return "Tuesday",
        3 => return "Wednesday",
        4 => return "Thursday",
        5 => return "Friday",
        6 => return "Saturday",
        7 => return "Sunday",
        _ => return "Invalid"
    }
}
```

The `_` pattern matches anything (wildcard).

### Destructuring in Patterns

Extract values from enum variants:

```novus
enum Result<T, E> {
    Ok(T),
    Err(E),
}

fn handle_result(r: Result<i32, str>) -> i32 {
    match r {
        Result::Ok(value) => return value,
        Result::Err(msg) => {
            // msg is the error message
            return -1
        }
    }
}
```

### Match is Exhaustive

The compiler ensures all cases are covered:

```novus
enum Color {
    Red,
    Green,
    Blue,
}

fn color_code(c: Color) -> i32 {
    match c {
        Color::Red => return 0,
        Color::Green => return 1,
        // ERROR: Missing Color::Blue case!
    }
}
```

Add all cases or use `_` to catch remaining values.

## Loop Examples

### Simple Counter

```novus
pub fn main() -> i32 {
    var count = 0
    while count < 5 {
        count++
    }
    return count  // Returns 5
}
```

### Sum Array Elements

```novus
pub fn main() -> i32 {
    let numbers = [1, 2, 3, 4, 5]
    var sum = 0
    var i = 0
    while i < 5 {
        sum = sum + numbers[i]
        i++
    }
    return sum  // Returns 15
}
```

### Find First Match

```novus
fn find_positive(values: [i32; 10]) -> i32 {
    var i = 0
    while i < 10 {
        if values[i] > 0 {
            return values[i]  // Return first positive
        }
        i++
    }
    return -1  // Not found
}
```

### Double Loop for 2D Grid

```novus
pub fn main() -> i32 {
    var total = 0
    var y = 0
    while y < 8 {
        var x = 0
        while x < 8 {
            total++
            x++
        }
        y++
    }
    return total  // Returns 64 (8×8)
}
```

## Control Flow Best Practices

1. **Keep conditions simple**: Complex boolean expressions are hard to read
2. **Use early returns**: Handle edge cases first to reduce nesting
3. **Prefer pattern matching**: Use `match` instead of long if-else chains
4. **Use `break` sparingly**: Consider restructuring code to avoid it
5. **Label nested loops**: Makes break/continue intent clearer
6. **Avoid deep nesting**: Extract nested logic into separate functions

## Complex Example

Combining multiple control flow constructs:

```novus
enum Command {
    Move(i32, i32),
    Stop,
    Reset,
}

fn process_commands(commands: [Command; 10], count: u32) -> i32 {
    var x = 0
    var y = 0
    var i = 0u32

    while i < count {
        let cmd = commands[i]

        match cmd {
            Command::Move(dx, dy) => {
                x = x + dx
                y = y + dy

                // Boundary check
                if x < 0 || x > 100 {
                    break  // Out of bounds
                }
                if y < 0 || y > 100 {
                    break
                }
            },
            Command::Stop => {
                break  // Stop processing
            },
            Command::Reset => {
                x = 0
                y = 0
            }
        }

        i++
    }

    return x + y
}
```

## Common Patterns

### Guard Clauses

Handle error cases early:

```novus
fn process(value: i32) -> i32 {
    if value < 0 {
        return -1  // Early exit for invalid input
    }
    if value > 100 {
        return -1  // Early exit for out of range
    }

    // Main logic here
    return value * 2
}
```

### State Machine

Use enums and match for state transitions:

```novus
enum State {
    Idle,
    Running,
    Paused,
    Stopped,
}

fn update_state(current: State, input: i32) -> State {
    match current {
        State::Idle => {
            if input == 1 {
                return State::Running
            }
            return State::Idle
        },
        State::Running => {
            if input == 2 {
                return State::Paused
            }
            if input == 3 {
                return State::Stopped
            }
            return State::Running
        },
        State::Paused => {
            if input == 1 {
                return State::Running
            }
            return State::Paused
        },
        State::Stopped => {
            return State::Stopped
        }
    }
}
```

### Range Checking with Match

```novus
fn categorize_age(age: i32) -> str {
    if age < 0 {
        return "Invalid"
    }
    if age < 13 {
        return "Child"
    }
    if age < 20 {
        return "Teen"
    }
    if age < 65 {
        return "Adult"
    }
    return "Senior"
}
```

## Coming from C

Key differences from C:

| C | Novus |
|---|-------|
| `if (x > 0)` | `if x > 0` (no parentheses required) |
| `while (x < 10)` | `while x < 10` |
| `for (i=0; i<10; i++)` | `var i = 0 while i < 10 { ... i++ }` |
| `while (1)` or `for (;;)` | `forever` |
| `switch` statement | `match` expression |
| `break;` | `break` |
| `continue;` | `continue` |

Key points:
- No parentheses around conditions (but braces required for bodies)
- `forever` keyword instead of `while(1)`
- `match` is more powerful than `switch` (exhaustiveness checking, destructuring)
- No fall-through in `match` (each arm must explicitly return or break)
