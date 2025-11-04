#!/bin/bash
# Compile panic examples and copy to Amiga shared drive

set -e

NOVUS="./Novus/bin/Debug/net9.0/Novus"
AMIGA_DIR="/Users/barry/Emulation/Amiga/A4000-DH0/Barry"
EXAMPLES_DIR="./Novus.Tests/Examples"

echo "=== Building Panic Test Examples for Amiga ==="
echo ""

compile_example() {
    local name=$1
    local source="$EXAMPLES_DIR/${name}.novus"
    local output="/tmp/${name}"

    echo "Compiling ${name}..."

    $NOVUS compile "$source" -o "$output" --cpu 68040 --verbose 2>&1 | tail -3

    if [ -f "$output" ]; then
        cp "$output" "$AMIGA_DIR/"
        echo "✓ Copied $name to Amiga drive"

        # Also copy C source if it exists
        if [ -f "${output}.c" ]; then
            cp "${output}.c" "$AMIGA_DIR/${name}.c"
            echo "✓ Copied ${name}.c for inspection"
        fi
    else
        echo "✗ Failed to build $name"
        return 1
    fi

    echo ""
}

# Build all three panic examples
compile_example "panic_pass_example"
compile_example "panic_fail_example"
compile_example "panic_defer_example"

echo "=== Build Complete! ==="
echo ""
echo "Binaries copied to: $AMIGA_DIR"
echo ""
echo "On Amiga (WinUAE/real hardware), run:"
echo ""
echo "  cd Barry:"
echo "  panic_pass_example  → Should exit cleanly (return code 5)"
echo "  panic_fail_example  → Should show GUI panic dialog: 'Division by zero!'"
echo "  panic_defer_example → Should show GUI panic dialog: 'Negative value not allowed!'"
echo ""
echo "Expected panic dialog format:"
echo "  Title: 'Novus Runtime Error'"
echo "  Message: 'PANIC: <message>'"
echo "  Location: 'File: ..., Line: ..., Column: ...'"
echo "  Button: 'OK'"
echo ""
