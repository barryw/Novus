# Novus Documentation

Use these as the current sources of truth:

- [Ownership and Memory Safety](../website/src/content/docs/guide/memory.md) — developer rules for ownership, `consuming`, borrows, views, `Result`, and raw access
- [Memory Safety Status](MemorySafetyStatus.md) — implemented guarantees, verification, and known limits
- [Standard Library Style Guide](STDLIB_STYLE_GUIDE.md) — API conventions library authors must follow
- [Parameter Passing](parameter_passing.md) — source contracts and ABI boundary
- [Amiga Language Audit](AMIGA_LANGUAGE_AUDIT.md) — language/library gaps found while porting real Amiga code
- [Handover](HANDOVER-2026-08-08.md) — current repository and hardware-test state

Other files in this directory include historical design proposals, completed
implementation plans, and subsystem notes. A proposal is not a language
guarantee unless the current guides above and compiler tests say it is.
