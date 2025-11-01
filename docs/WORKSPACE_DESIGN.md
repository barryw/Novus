# Novus Workspace/Solution Design

## The Problem

Currently, `novus.toml` represents a single package. But what if you want:
- Multiple executables in one project (e.g., CLI tool + Workbench GUI)
- A library + example programs
- Multiple related packages that should build together

## Proposed Solution: Two-Level Structure

### Level 1: **Workspace** (`Novus.toml` - capital N)
- Contains multiple packages
- Like .NET solutions or Cargo workspaces
- Defines workspace-level settings

### Level 2: **Package** (`novus.toml` - lowercase n)
- Individual buildable target
- Lives in `packages/` subdirectory
- Can be: cli, workbench, library, device, etc.

---

## File Structure

### Option A: Workspace Root (Recommended)

```
my-amiga-project/              # Workspace root
├── Novus.toml                 # Workspace configuration (capital N!)
├── packages/
│   ├── cli-tool/              # Package 1: CLI application
│   │   ├── novus.toml
│   │   └── src/
│   │       └── main.novus
│   ├── gui-app/               # Package 2: Workbench application
│   │   ├── novus.toml
│   │   └── src/
│   │       └── main.novus
│   └── shared-lib/            # Package 3: Shared library
│       ├── novus.toml
│       └── src/
│           └── lib.novus
├── build/                     # All build outputs go here
└── .gitignore
```

### Option B: Simple Project (Single Package)

```
my-simple-app/                 # Single package (no workspace)
├── novus.toml                 # Package configuration
├── src/
│   └── main.novus
└── build/
```

---

## Novus.toml (Workspace)

```toml
[workspace]
name = "my-amiga-suite"
version = "1.0.0"
authors = ["Your Name <you@example.com>"]

# List of packages in this workspace
members = [
    "packages/cli-tool",
    "packages/gui-app",
    "packages/shared-lib"
]

# Default build settings for all packages
[workspace.build]
target_cpu = "68020"
fpu = "auto"
optimization_level = 2

# Shared dependencies (all packages inherit these)
[workspace.dependencies]
# future: shared dependencies here
```

---

## novus.toml (Package) - Updated

```toml
[package]
name = "cli-tool"
version = "0.1.0"
type = "cli"                   # cli, workbench, dual, library, device
description = "Command-line tool"
authors = ["Barry"]

# Can override workspace settings
[build]
target_cpu = "68020"           # Override workspace default
optimization_level = 3         # Higher optimization for this package

[paths]
src = "src"

# Package-specific dependencies
[dependencies]
shared-lib = { path = "../shared-lib" }  # Reference other package in workspace
```

---

## Command Changes

### Creating Projects

```bash
# Create a new workspace (multi-package project)
novusc new my-project --workspace

# This creates:
# my-project/
# ├── Novus.toml (workspace)
# └── packages/
```

# Then add packages to the workspace
```bash
cd my-project
novusc new packages/cli-tool --type cli
novusc new packages/gui-app --type workbench
novusc new packages/mylib --type library
```

# Or create a simple single-package project (current behavior)
```bash
novusc new my-simple-app --type cli

# This creates:
# my-simple-app/
# ├── novus.toml (package)
# └── src/main.novus
```

### Building

```bash
# In a workspace: build all packages
cd my-project
novusc build

# Build specific package in workspace
novusc build --package cli-tool

# In a single-package project: build the package
cd my-simple-app
novusc build
```

---

## Terminology

| Term | Meaning | File | Example |
|------|---------|------|---------|
| **Workspace** | Container for multiple packages | `Novus.toml` | "my-amiga-suite" |
| **Package** | Single buildable target | `novus.toml` | "cli-tool.exe" |
| **Target** | What gets built | defined in package | cli, workbench, library |
| **Solution** | (Alternate term for workspace) | | .NET terminology |

---

## Benefits

### 1. **Shared Code**
```
workspace/
├── packages/
│   ├── core/           # Shared library
│   ├── cli/            # Uses core
│   └── gui/            # Uses core
```

### 2. **Related Tools**
```
workspace/
├── packages/
│   ├── compiler/       # Main tool
│   ├── formatter/      # Companion tool
│   └── lsp-server/     # IDE support
```

### 3. **Examples**
```
workspace/
├── packages/
│   ├── mylib/          # The library
│   └── examples/
│       ├── basic/
│       └── advanced/
```

---

## Implementation Plan

### Phase 1: Single Package (DONE ✅)
- ✅ `novusc new my-app` creates single package
- ✅ `novus.toml` with `[package]` section
- ✅ Project types: cli, workbench, library, device

### Phase 2: Workspace Support (NEXT)
1. Add `Novus.toml` workspace parser
2. Implement `--workspace` flag for `novusc new`
3. Update `novusc build` to detect workspace vs package
4. Build all packages in workspace
5. Handle package dependencies within workspace

### Phase 3: Advanced (FUTURE)
- Package dependencies between workspace members
- Build ordering based on dependencies
- Shared build cache
- Incremental builds

---

## Detection Logic

```csharp
// When running `novusc build`:

1. Check for Novus.toml in current directory
   - If found: Build all packages in workspace
   - If not found: Check for novus.toml

2. Check for novus.toml in current directory
   - If found: Build single package
   - If not found: Error "No project found"
```

---

## Backward Compatibility

**Important:** Existing single-package projects keep working!

```toml
# Old style (still works)
[package]
name = "my-app"
# ...

# New workspace style (optional)
[workspace]
members = ["packages/my-app"]
```

---

## Real-World Examples

### Example 1: Novus Compiler Itself

```
novus-compiler/
├── Novus.toml (workspace)
├── packages/
│   ├── novusc/         # Main compiler
│   ├── novus-fmt/      # Code formatter
│   ├── novus-lsp/      # Language server
│   └── stdlib/         # Standard library
```

### Example 2: Game with Tools

```
my-amiga-game/
├── Novus.toml
├── packages/
│   ├── game/           # The game (workbench)
│   ├── level-editor/   # Level editor (workbench)
│   ├── asset-packer/   # CLI tool
│   └── game-engine/    # Shared library
```

### Example 3: Simple CLI Tool (No Workspace)

```
my-tool/
├── novus.toml          # Just a package
└── src/main.novus
```

---

## Recommendation

**Start simple, add complexity later:**

1. **Phase 1 (NOW):** Single-package projects work great
2. **Phase 2 (LATER):** Add workspace support when needed
3. Users can convert single package → workspace later if needed

**For most users:** Single-package projects are perfect!
**For complex projects:** Workspaces provide organization.

---

## Migration Path

Convert single package to workspace:

```bash
# Before: my-app/ with novus.toml

# After:
mkdir -p packages/my-app
mv src packages/my-app/
mv novus.toml packages/my-app/
cat > Novus.toml << EOF
[workspace]
members = ["packages/my-app"]
EOF
```

---

**Decision:** Implement workspace support in Phase 2, after basic `novusc new` works well!
