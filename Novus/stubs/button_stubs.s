; Generated from SFD file by Novus SFD Parser
; Library: button.library
; Base: _ButtonBase
; Each function is in its own section for dead code elimination

	xref	_ButtonBase

	section	_BUTTON_GetClass_stub,code

; Class * BUTTON_GetClass()
	xdef	_BUTTON_GetClass
_BUTTON_GetClass:
	movem.l	a6,-(sp)
	movea.l	_ButtonBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

