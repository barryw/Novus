; Generated from SFD file by Novus SFD Parser
; Library: window.library
; Base: _WindowBase
; Each function is in its own section for dead code elimination

	xref	_WindowBase

	section	_WINDOW_GetClass_stub,code

; Class * WINDOW_GetClass()
	xdef	_WINDOW_GetClass
_WINDOW_GetClass:
	movem.l	a6,-(sp)
	movea.l	_WindowBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

