# BufferedWindow Quick Reference

**Module**: `std::ui::buffered_window`
**Target**: Modern Amigas (68040+, 16MB+ RAM)

---

## Basic Usage

```novus
from std::ui::buffered_window import BufferedWindow
from std::ui::window import WindowEvent

// Create window
let mut window = BufferedWindow::new("My App", 640, 480)?

// Draw to buffer
let ctx = window.draw_context()?
ctx.clear(0)
ctx.pen(1)
ctx.rect_fill(&Rect::from_pos_size(10, 10, 100, 100))

// Show buffer
window.present()?

// Event loop
forever {
    match window.wait_event() {
        WindowEvent::Close => break,
        WindowEvent::Resize(w, h) => {
            redraw(&window)?
            window.present()?
        },
        WindowEvent::Refresh(_) => {
            // Auto-handled! Do nothing.
        },
        _ => {}
    }
}
```

---

## Key Points

### ✅ DO
- Draw to buffer via `draw_context()`
- Call `present()` after drawing
- Redraw on `Resize` event
- Use for modern Amigas (16MB+ RAM)

### ❌ DON'T
- Draw directly to window RastPort
- Handle `Refresh` manually (it's automatic!)
- Forget to call `present()` after drawing
- Use on memory-constrained systems

---

## API Cheat Sheet

### Creation
```novus
let window = BufferedWindow::new(title, width, height)?
```

### Drawing
```novus
let ctx = window.draw_context()?  // Get DrawContext
let rp = window.buffer_rastport()  // Get RastPort (advanced)
```

### Presentation
```novus
window.present()?  // Blit buffer → window
window.clear(0)    // Clear buffer to color
```

### Events
```novus
let event = window.wait_event()           // Blocking
let opt = window.poll_event()             // Non-blocking
```

### Info
```novus
let (w, h) = window.buffer_size()         // Get dimensions
let handle = window.window_handle()       // Get WindowHandle
```

---

## Event Handling

| Event | Auto-Handled? | Developer Action |
|-------|---------------|------------------|
| `Close` | No | Break loop |
| `Resize(w,h)` | Partial | Redraw + present |
| `Refresh(_)` | **YES** | Nothing! (optional log) |
| `MouseButton` | No | Handle input |
| `RawKey` | No | Handle input |

**Refresh is automatic!** Buffer is blitted to window before event is returned.

---

## Memory Usage

```
Buffer = width × height × depth / 8 bytes
```

| Resolution | Depth | Size |
|------------|-------|------|
| 320×200 | 8 | 62 KB |
| 640×480 | 8 | 300 KB |
| 800×600 | 8 | 468 KB |
| 1024×768 | 8 | 768 KB |

On 16MB Amiga: 768KB = 4.7% RAM (trivial)

---

## Common Patterns

### Redraw Function
```novus
fn redraw_content(window: &BufferedWindow) -> Result<(), GraphicsError> {
    let ctx = window.draw_context()?
    ctx.clear(0)
    // ... draw content ...
    window.present()?
    return Result::Ok(())
}
```

### Animation Loop
```novus
while running {
    // Poll events (non-blocking)
    match window.poll_event() {
        Option::Some(WindowEvent::Close) => running = false,
        _ => {}
    }

    // Update & draw
    update_state()
    draw_frame(&window)?
    window.present()?

    // Frame limit
    WaitTOF()  // ~50Hz PAL
}
```

### Interactive Drawing
```novus
WindowEvent::MouseButton(x, y, pressed) => {
    if pressed {
        let ctx = window.draw_context()?
        ctx.pen(2)
        ctx.circle(x, y, 10)
        window.present()?
    }
}
```

---

## Troubleshooting

### Problem: Content disappears on resize
**Solution**: You must redraw after resize:
```novus
WindowEvent::Resize(w, h) => {
    draw_my_content(&window)?  // Redraw to new buffer
    window.present()?           // Show it
}
```

### Problem: Content flickers
**Solution**: Draw to buffer, then present (don't draw directly to window)
```novus
// WRONG
let ctx = DrawContext::from_window(window.window_handle().handle())?

// RIGHT
let ctx = window.draw_context()?
```

### Problem: High memory usage
**Solution**: BufferedWindow allocates width×height×depth buffer. For large windows, use manual buffering with dirty rectangles.

### Problem: Content doesn't update
**Solution**: Call `present()` after drawing:
```novus
let ctx = window.draw_context()?
ctx.rect_fill(&rect)
window.present()?  // Don't forget!
```

---

## Performance Tips

1. **Partial updates**: Manually call `BltBitMapRastPort()` for changed regions
2. **Triple buffering**: Advanced users can allocate extra buffers manually
3. **Frame limiting**: Use `WaitTOF()` for 50/60 Hz sync
4. **Blitter**: `present()` uses Blitter (2-4× faster than CPU copy)

---

## See Also

- **Full Design**: `docs/BufferedWindow_Design.md`
- **Examples**:
  - `Novus.Tests/Examples/buffered_window_demo.novus`
  - `Novus.Tests/Examples/buffered_window_animation.novus`
- **Related Modules**:
  - `std::ui::window` - Underlying window management
  - `std::graphics::draw` - DrawContext for rendering
  - `std::graphics::bitmap` - BitMapHandle RAII wrapper

---

**Minimum System**: A4000, 68040, 16MB RAM
**Recommended**: A1200/A4000, 68060, 32MB RAM
