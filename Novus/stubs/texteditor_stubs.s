; Generated from SFD file by Novus SFD Parser
; Library: texteditor.library
; Base: _TextFieldBase
; Each function is in its own section for dead code elimination

	xref	_TextFieldBase

	section	_TEXTEDITOR_GetClass_stub,code

; Class * TEXTEDITOR_GetClass()
	xdef	_TEXTEDITOR_GetClass
_TEXTEDITOR_GetClass:
	movea.l	_TextFieldBase,a6
	jsr	-30(a6)
	rts

