; Generated from SFD file by Novus SFD Parser
; Library: getfont.library
; Base: _GetFontBase
; Each function is in its own section for dead code elimination

	xref	_GetFontBase

	section	_GETFONT_GetClass_stub,code

; Class * GETFONT_GetClass()
	xdef	_GETFONT_GetClass
_GETFONT_GetClass:
	movem.l	a6,-(sp)
	movea.l	_GetFontBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

