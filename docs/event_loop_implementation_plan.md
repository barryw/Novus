# Novus Event Loop Implementation Plan

## What We Have

### FFI Layer (std::ffi)

**Structs:**
- `MsgPort` - Message port with signal bit
- `Message` - Base message type
- `Window` - Intuition window (opaque in most respects)
- `IntuiMessage` - Forward declaration (opaque type)

**Functions:**
- `GT_GetIMsg(port: *MsgPort) -> *IntuiMessage` - Get message from port
- `GT_ReplyIMsg(msg: *IntuiMessage)` - Reply to message
- `RefreshWindowFrame(window: *Window)` - Refresh window borders
- `CloseWindow(window: *Window)` - Close window

**Constants (std::ffi::amiga_consts):**
```
IDCMP_ACTIVEWINDOW    = $00040000
IDCMP_CHANGEWINDOW    = $02000000
IDCMP_CLOSEWINDOW     = $00000200
IDCMP_DELTAMOVE       = $00100000
IDCMP_DISKINSERTED    = $00008000
IDCMP_DISKREMOVED     = $00010000
IDCMP_GADGETDOWN      = $00000020
IDCMP_GADGETHELP      = $04000000
IDCMP_GADGETUP        = $00000040
IDCMP_IDCMPUPDATE     = $00800000
IDCMP_INACTIVEWINDOW  = $00080000
IDCMP_INTUITICKS      = $00400000
IDCMP_LONELYMESSAGE   = $80000000
IDCMP_MENUHELP        = $01000000
IDCMP_MENUPICK        = $00000100
IDCMP_MENUVERIFY      = $00002000
IDCMP_MOUSEBUTTONS    = $00000008
IDCMP_MOUSEMOVE       = $00000010
IDCMP_NEWPREFS        = $00004000
IDCMP_NEWSIZE         = $00000002
IDCMP_RAWKEY          = $00000400
IDCMP_REFRESHWINDOW   = $00000004
IDCMP_REQCLEAR        = $00001000
IDCMP_REQSET          = $00000080
IDCMP_REQVERIFY       = $00000800
IDCMP_SIZEVERIFY      = $00000001
IDCMP_VANILLAKEY      = $00200000
IDCMP_WBENCHMESSAGE   = $00020000
```

## What We Need to Add

### 1. IntuiMessage Struct Fields (std::ffi::amiga_structs)

We need to add the actual IntuiMessage struct with its fields:

```novus
pub struct IntuiMessage {
    im_NextMessage: *IntuiMessage,  // Exec message structure
    im_Class: u32,                   // IDCMP class
    im_Code: u16,                    // Event code
    im_Qualifier: u16,               // Shift/Alt/Ctrl/etc
    im_IAddress: *u8,                // Generic pointer (gadget, etc)
    im_MouseX: i16,                  // Mouse X coordinate
    im_MouseY: i16,                  // Mouse Y coordinate
    im_Seconds: u32,                 // Timestamp seconds
    im_Micros: u32,                  // Timestamp microseconds
    im_IDCMPWindow: *Window,         // Window pointer
    im_SpecialLink: *u8,             // Reserved
}
```

### 2. Exec Functions (std::ffi::exec)

We need `Wait()` function:

```novus
extern pub fn Wait(signalSet: u32) -> u32
```

### 3. MsgPort Helper Methods

We need to access the UserPort from a Window and get its signal bit:

```novus
// In std::ffi::amiga_structs or std::intuition

impl Window {
    pub fn user_port(&self) -> *MsgPort {
        unsafe {
            // Window.UserPort is at offset 86 in the struct
            let window_ptr = self as *Window as *u8
            let port_ptr_addr = window_ptr + 86
            *(*MsgPort)(port_ptr_addr)
        }
    }
}

impl MsgPort {
    pub fn signal_bit(&self) -> u8 {
        self.mp_SigBit
    }
}
```

**ISSUE:** We don't currently have the full Window struct definition, so we can't access UserPort directly. We have two options:

**Option A:** Define the full Window struct with all fields (tedious, fragile)
**Option B:** Add accessor functions to intuition.library FFI (safer)

```novus
// In std::ffi::intuition - add these functions:
extern pub fn GetWindowUserPort(window: *Window) -> *MsgPort
extern pub fn GetWindowIDCMPFlags(window: *Window) -> u32
```

Actually, let's check if we can just add inline asm to access the field directly without the full struct.

### 4. High-Level Event Enum (std::intuition)

```novus
pub enum WindowEvent {
    CloseWindow,
    RefreshWindow,
    NewSize {
        width: i16,
        height: i16
    },
    MouseMove {
        x: i16,
        y: i16,
        qualifier: u16
    },
    MouseDown {
        x: i16,
        y: i16,
        button: u16
    },
    MouseUp {
        x: i16,
        y: i16,
        button: u16
    },
    RawKey {
        code: u16,
        qualifier: u16
    },
    VanillaKey {
        key: u8
    },
    GadgetDown {
        gadget: *Gadget,
        x: i16,
        y: i16
    },
    GadgetUp {
        gadget: *Gadget,
        x: i16,
        y: i16
    },
    MenuPick {
        menu_number: u16
    },
    ActiveWindow,
    InactiveWindow,
    ChangeWindow,
    Unknown {
        class: u32,
        code: u16
    }
}
```

### 5. RAII Message Wrapper (std::intuition)

```novus
/// Safe wrapper around IntuiMessage that auto-replies on drop
pub struct IntuiMessageHandle {
    msg: *IntuiMessage,
    replied: bool,
}

impl IntuiMessageHandle {
    /// Create handle from raw message pointer
    /// SAFETY: Caller must ensure msg is valid
    unsafe fn from_raw(msg: *IntuiMessage) -> IntuiMessageHandle {
        IntuiMessageHandle {
            msg: msg,
            replied: false,
        }
    }

    /// Get the message class (IDCMP type)
    pub fn class(&self) -> u32 {
        unsafe { (*self.msg).im_Class }
    }

    /// Get the event code
    pub fn code(&self) -> u16 {
        unsafe { (*self.msg).im_Code }
    }

    /// Get the qualifier (shift/alt/ctrl)
    pub fn qualifier(&self) -> u16 {
        unsafe { (*self.msg).im_Qualifier }
    }

    /// Get mouse X position
    pub fn mouse_x(&self) -> i16 {
        unsafe { (*self.msg).im_MouseX }
    }

    /// Get mouse Y position
    pub fn mouse_y(&self) -> i16 {
        unsafe { (*self.msg).im_MouseY }
    }

    /// Get the gadget address (for gadget events)
    pub fn gadget(&self) -> *Gadget {
        unsafe { (*self.msg).im_IAddress as *Gadget }
    }

    /// Convert to high-level WindowEvent
    pub fn to_event(&self) -> WindowEvent {
        let class = self.class()
        let code = self.code()
        let qualifier = self.qualifier()
        let x = self.mouse_x()
        let y = self.mouse_y()

        match class {
            IDCMP_CLOSEWINDOW => WindowEvent::CloseWindow,
            IDCMP_REFRESHWINDOW => WindowEvent::RefreshWindow,
            IDCMP_NEWSIZE => WindowEvent::NewSize { width: x, height: y },
            IDCMP_MOUSEMOVE => WindowEvent::MouseMove { x, y, qualifier },
            IDCMP_MOUSEBUTTONS => {
                if (code & $0080) != 0 {  // IECODE_UP_PREFIX
                    WindowEvent::MouseUp { x, y, button: code & $007F }
                } else {
                    WindowEvent::MouseDown { x, y, button: code }
                }
            },
            IDCMP_RAWKEY => WindowEvent::RawKey { code, qualifier },
            IDCMP_VANILLAKEY => WindowEvent::VanillaKey { key: code as u8 },
            IDCMP_GADGETDOWN => WindowEvent::GadgetDown {
                gadget: self.gadget(),
                x,
                y
            },
            IDCMP_GADGETUP => WindowEvent::GadgetUp {
                gadget: self.gadget(),
                x,
                y
            },
            IDCMP_MENUPICK => WindowEvent::MenuPick { menu_number: code },
            IDCMP_ACTIVEWINDOW => WindowEvent::ActiveWindow,
            IDCMP_INACTIVEWINDOW => WindowEvent::InactiveWindow,
            IDCMP_CHANGEWINDOW => WindowEvent::ChangeWindow,
            _ => WindowEvent::Unknown { class, code }
        }
    }

    /// Manually reply to the message (prevents auto-reply on drop)
    pub fn reply(&mut self) {
        if !self.replied {
            unsafe {
                GT_ReplyIMsg(self.msg)
            }
            self.replied = true
        }
    }
}

impl Drop for IntuiMessageHandle {
    fn drop(&mut self) {
        // Auto-reply if not already replied
        self.reply()
    }
}
```

### 6. Event Iterator (std::intuition)

```novus
pub struct WindowEvents<'a> {
    window: &'a Window,
}

impl<'a> Iterator for WindowEvents<'a> {
    type Item = WindowEvent

    fn next(&mut self) -> Option<WindowEvent> {
        unsafe {
            // Get the window's user port
            let user_port = self.window.user_port()

            // Wait for signal
            Wait(1 << user_port.signal_bit())

            // Get message
            let msg_ptr = GT_GetIMsg(user_port)
            if msg_ptr == 0 as *IntuiMessage {
                return None  // No message
            }

            // Wrap in RAII handle
            let msg_handle = IntuiMessageHandle::from_raw(msg_ptr)

            // Convert to event
            let event = msg_handle.to_event()

            // msg_handle drops here, auto-replies
            Some(event)
        }
    }
}

impl Window {
    /// Get an iterator over window events
    /// This will block waiting for events
    pub fn events(&self) -> WindowEvents {
        WindowEvents { window: self }
    }
}
```

## Implementation Strategy

### Phase 1: Missing FFI Pieces (REQUIRED)

1. **Add IntuiMessage struct definition** to `std/ffi/amiga_structs.novus`
   - Full struct with all fields

2. **Add Wait() function** to `std/ffi/exec.novus`
   - Check if it exists, add if missing

3. **Add Window.UserPort accessor**
   - Either via full struct definition
   - Or via inline asm field access
   - Or via helper function

4. **Add MsgPort.mp_SigBit accessor**
   - Already in struct, just verify

### Phase 2: High-Level Wrappers (std::intuition)

1. **Add WindowEvent enum**
   - All IDCMP event types as variants

2. **Add IntuiMessageHandle struct**
   - RAII wrapper with Drop impl
   - to_event() converter

3. **Add WindowEvents iterator**
   - Implements Iterator trait
   - Calls Wait/GetIMsg/Reply automatically

4. **Add Window::events() method**
   - Returns WindowEvents iterator

### Phase 3: Examples and Tests

1. **Create simple event loop example**
   - Open window
   - Iterate over events
   - Handle close and refresh

2. **Create paint program example**
   - Mouse tracking
   - Drawing

3. **Test on real Amiga**
   - Verify Wait() works correctly
   - Verify message reply works
   - Check for memory leaks

## Level 1 vs Level 2 Usage

### Level 1: Low-Level (Traditional)

```novus
use std::ffi::intuition::{CloseWindow, RefreshWindowFrame}
use std::ffi::gadtools::{GT_GetIMsg, GT_ReplyIMsg}
use std::ffi::exec::Wait
use std::ffi::amiga_consts::*

pub fn traditional_loop(window: *Window) {
    let mut close_win = false

    while !close_win {
        unsafe {
            // Get user port
            let user_port = (*window).user_port()  // Or helper function

            // Wait for signal
            Wait(1 << user_port.signal_bit())

            // Get message
            let msg = GT_GetIMsg(user_port)
            let msg_class = (*msg).im_Class

            // Reply immediately (traditional pattern)
            GT_ReplyIMsg(msg)

            // Handle event
            if msg_class == IDCMP_CLOSEWINDOW {
                close_win = true
            } else if msg_class == IDCMP_REFRESHWINDOW {
                RefreshWindowFrame(window)
            }
        }
    }
}
```

### Level 2: Safe Iterator (RECOMMENDED)

```novus
use std::intuition::{WindowEvent}
use std::ffi::intuition::{RefreshWindowFrame}

pub fn modern_loop(window: *Window) -> Result<(), NovusError> {
    // Iterator automatically handles Wait/GetIMsg/Reply
    for event in (*window).events() {
        match event {
            WindowEvent::CloseWindow => {
                break  // Exit loop
            }
            WindowEvent::RefreshWindow => {
                unsafe { RefreshWindowFrame(window) }
            }
            WindowEvent::MouseMove { x, y, .. } => {
                println("Mouse at {}, {}", x, y)
            }
            _ => {}
        }
        // Message automatically replied when event goes out of scope
    }

    Ok(())
}
```

## Key Decisions

### 1. IntuiMessage as Full Struct or Opaque?

**Decision: Full Struct**
- We need to access im_Class, im_Code, im_MouseX, im_MouseY, etc.
- These are stable ABI fields that won't change
- Simplifies implementation significantly

### 2. Window.UserPort Access

**Decision: Inline asm or helper function**
- Full Window struct is HUGE and fragile
- UserPort is at fixed offset 86
- Either:
  - Add inline asm to access field directly
  - Add GetWindowUserPort() FFI function
  - Investigate if vbcc has a way to access without full struct

**For now:** Assume we'll add proper accessor methods

### 3. Iterator Lifetime

**Decision: Borrow window for lifetime of iterator**
- Window must live at least as long as iterator
- Iterator borrows &Window, not *Window
- Prevents use-after-close bugs

### 4. Error Handling

**Decision: Iterator returns Option, not Result**
- None means no more messages (shouldn't happen with Wait)
- User can convert to Result if needed
- Keeps iterator simple

## Next Steps

1. **Check existing code** for Wait() and Window struct
2. **Add missing FFI pieces** to std/ffi/
3. **Implement WindowEvent enum** in std/intuition
4. **Implement IntuiMessageHandle** with Drop
5. **Implement WindowEvents iterator**
6. **Write example** and test on UAE
7. **Test on real hardware**

## Open Questions

1. **How do we handle multiple windows?**
   - Could wait on multiple signal bits with OR mask
   - Would need to check which port has messages
   - Future enhancement for Level 4 (async)

2. **What about menu events with MenuPick?**
   - Need ItemAddress() to decode menu number
   - Add to WindowEvent::MenuPick helper?

3. **How do we handle IDCMP_INTUITICKS?**
   - Timer events every 1/10 second
   - Useful for animations
   - Add as WindowEvent::IntuiTick?

4. **Mouse button decoding**
   - Code $68 = left button down
   - Code $E8 = left button up (has $80 flag)
   - Code $69 = right button down
   - Need to decode properly

5. **Do we need select-key from RAWKEY?**
   - RawKey provides code + qualifier
   - VanillaKey provides ASCII
   - Most apps use VanillaKey for text input
