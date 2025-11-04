# C Interop Example

This example demonstrates how to call Novus functions from C code using the `#[export]` attribute.

## Overview

Novus provides seamless C interop since it compiles to C as an intermediate representation. You can expose Novus functions to C code by marking them with the `#[export]` attribute.

## Files

- `novus_math.novus` - Novus library with exported math functions
- `c_client.c` - C program that calls the Novus functions (to be created)
- `novus_math_exports.h` - Auto-generated C header (created by compiler)
- `novus_types.h` - Auto-generated shared types header (created by compiler)

## Usage

### 1. Write Novus code with #[export]

```novus
#[export]
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}
```

### 2. Compile the Novus code

```bash
novusc compile novus_math.novus -o novus_math
```

This generates:
- Individual C files for each function (e.g., `novus_math_add.c`)
- `novus_math_exports.h` - Header with exported function declarations
- `novus_types.h` - Shared type definitions
- Object files (`.o`) for each function

### 3. Write C code that uses the exported functions

```c
#include "novus_math_exports.h"
#include <stdio.h>

int main(void) {
    int result = add(10, 20);
    printf("10 + 20 = %d\n", result);
    return 0;
}
```

### 4. Compile and link the C code

```bash
# Compile the C client
vc +aos68k -c99 c_client.c -o c_client.o

# Link with Novus object files
vlink -bamigahunk -x -Bstatic -Cvbcc -nostdlib \
    c_client.o novus_math_add.o novus_math_subtract.o \
    <other novus object files> \
    -o final_program
```

## Key Features

### Type Mapping

Novus types are automatically mapped to C types:

| Novus Type | C Type     |
|------------|------------|
| `i8`       | `int8_t`   |
| `i16`      | `int16_t`  |
| `i32`      | `int32_t`  |
| `i64`      | `int64_t`  |
| `u8`       | `uint8_t`  |
| `u16`      | `uint16_t` |
| `u32`      | `uint32_t` |
| `u64`      | `uint64_t` |
| `bool`     | `bool`     |
| `*T`       | `T*`       |
| no return  | `void`     |

### Name Mangling

- **Exported functions**: No name mangling - uses the exact Novus function name
- **Non-exported functions**: May be mangled or inlined by the compiler

### Linkage

- **Exported functions**: Have external linkage (no `static` keyword)
- **Non-exported functions**: May have internal linkage

### Generated Header Format

The auto-generated header includes:

```c
#ifndef NOVUS_EXPORTS_H
#define NOVUS_EXPORTS_H

#include <stdint.h>
#include <stdbool.h>
#include "novus_types.h"

#ifdef __cplusplus
extern "C" {
#endif

// Exported Novus functions

int32_t add(int32_t a, int32_t b);
int32_t multiply(int32_t x, int32_t y);

#ifdef __cplusplus
}
#endif

#endif // NOVUS_EXPORTS_H
```

## Limitations

- Generic functions cannot be exported (they need monomorphization)
- Struct/enum returns are handled via output parameters (VBCC limitation)
- Complex Novus types (like `String`, `Result`, `Option`) require FFI wrappers

## Best Practices

1. **Keep exported interfaces simple**: Use basic types for better C compatibility
2. **Use `pub` with `#[export]`**: Exported functions should be public
3. **Document your exports**: Add comments to exported functions
4. **Test both ways**: Test calling from C and from Novus
5. **Version your API**: Use semantic versioning for exported interfaces

## Example: Complete Workflow

```bash
# 1. Compile Novus library
novusc compile novus_math.novus -o novus_math

# 2. This generates novus_math_exports.h

# 3. Write your C client using the header

# 4. Compile everything together
# (Full linking script would go here)
```

## Advanced: Calling C from Novus

You can also call C functions from Novus using `extern fn`:

```novus
extern fn printf(format: *u8, ...args) -> i32

fn main() -> i32 {
    printf("Hello from Novus!\n")
    return 0
}
```

This creates **bidirectional interop** between Novus and C!
