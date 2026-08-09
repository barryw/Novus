; Generated from SFD file by Novus SFD Parser
; Library: misc.resource
; Base: _MiscBase
; Each function is in its own section for dead code elimination

	xref	_MiscBase

	section	_AllocMiscResource_stub,code

; UBYTE * AllocMiscResource(ULONG unitNum, CONST_STRPTR name)
	xdef	_AllocMiscResource
_AllocMiscResource:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_MiscBase,a6
	jsr	-6(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeMiscResource_stub,code

; VOID FreeMiscResource(ULONG unitNum)
	xdef	_FreeMiscResource
_FreeMiscResource:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_MiscBase,a6
	jsr	-12(a6)
	movem.l	(sp)+,a6
	rts

