; Generated from SFD file by Novus SFD Parser
; Library: getscreenmode.library
; Base: _GetScreenModeBase
; Each function is in its own section for dead code elimination

	xref	_GetScreenModeBase

	section	_GETSCREENMODE_GetClass_stub,code

; Class * GETSCREENMODE_GetClass()
	xdef	_GETSCREENMODE_GetClass
_GETSCREENMODE_GetClass:
	movem.l	a6,-(sp)
	movea.l	_GetScreenModeBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

