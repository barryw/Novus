; Generated from SFD file by Novus SFD Parser
; Library: bitmap.library
; Base: _BitMapBase
; Each function is in its own section for dead code elimination

	xref	_BitMapBase

	section	_BITMAP_GetClass_stub,code

; Class * BITMAP_GetClass()
	xdef	_BITMAP_GetClass
_BITMAP_GetClass:
	movem.l	a6,-(sp)
	movea.l	_BitMapBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

