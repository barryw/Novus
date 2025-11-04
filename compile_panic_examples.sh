#!/bin/bash
# Compile panic examples for Amiga testing
# This uses the Novus compiler test infrastructure

set -e

PROJECT_ROOT="/Users/barry/RiderProjects/Novus"
COMPILER="$PROJECT_ROOT/Novus/bin/Debug/net9.0/Novus.dll"
EXAMPLES_DIR="$PROJECT_ROOT/Novus.Tests/Examples"
AMIGA_DIR="/Users/barry/Emulation/Amiga/A4000-DH0/Barry"

cd "$PROJECT_ROOT"

echo "=== Building Panic Examples for Amiga ==="
echo ""

# Check compiler exists
if [ ! -f "$COMPILER" ]; then
    echo "ERROR: Compiler not found. Building..."
    dotnet build Novus/Novus.csproj
fi

compile_example() {
    local example_name=$1
    local source_file="$EXAMPLES_DIR/${example_name}.novus"

    if [ ! -f "$source_file" ]; then
        echo "ERROR: Source file not found: $source_file"
        return 1
    fi

    echo "Building ${example_name}..."

    # Use the compiler test helper
    dotnet run --project Novus/Novus.csproj -- compile-standalone \
        "$source_file" \
        "$AMIGA_DIR/$example_name" \
        --debug 2>&1 | grep -v "^Determining projects" | grep -v "^  " || true

    if [ -f "$AMIGA_DIR/$example_name" ]; then
        echo "✓ Created $AMIGA_DIR/$example_name"
        # Also copy the C source for inspection
        if [ -f "${example_name}.c" ]; then
            cp "${example_name}.c" "$AMIGA_DIR/"
            echo "✓ Copied ${example_name}.c"
        fi
    else
        echo "✗ Failed to create $example_name"
    fi

    echo ""
}

# Build the panic examples
compile_example "panic_pass_example"
compile_example "panic_fail_example"
compile_example "panic_defer_example"

echo "=== Done! ==="
echo ""
echo "Test binaries copied to: $AMIGA_DIR"
echo ""
echo "On Amiga, run:"
echo "  panic_pass_example  - Should exit with code 5 (no panic)"
echo "  panic_fail_example  - Should show panic GUI dialog"
echo "  panic_defer_example - Should show panic GUI dialog after cleanup"
