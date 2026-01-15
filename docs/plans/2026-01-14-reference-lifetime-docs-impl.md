# Reference Lifetime Documentation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Update all documentation to reflect the new reference lifetime tracking feature.

**Architecture:** Three deliverables - (1) new LaTeX subsection in Guide Chapter 5, (2) new website markdown page, (3) example file demonstrating safe patterns. Use the docs-expert agent for content writing.

**Tech Stack:** LaTeX (Guide), Markdown/Astro (Website), Novus (example), Make (PDF build)

---

## Task 1: Update the Borrow Checker Caveat

**Files:**
- Modify: `guide/chapters/05-memory-management.tex:158`

**Step 1: Read current text**

Read line 158 to confirm the outdated caveat text.

**Step 2: Replace the caveat**

Find this text:
```latex
\textbf{Important:} Novus's borrow checker tracks \textit{moves} but does not enforce reference exclusivity. You can create multiple mutable references to the same value. This is less safe than Rust but simpler and sufficient for most Amiga programs where careful discipline is expected.
```

Replace with:
```latex
\textbf{Important:} Novus's borrow checker tracks \textit{moves} and \textit{reference lifetimes}. References cannot outlive their source, and converting references to raw pointers requires \texttt{unsafe}. Novus does not enforce reference \textit{exclusivity} (you can have multiple mutable references), but it does prevent dangling references. See Section~\ref{sec:lifetimes} for details.
```

**Step 3: Verify the edit**

Read the file to confirm the change was made correctly.

**Step 4: Commit**

```bash
git add guide/chapters/05-memory-management.tex
git commit -m "docs(guide): update borrow checker caveat for lifetime tracking"
```

---

## Task 2: Add Reference Lifetimes Subsection to Guide

**Files:**
- Modify: `guide/chapters/05-memory-management.tex` (after line ~237, after "References vs Raw Pointers" section ends)

**Step 1: Find insertion point**

The new subsection goes after `\subsection{References vs Raw Pointers}` ends (around line 237) and before `\section{The Drop Trait}` (line 238).

**Step 2: Insert the new subsection**

Insert this content before `\section{The Drop Trait}`:

```latex
\subsection{Reference Lifetimes}
\label{sec:lifetimes}

References in Novus have \textit{lifetimes}---they are tied to the scope of the value they borrow. The compiler tracks these lifetimes and prevents you from using a reference after its source has been dropped. This eliminates an entire class of bugs: dangling pointers.

\subsubsection{Why Lifetimes Matter}

Consider this common pattern when working with AmigaOS screens:

\begin{lstlisting}
fn dangerous() {
    let rp: &RastPort
    {
        let screen = ScreenHandle::lores("Demo", 5)?
        rp = screen.rastport()  // rp borrows from screen
    }  // screen dropped here!
    SetAPen(rp, 2)  // BUG: rp is dangling!
}
\end{lstlisting}

Without lifetime checking, this code would compile and crash at runtime---a Guru Meditation. With Novus's lifetime tracking, the compiler catches this at compile time:

\begin{verbatim}
error[E0597]: `screen` does not live long enough
  --> example.novus:5:14
   |
 4 |         let screen = ScreenHandle::lores("Demo", 5)?
   |             ^^^^^^ borrowed value
 5 |         rp = screen.rastport()
   |              ----------------- borrow occurs here
 6 |     }
   |     ^ `screen` dropped here while still borrowed
\end{verbatim}

\subsubsection{Lifetime Rules}

Novus enforces these lifetime rules at compile time:

\begin{enumerate}
    \item \textbf{References cannot outlive their source.} A reference must not be used after the value it borrows from goes out of scope.

    \item \textbf{References cannot be stored in struct fields.} This is a v1 limitation---lifetime parameters on structs are planned for a future version. Use raw pointers if you need to store references in structs.

    \item \textbf{Method return lifetimes are inferred.} When a method returns a reference, it's tied to \texttt{\&self} (or the single reference parameter if no self). Multiple reference parameters without \texttt{\&self} is ambiguous and rejected.

    \item \textbf{Converting reference to pointer requires unsafe.} To escape lifetime tracking, you must explicitly use an \texttt{unsafe} block.
\end{enumerate}

\subsubsection{Error Reference}

\paragraph{E0597: Does not live long enough}

Triggered when a reference outlives the value it borrows from.

\begin{lstlisting}
fn bad() {
    let r: &i32
    {
        let x: i32 = 42
        r = &x      // x is in inner scope
    }               // x dropped here
    let y = *r      // ERROR: x doesn't live long enough
}
\end{lstlisting}

\textbf{Fix:} Move the reference into the same scope as its source, or restructure to avoid storing the reference.

\paragraph{E0106: Cannot contain reference / Cannot infer lifetime}

Triggered when storing a reference in a struct field, or when lifetime inference is ambiguous.

\begin{lstlisting}
// ERROR: struct cannot contain reference
struct BadCache {
    rp: &RastPort  // Not allowed in v1
}

// ERROR: cannot infer lifetime
fn pick(a: &i32, b: &i32) -> &i32 {  // Which input?
    return a
}
\end{lstlisting}

\textbf{Fix for structs:} Use a raw pointer (\texttt{*RastPort}) and manage safety manually.

\textbf{Fix for functions:} Add \texttt{\&self} parameter, or reduce to single reference parameter.

\paragraph{E0133: Requires unsafe}

Triggered when converting a reference to a raw pointer outside an \texttt{unsafe} block.

\begin{lstlisting}
let x: i32 = 42
let r: &i32 = &x
let p: *i32 = (*i32)r  // ERROR: requires unsafe
\end{lstlisting}

\textbf{Fix:} Wrap in \texttt{unsafe} if you can guarantee pointer validity:

\begin{lstlisting}
let p: *i32 = unsafe { (*i32)r }  // OK
\end{lstlisting}

\paragraph{E0515: Cannot return reference to local}

Triggered when returning a reference to a local variable.

\begin{lstlisting}
fn bad() -> &i32 {
    let x: i32 = 42
    return &x  // ERROR: x will be dropped when function returns
}
\end{lstlisting}

\textbf{Fix:} Return an owned value, or take the value as a reference parameter and return a reference tied to it.

\subsubsection{Escape Hatches}

Sometimes you need to break the rules---especially when interfacing with AmigaOS libraries that manage their own memory. Novus provides explicit escape hatches:

\textbf{Raw pointers} (\texttt{*T}) have no lifetime tracking. Use them for FFI and cases where you manually guarantee validity:

\begin{lstlisting}
// FFI function returns pointer with unknown lifetime
extern fn GetSomePointer() -> *Thing

fn use_ffi() {
    let ptr: *Thing = GetSomePointer()
    unsafe {
        // You guarantee ptr is valid
        do_something(*ptr)
    }
}
\end{lstlisting}

\textbf{Unsafe blocks} let you convert references to pointers when needed:

\begin{lstlisting}
fn pass_to_legacy_api(data: &MyData) {
    let ptr: *MyData = unsafe { (*MyData)data }
    LegacyFunction(ptr)  // You guarantee data outlives this call
}
\end{lstlisting}

The philosophy is \textit{power with guardrails}: safe by default, explicit opt-out when you need control. The \texttt{unsafe} keyword marks exactly where you're taking responsibility for memory safety.

```

**Step 3: Verify the edit**

Read the file around the insertion point to confirm proper placement.

**Step 4: Commit**

```bash
git add guide/chapters/05-memory-management.tex
git commit -m "docs(guide): add Reference Lifetimes subsection to Chapter 5"
```

---

## Task 3: Rebuild Guide PDF

**Step 1: Build the PDF**

```bash
make -C guide
```

**Step 2: Verify build succeeded**

Check that `guide/novus-guide.pdf` was updated (check modification time).

**Step 3: Commit the PDF**

```bash
git add guide/novus-guide.pdf
git commit -m "docs(guide): rebuild PDF with lifetime documentation"
```

---

## Task 4: Create Website Memory Safety Page

**Files:**
- Create: `website/src/content/docs/memory-safety.md`

**Step 1: Check docs directory structure**

```bash
ls website/src/content/docs/
```

Understand existing file naming conventions and frontmatter format.

**Step 2: Create the new page**

Create `website/src/content/docs/memory-safety.md`:

```markdown
---
title: Memory Safety
description: How Novus prevents memory bugs at compile time
---

# Memory Safety

Novus prevents common memory bugs at compile time. No garbage collector, no runtime overhead—just the compiler catching mistakes before they become Guru Meditations.

The philosophy is **power with guardrails**: safe by default, with explicit escape hatches when you need direct hardware control.

## Ownership

Every value in Novus has exactly one owner—the variable that holds it. When the owner goes out of scope, the value is automatically cleaned up:

```novus
fn example() {
    let screen = ScreenHandle::lores("Demo", 5)?
    // use screen...
}  // screen automatically closed here
```

No manual cleanup, no memory leaks. See the [Programmer's Guide](/guide) Chapter 5 for details on move semantics and the Drop trait.

## References vs Pointers

Novus has two ways to access data without taking ownership:

| | References (`&T`) | Raw Pointers (`*T`) |
|---|---|---|
| Null | Never null | Can be null |
| Lifetimes | Compiler-tracked | No tracking |
| Safety | Safe by default | Requires `unsafe` |
| Use for | Normal code | FFI, hardware |

```novus
let x: i32 = 42

// Reference - compiler ensures it stays valid
let r: &i32 = &x
let value = *r  // Safe

// Raw pointer - you manage validity
let p: *i32 = unsafe { (*i32)&x }
unsafe {
    let value = *p  // Your responsibility
}
```

**Guideline:** Use references by default. Use raw pointers only for FFI calls and direct hardware access.

## What the Compiler Catches

The compiler prevents these common bugs:

**Dangling references** - using a reference after its source is dropped:

```novus
fn bad() {
    let r: &i32
    {
        let x: i32 = 42
        r = &x
    }  // x dropped
    let y = *r  // ERROR: x does not live long enough
}
```

**Returning local references** - functions can't return references to their local variables:

```novus
fn bad() -> &i32 {
    let x: i32 = 42
    return &x  // ERROR: cannot return reference to local
}
```

**Accidental lifetime escape** - converting a reference to a raw pointer requires explicit `unsafe`:

```novus
let r: &i32 = &x
let p: *i32 = (*i32)r  // ERROR: requires unsafe block
```

## The Amiga Context

On modern systems, memory bugs often just crash one process. On the Amiga, there's no memory protection—a dangling pointer can corrupt any memory in the system, leading to a Guru Meditation or corrupted data.

Novus catches these bugs at compile time, before your code ever runs. You get the low-level control Amiga programming demands, with safety guarantees that prevent the crashes.

For the complete reference on ownership, borrowing, Drop, and RAII patterns, see Chapter 5 of the [Programmer's Guide](/guide).
```

**Step 3: Verify the file was created**

```bash
cat website/src/content/docs/memory-safety.md | head -20
```

**Step 4: Commit**

```bash
git add website/src/content/docs/memory-safety.md
git commit -m "docs(website): add Memory Safety overview page"
```

---

## Task 5: Create Lifetime Safety Demo Example

**Files:**
- Create: `Novus.Tests/Examples/lifetime_safety_demo.novus`

**Step 1: Create the example file**

Create `Novus.Tests/Examples/lifetime_safety_demo.novus`:

```novus
// Lifetime Safety Demo
//
// This example demonstrates Novus's reference lifetime tracking.
// References are safe by default - the compiler prevents dangling pointers.

from std::ffi::dos import Delay

// CORRECT: Reference and source in same scope
fn safe_reference() {
    let x: i32 = 42
    let r: &i32 = &x    // r borrows x
    let value = *r      // OK: x is still alive
    Delay(1)
}

// CORRECT: Reference in inner scope, source in outer
fn inner_scope_ok() {
    let x: i32 = 100
    {
        let r: &i32 = &x  // OK: x outlives r
        let value = *r
    }
    // x still valid here
}

// CORRECT: Method returns reference tied to &self
struct Container {
    value: i32
}

impl Container {
    fn get_ref(&self) -> &i32 {
        return &self.value  // Lifetime tied to self
    }
}

fn method_lifetime() {
    let c = Container { value: 42 }
    let r = c.get_ref()  // r lives as long as c
    let value = *r       // OK: c still alive
}

// ESCAPE HATCH: When you need raw pointers for FFI
fn unsafe_escape_hatch() {
    let x: i32 = 42
    let r: &i32 = &x

    // Convert to raw pointer - requires unsafe
    let ptr: *i32 = unsafe { (*i32)r }

    // Now you're responsible for ensuring ptr stays valid
    unsafe {
        let value = *ptr
    }
}

// ============================================================
// The following patterns would produce compile errors:
// ============================================================
//
// ERROR E0597: Reference outlives source
// fn dangling() {
//     let r: &i32
//     {
//         let x: i32 = 42
//         r = &x
//     }  // x dropped here
//     let y = *r  // ERROR: x does not live long enough
// }
//
// ERROR E0515: Cannot return reference to local
// fn return_local() -> &i32 {
//     let x: i32 = 42
//     return &x  // ERROR: cannot return reference to local
// }
//
// ERROR E0106: Cannot store reference in struct
// struct BadCache {
//     ptr: &i32  // ERROR: struct cannot contain reference field
// }
//
// ERROR E0133: Ref-to-pointer outside unsafe
// fn no_unsafe() {
//     let x: i32 = 42
//     let r: &i32 = &x
//     let p: *i32 = (*i32)r  // ERROR: requires unsafe
// }

pub fn main() -> i32 {
    safe_reference()
    inner_scope_ok()
    method_lifetime()
    unsafe_escape_hatch()
    return 0
}
```

**Step 2: Compile the example to verify it works**

```bash
dotnet run --project Novus -- compile Novus.Tests/Examples/lifetime_safety_demo.novus -o /tmp/lifetime_demo
```

Expected: Compiles successfully (no errors, possibly some warnings).

**Step 3: Commit**

```bash
git add Novus.Tests/Examples/lifetime_safety_demo.novus
git commit -m "docs(examples): add lifetime_safety_demo showing safe patterns"
```

---

## Task 6: Final Verification

**Step 1: Run full test suite**

```bash
dotnet test Novus.Tests -v n
```

Expected: All tests pass (new example will be picked up by existing test discovery).

**Step 2: Final commit if any changes**

```bash
git status
```

If there are uncommitted changes, commit them.

**Step 3: Summary**

Verify all deliverables:
- [ ] Guide caveat updated (Task 1)
- [ ] Guide subsection added (Task 2)
- [ ] Guide PDF rebuilt (Task 3)
- [ ] Website page created (Task 4)
- [ ] Example file created (Task 5)
- [ ] All tests pass (Task 6)
