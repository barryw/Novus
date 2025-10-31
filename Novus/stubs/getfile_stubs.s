; Generated from SFD file by Novus SFD Parser
; Library: getfile.library
; Base: _GetFileBase
; Each function is in its own section for dead code elimination

	xref	_GetFileBase

	section	_GETFILE_GetClass_stub,code

; Class * GETFILE_GetClass()
	xdef	_GETFILE_GetClass
_GETFILE_GetClass:
	movea.l	_GetFileBase,a6
	jsr	-30(a6)
	rts

