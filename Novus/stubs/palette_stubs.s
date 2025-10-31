; Generated from SFD file by Novus SFD Parser
; Library: palette.library
; Base: _PaletteBase
; Each function is in its own section for dead code elimination

	xref	_PaletteBase

	section	_PALETTE_GetClass_stub,code

; Class * PALETTE_GetClass()
	xdef	_PALETTE_GetClass
_PALETTE_GetClass:
	movea.l	_PaletteBase,a6
	jsr	-30(a6)
	rts

