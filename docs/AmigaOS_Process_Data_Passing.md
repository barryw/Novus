# AmigaOS Process Data Passing - The Correct Way

## The Problem

When spawning a new AmigaOS process with `CreateNewProc()`, you often need to pass initialization data (like a message port pointer, configuration struct, etc.) to the child process. This is not straightforward because:

1. `NP_Arguments` is for CLI argument strings, not arbitrary pointers
2. `NP_ExitData` is for the **exit handler**, not normal execution
3. `pr_MsgPort` is **reserved for DOS** and shouldn't be manually manipulated

## The Solution: WBStartup Message Protocol

The **correct, documented AmigaOS method** is to use the **WBStartup message protocol**. This is what Workbench uses to launch applications, and it's the standard pattern for passing initialization data.

### How It Works

1. **Parent creates a custom startup message** containing your data pointer
2. **Parent sends it to child's `pr_MsgPort`** after `CreateNewProc()` returns
3. **Child retrieves it via `GetMsg(pr_MsgPort)`** at the start of its entry function
4. **Child extracts the data and frees the message**

### Why This Works

- When you call `CreateNewProc()`, DOS creates the child process and sets up `pr_MsgPort` **before** your entry point runs
- The `pr_MsgPort` is specifically designed to receive startup messages
- This is safe because:
  - DOS owns the port creation and ensures it exists
  - Your entry function doesn't run until after `CreateNewProc()` returns
  - The parent can safely send to the port after getting the `Process*` back
  - The child can safely receive from it at startup

### Key Insight: CLI vs Workbench Processes

- **Workbench processes** (`NP_Cli = 0`): Expect a WBStartup message by default
- **CLI processes** (`NP_Cli = 1`): Have a CLI context but can still receive messages

For Novus, we create **CLI processes** to get proper I/O handles (for `write()`, `Printf()`, etc.), but we **still use the WBStartup message protocol** to pass data. This gives us the best of both worlds.

## Novus Implementation

### Parent Side: `spawn_task_fn_with_ptr()`

```novus
// Spawn a worker with a message port pointer
let (tx, rx) = bounded_channel::<i32>()?
let port_ptr = (*u8)rx.port()
let handle = spawn_task_fn_with_ptr("Worker", worker_entry, port_ptr, 8192)?
```

**What happens:**
1. `spawn_task_fn_with_ptr()` calls `CreateNewProc()` with standard tags
2. After process creation succeeds, allocates a `Message` with the data pointer as payload
3. Sends message to `child_proc.pr_MsgPort` via `PutMsg()`
4. Returns the `ProcessHandle`

### Child Side: `receive_startup_data()`

```novus
fn worker_entry() -> i32 {
    // Get data pointer from parent
    let data_opt = receive_startup_data()
    let port = match data_opt {
        Option::Some(ptr) => (*MsgPort)ptr,
        Option::None => return 1,  // Error - no startup data
    }

    // Now use the port...
    // ...

    return 0
}
```

**What happens:**
1. `receive_startup_data()` gets the current process via `FindTask(null)`
2. Waits on `pr_MsgPort` signal and retrieves the message via `GetMsg()`
3. Extracts the data pointer from the message payload
4. Frees the message memory
5. Returns `Option::Some(data_ptr)`

## Message Structure

```
+------------------+
| Message Header   |  14 bytes
| - mn_Node        |
| - mn_ReplyPort   |  (null - no reply needed)
| - mn_Length      |
+------------------+
| Data Pointer     |  4 bytes (pointer to actual data)
+------------------+
```

## Why Previous Attempts Failed

### ❌ `NP_Arguments`
- **Purpose**: Pass CLI argument strings to DOS
- **Problem**: Only works for string arguments, gets parsed by DOS, not for arbitrary pointers

### ❌ `NP_ExitData`
- **Purpose**: Pass data to the **exit handler** (set via `NP_ExitCode`)
- **Problem**: Only available in the exit handler, **not** during normal execution
- **Documentation quote**: "the optional argument for the NP_ExitCode routine"

### ❌ Manual `pr_MsgPort` manipulation
- **Purpose**: DOS internal message port for process management
- **Problem**: Trying to replace or manually use it caused crashes (illegal instruction)
- **Why**: DOS expects to control this port for its own purposes

### ✅ WBStartup Message Protocol
- **Purpose**: Standard AmigaOS pattern for passing initialization data
- **Used by**: Workbench, all proper AmigaOS applications
- **Safe because**: Uses the port correctly - sends message **to** it, doesn't replace it
- **Works with**: Both CLI and Workbench-style processes

## References

- **AmigaOS NDK 3.9**: dos.library/CreateNewProc documentation
- **RKM Libraries Manual**: Process creation and WBStartup protocol
- **AutoDocs**: NP_ExitData, NP_ExitCode tags

## Common Patterns

### Pattern 1: Passing a Channel

```novus
fn worker(port: *MsgPort) -> i32 {
    let data_opt = receive_startup_data()
    let port = match data_opt {
        Option::Some(ptr) => (*MsgPort)ptr,
        Option::None => return 1,
    }

    // Receive messages from parent
    forever {
        let msg = GetMsg(port)
        // ... process ...
    }
}

// Parent
let (tx, rx) = channel::<WorkItem>()?
let handle = spawn_task_fn_with_ptr("Worker", worker, (*u8)rx.port(), 8192)?
```

### Pattern 2: Passing a Config Struct

```novus
struct Config {
    buffer_size: u32,
    parent_port: *MsgPort,
}

fn worker() -> i32 {
    let data_opt = receive_startup_data()
    let config = match data_opt {
        Option::Some(ptr) => (*Config)ptr,
        Option::None => return 1,
    }

    // Use config...
}

// Parent
var cfg = Config { buffer_size: 1024, parent_port: my_port }
let handle = spawn_task_fn_with_ptr("Worker", worker, (*u8)&cfg, 8192)?
```

**IMPORTANT**: The config struct must remain alive for the lifetime of the child process!

## Safety Considerations

1. **Message allocation**: Always use `MEMF_PUBLIC` so child can access it
2. **Message lifetime**: Child must free the startup message after extracting data
3. **Data lifetime**: Data pointed to by the startup pointer must outlive the child
4. **Port ownership**: Never try to replace or delete `pr_MsgPort` - DOS owns it
5. **CLI vs WB**: Use `NP_Cli = 1` for proper I/O, but still use message protocol for data

## Conclusion

The WBStartup message protocol is the **standard, documented, correct way** to pass initialization data to spawned AmigaOS processes. It's:

- **Safe**: Uses OS-provided mechanisms correctly
- **Portable**: Works on all AmigaOS versions (V36+)
- **Standard**: What real AmigaOS applications use
- **Flexible**: Can pass any pointer (ports, structs, arrays, etc.)
- **Clean**: No hacks, no undocumented behavior, no reserved fields

When in doubt, follow the OS conventions. This is what they're designed for.
