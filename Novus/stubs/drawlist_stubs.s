; Generated from SFD file by Novus SFD Parser
; Library: drawlist.library
; Base: _DrawListBase
; Each function is in its own section for dead code elimination

	xref	_DrawListBase

	section	_DRAWLIST_GetClass_stub,code

; Class * DRAWLIST_GetClass()
	xdef	_DRAWLIST_GetClass
_DRAWLIST_GetClass:
	movea.l	_DrawListBase,a6
	jsr	-30(a6)
	rts

