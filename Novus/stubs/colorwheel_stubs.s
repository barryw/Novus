; Generated from SFD file by Novus SFD Parser
; Library: colorwheel.library
; Base: _ColorWheelBase
; Each function is in its own section for dead code elimination

	xref	_ColorWheelBase

	section	_ConvertHSBToRGB_stub,code

; VOID ConvertHSBToRGB(struct ColorWheelHSB * hsb, struct ColorWheelRGB * rgb)
	xdef	_ConvertHSBToRGB
_ConvertHSBToRGB:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ColorWheelBase,a6
	jsr	-30(a6)
	rts

	section	_ConvertRGBToHSB_stub,code

; VOID ConvertRGBToHSB(struct ColorWheelRGB * rgb, struct ColorWheelHSB * hsb)
	xdef	_ConvertRGBToHSB
_ConvertRGBToHSB:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ColorWheelBase,a6
	jsr	-36(a6)
	rts

