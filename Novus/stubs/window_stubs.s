; Generated from SFD file by Novus SFD Parser
; Library: window.library
; Base: _WindowBase
; Each function is in its own section for dead code elimination

	xref	_WindowBase

	section	_WINDOW_GetClass_stub,code

; Class * WINDOW_GetClass()
	xdef	_WINDOW_GetClass
_WINDOW_GetClass:
	movea.l	_WindowBase,a6
	jsr	-30(a6)
	rts

