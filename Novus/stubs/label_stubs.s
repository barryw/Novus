; Generated from SFD file by Novus SFD Parser
; Library: label.library
; Base: _LabelBase
; Each function is in its own section for dead code elimination

	xref	_LabelBase

	section	_LABEL_GetClass_stub,code

; Class * LABEL_GetClass()
	xdef	_LABEL_GetClass
_LABEL_GetClass:
	movea.l	_LabelBase,a6
	jsr	-30(a6)
	rts

