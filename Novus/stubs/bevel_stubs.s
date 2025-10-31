; Generated from SFD file by Novus SFD Parser
; Library: bevel.library
; Base: _BevelBase
; Each function is in its own section for dead code elimination

	xref	_BevelBase

	section	_BEVEL_GetClass_stub,code

; Class * BEVEL_GetClass()
	xdef	_BEVEL_GetClass
_BEVEL_GetClass:
	movea.l	_BevelBase,a6
	jsr	-30(a6)
	rts

