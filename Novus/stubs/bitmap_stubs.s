; Generated from SFD file by Novus SFD Parser
; Library: bitmap.library
; Base: _BitMapBase
; Each function is in its own section for dead code elimination

	xref	_BitMapBase

	section	_BITMAP_GetClass_stub,code

; Class * BITMAP_GetClass()
	xdef	_BITMAP_GetClass
_BITMAP_GetClass:
	movea.l	_BitMapBase,a6
	jsr	-30(a6)
	rts

