# Example Files Cleanup Report

**Date:** 2026-01-22
**Total Files:** 278
**Pass:** 221 (79.5%)
**Fail:** 57 (20.5%)

---

## Summary

After compiling all 278 example files, 221 compile successfully. The 57 failures fall into clear categories, and many are not due to bugs in the examples themselves.

---

## Category 1: KEEP (221 files)

All files that compile successfully. These demonstrate working Novus features.

**Action:** Keep as-is. No changes needed.

---

## Category 2: DELETE - Test Files Without main() (24 files)

These files use `@test` attributes and are designed to run with `novus test`, not `novus compile`. They should NOT be in the Examples directory - they belong in a test suite.

| File | Reason |
|------|--------|
| channel_comprehensive_test.novus | Has @test functions |
| str_equals_test.novus | Has @test functions |
| test_bsdsocket.novus | Has @test functions |
| test_const_fn.novus | Has @test functions |
| test_file_io.novus | Has @test functions |
| test_fixed32_asm.novus | Has @test functions |
| test_framework_example.novus | Has @test functions |
| test_hashmap.novus | Has @test functions |
| test_intrinsics.novus | Has @test functions |
| test_intrusive_list.novus | Has @test functions |
| test_ip_addr.novus | Has @test functions |
| test_math_angle.novus | Has @test functions |
| test_math_core.novus | Has @test functions |
| test_math_ease.novus | Has @test functions |
| test_math_fixed.novus | Has @test functions |
| test_math_interp.novus | Has @test functions |
| test_math_sqrt.novus | Has @test functions |
| test_math_trig.novus | Has @test functions |
| test_math_vec2.novus | Has @test functions |
| test_net_features.novus | Has @test functions |
| test_priority_list.novus | Has @test functions |
| test_ringbuffer.novus | Has @test functions |
| test_semaphore.novus | Has @test functions |
| test_slotmap.novus | Has @test functions |
| test_smallvec.novus | Has @test functions |
| test_trig_asm.novus | Has @test functions |
| test_vec2_asm.novus | Has @test functions |

**Action:** Move to `Novus/std/tests/` or delete. These are unit tests, not examples.

---

## Category 3: DELETE - Intentional Error Demonstrations (6 files)

These files are designed to show compiler errors (borrow checker, type errors). They will never compile.

| File | Error Type |
|------|------------|
| move_if_error.novus | Demonstrates use-after-move error |
| move_match_error.novus | Demonstrates use-after-move error |
| move_while_error.novus | Demonstrates use-after-move error |
| test_fp_error_wrong_param_count.novus | Demonstrates function pointer mismatch |
| test_fp_error_wrong_param_types.novus | Demonstrates function pointer mismatch |
| test_fp_error_wrong_return_type.novus | Demonstrates function pointer mismatch |

**Action:** Delete. Negative examples don't belong in the Examples directory.

---

## Category 4: BLOCKED - Stdlib Bug in chip_pool.novus (10 files)

These fail due to a bug in `std/memory/chip_pool.novus` line 83: "invalid types for assignment". Fix the stdlib, then these will compile.

| File |
|------|
| bank_test.novus |
| chip_cache_test.novus |
| embed_mod_test.novus |
| mod_callback_test.novus |
| mod_chipcache_test.novus |
| mod_player_demo.novus |
| mod_player_test.novus |
| ptplayer_test.novus |
| streaming_test.novus |
| wav_asset_test.novus |

**Action:** Fix `chip_pool.novus`, then verify these compile.

---

## Category 5: BLOCKED - Stdlib Bug in WBStartup (2 files)

These fail because the `WBStartup` struct is incomplete in the FFI bindings.

| File |
|------|
| workbench_startup_simple.novus |
| workbench_startup_test.novus |

**Action:** Complete `WBStartup` struct in stdlib, then verify.

---

## Category 6: BLOCKED - Stdlib Bug in thread.novus (1 file)

| File |
|------|
| thread_example.novus |

Fails due to bug in `std/thread/thread.novus` line 307.

**Action:** Fix stdlib bug, then verify.

---

## Category 7: FIX - Array Size Syntax (2 files)

These have the newly-forbidden `[T; N] = [elements]` syntax.

| File |
|------|
| mui_demo.novus |
| test_freelist.novus |

**Action:** Change `[T; N]` to `[T]` for element initializers.

---

## Category 8: DELETE - Outdated/Broken Examples (12 files)

These have real bugs or use outdated syntax/features.

| File | Issue |
|------|-------|
| bench_example.novus | VBCC fails on generated C |
| bouncing_ball_hardware.novus | Multiple errors: unsafe block needed, type errors |
| bouncing_ball_os.novus | Same issues |
| buffered_window_animation.novus | Same issues |
| buffered_window_demo.novus | Same issues |
| font_showcase.novus | VBCC fails on generated C |
| test_amiga_abi.novus | Parser error - outdated syntax |
| test_drop_minimal.novus | "unknown identifier <value>" |
| test_extern_var.novus | VBCC CUSTOM macro conflict |

**Action:** Delete. These are broken and would require significant rework.

---

## Recommended Actions Summary

| Action | Files | Count |
|--------|-------|-------|
| **KEEP** | Compiling examples | 221 |
| **DELETE** | Test files (move to tests) | 24 |
| **DELETE** | Intentional error demos | 6 |
| **DELETE** | Broken/outdated | 12 |
| **FIX STDLIB** | chip_pool.novus bug | (affects 10 examples) |
| **FIX STDLIB** | WBStartup struct | (affects 2 examples) |
| **FIX STDLIB** | thread.novus bug | (affects 1 example) |
| **FIX EXAMPLE** | Array size syntax | 2 |

**Net result after cleanup:**
- 221 working examples (KEEP)
- 42 files deleted (test files + error demos + broken)
- 13 examples blocked on stdlib fixes
- 2 examples need minor syntax fix

---

## Approval Required

Please review this report and confirm:

1. **Proceed with deletions?** (42 files)
2. **Fix the 2 array syntax issues?**
3. **Should I also fix the stdlib bugs?** (chip_pool, WBStartup, thread)
