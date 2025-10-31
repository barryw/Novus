; Generated from SFD file by Novus SFD Parser
; Library: penmap.library
; Base: _PenMapBase
; Each function is in its own section for dead code elimination

	xref	_PenMapBase

	section	_PENMAP_GetClass_stub,code

; Class * PENMAP_GetClass()
	xdef	_PENMAP_GetClass
_PENMAP_GetClass:
	movea.l	_PenMapBase,a6
	jsr	-30(a6)
	rts

