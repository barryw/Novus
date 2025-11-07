# greeting.library - Library Project

This project builds the `greeting.library` shared library for AmigaOS.

## Project Configuration

See `project.toml` for build configuration:

- **Type**: `library` - Builds an AmigaOS shared library (.library file)
- **Entry**: `./src/lib.novus` - Main library source file
- **Output**: `greeting.library` - Name of the generated library file
- **Target CPU**: `68020` - Minimum CPU requirement
- **FPU**: `auto` - Automatic FPU detection

## Source Code

The library source is in `src/lib.novus`. It uses the `@library` attribute to define the library:

```novus
@library(name = "greeting.library", version = 1, revision = 0)
pub struct GreetingLibrary {
    call_count: u32,
}

impl GreetingLibrary {
    pub fn GetVersion() -> u32 { ... }
    pub fn Add(a: i32, b: i32) -> i32 { ... }
    pub fn GetCallCount() -> u32 { ... }
}
```

## Building

From the workspace root:
```bash
novusc build
```

Or build just this project:
```bash
cd library
novusc build
```

## Generated Files

The build creates a `greeting.library/` directory with:

| File | Purpose |
|------|---------|
| `greeting.library` | Library binary (install to LIBS:) |
| `greeting.h` | C header with function declarations |
| `greeting_lib.o` | Auto-open/close stub for linking |
| `greeting_lib.fd` | VBCC function description file |
| `greeting.novus` | Novus FFI bindings |

## Project Settings

### Optimization Level

Edit `project.toml` to change optimization:
```toml
[build]
optimization_level = 2  # 0=none, 1=basic, 2=full
```

### CPU Target

Change minimum CPU requirement:
```toml
[build]
target_cpu = "68000"  # Options: 68000, 68020, 68030, 68040, 68060
```

### Library Version

Update in the `@library` attribute:
```novus
@library(name = "greeting.library", version = 2, revision = 5)
```

## Adding Functions

Add public methods to the `impl GreetingLibrary` block:

```novus
impl GreetingLibrary {
    pub fn MyNewFunction(param: i32) -> i32 {
        return param * 2
    }
}
```

Rebuild - the compiler automatically:
- Assigns a vector offset
- Generates A6 wrapper assembly
- Updates C header and Novus FFI bindings
- Updates FD file
