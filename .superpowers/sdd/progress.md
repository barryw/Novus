# Per-CPU NDK cache plan — DEFERRED

Plan: docs/superpowers/plans/2026-08-20-ndk-per-cpu-cache.md
Spec: docs/superpowers/specs/2026-08-20-ndk-per-cpu-cache-design.md

Status: not started. Deferred by user on 2026-08-20 to fix A1200 runtime
failures first (A1200 was 285/292 vs A4000 292/292).

Pre-flight decisions already made, apply when execution resumes:
- Cache tests serialize via a single [Collection("NdkCache")]; the rest of the
  suite stays parallel (xunit.runner.json has parallelizeTestCollections:true).
- Work directly on main.
- Task 3 Step 5 spike commands were cleaned up in the plan.

No tasks dispatched. No implementation commits exist.
