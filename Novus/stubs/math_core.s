; math_core.s - Core math functions in pure 68020+ assembly
; Calling convention: VBCC __regargs
;   Args: d0, d1, a0, a1 (left to right)
;   Return: d0 (d0:d1 for 64-bit)
;   Preserve: d2-d7, a2-a6
;   Volatile: d0, d1, a0, a1

    section CODE,code

; ============================================================================
; Absolute Value
; ============================================================================

; abs_i32(x: i32) -> i32
; Returns |x|
    xdef _asm_abs_i32
_asm_abs_i32:
    tst.l   d0
    bpl.s   .done           ; if positive, already done
    neg.l   d0              ; negate if negative
.done:
    rts

; abs_i16(x: i16) -> i16
    xdef _asm_abs_i16
_asm_abs_i16:
    tst.w   d0
    bpl.s   .done
    neg.w   d0
.done:
    rts

; abs_i8(x: i8) -> i8
    xdef _asm_abs_i8
_asm_abs_i8:
    tst.b   d0
    bpl.s   .done
    neg.b   d0
.done:
    rts

; ============================================================================
; Minimum (signed)
; ============================================================================

; min_i32(a: i32, b: i32) -> i32
    xdef _asm_min_i32
_asm_min_i32:
    cmp.l   d1,d0           ; compare a - b
    ble.s   .done           ; if a <= b, return a (already in d0)
    move.l  d1,d0           ; else return b
.done:
    rts

; min_i16(a: i16, b: i16) -> i16
    xdef _asm_min_i16
_asm_min_i16:
    cmp.w   d1,d0
    ble.s   .done
    move.w  d1,d0
.done:
    rts

; min_i8(a: i8, b: i8) -> i8
    xdef _asm_min_i8
_asm_min_i8:
    cmp.b   d1,d0
    ble.s   .done
    move.b  d1,d0
.done:
    rts

; ============================================================================
; Minimum (unsigned)
; ============================================================================

; min_u32(a: u32, b: u32) -> u32
    xdef _asm_min_u32
_asm_min_u32:
    cmp.l   d1,d0
    bls.s   .done           ; bls = branch if lower or same (unsigned)
    move.l  d1,d0
.done:
    rts

; min_u16(a: u16, b: u16) -> u16
    xdef _asm_min_u16
_asm_min_u16:
    cmp.w   d1,d0
    bls.s   .done
    move.w  d1,d0
.done:
    rts

; min_u8(a: u8, b: u8) -> u8
    xdef _asm_min_u8
_asm_min_u8:
    cmp.b   d1,d0
    bls.s   .done
    move.b  d1,d0
.done:
    rts

; ============================================================================
; Maximum (signed)
; ============================================================================

; max_i32(a: i32, b: i32) -> i32
    xdef _asm_max_i32
_asm_max_i32:
    cmp.l   d1,d0
    bge.s   .done           ; if a >= b, return a
    move.l  d1,d0
.done:
    rts

; max_i16(a: i16, b: i16) -> i16
    xdef _asm_max_i16
_asm_max_i16:
    cmp.w   d1,d0
    bge.s   .done
    move.w  d1,d0
.done:
    rts

; max_i8(a: i8, b: i8) -> i8
    xdef _asm_max_i8
_asm_max_i8:
    cmp.b   d1,d0
    bge.s   .done
    move.b  d1,d0
.done:
    rts

; ============================================================================
; Maximum (unsigned)
; ============================================================================

; max_u32(a: u32, b: u32) -> u32
    xdef _asm_max_u32
_asm_max_u32:
    cmp.l   d1,d0
    bhs.s   .done           ; bhs = branch if higher or same (unsigned)
    move.l  d1,d0
.done:
    rts

; max_u16(a: u16, b: u16) -> u16
    xdef _asm_max_u16
_asm_max_u16:
    cmp.w   d1,d0
    bhs.s   .done
    move.w  d1,d0
.done:
    rts

; max_u8(a: u8, b: u8) -> u8
    xdef _asm_max_u8
_asm_max_u8:
    cmp.b   d1,d0
    bhs.s   .done
    move.b  d1,d0
.done:
    rts

; ============================================================================
; Clamp (signed)
; ============================================================================

; clamp_i32(val: i32, min_val: i32, max_val: i32) -> i32
; Args: d0 = val, d1 = min_val, d2 = max_val
    xdef _asm_clamp_i32
_asm_clamp_i32:
    ; First clamp to min
    cmp.l   d1,d0
    bge.s   .check_max
    move.l  d1,d0           ; val < min, return min
    rts
.check_max:
    ; Now clamp to max
    cmp.l   d2,d0
    ble.s   .done
    move.l  d2,d0           ; val > max, return max
.done:
    rts

; clamp_i16(val: i16, min_val: i16, max_val: i16) -> i16
; All three integer arguments use data registers.
    xdef _asm_clamp_i16
_asm_clamp_i16:
    cmp.w   d1,d0
    bge.s   .check_max
    move.w  d1,d0
    rts
.check_max:
    cmp.w   d2,d0
    ble.s   .done
    move.w  d2,d0
.done:
    rts

; clamp_i8(val: i8, min_val: i8, max_val: i8) -> i8
    xdef _asm_clamp_i8
_asm_clamp_i8:
    cmp.b   d1,d0
    bge.s   .check_max
    move.b  d1,d0
    rts
.check_max:
    cmp.b   d2,d0
    ble.s   .done
    move.b  d2,d0
.done:
    rts

; ============================================================================
; Clamp (unsigned)
; ============================================================================

; clamp_u32(val: u32, min_val: u32, max_val: u32) -> u32
    xdef _asm_clamp_u32
_asm_clamp_u32:
    cmp.l   d1,d0
    bhs.s   .check_max      ; unsigned: higher or same
    move.l  d1,d0
    rts
.check_max:
    cmp.l   d2,d0
    bls.s   .done           ; unsigned: lower or same
    move.l  d2,d0
.done:
    rts

; clamp_u16(val: u16, min_val: u16, max_val: u16) -> u16
    xdef _asm_clamp_u16
_asm_clamp_u16:
    cmp.w   d1,d0
    bhs.s   .check_max
    move.w  d1,d0
    rts
.check_max:
    cmp.w   d2,d0
    bls.s   .done
    move.w  d2,d0
.done:
    rts

; clamp_u8(val: u8, min_val: u8, max_val: u8) -> u8
    xdef _asm_clamp_u8
_asm_clamp_u8:
    cmp.b   d1,d0
    bhs.s   .check_max
    move.b  d1,d0
    rts
.check_max:
    cmp.b   d2,d0
    bls.s   .done
    move.b  d2,d0
.done:
    rts

; ============================================================================
; Sign
; ============================================================================

; sign_i32(x: i32) -> i32
; Returns -1 if negative, 0 if zero, 1 if positive
    xdef _asm_sign_i32
_asm_sign_i32:
    tst.l   d0
    beq.s   .zero
    bmi.s   .negative
    moveq   #1,d0
    rts
.negative:
    moveq   #-1,d0
    rts
.zero:
    moveq   #0,d0
    rts

; sign_i16(x: i16) -> i16
    xdef _asm_sign_i16
_asm_sign_i16:
    tst.w   d0
    beq.s   .zero
    bmi.s   .negative
    moveq   #1,d0
    rts
.negative:
    moveq   #-1,d0
    rts
.zero:
    moveq   #0,d0
    rts

; sign_i8(x: i8) -> i8
    xdef _asm_sign_i8
_asm_sign_i8:
    tst.b   d0
    beq.s   .zero
    bmi.s   .negative
    moveq   #1,d0
    rts
.negative:
    moveq   #-1,d0
    rts
.zero:
    moveq   #0,d0
    rts

; ============================================================================
; Power of Two
; ============================================================================

; is_power_of_two_u32(x: u32) -> bool
; Returns 1 if x is a power of two, 0 otherwise
; A number is power of two if x != 0 && (x & (x-1)) == 0
    xdef _asm_is_power_of_two_u32
_asm_is_power_of_two_u32:
    tst.l   d0
    beq.s   .false          ; x == 0, return false
    move.l  d0,d1
    subq.l  #1,d1           ; d1 = x - 1
    and.l   d0,d1           ; d1 = x & (x - 1)
    bne.s   .false          ; if not zero, not power of two
    moveq   #1,d0
    rts
.false:
    moveq   #0,d0
    rts

; is_power_of_two_u16(x: u16) -> bool
    xdef _asm_is_power_of_two_u16
_asm_is_power_of_two_u16:
    tst.w   d0
    beq.s   .false
    move.w  d0,d1
    subq.w  #1,d1
    and.w   d0,d1
    bne.s   .false
    moveq   #1,d0
    rts
.false:
    moveq   #0,d0
    rts

; next_power_of_two_u32(x: u32) -> u32
; Round up to the next power of two
    xdef _asm_next_power_of_two_u32
_asm_next_power_of_two_u32:
    tst.l   d0
    beq.s   .done           ; if 0, return 0
    subq.l  #1,d0           ; n = x - 1
    move.l  d0,d1
    lsr.l   #1,d1
    or.l    d1,d0           ; n |= n >> 1
    move.l  d0,d1
    lsr.l   #2,d1
    or.l    d1,d0           ; n |= n >> 2
    move.l  d0,d1
    lsr.l   #4,d1
    or.l    d1,d0           ; n |= n >> 4
    move.l  d0,d1
    lsr.l   #8,d1
    or.l    d1,d0           ; n |= n >> 8
    move.l  d0,d1
    swap    d1              ; lsr.l #16 via swap
    clr.w   d1
    or.l    d1,d0           ; n |= n >> 16
    addq.l  #1,d0           ; n + 1
.done:
    rts

; next_power_of_two_u16(x: u16) -> u16
    xdef _asm_next_power_of_two_u16
_asm_next_power_of_two_u16:
    tst.w   d0
    beq.s   .done
    subq.w  #1,d0
    move.w  d0,d1
    lsr.w   #1,d1
    or.w    d1,d0
    move.w  d0,d1
    lsr.w   #2,d1
    or.w    d1,d0
    move.w  d0,d1
    lsr.w   #4,d1
    or.w    d1,d0
    move.w  d0,d1
    lsr.w   #8,d1
    or.w    d1,d0
    addq.w  #1,d0
.done:
    rts

; ============================================================================
; Division with Rounding
; ============================================================================

; div_ceil_u32(a: u32, b: u32) -> u32
; Returns (a + b - 1) / b  (ceiling division)
    xdef _asm_div_ceil_u32
_asm_div_ceil_u32:
    add.l   d1,d0           ; a + b
    subq.l  #1,d0           ; a + b - 1
    divu.l  d1,d0           ; / b
    rts

; div_ceil_u16(a: u16, b: u16) -> u16
    xdef _asm_div_ceil_u16
_asm_div_ceil_u16:
    add.w   d1,d0
    subq.w  #1,d0
    and.l   #$FFFF,d0       ; zero-extend for divu
    and.l   #$FFFF,d1
    divu.w  d1,d0           ; 32/16 -> 16 quotient in low word
    rts

; div_round_u32(a: u32, b: u32) -> u32
; Returns (a + b/2) / b  (round to nearest)
    xdef _asm_div_round_u32
_asm_div_round_u32:
    move.l  d2,-(sp)        ; save d2
    move.l  d1,d2
    lsr.l   #1,d2           ; b / 2
    add.l   d2,d0           ; a + b/2
    divu.l  d1,d0           ; / b
    move.l  (sp)+,d2        ; restore d2
    rts

; div_round_u16(a: u16, b: u16) -> u16
    xdef _asm_div_round_u16
_asm_div_round_u16:
    move.w  d1,d2
    lsr.w   #1,d2           ; b / 2 (d2 is volatile for 16-bit ops in practice)
    add.w   d2,d0           ; a + b/2
    and.l   #$FFFF,d0
    and.l   #$FFFF,d1
    divu.w  d1,d0
    rts

; ============================================================================
; Difference (absolute)
; ============================================================================

; diff_u32(a: u32, b: u32) -> u32
; Returns |a - b| for unsigned values
    xdef _asm_diff_u32
_asm_diff_u32:
    cmp.l   d1,d0
    bhs.s   .a_bigger       ; if a >= b (unsigned)
    exg     d0,d1           ; swap so we compute b - a
.a_bigger:
    sub.l   d1,d0
    rts

; diff_u16(a: u16, b: u16) -> u16
    xdef _asm_diff_u16
_asm_diff_u16:
    cmp.w   d1,d0
    bhs.s   .a_bigger
    exg     d0,d1
.a_bigger:
    sub.w   d1,d0
    rts

; diff_i32(a: i32, b: i32) -> i32
; Returns |a - b|
    xdef _asm_diff_i32
_asm_diff_i32:
    sub.l   d1,d0           ; a - b
    bpl.s   .done           ; if positive, we're done
    neg.l   d0              ; else negate
.done:
    rts

; diff_i16(a: i16, b: i16) -> i16
    xdef _asm_diff_i16
_asm_diff_i16:
    sub.w   d1,d0
    bpl.s   .done
    neg.w   d0
.done:
    rts

    end
