; Generated from SFD file by Novus SFD Parser
; Library: mathieeedoubtrans.library
; Base: _MathIeeeDoubTransBase
; Each function is in its own section for dead code elimination

	xref	_MathIeeeDoubTransBase

	section	_IEEEDPAtan_stub,code

; DOUBLE IEEEDPAtan(DOUBLE parm)
	xdef	_IEEEDPAtan
_IEEEDPAtan:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts
	section	_IEEEDPSin_stub,code

; DOUBLE IEEEDPSin(DOUBLE parm)
	xdef	_IEEEDPSin
_IEEEDPSin:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPCos_stub,code

; DOUBLE IEEEDPCos(DOUBLE parm)
	xdef	_IEEEDPCos
_IEEEDPCos:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPTan_stub,code

; DOUBLE IEEEDPTan(DOUBLE parm)
	xdef	_IEEEDPTan
_IEEEDPTan:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPSincos_stub,code

; DOUBLE IEEEDPSincos(DOUBLE * pf2, DOUBLE parm)
	xdef	_IEEEDPSincos
_IEEEDPSincos:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movem.l	12(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPSinh_stub,code

; DOUBLE IEEEDPSinh(DOUBLE parm)
	xdef	_IEEEDPSinh
_IEEEDPSinh:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPCosh_stub,code

; DOUBLE IEEEDPCosh(DOUBLE parm)
	xdef	_IEEEDPCosh
_IEEEDPCosh:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPTanh_stub,code

; DOUBLE IEEEDPTanh(DOUBLE parm)
	xdef	_IEEEDPTanh
_IEEEDPTanh:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPExp_stub,code

; DOUBLE IEEEDPExp(DOUBLE parm)
	xdef	_IEEEDPExp
_IEEEDPExp:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPLog_stub,code

; DOUBLE IEEEDPLog(DOUBLE parm)
	xdef	_IEEEDPLog
_IEEEDPLog:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPPow_stub,code

; DOUBLE IEEEDPPow(DOUBLE exp, DOUBLE arg)
	xdef	_IEEEDPPow
_IEEEDPPow:
	movem.l	d2/d3/a6,-(sp)
	movem.l	16(sp),d2-d3
	movem.l	24(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_IEEEDPSqrt_stub,code

; DOUBLE IEEEDPSqrt(DOUBLE parm)
	xdef	_IEEEDPSqrt
_IEEEDPSqrt:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPTieee_stub,code

; FLOAT IEEEDPTieee(DOUBLE parm)
	xdef	_IEEEDPTieee
_IEEEDPTieee:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPFieee_stub,code

; DOUBLE IEEEDPFieee(FLOAT single)
	xdef	_IEEEDPFieee
_IEEEDPFieee:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPAsin_stub,code

; DOUBLE IEEEDPAsin(DOUBLE parm)
	xdef	_IEEEDPAsin
_IEEEDPAsin:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPAcos_stub,code

; DOUBLE IEEEDPAcos(DOUBLE parm)
	xdef	_IEEEDPAcos
_IEEEDPAcos:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPLog10_stub,code

; DOUBLE IEEEDPLog10(DOUBLE parm)
	xdef	_IEEEDPLog10
_IEEEDPLog10:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubTransBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts
