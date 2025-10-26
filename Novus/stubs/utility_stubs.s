; utility library stubs for Novus
; Auto-generated from utility_lib.fd

	xref	_UtilityBase	; Provided by startup.o + -lamiga

	section	text,code

; FindTagItem(tagVal, tagList)
	xdef	_FindTagItem
_FindTagItem:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),d0	; tagVal
	move.l	20(sp),a0	; tagList
	move.l	_UtilityBase,a6
	jsr	-30(a6)	; FindTagItem()
	movem.l	(sp)+,d0/a0/a6
	rts

; GetTagData(tagValue, defaultVal, tagList)
	xdef	_GetTagData
_GetTagData:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),d0	; tagValue
	move.l	20(sp),d1	; defaultVal
	move.l	24(sp),a0	; tagList
	move.l	_UtilityBase,a6
	jsr	-36(a6)	; GetTagData()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; PackBoolTags(initialFlags, tagList, boolMap)
	xdef	_PackBoolTags
_PackBoolTags:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),d0	; initialFlags
	move.l	20(sp),a0	; tagList
	move.l	24(sp),a1	; boolMap
	move.l	_UtilityBase,a6
	jsr	-42(a6)	; PackBoolTags()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; NextTagItem(tagListPtr)
	xdef	_NextTagItem
_NextTagItem:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; tagListPtr
	move.l	_UtilityBase,a6
	jsr	-48(a6)	; NextTagItem()
	movem.l	(sp)+,a0/a6
	rts

; FilterTagChanges(changeList, originalList, apply)
	xdef	_FilterTagChanges
_FilterTagChanges:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; changeList
	move.l	20(sp),a1	; originalList
	move.l	24(sp),d0	; apply
	move.l	_UtilityBase,a6
	jsr	-54(a6)	; FilterTagChanges()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; MapTags(tagList, mapList, mapType)
	xdef	_MapTags
_MapTags:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; tagList
	move.l	20(sp),a1	; mapList
	move.l	24(sp),d0	; mapType
	move.l	_UtilityBase,a6
	jsr	-60(a6)	; MapTags()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; AllocateTagItems(numTags)
	xdef	_AllocateTagItems
_AllocateTagItems:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; numTags
	move.l	_UtilityBase,a6
	jsr	-66(a6)	; AllocateTagItems()
	movem.l	(sp)+,d0/a6
	rts

; CloneTagItems(tagList)
	xdef	_CloneTagItems
_CloneTagItems:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; tagList
	move.l	_UtilityBase,a6
	jsr	-72(a6)	; CloneTagItems()
	movem.l	(sp)+,a0/a6
	rts

; FreeTagItems(tagList)
	xdef	_FreeTagItems
_FreeTagItems:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; tagList
	move.l	_UtilityBase,a6
	jsr	-78(a6)	; FreeTagItems()
	movem.l	(sp)+,a0/a6
	rts

; RefreshTagItemClones(clone, original)
	xdef	_RefreshTagItemClones
_RefreshTagItemClones:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; clone
	move.l	16(sp),a1	; original
	move.l	_UtilityBase,a6
	jsr	-84(a6)	; RefreshTagItemClones()
	movem.l	(sp)+,a0-a1/a6
	rts

; TagInArray(tagValue, tagArray)
	xdef	_TagInArray
_TagInArray:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),d0	; tagValue
	move.l	20(sp),a0	; tagArray
	move.l	_UtilityBase,a6
	jsr	-90(a6)	; TagInArray()
	movem.l	(sp)+,d0/a0/a6
	rts

; FilterTagItems(tagList, filterArray, logic)
	xdef	_FilterTagItems
_FilterTagItems:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; tagList
	move.l	20(sp),a1	; filterArray
	move.l	24(sp),d0	; logic
	move.l	_UtilityBase,a6
	jsr	-96(a6)	; FilterTagItems()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; CallHookPkt(hook, object, paramPacket)
	xdef	_CallHookPkt
_CallHookPkt:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; hook
	move.l	16(sp),a2	; object
	move.l	20(sp),a1	; paramPacket
	move.l	_UtilityBase,a6
	jsr	-102(a6)	; CallHookPkt()
	movem.l	(sp)+,a0-a2/a6
	rts

; Amiga2Date(seconds, result)
	xdef	_Amiga2Date
_Amiga2Date:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),d0	; seconds
	move.l	20(sp),a0	; result
	move.l	_UtilityBase,a6
	jsr	-120(a6)	; Amiga2Date()
	movem.l	(sp)+,d0/a0/a6
	rts

; Date2Amiga(date)
	xdef	_Date2Amiga
_Date2Amiga:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; date
	move.l	_UtilityBase,a6
	jsr	-126(a6)	; Date2Amiga()
	movem.l	(sp)+,a0/a6
	rts

; CheckDate(date)
	xdef	_CheckDate
_CheckDate:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; date
	move.l	_UtilityBase,a6
	jsr	-132(a6)	; CheckDate()
	movem.l	(sp)+,a0/a6
	rts

; SMult32(arg1, arg2)
	xdef	_SMult32
_SMult32:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; arg1
	move.l	16(sp),d1	; arg2
	move.l	_UtilityBase,a6
	jsr	-138(a6)	; SMult32()
	movem.l	(sp)+,d0-d1/a6
	rts

; UMult32(arg1, arg2)
	xdef	_UMult32
_UMult32:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; arg1
	move.l	16(sp),d1	; arg2
	move.l	_UtilityBase,a6
	jsr	-144(a6)	; UMult32()
	movem.l	(sp)+,d0-d1/a6
	rts

; SDivMod32(dividend, divisor)
	xdef	_SDivMod32
_SDivMod32:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; dividend
	move.l	16(sp),d1	; divisor
	move.l	_UtilityBase,a6
	jsr	-150(a6)	; SDivMod32()
	movem.l	(sp)+,d0-d1/a6
	rts

; UDivMod32(dividend, divisor)
	xdef	_UDivMod32
_UDivMod32:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; dividend
	move.l	16(sp),d1	; divisor
	move.l	_UtilityBase,a6
	jsr	-156(a6)	; UDivMod32()
	movem.l	(sp)+,d0-d1/a6
	rts

; Stricmp(string1, string2)
	xdef	_Stricmp
_Stricmp:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; string1
	move.l	16(sp),a1	; string2
	move.l	_UtilityBase,a6
	jsr	-162(a6)	; Stricmp()
	movem.l	(sp)+,a0-a1/a6
	rts

; Strnicmp(string1, string2, length)
	xdef	_Strnicmp
_Strnicmp:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; string1
	move.l	20(sp),a1	; string2
	move.l	24(sp),d0	; length
	move.l	_UtilityBase,a6
	jsr	-168(a6)	; Strnicmp()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; ToUpper(character)
	xdef	_ToUpper
_ToUpper:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; character
	move.l	_UtilityBase,a6
	jsr	-174(a6)	; ToUpper()
	movem.l	(sp)+,d0/a6
	rts

; ToLower(character)
	xdef	_ToLower
_ToLower:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; character
	move.l	_UtilityBase,a6
	jsr	-180(a6)	; ToLower()
	movem.l	(sp)+,d0/a6
	rts

; ApplyTagChanges(list, changeList)
	xdef	_ApplyTagChanges
_ApplyTagChanges:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; list
	move.l	16(sp),a1	; changeList
	move.l	_UtilityBase,a6
	jsr	-186(a6)	; ApplyTagChanges()
	movem.l	(sp)+,a0-a1/a6
	rts

; SMult64(arg1, arg2)
	xdef	_SMult64
_SMult64:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; arg1
	move.l	16(sp),d1	; arg2
	move.l	_UtilityBase,a6
	jsr	-198(a6)	; SMult64()
	movem.l	(sp)+,d0-d1/a6
	rts

; UMult64(arg1, arg2)
	xdef	_UMult64
_UMult64:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; arg1
	move.l	16(sp),d1	; arg2
	move.l	_UtilityBase,a6
	jsr	-204(a6)	; UMult64()
	movem.l	(sp)+,d0-d1/a6
	rts

; PackStructureTags(pack, packTable, tagList)
	xdef	_PackStructureTags
_PackStructureTags:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; pack
	move.l	16(sp),a1	; packTable
	move.l	20(sp),a2	; tagList
	move.l	_UtilityBase,a6
	jsr	-210(a6)	; PackStructureTags()
	movem.l	(sp)+,a0-a2/a6
	rts

; UnpackStructureTags(pack, packTable, tagList)
	xdef	_UnpackStructureTags
_UnpackStructureTags:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; pack
	move.l	16(sp),a1	; packTable
	move.l	20(sp),a2	; tagList
	move.l	_UtilityBase,a6
	jsr	-216(a6)	; UnpackStructureTags()
	movem.l	(sp)+,a0-a2/a6
	rts

; AddNamedObject(nameSpace, object)
	xdef	_AddNamedObject
_AddNamedObject:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; nameSpace
	move.l	16(sp),a1	; object
	move.l	_UtilityBase,a6
	jsr	-222(a6)	; AddNamedObject()
	movem.l	(sp)+,a0-a1/a6
	rts

; AllocNamedObjectA(name, tagList)
	xdef	_AllocNamedObjectA
_AllocNamedObjectA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	16(sp),a1	; tagList
	move.l	_UtilityBase,a6
	jsr	-228(a6)	; AllocNamedObjectA()
	movem.l	(sp)+,a0-a1/a6
	rts

; AttemptRemNamedObject(object)
	xdef	_AttemptRemNamedObject
_AttemptRemNamedObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; object
	move.l	_UtilityBase,a6
	jsr	-234(a6)	; AttemptRemNamedObject()
	movem.l	(sp)+,a0/a6
	rts

; FindNamedObject(nameSpace, name, lastObject)
	xdef	_FindNamedObject
_FindNamedObject:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; nameSpace
	move.l	16(sp),a1	; name
	move.l	20(sp),a2	; lastObject
	move.l	_UtilityBase,a6
	jsr	-240(a6)	; FindNamedObject()
	movem.l	(sp)+,a0-a2/a6
	rts

; FreeNamedObject(object)
	xdef	_FreeNamedObject
_FreeNamedObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; object
	move.l	_UtilityBase,a6
	jsr	-246(a6)	; FreeNamedObject()
	movem.l	(sp)+,a0/a6
	rts

; NamedObjectName(object)
	xdef	_NamedObjectName
_NamedObjectName:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; object
	move.l	_UtilityBase,a6
	jsr	-252(a6)	; NamedObjectName()
	movem.l	(sp)+,a0/a6
	rts

; ReleaseNamedObject(object)
	xdef	_ReleaseNamedObject
_ReleaseNamedObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; object
	move.l	_UtilityBase,a6
	jsr	-258(a6)	; ReleaseNamedObject()
	movem.l	(sp)+,a0/a6
	rts

; RemNamedObject(object, message)
	xdef	_RemNamedObject
_RemNamedObject:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; object
	move.l	16(sp),a1	; message
	move.l	_UtilityBase,a6
	jsr	-264(a6)	; RemNamedObject()
	movem.l	(sp)+,a0-a1/a6
	rts

; GetUniqueID()
	xdef	_GetUniqueID
_GetUniqueID:
	movem.l	a6,-(sp)
	move.l	_UtilityBase,a6
	jsr	-270(a6)	; GetUniqueID()
	movem.l	(sp)+,a6
	rts

