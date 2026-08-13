# Amiga event loops

Novus exposes event handling at the same three levels as the rest of the Amiga library.

## Application API

Normal applications use `amiga::ui::Window` and the typed `Event` enum:

```novus
from amiga::ui import Event, Window

fn run(window: &Window) {
    forever {
        match window.wait_event() {
            Event::Close => { break },
            Event::GadgetUp(id, code) => handle_action(id, code),
            Event::MenuPick(menu, item) => handle_menu(menu, item),
            Event::Refresh => {},
            _ => {},
        }
    }
}
```

`wait_event()` waits on the window port, obtains the GadTools message, copies its useful data into `Event`, and replies to the native message before returning. Application code cannot forget the reply or retain an invalid message pointer.

The owning `Window` closes itself and drains pending messages from `Drop`, including early-return and `?` error paths.

## Systems API

NDK-aware code uses `amiga::sys::gadtools::GadToolsWindow` or `amiga::sys::intuition::WindowHandle`. These types retain the Amiga subsystem model while owning cleanup.

An application window can step down temporarily:

```novus
let system = window.system()
custom_window_operation(system)
// `window` still owns the native window and remains usable.
```

Controls returned by `window.control(id)`, `window.checkbox(id)`, and related methods borrow the window. Their `as_raw()` escape hatch does not transfer ownership.

## Raw NDK API

Code that deliberately manages native message lifetimes imports `amiga::raw::exec`, `amiga::raw::gadtools`, and `amiga::raw::intuition`. That path is `unsafe` because every `GT_GetIMsg()` result must be paired with exactly one `GT_ReplyIMsg()` before the message storage becomes invalid.

Use the raw layer only when a systems or application event cannot express the required operation. Add the missing typed event when the same pattern is broadly useful.
