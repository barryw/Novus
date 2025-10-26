; mathieeesingbas library stubs for Novus
; Auto-generated from mathieeesingbas_lib.fd

	xref	_MathIeeeSingBasBase	; Provided by startup.o + -lamiga

	section	text,code

; IEEESPFix(parm)
	xdef	_IEEESPFix
_IEEESPFix:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-30(a6)	; IEEESPFix()
	movem.l	(sp)+,d0/a6
	rts

; IEEESPFlt(integer)
	xdef	_IEEESPFlt
_IEEESPFlt:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; integer
	move.l	_MathIeeeSingBasBase,a6
	jsr	-36(a6)	; IEEESPFlt()
	movem.l	(sp)+,d0/a6
	rts

; IEEESPCmp(leftParm, rightParm)
	xdef	_IEEESPCmp
_IEEESPCmp:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; leftParm
	move.l	16(sp),d1	; rightParm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-42(a6)	; IEEESPCmp()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEESPTst(parm)
	xdef	_IEEESPTst
_IEEESPTst:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-48(a6)	; IEEESPTst()
	movem.l	(sp)+,d0/a6
	rts

; IEEESPAbs(parm)
	xdef	_IEEESPAbs
_IEEESPAbs:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-54(a6)	; IEEESPAbs()
	movem.l	(sp)+,d0/a6
	rts

; IEEESPNeg(parm)
	xdef	_IEEESPNeg
_IEEESPNeg:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-60(a6)	; IEEESPNeg()
	movem.l	(sp)+,d0/a6
	rts

; IEEESPAdd(leftParm, rightParm)
	xdef	_IEEESPAdd
_IEEESPAdd:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; leftParm
	move.l	16(sp),d1	; rightParm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-66(a6)	; IEEESPAdd()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEESPSub(leftParm, rightParm)
	xdef	_IEEESPSub
_IEEESPSub:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; leftParm
	move.l	16(sp),d1	; rightParm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-72(a6)	; IEEESPSub()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEESPMul(leftParm, rightParm)
	xdef	_IEEESPMul
_IEEESPMul:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; leftParm
	move.l	16(sp),d1	; rightParm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-78(a6)	; IEEESPMul()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEESPDiv(dividend, divisor)
	xdef	_IEEESPDiv
_IEEESPDiv:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; dividend
	move.l	16(sp),d1	; divisor
	move.l	_MathIeeeSingBasBase,a6
	jsr	-84(a6)	; IEEESPDiv()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEESPFloor(parm)
	xdef	_IEEESPFloor
_IEEESPFloor:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-90(a6)	; IEEESPFloor()
	movem.l	(sp)+,d0/a6
	rts

; IEEESPCeil(parm)
	xdef	_IEEESPCeil
_IEEESPCeil:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	_MathIeeeSingBasBase,a6
	jsr	-96(a6)	; IEEESPCeil()
	movem.l	(sp)+,d0/a6
	rts

