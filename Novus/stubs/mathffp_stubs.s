; Generated from SFD file by Novus SFD Parser
; Library: mathffp.library
; Base: _MathBase
; Each function is in its own section for dead code elimination

	xref	_MathBase

	section	_SPFix_stub,code

; LONG SPFix(FLOAT parm)
	xdef	_SPFix
_SPFix:
	move.l	4(sp),d0
	movea.l	_MathBase,a6
	jsr	-30(a6)
	rts

	section	_SPFlt_stub,code

; FLOAT SPFlt(LONG integer)
	xdef	_SPFlt
_SPFlt:
	move.l	4(sp),d0
	movea.l	_MathBase,a6
	jsr	-36(a6)
	rts

	section	_SPCmp_stub,code

; LONG SPCmp(FLOAT leftParm, FLOAT rightParm)
	xdef	_SPCmp
_SPCmp:
	move.l	4(sp),d1
	move.l	8(sp),d0
	movea.l	_MathBase,a6
	jsr	-42(a6)
	rts

	section	_SPTst_stub,code

; LONG SPTst(FLOAT parm)
	xdef	_SPTst
_SPTst:
	move.l	4(sp),d1
	movea.l	_MathBase,a6
	jsr	-48(a6)
	rts

	section	_SPAbs_stub,code

; FLOAT SPAbs(FLOAT parm)
	xdef	_SPAbs
_SPAbs:
	move.l	4(sp),d0
	movea.l	_MathBase,a6
	jsr	-54(a6)
	rts

	section	_SPNeg_stub,code

; FLOAT SPNeg(FLOAT parm)
	xdef	_SPNeg
_SPNeg:
	move.l	4(sp),d0
	movea.l	_MathBase,a6
	jsr	-60(a6)
	rts

	section	_SPAdd_stub,code

; FLOAT SPAdd(FLOAT leftParm, FLOAT rightParm)
	xdef	_SPAdd
_SPAdd:
	move.l	4(sp),d1
	move.l	8(sp),d0
	movea.l	_MathBase,a6
	jsr	-66(a6)
	rts

	section	_SPSub_stub,code

; FLOAT SPSub(FLOAT leftParm, FLOAT rightParm)
	xdef	_SPSub
_SPSub:
	move.l	4(sp),d1
	move.l	8(sp),d0
	movea.l	_MathBase,a6
	jsr	-72(a6)
	rts

	section	_SPMul_stub,code

; FLOAT SPMul(FLOAT leftParm, FLOAT rightParm)
	xdef	_SPMul
_SPMul:
	move.l	4(sp),d1
	move.l	8(sp),d0
	movea.l	_MathBase,a6
	jsr	-78(a6)
	rts

	section	_SPDiv_stub,code

; FLOAT SPDiv(FLOAT leftParm, FLOAT rightParm)
	xdef	_SPDiv
_SPDiv:
	move.l	4(sp),d1
	move.l	8(sp),d0
	movea.l	_MathBase,a6
	jsr	-84(a6)
	rts

	section	_SPFloor_stub,code

; FLOAT SPFloor(FLOAT parm)
	xdef	_SPFloor
_SPFloor:
	move.l	4(sp),d0
	movea.l	_MathBase,a6
	jsr	-90(a6)
	rts

	section	_SPCeil_stub,code

; FLOAT SPCeil(FLOAT parm)
	xdef	_SPCeil
_SPCeil:
	move.l	4(sp),d0
	movea.l	_MathBase,a6
	jsr	-96(a6)
	rts

