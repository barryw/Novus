# Novus implementation status

Novus targets AmigaOS on Motorola 68020 and newer processors. Supported compiler
targets are `auto`, `68020`, `68030`, `68040`, `68060`, and `68080`; `auto` uses a
68020-safe baseline.

Implemented foundations include the parser and semantic analyzer, typed IR,
incremental project builds, C/VBCC and direct 68k backends, the standard library,
generated NDK bindings, libraries, devices, handlers, Workbench applications,
tests, and emulator/A4000 execution harnesses.

The current verified state and outstanding hardware observations live in
`docs/HANDOVER-2026-08-08.md`. Language syntax and user-facing examples live in
the main README, `docs/LanguageDesignDoc.md`, and the project templates. Historical
completion/design reports may describe the implementation at the date in their
title and are not the current command reference.
