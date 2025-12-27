; math_trig.s - Optimized 68020+ trigonometry using lookup tables
; Part of Novus standard library
;
; All functions follow VBCC __regargs calling convention:
; - Arguments in d0, d1, a0, a1 (register-based, NOT stack)
; - Return value in d0 (d0/d1 for 64-bit)
; - Preserve d2-d7, a2-a6
; - Scratch: d0, d1, a0, a1
; - Symbols exported with @ prefix for __regargs linkage
;
; Angle representation:
; - Angles are u16 (0-65535) mapping to 0-360 degrees (BAMS)
; - 0 = 0°, 16384 = 90°, 32768 = 180°, 49152 = 270°
;
; Return values:
; - sin_i16: 14-bit scaled i16 (-16384 to 16384, where 16384 = 1.0)
; - sin_fixed32: 16.16 Fixed32 (-65536 to 65536, where 65536 = 1.0)

	section	"CODE",code

; ============================================================================
; Sine Table (first quadrant, 65 entries, 14-bit scaled)
; ============================================================================

; Values: sin(i * 90 / 64) * 16384 for i = 0..64
; 16384 = 1.0 in 14-bit scaling

	cnop	0,4
sine_table:
	dc.w	0	; sin(0.000°) = 0.0000
	dc.w	402	; sin(1.406°) = 0.0245
	dc.w	804	; sin(2.813°) = 0.0491
	dc.w	1205	; sin(4.219°) = 0.0736
	dc.w	1606	; sin(5.625°) = 0.0980
	dc.w	2006	; sin(7.031°) = 0.1224
	dc.w	2404	; sin(8.438°) = 0.1467
	dc.w	2801	; sin(9.844°) = 0.1710
	dc.w	3196	; sin(11.250°) = 0.1951
	dc.w	3590	; sin(12.656°) = 0.2191
	dc.w	3981	; sin(14.063°) = 0.2430
	dc.w	4370	; sin(15.469°) = 0.2667
	dc.w	4756	; sin(16.875°) = 0.2903
	dc.w	5139	; sin(18.281°) = 0.3137
	dc.w	5520	; sin(19.688°) = 0.3369
	dc.w	5897	; sin(21.094°) = 0.3599
	dc.w	6270	; sin(22.500°) = 0.3827
	dc.w	6639	; sin(23.906°) = 0.4052
	dc.w	7005	; sin(25.313°) = 0.4276
	dc.w	7366	; sin(26.719°) = 0.4496
	dc.w	7723	; sin(28.125°) = 0.4714
	dc.w	8076	; sin(29.531°) = 0.4929
	dc.w	8423	; sin(30.938°) = 0.5141
	dc.w	8765	; sin(32.344°) = 0.5350
	dc.w	9102	; sin(33.750°) = 0.5556
	dc.w	9434	; sin(35.156°) = 0.5758
	dc.w	9760	; sin(36.563°) = 0.5957
	dc.w	10080	; sin(37.969°) = 0.6152
	dc.w	10394	; sin(39.375°) = 0.6344
	dc.w	10702	; sin(40.781°) = 0.6532
	dc.w	11003	; sin(42.188°) = 0.6716
	dc.w	11297	; sin(43.594°) = 0.6895
	dc.w	11585	; sin(45.000°) = 0.7071
	dc.w	11866	; sin(46.406°) = 0.7242
	dc.w	12140	; sin(47.813°) = 0.7410
	dc.w	12406	; sin(49.219°) = 0.7572
	dc.w	12665	; sin(50.625°) = 0.7730
	dc.w	12916	; sin(52.031°) = 0.7883
	dc.w	13160	; sin(53.438°) = 0.8032
	dc.w	13395	; sin(54.844°) = 0.8176
	dc.w	13623	; sin(56.250°) = 0.8315
	dc.w	13842	; sin(57.656°) = 0.8449
	dc.w	14053	; sin(59.063°) = 0.8577
	dc.w	14256	; sin(60.469°) = 0.8701
	dc.w	14449	; sin(61.875°) = 0.8819
	dc.w	14635	; sin(63.281°) = 0.8932
	dc.w	14811	; sin(64.688°) = 0.9040
	dc.w	14978	; sin(66.094°) = 0.9142
	dc.w	15137	; sin(67.500°) = 0.9239
	dc.w	15286	; sin(68.906°) = 0.9330
	dc.w	15426	; sin(70.313°) = 0.9415
	dc.w	15557	; sin(71.719°) = 0.9495
	dc.w	15679	; sin(73.125°) = 0.9569
	dc.w	15791	; sin(74.531°) = 0.9638
	dc.w	15893	; sin(75.938°) = 0.9700
	dc.w	15986	; sin(77.344°) = 0.9757
	dc.w	16069	; sin(78.750°) = 0.9808
	dc.w	16143	; sin(80.156°) = 0.9853
	dc.w	16207	; sin(81.563°) = 0.9892
	dc.w	16261	; sin(82.969°) = 0.9925
	dc.w	16305	; sin(84.375°) = 0.9952
	dc.w	16340	; sin(85.781°) = 0.9973
	dc.w	16364	; sin(87.188°) = 0.9988
	dc.w	16379	; sin(88.594°) = 0.9997
	dc.w	16384	; sin(90.000°) = 1.0000

; ============================================================================
; @trig_sin_i16 - Sine as 14-bit scaled i16
; ============================================================================
;
; Input: d0.w = angle (0-65535 BAMS)
; Output: d0.w = sin(angle) * 16384 (-16384 to 16384)
;
; Algorithm:
;   1. Convert angle to 8-bit index (0-255)
;   2. Upper 2 bits = quadrant, lower 6 bits = local index
;   3. For Q2/Q4, mirror index: 64 - local_idx
;   4. Lookup table value
;   5. For Q3/Q4, negate result

	xdef	@trig_sin_i16
@trig_sin_i16:
	move.l	d2,-(sp)	; Save d2

	; d0.w = angle (0-65535)
	; Convert to 8-bit index: angle >> 8 = 0-255
	move.w	d0,d1
	lsr.w	#8,d1		; d1 = idx (0-255)

	; Quadrant = idx >> 6 (0-3)
	move.w	d1,d2
	lsr.w	#6,d2		; d2 = quadrant

	; Local index = idx & 63 (0-63)
	and.w	#$3F,d1		; d1 = local_idx

	; For Q2 (1) and Q4 (3), mirror: local_idx = 64 - local_idx
	btst	#0,d2		; Test bit 0 of quadrant
	beq.s	.no_mirror
	neg.w	d1
	add.w	#64,d1		; d1 = 64 - local_idx
.no_mirror:
	; Lookup table value
	lea	sine_table(pc),a0
	add.w	d1,d1		; d1 *= 2 (word index)
	move.w	(a0,d1.w),d0	; d0 = table[local_idx]

	; For Q3 (2) and Q4 (3), negate: result = -result
	btst	#1,d2		; Test bit 1 of quadrant (>=2)
	beq.s	.no_negate
	neg.w	d0
.no_negate:
	move.l	(sp)+,d2	; Restore d2
	rts

; ============================================================================
; @trig_cos_i16 - Cosine as 14-bit scaled i16
; ============================================================================
;
; Input: d0.w = angle (0-65535 BAMS)
; Output: d0.w = cos(angle) * 16384 (-16384 to 16384)
;
; cos(x) = sin(x + 90°) = sin(x + 16384)

	xdef	@trig_cos_i16
@trig_cos_i16:
	add.w	#16384,d0	; Add 90 degrees (wraps naturally)
	bra	@trig_sin_i16

; ============================================================================
; @trig_sin_fixed32 - Sine as Fixed32 (16.16 format)
; ============================================================================
;
; Input: d0.w = angle (0-65535 BAMS)
; Output: d0.l = sin(angle) in Fixed32 format (-65536 to 65536)
;
; Algorithm: Get 14-bit result, shift left by 2 to get 16-bit

	xdef	@trig_sin_fixed32
@trig_sin_fixed32:
	bsr.s	@trig_sin_i16		; Get 14-bit result in d0.w
	ext.l	d0			; Sign-extend to 32 bits
	asl.l	#2,d0			; Scale from 14-bit to 16-bit
	rts

; ============================================================================
; @trig_cos_fixed32 - Cosine as Fixed32 (16.16 format)
; ============================================================================
;
; Input: d0.w = angle (0-65535 BAMS)
; Output: d0.l = cos(angle) in Fixed32 format (-65536 to 65536)

	xdef	@trig_cos_fixed32
@trig_cos_fixed32:
	add.w	#16384,d0		; Add 90 degrees
	bsr.s	@trig_sin_i16		; Get 14-bit result
	ext.l	d0			; Sign-extend
	asl.l	#2,d0			; Scale to 16-bit
	rts

; ============================================================================
; @trig_sincos_i16 - Get both sine and cosine at once
; ============================================================================
;
; Input: d0.w = angle (0-65535 BAMS)
;        a0 = pointer to result struct {i16 sin, i16 cos}
; Output: struct filled with sin and cos values
;
; More efficient than calling sin and cos separately

	xdef	@trig_sincos_i16
@trig_sincos_i16:
	movem.l	d2/a2,-(sp)
	move.w	d0,d2		; d2 = save angle
	move.l	a0,a2		; a2 = output pointer

	; Get sine
	bsr.s	@trig_sin_i16
	move.w	d0,(a2)		; Store sin

	; Get cosine
	move.w	d2,d0
	add.w	#16384,d0	; angle + 90°
	bsr.s	@trig_sin_i16
	move.w	d0,2(a2)	; Store cos

	movem.l	(sp)+,d2/a2
	rts

; ============================================================================
; @trig_sincos_fixed32 - Get both sine and cosine as Fixed32
; ============================================================================
;
; Input: d0.w = angle (0-65535 BAMS)
;        a0 = pointer to result struct {i32 sin, i32 cos}
; Output: struct filled with Fixed32 sin and cos values

	xdef	@trig_sincos_fixed32
@trig_sincos_fixed32:
	movem.l	d2/a2,-(sp)
	move.w	d0,d2		; d2 = save angle
	move.l	a0,a2		; a2 = output pointer

	; Get sine as Fixed32
	bsr.s	@trig_sin_i16
	ext.l	d0
	asl.l	#2,d0
	move.l	d0,(a2)		; Store sin

	; Get cosine as Fixed32
	move.w	d2,d0
	add.w	#16384,d0	; angle + 90°
	bsr.s	@trig_sin_i16
	ext.l	d0
	asl.l	#2,d0
	move.l	d0,4(a2)	; Store cos

	movem.l	(sp)+,d2/a2
	rts

; ============================================================================
; @trig_degrees_to_angle - Convert degrees to BAMS angle
; ============================================================================
;
; Input: d0.w = degrees (0-359)
; Output: d0.w = angle (0-65535)
;
; angle = degrees * 65536 / 360 ≈ degrees * 182 + degrees * 11 / 256

	xdef	@trig_degrees_to_angle
@trig_degrees_to_angle:
	ext.l	d0		; Sign-extend degrees
	move.l	d0,d1		; d1 = degrees

	; base = degrees * 182
	muls.w	#182,d0		; d0 = degrees * 182

	; correction = degrees * 11 / 256
	muls.w	#11,d1		; d1 = degrees * 11
	asr.l	#8,d1		; d1 = degrees * 11 / 256

	add.l	d1,d0		; d0 = base + correction
	rts

; ============================================================================
; @trig_angle_to_degrees - Convert BAMS angle to degrees
; ============================================================================
;
; Input: d0.w = angle (0-65535)
; Output: d0.w = degrees (0-359)
;
; degrees = angle * 360 / 65536

	xdef	@trig_angle_to_degrees
@trig_angle_to_degrees:
	and.l	#$FFFF,d0	; Zero-extend to 32 bits
	mulu.w	#360,d0		; d0 = angle * 360 (unsigned!)
	swap	d0		; d0 >> 16 (divide by 65536)
	rts

	end
