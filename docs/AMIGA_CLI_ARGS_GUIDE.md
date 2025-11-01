# AmigaOS Command-Line Arguments - Complete Guide

**TL;DR:** AmigaOS has **three different ways** to handle command-line arguments, unlike Linux's single `argc/argv` approach.

---

## Overview: Three Entry Methods

On AmigaOS, your program can be launched in **three different ways**:

1. **From CLI/Shell** - User types command in AmigaDOS Shell
2. **From Workbench** - User double-clicks icon
3. **As a background process** - Launched by another program

Each method provides arguments differently!

---

## Method 1: CLI/Shell Launch (Traditional)

### The Classic Way (Like C's argc/argv)

When launched from CLI, AmigaOS **does NOT** use `argc/argv` like Unix. Instead:

```c
int main(int argc, char **argv)  // This is FAKE on Amiga!
```

**What really happens:**

1. The **C runtime startup code** reads the command line from DOS
2. It calls `DOS->Input()` and `DOS->Read()` to get the command string
3. It parses the string into tokens and builds argc/argv
4. Then calls your `main()`

**The real Amiga way** (without C runtime):

```novus
// Amiga programs can read the command line directly from DOS
from std::ffi::dos import Input, Read, Output, Write

pub fn main() -> i32 {
    // Get the input filehandle (CLI command line)
    let input_fh = Input()

    if input_fh == 0 {
        // Launched from Workbench, not CLI
        return handle_workbench_launch()
    }

    // Read command line from DOS
    var buffer: [256]u8
    let bytes_read = Read(input_fh, &buffer[0] as i32, 256)

    // Parse buffer into arguments yourself
    // (AmigaDOS does NOT tokenize for you!)

    return 0
}
```

**Key Differences from Linux:**

| Feature | Linux (argc/argv) | AmigaOS CLI |
|---------|-------------------|-------------|
| **Parsing** | Done by OS kernel | Done by C runtime OR you |
| **Format** | Array of strings | Single string you must parse |
| **Quoting** | Shell handles | You handle (or C runtime does) |
| **Max length** | Kernel limited | DOS buffer limited (usually 256 bytes) |

---

## Method 2: Workbench Launch (GUI Double-Click)

When user **double-clicks your icon** in Workbench:

### Entry Point Changes!

```novus
// Your program receives WBStartup message instead of argc/argv
from std::ffi::amiga_structs import WBStartup, WBArg

pub fn main() -> i32 {
    // Check if launched from Workbench
    // (argc == 0 means WBStartup)

    // The REAL entry point on Amiga receives WBStartup as a message
    // in the process's message port

    return 0
}
```

### WBStartup Structure:

```novus
pub struct WBStartup {
    sm_Message: Message,        // Standard Exec message
    sm_Process: *MsgPort,        // Your process's message port
    sm_Segment: i32,             // Your program's code segment
    sm_NumArgs: i32,             // Number of arguments (files clicked)
    sm_ToolWindow: *u8,          // Tool window spec (or NULL)
    sm_ArgList: *WBArg,          // Array of WBArg (files/icons)
}

pub struct WBArg {
    wa_Lock: i32,                // DOS lock on directory
    wa_Name: *u8,                // Filename (not full path!)
}
```

**How to detect Workbench launch:**

```novus
// Traditional C approach (used by VBCC):
int main(int argc, char **argv) {
    if (argc == 0) {
        // Launched from Workbench
        struct WBStartup *wbmsg = (struct WBStartup *)argv;
        // Process WBStartup
    } else {
        // Launched from CLI
        // Use argc/argv normally
    }
}
```

**Workbench Launch Flow:**

1. User double-clicks icon
2. Workbench sends `WBStartup` message to your process
3. Your program starts, but **argc=0** and **argv** points to `WBStartup`
4. You must reply to the message when done: `ReplyMsg(wbmsg)`

---

## Method 3: ReadArgs() - The Modern Amiga Way

**ReadArgs()** is AmigaDOS 2.0+'s **official argument parser** - like getopt() but better!

### Why ReadArgs() is Superior:

- **Template-based** - You define argument format in a template string
- **Type-safe** - Automatically parses switches, numbers, strings, multi-args
- **Built-in help** - Generates help text from template
- **Localization** - Supports localized argument names

### Template Syntax:

```
"FROM/A,TO/A,VERBOSE/S,COUNT/N"
```

Modifiers:
- `/A` = Required (Always needed)
- `/S` = Switch (Boolean flag)
- `/K` = Keyword (must use NAME=value)
- `/N` = Number (parse as integer)
- `/M` = Multiple (accepts multiple values)
- `/F` = Rest of line

### Example Usage:

```novus
from std::ffi::dos import ReadArgs, FreeArgs
from std::ffi::amiga_structs import RDArgs

pub fn parse_args() -> i32 {
    // Define template
    let template: String = "FROM/A,TO/A,VERBOSE/S"

    // Allocate result array (one slot per template item)
    var results: [3]i32 = {0, 0, 0}

    // Create RDArgs structure
    var rdargs: RDArgs

    // Parse arguments
    let parsed = ReadArgs(
        template.ptr,
        &results[0] as *u8,
        &rdargs as *RDArgs
    )

    if parsed == 0 as *RDArgs {
        // Parse failed
        return 1
    }

    // Extract values
    let from_file: *u8 = results[0] as *u8   // String
    let to_file: *u8 = results[1] as *u8     // String
    let verbose: i32 = results[2]            // Boolean (0 or -1)

    // Use arguments...

    // IMPORTANT: Free RDArgs when done
    FreeArgs(&rdargs as *RDArgs)

    return 0
}
```

### Template Examples:

```
"FILE/A"                    # Required filename
"FROM/A,TO/A"               # Two required args
"VERBOSE/S,QUIET/S"         # Two switches
"COUNT/N/A"                 # Required number
"FILES/M"                   # Multiple files
"PATTERN/K"                 # Keyword arg (PATTERN=*.c)
"COMMENT/F"                 # Rest of line
```

**User types:**
```
mycmd FROM myfile.txt TO output.txt VERBOSE
```

**ReadArgs() fills results array:**
```
results[0] = pointer to "myfile.txt"
results[1] = pointer to "output.txt"
results[2] = -1 (TRUE for VERBOSE)
```

---

## Complete Comparison Table

| Feature | Linux argc/argv | AmigaOS CLI | AmigaOS ReadArgs() | Workbench WBStartup |
|---------|----------------|-------------|-------------------|---------------------|
| **Launch method** | Shell | CLI/Shell | CLI/Shell | GUI icon |
| **Format** | String array | Single string | Template-based | Message structure |
| **Parsing** | Pre-parsed | Manual | Automatic | N/A (file list) |
| **Type safety** | None | None | Built-in | N/A |
| **Help text** | Manual | Manual | Auto-generated | N/A |
| **Switches** | Manual (-v) | Manual | Built-in (/S) | N/A |
| **Validation** | Manual | Manual | Built-in (/A) | N/A |

---

## Recommended Approach for Novus

### Option 1: Simple (C Runtime Compatibility)

Use traditional `argc/argv` with VBCC C runtime:

```novus
pub fn main() -> i32 {
    // VBCC startup code provides argc/argv
    // Just like C programs
    return 0
}
```

**Pros:**
- Works immediately
- Compatible with C
- No extra code needed

**Cons:**
- Not "true" Amiga style
- Requires C runtime
- No Workbench support without extra code

---

### Option 2: Native Amiga (ReadArgs)

Implement ReadArgs() wrapper in Novus stdlib:

```novus
// Proposed Novus API
from std::args import Args

pub fn main() -> i32 {
    let args = Args::parse("FROM/A,TO/A,VERBOSE/S") or {
        panic("Invalid arguments")
    }

    let from = args.get_string("FROM")
    let to = args.get_string("TO")
    let verbose = args.get_switch("VERBOSE")

    // Use args...

    return 0
}
```

**Pros:**
- Native AmigaOS
- Type-safe
- Auto help text
- Professional

**Cons:**
- Need to implement Args wrapper
- More complex

---

### Option 3: Dual Mode (Best of Both)

Support both CLI and Workbench launches:

```novus
pub fn main() -> i32 {
    // Detect launch mode
    if launched_from_workbench() {
        return handle_workbench()
    } else {
        return handle_cli()
    }
}

fn launched_from_workbench() -> bool {
    // Check if Input() returns 0
    let input_fh = Input()
    return input_fh == 0
}
```

---

## Current State in Novus

Based on the codebase analysis:

1. ✅ **WBStartup struct defined** - in `std/ffi/amiga_structs.novus:1223`
2. ✅ **ReadArgs() extern declared** - in `std/ffi/dos.novus:143`
3. ✅ **RDArgs struct defined** - in `std/ffi/amiga_structs.novus:617`
4. ❌ **High-level Args API** - Not yet implemented
5. ❌ **Workbench launch support** - Not yet implemented

---

## Recommendation for Novus

**Implement a three-tier approach:**

### Tier 1: Basic (Current - Works Now)
```novus
pub fn main() -> i32 {
    // Use VBCC's argc/argv from C runtime
    return 0
}
```

### Tier 2: Native ReadArgs (Implement Next)
```novus
from std::args import parse_args

pub fn main() -> i32 {
    let args = parse_args("FROM/A,TO/A") or { return 1 }
    // ...
}
```

### Tier 3: Full Amiga (Future)
```novus
#[amiga_program(cli = "FROM/A,TO/A", workbench = true)]
pub fn main(args: Args) -> i32 {
    // Compiler handles both CLI and Workbench
    // args works regardless of launch method
}
```

---

## Example: Complete Amiga-Native Argument Handling

Here's how a proper AmigaOS program handles all three launch modes:

```c
// Traditional C approach
#include <dos/dos.h>
#include <dos/rdargs.h>
#include <workbench/startup.h>

int main(int argc, char **argv) {
    if (argc == 0) {
        // Launched from Workbench
        struct WBStartup *wbmsg = (struct WBStartup *)argv;

        // Process files from wbmsg->sm_ArgList
        for (int i = 0; i < wbmsg->sm_NumArgs; i++) {
            struct WBArg *arg = &wbmsg->sm_ArgList[i];
            // arg->wa_Lock = directory lock
            // arg->wa_Name = filename
        }

        // MUST reply to message!
        Forbid();
        ReplyMsg((struct Message *)wbmsg);

    } else {
        // Launched from CLI - use ReadArgs
        LONG args[3] = {0};
        struct RDArgs *rdargs;

        rdargs = ReadArgs("FROM/A,TO/A,VERBOSE/S", args, NULL);
        if (rdargs) {
            char *from = (char *)args[0];
            char *to = (char *)args[1];
            LONG verbose = args[2];

            // Do work...

            FreeArgs(rdargs);
        } else {
            PrintFault(IoErr(), "myprogram");
            return RETURN_ERROR;
        }
    }

    return 0;
}
```

---

## Summary

**The short answer:**

AmigaOS **does NOT use argc/argv natively**. You have three options:

1. **Use VBCC's C runtime** - gives you argc/argv (fake, but works)
2. **Use ReadArgs()** - the "proper" AmigaOS 2.0+ way
3. **Handle WBStartup** - for GUI launches

**For Novus, I recommend:**

Start with VBCC's argc/argv (works now), then add a high-level `std::args` module that wraps ReadArgs() for the authentic Amiga experience.

---

## Files in Novus That Already Support This

- `Novus/std/ffi/dos.novus:143` - `ReadArgs()` extern
- `Novus/std/ffi/dos.novus:152` - `FreeArgs()` extern
- `Novus/std/ffi/amiga_structs.novus:617` - `RDArgs` struct
- `Novus/std/ffi/amiga_structs.novus:1223` - `WBStartup` struct
- `Novus/std/ffi/amiga_structs.novus:1232` - `WBArg` struct

**Next step:** Implement `std::args` module with a nice Novus wrapper!

---

**End of Guide**
