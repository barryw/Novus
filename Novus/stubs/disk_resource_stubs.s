; Generated from SFD file by Novus SFD Parser
; Library: disk.resource
; Base: _DiskBase
; Each function is in its own section for dead code elimination

	xref	_DiskBase

	section	_AllocUnit_stub,code

; BOOL AllocUnit(LONG unitNum)
	xdef	_AllocUnit
_AllocUnit:
	move.l	4(sp),d0
	movea.l	_DiskBase,a6
	jsr	-6(a6)
	rts

	section	_FreeUnit_stub,code

; VOID FreeUnit(LONG unitNum)
	xdef	_FreeUnit
_FreeUnit:
	move.l	4(sp),d0
	movea.l	_DiskBase,a6
	jsr	-12(a6)
	rts

	section	_GetUnit_stub,code

; struct DiscResourceUnit * GetUnit(struct DiscResourceUnit * unitPointer)
	xdef	_GetUnit
_GetUnit:
	movea.l	4(sp),a1
	movea.l	_DiskBase,a6
	jsr	-18(a6)
	rts

	section	_GiveUnit_stub,code

; VOID GiveUnit()
	xdef	_GiveUnit
_GiveUnit:
	movea.l	_DiskBase,a6
	jsr	-24(a6)
	rts

	section	_GetUnitID_stub,code

; LONG GetUnitID(LONG unitNum)
	xdef	_GetUnitID
_GetUnitID:
	move.l	4(sp),d0
	movea.l	_DiskBase,a6
	jsr	-30(a6)
	rts

	section	_ReadUnitID_stub,code

; LONG ReadUnitID(LONG unitNum)
	xdef	_ReadUnitID
_ReadUnitID:
	move.l	4(sp),d0
	movea.l	_DiskBase,a6
	jsr	-36(a6)
	rts

