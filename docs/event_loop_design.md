# Novus Event Loop Design for Amiga

## Traditional Pattern (Current Amiga C Code)

```c
while (closewin == FALSE) {
   Wait(1L << myWindow->UserPort->mp_SigBit);
   msg = GT_GetIMsg(myWindow->UserPort);
   msgClass = msg->Class;
   GT_ReplyIMsg(msg);

   if (msgClass == IDCMP_CLOSEWINDOW) {
      CloseWindow(myWindow);
      closewin = TRUE;
   }
   if (msgClass == IDCMP_REFRESHWINDOW)
      RefreshWindowFrame(myWindow);
}
```

## Problems with Traditional Pattern

1. **Manual message reply** - Easy to forget `GT_ReplyIMsg()`, causes deadlock
2. **No type safety** - Message class is just an integer constant
3. **Verbose boilerplate** - Same Wait/Get/Reply pattern everywhere
4. **Error-prone** - Easy to use message after replying
5. **Hard to compose** - Can't easily wait on multiple sources

## Novus Multi-Level Design

### Level 1: Low-Level (Traditional Access)

For developers who want direct control or are porting existing code:

```novus
use std::intuition::{Window, IntuiMessage, IDCMP}
use std::exec::Wait

pub fn traditional_loop(window: &Window) {
    let mut close_win = false

    while !close_win {
        // Direct low-level access
        unsafe {
            Wait(1 << window.user_port().signal_bit())

            let msg = window.user_port().get_message()
            let msg_class = msg.class()
            msg.reply()  // Manual reply required

            match msg_class {
                IDCMP::CLOSEWINDOW => {
                    close_win = true
                }
                IDCMP::REFRESHWINDOW => {
                    window.refresh_frame()
                }
                _ => {}
            }
        }
    }
}
```

**Key Points:**
- Requires `unsafe` block - you're managing message lifecycle manually
- Direct access to all OS structures
- Matches traditional pattern exactly

### Level 2: Safe Iterator Pattern

Modern iteration with automatic message reply via RAII:

```novus
use std::intuition::{Window, WindowEvent}

pub fn iterator_loop(window: &mut Window) -> Result<(), Error> {
    // Iterator automatically handles Wait/Get/Reply
    for event in window.events() {
        match event {
            WindowEvent::CloseWindow => {
                return Ok(())  // Exit loop on close
            }
            WindowEvent::RefreshWindow => {
                window.refresh_frame()?
            }
            WindowEvent::MouseMove { x, y } => {
                // Handle mouse movement
            }
            WindowEvent::Gadget { id, .. } => {
                // Handle gadget events
            }
            _ => {}
        }
        // Message automatically replied when event goes out of scope
    }

    Ok(())
}
```

**Key Points:**
- `window.events()` returns iterator that yields typed events
- Each event is RAII - automatically replies on drop
- Type-safe pattern matching
- Can break/return without worrying about reply
- Iterator handles Wait() automatically

### Level 3: Callback/Async Pattern

High-level declarative style with callbacks or async:

```novus
use std::intuition::{Window, WindowBuilder, EventHandler}

pub fn callback_loop() -> Result<(), Error> {
    let mut window = WindowBuilder::new()
        .title("My Window")
        .dimensions(320, 200, 640, 480)
        .idcmp_flags(IDCMP::CLOSEWINDOW | IDCMP::REFRESHWINDOW | IDCMP::MOUSEBUTTONS)
        .on_close(|| {
            println("Window closing...")
            EventAction::Close
        })
        .on_refresh(|win| {
            win.refresh_frame()
            EventAction::Continue
        })
        .on_mouse_click(|x, y, button| {
            println("Clicked at {}, {} with button {}", x, y, button)
            EventAction::Continue
        })
        .build()?

    // Run event loop until EventAction::Close returned
    window.run()?

    Ok(())
}
```

**Key Points:**
- Declarative event handler registration
- No manual loop management
- Window automatically cleaned up via RAII
- Can return `EventAction::Close` to exit

### Level 4: Async/Await Pattern (Future Enhancement)

For complex multi-window or multi-source applications:

```novus
use std::intuition::{Window, WindowEvent}
use std::async::{select, timeout}

pub async fn async_loop(window: &mut Window, serial: &mut SerialPort) -> Result<(), Error> {
    loop {
        // Wait on multiple event sources simultaneously
        select! {
            event = window.next_event() => {
                match event? {
                    WindowEvent::CloseWindow => break,
                    WindowEvent::RefreshWindow => {
                        window.refresh_frame()?
                    }
                    _ => {}
                }
            }

            data = serial.read_line() => {
                println("Serial: {}", data?)
            }

            _ = timeout(Duration::seconds(5)) => {
                println("5 seconds elapsed, updating display...")
                window.update_status()?
            }
        }
    }

    Ok(())
}
```

**Key Points:**
- Composable event sources
- Timeouts and deadlines
- No manual signal mask management
- Async functions compile to state machines (no threads)

## Implementation Details

### RAII Message Handle

```novus
/// Safe wrapper around IntuiMessage that auto-replies on drop
pub struct WindowEvent {
    msg: *IntuiMessage,
    port: *MsgPort,
    event_type: EventType,
}

impl WindowEvent {
    // Convert raw message to typed event
    unsafe fn from_message(msg: *IntuiMessage, port: *MsgPort) -> WindowEvent {
        let event_type = match msg.class {
            IDCMP::CLOSEWINDOW => EventType::CloseWindow,
            IDCMP::REFRESHWINDOW => EventType::RefreshWindow,
            IDCMP::MOUSEBUTTONS => EventType::MouseClick {
                x: msg.mouse_x,
                y: msg.mouse_y,
                button: msg.code,
            },
            // ... etc
        }

        WindowEvent { msg, port, event_type }
    }
}

impl Drop for WindowEvent {
    fn drop(&mut self) {
        // Automatically reply to message when event goes out of scope
        unsafe {
            ReplyMsg(self.msg as *Message)
        }
    }
}
```

### Event Iterator

```novus
pub struct WindowEventIterator<'a> {
    window: &'a Window,
}

impl<'a> Iterator for WindowEventIterator<'a> {
    type Item = WindowEvent;

    fn next(&mut self) -> Option<WindowEvent> {
        unsafe {
            // Wait for signal
            Wait(1 << self.window.user_port().signal_bit())

            // Get message (returns None if no messages)
            let msg = self.window.user_port().get_message()?

            // Convert to typed event
            Some(WindowEvent::from_message(msg, self.window.user_port()))
        }
    }
}

impl Window {
    pub fn events(&self) -> WindowEventIterator {
        WindowEventIterator { window: self }
    }
}
```

## Comparison Matrix

| Feature | Traditional | Iterator | Callback | Async |
|---------|------------|----------|----------|-------|
| **Safety** | Unsafe | Safe | Safe | Safe |
| **Message Reply** | Manual | Auto | Auto | Auto |
| **Type Safety** | No | Yes | Yes | Yes |
| **Composability** | Low | Medium | Medium | High |
| **Multi-Source** | Manual | Manual | Medium | Easy |
| **Learning Curve** | Familiar | Easy | Easy | Medium |
| **Control** | Full | Full | Medium | Medium |

## Migration Path

1. **Start with Iterator** - Most Amiga developers should use Level 2
2. **Use Traditional for ports** - When porting existing C code verbatim
3. **Use Callbacks for simple UI** - When you want declarative style
4. **Use Async for complex apps** - Multi-window, networking, etc.

## Standard Library Organization

```
std::intuition
├── raw          # Low-level bindings (traditional access)
├── events       # Event types and iterator
├── handlers     # Callback-based builders
└── async        # Async event sources (future)
```

## Example: Real-World Application

```novus
use std::intuition::{WindowBuilder, EventAction, IDCMP}
use std::graphics::{RastPort, Rectangle}

pub fn paint_program() -> Result<(), Error> {
    let mut drawing = false
    let mut last_x = 0
    let mut last_y = 0

    let mut window = WindowBuilder::new()
        .title("Paint")
        .dimensions(0, 0, 640, 480)
        .idcmp_flags(IDCMP::CLOSEWINDOW | IDCMP::MOUSEBUTTONS | IDCMP::MOUSEMOVE)
        .flags(WFLG_DRAGBAR | WFLG_CLOSEGADGET | WFLG_REPORTMOUSE)
        .build()?

    // Iterator-based approach with mutable state
    for event in window.events() {
        match event {
            WindowEvent::CloseWindow => break,

            WindowEvent::MouseDown { x, y, button } if button == 1 => {
                drawing = true
                last_x = x
                last_y = y
            }

            WindowEvent::MouseUp { button, .. } if button == 1 => {
                drawing = false
            }

            WindowEvent::MouseMove { x, y } if drawing => {
                window.rast_port().draw_line(last_x, last_y, x, y)?
                last_x = x
                last_y = y
            }

            _ => {}
        }
    }

    Ok(())
}
```

## Conclusion

This design provides:

1. **Backward compatibility** - Traditional unsafe access for porting
2. **Safety by default** - Iterator pattern prevents message leaks
3. **Modern ergonomics** - Callbacks and async for complex apps
4. **Zero overhead** - All abstractions compile away
5. **Incremental adoption** - Start simple, add complexity as needed

The key insight is that RAII message handles eliminate the most common error (forgetting to reply), while still allowing full control when needed.
