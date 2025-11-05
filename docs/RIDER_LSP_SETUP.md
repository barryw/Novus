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

## Step 2: Install LSP4IJ Plugin in Rider

1. Open Rider
2. Go to **Settings** → **Plugins** (Cmd+, on Mac, Ctrl+Alt+S on Windows)
3. Click **Marketplace** tab
4. Search for **"LSP4IJ"**
5. Click **Install** on the LSP4IJ plugin
6. Click **Restart IDE** when prompted

## Step 3: Configure the Novus Language Server

### Method 1: Using LSP4IJ Console Settings (Recommended)

1. Go to **Settings** → **Languages & Frameworks** → **Language Servers** (or search "Language Servers" in settings)
2. Under **Server Definitions**, click **+** to add a new server
3. Fill in the configuration:
   - **Language:** Create new or select "Text"
   - **Server name:** `Novus Language Server`
   - **Command:**
     ```
     /Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer
     ```
   - **File name patterns:** `*.novus`
   - **Configuration:** (leave empty)
4. Click **OK**
5. Click **Apply**

### Method 2: Create Language Server Mapping File

Create a file at `.idea/languageServers.xml`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<project version="4">
  <component name="LanguageServerMappings">
    <server id="novus-lsp">
      <executable>
        <command>/Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer</command>
      </executable>
      <mappings>
        <file pattern="*.novus" />
      </mappings>
    </server>
  </component>
</project>
```

Then restart Rider.

### Method 3: Associate File Type First

Sometimes LSP4IJ works better if you set up the file type association first:

1. **Create custom file type:**
   - Settings → Editor → File Types
   - Click **+** to add new file type
   - Name: `Novus`
   - Line comment: `//`
   - Block comment start: `/*`
   - Block comment end: `*/`
   - Add pattern: `*.novus`
   - Click OK

2. **Then configure language server** using Method 1 above, selecting "Novus" as the language

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

**Check if LSP4IJ plugin is enabled:**
- Settings → Plugins → Look for "LSP4IJ" - should be enabled and active

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
1. Settings → Languages & Frameworks → Language Servers
2. Find Novus Language Server in the list
3. Check the **Status** column - should show "Running"
4. Click on the server to see logs in the bottom panel

**Check LSP4IJ console:**
1. View → Tool Windows → LSP Console
2. You should see language server startup messages
3. Look for errors in red

### Rebuild After Compiler Changes

If you modify the Novus compiler code:
```bash
dotnet build Novus.LanguageServer/Novus.LanguageServer.csproj
```

Then restart the language server:
1. Settings → Languages & Frameworks → Language Servers
2. Find "Novus Language Server"
3. Click **Restart** button (or just close/reopen the `.novus` file)

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
