; Generated from SFD file by Novus SFD Parser
; Library: nonvolatile.library
; Base: _NVBase
; Each function is in its own section for dead code elimination

	xref	_NVBase

	section	_GetCopyNV_stub,code

; APTR GetCopyNV(CONST_STRPTR appName, CONST_STRPTR itemName, BOOL killRequesters)
	xdef	_GetCopyNV
_GetCopyNV:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d1
	movea.l	_NVBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeNVData_stub,code

; VOID FreeNVData(APTR data)
	xdef	_FreeNVData
_FreeNVData:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_NVBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_StoreNV_stub,code

; UWORD StoreNV(CONST_STRPTR appName, CONST_STRPTR itemName, const APTR data, ULONG length, BOOL killRequesters)
	xdef	_StoreNV
_StoreNV:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	move.l	24(sp),d0
	move.l	28(sp),d1
	movea.l	_NVBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_DeleteNV_stub,code

; BOOL DeleteNV(CONST_STRPTR appName, CONST_STRPTR itemName, BOOL killRequesters)
	xdef	_DeleteNV
_DeleteNV:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d1
	movea.l	_NVBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetNVInfo_stub,code

; struct NVInfo * GetNVInfo(BOOL killRequesters)
	xdef	_GetNVInfo
_GetNVInfo:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_NVBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetNVList_stub,code

; struct MinList * GetNVList(CONST_STRPTR appName, BOOL killRequesters)
	xdef	_GetNVList
_GetNVList:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d1
	movea.l	_NVBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetNVProtection_stub,code

; BOOL SetNVProtection(CONST_STRPTR appName, CONST_STRPTR itemName, LONG mask, BOOL killRequesters)
	xdef	_SetNVProtection
_SetNVProtection:
	movem.l	d2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	move.l	20(sp),d2
	move.l	24(sp),d1
	movea.l	_NVBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,d2/a6
	rts

