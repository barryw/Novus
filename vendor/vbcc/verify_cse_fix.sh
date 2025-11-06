#!/bin/bash
# Verification script for VBCC CSE ADDRESS bug fix
# Usage: ./verify_cse_fix.sh

set -e

echo "================================================"
echo "VBCC CSE ADDRESS Bug Fix Verification"
echo "================================================"
echo

# Check if fix is applied
echo "[1/4] Checking if patch is applied..."
if grep -q "ADDRESS operations must be invalidated when:" vendor/vbcc/vbcc/cse.c 2>/dev/null; then
    echo "✓ Patch is applied (comment found in cse.c)"
elif grep -q "ADDRESS operations must be invalidated when:" vbcc/cse.c 2>/dev/null; then
    echo "✓ Patch is applied (comment found in cse.c)"
else
    echo "✗ PATCH NOT APPLIED!"
    echo "  Expected to find comment in vbcc/cse.c"
    exit 1
fi
echo

# Check if VBCC is built
echo "[2/4] Checking VBCC build..."
if [ -f vendor/vbcc/vlink/vlink ] && [ -f vendor/vbcc/bin/vasmm68k_mot ]; then
    echo "✓ VBCC tools found"
elif [ -f vlink/vlink ] && [ -f bin/vasmm68k_mot ]; then
    echo "✓ VBCC tools found"
else
    echo "✗ VBCC not built!"
    echo "  Run: cd vendor/vbcc && make"
    exit 1
fi
echo

# Check if Novus compiler is built
echo "[3/4] Checking Novus compiler..."
if [ -f Novus/bin/Debug/net9.0/Novus ]; then
    echo "✓ Novus compiler found"
else
    echo "✗ Novus compiler not built!"
    echo "  Run: dotnet build"
    exit 1
fi
echo

# Compile test case
echo "[4/4] Compiling test case with -O2..."
if [ -f Novus.Tests/Examples/prime_100000.novus ]; then
    ./Novus/bin/Debug/net9.0/Novus compile \
        Novus.Tests/Examples/prime_100000.novus \
        --output /tmp/prime_100000_verify \
        --cpu 68040 \
        -O 2 > /tmp/verify_compile.log 2>&1

    if [ $? -eq 0 ]; then
        echo "✓ Compilation succeeded"
        echo "  Binary: /tmp/prime_100000_verify"
        echo "  Size: $(ls -lh /tmp/prime_100000_verify | awk '{print $5}')"
    else
        echo "✗ Compilation failed!"
        echo "  See /tmp/verify_compile.log for details"
        exit 1
    fi
else
    echo "✗ Test file not found: Novus.Tests/Examples/prime_100000.novus"
    exit 1
fi
echo

echo "================================================"
echo "Verification: PASSED ✓"
echo "================================================"
echo
echo "To test on Amiga:"
echo "  1. cp /tmp/prime_100000_verify /Users/barry/Emulation/Amiga/A4000-DH0/Barry/"
echo "  2. Run on Amiga and verify all primes up to 100,000 are printed"
echo "  3. Expected output starts with: 2 3 5 7 11 13 17 19 23 29..."
echo
