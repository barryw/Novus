# Novus Language Server Protocol (LSP) Architecture

## Overview

The Novus Language Server provides IDE intelligence for the Novus programming language through the Language Server Protocol (LSP). It enables real-time syntax checking, semantic analysis, type checking, and error diagnostics in any LSP-compatible editor.

**Status**: Phase 1 Complete (Diagnostics)
**Created**: 2025-01-04

## Architecture

### High-Level Design

```
┌─────────────────────────────────────────────────────────────┐
│                     VS Code Extension                       │
│  (vscode-novus/src/extension.ts)                           │
│  - Discovers and launches language server                  │
│  - Manages client-server connection                        │
└─────────────────┬───────────────────────────────────────────┘
                  │ LSP over stdio
                  │ (JSON-RPC messages)
┌─────────────────▼───────────────────────────────────────────┐
│              Novus.LanguageServer                           │
│  (C# / .NET 9.0 / OmniSharp.Extensions.LanguageServer)     │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Program.cs                                          │  │
│  │  - Entry point                                       │  │
│  │  - Discovers standard library path                  │  │
│  │  - Configures DI container                          │  │
│  │  - Registers handlers                               │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  DocumentManager                                     │  │
│  │  - Tracks open documents (uri → DocumentState)      │  │
│  │  - Parses documents in LSP mode                     │  │
│  │  - Runs semantic analysis                           │  │
│  │  - Aggregates diagnostics                           │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  TextDocumentHandler                                 │  │
│  │  - didOpen: Open document, parse, publish diags     │  │
│  │  - didChange: Update document, re-parse             │  │
│  │  - didSave: Re-parse, publish diagnostics           │  │
│  │  - didClose: Remove document, clear diagnostics     │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  DocumentState                                       │  │
│  │  - Uri: Document identifier                         │  │
│  │  - Text: Current document content                   │  │
│  │  - Version: Document version number                 │  │
│  │  - ParseTree: ANTLR parse tree                      │  │
│  │  - Diagnostics: Errors and warnings                 │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────┬───────────────────────────────────────────┘
                  │ Uses
┌─────────────────▼───────────────────────────────────────────┐
│                    Novus.Core                               │
│  (Shared compiler library)                                  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  NovusParserFactory                                  │  │
│  │  - CreateParser(mode: Compilation | LanguageServer) │  │
│  │  - Two-mode parsing strategy                        │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  SemanticAnalyzer                                    │  │
│  │  - Type checking                                     │  │
│  │  - Symbol resolution                                │  │
│  │  - Import resolution                                │  │
│  │  - Generates semantic diagnostics                   │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  DiagnosticBag                                       │  │
│  │  - Collects errors and warnings                     │  │
│  │  - Source location tracking                         │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Key Design Decisions

### 1. **Compiler-as-Library Architecture**

We extracted the core compiler functionality into `Novus.Core`, a shared library used by both:
- `Novus` (CLI compiler)
- `Novus.LanguageServer` (LSP server)

**Rationale**: Ensures the LSP always uses the exact same parsing and semantic analysis logic as the compiler. No code duplication, no drift between IDE and compiler behavior.

### 2. **Two-Mode Parsing**

The `NovusParserFactory` supports two parsing modes:

- **Compilation Mode** (strict):
  - Stops on first error
  - Used by the CLI compiler
  - Fast-fail for build pipelines

- **LanguageServer Mode** (error-tolerant):
  - Continues parsing on errors
  - Builds partial AST for broken code
  - Enables code completion in incomplete files
  - Uses custom ANTLR error strategy

**Rationale**: IDEs need to provide intelligence even when code is incomplete or broken. Users expect autocomplete to work while typing, not just when code compiles.

### 3. **Standard Library Path Discovery**

The language server searches multiple locations for the standard library:

1. Same directory as LSP binary (`bin/Debug/net9.0/std`)
2. Relative path from LSP to compiler (`../../../Novus/std`)
3. Absolute development path (`/Users/barry/RiderProjects/Novus/Novus/std`)

**Rationale**: Support both development and production deployments without configuration.

### 4. **Diagnostic Range Validation**

All diagnostic ranges are validated to ensure:
- Line numbers are never negative (convert 0-based → 1-based carefully)
- Column numbers are never negative
- Lengths are at least 1 character

**Rationale**: Invalid ranges cause VS Code to silently drop diagnostics. Users see no errors, which is worse than showing incorrect ranges.

### 5. **Real-Time Analysis**

Documents are parsed and analyzed on every change:
- `didOpen`: Initial parse + analysis
- `didChange`: Incremental re-parse + analysis
- `didSave`: Full re-parse + analysis

**Rationale**: Provides immediate feedback. Users see errors as they type, not just on save.

## Data Flow

### Document Open Flow

```
1. User opens test.novus in VS Code
   ↓
2. VS Code sends didOpen notification to LSP
   ↓
3. TextDocumentHandler.Handle(didOpen)
   ↓
4. DocumentManager.Open(uri, text, version)
   ↓
5. DocumentManager.ParseDocument()
   ├─ NovusParserFactory.CreateParser(mode: LanguageServer)
   ├─ parser.compilationUnit() → ParseTree
   ├─ SemanticAnalyzer.Analyze(parseTree)
   └─ Merge parse + semantic diagnostics
   ↓
6. TextDocumentHandler.PublishDiagnostics(uri)
   ├─ Convert diagnostics to LSP format
   ├─ Validate ranges (no negatives)
   └─ Send PublishDiagnosticsParams to client
   ↓
7. VS Code receives diagnostics
   ↓
8. VS Code renders red squiggles
```

### Document Change Flow

```
1. User types in test.novus
   ↓
2. VS Code sends didChange notification
   ↓
3. TextDocumentHandler.Handle(didChange)
   ↓
4. DocumentManager.Update(uri, newText, newVersion)
   ↓
5. DocumentManager.ParseDocument() [same as above]
   ↓
6. TextDocumentHandler.PublishDiagnostics(uri)
   ↓
7. VS Code updates squiggles in real-time
```

## Error Recovery Strategy

### Syntax Errors (Parser)

The parser uses ANTLR's `BailErrorStrategy` in compilation mode and custom error recovery in LSP mode:

- **LSP Mode Error Recovery**:
  - Uses `NovusLspErrorStrategy` (extends `DefaultErrorStrategy`)
  - Attempts to recover from errors by skipping tokens
  - Inserts missing tokens when possible
  - Continues parsing to find more errors

### Semantic Errors (Type Checker)

The semantic analyzer is designed to be fault-tolerant:
- Wraps analysis in try-catch to prevent crashes
- Continues analyzing even after errors
- Reports all errors found, not just the first one

## Performance Considerations

### Current Implementation (Phase 1)

- **Per-file analysis**: Each file is analyzed independently
- **Full re-parse on change**: No incremental parsing yet
- **Synchronous analysis**: Blocks on parse + semantic analysis

**Performance Profile**:
- Small files (<1000 lines): Sub-100ms response
- Medium files (1000-5000 lines): 100-500ms response
- Large files (>5000 lines): May lag

### Future Optimizations (Phase 2+)

1. **Incremental Parsing**: Only re-parse changed regions
2. **Async Analysis**: Parse in background thread
3. **Caching**: Cache parse trees for unchanged files
4. **Project-Wide Analysis**: Understand module dependencies
5. **Debouncing**: Wait for typing to pause before re-parsing

## Testing Strategy

### Unit Tests

- **DocumentManager Tests**:
  - Open/Update/Close operations
  - Parse error handling
  - Diagnostic aggregation
  - Thread safety (concurrent document access)

- **TextDocumentHandler Tests**:
  - didOpen/didChange/didSave/didClose handling
  - Diagnostic conversion (Novus → LSP format)
  - Range validation
  - URI handling

### Integration Tests

- **End-to-End Diagnostics**:
  - Load test files with known errors
  - Verify correct diagnostics published
  - Verify correct line/column positions
  - Verify error messages

- **Standard Library Integration**:
  - Verify stdlib modules can be imported
  - Verify stdlib types are recognized
  - Verify stdlib functions are available

### Manual Testing Checklist

- [ ] Syntax errors show red squiggles
- [ ] Type errors show red squiggles
- [ ] Errors update in real-time while typing
- [ ] Errors clear when code is fixed
- [ ] Multiple errors can be shown simultaneously
- [ ] Hovering over error shows message
- [ ] Problems panel shows all errors
- [ ] Errors have correct line/column positions

## Extension Deployment

### Development Build

```bash
# Build language server
cd /Users/barry/RiderProjects/Novus
dotnet build Novus.LanguageServer/Novus.LanguageServer.csproj

# Build VS Code extension
cd vscode-novus
npm install
npm run compile

# Package extension
vsce package --allow-missing-repository

# Install locally
code --install-extension novus-language-support-0.1.0.vsix --force
```

### Production Build

Future work:
1. Copy language server binary into extension package
2. Bundle extension with webpack for smaller size
3. Publish to VS Code Marketplace
4. Automated CI/CD pipeline

## Phase 1 Capabilities (Current)

✅ **Implemented**:
- Document synchronization (open, change, save, close)
- Syntax error diagnostics
- Semantic error diagnostics (type checking)
- Real-time error updates
- Multi-error support
- Standard library import resolution

❌ **Not Yet Implemented**:
- Code completion (autocomplete)
- Go to definition
- Find references
- Hover documentation
- Signature help (function parameter hints)
- Code actions (quick fixes)
- Rename refactoring
- Document symbols (outline view)
- Workspace symbols (project-wide search)
- Formatting

## Phase 2 Roadmap

### P0 - Critical for Usability
1. **Code Completion**: Suggest variables, functions, types
2. **Hover Documentation**: Show type info and doc comments
3. **Go to Definition**: Jump to symbol definition

### P1 - High Value
4. **Signature Help**: Show function parameters while typing
5. **Document Symbols**: Outline view of functions/structs
6. **Find References**: Find all usages of a symbol

### P2 - Nice to Have
7. **Rename Refactoring**: Rename symbol across files
8. **Code Actions**: Quick fixes for common errors
9. **Formatting**: Auto-format code

## Known Issues

### Issue 1: No Incremental Parsing
**Impact**: Large files may lag on every keystroke
**Workaround**: Keep files under 1000 lines
**Fix**: Implement incremental parsing (Phase 2)

### Issue 2: No Project/Workspace Understanding
**Impact**: Multi-file projects don't resolve cross-file references
**Workaround**: Use single-file modules
**Fix**: Implement workspace symbol resolution (Phase 2)

### Issue 3: No Syntax Highlighting in Strings/Comments
**Impact**: String interpolation, escape sequences not highlighted
**Workaround**: None
**Fix**: Enhance TextMate grammar (vscode-novus)

## Debugging the Language Server

### Enable LSP Logging in VS Code

1. Open Command Palette (Cmd+Shift+P)
2. Run: "Developer: Set Log Level..."
3. Select "Trace"
4. View logs: Output panel → "Novus Language Server"

### Language Server Console Output

The server logs to `stderr` (visible in VS Code Output panel):

```
[LSP] Novus Language Server starting...
[LSP] Standard library path: /Users/barry/RiderProjects/Novus/Novus/std
[LSP] Document opened: file:///path/to/file.novus
[LSP] Parsing document: file:///path/to/file.novus
[LSP] Parse completed. Parse errors: 0
[LSP] Semantic analysis completed. Total diagnostics: 1
[LSP] Publishing 1 diagnostics for file:///path/to/file.novus
```

### Debug in Visual Studio / Rider

1. Open `Novus.LanguageServer` project
2. Set breakpoints in `TextDocumentHandler.cs` or `DocumentManager.cs`
3. Configure launch settings to wait for client connection
4. Launch VS Code extension in debug mode
5. Attach debugger to language server process

## Related Documentation

- [Language Design Doc](../LanguageDesignDoc.md) - Novus language specification
- [Testing Strategy](TESTING.md) - Overall testing approach
- [VS Code Extension README](../vscode-novus/README.md) - Extension user guide
- [Development Workflow](LSP_DEVELOPMENT.md) - How to build and test

## Contributors

- Barry (Initial implementation, architecture)

## Changelog

- **2025-01-04**: Phase 1 complete (diagnostics working)
- **2025-01-04**: Fixed diagnostic range validation
- **2025-01-04**: Fixed standard library path discovery
- **2025-01-04**: Created architecture documentation
