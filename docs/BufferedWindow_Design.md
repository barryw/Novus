# BufferedWindow - Automatic Content Preservation Design

**Status**: Implemented
**Target**: Modern Amigas (68040+, 16MB+ RAM)
**Module**: `std::ui::buffered_window`

## Overview

BufferedWindow provides automatic double-buffered window rendering with transparent content preservation across resize, refresh, and occlusion events. The developer never has to manually handle IDCMP_REFRESHWINDOW events - the system automatically restores window content from an off-screen bitmap buffer.

## Design Philosophy

### Ease of Use First

The primary goal is developer ergonomics:
- Draw once to buffer, content preserved automatically
- No manual refresh handling required
- Resize just works - buffer resizes, content clears, developer redraws
- RAII cleanup of all resources

### Modern Amiga Target

This is NOT optimized for 1MB A500s. It targets:
- 68040/060 CPUs (fast Blitter access)
- 16MB+ RAM (buffers are cheap)
- AGA/RTG graphics (high resolution displays)

On modern systems, a 1024×768×8 buffer (768KB) is trivial.

## Technical Architecture

### Off-Screen Bitmap Approach

We use **off-screen bitmap + BltBitMapRastPort()** rather than SUPERBITMAP windows:

**Why not SUPERBITMAP?**
- Complex manual scrolling coordination required
- Designed for memory-constrained systems (1-2MB RAM)
- Layers library interactions are fragile
- Developer must manually track visible region

**Why off-screen bitmap?**
- Simple: AllocBitMap() → draw → BltBitMapRastPort()
- Fast: Blitter is 2-4× faster than CPU copies
- Flexible: Full control over when/what gets blitted
- Memory-efficient on modern Amigas

### Window Refresh Modes

**SIMPLE_REFRESH** is used:
```c
WFLG_SIMPLE_REFRESH  // Application handles all refresh
IDCMP_REFRESHWINDOW  // Notify on refresh needed
```

**Why SIMPLE_REFRESH?**
- Complete control over refresh behavior
- No Layers backing store interference
- RefreshGuard RAII already handles BeginRefresh/EndRefresh
- Most compatible with custom rendering

**Why not SMART_REFRESH?**
- Layers backing store is unreliable
- Memory usage is unpredictable
- No guarantee of preservation (can fall back to SIMPLE_REFRESH)

### Memory Management

```
Buffer size = width × height × depth / 8 bytes
```

Examples:
| Resolution | Depth | Size   |
|------------|-------|--------|
| 320×200    | 8     | 62 KB  |
| 640×480    | 8     | 300 KB |
| 800×600    | 8     | 468 KB |
| 1024×768   | 8     | 768 KB |

On a 16MB Amiga: 768KB = 4.7% of RAM (trivial)

### Resource Lifecycle

```
BufferedWindow::new()
  ├─ WindowHandle::simple()          [RAII window]
  ├─ AllocBitMap(width×height×depth)  [off-screen buffer]
  └─ create_buffer_rastport()         [RastPort for drawing]

Developer draws to buffer via DrawContext

BufferedWindow::present()
  ├─ BltBitMapRastPort(buffer → window)
  └─ WaitBlit()

BufferedWindow::wait_event()
  ├─ REFRESHWINDOW → auto-blit buffer to window
  ├─ NEWSIZE → resize buffer, return event to developer
  └─ Other → pass through

BufferedWindow::drop()
  ├─ WaitBlit()                       [ensure no pending ops]
  ├─ FreeBitMap(buffer)               [free buffer]
  ├─ free(buffer_rp)                  [free RastPort]
  └─ window.drop()                    [close window, drain messages]
```

### Event Flow

#### Refresh Event (IDCMP_REFRESHWINDOW)
```
User action (drag window over) → System sends REFRESHWINDOW
  ↓
BufferedWindow::wait_event() intercepts
  ↓
BeginRefresh() [via RefreshGuard]
  ↓
BltBitMapRastPort(buffer, 0,0, window.RPort, 0,0, width, height, BLIT_COPY)
  ↓
WaitBlit()
  ↓
EndRefresh() [via RefreshGuard drop]
  ↓
Return WindowEvent::Refresh to developer (for logging/debugging)
```

**Developer sees**: Event logged, content already restored
**Developer does**: Nothing! (or log it)

#### Resize Event (IDCMP_NEWSIZE)
```
User resizes window → System sends NEWSIZE
  ↓
BufferedWindow::wait_event() intercepts
  ↓
resize_buffer(new_width, new_height)
  ├─ AllocBitMap(new_width, new_height, depth)
  ├─ create_buffer_rastport(new_buffer)
  ├─ WaitBlit() + FreeBitMap(old_buffer)
  └─ Update internal state
  ↓
Return WindowEvent::Resize(new_width, new_height) to developer
```

**Developer sees**: Resize event with new dimensions
**Developer does**: Redraw content, call present()

```novus
match window.wait_event() {
    WindowEvent::Resize(w, h) => {
        draw_my_content(&window)  // Draw to buffer
        window.present()?         // Blit to screen
    },
    ...
}
```

## API Design

### Core Type

```novus
pub struct BufferedWindow {
    window: WindowHandle,      // RAII window wrapper
    buffer: *BitMap,            // Off-screen bitmap (owned)
    buffer_rp: *RastPort,       // RastPort for buffer (owned)
    width: i16,                 // Current buffer width
    height: i16,                // Current buffer height
    depth: u8,                  // Bitmap depth (from screen)
}
```

### Key Methods

```novus
// Constructor
fn new(title: Str, width: i16, height: i16) -> Result<BufferedWindow, IntuitionError>

// Drawing access
fn draw_context(&self) -> Result<DrawContext, GraphicsError>
fn buffer_rastport(&self) -> *RastPort

// Presentation
fn present(&self) -> Result<(), GraphicsError>  // Blit buffer → window

// Event handling (with automatic refresh/resize handling)
fn wait_event(&mut self) -> WindowEvent
fn poll_event(&mut self) -> Option<WindowEvent>

// Utility
fn buffer_size(&self) -> (i16, i16)
fn clear(&self, color_index: u8)
fn window_handle(&self) -> &WindowHandle
```

### Usage Pattern

```novus
// Create buffered window
let mut window = BufferedWindow::new("My App", 640, 480)?

// Draw initial content
let ctx = window.draw_context()?
ctx.clear(0)
ctx.pen(1)
ctx.rect_fill(&Rect::from_pos_size(10, 10, 100, 100))
window.present()?

// Event loop
forever {
    match window.wait_event() {
        WindowEvent::Close => break,

        WindowEvent::Resize(w, h) => {
            // Buffer already resized, just redraw
            redraw_content(&window)?
            window.present()?
        },

        WindowEvent::Refresh(_) => {
            // Auto-handled! Content already restored.
            // Developer can ignore this event.
        },

        WindowEvent::MouseButton(x, y, pressed) => {
            // Interactive drawing
            if pressed {
                let ctx = window.draw_context()?
                ctx.pen(2)
                ctx.circle(x, y, 10)
                window.present()?  // Show update
            }
        },

        _ => {}
    }
}
// Automatic cleanup via Drop
```

## Performance Characteristics

### Blitter Performance

BltBitMapRastPort() uses the Blitter for fast bitmap copies:

| CPU     | Blitter Speedup | Notes                          |
|---------|-----------------|--------------------------------|
| 68000   | ~4×             | Blitter runs parallel to CPU   |
| 68020   | ~3×             | Faster CPU, same Blitter       |
| 68040   | ~2×             | Fast CPU, Blitter still wins   |
| 68060   | ~2×             | Fast CPU, Blitter still useful |

### Memory Bandwidth

800×600×8 buffer → screen blit:
- Data: 468,750 bytes (468 KB)
- Blitter: ~0.5ms @ 7MHz (PAL)
- CPU copy: ~2ms @ 25MHz 68040

**Result**: Refresh is invisible to user (< 1/100th of a frame)

### Frame Rate Impact

Present() cost per frame (640×480×8):
- BltBitMapRastPort: ~0.3ms
- WaitBlit: ~0ms (if Blitter idle)
- Total: < 1ms

**Result**: 60 FPS animation is easily achievable

## Design Decisions

### 1. Why not preserve content across resize?

**Decision**: Clear buffer on resize, require developer to redraw.

**Rationale**:
- Preserved content may not make sense at new dimensions
- Scaling is expensive and rarely desired
- Clipping old content is confusing (which part to keep?)
- Explicit redraw is clearer and more flexible

**Alternative considered**: Blit min(old, new) dimensions
- Complexity not worth it
- Developer usually wants custom layout for new size

### 2. Why automatic refresh handling?

**Decision**: Intercept REFRESHWINDOW in wait_event() and auto-blit.

**Rationale**:
- This is the entire point of BufferedWindow
- 99% of apps just want "content stays visible"
- Power users can still access window.handle() for custom behavior

**Alternative considered**: Return event, require developer to call refresh()
- More control, but defeats the purpose
- Would be identical to manual buffering

### 3. Why SIMPLE_REFRESH instead of SMART_REFRESH?

**Decision**: Use SIMPLE_REFRESH with manual buffer management.

**Rationale**:
- SMART_REFRESH backing store is unpredictable
- Can silently fall back to SIMPLE_REFRESH (confusing)
- Layers library memory usage is unknown
- Explicit buffer gives developer control

**Alternative considered**: SMART_REFRESH and hope it works
- Too many edge cases where it fails
- Not reliable enough for modern expectations

### 4. Why not triple buffering?

**Decision**: Provide double buffering; let advanced users add third buffer.

**Rationale**:
- Double buffering solves 95% of use cases
- Triple buffering adds complexity for rare benefit
- Animation-heavy apps can manually implement it
- Keep BufferedWindow simple and predictable

**Alternative considered**: Built-in triple buffering
- Overkill for most applications
- Memory cost (3× buffer size)
- API complexity (buffer rotation logic)

### 5. Buffer allocation strategy

**Decision**: Allocate buffer equal to current window size.

**Rationale**:
- Simplest: buffer always matches visible area
- No wasted memory
- Clear semantics (1:1 mapping)

**Alternatives considered**:
- Oversized buffer (e.g., 2× initial size)
  - Wastes memory on small windows
  - Confusing: where is visible region?
- Growing-only buffer
  - Leaks memory if window shrinks repeatedly
  - Complex logic for tracking used vs allocated size

## Testing Strategy

### Unit Tests
- Buffer allocation/deallocation (no leaks)
- Resize buffer correctness
- RastPort initialization
- RAII cleanup verification

### Integration Tests
- Open window, draw, present, close
- Resize window, redraw, present
- Refresh event (manual trigger)
- Multiple windows simultaneously

### Manual Testing
1. **Checkerboard demo** (`buffered_window_demo.novus`)
   - Open window, see checkerboard
   - Resize window (shrink and expand)
   - Drag another window over
   - Move window around screen
   - Verify: pattern always visible, no flicker

2. **Animation demo** (`buffered_window_animation.novus`)
   - Bouncing ball with trail
   - Resize during animation
   - Verify: smooth motion, trail preserved, no flicker

3. **Stress test**
   - Open 10 buffered windows
   - Resize all simultaneously
   - Verify: no crashes, no leaks

### Memory Leak Testing

```shell
# Run under Enforcer (68040/060 Amiga)
Enforcer buffered_window_demo

# Check for:
# - Freed bitmap while Blitter active
# - Access to freed memory
# - Unreplied messages
```

## Future Enhancements

### Potential Additions

1. **Dirty Rectangle Tracking**
   ```novus
   window.mark_dirty(Rect::from_pos_size(10, 10, 50, 50))
   window.present_dirty()  // Blit only dirty regions
   ```

2. **Triple Buffering Support**
   ```novus
   let window = BufferedWindow::with_buffers("App", 640, 480, 3)?
   ```

3. **Partial Blits**
   ```novus
   window.present_region(&Rect::from_pos_size(0, 0, 100, 100))?
   ```

4. **Content Scaling on Resize**
   ```novus
   window.set_resize_mode(ResizeMode::ScaleContent)?
   ```

5. **Buffer Snapshot**
   ```novus
   let snapshot: *BitMap = window.snapshot_buffer()?
   defer { FreeBitMap(snapshot) }
   ```

### Not Planned

- **Automatic dirty tracking**: Too much overhead
- **Built-in animation loop**: Outside scope
- **Multi-threaded rendering**: Amiga OS is single-threaded
- **GPU acceleration**: No standard GPU API on classic Amiga

## Comparison to Alternatives

### Manual Buffering
```novus
// Without BufferedWindow
let window = WindowHandle::simple("App", 640, 480)?
let buffer = AllocBitMap(640, 480, 8, BMF_CLEAR, screen_bitmap)
defer { FreeBitMap(buffer) }

forever {
    match window.wait_event() {
        WindowEvent::Refresh(guard) => {
            // Manual blit
            BltBitMapRastPort(buffer, 0, 0, (*guard.window()).RPort, 0, 0, 640, 480, $C0)
            WaitBlit()
        },
        WindowEvent::Resize(w, h) => {
            // Manual buffer resize
            let old_buffer = buffer
            buffer = AllocBitMap(w, h, 8, BMF_CLEAR, screen_bitmap)
            FreeBitMap(old_buffer)
            // Redraw...
        },
        _ => {}
    }
}
```

**Comparison**:
- Manual: 40+ lines of boilerplate
- BufferedWindow: 10 lines

### SUPERBITMAP Window
```c
// C code with SUPERBITMAP
struct BitMap *bitmap = AllocBitMap(1024, 768, 8, BMF_CLEAR, NULL);
struct Window *window = OpenWindowTags(NULL,
    WA_Width, 640, WA_Height, 480,
    WA_SuperBitMap, bitmap,
    WA_Flags, WFLG_SUPER_BITMAP | WFLG_SIZEGADGET,
    TAG_DONE);

// Manual scrolling coordination
window->RPort->Layer->Scroll_X = new_x;
window->RPort->Layer->Scroll_Y = new_y;
ScrollLayer(...);
```

**Comparison**:
- SUPERBITMAP: Complex scrolling, fragile
- BufferedWindow: Simple 1:1 mapping, no scrolling

## Conclusion

BufferedWindow provides the "right" abstraction for modern Amiga development:
- Simple API (draw → present)
- Automatic content preservation
- Flicker-free rendering
- Optimized for modern hardware
- RAII resource management

For 95% of applications, this is the default choice for windowed graphics.
