; math_angle.s - Angle manipulation functions in pure 68020+ assembly
; Calling convention: VBCC standard C
;   Args: stack (right to left)
;   Return: d0
;   Preserve: d2-d7, a2-a6
;   Volatile: d0, d1, a0, a1
;
; BAMS (Binary Angular Measurement) format:
;   u16: 0-65535 maps to 0-360 degrees
;   Wraps naturally due to u16 overflow
;   16384 = 90 deg, 32768 = 180 deg, 49152 = 270 deg

    section CODE,code

; ============================================================================
; Angle Normalization
; ============================================================================

; normalize_i32(angle: i32) -> u16
; Normalize signed angle to u16 range [0, 65535]
; Uses proper modulo instead of loops
    xdef _asm_normalize_i32
_asm_normalize_i32:
    move.l  4(sp),d0        ; get angle
    ; d0 mod 65536 (handle negative values correctly)
    ; For positive: just take low 16 bits
    ; For negative: need to add 65536 until positive, but efficiently
    tst.l   d0
    bpl.s   .positive
    ; Negative: negate, take low 16 bits, then subtract from 65536
    ; Unless already a multiple of 65536 (remainder 0)
    neg.l   d0
    andi.l  #$FFFF,d0       ; remainder of abs value
    beq.s   .done           ; if 0, result is 0
    neg.l   d0              ; negate back
    addi.l  #$10000,d0      ; add 65536 to make positive
.positive:
    andi.l  #$FFFF,d0       ; take low 16 bits
.done:
    rts

; ============================================================================
; Angle Difference
; ============================================================================

; angle_diff(from: u16, to: u16) -> i16
; Get signed difference (shortest path from 'from' to 'to')
; Result in range [-32768, 32767]
    xdef _asm_angle_diff
_asm_angle_diff:
    move.w  6(sp),d0        ; get 'to' (second arg, offset 4+2 for alignment)
    sub.w   4(sp),d0        ; subtract 'from'
    ext.l   d0              ; sign-extend to i32 for return
    rts

; angle_diff_abs(a: u16, b: u16) -> u16
; Get absolute difference (smaller arc)
    xdef _asm_angle_diff_abs
_asm_angle_diff_abs:
    move.w  6(sp),d0        ; get 'b'
    sub.w   4(sp),d0        ; b - a (signed)
    bpl.s   .positive
    neg.w   d0              ; negate if negative
.positive:
    andi.l  #$FFFF,d0       ; zero-extend
    rts

; ============================================================================
; Angle Comparison
; ============================================================================

; angle_between(angle: u16, start: u16, end: u16) -> bool
; Check if angle is between start and end (clockwise)
    xdef _asm_angle_between
_asm_angle_between:
    move.w  4(sp),d0        ; angle
    move.w  6(sp),d1        ; start
    sub.w   d1,d0           ; norm_angle = angle - start
    move.w  8(sp),d1        ; end
    sub.w   6(sp),d1        ; norm_end = end - start
    cmp.w   d1,d0           ; norm_angle <= norm_end?
    bls.s   .true
    moveq   #0,d0
    rts
.true:
    moveq   #1,d0
    rts

; angle_approx_eq(a: u16, b: u16, tolerance: u16) -> bool
; Check if two angles are approximately equal
    xdef _asm_angle_approx_eq
_asm_angle_approx_eq:
    move.w  6(sp),d0        ; b
    sub.w   4(sp),d0        ; b - a (signed diff)
    bpl.s   .positive
    neg.w   d0
.positive:
    ; d0 = abs diff
    cmp.w   8(sp),d0        ; diff <= tolerance?
    bls.s   .true
    moveq   #0,d0
    rts
.true:
    moveq   #1,d0
    rts

; ============================================================================
; Angle Snapping
; ============================================================================

; angle_snap(angle: u16, snap_angle: u16) -> u16
; Snap angle to nearest multiple of snap_angle
    xdef _asm_angle_snap
_asm_angle_snap:
    move.l  d2,-(sp)        ; save d2
    move.w  8(sp),d0        ; angle (4 + 4 for saved d2)
    move.w  10(sp),d1       ; snap_angle
    beq.s   .return_angle   ; if snap_angle == 0, return angle as-is

    ; half_snap = snap_angle >> 1
    move.w  d1,d2
    lsr.w   #1,d2

    ; adjusted = angle + half_snap
    add.w   d2,d0

    ; quotient = adjusted / snap_angle
    andi.l  #$FFFF,d0       ; zero-extend for division
    andi.l  #$FFFF,d1
    divu.w  d1,d0           ; d0.w = quotient

    ; result = quotient * snap_angle
    mulu.w  10(sp),d0       ; multiply by snap_angle
    andi.l  #$FFFF,d0       ; ensure u16 result

    move.l  (sp)+,d2        ; restore d2
    rts

.return_angle:
    move.l  (sp)+,d2        ; restore d2
    rts

; ============================================================================
; Direction Helpers
; ============================================================================

; angle_to_8way(angle: u16) -> u8
; Get 8-way direction index (0-7)
    xdef _asm_angle_to_8way
_asm_angle_to_8way:
    move.w  4(sp),d0        ; angle
    addi.w  #4096,d0        ; add half sector (22.5 deg)
    andi.l  #$FFFF,d0       ; zero-extend
    divu.w  #8192,d0        ; divide by 45 deg
    andi.l  #7,d0           ; mask to 0-7
    rts

; angle_to_4way(angle: u16) -> u8
; Get 4-way direction index (0-3)
    xdef _asm_angle_to_4way
_asm_angle_to_4way:
    move.w  4(sp),d0        ; angle
    addi.w  #8192,d0        ; add half sector (45 deg)
    andi.l  #$FFFF,d0       ; zero-extend
    divu.w  #16384,d0       ; divide by 90 deg
    andi.l  #3,d0           ; mask to 0-3
    rts

; angle_from_8way(direction: u8) -> u16
; Convert 8-way direction to angle
    xdef _asm_angle_from_8way
_asm_angle_from_8way:
    moveq   #0,d0
    move.b  7(sp),d0        ; direction (byte at offset 4+3)
    mulu.w  #8192,d0        ; * 45 deg
    rts

; angle_from_4way(direction: u8) -> u16
; Convert 4-way direction to angle
    xdef _asm_angle_from_4way
_asm_angle_from_4way:
    moveq   #0,d0
    move.b  7(sp),d0        ; direction
    mulu.w  #16384,d0       ; * 90 deg
    rts

; ============================================================================
; Quadrant Helpers
; ============================================================================

; quadrant(angle: u16) -> u8
; Get quadrant (0-3)
    xdef _asm_quadrant
_asm_quadrant:
    move.w  4(sp),d0        ; angle
    lsr.w   #8,d0           ; shift right by 8
    lsr.w   #6,d0           ; shift right by 6 more (total 14)
    andi.l  #3,d0           ; mask to 0-3
    rts

; facing_right(angle: u16) -> bool
; Check if angle is in right half (-90 to +90, i.e., 270-90 with wrap)
    xdef _asm_facing_right
_asm_facing_right:
    move.w  4(sp),d0        ; angle
    ; Right half: angle >= 49152 OR angle <= 16384
    cmpi.w  #49152,d0
    bhs.s   .true           ; unsigned: >= 49152
    cmpi.w  #16384,d0
    bls.s   .true           ; unsigned: <= 16384
    moveq   #0,d0
    rts
.true:
    moveq   #1,d0
    rts

; facing_left(angle: u16) -> bool
; Check if angle is in left half
    xdef _asm_facing_left
_asm_facing_left:
    move.w  4(sp),d0
    ; Left half: angle > 16384 AND angle < 49152
    cmpi.w  #16384,d0
    bls.s   .false
    cmpi.w  #49152,d0
    bhs.s   .false
    moveq   #1,d0
    rts
.false:
    moveq   #0,d0
    rts

; facing_up(angle: u16) -> bool
; Check if angle < 32768 (ANGLE_HALF)
    xdef _asm_facing_up
_asm_facing_up:
    move.w  4(sp),d0
    cmpi.w  #32768,d0
    bhs.s   .false
    moveq   #1,d0
    rts
.false:
    moveq   #0,d0
    rts

; facing_down(angle: u16) -> bool
; Check if angle >= 32768 (ANGLE_HALF)
    xdef _asm_facing_down
_asm_facing_down:
    move.w  4(sp),d0
    cmpi.w  #32768,d0
    blo.s   .false
    moveq   #1,d0
    rts
.false:
    moveq   #0,d0
    rts

; ============================================================================
; Angle Movement
; ============================================================================

; angle_move_towards(current: u16, target: u16, max_delta: u16) -> u16
; Move angle towards target by at most max_delta (shortest path)
    xdef _asm_angle_move_towards
_asm_angle_move_towards:
    move.l  d2,-(sp)        ; save d2
    move.w  8(sp),d0        ; current
    move.w  10(sp),d1       ; target
    move.w  12(sp),d2       ; max_delta

    ; Calculate signed diff = target - current
    sub.w   d0,d1           ; d1 = target - current (signed)
    beq.s   .at_target      ; if diff == 0, return target

    ; Get absolute diff
    move.w  d1,a0           ; save signed diff in a0 (temporarily)
    bpl.s   .pos_diff
    neg.w   d1              ; d1 = abs(diff)
.pos_diff:

    ; Can we reach target in one step?
    cmp.w   d2,d1           ; abs_diff <= max_delta?
    bls.s   .at_target      ; yes, return target

    ; Move towards target
    move.w  a0,d1           ; restore signed diff
    tst.w   d1
    bmi.s   .move_neg
    ; diff > 0: move forward
    add.w   d2,d0           ; current + max_delta
    move.l  (sp)+,d2
    andi.l  #$FFFF,d0
    rts
.move_neg:
    ; diff < 0: move backward
    sub.w   d2,d0           ; current - max_delta
    move.l  (sp)+,d2
    andi.l  #$FFFF,d0
    rts

.at_target:
    move.w  10(sp),d0       ; return target
    move.l  (sp)+,d2
    andi.l  #$FFFF,d0
    rts

    end
