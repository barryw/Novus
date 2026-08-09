; Generated from SFD file by Novus SFD Parser
; Library: mathieeedoubbas.library
; Base: _MathIeeeDoubBasBase
; Each function is in its own section for dead code elimination

	xref	_MathIeeeDoubBasBase

	section	_IEEEDPFix_stub,code

; LONG IEEEDPFix(DOUBLE parm)
	xdef	_IEEEDPFix
_IEEEDPFix:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPFlt_stub,code

; DOUBLE IEEEDPFlt(LONG integer)
	xdef	_IEEEDPFlt
_IEEEDPFlt:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPCmp_stub,code

; LONG IEEEDPCmp(DOUBLE leftParm, DOUBLE rightParm)
	xdef	_IEEEDPCmp
_IEEEDPCmp:
	movem.l	d2/d3/a6,-(sp)
	movem.l	16(sp),d0-d1
	movem.l	24(sp),d2-d3
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_IEEEDPTst_stub,code

; LONG IEEEDPTst(DOUBLE parm)
	xdef	_IEEEDPTst
_IEEEDPTst:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPAbs_stub,code

; DOUBLE IEEEDPAbs(DOUBLE parm)
	xdef	_IEEEDPAbs
_IEEEDPAbs:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPNeg_stub,code

; DOUBLE IEEEDPNeg(DOUBLE parm)
	xdef	_IEEEDPNeg
_IEEEDPNeg:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPAdd_stub,code

; DOUBLE IEEEDPAdd(DOUBLE leftParm, DOUBLE rightParm)
	xdef	_IEEEDPAdd
_IEEEDPAdd:
	movem.l	d2/d3/a6,-(sp)
	movem.l	16(sp),d0-d1
	movem.l	24(sp),d2-d3
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_IEEEDPSub_stub,code

; DOUBLE IEEEDPSub(DOUBLE leftParm, DOUBLE rightParm)
	xdef	_IEEEDPSub
_IEEEDPSub:
	movem.l	d2/d3/a6,-(sp)
	movem.l	16(sp),d0-d1
	movem.l	24(sp),d2-d3
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_IEEEDPMul_stub,code

; DOUBLE IEEEDPMul(DOUBLE factor1, DOUBLE factor2)
	xdef	_IEEEDPMul
_IEEEDPMul:
	movem.l	d2/d3/a6,-(sp)
	movem.l	16(sp),d0-d1
	movem.l	24(sp),d2-d3
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_IEEEDPDiv_stub,code

; DOUBLE IEEEDPDiv(DOUBLE dividend, DOUBLE divisor)
	xdef	_IEEEDPDiv
_IEEEDPDiv:
	movem.l	d2/d3/a6,-(sp)
	movem.l	16(sp),d0-d1
	movem.l	24(sp),d2-d3
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_IEEEDPFloor_stub,code

; DOUBLE IEEEDPFloor(DOUBLE parm)
	xdef	_IEEEDPFloor
_IEEEDPFloor:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_IEEEDPCeil_stub,code

; DOUBLE IEEEDPCeil(DOUBLE parm)
	xdef	_IEEEDPCeil
_IEEEDPCeil:
	movem.l	a6,-(sp)
	movem.l	8(sp),d0-d1
	movea.l	_MathIeeeDoubBasBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

