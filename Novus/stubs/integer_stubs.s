; Generated from SFD file by Novus SFD Parser
; Library: integer.library
; Base: _IntegerBase
; Each function is in its own section for dead code elimination

	xref	_IntegerBase

	section	_INTEGER_GetClass_stub,code

; Class * INTEGER_GetClass()
	xdef	_INTEGER_GetClass
_INTEGER_GetClass:
	movem.l	a6,-(sp)
	movea.l	_IntegerBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

