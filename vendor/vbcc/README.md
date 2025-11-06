# README

The `vbcc` toolchain (`vbcc`, `vasm`, `vlink`) taken as-is from http://compilers.de/
and http://sun.hasenbraten.de/, with a `Makefile` that builds the cross-compiler 
toolchain targeting Amiga with Kickstart 1.3.

The Makefile is built to suit my needs, but if you're running a halfway decent Linux
(i.e. Ubuntu) and have the essential build tools installed (`make` and `gcc`), 
just running `make && make install` should work for you.

By default, `vbcc` will be installed in `/opt/vbcc`. If you prefer a different
location, just run `make install INSTALL_DIR=/path/to/vbcc`.

Make sure that you set the `VBCC` env variable to the vbcc directory, and add
$VBCC/bin to your PATH.

## NDKs
This repo also contains the Amiga NDK for Kicstart/Workbench 1.3. The NDK was 
taken from the [Amiga Developer CD 1.2](https://archive.org/details/amigadevelopercdv1.2),
hoping that nobody is going to sue me... 

The NDKs were also added to the configs, so when you compile with `vc +kick13`, the 
include path already contains the Amiga headers.
