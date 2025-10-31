; Generated from SFD file by Novus SFD Parser
; Library: space.library
; Base: _SpaceBase
; Each function is in its own section for dead code elimination

	xref	_SpaceBase

	section	_SPACE_GetClass_stub,code

; Class * SPACE_GetClass()
	xdef	_SPACE_GetClass
_SPACE_GetClass:
	movea.l	_SpaceBase,a6
	jsr	-30(a6)
	rts

