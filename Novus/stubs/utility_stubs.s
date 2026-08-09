; Generated from SFD file by Novus SFD Parser
; Library: utility.library
; Base: _UtilityBase
; Each function is in its own section for dead code elimination

	xref	_UtilityBase

	section	_FindTagItem_stub,code

; struct TagItem * FindTagItem(Tag tagVal, const struct TagItem * tagList)
	xdef	_FindTagItem
_FindTagItem:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetTagData_stub,code

; ULONG GetTagData(Tag tagValue, ULONG defaultVal, const struct TagItem * tagList)
	xdef	_GetTagData
_GetTagData:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_PackBoolTags_stub,code

; ULONG PackBoolTags(ULONG initialFlags, const struct TagItem * tagList, const struct TagItem * boolMap)
	xdef	_PackBoolTags
_PackBoolTags:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_NextTagItem_stub,code

; struct TagItem * NextTagItem(struct TagItem ** tagListPtr)
	xdef	_NextTagItem
_NextTagItem:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_FilterTagChanges_stub,code

; VOID FilterTagChanges(struct TagItem * changeList, struct TagItem * originalList, ULONG apply)
	xdef	_FilterTagChanges
_FilterTagChanges:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_UtilityBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_MapTags_stub,code

; VOID MapTags(struct TagItem * tagList, const struct TagItem * mapList, ULONG mapType)
	xdef	_MapTags
_MapTags:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_UtilityBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocateTagItems_stub,code

; struct TagItem * AllocateTagItems(ULONG numTags)
	xdef	_AllocateTagItems
_AllocateTagItems:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_UtilityBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloneTagItems_stub,code

; struct TagItem * CloneTagItems(const struct TagItem * tagList)
	xdef	_CloneTagItems
_CloneTagItems:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeTagItems_stub,code

; VOID FreeTagItems(struct TagItem * tagList)
	xdef	_FreeTagItems
_FreeTagItems:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_RefreshTagItemClones_stub,code

; VOID RefreshTagItemClones(struct TagItem * clone, const struct TagItem * original)
	xdef	_RefreshTagItemClones
_RefreshTagItemClones:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_TagInArray_stub,code

; BOOL TagInArray(Tag tagValue, const Tag * tagArray)
	xdef	_TagInArray
_TagInArray:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_FilterTagItems_stub,code

; ULONG FilterTagItems(struct TagItem * tagList, const Tag * filterArray, ULONG logic)
	xdef	_FilterTagItems
_FilterTagItems:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_UtilityBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_CallHookPkt_stub,code

; ULONG CallHookPkt(struct Hook * hook, APTR object, APTR paramPacket)
	xdef	_CallHookPkt
_CallHookPkt:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a2
	movea.l	20(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_Amiga2Date_stub,code

; VOID Amiga2Date(ULONG seconds, struct ClockData * result)
	xdef	_Amiga2Date
_Amiga2Date:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_Date2Amiga_stub,code

; ULONG Date2Amiga(const struct ClockData * date)
	xdef	_Date2Amiga
_Date2Amiga:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_CheckDate_stub,code

; ULONG CheckDate(const struct ClockData * date)
	xdef	_CheckDate
_CheckDate:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_SMult32_stub,code

; LONG SMult32(LONG arg1, LONG arg2)
	xdef	_SMult32
_SMult32:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_UtilityBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_UMult32_stub,code

; ULONG UMult32(ULONG arg1, ULONG arg2)
	xdef	_UMult32
_UMult32:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_UtilityBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_SDivMod32_stub,code

; LONG SDivMod32(LONG dividend, LONG divisor)
	xdef	_SDivMod32
_SDivMod32:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_UtilityBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

	section	_UDivMod32_stub,code

; ULONG UDivMod32(ULONG dividend, ULONG divisor)
	xdef	_UDivMod32
_UDivMod32:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_UtilityBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a6
	rts

	section	_Stricmp_stub,code

; LONG Stricmp(CONST_STRPTR string1, CONST_STRPTR string2)
	xdef	_Stricmp
_Stricmp:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a6
	rts

	section	_Strnicmp_stub,code

; LONG Strnicmp(CONST_STRPTR string1, CONST_STRPTR string2, LONG length)
	xdef	_Strnicmp
_Strnicmp:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_UtilityBase,a6
	jsr	-168(a6)
	movem.l	(sp)+,a6
	rts

	section	_ToUpper_stub,code

; UBYTE ToUpper(UBYTE character)
	xdef	_ToUpper
_ToUpper:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_UtilityBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a6
	rts

	section	_ToLower_stub,code

; UBYTE ToLower(UBYTE character)
	xdef	_ToLower
_ToLower:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_UtilityBase,a6
	jsr	-180(a6)
	movem.l	(sp)+,a6
	rts

	section	_ApplyTagChanges_stub,code

; VOID ApplyTagChanges(struct TagItem * list, const struct TagItem * changeList)
	xdef	_ApplyTagChanges
_ApplyTagChanges:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-186(a6)
	movem.l	(sp)+,a6
	rts

	section	_SMult64_stub,code

; LONG SMult64(LONG arg1, LONG arg2)
	xdef	_SMult64
_SMult64:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_UtilityBase,a6
	jsr	-198(a6)
	movem.l	(sp)+,a6
	rts

	section	_UMult64_stub,code

; ULONG UMult64(ULONG arg1, ULONG arg2)
	xdef	_UMult64
_UMult64:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_UtilityBase,a6
	jsr	-204(a6)
	movem.l	(sp)+,a6
	rts

	section	_PackStructureTags_stub,code

; ULONG PackStructureTags(APTR pack, const ULONG * packTable, const struct TagItem * tagList)
	xdef	_PackStructureTags
_PackStructureTags:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_UtilityBase,a6
	jsr	-210(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_UnpackStructureTags_stub,code

; ULONG UnpackStructureTags(const APTR pack, const ULONG * packTable, struct TagItem * tagList)
	xdef	_UnpackStructureTags
_UnpackStructureTags:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_UtilityBase,a6
	jsr	-216(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_AddNamedObject_stub,code

; BOOL AddNamedObject(struct NamedObject * nameSpace, struct NamedObject * object)
	xdef	_AddNamedObject
_AddNamedObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-222(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocNamedObjectA_stub,code

; struct NamedObject * AllocNamedObjectA(CONST_STRPTR name, const struct TagItem * tagList)
	xdef	_AllocNamedObjectA
_AllocNamedObjectA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-228(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocNamedObject_stub,code

; struct NamedObject * AllocNamedObject(CONST_STRPTR name, Tag tagList, ... )
	xdef	_AllocNamedObject
_AllocNamedObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-228(a6)
	movem.l	(sp)+,a6
	rts

	section	_AttemptRemNamedObject_stub,code

; LONG AttemptRemNamedObject(struct NamedObject * object)
	xdef	_AttemptRemNamedObject
_AttemptRemNamedObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-234(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindNamedObject_stub,code

; struct NamedObject * FindNamedObject(struct NamedObject * nameSpace, CONST_STRPTR name, struct NamedObject * lastObject)
	xdef	_FindNamedObject
_FindNamedObject:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_UtilityBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_FreeNamedObject_stub,code

; VOID FreeNamedObject(struct NamedObject * object)
	xdef	_FreeNamedObject
_FreeNamedObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-246(a6)
	movem.l	(sp)+,a6
	rts

	section	_NamedObjectName_stub,code

; STRPTR NamedObjectName(struct NamedObject * object)
	xdef	_NamedObjectName
_NamedObjectName:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-252(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReleaseNamedObject_stub,code

; VOID ReleaseNamedObject(struct NamedObject * object)
	xdef	_ReleaseNamedObject
_ReleaseNamedObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_UtilityBase,a6
	jsr	-258(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemNamedObject_stub,code

; VOID RemNamedObject(struct NamedObject * object, struct Message * message)
	xdef	_RemNamedObject
_RemNamedObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_UtilityBase,a6
	jsr	-264(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetUniqueID_stub,code

; ULONG GetUniqueID()
	xdef	_GetUniqueID
_GetUniqueID:
	movem.l	a6,-(sp)
	movea.l	_UtilityBase,a6
	jsr	-270(a6)
	movem.l	(sp)+,a6
	rts

