# Novus LSP Development Workflow

Complete guide to developing, testing, and debugging the Novus Language Server and VS Code extension.

## Project Structure

```
Novus/
├── Novus.Core/                    # Shared compiler library
│   ├── Parser/                    # ANTLR parser, error recovery
│   ├── SemanticAnalysis/          # Type checker, symbol resolution
│   ├── Diagnostics/               # Error/warning collection
│   └── ...
├── Novus.LanguageServer/          # LSP server (C# / .NET 10.0)
│   ├── Program.cs                 # Entry point, DI setup
│   ├── DocumentManager.cs         # Document state tracking
│   ├── TextDocumentHandler.cs     # LSP event handlers
│   └── Novus.LanguageServer.csproj
├── vscode-novus/                  # VS Code extension (TypeScript)
│   ├── src/extension.ts           # Extension entry point
│   ├── syntaxes/                  # TextMate grammar
│   ├── package.json               # Extension manifest
│   └── tsconfig.json              # TypeScript config
└── docs/
    ├── LSP_ARCHITECTURE.md        # Architecture documentation
    └── LSP_DEVELOPMENT.md         # This file
```

## Prerequisites

### Required Tools

1. **.NET 10.0 SDK**
   ```bash
   dotnet --version  # Should be 9.0.x
   ```

2. **Node.js 18+** and **npm**
   ```bash
   node --version    # Should be 18.x or higher
   npm --version
   ```

3. **VS Code**
   ```bash
   code --version
   ```

4. **vsce** (VS Code Extension Manager)
   ```bash
   npm install -g @vscode/vsce
   ```

### Recommended Tools

- **Visual Studio** or **Rider** (for C# debugging)
- **VS Code Extension Development** skills (helpful but not required)

## Development Workflow

### 1. Build the Language Server

```bash
cd /Users/barry/RiderProjects/Novus
dotnet build Novus.LanguageServer/Novus.LanguageServer.csproj
```

**Output**: `Novus.LanguageServer/bin/Debug/net10.0/Novus.LanguageServer`

**Verify**:
```bash
ls -la Novus.LanguageServer/bin/Debug/net10.0/Novus.LanguageServer
# Should show executable file
```

### 2. Build the VS Code Extension

```bash
cd vscode-novus
npm install        # First time only
npm run compile    # Compiles TypeScript to JavaScript
```

**Output**: `out/extension.js`

**Verify**:
```bash
ls -la out/extension.js
# Should show compiled JavaScript
```

### 3. Package the Extension

```bash
cd vscode-novus
vsce package --allow-missing-repository
```

**Output**: `novus-language-support-0.1.0.vsix`

**Flags**:
- `--allow-missing-repository`: Allows packaging without a git repository URL
- Future: Add proper repository URL to `package.json`

### 4. Install the Extension Locally

```bash
code --install-extension novus-language-support-0.1.0.vsix --force
```

**Flags**:
- `--force`: Overwrites existing installation

**Verify**:
```bash
code --list-extensions | grep novus
# Should show: novus.novus-language-support
```

### 5. Reload VS Code

After installing, reload VS Code to activate the updated extension:

1. Open Command Palette (`Cmd+Shift+P`)
2. Run: "Developer: Reload Window"

Or close and reopen VS Code.

### 6. Test the Extension

1. Create a test file: `test.novus`
2. Add code with intentional errors:
   ```novus
   from std::core import Result

   pub fn main() -> i32 {
       let x = 42
       let y = "hello"
       return x + y  // Type error!
   }
   ```
3. Verify red squiggles appear under the `+` operator
4. Hover to see error message: "cannot apply operator '+' to non-numeric type 'String'"

## One-Line Quick Rebuild

For rapid iteration, use this one-liner:

```bash
cd /Users/barry/RiderProjects/Novus && \
  dotnet build Novus.LanguageServer/Novus.LanguageServer.csproj && \
  cd vscode-novus && \
  npm run compile && \
  vsce package --allow-missing-repository && \
  code --install-extension novus-language-support-0.1.0.vsix --force
```

Then reload VS Code window.

## Debugging

### Debugging the Language Server (C#)

#### Method 1: Attach to Running Process (Easiest)

1. Open a `.novus` file in VS Code (starts the language server)
2. Find the process ID:
   ```bash
   ps aux | grep Novus.LanguageServer
   ```
3. In Rider or Visual Studio:
   - Run → Attach to Process
   - Search for "Novus.LanguageServer"
   - Click Attach
4. Set breakpoints in `TextDocumentHandler.cs` or `DocumentManager.cs`
5. Make changes in VS Code to trigger breakpoints

#### Method 2: Launch with Debugger Wait

1. Modify `src/extension.ts` to add debug flag:
   ```typescript
   const serverOptions: ServerOptions = {
       run: { command: serverPath, transport: TransportKind.stdio },
       debug: {
           command: serverPath,
           transport: TransportKind.stdio,
           options: { env: { WAIT_FOR_DEBUGGER: "1" } }
       }
   };
   ```

2. Add wait logic in `Program.cs`:
   ```csharp
   if (Environment.GetEnvironmentVariable("WAIT_FOR_DEBUGGER") == "1")
   {
       Console.Error.WriteLine("[LSP] Waiting for debugger...");
       while (!System.Diagnostics.Debugger.IsAttached)
       {
           System.Threading.Thread.Sleep(100);
       }
   }
   ```

3. Rebuild and install extension
4. Open `.novus` file in VS Code
5. Attach debugger (Method 1)
6. Debugger will break at first breakpoint

### Debugging the VS Code Extension (TypeScript)

1. Open `vscode-novus` folder in VS Code
2. Press `F5` (or Run → Start Debugging)
3. This opens a new "Extension Development Host" window
4. Open a `.novus` file in the development host
5. Set breakpoints in `src/extension.ts`
6. Extension code will break at breakpoints

**Console Logs**: View in Debug Console (Cmd+Shift+Y)

### Viewing Language Server Logs

The language server logs to stderr, which VS Code captures:

1. Open Output panel (`Cmd+Shift+U`)
2. Select "Novus Language Server" from dropdown
3. View real-time logs:
   ```
   [LSP] Novus Language Server starting...
   [LSP] Standard library path: /Users/barry/RiderProjects/Novus/Novus/std
   [LSP] Document opened: file:///path/to/test.novus
   [LSP] Parsing document: file:///path/to/test.novus
   [LSP] Publishing 1 diagnostics for file:///path/to/test.novus
   ```

**Tip**: Enable trace logging for more detail:
1. Command Palette → "Developer: Set Log Level..."
2. Select "Trace"

## Testing

### Manual Testing Checklist

After making changes, verify these scenarios:

- [ ] **Syntax highlighting works**
  - Keywords colored correctly
  - Strings, numbers, comments highlighted

- [ ] **Syntax errors detected**
  - Missing semicolons
  - Unmatched braces
  - Invalid tokens

- [ ] **Type errors detected**
  - Type mismatches
  - Invalid operations
  - Undefined variables

- [ ] **Real-time updates**
  - Errors appear as you type
  - Errors disappear when fixed
  - Multiple errors shown simultaneously

- [ ] **Standard library imports**
  - `from std::core import Result` works
  - `from std::io import println` works
  - Unknown modules show error

- [ ] **Hover over errors**
  - Shows full error message
  - Shows error code (if applicable)

- [ ] **Problems panel**
  - Lists all errors
  - Clicking error jumps to location

### Unit Testing (Future)

We'll add unit tests using xUnit:

```bash
# Create test project
dotnet new xunit -n Novus.LanguageServer.Tests

# Add reference to language server
cd Novus.LanguageServer.Tests
dotnet add reference ../Novus.LanguageServer/Novus.LanguageServer.csproj

# Run tests
dotnet test
```

**Test Coverage Targets**:
- DocumentManager: Open, Update, Close operations
- TextDocumentHandler: didOpen, didChange, didSave, didClose
- Diagnostic conversion: Novus format → LSP format
- Range validation: No negative line/column numbers

### Integration Testing (Future)

End-to-end tests that:
1. Launch language server
2. Send LSP messages (didOpen, didChange, etc.)
3. Verify diagnostic responses
4. Test with known-good and known-bad files

## Common Issues and Solutions

### Issue: Extension Not Activating

**Symptom**: Opening `.novus` file doesn't trigger extension

**Debug**:
1. Check extension is installed:
   ```bash
   code --list-extensions | grep novus
   ```
2. Check extension logs: Output → "Extension Host"
3. Look for activation errors

**Fix**:
- Ensure `package.json` has correct `activationEvents`:
  ```json
  "activationEvents": ["onLanguage:novus"]
  ```
- Reload window

### Issue: Language Server Not Starting

**Symptom**: Extension activates but no diagnostics appear

**Debug**:
1. Check language server logs: Output → "Novus Language Server"
2. Look for "Language Server not found" message
3. Verify server binary exists:
   ```bash
   ls -la /Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Debug/net10.0/Novus.LanguageServer
   ```

**Fix**:
- Rebuild language server: `dotnet build Novus.LanguageServer/`
- Check server path in `extension.ts`: `findServerPath()`

### Issue: Standard Library Not Found

**Symptom**: Errors like "module 'std::core' not found"

**Debug**:
1. Check language server log for: `[LSP] Standard library path: ...`
2. Verify directory exists:
   ```bash
   ls -la /Users/barry/RiderProjects/Novus/Novus/std/
   ```
3. Verify it contains `.novus` files:
   ```bash
   ls /Users/barry/RiderProjects/Novus/Novus/std/*.novus
   ```

**Fix**:
- Update `Program.cs` with correct path
- Ensure `possibleStdPaths` includes your setup

### Issue: Diagnostics Not Showing

**Symptom**: Code has errors but no red squiggles

**Debug**:
1. Check language server logs for:
   ```
   [LSP] Publishing X diagnostics for file://...
   ```
2. If X > 0 but no squiggles, check diagnostic ranges:
   ```
   [LSP]     Range: (5,15) to (5,16)
   ```
3. If range has negative numbers like `(-1,-1)`, that's the bug

**Fix**:
- Ensure `TextDocumentHandler.PublishDiagnostics()` validates ranges:
  ```csharp
  int line = Math.Max(0, diagnostic.Location.Line - 1);
  int col = Math.Max(0, diagnostic.Location.Column - 1);
  int length = Math.Max(1, diagnostic.Location.Length);
  ```

### Issue: Performance Lag

**Symptom**: Typing feels slow in large files

**Debug**:
1. Check file size:
   ```bash
   wc -l test.novus
   ```
2. Check language server logs for timing (add stopwatch logging)

**Fix**:
- Keep files under 1000 lines (workaround)
- Phase 2: Implement incremental parsing

## Making Changes

### Adding a New Diagnostic

1. **Add to semantic analyzer** (`Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`):
   ```csharp
   _diagnostics.Add(DiagnosticCode.MyNewError,
       "My error message",
       location,
       isError: true);
   ```

2. **Rebuild**:
   ```bash
   dotnet build Novus.Core/
   dotnet build Novus.LanguageServer/
   ```

3. **Test**:
   - Create `.novus` file that should trigger error
   - Verify error appears with correct message

### Adding a New LSP Capability

Example: Adding hover support

1. **Create handler** (`Novus.LanguageServer/HoverHandler.cs`):
   ```csharp
   public class HoverHandler : HoverHandlerBase
   {
       public override Task<Hover?> Handle(HoverParams request, CancellationToken token)
       {
           // Get symbol at position
           // Return hover content
       }
   }
   ```

2. **Register handler** (`Program.cs`):
   ```csharp
   .WithHandler<HoverHandler>()
   ```

3. **Rebuild and test**

### Modifying Syntax Highlighting

1. **Edit TextMate grammar** (`vscode-novus/syntaxes/novus.tmLanguage.json`)

2. **Add pattern**:
   ```json
   {
     "name": "keyword.control.novus",
     "match": "\\b(if|else|while)\\b"
   }
   ```

3. **Rebuild extension**:
   ```bash
   cd vscode-novus
   npm run compile
   vsce package --allow-missing-repository
   code --install-extension novus-language-support-0.1.0.vsix --force
   ```

4. **Reload VS Code and test**

## Performance Profiling

### Language Server Performance

Add stopwatch logging to measure performance:

```csharp
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... code to measure ...
sw.Stop();
Console.Error.WriteLine($"[LSP] Operation took {sw.ElapsedMilliseconds}ms");
```

### Extension Performance

Use VS Code's built-in profiler:

1. Command Palette → "Developer: Show Running Extensions"
2. View activation time, CPU usage
3. Look for slow extensions

## Release Checklist

Before releasing a new version:

- [ ] All tests pass (`dotnet test`)
- [ ] Manual testing checklist complete
- [ ] Version bumped in `package.json`
- [ ] CHANGELOG.md updated
- [ ] README.md updated with new features
- [ ] Documentation updated
- [ ] Language server binary included in extension (future)
- [ ] Extension packaged: `vsce package`
- [ ] Extension tested on clean VS Code install
- [ ] Git tag created: `git tag v0.1.0`
- [ ] Published to marketplace (future)

## CI/CD (Future)

Automated pipeline:

```yaml
# .github/workflows/lsp.yml
name: Build LSP
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '9.0.x'
      - run: dotnet build Novus.LanguageServer/
      - run: dotnet test Novus.LanguageServer.Tests/

  package:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - uses: actions/setup-node@v2
        with:
          node-version: '18'
      - run: cd vscode-novus && npm install
      - run: cd vscode-novus && npm run compile
      - run: cd vscode-novus && vsce package
      - uses: actions/upload-artifact@v2
        with:
          name: vsix
          path: vscode-novus/*.vsix
```

## Resources

- [LSP Specification](https://microsoft.github.io/language-server-protocol/)
- [OmniSharp.Extensions.LanguageServer](https://github.com/OmniSharp/csharp-language-server-protocol)
- [VS Code Extension API](https://code.visualstudio.com/api)
- [TextMate Grammar Guide](https://macromates.com/manual/en/language_grammars)
- [ANTLR 4 Documentation](https://www.antlr.org/)

## Getting Help

- Check existing documentation in `docs/`
- Review LSP architecture: `docs/LSP_ARCHITECTURE.md`
- Look at similar extensions for reference
- Ask in Novus development chat/Discord

---

**Happy LSP development!** 🚀
