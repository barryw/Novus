; Generated from SFD file by Novus SFD Parser
; Library: slider.library
; Base: _SliderBase
; Each function is in its own section for dead code elimination

	xref	_SliderBase

	section	_SLIDER_GetClass_stub,code

; Class * SLIDER_GetClass()
	xdef	_SLIDER_GetClass
_SLIDER_GetClass:
	movea.l	_SliderBase,a6
	jsr	-30(a6)
	rts

