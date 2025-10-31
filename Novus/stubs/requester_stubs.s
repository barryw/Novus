; Generated from SFD file by Novus SFD Parser
; Library: requester.library
; Base: _RequesterBase
; Each function is in its own section for dead code elimination

	xref	_RequesterBase

	section	_REQUESTER_GetClass_stub,code

; Class * REQUESTER_GetClass()
	xdef	_REQUESTER_GetClass
_REQUESTER_GetClass:
	movea.l	_RequesterBase,a6
	jsr	-30(a6)
	rts

