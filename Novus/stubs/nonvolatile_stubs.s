; Generated from SFD file by Novus SFD Parser
; Library: nonvolatile.library
; Base: _NVBase
; Each function is in its own section for dead code elimination

	xref	_NVBase

	section	_GetCopyNV_stub,code

; APTR GetCopyNV(CONST_STRPTR appName, CONST_STRPTR itemName, BOOL killRequesters)
	xdef	_GetCopyNV
_GetCopyNV:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d1
	movea.l	_NVBase,a6
	jsr	-30(a6)
	rts

	section	_FreeNVData_stub,code

; VOID FreeNVData(APTR data)
	xdef	_FreeNVData
_FreeNVData:
	movea.l	4(sp),a0
	movea.l	_NVBase,a6
	jsr	-36(a6)
	rts

	section	_StoreNV_stub,code

; UWORD StoreNV(CONST_STRPTR appName, CONST_STRPTR itemName, const APTR data, ULONG length, BOOL killRequesters)
	xdef	_StoreNV
_StoreNV:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_NVBase,a6
	jsr	-42(a6)
	rts

	section	_DeleteNV_stub,code

; BOOL DeleteNV(CONST_STRPTR appName, CONST_STRPTR itemName, BOOL killRequesters)
	xdef	_DeleteNV
_DeleteNV:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d1
	movea.l	_NVBase,a6
	jsr	-48(a6)
	rts

	section	_GetNVInfo_stub,code

; struct NVInfo * GetNVInfo(BOOL killRequesters)
	xdef	_GetNVInfo
_GetNVInfo:
	move.l	4(sp),d1
	movea.l	_NVBase,a6
	jsr	-54(a6)
	rts

	section	_GetNVList_stub,code

; struct MinList * GetNVList(CONST_STRPTR appName, BOOL killRequesters)
	xdef	_GetNVList
_GetNVList:
	movea.l	4(sp),a0
	move.l	8(sp),d1
	movea.l	_NVBase,a6
	jsr	-60(a6)
	rts

	section	_SetNVProtection_stub,code

; BOOL SetNVProtection(CONST_STRPTR appName, CONST_STRPTR itemName, LONG mask, BOOL killRequesters)
	xdef	_SetNVProtection
_SetNVProtection:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d2
	move.l	16(sp),d1
	movea.l	_NVBase,a6
	jsr	-66(a6)
	rts

