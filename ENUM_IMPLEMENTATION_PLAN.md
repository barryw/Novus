# Swift-Style Enum Implementation Plan for Novus

## Current Status

✅ **Working**: Simple enums (C-style)
❌ **Not Working**: Enums with associated data (Swift/Rust-style)

## What Needs To Be Done

Swift-style enums are **tagged unions** requiring:
1. Tag field (discriminant) - identifies active variant
2. Data field (union) - holds associated data

**Estimated effort: 22-36 hours of focused development**

See full plan in this document for implementation details.

## Immediate Workaround

For the error taxonomy, use **only simple enum variants** (no associated data):

```novus
pub enum DosError {
    NotFound,
    DiskFull,
    WriteProtected,
    // ... all other errors as simple variants
    // NO: RawCode(i32)  ← This won't work yet
}
```

Store raw codes separately when needed.
