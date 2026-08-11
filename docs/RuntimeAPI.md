# Novus runtime API

Novus targets Motorola 68020 and newer. Integer multiply, divide, and shifts use
the selected CPU's native instruction set; there is no legacy CPU dispatch ABI.

The runtime is primarily compiler-internal. Its stable responsibilities are:

- checked allocation and raw allocation for standard-library internals;
- bounds, division, null, stack, assertion, and panic reporting;
- interrupt-context-aware failure reporting (`Alert()` when requesters are unsafe);
- deterministic memory copy/set helpers used by generated code;
- comparison helpers used to work around documented VBCC condition-code hazards;
- test-mode panic capture and debug-value formatting.

The authoritative C declarations and interrupt-safety notes are in
`Novus/runtime/novus_runtime.h`. Application code should prefer Novus standard
library APIs. Assembly code may call a declared `__novus_*` symbol only when it
links the matching Novus runtime and follows the C ABI; undeclared historical
math-dispatch symbols are not part of the runtime contract.

For hardware and interrupt code, use Novus's `read_volatile`, `write_volatile`,
`memory_fence`, `amiga fn`, `@interrupt`, and `@interrupt_vector` facilities rather
than reaching through runtime internals.
