; Generated from SFD file by Novus SFD Parser
; Library: glyph.library
; Base: _GlyphBase
; Each function is in its own section for dead code elimination

	xref	_GlyphBase

	section	_GLYPH_GetClass_stub,code

; Class * GLYPH_GetClass()
	xdef	_GLYPH_GetClass
_GLYPH_GetClass:
	movea.l	_GlyphBase,a6
	jsr	-30(a6)
	rts

