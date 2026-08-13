# Novus CLI Application Template

This template creates a standalone command-line application for AmigaOS that works correctly when launched from both the Shell and Workbench.

## Project Structure

```
myapp/
├── workspace.toml        # Workspace configuration
├── cli/                  # CLI project
│   ├── project.toml      # Project configuration
│   └── src/
│       └── main.novus    # Main program entry point
└── README.md             # This file
```

## Building

```bash
# Build the project
novusc build

# Build in release mode (optimized)
novusc build --release

# Build for specific CPU
novusc build --cpu 68040

# Build with specific FPU
novusc build --fpu 68881
```

## Running

### From Shell (CLI)
```
1> myapp
1> myapp help
1> myapp version arg1 arg2
```

### From Workbench
Double-click the program icon. Arguments are passed through tool types if configured.

## Argument Handling

The template demonstrates proper argument parsing that works in both launch modes:

### Main Function Signature
```novus
fn main(argc: i32, argv: **u8) -> i32
```

### Shell Launch
- `argc` contains the number of arguments (minimum 1)
- `argv[0]` contains the program name
- `argv[1...]` contain the command-line arguments

### Workbench Launch
- `argc` will be 0
- `argv` contains a pointer to the WBStartup message
- Arguments come from the WBStartup message (handled by runtime)
- Tool types in the `.info` file can provide arguments

## Key Features

✅ **Proper Argument Detection**: Automatically detects Shell vs Workbench launch
✅ **Hardware Detection**: Access CPU, FPU, and Chipset at runtime
✅ **Clean Exit Codes**: Returns proper exit codes to the Shell
✅ **Workbench Safe**: Never calls UnLock() on Workbench-provided locks

## Important Notes

### WBStartup Message Handling
When launched from Workbench, the program receives a `WBStartup` message instead of argc/argv. The Novus runtime automatically handles this:

1. Retrieves the WBStartup message before calling main()
2. Sets `__argc` to 0 to signal Workbench launch
3. Properly manages the message lifecycle
4. **Never unlocks** wa_Lock values (they belong to Workbench)

### Lock Safety
According to AmigaOS conventions:
- **DO NOT** call UnLock() on locks from WBStartup->sm_ArgList
- These locks are owned by Workbench and managed automatically
- Unlocking them causes system hangs

### Exit Codes
Return proper exit codes from main():
- `0` = Success
- `1-255` = Error codes
- Shell will see the return value
- Workbench ignores the return value

## Customization

1. **Change the program name**: Edit `name` in `cli/project.toml`
2. **Add dependencies**: Add to `[dependencies]` section in `cli/project.toml`
3. **Hardware targets**: Adjust `target_cpu`, `fpu`, `chipset` in `workspace.toml` (applies to all projects) or in `cli/project.toml` (project-specific)
4. **Add more commands**: Extend the argument parsing in `cli/src/main.novus`
5. **Add more projects**: Create new project directories and add them to `members` in `workspace.toml`

## Example: Typed ReadArgs Support

For more sophisticated argument parsing, use the owning application wrapper around DOS `ReadArgs()`:

```novus
from amiga::workbench import Args, ArgsError

fn parse_args() -> Result<Args, ArgsError> {
    // Args owns the native RDArgs state and releases it automatically.
    return Args::parse("FROM/A,TO/A,VERBOSE/S")
}
```

## Building for Distribution

```bash
# Build optimized release
novusc build --release --cpu 68020

# The executable will be in: build/myapp

# Copy to your distribution directory
cp build/myapp RAM:
```

## Testing on Real Hardware

1. Build the program
2. Copy to Amiga (via network, floppy, or emulator shared folder)
3. Test from Shell: `myapp help`
4. Test from Workbench: Double-click icon

## Hardware Conditional Compilation

Use preprocessor directives to optimize for specific hardware:

```novus
#if M68040
    // Optimized code for 68040
#elif M68020
    // Code for 68020
#else
    // Portable 68020+ fallback
#endif
```

## See Also

- [Language Design Document](../../docs/LanguageDesignDoc.md)
- [DOS Module Documentation](../../docs/stdlib/dos.md)
- [AmigaOS Workbench Library](https://wiki.amigaos.net/wiki/Workbench_Library)
