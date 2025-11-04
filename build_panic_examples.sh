#!/bin/bash
# Build panic examples for Amiga smoke testing

set -e

NOVUS="./Novus/bin/Debug/net9.0/Novus"
AMIGA_DIR="/Users/barry/Emulation/Amiga/A4000-DH0/Barry"
EXAMPLES_DIR="./Novus.Tests/Examples"

echo "Building panic examples..."

# Create a test that will panic
cat > /tmp/panic_test.novus << 'EOF'
from std::core import Result

pub fn divide(a: i32, b: i32) -> i32 {
    if b == 0 {
        panic!("Division by zero!")
    }
    return a / b
}

pub fn main() -> i32 {
    // This will trigger a panic
    let result = divide(10, 0)
    return result
}
EOF

# Create a test that won't panic (control)
cat > /tmp/panic_no_trigger.novus << 'EOF'
from std::core import Result

pub fn divide(a: i32, b: i32) -> i32 {
    if b == 0 {
        panic!("Division by zero!")
    }
    return a / b
}

pub fn main() -> i32 {
    // This is safe - no panic
    let result = divide(10, 2)
    return result
}
EOF

# Create a test with defer blocks
cat > /tmp/panic_with_defer.novus << 'EOF'
from std::core import Result
from std::io import println

pub fn test_cleanup() -> i32 {
    var cleanup_flag = 0
    defer cleanup_flag = 1

    let x = -1
    if x < 0 {
        panic!("Negative value not allowed!")
    }

    return 0
}

pub fn main() -> i32 {
    return test_cleanup()
}
EOF

echo "Building panic_test (will panic)..."
$NOVUS build /tmp/panic_test.novus --debug 2>&1 || true

echo "Building panic_no_trigger (safe)..."
$NOVUS build /tmp/panic_no_trigger.novus --debug 2>&1 || true

echo "Building panic_with_defer (panic with cleanup)..."
$NOVUS build /tmp/panic_with_defer.novus --debug 2>&1 || true

# Copy to Amiga drive
echo ""
echo "Copying binaries to Amiga shared drive..."
[ -f panic_test ] && cp panic_test "$AMIGA_DIR/" && echo "✓ Copied panic_test"
[ -f panic_no_trigger ] && cp panic_no_trigger "$AMIGA_DIR/" && echo "✓ Copied panic_no_trigger"
[ -f panic_with_defer ] && cp panic_with_defer "$AMIGA_DIR/" && echo "✓ Copied panic_with_defer"

# Also copy the C source for inspection
[ -f panic_test.c ] && cp panic_test.c "$AMIGA_DIR/" && echo "✓ Copied panic_test.c"
[ -f panic_no_trigger.c ] && cp panic_no_trigger.c "$AMIGA_DIR/" && echo "✓ Copied panic_no_trigger.c"
[ -f panic_with_defer.c ] && cp panic_with_defer.c "$AMIGA_DIR/" && echo "✓ Copied panic_with_defer.c"

echo ""
echo "Done! Test binaries are in $AMIGA_DIR"
echo ""
echo "On Amiga, run:"
echo "  panic_no_trigger   <- Should run fine, exit normally"
echo "  panic_test         <- Should show panic dialog: 'Division by zero!'"
echo "  panic_with_defer   <- Should show panic dialog: 'Negative value not allowed!'"
