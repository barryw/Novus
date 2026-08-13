---
title: Language Reference
description: Complete reference for the Novus programming language syntax and semantics
---

This document provides a comprehensive reference for the Novus programming language. For tutorials and guides, see the [Getting Started](/getting-started/hello-world) section.

## Lexical Structure

### Comments

Novus supports two comment styles:

```novus
// Single-line comment

/*
   Multi-line comment
   can span multiple lines
*/
```

Multi-line comments end at the first `*/`:

```novus
/* This is a comment */
```

### Identifiers

Identifiers are used for variable names, function names, type names, and other named entities.

**Rules:**
- Must start with a letter (`a-z`, `A-Z`) or underscore (`_`)
- Can contain letters, digits (`0-9`), and underscores
- Case-sensitive: `myVar` and `MyVar` are different identifiers
- Cannot be a reserved keyword

**Examples:**
```novus
counter
_temp
MyStruct
get_value
parse2JSON
```

### Reserved Keywords

The following keywords are reserved and cannot be used as identifiers:

```novus
as          at          assert      blitter     bool        break
closure     const       consuming   continue    copper      defer
drop_in_place          else        enum        extern      f32
f64         false       fixed16     fixed32     fn          for
forever     from        i8          i16         i32         i64
if          impl        import      in          internal    let
match       null        offsetof    panic       pub         return
self        Self        sizeof      static      struct      trait
true        u8          u16         u32         u64         unless
unsafe      use         using       var         volatile    where
while       zeroed
```

**Macro-like keywords:**
- `dbg!` - debug print
- `matches!` - pattern matching test
- `unreachable!` - mark unreachable code

**Inline assembly keywords:**
- `asm` - inline assembly block
- `clobbers` - specify clobbered registers

## Literals

### Integer Literals

Integer literals can be written in decimal, binary, or hexadecimal notation:

```novus
// Decimal
42
1000
-50

// Binary (% prefix)
%10101010
%1111_0000  // underscores for readability

// Hexadecimal ($ or 0x prefix)
$FF
$DEAD_BEEF
0xFF
0xDEADBEEF
```

The surrounding expression normally determines the type. Use a cast only when no useful context exists:
```novus
let byte: u8 = 42
let signed = (i16)(-50)
let count = (u32)1000
let mask = (i64)0xFFFF
```

### Floating-Point Literals

Floating-point literals use decimal notation with a decimal point:

```novus
3.14
0.5
2.0
-1.5
```

Floating-point and fixed-point literals follow the same rule:
```novus
let single: f32 = 3.14
let double = (f64)2.0
let position: fixed16 = 1.5
let precise = (fixed32)2.5
```

### Character Literals

Character literals represent a single Unicode character enclosed in single quotes:

```novus
'a'
'Z'
'0'
'$'
```

**Escape sequences:**
```novus
'\n'    // newline
'\r'    // carriage return
'\t'    // tab
'\0'    // null character
'\\'    // backslash
'\''    // single quote
'\"'    // double quote
'\x41'  // hexadecimal byte (A)
```

### String Literals

String literals are sequences of characters enclosed in double quotes:

```novus
"Hello, Amiga!"
"Path: /Work/MyProject"
""  // empty string
```

**Escape sequences:**
```novus
"Line 1\nLine 2"           // newline
"Column\tColumn"           // tab
"Quote: \"Hello\""         // escaped quote
"Backslash: \\"            // escaped backslash
"Hex byte: \x41\x42\x43"   // ABC
```

**Formatted string literals:**
```novus
let name = "World"
let count = 42
f"Hello, {name}! Count: {count}"  // "Hello, World! Count: 42"
```

### Boolean Literals

```novus
true
false
```

### Null Literal

```novus
null
```

## Primitive Types

Novus provides a comprehensive set of primitive types optimized for the Motorola 68000 architecture:

| Type | Size | Range | Description |
|------|------|-------|-------------|
| `u8` | 1 byte | 0 to 255 | Unsigned 8-bit integer |
| `u16` | 2 bytes | 0 to 65,535 | Unsigned 16-bit integer |
| `u32` | 4 bytes | 0 to 4,294,967,295 | Unsigned 32-bit integer |
| `u64` | 8 bytes | 0 to 18,446,744,073,709,551,615 | Unsigned 64-bit integer |
| `i8` | 1 byte | -128 to 127 | Signed 8-bit integer |
| `i16` | 2 bytes | -32,768 to 32,767 | Signed 16-bit integer |
| `i32` | 4 bytes | -2,147,483,648 to 2,147,483,647 | Signed 32-bit integer |
| `i64` | 8 bytes | -9,223,372,036,854,775,808 to 9,223,372,036,854,775,807 | Signed 64-bit integer |
| `bool` | 1 byte | `true` or `false` | Boolean type |
| `f32` | 4 bytes | IEEE 754 single-precision | 32-bit floating-point |
| `f64` | 8 bytes | IEEE 754 double-precision | 64-bit floating-point |
| `fixed16` | 4 bytes | 16.16 fixed-point | Fixed-point arithmetic |
| `fixed32` | 8 bytes | 32-bit fixed-point | High-precision fixed-point |

**Examples:**
```novus
let byte: u8 = 255
let offset: i16 = -100
let address: u32 = $DFF000
let flag: bool = true
let pi: f32 = 3.14159
let angle: fixed16 = 1.5
```

## Compound Types

### Arrays

Fixed-size arrays with compile-time known length:

```novus
let numbers: [i32] = [1, 2, 3, 4, 5]  // size inferred from elements
let buffer: [u8; 256]  // uninitialized (size required)
let colors: [u8] = [255, 0, 0]  // RGB red
```

**Array access:**
```novus
let first = numbers[0]
numbers[2] = 99
```

### Tuples

Fixed-size heterogeneous collections:

```novus
let point: (i16, i16) = (100, 200)
let rgb: (u8, u8, u8) = (255, 128, 0)
let mixed: (i32, bool, u8) = (42, true, 255)
```

**Tuple destructuring:**
```novus
let (x, y) = point
```

### Slices

Dynamic views into arrays or sequences:

```novus
let numbers = [1, 2, 3, 4, 5]
let slice: Slice<i32> = &numbers
let first = slice.get(0)
```

`Slice<T>.get` is bounds checked and returns `Option<&T>`.

### Pointers

Raw pointers for low-level memory access:

```novus
let ptr: *u8 = unsafe { (*u8)&value }
let mut_ptr: *u8 = unsafe { (*u8)&var value }
```

**Pointer operations require `unsafe` blocks.**

### References

Safe borrowed references:

```novus
let ref: &i32 = &value        // immutable reference
let mut_ref: &var i32 = &var value  // mutable reference
```

## User-Defined Types

### Structs

Structures define custom data types with named fields:

```novus
pub struct Point {
    x: i16,
    y: i16,
}

pub struct Color {
    r: u8,
    g: u8,
    b: u8,
}
```

Visibility is specified at the struct level, not per-field.

**Creating instances:**
```novus
let origin = Point { x: 0, y: 0 }
let red = Color { r: 255, g: 0, b: 0 }
```

**Field access:**
```novus
let x_coord = origin.x
origin.y = 100
```

### Enums

Enumerations define types with a fixed set of variants:

```novus
pub enum Direction {
    North,
    South,
    East,
    West
}

pub enum Result<T, E> {
    Ok(T),
    Err(E)
}

pub enum Option<T> {
    Some(T),
    None
}
```

**Pattern matching with enums:**
```novus
match direction {
    Direction::North => move_up(),
    Direction::South => move_down(),
    Direction::East => move_right(),
    Direction::West => move_left()
}
```

### Traits

Traits define shared behavior across types:

```novus
pub trait Draw {
    fn draw(&self)
}

pub trait Printable {
    fn to_string(&self) -> String
}
```

**Trait implementations:**
```novus
impl Draw for Circle {
    fn draw(&self) {
        // implementation
    }
}
```

## Variables and Bindings

### Let Bindings

Immutable bindings with `let`:

```novus
let x = 42
let name = "Amiga"
let point = Point { x: 10, y: 20 }
```

### Mutable Variables

Mutable variables with `var`:

```novus
var counter = 0
counter = counter + 1

var buffer: [u8; 64] = @zeroed([u8; 64])
buffer[0] = 255
```

### Type Annotations

Explicit type annotations:

```novus
let x: i32 = 42
var count: u16 = 0
let result: Result<i32, Error> = get_value()
```

### Shadowing

Variables can be shadowed in inner scopes:

```novus
let x = 5
{
    let x = 10  // shadows outer x
    // x is 10 here
}
// x is 5 here
```

## Control Flow

### If Expressions

```novus
if condition {
    // code
}

if x > 0 {
    positive()
} else {
    negative_or_zero()
}

if x < 0 {
    negative()
} else if x == 0 {
    zero()
} else {
    positive()
}
```

**If as an expression:**
```novus
let sign = if x >= 0 { 1 } else { -1 }
```

### Match Expressions

Pattern matching for complex control flow:

```novus
match value {
    0 => "zero",
    1 => "one",
    2 | 3 => "two or three",
    _ => "other"
}

match result {
    Result::Ok(val) => process(val),
    Result::Err(e) => handle_error(e)
}
```

### While Loops

```novus
while condition {
    // loop body
}

let var i = 0
while i < 10 {
    process(i)
    i = i + 1
}
```

### For Loops

Iterate over ranges:

```novus
for i in 0..10 {
    // i goes from 0 to 9
}

for i in 0..=10 {
    // i goes from 0 to 10 (inclusive)
}
```

### Forever Loops

Infinite loops:

```novus
forever {
    // infinite loop
    if should_exit {
        break
    }
}
```

### Loop Control

```novus
break     // exit loop
continue  // skip to next iteration
```

## Functions

### Function Definitions

```novus
fn add(a: i32, b: i32) -> i32 {
    return a + b
}

pub fn greet(name: String) {
    print(f"Hello, {name}!")
}
```

### Return Values

Functions implicitly return the last expression:

```novus
fn square(x: i32) -> i32 {
    x * x  // implicit return
}

fn abs(x: i32) -> i32 {
    if x < 0 {
        -x
    } else {
        x
    }
}
```

### Multiple Return Values

Use tuples for multiple return values:

```novus
fn div_mod(a: u32, b: u32) -> (u32, u32) {
    return (a / b, a % b)
}
```

Destructure the result:

```novus
let (quotient, remainder) = div_mod(17, 5)
```

## Operators

### Arithmetic Operators

```novus
+   // addition
-   // subtraction
*   // multiplication
/   // division
%   // modulo/remainder
```

### Comparison Operators

```novus
==  // equal
!=  // not equal
<   // less than
<=  // less than or equal
>   // greater than
>=  // greater than or equal
```

### Logical Operators

```novus
&&  // logical AND
||  // logical OR
!   // logical NOT
```

### Bitwise Operators

```novus
&   // bitwise AND
|   // bitwise OR
^   // bitwise XOR
~   // bitwise NOT
<<  // left shift
>>  // right shift
```

### Assignment Operators

```novus
=    // assignment
+=   // add and assign
-=   // subtract and assign
*=   // multiply and assign
/=   // divide and assign
%=   // modulo and assign
&=   // bitwise AND and assign
|=   // bitwise OR and assign
^=   // bitwise XOR and assign
<<=  // left shift and assign
>>=  // right shift and assign
```

### Range Operators

```novus
..   // exclusive range (0..10 = 0 to 9)
..=  // inclusive range (0..=10 = 0 to 10)
```

## Memory and Safety

### Unsafe Blocks

Operations that bypass safety checks:

```novus
unsafe {
    let ptr = $DFF000 as *u16
    *ptr = 0x0020
}
```

### Using Blocks

RAII-style resource management:

```novus
using window = open_window() {
    // window is automatically closed when scope exits
}
```

### Defer Statements

Defer execution until scope exit:

```novus
fn process_file(path: String) {
    let file = open(path)
    defer close(file)

    // file will be closed when function returns
    read_data(file)
}
```

## Imports and Modules

### Importing from Modules

```novus
from std::collections::vec import Vec
from std::io import print, println
from std::core import Option, Result
```

### Import Aliases

```novus
from std::collections::hashmap import HashMap as Map
```

## Visibility and Access Control

Visibility modifiers:
- `pub` — public visibility
- `internal` — internal to current project
- (no modifier) — private (default)

## Compile-Time Features

### Const Functions

Functions that can be evaluated at compile time:

```novus
const fn square(x: i32) -> i32 {
    return x * x
}
```

Called at compile time:

```novus
let size: u32 = square(16)
```

### Sizeof, Offsetof, Alignof

Compile-time size and layout queries:

```novus
sizeof(u32)              // 4
offsetof(Point, y)       // offset of field y
alignof(f64)             // alignment requirement
```

### Assertions

Runtime and compile-time assertions:

```novus
assert!(x > 0)
assert!(buffer.len() > 0)
```

## Attributes

Attributes provide metadata for functions, types, and other items:

```novus
@test
fn test_addition() {
    assert!(add(2, 3) == 5)
}
```

```novus
@bench
fn bench_sort() {
    // ... benchmark code
}
```

```novus
@export
fn exported_function() {
    // ... function exported to C
}
```

---

**Note:** This reference is a work in progress. Some language features are still under development. For the most up-to-date information, see the [Novus GitHub repository](https://github.com/BarryPSmith/Novus).
