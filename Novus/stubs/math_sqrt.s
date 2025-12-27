; math_sqrt.s - Optimized 68020+ assembly math routines
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
; FPU Detection
; ============================================================================

	xref	_SysBase

; Cache for FPU detection (0 = unchecked, 1 = no FPU, 2 = has FPU)
	section	"BSS",bss
_fpu_cache:
	ds.b	1
	even

	section	"CODE",code

; @math_has_fpu - Check if FPU is available (cached)
; Returns: d0 = 1 if FPU available, 0 if not
	xdef	@math_has_fpu
@math_has_fpu:
	lea	_fpu_cache,a0
	move.b	(a0),d0
	beq.s	.check_fpu		; 0 = not checked yet
	subq.b	#1,d0			; 1->0 (no FPU), 2->1 (has FPU)
	ext.w	d0
	ext.l	d0
	rts

.check_fpu:
	movem.l	d1/a0-a1,-(sp)
	move.l	_SysBase,a0
	move.w	296(a0),d0		; AttnFlags offset in ExecBase
	; Bit 4 = AFF_68881, Bit 5 = AFF_68882, Bit 6 = AFF_FPU40
	and.w	#$0070,d0		; Mask FPU bits
	beq.s	.no_fpu

	lea	_fpu_cache,a0
	move.b	#2,(a0)			; Has FPU
	moveq	#1,d0
	movem.l	(sp)+,d1/a0-a1
	rts

.no_fpu:
	lea	_fpu_cache,a0
	move.b	#1,(a0)			; No FPU
	moveq	#0,d0
	movem.l	(sp)+,d1/a0-a1
	rts

; ============================================================================
; Software Integer Square Root (u32) - Newton-Raphson with BFFFO
; ============================================================================

; _math_sqrt_sw_u32 - Software integer sqrt for u32
; Input: d0.l = value
; Output: d0.l = floor(sqrt(value))
; Preserves: d2-d7, a2-a6
; DEBUG: Simple echo function to test if values are passed correctly
	xdef	@math_echo_u32
@math_echo_u32:
	; Just return the input value unchanged
	rts

	xdef	@math_sqrt_sw_u32
@math_sqrt_sw_u32:
	tst.l	d0
	bne.s	.not_zero
	; sqrt(0) = 0, just return (d0 already 0)
	rts
.not_zero:
	cmp.l	#1,d0
	bne.s	.not_one
	; sqrt(1) = 1, just return (d0 already 1)
	rts
.not_one:
	movem.l	d2-d4,-(sp)
	move.l	d0,d2			; d2 = x (original value)

	; Use BFFFO to find highest set bit for initial guess
	; sqrt(x) ≈ 2^(log2(x)/2)
	bfffo	d0{0:32},d1		; d1 = number of leading zeros
	moveq	#31,d3
	sub.l	d1,d3			; d3 = bit position of highest set bit
	lsr.l	#1,d3			; d3 = bit_pos / 2
	moveq	#1,d4
	lsl.l	d3,d4			; d4 = initial guess = 2^(bit_pos/2)

	; Newton-Raphson: guess = (guess + x/guess) / 2
	; Use divul.l for proper 32/32 -> 32 division
	; 4 iterations is enough for 32-bit precision

	; Iteration 1
	move.l	d2,d0
	move.l	d4,d1
	divul.l	d1,d0			; d0 = d0 / d1 (32-bit quotient)
	add.l	d4,d0
	lsr.l	#1,d0
	move.l	d0,d4

	; Iteration 2
	move.l	d2,d0
	move.l	d4,d1
	divul.l	d1,d0
	add.l	d4,d0
	lsr.l	#1,d0
	move.l	d0,d4

	; Iteration 3
	move.l	d2,d0
	move.l	d4,d1
	divul.l	d1,d0
	add.l	d4,d0
	lsr.l	#1,d0
	move.l	d0,d4

	; Iteration 4
	move.l	d2,d0
	move.l	d4,d1
	divul.l	d1,d0
	add.l	d4,d0
	lsr.l	#1,d0

	; Final correction: if result^2 > x, subtract 1
	move.l	d0,d1
	mulu.l	d1,d1			; d1 = result^2
	cmp.l	d2,d1
	bls.s	.done
	subq.l	#1,d0
.done:
	movem.l	(sp)+,d2-d4
	rts


; ============================================================================
; Software Integer Square Root (i32)
; ============================================================================

; _math_sqrt_sw_i32 - Software integer sqrt for i32
; Input: d0.l = value (signed)
; Output: d0.l = floor(sqrt(value)) or 0 if negative
	xdef	@math_sqrt_sw_i32
@math_sqrt_sw_i32:
	tst.l	d0
	ble.s	.neg_or_zero
	bra	@math_sqrt_sw_u32	; Tail call to u32 version
.neg_or_zero:
	moveq	#0,d0
	rts

; ============================================================================
; Software Integer Square Root (u16)
; ============================================================================

; _math_sqrt_sw_u16 - Software integer sqrt for u16
; Input: d0.w = value
; Output: d0.w = floor(sqrt(value))
	xdef	@math_sqrt_sw_u16
@math_sqrt_sw_u16:
	and.l	#$FFFF,d0		; Zero-extend to 32-bit
	bra	@math_sqrt_sw_u32	; Use 32-bit version, result fits in 16 bits

; ============================================================================
; FPU Square Root (u32)
; ============================================================================

; _math_sqrt_fpu_u32 - FPU sqrt for u32
; Input: d0.l = value
; Output: d0.l = floor(sqrt(value))
	xdef	@math_sqrt_fpu_u32
@math_sqrt_fpu_u32:
	tst.l	d0
	beq.s	.fpu_zero
	fmove.l	d0,fp0			; Convert to float
	fsqrt.x	fp0,fp0			; Hardware sqrt
	fintrz.x fp0,fp0		; Truncate to integer
	fmove.l	fp0,d0			; Convert back
	rts
.fpu_zero:
	moveq	#0,d0
	rts

; ============================================================================
; FPU Square Root (i32)
; ============================================================================

; _math_sqrt_fpu_i32 - FPU sqrt for i32
; Input: d0.l = value (signed)
; Output: d0.l = floor(sqrt(value)) or 0 if negative
	xdef	@math_sqrt_fpu_i32
@math_sqrt_fpu_i32:
	tst.l	d0
	ble.s	.fpu_neg
	fmove.l	d0,fp0
	fsqrt.x	fp0,fp0
	fintrz.x fp0,fp0
	fmove.l	fp0,d0
	rts
.fpu_neg:
	moveq	#0,d0
	rts

; ============================================================================
; FPU Square Root (Fixed32 - 16.16 fixed point)
; ============================================================================

; _math_sqrt_fpu_fixed32 - FPU sqrt for 16.16 fixed point
; Input: d0.l = raw fixed-point value
; Output: d0.l = sqrt result as raw fixed-point
	xdef	@math_sqrt_fpu_fixed32
@math_sqrt_fpu_fixed32:
	tst.l	d0
	ble.s	.fixed_zero
	fmove.l	d0,fp0			; Load raw value
	fsqrt.x	fp0,fp0			; sqrt(raw)
	fmove.l	#256,fp1		; Scale factor (sqrt(65536) = 256)
	fmul.x	fp1,fp0			; Multiply to get 16.16 result
	fintrz.x fp0,fp0		; Truncate
	fmove.l	fp0,d0
	rts
.fixed_zero:
	moveq	#0,d0
	rts

; ============================================================================
; Software Fixed32 Square Root
; ============================================================================

; _math_sqrt_sw_fixed32 - Software sqrt for 16.16 fixed point
; Input: d0.l = raw fixed-point value
; Output: d0.l = sqrt result as raw fixed-point
; For fixed point sqrt(x) where x is 16.16:
; We compute sqrt(x * 65536) = sqrt(x) * 256
; So result = sqrt_u32(raw) * 256 / sqrt(65536) = sqrt_u32(raw << 16)
; But that overflows, so we do: sqrt_u32(raw) << 8
	xdef	@math_sqrt_sw_fixed32
@math_sqrt_sw_fixed32:
	tst.l	d0
	ble.s	.sw_fixed_zero

	movem.l	d2,-(sp)
	move.l	d0,d2			; Save raw input

	; For better precision, shift left 16 and sqrt, or use approximation
	; Simple approach: sqrt(raw) << 8 gives rough result
	; Better: sqrt(raw << 16) but that can overflow for raw > 65535

	; Check if we can shift left without overflow
	cmp.l	#$FFFF,d2
	bhi.s	.large_fixed

	; Small value: shift left 16, sqrt gives exact result
	lsl.l	#8,d0			; Shift left 8 (will shift 8 more via result)
	lsl.l	#8,d0			; Now shifted 16
	bsr	@math_sqrt_sw_u32
	movem.l	(sp)+,d2
	rts

.large_fixed:
	; Large value: sqrt(raw) << 8
	bsr	@math_sqrt_sw_u32
	lsl.l	#8,d0
	movem.l	(sp)+,d2
	rts

.sw_fixed_zero:
	moveq	#0,d0
	rts

; ============================================================================
; Distance calculation
; ============================================================================

; _math_distance - Calculate integer distance between two points
; Input: d0.l = x1, d1.l = y1, 4(sp) = x2, 8(sp) = y2
; Output: d0.l = floor(sqrt((x2-x1)^2 + (y2-y1)^2))
	xdef	@math_distance
@math_distance:
	movem.l	d2-d4,-(sp)
	move.l	16(sp),d2		; x2
	move.l	20(sp),d3		; y2

	sub.l	d0,d2			; dx = x2 - x1
	sub.l	d1,d3			; dy = y2 - y1

	muls.l	d2,d2			; dx^2
	muls.l	d3,d3			; dy^2
	add.l	d3,d2			; dist_sq = dx^2 + dy^2

	ble.s	.dist_zero

	; DEBUG: Force software path - skip FPU check
.dist_sw:
	move.l	d2,d0
	bsr	@math_sqrt_sw_u32
	movem.l	(sp)+,d2-d4
	rts

.dist_zero:
	moveq	#0,d0
	movem.l	(sp)+,d2-d4
	rts

; ============================================================================
; Hypotenuse (overflow-safe)
; ============================================================================

; _math_hypot - Calculate sqrt(a^2 + b^2) with overflow protection
; Input: d0.l = a, d1.l = b
; Output: d0.l = hypotenuse
	xdef	@math_hypot
@math_hypot:
	movem.l	d2-d4,-(sp)

	; Make both positive
	tst.l	d0
	bpl.s	.a_pos
	neg.l	d0
.a_pos:
	tst.l	d1
	bpl.s	.b_pos
	neg.l	d1
.b_pos:
	; Handle trivial cases
	tst.l	d0
	beq	.ret_b
	tst.l	d1
	beq	.ret_a

	; Ensure d0 >= d1
	cmp.l	d1,d0
	bge.s	.ordered
	exg	d0,d1
.ordered:
	move.l	d0,d2			; d2 = x (larger)
	move.l	d1,d3			; d3 = y (smaller)

	; Check for overflow: if x > 32767, scale down
	cmp.l	#32767,d2
	bgt.s	.scale_down

	; Normal case: compute x^2 + y^2
	muls.l	d2,d2
	muls.l	d3,d3
	add.l	d3,d2

	; DEBUG: Force software path
.hypot_sw:
	move.l	d2,d0
	bsr	@math_sqrt_sw_u32
	movem.l	(sp)+,d2-d4
	rts

.scale_down:
	; Scale both down by 256
	lsr.l	#8,d2
	lsr.l	#8,d3
	muls.l	d2,d2
	muls.l	d3,d3
	add.l	d3,d2

	; DEBUG: Force software path
.hypot_sw_scaled:
	move.l	d2,d0
	bsr	@math_sqrt_sw_u32
	lsl.l	#8,d0
	movem.l	(sp)+,d2-d4
	rts

.ret_a:
	movem.l	(sp)+,d2-d4
	rts
.ret_b:
	move.l	d1,d0
	movem.l	(sp)+,d2-d4
	rts

; ============================================================================
; Inverse Square Root (for normalization)
; ============================================================================

; _math_inv_sqrt - Approximate 1/sqrt(x) as 16.16 fixed point
; Input: d0.l = x (u32)
; Output: d0.l = 1/sqrt(x) in 16.16 format
	xdef	@math_inv_sqrt
@math_inv_sqrt:
	tst.l	d0
	beq.s	.inv_zero

	movem.l	d2,-(sp)
	move.l	d0,d2			; Save x

	; DEBUG: Force software path
	; d0 already contains x
	bsr	@math_sqrt_sw_u32

.inv_div:
	tst.l	d0
	beq.s	.inv_zero_pop

	; 1/sqrt in 16.16 = 65536 / sqrt
	move.l	d0,d1
	move.l	#65536,d0
	divul.l	d1,d0			; d0 = 65536 / sqrt
	movem.l	(sp)+,d2
	rts

.inv_zero_pop:
	movem.l	(sp)+,d2
.inv_zero:
	move.l	#$7FFFFFFF,d0		; Return max value for divide by zero
	rts

; ============================================================================
; Fast Approximate Square Root (Newton-Raphson, lower precision)
; ============================================================================

; _math_sqrt_fast - Fast approximate sqrt using Newton-Raphson
; Input: d0.l = x (u32)
; Output: d0.l = approximate sqrt(x)
	xdef	@math_sqrt_fast
@math_sqrt_fast:
	tst.l	d0
	beq.s	.fast_zero
	cmp.l	#1,d0
	beq.s	.fast_one

	; DEBUG: Force software path - skip FPU check
	movem.l	d2-d3,-(sp)
	move.l	d0,d2
	bra.s	.fast_sw

	; bsr	@math_has_fpu
	; tst.l	d0
	; beq.s	.fast_sw
	;
	; move.l	d2,d0
	; bsr	@math_sqrt_fpu_u32
	; movem.l	(sp)+,d2-d3
	; rts

.fast_sw:
	; Initial guess using BFFFO
	move.l	d2,d0
	bfffo	d0{0:32},d1
	moveq	#31,d3
	sub.l	d1,d3
	lsr.l	#1,d3
	moveq	#1,d0
	lsl.l	d3,d0			; Initial guess

	; Only 3 Newton-Raphson iterations (less precise but faster)
	move.l	d2,d1
	move.l	d0,d3
	divul.l	d3,d1			; d1 = x / guess
	add.l	d1,d0
	lsr.l	#1,d0

	move.l	d2,d1
	move.l	d0,d3
	divul.l	d3,d1
	add.l	d1,d0
	lsr.l	#1,d0

	move.l	d2,d1
	move.l	d0,d3
	divul.l	d3,d1
	add.l	d1,d0
	lsr.l	#1,d0

	; Final correction
	move.l	d0,d1
	mulu.l	d1,d1
	cmp.l	d2,d1
	bls.s	.fast_done
	subq.l	#1,d0
.fast_done:
	movem.l	(sp)+,d2-d3
	rts

.fast_zero:
	moveq	#0,d0
	rts
.fast_one:
	moveq	#1,d0
	rts

	end
