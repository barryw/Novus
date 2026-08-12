---
title: Memory Safety
description: How Novus prevents memory bugs at compile time
---

The canonical guide to ownership, `consuming`, shared and mutable borrows,
owner-tied views, raw handles, and `unsafe` is now
[Ownership and Memory Safety](/guide/memory/).

Novus uses the same rules for ordinary values and Amiga resources: borrow by
default, consume only to transfer ownership, return failures as `Result`, and
keep raw pointers at explicit low-level boundaries.
