; Generated from SFD file by Novus SFD Parser
; Library: string.library
; Base: _StringBase
; Each function is in its own section for dead code elimination

	xref	_StringBase

	section	_STRING_GetClass_stub,code

; Class * STRING_GetClass()
	xdef	_STRING_GetClass
_STRING_GetClass:
	movem.l	a6,-(sp)
	movea.l	_StringBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

