; NDK 3.9 declares fpbcd(), but its amiga.lib archive does not define it.
; Reuse the archive's fpa() conversion, then pack its fixed 14-byte output.

	xref	_fpa

	section	_fpbcd_compat,code
	xdef	_fpbcd
_fpbcd:
	movem.l	d2-d4/a2-a3,-(sp)
	move.l	24(sp),d2		; FFP bits
	movea.l	28(sp),a2		; eight-byte output
	lea	-14(sp),sp
	movea.l	sp,a3

	move.l	a3,-(sp)
	move.l	d2,-(sp)
	jsr	_fpa
	addq.l	#8,sp
	move.l	d0,d2			; signed decimal exponent

	lea	2(a3),a0		; eight mantissa digits
	movea.l	a2,a1
	moveq	#3,d4
.mantissa:
	moveq	#0,d0
	move.b	(a0)+,d0
	subi.b	#'0',d0
	lsl.b	#4,d0
	moveq	#0,d1
	move.b	(a0)+,d1
	subi.b	#'0',d1
	or.b	d1,d0
	move.b	d0,(a1)+
	dbra	d4,.mantissa

	moveq	#0,d0
	cmpi.b	#'-',(a3)
	bne.s	.mantissa_sign
	moveq	#-1,d0
.mantissa_sign:
	move.b	d0,4(a2)

	moveq	#0,d0
	move.b	12(a3),d0
	subi.b	#'0',d0
	lsl.b	#4,d0
	moveq	#0,d1
	move.b	13(a3),d1
	subi.b	#'0',d1
	or.b	d1,d0
	move.b	d0,5(a2)

	moveq	#0,d0
	cmpi.b	#'-',11(a3)
	bne.s	.exponent_sign
	moveq	#-1,d0
.exponent_sign:
	move.b	d0,6(a2)
	move.b	d2,7(a2)

	lea	14(sp),sp
	movem.l	(sp)+,d2-d4/a2-a3
	rts
