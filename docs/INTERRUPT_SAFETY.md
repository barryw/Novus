# Interrupt Safety in Novus

This document describes the interrupt safety characteristics of the Novus runtime and provides guidance for writing interrupt-safe code.

## Overview

The Novus runtime is **NOT safe to call from interrupt context**. This is a deliberate design decision - the runtime prioritizes clear error messages and user-friendly diagnostics over interrupt compatibility.

## Why the Runtime is Not Interrupt-Safe

### Memory Allocation

All Novus memory allocation uses `Forbid()/Permit()` for thread safety, not `Disable()/Enable()`. The difference is critical:

- `Forbid()/Permit()` - Prevents task switching but allows interrupts
- `Disable()/Enable()` - Disables all interrupts (required for interrupt handlers)

Calling `AllocMem()` or `FreeMem()` from interrupt context while another allocation is in progress will cause unpredictable behavior.

### Error Handlers

Novus error handlers display GUI requesters using Intuition:

- `__novus_panic()` - Shows a requester with the panic message
- `__novus_assert_fail()` - Shows a requester with the failed assertion
- `__novus_bounds_check_fail()` - Shows a requester with the array bounds error

Intuition functions are not interrupt-safe - they allocate memory, take semaphores, and wait for user input.

### String Formatting

The `__novus_fmt_*` functions build formatted strings, which may:
- Allocate temporary buffers
- Call into the runtime's memory allocator
- Use static buffers that could be corrupted by reentrant calls

## Writing Interrupt-Safe Code

If you need to handle events in interrupt context, follow these guidelines:

### Use Raw AmigaOS Calls

For interrupt handlers, use direct AmigaOS calls within `unsafe` blocks:

```novus
@interrupt
fn my_audio_handler(data: *AudioData) {
    unsafe {
        // Direct hardware register access - no runtime calls
        let custom = (*Custom)$dff000
        custom.intreq = $0080  // Clear audio interrupt

        // Signal a task to do the real work
        Signal(data.task, data.signal_mask)
    }
}
```

### Signal Tasks for Complex Work

The recommended pattern for interrupt handling:

1. Keep the interrupt handler minimal (acknowledge interrupt, signal task)
2. Do all complex processing in a task that waits for the signal

```novus
struct AudioHandler {
    task: *Task,
    signal_bit: i8,
    signal_mask: u32,
    // ... audio state
}

// Interrupt handler - runs in interrupt context
@interrupt
fn audio_interrupt(handler: *AudioHandler) {
    unsafe {
        // Acknowledge interrupt (hardware access is safe)
        let custom = (*Custom)$dff000
        custom.intreq = $0080

        // Signal the task - Signal() is interrupt-safe
        Signal(handler.task, handler.signal_mask)
    }
}

// Task - runs in normal context, can use full runtime
fn audio_task(handler: &mut AudioHandler) {
    loop {
        Wait(handler.signal_mask)

        // Now we can use the full Novus runtime
        let next_sample = handler.get_next_sample()
        match next_sample {
            Some(sample) => handler.play(sample),
            None => break,
        }
    }
}
```

### Interrupt-Safe AmigaOS Functions

These exec.library functions ARE safe to call from interrupt context:
- `Signal()` - Signal a task
- `Cause()` - Queue a software interrupt
- `ReplyMsg()` - Reply to a message
- `PutMsg()` - Put a message on a port
- `GetMsg()` - Get a message from a port (without waiting)
- `AddHead()`, `AddTail()`, `Remove()` - List manipulation
- `FindName()` - List search

These are NOT safe from interrupt context:
- `AllocMem()`, `FreeMem()` - Memory allocation
- `OpenLibrary()`, `CloseLibrary()` - Library management
- `Wait()`, `WaitPort()` - Task waiting (will hang!)
- Any Intuition function
- Any DOS function

## Error Handling in Interrupt Context

If you need to report errors from interrupt context:

### Option 1: Set a Flag

```novus
static mut interrupt_error: bool = false
static mut interrupt_error_code: u32 = 0

@interrupt
fn my_handler(data: *MyData) {
    unsafe {
        if some_error_condition {
            interrupt_error = true
            interrupt_error_code = ERROR_CODE
        }
    }
}

// Check from task context
fn check_interrupt_errors() {
    unsafe {
        if interrupt_error {
            interrupt_error = false
            panic!("Interrupt error: {}", interrupt_error_code)
        }
    }
}
```

### Option 2: Signal with Error Data

```novus
struct InterruptData {
    task: *Task,
    signal_mask: u32,
    error: Option<InterruptError>,
}

@interrupt
fn my_handler(data: *InterruptData) {
    unsafe {
        if some_error_condition {
            (*data).error = Some(InterruptError::Overflow)
            Signal((*data).task, (*data).signal_mask)
        }
    }
}
```

## Future Considerations

In the future, we may add:
- A separate interrupt-safe panic mechanism that queues errors for later display
- An interrupt-safe logging facility that writes to a ring buffer
- Compile-time checking that `@interrupt` functions don't call runtime functions

For now, treat interrupt context as hostile to the runtime and keep handlers minimal.
