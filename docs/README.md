# Novus Design Documents

This directory contains design documents for major Novus language features.

## Memory Management

### [novus_memory_management_design.md](./novus_memory_management_design.md)
Comprehensive design for automatic memory management in Novus, covering:
- Scope-based RAII (Resource Acquisition Is Initialization)
- `Box<T>` for owned heap allocation
- `Rc<T>` for reference-counted shared ownership
- Comparison with C, C++, Rust, and Swift approaches
- Implementation phases and roadmap

### [novus_ffi_memory_design.md](./novus_ffi_memory_design.md)
How automatic memory management works seamlessly with NDK FFI:
- Two-layer approach: raw FFI (`std/ffi/`) vs safe wrappers (`std/`)
- Converting between `Box<T>`, `Rc<T>`, and raw pointers
- Wrapping NDK functions (AllocMem, OpenLibrary, etc.)
- Real-world examples with Amiga libraries
- Patterns for FFI integration

### [box_api_examples.md](./box_api_examples.md)
Complete API reference and usage examples for `Box<T>`:
- What `Box.alloc()` returns and how to use it
- Array indexing and pointer access
- Integration with NDK functions
- Ownership transfer patterns
- Real-world examples (buffers, screen memory, etc.)

### [defer_design.md](./defer_design.md)
Design for the `defer` statement with closure support:
- Automatic resource cleanup at scope exit
- Closure capture semantics
- LIFO execution order
- Integration with Box/Rc
- Real-world NDK examples (OpenLibrary, files, windows)
- Comparison with Swift, Go, Zig, Odin

## Implementation Status

| Feature | Status | Notes |
|---------|--------|-------|
| Raw pointers (`*T`) | ✅ Implemented | Works with FFI |
| `Box<T>` | 📋 Designed | Ready to implement |
| `Rc<T>` | 📋 Designed | Can be library code |
| `defer` | 📋 Designed | Ready to implement |

## Design Philosophy

**Safe and modern by default, with escape hatches for manual control.**

- **`Box<T>`** - Automatic cleanup for owned heap allocations
- **`Rc<T>`** - Simple shared ownership without borrow checker
- **`defer`** - Automatic resource cleanup (files, libraries, locks)
- **Raw pointers (`*T`)** - Manual control when needed (The Old Way™)

All designed to work seamlessly with Amiga NDK while providing modern safety and ergonomics.
