# Setting Up Novus Language Server in JetBrains Rider

This guide shows you how to edit Novus code in Rider with full language server support (syntax highlighting, autocomplete, diagnostics, etc.) alongside your C# compiler code.

## Prerequisites

- JetBrains Rider 2023.3 or later
- .NET 9.0 SDK
- Novus Language Server built (see below)

## Step 1: Build the Language Server

```bash
cd /Users/barry/RiderProjects/Novus
dotnet build Novus.LanguageServer/Novus.LanguageServer.csproj
```

The language server will be at:
```
Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer
```

## Step 2: Install LSP Support Plugin in Rider

1. Open Rider
2. Go to **Settings** → **Plugins**
3. Search for **"LSP Support"**
4. Install the plugin
5. Restart Rider

## Step 3: Configure the Novus Language Server

### Option A: Using Rider's LSP Settings UI

1. Go to **Settings** → **Languages & Frameworks** → **Language Server Protocol**
2. Click **+** to add a new server
3. Configure:
   - **Name:** `Novus`
   - **Extension/Language ID:** `novus`
   - **File extensions:** `*.novus`
   - **Command:** `/Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer`
   - **Arguments:** (leave empty)
4. Click **OK**

### Option B: Manual Configuration (if .idea config files work)

The project already has `.idea/novus-lsp.xml` and `.idea/fileTypes.xml` configured.

If Rider doesn't pick these up automatically:
1. Close Rider
2. Delete `.idea/` folder (it will regenerate)
3. Reopen project
4. Follow Option A above

## Step 4: Test It Out

1. Open any `.novus` file (try `Novus/std/core.novus`)
2. You should see:
   - ✅ Syntax highlighting
   - ✅ Error diagnostics (red squiggles)
   - ✅ Hover information
   - ✅ Go to definition
   - ✅ Code completion

## Troubleshooting

### Language Server Not Starting

**Check if LSP plugin is enabled:**
- Settings → Plugins → Look for "LSP Support" - should be enabled

**Check language server is executable:**
```bash
chmod +x /Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer
```

**Check language server runs manually:**
```bash
cd /Users/barry/RiderProjects/Novus
./Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer
```

You should see:
```
[LSP] Novus Language Server starting...
[LSP] Compiler directory: ...
[LSP] Standard library path: ...
```

Press Ctrl+C to stop it.

### No Syntax Highlighting

**Check file type association:**
1. Right-click a `.novus` file
2. Select **Associate with File Type...**
3. Choose **Text** or create a custom **Novus** file type

**Check language server logs:**
1. Settings → Languages & Frameworks → Language Server Protocol
2. Select Novus server
3. Click **Show Logs**

### Rebuild After Compiler Changes

If you modify the Novus compiler code:
```bash
dotnet build Novus.LanguageServer/Novus.LanguageServer.csproj
```

Then restart the language server:
1. Settings → Languages & Frameworks → Language Server Protocol
2. Select Novus
3. Click **Restart**

## Features Supported

The Novus Language Server currently provides:

✅ **Diagnostics** - Syntax errors, type errors, semantic errors
✅ **Hover** - Type information and documentation
✅ **Go to Definition** - Jump to function/type definitions
✅ **Code Completion** - Autocomplete for functions, types, keywords
✅ **Document Symbols** - Outline view of functions/types in file

## Advanced: Debugging the Language Server

To debug the language server while using it in Rider:

1. Add this to `Novus.LanguageServer/Program.cs`:
```csharp
#if DEBUG
Console.Error.WriteLine("Waiting for debugger... PID: " + Environment.ProcessId);
System.Threading.Thread.Sleep(10000); // 10 second delay
#endif
```

2. Rebuild the language server
3. Restart Rider
4. When Rider starts the LSP, you'll see the PID in Rider's logs
5. Attach Visual Studio/Rider debugger to that PID
6. Set breakpoints in language server code

## Tips

**Split Editor Layout:**
- Open C# compiler code on the left
- Open Novus code on the right
- Edit both side-by-side with full IDE support!

**File Watchers:**
- Settings → Tools → File Watchers
- Add watcher for `*.novus` files to auto-format or lint on save

**Quick Open:**
- Cmd+Shift+O (Mac) / Ctrl+Shift+N (Windows)
- Type filename to quickly open any `.novus` file

## Known Issues

- **First startup slow:** The language server compiles all stdlib on first request
- **Large files:** May take a moment to analyze
- **Syntax highlighting basic:** Uses TextMate grammar from VSCode extension (limited)

## Next Steps

Consider creating a proper Rider plugin (instead of using LSP) for:
- Native syntax highlighting
- Better integration with Rider's UI
- Custom inspections and quick-fixes
- Integrated compiler output

But for now, LSP support gives you excellent editing experience!
