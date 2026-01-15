# Reference Lifetime Documentation Update Design

**Date:** 2026-01-14
**Status:** Approved
**Goal:** Update all documentation to reflect the new reference lifetime tracking feature

## Overview

Update the Programmer's Guide and website documentation to cover reference lifetime safety. Tone: "power with guardrails" - emphasize safety by default with explicit escape hatches.

**Website = overview, Guide = deep dive**
- Website covers concepts briefly with examples
- Guide has full technical details, edge cases, error messages

## Deliverables

### 1. Programmer's Guide (Chapter 5)

**File:** `guide/chapters/05-memory-management.tex`

**New Subsection: "Reference Lifetimes"** (after "References vs Raw Pointers", ~line 237)

Structure:
- **Opening** - References are tied to the scope of what they borrow
- **Why Lifetimes Matter** - The dangling pointer problem, ScreenHandle/rastport motivating example
- **Lifetime Rules**
  - Rule 1: A reference cannot outlive its source
  - Rule 2: References cannot be stored in struct fields (v1 limitation)
  - Rule 3: Method returns tie to `&self` lifetime (or single ref param)
  - Rule 4: Converting reference to pointer requires `unsafe`
- **Error Reference** - Each error with: code, trigger condition, example, fix
  - E0597: "does not live long enough"
  - E0106: "cannot contain reference" / "cannot infer lifetime"
  - E0133: "requires unsafe"
  - E0515: "cannot return reference to local"
- **Escape Hatches** - Raw pointers, unsafe blocks, FFI interop

**Update Existing Caveat** (line 158)

FROM:
> **Important:** Novus's borrow checker tracks *moves* but does not enforce reference exclusivity. You can create multiple mutable references to the same value. This is less safe than Rust but simpler...

TO:
> **Important:** Novus's borrow checker tracks moves and reference lifetimes. References cannot outlive their source, and converting references to raw pointers requires `unsafe`. Novus does not enforce reference *exclusivity* (you can have multiple mutable references), but it does prevent dangling references. See Section 5.4.5 for details.

### 2. Website Documentation

**New File:** `website/src/content/docs/memory-safety.md`

Structure:
- **Title:** Memory Safety
- **Intro** (~100 words) - Compile-time safety, no GC overhead, "power with guardrails"
- **Ownership in Brief** (~100 words) - Single owner, cleanup on scope exit, link to Guide
- **References vs Pointers** (~150 words) - `&T` vs `*T`, when to use each, code example
- **What the Compiler Catches** (~150 words) - Dangling refs, returning local refs, E0597 example
- **The Amiga Context** (~100 words) - No virtual memory, Guru Meditations, safety matters

### 3. Example Updates

**Review existing:**
- `homepage_example.novus` - Already correct
- `screen_*.novus` examples - Verify proper lifetime scoping

**Add new:**
- `Novus.Tests/Examples/lifetime_safety_demo.novus` (~30 lines)
  - Correct pattern (reference in same scope)
  - Comments showing what would fail and why
  - Unsafe escape hatch for FFI

## Files Changed

| File | Change |
|------|--------|
| `guide/chapters/05-memory-management.tex` | Add subsection, update caveat |
| `website/src/content/docs/memory-safety.md` | New file |
| `Novus.Tests/Examples/lifetime_safety_demo.novus` | New file |

## Not In Scope

- Other guide chapters (they reference Chapter 5, no changes needed)
- API documentation (this is conceptual, not API)
- The implementation design doc (`docs/plans/2026-01-14-reference-lifetime-tracking.md`)

## Implementation Notes

- Rebuild Guide PDF after LaTeX changes: `make -C guide`
- Website rebuilds automatically on deploy
- New example file will be picked up by existing test infrastructure
