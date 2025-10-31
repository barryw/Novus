; Generated from SFD file by Novus SFD Parser
; Library: dtclass.library
; Base: _DTClassBase
; Each function is in its own section for dead code elimination

	xref	_DTClassBase

	section	_ObtainEngine_stub,code

; Class * ObtainEngine()
	xdef	_ObtainEngine
_ObtainEngine:
	movea.l	_DTClassBase,a6
	jsr	-30(a6)
	rts

