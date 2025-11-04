# Novus Language Support for VS Code

Official VS Code extension for the [Novus programming language](https://github.com/yourusername/novus) - a modern systems programming language for Amiga 68k.

**Tagline**: "New code for classic machines"

## Features

### ✅ Currently Supported (v0.1.0)

- **Syntax Highlighting**: Full syntax highlighting for `.novus` files
  - Keywords: `fn`, `let`, `mut`, `return`, `if`, `else`, `while`, `for`, `struct`, `impl`, `pub`, `from`, `import`, etc.
  - Types: `i8`, `i16`, `i32`, `u8`, `u16`, `u32`, `bool`, `String`, etc.
  - Comments: Line (`//`) and block (`/* */`) comments
  - String literals and escape sequences

- **Real-Time Diagnostics**: Errors and warnings as you type
  - Syntax errors (missing semicolons, unmatched braces, etc.)
  - Type errors (type mismatches, invalid operations, etc.)
  - Undefined variable/function errors
  - Import/module resolution errors

- **Bracket Matching**: Auto-closing and matching for:
  - Parentheses `()`
  - Braces `{}`
  - Brackets `[]`

- **Auto-Indentation**: Smart indentation based on context

### 🚧 Planned Features (Phase 2)

- **Code Completion**: IntelliSense for variables, functions, types
- **Go to Definition**: Jump to symbol definitions
- **Hover Documentation**: View type info and doc comments
- **Signature Help**: Function parameter hints
- **Find References**: Find all usages of a symbol
- **Rename Refactoring**: Rename symbols across files
- **Document Symbols**: Outline view of file structure
- **Code Actions**: Quick fixes for common errors

## Installation

### From VSIX (Development)

1. Download the latest `.vsix` file from releases
2. Open VS Code
3. Press `Cmd+Shift+P` (macOS) or `Ctrl+Shift+P` (Windows/Linux)
4. Type "Extensions: Install from VSIX..."
5. Select the downloaded `.vsix` file

### From VS Code Marketplace (Future)

Once published:

1. Open VS Code
2. Go to Extensions (`Cmd+Shift+X`)
3. Search for "Novus Language Support"
4. Click Install

## Requirements

### Language Server

This extension requires the Novus Language Server to function. The language server provides the intelligence (diagnostics, completion, etc.).

**Development Setup**: The language server is automatically discovered if you have the Novus compiler installed.

The extension searches for the language server in these locations (in order):

1. `/Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer` (Development)
2. `/Users/barry/RiderProjects/Novus/Novus.LanguageServer/bin/Release/net9.0/Novus.LanguageServer` (Development Release)
3. `../Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer` (Relative path)
4. `../server/Novus.LanguageServer` (Bundled with extension)

**Production Setup** (Future): The language server will be bundled with the extension.

### Standard Library

The language server requires access to the Novus standard library for import resolution and type checking.

The standard library is searched in:

1. Same directory as language server (`bin/Debug/net9.0/std`)
2. Relative to language server (`../../../../Novus/std`)
3. Absolute development path (`/Users/barry/RiderProjects/Novus/Novus/std`)

## Usage

### Creating a Novus File

1. Create a new file with the `.novus` extension
2. VS Code will automatically activate the Novus extension
3. Start typing - syntax highlighting and diagnostics will work automatically!

### Example: Hello World

```novus
from std::io import println

pub fn main() -> i32 {
    println("Hello, Amiga!")
    return 0
}
```

### Example: Type Error Detection

```novus
from std::core import Result

pub fn main() -> i32 {
    let x = 42
    let y = "hello"
    return x + y  // ERROR: Cannot apply operator '+' to non-numeric type 'String'
}
```

The extension will show a red squiggle under the `+` operator with the error message.

### Viewing Diagnostics

Errors and warnings appear in three places:

1. **Inline**: Red/yellow squiggles in the editor
2. **Hover**: Hover over squiggle to see error message
3. **Problems Panel**: View → Problems (or `Cmd+Shift+M`)

## Configuration

Currently, the extension works with zero configuration. Future versions will support:

- Custom language server path
- Custom standard library path
- Diagnostic severity levels
- Formatter options

## Development

### Building from Source

```bash
# Clone the Novus repository
git clone https://github.com/yourusername/novus.git
cd novus

# Build the language server
dotnet build Novus.LanguageServer/Novus.LanguageServer.csproj

# Build the VS Code extension
cd vscode-novus
npm install
npm run compile

# Package the extension
npm install -g @vscode/vsce
vsce package --allow-missing-repository

# Install locally
code --install-extension novus-language-support-0.1.0.vsix --force
```

### Debugging the Extension

1. Open `vscode-novus` folder in VS Code
2. Press `F5` to launch Extension Development Host
3. Open a `.novus` file in the development window
4. View language server logs: Output → "Novus Language Server"

### Language Server Logs

The language server logs diagnostic information to stderr, which appears in the VS Code Output panel:

```
[LSP] Novus Language Server starting...
[LSP] Standard library path: /Users/barry/RiderProjects/Novus/Novus/std
[LSP] Document opened: file:///path/to/test.novus
[LSP] Parsing document: file:///path/to/test.novus
[LSP] Parse completed. Parse errors: 0
[LSP] Semantic analysis completed. Total diagnostics: 1
[LSP] Publishing 1 diagnostics for file:///path/to/test.novus
```

## Troubleshooting

### Extension Not Activating

**Symptom**: `.novus` files show no syntax highlighting

**Fix**:
1. Check the file extension is exactly `.novus`
2. Reload window: `Cmd+Shift+P` → "Developer: Reload Window"
3. Check extension is installed: Extensions → Search for "Novus"

### No Error Diagnostics

**Symptom**: Code has errors but no red squiggles appear

**Fix**:
1. Check language server is running: Output → "Novus Language Server"
2. Look for errors in the log:
   - `WARNING: std library not found` → Standard library path is wrong
   - `Language Server not found` → Language server binary not found
3. Reload window to restart language server

### Language Server Not Found

**Symptom**: Extension activates but no diagnostics, log shows "Language Server not found"

**Fix**:
1. Build the language server:
   ```bash
   cd /Users/barry/RiderProjects/Novus
   dotnet build Novus.LanguageServer/Novus.LanguageServer.csproj
   ```
2. Verify the binary exists:
   ```bash
   ls -la Novus.LanguageServer/bin/Debug/net9.0/Novus.LanguageServer
   ```
3. Reload VS Code window

### Standard Library Not Found

**Symptom**: Errors about `module 'std::core' not found`

**Fix**:
1. Verify standard library exists:
   ```bash
   ls -la /Users/barry/RiderProjects/Novus/Novus/std/
   ```
2. Check language server log for: `[LSP] Standard library path: ...`
3. Ensure the path points to a directory containing `.novus` files

## Known Issues

### Performance on Large Files

Files over 5000 lines may experience lag when typing. This will be improved in Phase 2 with incremental parsing.

**Workaround**: Keep files under 1000 lines by splitting into modules.

### No Multi-File Support

The language server currently analyzes files independently. Cross-file symbol resolution doesn't work yet.

**Impact**: Imports from other files in your project may show "module not found" errors.

**Workaround**: Only standard library imports work in v0.1.0.

**Fix**: Phase 2 will add workspace/project support.

## Contributing

Contributions are welcome! Please see the main [Novus repository](https://github.com/yourusername/novus) for contribution guidelines.

### Reporting Issues

Please report issues on the [GitHub issue tracker](https://github.com/yourusername/novus/issues) with:

1. VS Code version
2. Extension version
3. Example `.novus` file that reproduces the issue
4. Language server logs (from Output panel)

## License

[Your License Here - e.g., MIT, Apache 2.0]

## Links

- [Novus Language Repository](https://github.com/yourusername/novus)
- [Language Design Document](https://github.com/yourusername/novus/blob/main/LanguageDesignDoc.md)
- [LSP Architecture Documentation](https://github.com/yourusername/novus/blob/main/docs/LSP_ARCHITECTURE.md)

## Changelog

### 0.1.0 - 2025-01-04

#### Added
- Initial release
- Syntax highlighting for Novus language
- Real-time error diagnostics (syntax and semantic)
- Bracket matching and auto-closing
- Auto-indentation
- Language server integration

#### Known Limitations
- No code completion yet
- No go-to-definition yet
- No workspace/multi-file support yet
- Performance may lag on files >1000 lines

---

**Made with ❤️ for the Amiga community**
