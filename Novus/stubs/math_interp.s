; math_interp.s - Interpolation functions in pure 68020+ assembly
; Calling convention: VBCC standard C
;   Args: stack (right to left)
;   Return: d0
;   Preserve: d2-d7, a2-a6
;   Volatile: d0, d1, a0, a1
;
; Note: Most interpolation functions use Fixed32 operations which are
; already optimized in math_fixed.s. This file contains only the i32-based
; helper functions.

    section CODE,code

; ============================================================================
; Step Functions
; ============================================================================

; step_i32(edge: i32, x: i32) -> i32
; Returns 0 if x < edge, 1 if x >= edge
    xdef _asm_step_i32
_asm_step_i32:
    move.l  8(sp),d0        ; x
    cmp.l   4(sp),d0        ; compare x - edge
    blt.s   .zero           ; if x < edge, return 0
    moveq   #1,d0
    rts
.zero:
    moveq   #0,d0
    rts

; ============================================================================
; Move Towards
; ============================================================================

; move_towards_i32(current: i32, target: i32, max_delta: i32) -> i32
; Move current towards target by at most max_delta
    xdef _asm_move_towards_i32
_asm_move_towards_i32:
    move.l  d2,-(sp)        ; save d2
    move.l  8(sp),d0        ; current (4 + offset for saved d2)
    move.l  12(sp),d1       ; target
    move.l  16(sp),d2       ; max_delta

    ; diff = target - current
    sub.l   d0,d1           ; d1 = target - current

    ; abs_diff
    move.l  d1,a0           ; save diff in a0
    bpl.s   .pos_diff
    neg.l   d1              ; d1 = abs(diff)
.pos_diff:

    ; if abs_diff <= max_delta, return target
    cmp.l   d2,d1           ; compare abs_diff - max_delta
    ble.s   .return_target

    ; Check sign of original diff
    move.l  a0,d1           ; restore diff
    tst.l   d1
    bmi.s   .negative

    ; diff > 0: return current + max_delta
    add.l   d2,d0
    move.l  (sp)+,d2
    rts

.negative:
    ; diff < 0: return current - max_delta
    sub.l   d2,d0
    move.l  (sp)+,d2
    rts

.return_target:
    move.l  12(sp),d0       ; return target
    move.l  (sp)+,d2
    rts

; ============================================================================
; Lerp i32 (optimized - doesn't use Fixed32)
; ============================================================================

; lerp_i32_fast(a: i32, b: i32, t_raw: i32) -> i32
; Fast lerp for i32 where t_raw is in Fixed32 format (16.16)
; Returns: a + ((b - a) * t_raw) >> 16
    xdef _asm_lerp_i32_fast
_asm_lerp_i32_fast:
    move.l  d2,-(sp)        ; save d2
    move.l  8(sp),d0        ; a
    move.l  12(sp),d1       ; b
    move.l  16(sp),d2       ; t_raw (16.16 fixed point)

    ; diff = b - a
    sub.l   d0,d1           ; d1 = b - a

    ; scaled = diff * t_raw (signed 32x32 -> 64)
    ; We need ((diff * t_raw) >> 16)
    ; Use muls.l for signed multiply
    muls.l  d2,d2:d1        ; d2:d1 = diff * t_raw (64-bit result)

    ; Shift right by 16: take high 16 bits of d1 and low 16 bits of d2
    ; Result = (d2 << 16) | (d1 >> 16)
    lsr.l   #8,d1
    lsr.l   #8,d1           ; d1 >> 16 (shift in two steps)
    swap    d2              ; high word of d2 to low
    andi.l  #$FFFF0000,d2   ; keep only high 16 bits (now in high word position)
    or.l    d2,d1           ; combine

    ; result = a + scaled
    add.l   d1,d0

    move.l  (sp)+,d2
    rts

    end
