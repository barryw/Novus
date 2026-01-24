# Documentation Audit Design

**Date:** 2026-01-23
**Status:** Approved
**Goal:** Ensure all user-facing documentation is 100% accurate — no hallucinations, no made-up features, no outdated examples.

---

## Scope

### In Scope (100% accuracy required)

| Location | Type | Files |
|----------|------|-------|
| `website/src/content/` | Website (Astro/Starlight) | ~13 pages |
| `guide/` | Programmer's Guide (LaTeX) | 16 chapters |

### Out of Scope (design intent acceptable)

| Location | Type | Notes |
|----------|------|-------|
| `docs/` | Internal design docs | Can contain aspirational/planned features |

---

## Verification Hierarchy

A feature is only "implemented" if it passes verification at each level:

| Level | Source | Question |
|-------|--------|----------|
| 1. Grammar | `NovusParser.g4`, `NovusLexer.g4` | Can it parse? |
| 2. Semantic | `SemanticAnalyzer.cs`, `TypeChecker.cs`, `IrBuilder*.cs` | Does it type-check? |
| 3. Codegen | `CCodeGenerator.cs` | Does it emit code? |
| 4. Tests | `Novus.Tests/Examples/` | Is it proven to work? (ideal) |

**Rule:** If a feature fails levels 1-3, it doesn't exist and must be:
- Removed from documentation, OR
- Clearly marked as "Planned" with a visual indicator

---

## Process

### Per-Page Audit Steps

1. **Extract claims** — Every statement about what Novus can do
2. **Categorize claims** — Syntax, type system, stdlib, tooling, etc.
3. **Verify each claim** against source of truth:
   - Grep grammar for syntax rules
   - Grep semantic analyzer for type handling
   - Grep codegen for emission
4. **Extract code snippets** — Every ```novus code block
5. **Verify snippets compile** — Run through `novus check`
6. **Document findings** — Track correct, wrong, or missing
7. **Fix issues** — Update docs to match reality

### Tracking Format

**Claims:**

| Claim | Location | Grammar | Semantic | Codegen | Test | Verdict |
|-------|----------|---------|----------|---------|------|---------|
| "supports where clauses" | variables-types.md:45 | ✅ | ✅ | ✅ | ✅ | CORRECT |
| "fixed16 type exists" | variables-types.md:78 | ✅ | ❌ | ❌ | ❌ | MARK PLANNED |

**Code Snippets:**

| Snippet | Location | Parses | Type-checks | Verdict |
|---------|----------|--------|-------------|---------|
| `let x: i32 = 5` | first-program.md:23 | ✅ | ✅ | CORRECT |
| `copper { move... }` | memory.md:89 | ✅ | ❌ | MARK PLANNED |

---

## Tooling

### Snippet Verification Tool

**Type:** C# dotnet tool (integrates with project, reuses parser)

**Input:** Directory of markdown/mdx files

**Process:**
1. Find all ```novus code blocks
2. Extract each snippet with source location (file:line)
3. Determine if snippet needs wrapping (fn main wrapper for expressions)
4. Write to temp file
5. Run `novus check <temp_file>`
6. Capture pass/fail + error message

**Output:**
```
✅ website/src/content/docs/guide/functions.md:45 — PASS
❌ website/src/content/docs/guide/memory.md:89 — FAIL: unknown type 'fixed16'
⚠️ website/src/content/docs/guide/control-flow.md:23 — SKIP: incomplete fragment
```

---

## Execution Order

### Phase 1: Website — Getting Started
1. `getting-started/introduction.md`
2. `getting-started/installation.md`
3. `getting-started/first-program.md`

### Phase 2: Website — Core Guide
4. `guide/variables-types.md`
5. `guide/control-flow.md`
6. `guide/functions.md`
7. `guide/error-handling.md`
8. `guide/memory.md`

### Phase 3: Website — Reference
9. `reference/language.md`
10. `reference/cli.md`

### Phase 4: Website — Additional
11. `index.mdx`
12. `guides/memory-safety.md`
13. `blog/2026-01-14-introducing-novus.md`

### Phase 5: Programmer's Guide
14. All 16 LaTeX chapters in `guide/chapters/`

---

## Handling Issues

### False Claims (feature doesn't exist)

**Option A:** Remove the claim entirely

**Option B:** Add "Planned" callout:
```markdown
> **Planned Feature**
> Fixed-point math (`fixed16`, `fixed32`) is designed but not yet implemented.
```

### Outdated Code Snippets

1. Fix the snippet to use current syntax
2. Verify it compiles with `novus check`
3. Add to test suite if significant

### Missing Documentation

If verification reveals implemented features not documented:
1. Add documentation for the feature
2. Include working example

---

## Success Criteria

- [ ] All website pages audited
- [ ] All Programmer's Guide chapters audited
- [ ] Snippet verification tool built and passing
- [ ] Zero false claims about implemented features
- [ ] All code snippets parse and type-check
- [ ] Clear "Planned" markers on future features

---

## Deliverables

1. **Snippet verification tool** — `tools/verify-snippets/` or similar
2. **Audit records** — Tracking spreadsheet or markdown per phase
3. **Updated documentation** — All fixes applied
4. **Confidence report** — Summary of accuracy before/after

---

**Approved:** 2026-01-23
