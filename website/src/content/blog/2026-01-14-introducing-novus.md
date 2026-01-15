---
title: "Introducing Novus"
description: "A modern systems programming language for the Amiga 68k family."
date: 2026-01-14
author: "Barry Lapthorn"
tags: ["announcement", "release"]
---

Welcome to Novus - a modern systems programming language designed specifically for the Amiga 68k ecosystem. After years of watching retro computing gain momentum and seeing developers return to classic platforms, we asked ourselves: what if Amiga developers could have modern language features without sacrificing the direct hardware access and efficiency that made Amiga development special?

## Why Another Language?

The Amiga has always been about pushing boundaries. In the 1980s and 1990s, it offered capabilities that seemed magical - true multitasking, hardware sprites, and a custom chipset that let talented developers create experiences far beyond what the specs suggested. Today's Amiga developers still push those boundaries, but they're often working with tools designed decades ago.

We love C and the classic Amiga toolchains, but modern language design has taught us a lot about safety, ergonomics, and productivity. Novus brings those lessons to Amiga development while respecting the platform's unique constraints and capabilities.

## What Makes Novus Different?

### Explicit and Predictable

Novus follows a simple principle: **no surprises**. Every allocation is visible, every system call returns a `Result` type, and there's no garbage collection pausing your carefully timed copper lists. When you write Novus code, you know exactly what the 68k will be doing.

### Amiga First, Not Cross-Platform

Novus isn't trying to be a portable systems language. It's laser-focused on the Amiga 68k family. This means first-class support for:

- **Hardware registers** - symbolic access to custom chips with volatile semantics
- **Copper lists** - declarative DSL with compile-time validation
- **Blitter operations** - safe, typed blitter jobs
- **AmigaOS APIs** - proper `Result`-based wrappers for Exec, Intuition, Graphics, and DOS

### Modern Safety with Low-Level Power

By default, Novus is safe:
- Bounds checking in debug builds
- `Result` and `Option` types instead of null pointers
- RAII-style resource management with `defer` blocks
- Strong typing with no implicit conversions

But when you need raw power, `unsafe` blocks give you direct hardware access and the ability to do whatever the 68k can do.

### Target-Aware Compilation

Novus understands your hardware:
- **CPU profiles** - 68020/030/040/060 with appropriate instruction selection
- **Chipset awareness** - OCS/ECS/AGA validation at compile time
- **Fat binaries** - planned for future releases (runtime CPU dispatch)
- **Memory control** - explicit Chip/Fast memory allocation

## Current Status

Novus is in early development. The compiler can already:
- Parse basic syntax and build an AST
- Perform type checking and semantic analysis
- Generate 68k assembly via VBCC toolchain
- Build executables that run on real Amigas and emulators

We're currently implementing:
- Standard library (collections, strings, memory management)
- AmigaOS FFI layer (Exec, DOS, Graphics, Intuition)
- Advanced features (pattern matching, async/await)
- Hardware DSLs (Copper, Blitter, Paula audio)

## What's Next?

Our immediate roadmap focuses on:

1. **Core Language** - completing the type system, implementing traits, and stabilizing syntax
2. **Standard Library** - building out collections, string handling, and memory management primitives
3. **AmigaOS Integration** - wrapping system libraries with safe, Result-based APIs
4. **Examples and Documentation** - showing real-world usage patterns

The ultimate goal? A self-hosting compiler that runs on AmigaOS itself. Imagine developing entirely on your Amiga 4000, using modern tooling designed for the platform.

## Get Involved

Novus is open source and we'd love your feedback. Whether you're an experienced Amiga developer or curious about retro computing, check out the [GitHub repository](https://github.com/barryw/novus) and join the conversation.

The Amiga community has always been creative, passionate, and technically brilliant. We can't wait to see what you build with Novus.

**New code for classic machines** - let's make it happen.
