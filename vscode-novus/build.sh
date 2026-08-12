#!/bin/bash
#
# Novus VS Code Extension Build Script
# Builds the language server and packages the VS Code extension
#
# Usage:
#   ./build.sh              # Full build and package
#   ./build.sh --install    # Build, package, and install in VS Code
#   ./build.sh --clean      # Clean build artifacts
#

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Directories
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LSP_PROJECT="$PROJECT_ROOT/Novus.LanguageServer"
EXTENSION_DIR="$SCRIPT_DIR"
BUNDLE_DIR="$EXTENSION_DIR/server"
TARGET_FRAMEWORK="net10.0"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Novus VS Code Extension Build${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Parse arguments
INSTALL=false
CLEAN=false

for arg in "$@"; do
    case $arg in
        --install)
            INSTALL=true
            ;;
        --clean)
            CLEAN=true
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --install    Install extension in VS Code after building"
            echo "  --clean      Clean build artifacts"
            echo "  --help       Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $arg${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Clean function
clean() {
    echo -e "${YELLOW}Cleaning build artifacts...${NC}"

    if [ -d "$LSP_PROJECT/bin" ]; then
        rm -rf "$LSP_PROJECT/bin"
        echo "  ✓ Removed $LSP_PROJECT/bin"
    fi

    if [ -d "$LSP_PROJECT/obj" ]; then
        rm -rf "$LSP_PROJECT/obj"
        echo "  ✓ Removed $LSP_PROJECT/obj"
    fi

    if [ -d "$EXTENSION_DIR/out" ]; then
        rm -rf "$EXTENSION_DIR/out"
        echo "  ✓ Removed $EXTENSION_DIR/out"
    fi

    if [ -d "$BUNDLE_DIR" ]; then
        rm -rf "$BUNDLE_DIR"
        echo "  ✓ Removed $BUNDLE_DIR"
    fi

    if [ -f "$EXTENSION_DIR/novus-language-support-0.1.0.vsix" ]; then
        rm "$EXTENSION_DIR/novus-language-support-0.1.0.vsix"
        echo "  ✓ Removed novus-language-support-0.1.0.vsix"
    fi

    echo -e "${GREEN}Clean complete!${NC}"
}

# If clean flag is set, clean and exit
if [ "$CLEAN" = true ]; then
    clean
    exit 0
fi

# Step 1: Build Language Server
echo -e "${BLUE}[1/4] Building Novus Language Server...${NC}"
cd "$PROJECT_ROOT"

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}ERROR: dotnet not found${NC}"
    echo "Please install .NET 10.0 SDK: https://dotnet.microsoft.com/download"
    exit 1
fi

echo "  Running: dotnet build -c Release $LSP_PROJECT/Novus.LanguageServer.csproj"
if dotnet build -c Release "$LSP_PROJECT/Novus.LanguageServer.csproj" --nologo --verbosity quiet; then
    echo -e "${GREEN}  ✓ Language server built successfully${NC}"
else
    echo -e "${RED}  ✗ Language server build failed${NC}"
    exit 1
fi

# Verify binary exists
LSP_BINARY="$LSP_PROJECT/bin/Release/$TARGET_FRAMEWORK/Novus.LanguageServer"
if [ ! -f "$LSP_BINARY" ]; then
    echo -e "${RED}  ✗ Language server binary not found at $LSP_BINARY${NC}"
    exit 1
fi

echo "  ✓ Binary: $LSP_BINARY"

echo "  Running language server regression tests..."
if dotnet test -c Release "$PROJECT_ROOT/Novus.LanguageServer.Tests/Novus.LanguageServer.Tests.csproj" --nologo --verbosity quiet; then
    echo -e "${GREEN}  ✓ Language server tests passed${NC}"
else
    echo -e "${RED}  ✗ Language server tests failed${NC}"
    exit 1
fi

# Bundle the matching server, dependencies, and standard library. Letting an
# installed extension discover an arbitrary old development build makes valid
# current syntax look broken in the editor.
rm -rf "$BUNDLE_DIR"
mkdir -p "$BUNDLE_DIR"
cp -R "$LSP_PROJECT/bin/Release/$TARGET_FRAMEWORK/." "$BUNDLE_DIR/"
cp -R "$PROJECT_ROOT/Novus/std" "$BUNDLE_DIR/std"
echo "  ✓ Bundled server and stdlib: $BUNDLE_DIR"
echo ""

# Step 2: Install npm dependencies
echo -e "${BLUE}[2/4] Installing npm dependencies...${NC}"
cd "$EXTENSION_DIR"

if ! command -v npm &> /dev/null; then
    echo -e "${RED}ERROR: npm not found${NC}"
    echo "Please install Node.js: https://nodejs.org/"
    exit 1
fi

if [ ! -d "node_modules" ]; then
    echo "  Running: npm install"
    if npm install --silent; then
        echo -e "${GREEN}  ✓ Dependencies installed${NC}"
    else
        echo -e "${RED}  ✗ npm install failed${NC}"
        exit 1
    fi
else
    echo "  ✓ Dependencies already installed (node_modules exists)"
fi
echo ""

# Step 3: Compile TypeScript
echo -e "${BLUE}[3/4] Compiling TypeScript...${NC}"
echo "  Running: npm run compile"
if npm run compile --silent; then
    echo -e "${GREEN}  ✓ TypeScript compiled successfully${NC}"
else
    echo -e "${RED}  ✗ TypeScript compilation failed${NC}"
    exit 1
fi

# Verify output exists
if [ ! -f "out/extension.js" ]; then
    echo -e "${RED}  ✗ Compiled output not found at out/extension.js${NC}"
    exit 1
fi

echo "  ✓ Output: out/extension.js"
echo ""

# Step 4: Package extension
echo -e "${BLUE}[4/4] Packaging VS Code extension...${NC}"

if ! command -v vsce &> /dev/null; then
    echo -e "${YELLOW}  WARNING: vsce not found${NC}"
    echo "  Installing vsce globally..."
    if npm install -g @vscode/vsce --silent; then
        echo -e "${GREEN}  ✓ vsce installed${NC}"
    else
        echo -e "${RED}  ✗ Failed to install vsce${NC}"
        exit 1
    fi
fi

echo "  Running: vsce package --allow-missing-repository"
if vsce package --allow-missing-repository > /dev/null 2>&1; then
    echo -e "${GREEN}  ✓ Extension packaged successfully${NC}"
else
    echo -e "${RED}  ✗ Extension packaging failed${NC}"
    exit 1
fi

# Verify VSIX exists
VSIX_FILE="$EXTENSION_DIR/novus-language-support-0.1.0.vsix"
if [ ! -f "$VSIX_FILE" ]; then
    echo -e "${RED}  ✗ VSIX file not found at $VSIX_FILE${NC}"
    exit 1
fi

VSIX_SIZE=$(du -h "$VSIX_FILE" | cut -f1)
echo "  ✓ Package: $VSIX_FILE ($VSIX_SIZE)"
echo ""

# Step 5: Install (if requested)
if [ "$INSTALL" = true ]; then
    echo -e "${BLUE}[5/5] Installing extension in VS Code...${NC}"

    if ! command -v code &> /dev/null; then
        echo -e "${RED}ERROR: 'code' command not found${NC}"
        echo "Please install VS Code or add it to your PATH"
        exit 1
    fi

    echo "  Running: code --install-extension $VSIX_FILE --force"
    if code --install-extension "$VSIX_FILE" --force > /dev/null 2>&1; then
        echo -e "${GREEN}  ✓ Extension installed successfully${NC}"
        echo ""
        echo -e "${YELLOW}  ⚠️  Please reload VS Code to activate the updated extension${NC}"
        echo -e "${YELLOW}     Command Palette → 'Developer: Reload Window'${NC}"
    else
        echo -e "${RED}  ✗ Extension installation failed${NC}"
        exit 1
    fi
fi

# Summary
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  Build Complete! ✓${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Language Server: $LSP_BINARY"
echo "Extension Package: $VSIX_FILE"
echo ""

if [ "$INSTALL" = false ]; then
    echo "To install, run:"
    echo "  code --install-extension $VSIX_FILE --force"
    echo ""
    echo "Or rebuild with --install flag:"
    echo "  ./build.sh --install"
fi
