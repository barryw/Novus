# ⚠️ Novus Resource Safety - Important Warnings

## DO NOT Wrap System-Owned Resources!

Novus RAII types (`FileHandle`, `DirLock`, `Signal`, `MsgPort`, `BitMap`, `CopperList`) provide automatic cleanup for resources **that you own**. However, some AmigaOS resources are owned by the system and should **NEVER** be wrapped in RAII types.

Wrapping system-owned resources will cause **double-free crashes** when both your code and the system try to free the same resource!

---

## ❌ DO NOT Wrap These Resources

### Intuition Resources

#### Window.UserPort
**Owner**: Intuition (freed by `CloseWindow()`)

❌ **WRONG**:
```novus
struct Window {
    handle: *Window,
    user_port: MsgPort,  // ❌ CRASH! Double-free!
}
```

✅ **CORRECT**:
```novus
struct Window {
    handle: *Window,
    // Don't wrap UserPort - use raw pointer
}

impl Window {
    pub fn user_port(&self) -> *MsgPort {
        unsafe { (*self.handle).UserPort }
    }
}
```

#### Screen.BitMap
**Owner**: Intuition (freed by `CloseScreen()`)

❌ **WRONG**:
```novus
let screen_bitmap = BitMap::from_raw(unsafe { &(*screen.as_raw()).BitMap });  // ❌ CRASH!
```

✅ **CORRECT**:
```novus
// Use raw pointer for screen bitmap
let screen_bitmap: *BitMap = unsafe { &(*screen.as_raw()).BitMap };
```

---

### Graphics Resources

#### ViewPort.UCopList
**Owner**: Graphics library (freed when View is closed)

❌ **WRONG**:
```novus
let ucop = unsafe { CINIT((*vp).UCopIns, 100) };
let copper = CopperList::new(ucop, true);  // ❌ CRASH! ViewPort owns this!
```

✅ **CORRECT**:
```novus
// Build copper list directly, don't wrap it
let ucop = unsafe { CINIT((*vp).UCopIns, 100) };
// Use raw pointer - ViewPort will free it
```

---

### DOS Resources

#### Standard I/O Handles
**Owner**: Process (freed when process exits)

Functions: `Input()`, `Output()`, `Error()`

❌ **WRONG**:
```novus
let stdout = FileHandle::from_raw(unsafe { Output() });  // ❌ CRASH!
```

✅ **CORRECT**:
```novus
fn print_to_stdout(msg: *u8) {
    let stdout = unsafe { Output() };  // Raw BPTR
    unsafe { FPuts(stdout, msg) }
    // Don't close - process owns it!
}
```

---

### Exec Resources

#### System Signals
**Owner**: System (pre-allocated, never freed)

Signals: `SIGBREAKF_CTRL_C`, `SIGBREAKF_CTRL_D`, `SIGBREAKF_CTRL_E`, `SIGBREAKF_CTRL_F`

❌ **WRONG**:
```novus
let ctrl_c = Signal::from_raw(SIGBREAKF_CTRL_C);  // ❌ CRASH! System signal!
```

✅ **CORRECT**:
```novus
fn wait_for_ctrl_c() {
    let mask = SIGBREAKF_CTRL_C | SIGBREAKF_CTRL_D;
    let received = unsafe { Wait(mask) };  // Raw API
    // Don't wrap system signals!
}
```

---

## ✅ When to Use RAII Types

Use RAII types (`FileHandle`, `MsgPort`, etc.) **ONLY** when:

1. **You allocated the resource** (via `Open()`, `CreateMsgPort()`, `AllocBitMap()`, etc.)
2. **You own the resource** (you are responsible for freeing it)
3. **The resource should be freed** when it goes out of scope

### Examples of Correct Usage

#### FileHandle
✅ You called `Open()`:
```novus
let file = FileHandle::open("S:Startup-Sequence", MODE_OLDFILE)?;
// file.drop() will call Close() - CORRECT!
```

❌ System handle:
```novus
let stdout = unsafe { Output() };  // Don't wrap!
```

#### MsgPort
✅ You called `CreateMsgPort()`:
```novus
let port = MsgPort::create()?;
// port.drop() will call DeleteMsgPort() - CORRECT!
```

❌ Intuition's port:
```novus
let user_port = unsafe { (*window).UserPort };  // Don't wrap!
```

#### BitMap
✅ You called `AllocBitMap()`:
```novus
let bitmap = BitMap::alloc(320, 256, 5, BMF_CLEAR, 0)?;
// bitmap.drop() will call FreeBitMap() - CORRECT!
```

❌ Screen's bitmap:
```novus
let screen_bm = unsafe { &(*screen).BitMap };  // Don't wrap!
```

#### Signal
✅ You called `AllocSignal()`:
```novus
let signal = Signal::alloc()?;
// signal.drop() will call FreeSignal() - CORRECT!
```

❌ System signal:
```novus
let ctrl_c_mask = SIGBREAKF_CTRL_C;  // Don't wrap!
```

---

## 🔍 How to Tell Who Owns a Resource

### Rule of Thumb

Ask yourself: **"Did I allocate this with an Alloc* or Create* function?"**

- ✅ **YES** → You own it → Use RAII type
- ❌ **NO** → System owns it → Use raw pointer

### Common Ownership Patterns

| You Own (Use RAII) | System Owns (Use Raw) |
|-------------------|----------------------|
| `Open()` file handles | `Input()`, `Output()`, `Error()` |
| `CreateMsgPort()` ports | `Window->UserPort` |
| `AllocBitMap()` bitmaps | `Screen->BitMap` |
| `AllocSignal()` signals | `SIGBREAKF_CTRL_C` |
| `MemoryBlock::alloc()` memory | ViewPort copper lists |
| `Lock()` directory locks | System assigns |

---

## 🛡️ Escape Hatches

All RAII types provide escape hatches for advanced use cases:

### from_raw() - Take Ownership
```novus
// Wrap an existing resource (only if you OWN it!)
let file = FileHandle::from_raw(fh);
// file.drop() will close it
```

⚠️  **WARNING**: Only use `from_raw()` if you **own** the resource and it should be freed!

### into_raw() - Relinquish Ownership
```novus
// Extract raw handle and prevent drop
let file = FileHandle::open("file", MODE_OLDFILE)?;
let fh = file.into_raw();  // Transfer ownership
// You must now manually Close(fh)
```

Use `into_raw()` when:
- Passing ownership to another system
- Storing handle beyond current scope
- Need manual control over cleanup

---

## 📚 Reference: All System-Owned Resources

### Intuition (intuition.library)
- `Window->UserPort` - Message port for IDCMP messages
- `Screen->BitMap` - Screen's bitmap
- `Screen->RastPort` - Screen's rastport
- `Window->RPort` - Window's rastport

### Graphics (graphics.library)
- `View->ViewPort` - ViewPorts in a View
- `ViewPort->RasInfo->BitMap` - Display bitmap
- `ViewPort->UCopIns` - User copper list

### DOS (dos.library)
- `Input()` - Process stdin
- `Output()` - Process stdout
- `Error()` - Process stderr
- `Cli()->cli_CommandDir` - Current directory lock

### Exec (exec.library)
- `SIGBREAKF_CTRL_C/D/E/F` - System break signals
- `FindTask(NULL)->tc_UserData` - Task user data
- System lists (ExecBase->LibList, DeviceList, etc.)

---

## ⚡ Quick Safety Checklist

Before wrapping a resource in an RAII type, ask:

1. ✅ Did I allocate this with `Alloc*()`, `Create*()`, `Open()`, or `Lock()`?
2. ✅ Am I responsible for freeing it?
3. ✅ Will it be freed when this scope ends?

If **ALL THREE** are YES → Use RAII type

If **ANY** are NO → Use raw pointer

---

## 💡 When in Doubt

**If you're not sure who owns a resource:**

1. Check the NDK Autodocs for the function that returned it
2. Look for a corresponding `Free*()`, `Delete*()`, `Close*()`, or `UnLock*()` function
3. If there's no cleanup function → System owns it, don't wrap!
4. If there IS a cleanup function → Check who should call it:
   - You call it → Use RAII
   - System calls it → Use raw pointer

---

## 🎓 Learning Resources

### NDK Documentation
- Intuition: `autodocs/intuition.doc` - Window/Screen ownership
- Graphics: `autodocs/graphics.doc` - Bitmap/ViewPort ownership
- DOS: `autodocs/dos.doc` - File handle ownership
- Exec: `autodocs/exec.doc` - Signal/Port ownership

### Example Code Patterns
See `examples/` directory for correct usage patterns.

---

**Remember**: RAII is powerful but only for resources **you own**. When in doubt, use raw pointers and manual cleanup - it's better to be explicit than to crash!
