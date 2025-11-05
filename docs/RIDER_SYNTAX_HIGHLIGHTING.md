# Setting Up Syntax Highlighting for Novus in Rider

LSP4IJ provides language features (diagnostics, hover, completion) but **NOT** syntax highlighting. You need to set up a custom file type in Rider for that.

## Quick Setup (2 minutes)

### Step 1: Create Custom File Type

1. **Open Settings** (Cmd+, on Mac, Ctrl+Alt+S on Windows)
2. Navigate to **Editor → File Types**
3. Click **+** to add a new file type
4. Configure:

**Basic Info:**
- **Name:** `Novus`
- **Description:** `Novus programming language`
- **File name patterns:** `*.novus`

**Comment Settings:**
- **Line comment:** `//`
- **Block comment start:** `/*`
- **Block comment end:** `*/`
- **Hex prefix:** `0x`
- **Number postfixes:** (leave empty)
- **Support paired braces:** ✓ (checked)
- **Support paired brackets:** ✓ (checked)
- **Support string escapes:** ✓ (checked)

### Step 2: Add Keywords (Tab 1)

Click the **Keywords** tab and add these (one per line or space-separated):

```
as break const defer else enum extern false fn for from if impl import
let match mut pub return static struct trait true unsafe use var while
i8 i16 i32 i64 u8 u16 u32 u64 f32 f64 bool str void Self
```

### Step 3: Add Keywords (Tab 2) - Optional

If you want secondary highlighting for types, add:

```
Result Option Vec String Box
```

### Step 4: Configure Colors (Optional but Recommended)

While still in File Types settings, click the **Colors & Fonts** preview button or:
1. Settings → Editor → Color Scheme → Custom Language
2. Find "Novus" in the list
3. Customize colors:
   - **Keywords:** Bold, color: #CC7832 (orange)
   - **Numbers:** Color: #6897BB (blue)
   - **Strings:** Color: #6A8759 (green)
   - **Comments:** Italic, color: #808080 (gray)
   - **Braces:** Color: #FFD700 (yellow)

## Result

After setup, you'll get:
- ✅ Keywords highlighted (if, fn, struct, etc.)
- ✅ Comments grayed out
- ✅ Strings in green
- ✅ Numbers in blue
- ✅ Proper brace matching
- ✅ Auto-indent
- ✅ Line/block comment shortcuts (Cmd+/)

Combined with LSP4IJ, you get a **full IDE experience**!

## Alternative: Import TextMate Bundle (Advanced)

Rider has experimental TextMate bundle support, but it's not officially supported and may not work reliably. If you want to try it:

### Option 1: Use TextMate Bundles Plugin

1. Install **"TextMate Bundles Support"** plugin (if available)
2. This is experimental and may not work with all grammars

### Option 2: Convert to Rider's XML Format

You can convert the VSCode TextMate grammar to Rider's format, but it's complex:

1. The grammar is at: `vscode-novus/syntaxes/novus.tmLanguage.json`
2. Rider uses a different XML-based format for syntax highlighting
3. Manual conversion would be needed

**For now, the custom file type (Step 1-4 above) is the most reliable approach.**

## Comparison: Custom File Type vs TextMate

| Feature | Custom File Type | TextMate Grammar |
|---------|-----------------|------------------|
| Keywords | ✅ Full support | ✅ Full support |
| Comments | ✅ Full support | ✅ Full support |
| Strings | ✅ Basic support | ✅ Advanced (escapes, interpolation) |
| Numbers | ✅ Basic support | ✅ Advanced (hex, binary, etc.) |
| Context-aware | ❌ No | ✅ Yes |
| Setup time | 2 minutes | Complex |
| Reliability | ✅ Native | ⚠️ Experimental |

**Recommendation:** Use the custom file type. It's 90% as good and 100% reliable.

## Tips

**Keyboard Shortcuts with Custom File Type:**
- **Cmd+/ (Mac) / Ctrl+/** - Toggle line comment
- **Cmd+Shift+/ (Mac) / Ctrl+Shift+/** - Toggle block comment
- **Tab / Shift+Tab** - Indent / Outdent
- **Cmd+[ or ]** - Indent left/right

**Auto-format:**
The custom file type won't have semantic formatting, but you can:
1. Settings → Editor → Code Style → Novus (will be created)
2. Configure indentation, brace style, etc.

**Split View:**
- Right-click tab → Split Right
- Edit C# compiler code and Novus code side-by-side
- Both with full syntax highlighting!

## Testing Your Setup

1. Open `Novus/std/core.novus`
2. You should see:
   - `fn`, `pub`, `enum`, etc. highlighted as keywords
   - Comments in gray and italic
   - Strings in green
   - Numbers in blue
   - LSP diagnostics (wavy underlines) working

Perfect for compiler development! 🎨
