# Novus LSP Quick Start Guide

Ultra-fast guide to get the Novus Language Server running in VS Code.

## TL;DR - Just Make It Work!

```bash
# Build and install everything
cd /Users/barry/RiderProjects/Novus/vscode-novus
./build.sh --install

# Reload VS Code
# Cmd+Shift+P → "Developer: Reload Window"

# Open a .novus file - done!
```

## Verify It's Working

1. Create `test.novus`:
   ```novus
   from std::core import Result

   pub fn main() -> i32 {
       let x = 42
       let y = "hello"
       return x + y  // Should show red squiggle!
   }
   ```

2. You should see:
   - ✅ Syntax highlighting
   - ✅ Red squiggle under `+`
   - ✅ Hover shows: "cannot apply operator '+' to non-numeric type 'String'"

## Troubleshooting

### No syntax highlighting?
- Check file extension is `.novus` (not `.txt`)
- Reload window: Cmd+Shift+P → "Developer: Reload Window"

### No red squiggles?
- Check Output panel: View → Output → "Novus Language Server"
- Look for: `[LSP] Publishing X diagnostics`
- If X is 0, there's a bug. If X > 0 but no squiggles, file an issue.

### "Language Server not found"?
```bash
# Rebuild the language server
cd /Users/barry/RiderProjects/Novus
dotnet build Novus.LanguageServer/
```

### "std::core not found"?
```bash
# Verify stdlib exists
ls /Users/barry/RiderProjects/Novus/Novus/std/
# Should show .novus files
```

## Development Iteration

When making changes to the LSP:

```bash
# One-liner rebuild (run from anywhere in the project)
cd /Users/barry/RiderProjects/Novus/vscode-novus && ./build.sh --install
```

Then reload VS Code.

## Running Tests

```bash
cd /Users/barry/RiderProjects/Novus
dotnet test Novus.LanguageServer.Tests/
```

Expected output:
```
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14
```

## Documentation

- **Architecture**: `docs/LSP_ARCHITECTURE.md`
- **Development**: `docs/LSP_DEVELOPMENT.md`
- **Complete Summary**: `docs/LSP_COMPLETE.md`
- **Extension README**: `vscode-novus/README.md`

## What Works (Phase 1)

✅ Syntax highlighting
✅ Syntax error detection
✅ Type error detection
✅ Real-time diagnostics
✅ Hover to see error messages
✅ Problems panel integration

## What Doesn't Work Yet (Phase 2)

❌ Code completion
❌ Go to definition
❌ Find references
❌ Hover type information
❌ Signature help

---

**Got issues?** Check `docs/LSP_DEVELOPMENT.md` for detailed troubleshooting.
