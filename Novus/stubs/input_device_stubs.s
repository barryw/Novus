; Generated from SFD file by Novus SFD Parser
; Library: input.device
; Base: _InputBase
; Each function is in its own section for dead code elimination

	xref	_InputBase

	section	_PeekQualifier_stub,code

; UWORD PeekQualifier()
	xdef	_PeekQualifier
_PeekQualifier:
	movea.l	_InputBase,a6
	jsr	-42(a6)
	rts

