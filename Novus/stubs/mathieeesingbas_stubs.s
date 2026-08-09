; Generated from SFD file by Novus SFD Parser
; Library: mathieeesingbas.library
; Base: _MathIeeeSingBasBase
; Each function is in its own section for dead code elimination

	xref	_MathIeeeSingBasBase

	section	_IEEESPFix_stub,code

; LONG IEEESPFix(FLOAT parm)
	xdef	_IEEESPFix
_IEEESPFix:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPFlt_stub,code

; FLOAT IEEESPFlt(LONG integer)
	xdef	_IEEESPFlt
_IEEESPFlt:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPCmp_stub,code

; LONG IEEESPCmp(FLOAT leftParm, FLOAT rightParm)
	xdef	_IEEESPCmp
_IEEESPCmp:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPTst_stub,code

; LONG IEEESPTst(FLOAT parm)
	xdef	_IEEESPTst
_IEEESPTst:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPAbs_stub,code

; FLOAT IEEESPAbs(FLOAT parm)
	xdef	_IEEESPAbs
_IEEESPAbs:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPNeg_stub,code

; FLOAT IEEESPNeg(FLOAT parm)
	xdef	_IEEESPNeg
_IEEESPNeg:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPAdd_stub,code

; FLOAT IEEESPAdd(FLOAT leftParm, FLOAT rightParm)
	xdef	_IEEESPAdd
_IEEESPAdd:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPSub_stub,code

; FLOAT IEEESPSub(FLOAT leftParm, FLOAT rightParm)
	xdef	_IEEESPSub
_IEEESPSub:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPMul_stub,code

; FLOAT IEEESPMul(FLOAT leftParm, FLOAT rightParm)
	xdef	_IEEESPMul
_IEEESPMul:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPDiv_stub,code

; FLOAT IEEESPDiv(FLOAT dividend, FLOAT divisor)
	xdef	_IEEESPDiv
_IEEESPDiv:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPFloor_stub,code

; FLOAT IEEESPFloor(FLOAT parm)
	xdef	_IEEESPFloor
_IEEESPFloor:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPCeil_stub,code

; FLOAT IEEESPCeil(FLOAT parm)
	xdef	_IEEESPCeil
_IEEESPCeil:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingBasBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

