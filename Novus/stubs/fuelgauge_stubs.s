; Generated from SFD file by Novus SFD Parser
; Library: fuelgauge.library
; Base: _FuelGaugeBase
; Each function is in its own section for dead code elimination

	xref	_FuelGaugeBase

	section	_FUELGAUGE_GetClass_stub,code

; Class * FUELGAUGE_GetClass()
	xdef	_FUELGAUGE_GetClass
_FUELGAUGE_GetClass:
	movea.l	_FuelGaugeBase,a6
	jsr	-30(a6)
	rts

