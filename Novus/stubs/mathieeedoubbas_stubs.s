; mathieeedoubbas library stubs for Novus
; Auto-generated from mathieeedoubbas_lib.fd

	xref	_MathIeeeDoubBasBase	; Provided by startup.o + -lamiga

	section	"CODE",code

; IEEEDPFix(parm)
	xdef	_IEEEDPFix
_IEEEDPFix:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	16(sp),d1	; reg1
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-30(a6)	; IEEEDPFix()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEEDPFlt(integer)
	xdef	_IEEEDPFlt
_IEEEDPFlt:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; integer
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-36(a6)	; IEEEDPFlt()
	movem.l	(sp)+,d0/a6
	rts

; IEEEDPCmp(leftParm, rightParm)
	xdef	_IEEEDPCmp
_IEEEDPCmp:
	movem.l	d0-d3/a6,-(sp)
	move.l	12(sp),d0	; leftParm
	move.l	16(sp),d1	; rightParm
	move.l	20(sp),d2	; reg2
	move.l	24(sp),d3	; reg3
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-42(a6)	; IEEEDPCmp()
	movem.l	(sp)+,d0-d3/a6
	rts

; IEEEDPTst(parm)
	xdef	_IEEEDPTst
_IEEEDPTst:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	16(sp),d1	; reg1
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-48(a6)	; IEEEDPTst()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEEDPAbs(parm)
	xdef	_IEEEDPAbs
_IEEEDPAbs:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	16(sp),d1	; reg1
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-54(a6)	; IEEEDPAbs()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEEDPNeg(parm)
	xdef	_IEEEDPNeg
_IEEEDPNeg:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	16(sp),d1	; reg1
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-60(a6)	; IEEEDPNeg()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEEDPAdd(leftParm, rightParm)
	xdef	_IEEEDPAdd
_IEEEDPAdd:
	movem.l	d0-d3/a6,-(sp)
	move.l	12(sp),d0	; leftParm
	move.l	16(sp),d1	; rightParm
	move.l	20(sp),d2	; reg2
	move.l	24(sp),d3	; reg3
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-66(a6)	; IEEEDPAdd()
	movem.l	(sp)+,d0-d3/a6
	rts

; IEEEDPSub(leftParm, rightParm)
	xdef	_IEEEDPSub
_IEEEDPSub:
	movem.l	d0-d3/a6,-(sp)
	move.l	12(sp),d0	; leftParm
	move.l	16(sp),d1	; rightParm
	move.l	20(sp),d2	; reg2
	move.l	24(sp),d3	; reg3
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-72(a6)	; IEEEDPSub()
	movem.l	(sp)+,d0-d3/a6
	rts

; IEEEDPMul(factor1, factor2)
	xdef	_IEEEDPMul
_IEEEDPMul:
	movem.l	d0-d3/a6,-(sp)
	move.l	12(sp),d0	; factor1
	move.l	16(sp),d1	; factor2
	move.l	20(sp),d2	; reg2
	move.l	24(sp),d3	; reg3
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-78(a6)	; IEEEDPMul()
	movem.l	(sp)+,d0-d3/a6
	rts

; IEEEDPDiv(dividend, divisor)
	xdef	_IEEEDPDiv
_IEEEDPDiv:
	movem.l	d0-d3/a6,-(sp)
	move.l	12(sp),d0	; dividend
	move.l	16(sp),d1	; divisor
	move.l	20(sp),d2	; reg2
	move.l	24(sp),d3	; reg3
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-84(a6)	; IEEEDPDiv()
	movem.l	(sp)+,d0-d3/a6
	rts

; IEEEDPFloor(parm)
	xdef	_IEEEDPFloor
_IEEEDPFloor:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	16(sp),d1	; reg1
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-90(a6)	; IEEEDPFloor()
	movem.l	(sp)+,d0-d1/a6
	rts

; IEEEDPCeil(parm)
	xdef	_IEEEDPCeil
_IEEEDPCeil:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; parm
	move.l	16(sp),d1	; reg1
	move.l	_MathIeeeDoubBasBase,a6
	jsr	-96(a6)	; IEEEDPCeil()
	movem.l	(sp)+,d0-d1/a6
	rts

