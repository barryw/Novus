; Generated from SFD file by Novus SFD Parser
; Library: arexx.library
; Base: _ARexxBase
; Each function is in its own section for dead code elimination

	xref	_ARexxBase

	section	_AREXX_GetClass_stub,code

; Class * AREXX_GetClass()
	xdef	_AREXX_GetClass
_AREXX_GetClass:
	movem.l	a6,-(sp)
	movea.l	_ARexxBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

