; Generated from SFD file by Novus SFD Parser
; Library: scroller.library
; Base: _ScrollerBase
; Each function is in its own section for dead code elimination

	xref	_ScrollerBase

	section	_SCROLLER_GetClass_stub,code

; Class * SCROLLER_GetClass()
	xdef	_SCROLLER_GetClass
_SCROLLER_GetClass:
	movea.l	_ScrollerBase,a6
	jsr	-30(a6)
	rts

