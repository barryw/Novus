; math_fixed.s - Optimized 68020+ fixed-point arithmetic
; Part of Novus standard library
;
; All functions follow VBCC __regargs calling convention:
; - Arguments in d0, d1, a0, a1 (register-based, NOT stack)
; - Return value in d0 (d0/d1 for 64-bit)
; - Preserve d2-d7, a2-a6
; - Scratch: d0, d1, a0, a1
; - Symbols exported with @ prefix for __regargs linkage

	section	"CODE",code

; ============================================================================
; Fixed32 (16.16) Multiplication
; ============================================================================

; @fixed32_mul - Multiply two Fixed32 values
; Input: d0.l = a (16.16 fixed), d1.l = b (16.16 fixed)
; Output: d0.l = a * b (16.16 fixed)
; Uses 68020+ MULS.L for 32x32->64 bit multiply
	xdef	@fixed32_mul
@fixed32_mul:
	; 32x32->64 signed multiply: d0 * d1 -> d1:d0
	muls.l	d1,d1:d0	; d1:d0 = (i64)a * (i64)b

	; Result is in 32.32 format, we need 16.16
	; Shift right 16 bits = take middle 32 bits
	; d1 has high 32 bits, d0 has low 32 bits
	; We want bits 47:16, which is (d1 << 16) | (d0 >> 16)
	lsr.l	#8,d0		; Shift low word right 8
	lsr.l	#8,d0		; Shift low word right 8 more (total 16)
	swap	d1		; Move low 16 bits of d1 to high position
	clr.w	d1		; Clear low 16 bits of d1
	or.l	d1,d0		; Combine: d0 = (d1 << 16) | (d0 >> 16)
	rts

; ============================================================================
; Fixed32 (16.16) Division
; ============================================================================

; @fixed32_div - Divide two Fixed32 values
; Input: d0.l = a (16.16 fixed dividend), d1.l = b (16.16 fixed divisor)
; Output: d0.l = a / b (16.16 fixed)
; Uses 68020+ DIVS.L for 64/32->32 bit divide
	xdef	@fixed32_div
@fixed32_div:
	; Check for divide by zero
	tst.l	d1
	beq.s	.div_zero

	movem.l	d2-d3,-(sp)

	; We need to compute (a << 16) / b
	; Set up 64-bit dividend in d2:d0
	; a << 16 means: high 32 bits = a >> 16, low 32 bits = a << 16
	move.l	d0,d2		; d2 = a
	move.l	d0,d3		; d3 = a (save for low part)
	asr.l	#8,d2		; d2 = a >> 8 (signed shift)
	asr.l	#8,d2		; d2 = a >> 16 (high 32 bits of a << 16)
	swap	d3		; Move low 16 bits to high
	clr.w	d3		; Clear low 16 bits (d3 = a << 16, low 32 bits)
	move.l	d3,d0		; d0 = low 32 bits

	; 64/32 signed divide: d2:d0 / d1 -> d0 quotient, d2 remainder
	divs.l	d1,d2:d0

	movem.l	(sp)+,d2-d3
	rts

.div_zero:
	; Return maximum value on divide by zero
	move.l	#$7FFFFFFF,d0
	rts

; ============================================================================
; Fixed32 Multiply by Integer (faster special case)
; ============================================================================

; @fixed32_mul_i32 - Multiply Fixed32 by integer
; Input: d0.l = a (16.16 fixed), d1.l = b (integer scalar)
; Output: d0.l = a * b (16.16 fixed)
; Simple 32x32->32 multiply since no shift needed
	xdef	@fixed32_mul_i32
@fixed32_mul_i32:
	muls.l	d1,d0		; 32x32->32, result in d0
	rts

; ============================================================================
; Fixed32 Divide by Integer (faster special case)
; ============================================================================

; @fixed32_div_i32 - Divide Fixed32 by integer
; Input: d0.l = a (16.16 fixed), d1.l = b (integer divisor)
; Output: d0.l = a / b (16.16 fixed)
	xdef	@fixed32_div_i32
@fixed32_div_i32:
	tst.l	d1
	beq.s	.div_i32_zero
	divs.l	d1,d0		; 32/32->32
	rts

.div_i32_zero:
	move.l	#$7FFFFFFF,d0
	rts

; ============================================================================
; Fixed32 from Integer
; ============================================================================

; @fixed32_from_i32 - Convert i32 to Fixed32
; Input: d0.l = integer value
; Output: d0.l = Fixed32 (value << 16)
	xdef	@fixed32_from_i32
@fixed32_from_i32:
	swap	d0		; Move low 16 bits to high (same as << 16)
	clr.w	d0		; Clear low 16 bits
	rts

; ============================================================================
; Fixed32 to Integer (truncate)
; ============================================================================

; @fixed32_to_i32 - Convert Fixed32 to i32 (truncates fractional part)
; Input: d0.l = Fixed32 value
; Output: d0.l = integer part
	xdef	@fixed32_to_i32
@fixed32_to_i32:
	asr.l	#8,d0		; Shift right 8 (signed)
	asr.l	#8,d0		; Shift right 8 more (total 16)
	rts

; ============================================================================
; Fixed32 to Integer (rounded)
; ============================================================================

; @fixed32_to_i32_rounded - Convert Fixed32 to i32 with rounding
; Input: d0.l = Fixed32 value
; Output: d0.l = rounded integer part
	xdef	@fixed32_to_i32_rounded
@fixed32_to_i32_rounded:
	add.l	#$8000,d0	; Add 0.5 (FIXED32_HALF)
	asr.l	#8,d0		; Shift right 8 (signed)
	asr.l	#8,d0		; Shift right 8 more (total 16)
	rts

; ============================================================================
; Fixed16 (8.8) Multiplication
; ============================================================================

; @fixed16_mul - Multiply two Fixed16 values
; Input: d0.w = a (8.8 fixed), d1.w = b (8.8 fixed)
; Output: d0.w = a * b (8.8 fixed)
	xdef	@fixed16_mul
@fixed16_mul:
	muls.w	d1,d0		; 16x16->32 signed multiply
	asr.l	#8,d0		; Shift right 8 to get 8.8 result
	rts

; ============================================================================
; Fixed16 (8.8) Division
; ============================================================================

; @fixed16_div - Divide two Fixed16 values
; Input: d0.w = a (8.8 fixed dividend), d1.w = b (8.8 fixed divisor)
; Output: d0.w = a / b (8.8 fixed)
	xdef	@fixed16_div
@fixed16_div:
	tst.w	d1
	beq.s	.div16_zero

	ext.l	d0		; Sign-extend a to 32 bits
	lsl.l	#8,d0		; Shift left 8 (a << 8)
	ext.l	d1		; Sign-extend b to 32 bits
	divs.l	d1,d0		; 32/32->32 divide
	rts

.div16_zero:
	move.w	#$7FFF,d0
	rts

; ============================================================================
; Fixed32 Absolute Value
; ============================================================================

; @fixed32_abs - Absolute value of Fixed32
; Input: d0.l = Fixed32 value
; Output: d0.l = |value|
	xdef	@fixed32_abs
@fixed32_abs:
	tst.l	d0
	bpl.s	.abs_done
	neg.l	d0
.abs_done:
	rts

; ============================================================================
; Fixed32 Negate
; ============================================================================

; @fixed32_neg - Negate Fixed32
; Input: d0.l = Fixed32 value
; Output: d0.l = -value
	xdef	@fixed32_neg
@fixed32_neg:
	neg.l	d0
	rts

; ============================================================================
; Fixed32 Lerp (Linear Interpolation)
; ============================================================================

; @fixed32_lerp - Linear interpolation between two Fixed32 values
; Input: d0.l = a (start), d1.l = b (end), 4(sp) = t (0.0 to 1.0 as Fixed32)
; Output: d0.l = a + (b - a) * t
	xdef	@fixed32_lerp
@fixed32_lerp:
	movem.l	d2-d3,-(sp)
	move.l	12(sp),d2	; t from stack

	sub.l	d0,d1		; d1 = b - a

	; Multiply (b - a) * t using 64-bit intermediate
	muls.l	d2,d3:d1	; d3:d1 = (b - a) * t

	; Extract middle 32 bits (shift right 16)
	lsr.l	#8,d1
	lsr.l	#8,d1
	swap	d3
	clr.w	d3
	or.l	d3,d1

	add.l	d1,d0		; d0 = a + (b - a) * t

	movem.l	(sp)+,d2-d3
	rts

	end
