; math_vec2.s - Optimized 68020+ 2D vector operations
; Part of Novus standard library
;
; All functions follow VBCC __regargs calling convention:
; - Arguments in d0, d1, a0, a1 (register-based, NOT stack)
; - Return value in d0 (d0/d1 for 64-bit or struct)
; - Preserve d2-d7, a2-a6
; - Scratch: d0, d1, a0, a1
; - Symbols exported with @ prefix for __regargs linkage
;
; Vec2 representation:
; - Two Fixed32 (16.16) values: x and y (8 bytes total)
; - Passed as pointers to structs
;
; Vec2i representation:
; - Two i32 values: x and y (8 bytes total)

	section	"CODE",code

; ============================================================================
; Vec2 (Fixed32) ARITHMETIC OPERATIONS
; ============================================================================

; @vec2_add - Add two Vec2 vectors
; Input: a0 = v1, a1 = v2
; Output: d0 = x result, d1 = y result
	xdef	@vec2_add
@vec2_add:
	move.l	(a0),d0		; x1
	add.l	(a1),d0		; x1 + x2
	move.l	4(a0),d1	; y1
	add.l	4(a1),d1	; y1 + y2
	rts

; @vec2_sub - Subtract two Vec2 vectors
; Input: a0 = v1, a1 = v2
; Output: d0 = x result, d1 = y result
	xdef	@vec2_sub
@vec2_sub:
	move.l	(a0),d0		; x1
	sub.l	(a1),d0		; x1 - x2
	move.l	4(a0),d1	; y1
	sub.l	4(a1),d1	; y1 - y2
	rts

; @vec2_neg - Negate a Vec2 vector
; Input: a0 = v
; Output: d0 = -x, d1 = -y
	xdef	@vec2_neg
@vec2_neg:
	move.l	(a0),d0
	neg.l	d0
	move.l	4(a0),d1
	neg.l	d1
	rts

; @vec2_scale - Scale Vec2 by Fixed32
; Input: a0 = v, d0 = scale factor (Fixed32)
; Output: d0 = scaled x, d1 = scaled y
	xdef	@vec2_scale
@vec2_scale:
	movem.l	d2-d5,-(sp)
	move.l	d0,d2		; d2 = scale factor

	; x * scale
	move.l	(a0),d0
	muls.l	d2,d3:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d3
	clr.w	d3
	or.l	d3,d0		; d0 = scaled x
	move.l	d0,d4		; save in d4

	; y * scale
	move.l	4(a0),d0
	muls.l	d2,d5:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d5
	clr.w	d5
	or.l	d5,d0
	move.l	d0,d1		; d1 = scaled y
	move.l	d4,d0		; d0 = scaled x

	movem.l	(sp)+,d2-d5
	rts

; @vec2_scale_i32 - Scale Vec2 by integer
; Input: a0 = v, d0 = scale factor (i32)
; Output: d0 = scaled x, d1 = scaled y
	xdef	@vec2_scale_i32
@vec2_scale_i32:
	move.l	d2,-(sp)
	move.l	d0,d2		; d2 = scale factor

	move.l	(a0),d0
	muls.l	d2,d0		; x * scale (no shift needed for integer scale)

	move.l	4(a0),d1
	muls.l	d2,d1		; y * scale

	move.l	(sp)+,d2
	rts

; @vec2_div_i32 - Divide Vec2 by integer
; Input: a0 = v, d0 = divisor (i32)
; Output: d0 = divided x, d1 = divided y
	xdef	@vec2_div_i32
@vec2_div_i32:
	move.l	d2,-(sp)
	move.l	d0,d2		; d2 = divisor

	tst.l	d2
	beq.s	.div_zero

	move.l	(a0),d0
	divs.l	d2,d0		; x / divisor

	move.l	4(a0),d1
	divs.l	d2,d1		; y / divisor

	move.l	(sp)+,d2
	rts

.div_zero:
	move.l	#$7FFFFFFF,d0
	move.l	#$7FFFFFFF,d1
	move.l	(sp)+,d2
	rts

; @vec2_hadamard - Component-wise multiply (Hadamard product)
; Input: a0 = v1, a1 = v2
; Output: d0 = x1*x2, d1 = y1*y2
	xdef	@vec2_hadamard
@vec2_hadamard:
	movem.l	d2-d5,-(sp)

	; x1 * x2
	move.l	(a0),d0
	move.l	(a1),d1
	muls.l	d1,d2:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d2
	clr.w	d2
	or.l	d2,d0
	move.l	d0,d4		; save x result

	; y1 * y2
	move.l	4(a0),d0
	move.l	4(a1),d1
	muls.l	d1,d3:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d3
	clr.w	d3
	or.l	d3,d0
	move.l	d0,d1		; d1 = y result
	move.l	d4,d0		; d0 = x result

	movem.l	(sp)+,d2-d5
	rts

; @vec2_dot - Dot product of two Vec2
; Input: a0 = v1, a1 = v2
; Output: d0 = x1*x2 + y1*y2 (Fixed32)
	xdef	@vec2_dot
@vec2_dot:
	movem.l	d2-d5,-(sp)

	; x1 * x2
	move.l	(a0),d0
	move.l	(a1),d2
	muls.l	d2,d4:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d4
	clr.w	d4
	or.l	d4,d0		; d0 = x1*x2

	; y1 * y2
	move.l	4(a0),d1
	move.l	4(a1),d3
	muls.l	d3,d5:d1
	lsr.l	#8,d1
	lsr.l	#8,d1
	swap	d5
	clr.w	d5
	or.l	d5,d1		; d1 = y1*y2

	add.l	d1,d0		; d0 = dot product

	movem.l	(sp)+,d2-d5
	rts

; @vec2_cross - Cross product (Z component) of two Vec2
; Input: a0 = v1, a1 = v2
; Output: d0 = x1*y2 - y1*x2 (Fixed32)
	xdef	@vec2_cross
@vec2_cross:
	movem.l	d2-d5,-(sp)

	; x1 * y2
	move.l	(a0),d0
	move.l	4(a1),d2
	muls.l	d2,d4:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d4
	clr.w	d4
	or.l	d4,d0		; d0 = x1*y2

	; y1 * x2
	move.l	4(a0),d1
	move.l	(a1),d3
	muls.l	d3,d5:d1
	lsr.l	#8,d1
	lsr.l	#8,d1
	swap	d5
	clr.w	d5
	or.l	d5,d1		; d1 = y1*x2

	sub.l	d1,d0		; d0 = cross product

	movem.l	(sp)+,d2-d5
	rts

; @vec2_length_sq - Squared length of Vec2
; Input: a0 = v
; Output: d0 = x*x + y*y (Fixed32)
	xdef	@vec2_length_sq
@vec2_length_sq:
	movem.l	d2-d4,-(sp)

	; x * x
	move.l	(a0),d0
	move.l	d0,d2
	muls.l	d2,d3:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d3
	clr.w	d3
	or.l	d3,d0		; d0 = x*x

	; y * y
	move.l	4(a0),d1
	move.l	d1,d2
	muls.l	d2,d4:d1
	lsr.l	#8,d1
	lsr.l	#8,d1
	swap	d4
	clr.w	d4
	or.l	d4,d1		; d1 = y*y

	add.l	d1,d0		; d0 = length squared

	movem.l	(sp)+,d2-d4
	rts

; @vec2_length_approx - Approximate length using alpha-max-beta-min
; Input: a0 = v
; Output: d0 = approximate length (Fixed32)
; Algorithm: length ≈ max(|x|,|y|) + 0.4 * min(|x|,|y|)
	xdef	@vec2_length_approx
@vec2_length_approx:
	movem.l	d2-d3,-(sp)

	; Get absolute values
	move.l	(a0),d0
	bpl.s	.x_pos
	neg.l	d0
.x_pos:
	move.l	4(a0),d1
	bpl.s	.y_pos
	neg.l	d1
.y_pos:

	; Ensure d0 = max, d1 = min
	cmp.l	d1,d0
	bge.s	.ordered
	exg	d0,d1
.ordered:

	; length ≈ max + (min * 26214) >> 16
	move.l	d1,d2
	muls.l	#26214,d3:d2
	lsr.l	#8,d2
	lsr.l	#8,d2
	swap	d3
	clr.w	d3
	or.l	d3,d2

	add.l	d2,d0

	movem.l	(sp)+,d2-d3
	rts

; @vec2_perp - Perpendicular vector (90° CCW)
; Input: a0 = v
; Output: d0 = -y, d1 = x
	xdef	@vec2_perp
@vec2_perp:
	move.l	4(a0),d0	; d0 = y
	neg.l	d0		; d0 = -y
	move.l	(a0),d1		; d1 = x
	rts

; @vec2_perp_cw - Perpendicular vector (90° CW)
; Input: a0 = v
; Output: d0 = y, d1 = -x
	xdef	@vec2_perp_cw
@vec2_perp_cw:
	move.l	4(a0),d0	; d0 = y
	move.l	(a0),d1		; d1 = x
	neg.l	d1		; d1 = -x
	rts

; @vec2_rotate - Rotate Vec2 by precomputed sin/cos
; Input: a0 = v, d0 = cos(angle), d1 = sin(angle)
; Output: d0 = x', d1 = y'
; Formula: x' = x*cos - y*sin, y' = x*sin + y*cos
	xdef	@vec2_rotate
@vec2_rotate:
	movem.l	d2-d7,-(sp)

	move.l	d0,d4		; d4 = cos
	move.l	d1,d5		; d5 = sin
	move.l	(a0),d2		; d2 = x
	move.l	4(a0),d3	; d3 = y

	; x * cos
	move.l	d2,d0
	muls.l	d4,d6:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d6
	clr.w	d6
	or.l	d6,d0
	move.l	d0,d6		; d6 = x*cos

	; y * sin
	move.l	d3,d0
	muls.l	d5,d7:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d7
	clr.w	d7
	or.l	d7,d0		; d0 = y*sin

	sub.l	d0,d6		; d6 = x' = x*cos - y*sin

	; x * sin
	move.l	d2,d0
	muls.l	d5,d7:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d7
	clr.w	d7
	or.l	d7,d0
	move.l	d0,d7		; d7 = x*sin

	; y * cos
	move.l	d3,d0
	muls.l	d4,d1:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d1
	clr.w	d1
	or.l	d1,d0		; d0 = y*cos

	add.l	d7,d0		; d0 = y' = x*sin + y*cos
	move.l	d0,d1		; d1 = y'
	move.l	d6,d0		; d0 = x'

	movem.l	(sp)+,d2-d7
	rts

; @vec2_lerp - Linear interpolation between two Vec2
; Input: a0 = v1, a1 = v2, d0 = t (Fixed32, 0-1)
; Output: d0 = lerp x, d1 = lerp y
; Formula: result = v1 + (v2 - v1) * t = v1*(1-t) + v2*t
	xdef	@vec2_lerp
@vec2_lerp:
	movem.l	d2-d7/a2,-(sp)

	move.l	d0,d7		; d7 = t

	; Calculate (1-t)
	move.l	#65536,d6	; 1.0 in Fixed32
	sub.l	d7,d6		; d6 = 1-t

	; x1 * (1-t)
	move.l	(a0),d0
	muls.l	d6,d2:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d2
	clr.w	d2
	or.l	d2,d0
	move.l	d0,d4		; d4 = x1*(1-t)

	; x2 * t
	move.l	(a1),d0
	muls.l	d7,d3:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d3
	clr.w	d3
	or.l	d3,d0
	add.l	d0,d4		; d4 = lerp x

	; y1 * (1-t)
	move.l	4(a0),d0
	muls.l	d6,d2:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d2
	clr.w	d2
	or.l	d2,d0
	move.l	d0,d5		; d5 = y1*(1-t)

	; y2 * t
	move.l	4(a1),d0
	muls.l	d7,d3:d0
	lsr.l	#8,d0
	lsr.l	#8,d0
	swap	d3
	clr.w	d3
	or.l	d3,d0
	add.l	d0,d5		; d5 = lerp y

	move.l	d4,d0		; d0 = lerp x
	move.l	d5,d1		; d1 = lerp y

	movem.l	(sp)+,d2-d7/a2
	rts

; @vec2_min - Component-wise minimum
; Input: a0 = v1, a1 = v2
; Output: d0 = min x, d1 = min y
	xdef	@vec2_min
@vec2_min:
	move.l	(a0),d0
	move.l	(a1),d1
	cmp.l	d1,d0
	ble.s	.x_ok
	move.l	d1,d0
.x_ok:
	move.l	4(a0),d1
	move.l	4(a1),a0	; borrow a0 for temp
	cmp.l	a0,d1
	ble.s	.y_ok
	move.l	a0,d1
.y_ok:
	rts

; @vec2_max - Component-wise maximum
; Input: a0 = v1, a1 = v2
; Output: d0 = max x, d1 = max y
	xdef	@vec2_max
@vec2_max:
	move.l	(a0),d0
	move.l	(a1),d1
	cmp.l	d1,d0
	bge.s	.x_ok
	move.l	d1,d0
.x_ok:
	move.l	4(a0),d1
	move.l	4(a1),a0
	cmp.l	a0,d1
	bge.s	.y_ok
	move.l	a0,d1
.y_ok:
	rts

; ============================================================================
; Vec2i (Integer) OPERATIONS
; ============================================================================

; @vec2i_add - Add two Vec2i
; Input: a0 = v1, a1 = v2
; Output: d0 = x, d1 = y
	xdef	@vec2i_add
@vec2i_add:
	move.l	(a0),d0
	add.l	(a1),d0
	move.l	4(a0),d1
	add.l	4(a1),d1
	rts

; @vec2i_sub - Subtract two Vec2i
; Input: a0 = v1, a1 = v2
; Output: d0 = x, d1 = y
	xdef	@vec2i_sub
@vec2i_sub:
	move.l	(a0),d0
	sub.l	(a1),d0
	move.l	4(a0),d1
	sub.l	4(a1),d1
	rts

; @vec2i_neg - Negate Vec2i
; Input: a0 = v
; Output: d0 = -x, d1 = -y
	xdef	@vec2i_neg
@vec2i_neg:
	move.l	(a0),d0
	neg.l	d0
	move.l	4(a0),d1
	neg.l	d1
	rts

; @vec2i_scale - Scale Vec2i by integer
; Input: a0 = v, d0 = scale
; Output: d0 = scaled x, d1 = scaled y
	xdef	@vec2i_scale
@vec2i_scale:
	move.l	d2,-(sp)
	move.l	d0,d2

	move.l	(a0),d0
	muls.l	d2,d0

	move.l	4(a0),d1
	muls.l	d2,d1

	move.l	(sp)+,d2
	rts

; @vec2i_dot - Dot product of two Vec2i
; Input: a0 = v1, a1 = v2
; Output: d0 = x1*x2 + y1*y2
	xdef	@vec2i_dot
@vec2i_dot:
	move.l	d2,-(sp)

	move.l	(a0),d0
	move.l	(a1),d1
	muls.l	d1,d0

	move.l	4(a0),d1
	move.l	4(a1),d2
	muls.l	d2,d1

	add.l	d1,d0

	move.l	(sp)+,d2
	rts

; @vec2i_cross - Cross product (Z) of two Vec2i
; Input: a0 = v1, a1 = v2
; Output: d0 = x1*y2 - y1*x2
	xdef	@vec2i_cross
@vec2i_cross:
	move.l	d2,-(sp)

	move.l	(a0),d0
	move.l	4(a1),d1
	muls.l	d1,d0		; x1 * y2

	move.l	4(a0),d1
	move.l	(a1),d2
	muls.l	d2,d1		; y1 * x2

	sub.l	d1,d0

	move.l	(sp)+,d2
	rts

; @vec2i_length_sq - Squared length of Vec2i
; Input: a0 = v
; Output: d0 = x*x + y*y
	xdef	@vec2i_length_sq
@vec2i_length_sq:
	move.l	(a0),d0
	move.l	d0,d1
	muls.l	d1,d0		; x * x

	move.l	4(a0),d1
	muls.l	d1,d1		; y * y

	add.l	d1,d0
	rts

; @vec2i_perp - Perpendicular (90° CCW)
; Input: a0 = v
; Output: d0 = -y, d1 = x
	xdef	@vec2i_perp
@vec2i_perp:
	move.l	4(a0),d0
	neg.l	d0
	move.l	(a0),d1
	rts

; @vec2i_min - Component-wise minimum
; Input: a0 = v1, a1 = v2
; Output: d0 = min x, d1 = min y
	xdef	@vec2i_min
@vec2i_min:
	move.l	(a0),d0
	move.l	(a1),d1
	cmp.l	d1,d0
	ble.s	.xi_ok
	move.l	d1,d0
.xi_ok:
	move.l	4(a0),d1
	move.l	4(a1),a0
	cmp.l	a0,d1
	ble.s	.yi_ok
	move.l	a0,d1
.yi_ok:
	rts

; @vec2i_max - Component-wise maximum
; Input: a0 = v1, a1 = v2
; Output: d0 = max x, d1 = max y
	xdef	@vec2i_max
@vec2i_max:
	move.l	(a0),d0
	move.l	(a1),d1
	cmp.l	d1,d0
	bge.s	.xi_ok2
	move.l	d1,d0
.xi_ok2:
	move.l	4(a0),d1
	move.l	4(a1),a0
	cmp.l	a0,d1
	bge.s	.yi_ok2
	move.l	a0,d1
.yi_ok2:
	rts

	end
