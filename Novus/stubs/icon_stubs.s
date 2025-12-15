; Generated from SFD file by Novus SFD Parser
; Library: icon.library
; Base: _IconBase
; Each function is in its own section for dead code elimination

	xref	_IconBase

	section	_FreeFreeList_stub,code

; VOID FreeFreeList(struct FreeList * freelist)
	xdef	_FreeFreeList
_FreeFreeList:
	movea.l	4(sp),a0
	movea.l	_IconBase,a6
	jsr	-54(a6)
	rts

	section	_AddFreeList_stub,code

; BOOL AddFreeList(struct FreeList * freelist, const APTR mem, ULONG size)
	xdef	_AddFreeList
_AddFreeList:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IconBase,a6
	jsr	-72(a6)
	rts

	section	_GetDiskObject_stub,code

; struct DiskObject * GetDiskObject(const STRPTR name)
	xdef	_GetDiskObject
_GetDiskObject:
	movea.l	4(sp),a0
	movea.l	_IconBase,a6
	jsr	-78(a6)
	rts

	section	_PutDiskObject_stub,code

; BOOL PutDiskObject(const STRPTR name, const struct DiskObject * diskobj)
	xdef	_PutDiskObject
_PutDiskObject:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IconBase,a6
	jsr	-84(a6)
	rts

	section	_FreeDiskObject_stub,code

; VOID FreeDiskObject(struct DiskObject * diskobj)
	xdef	_FreeDiskObject
_FreeDiskObject:
	movea.l	4(sp),a0
	movea.l	_IconBase,a6
	jsr	-90(a6)
	rts

	section	_FindToolType_stub,code

; UBYTE * FindToolType(const STRPTR * toolTypeArray, const STRPTR typeName)
	xdef	_FindToolType
_FindToolType:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IconBase,a6
	jsr	-96(a6)
	rts

	section	_MatchToolValue_stub,code

; BOOL MatchToolValue(const STRPTR typeString, const STRPTR value)
	xdef	_MatchToolValue
_MatchToolValue:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IconBase,a6
	jsr	-102(a6)
	rts

	section	_BumpRevision_stub,code

; STRPTR BumpRevision(STRPTR newname, const STRPTR oldname)
	xdef	_BumpRevision
_BumpRevision:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IconBase,a6
	jsr	-108(a6)
	rts

	section	_GetDefDiskObject_stub,code

; struct DiskObject * GetDefDiskObject(LONG type)
	xdef	_GetDefDiskObject
_GetDefDiskObject:
	move.l	4(sp),d0
	movea.l	_IconBase,a6
	jsr	-120(a6)
	rts

	section	_PutDefDiskObject_stub,code

; BOOL PutDefDiskObject(const struct DiskObject * diskObject)
	xdef	_PutDefDiskObject
_PutDefDiskObject:
	movea.l	4(sp),a0
	movea.l	_IconBase,a6
	jsr	-126(a6)
	rts

	section	_GetDiskObjectNew_stub,code

; struct DiskObject * GetDiskObjectNew(const STRPTR name)
	xdef	_GetDiskObjectNew
_GetDiskObjectNew:
	movea.l	4(sp),a0
	movea.l	_IconBase,a6
	jsr	-132(a6)
	rts

	section	_DeleteDiskObject_stub,code

; BOOL DeleteDiskObject(const STRPTR name)
	xdef	_DeleteDiskObject
_DeleteDiskObject:
	movea.l	4(sp),a0
	movea.l	_IconBase,a6
	jsr	-138(a6)
	rts

	section	_DupDiskObjectA_stub,code

; struct DiskObject * DupDiskObjectA(const struct DiskObject * diskObject, const struct TagItem * tags)
	xdef	_DupDiskObjectA
_DupDiskObjectA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IconBase,a6
	jsr	-150(a6)
	rts

	section	_IconControlA_stub,code

; ULONG IconControlA(struct DiskObject * icon, const struct TagItem * tags)
	xdef	_IconControlA
_IconControlA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IconBase,a6
	jsr	-156(a6)
	rts

	section	_DrawIconStateA_stub,code

; VOID DrawIconStateA(struct RastPort * rp, const struct DiskObject * icon, const STRPTR label, LONG leftOffset, LONG topOffset, ULONG state, const struct TagItem * tags)
	xdef	_DrawIconStateA
_DrawIconStateA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	move.l	20(sp),d1
	move.l	24(sp),d2
	movea.l	28(sp),a3
	movea.l	_IconBase,a6
	jsr	-162(a6)
	rts

	section	_GetIconRectangleA_stub,code

; BOOL GetIconRectangleA(struct RastPort * rp, const struct DiskObject * icon, const STRPTR label, struct Rectangle * rect, const struct TagItem * tags)
	xdef	_GetIconRectangleA
_GetIconRectangleA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	20(sp),a4
	movea.l	_IconBase,a6
	jsr	-168(a6)
	rts

	section	_NewDiskObject_stub,code

; struct DiskObject * NewDiskObject(LONG type)
	xdef	_NewDiskObject
_NewDiskObject:
	move.l	4(sp),d0
	movea.l	_IconBase,a6
	jsr	-174(a6)
	rts

	section	_GetIconTagList_stub,code

; struct DiskObject * GetIconTagList(const STRPTR name, const struct TagItem * tags)
	xdef	_GetIconTagList
_GetIconTagList:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IconBase,a6
	jsr	-180(a6)
	rts

	section	_PutIconTagList_stub,code

; BOOL PutIconTagList(const STRPTR name, const struct DiskObject * icon, const struct TagItem * tags)
	xdef	_PutIconTagList
_PutIconTagList:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IconBase,a6
	jsr	-186(a6)
	rts

	section	_LayoutIconA_stub,code

; BOOL LayoutIconA(struct DiskObject * icon, struct Screen * screen, struct TagItem * tags)
	xdef	_LayoutIconA
_LayoutIconA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IconBase,a6
	jsr	-192(a6)
	rts

	section	_ChangeToSelectedIconColor_stub,code

; VOID ChangeToSelectedIconColor(struct ColorRegister * cr)
	xdef	_ChangeToSelectedIconColor
_ChangeToSelectedIconColor:
	movea.l	4(sp),a0
	movea.l	_IconBase,a6
	jsr	-198(a6)
	rts

