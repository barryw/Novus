#!/bin/bash
# Manual build script to test write() function
# This will be integrated into the build system later

set -e

echo "=== Manual Build Script ==="
echo "Building example with novus_io runtime support"

cd target/debug/bins

# Compile novus_io.c if not already done
if [ ! -f novus_io.o ]; then
    echo "Compiling novus_io.c..."
    vc +aos68k -c99 -c ../../../../../Novus/runtime/novus_io.c -o novus_io.o
fi

# Generate dos.library stubs if needed
if [ ! -f dos_stubs.o ]; then
    echo "Generating dos.library stubs..."
    # Generate stubs using fd2pragma or vbcc's own stub generator
    # For now, let's see if we can link without them by using inline calls
    touch dos_stubs_placeholder.txt
fi

echo "Linking example..."
# Link without greeting library for now (it has build issues)
# Just test if write() links properly
vlink -bamigahunk -x -Bstatic -Cvbcc -nostdlib \
    novus_startup.o \
    greeting-example.o \
    core_LibraryVersion::new.o \
    core_LibraryVersion::major.o \
    core_LibraryVersion::minor.o \
    core_LibraryVersion::patch.o \
    novus_io.o \
    exec_stubs.o \
    exec_base.o \
    -o test-write 2>&1 | tee link.log

if [ -f test-write ]; then
    echo "✓ Build successful: test-write"
    ls -lh test-write
else
    echo "✗ Build failed"
    cat link.log
    exit 1
fi
