# Documentation gap analysis

**Updated:** 2026-08-09
**Status:** current public documentation matches the implemented 68020+ toolchain.

The detailed Amiga language capability audit is in
[AMIGA_LANGUAGE_AUDIT.md](AMIGA_LANGUAGE_AUDIT.md). This file tracks the documentation
cleanup itself.

## Corrected

- Removed pre-68020 compiler targets, project settings, examples, and recommendations.
- Replaced draft library/device syntax with the native lifecycle and vector attributes.
- Documented resources, DOS handlers, deferred device I/O, AbortIO ownership, Amiga
  register-ABI function types, native unions, volatile memory, and both interrupt forms.
- Corrected fixed-point, closures, async, traits, and lifecycle support that older audits
  still labeled incomplete.
- Reduced progress and runtime documents that duplicated stale implementation detail.
- Marked aspirational copper, blitter, sprite, graphics-asset, fat-binary, and direct
  backend material as design work rather than shipped syntax.

## Intentionally retained

- Generated NDK bindings keep processor constants and NDK prose verbatim where those
  names are part of the AmigaOS ABI. Their presence does not enable an old CPU target.
- Historical research notes may discuss older Amiga processors when the history itself
  matters.

## Rule for future documentation

Examples in reference docs must compile, target 68020 or newer, and distinguish shipped
syntax from proposals. Templates are the canonical executable examples for project
kinds; the A4000 foundational suite is the canonical language-runtime check.
