; Generated from SFD file by Novus SFD Parser
; Library: mathieeesingtrans.library
; Base: _MathIeeeSingTransBase
; Each function is in its own section for dead code elimination

	xref	_MathIeeeSingTransBase

	section	_IEEESPAtan_stub,code

; FLOAT IEEESPAtan(FLOAT parm)
	xdef	_IEEESPAtan
_IEEESPAtan:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPSin_stub,code

; FLOAT IEEESPSin(FLOAT parm)
	xdef	_IEEESPSin
_IEEESPSin:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPCos_stub,code

; FLOAT IEEESPCos(FLOAT parm)
	xdef	_IEEESPCos
_IEEESPCos:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPTan_stub,code

; FLOAT IEEESPTan(FLOAT parm)
	xdef	_IEEESPTan
_IEEESPTan:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPSincos_stub,code

; FLOAT IEEESPSincos(FLOAT * cosptr, FLOAT parm)
	xdef	_IEEESPSincos
_IEEESPSincos:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPSinh_stub,code

; FLOAT IEEESPSinh(FLOAT parm)
	xdef	_IEEESPSinh
_IEEESPSinh:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPCosh_stub,code

; FLOAT IEEESPCosh(FLOAT parm)
	xdef	_IEEESPCosh
_IEEESPCosh:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPTanh_stub,code

; FLOAT IEEESPTanh(FLOAT parm)
	xdef	_IEEESPTanh
_IEEESPTanh:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPExp_stub,code

; FLOAT IEEESPExp(FLOAT parm)
	xdef	_IEEESPExp
_IEEESPExp:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPLog_stub,code

; FLOAT IEEESPLog(FLOAT parm)
	xdef	_IEEESPLog
_IEEESPLog:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPPow_stub,code

; FLOAT IEEESPPow(FLOAT exp, FLOAT arg)
	xdef	_IEEESPPow
_IEEESPPow:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	move.l	12(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPSqrt_stub,code

; FLOAT IEEESPSqrt(FLOAT parm)
	xdef	_IEEESPSqrt
_IEEESPSqrt:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPTieee_stub,code

; FLOAT IEEESPTieee(FLOAT parm)
	xdef	_IEEESPTieee
_IEEESPTieee:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPFieee_stub,code

; FLOAT IEEESPFieee(FLOAT parm)
	xdef	_IEEESPFieee
_IEEESPFieee:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPAsin_stub,code

; FLOAT IEEESPAsin(FLOAT parm)
	xdef	_IEEESPAsin
_IEEESPAsin:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPAcos_stub,code

; FLOAT IEEESPAcos(FLOAT parm)
	xdef	_IEEESPAcos
_IEEESPAcos:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEESPLog10_stub,code

; FLOAT IEEESPLog10(FLOAT parm)
	xdef	_IEEESPLog10
_IEEESPLog10:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeSingTransBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

