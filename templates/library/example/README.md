# greeting-example - Example Program Project

This project builds an example program that demonstrates how to use `greeting.library` from Novus code.

## Project Configuration

See `project.toml` for build configuration:

- **Type**: `cli` - Builds a command-line executable
- **Entry**: `./src/main.novus` - Main program entry point
- **Output**: `test-greeting` - Name of the generated executable
- **Target CPU**: `68020` - Minimum CPU requirement
- **FPU**: `auto` - Automatic FPU detection

## Building

From the solution root:
```bash
novusc build
```

Or build just this project:
```bash
cd example
novusc build
```

## Running

1. Install the library to LIBS:
   ```bash
   copy ../library/greeting.library/greeting.library LIBS:
   ```

2. Run the example:
   ```bash
   ./test-greeting
   ```

## Dependencies

This project depends on the `greeting.library` project. The dependency is declared in `project.toml`:

```toml
[dependencies]
library = { path = "../library" }
```

When building the solution, the compiler automatically:
1. Builds the library project first
2. Links the library's auto-open stub (`greeting_lib.o`) into this example
3. Ensures the library is installed to LIBS: before running

## Project Settings

### Optimization Level

Edit `project.toml`:
```toml
[build]
optimization_level = 2  # 0=none, 1=basic, 2=full
```

### CPU Target

```toml
[build]
target_cpu = "68000"  # Options: 68000, 68020, 68030, 68040, 68060
```

## Using the Library

This Novus example demonstrates calling library functions! It uses:

1. **C Wrapper Functions** (`greeting_calls.c`) - Uses VBCC inline pragmas to call the library
2. **Extern Declarations** (in Novus) - Declares the C wrapper functions
3. **Auto-Open/Close** (`greeting_lib.o`) - Library is automatically opened at startup

### How It Works

The example calls three library functions:
- `call_GreetingLibrary_GetVersion()` - Returns version (1.0)
- `call_GreetingLibrary_Add(5, 3)` - Returns 8
- `call_GreetingLibrary_GetCallCount()` - Returns call count

The build system automatically:
1. Compiles `greeting_calls.c` (the C wrapper)
2. Links it with the Novus code
3. Links `greeting_lib.o` (auto-open stub)
4. Creates the final executable

Run it on Amiga and check the exit code:
```bash
./greeting-example
echo $?  # Should print 8 (result of Add(5,3))
```
