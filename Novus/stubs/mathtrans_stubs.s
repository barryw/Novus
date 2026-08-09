; Generated from SFD file by Novus SFD Parser
; Library: mathtrans.library
; Base: _MathTransBase
; Each function is in its own section for dead code elimination

	xref	_MathTransBase

	section	_SPAtan_stub,code

; FLOAT SPAtan(FLOAT parm)
	xdef	_SPAtan
_SPAtan:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPSin_stub,code

; FLOAT SPSin(FLOAT parm)
	xdef	_SPSin
_SPSin:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPCos_stub,code

; FLOAT SPCos(FLOAT parm)
	xdef	_SPCos
_SPCos:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPTan_stub,code

; FLOAT SPTan(FLOAT parm)
	xdef	_SPTan
_SPTan:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPSincos_stub,code

; FLOAT SPSincos(FLOAT * cosResult, FLOAT parm)
	xdef	_SPSincos
_SPSincos:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	move.l	12(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPSinh_stub,code

; FLOAT SPSinh(FLOAT parm)
	xdef	_SPSinh
_SPSinh:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPCosh_stub,code

; FLOAT SPCosh(FLOAT parm)
	xdef	_SPCosh
_SPCosh:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPTanh_stub,code

; FLOAT SPTanh(FLOAT parm)
	xdef	_SPTanh
_SPTanh:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPExp_stub,code

; FLOAT SPExp(FLOAT parm)
	xdef	_SPExp
_SPExp:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPLog_stub,code

; FLOAT SPLog(FLOAT parm)
	xdef	_SPLog
_SPLog:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPPow_stub,code

; FLOAT SPPow(FLOAT power, FLOAT arg)
	xdef	_SPPow
_SPPow:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	move.l	12(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPSqrt_stub,code

; FLOAT SPSqrt(FLOAT parm)
	xdef	_SPSqrt
_SPSqrt:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPTieee_stub,code

; FLOAT SPTieee(FLOAT parm)
	xdef	_SPTieee
_SPTieee:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPFieee_stub,code

; FLOAT SPFieee(FLOAT parm)
	xdef	_SPFieee
_SPFieee:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPAsin_stub,code

; FLOAT SPAsin(FLOAT parm)
	xdef	_SPAsin
_SPAsin:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPAcos_stub,code

; FLOAT SPAcos(FLOAT parm)
	xdef	_SPAcos
_SPAcos:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_SPLog10_stub,code

; FLOAT SPLog10(FLOAT parm)
	xdef	_SPLog10
_SPLog10:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MathTransBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

