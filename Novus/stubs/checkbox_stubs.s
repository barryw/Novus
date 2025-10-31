; Generated from SFD file by Novus SFD Parser
; Library: checkbox.library
; Base: _CheckBoxBase
; Each function is in its own section for dead code elimination

	xref	_CheckBoxBase

	section	_CHECKBOX_GetClass_stub,code

; Class * CHECKBOX_GetClass()
	xdef	_CHECKBOX_GetClass
_CHECKBOX_GetClass:
	movea.l	_CheckBoxBase,a6
	jsr	-30(a6)
	rts

