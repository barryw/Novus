; icon library stubs for Novus
; Auto-generated from icon_lib.fd

	xref	_IconBase	; Provided by startup.o + -lamiga

	section	text,code

; FreeFreeList(freelist)
	xdef	_FreeFreeList
_FreeFreeList:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; freelist
	move.l	_IconBase,a6
	jsr	-54(a6)	; FreeFreeList()
	movem.l	(sp)+,a0/a6
	rts

; AddFreeList(freelist, mem, size)
	xdef	_AddFreeList
_AddFreeList:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; freelist
	move.l	16(sp),a1	; mem
	move.l	20(sp),a2	; size
	move.l	_IconBase,a6
	jsr	-72(a6)	; AddFreeList()
	movem.l	(sp)+,a0-a2/a6
	rts

; GetDiskObject(name)
	xdef	_GetDiskObject
_GetDiskObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	_IconBase,a6
	jsr	-78(a6)	; GetDiskObject()
	movem.l	(sp)+,a0/a6
	rts

; PutDiskObject(name, diskobj)
	xdef	_PutDiskObject
_PutDiskObject:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	16(sp),a1	; diskobj
	move.l	_IconBase,a6
	jsr	-84(a6)	; PutDiskObject()
	movem.l	(sp)+,a0-a1/a6
	rts

; FreeDiskObject(diskobj)
	xdef	_FreeDiskObject
_FreeDiskObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; diskobj
	move.l	_IconBase,a6
	jsr	-90(a6)	; FreeDiskObject()
	movem.l	(sp)+,a0/a6
	rts

; FindToolType(toolTypeArray, typeName)
	xdef	_FindToolType
_FindToolType:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; toolTypeArray
	move.l	16(sp),a1	; typeName
	move.l	_IconBase,a6
	jsr	-96(a6)	; FindToolType()
	movem.l	(sp)+,a0-a1/a6
	rts

; MatchToolValue(typeString, value)
	xdef	_MatchToolValue
_MatchToolValue:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; typeString
	move.l	16(sp),a1	; value
	move.l	_IconBase,a6
	jsr	-102(a6)	; MatchToolValue()
	movem.l	(sp)+,a0-a1/a6
	rts

; BumpRevision(newname, oldname)
	xdef	_BumpRevision
_BumpRevision:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; newname
	move.l	16(sp),a1	; oldname
	move.l	_IconBase,a6
	jsr	-108(a6)	; BumpRevision()
	movem.l	(sp)+,a0-a1/a6
	rts

; GetDefDiskObject(type)
	xdef	_GetDefDiskObject
_GetDefDiskObject:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; type
	move.l	_IconBase,a6
	jsr	-120(a6)	; GetDefDiskObject()
	movem.l	(sp)+,d0/a6
	rts

; PutDefDiskObject(diskObject)
	xdef	_PutDefDiskObject
_PutDefDiskObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; diskObject
	move.l	_IconBase,a6
	jsr	-126(a6)	; PutDefDiskObject()
	movem.l	(sp)+,a0/a6
	rts

; GetDiskObjectNew(name)
	xdef	_GetDiskObjectNew
_GetDiskObjectNew:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	_IconBase,a6
	jsr	-132(a6)	; GetDiskObjectNew()
	movem.l	(sp)+,a0/a6
	rts

; DeleteDiskObject(name)
	xdef	_DeleteDiskObject
_DeleteDiskObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	_IconBase,a6
	jsr	-138(a6)	; DeleteDiskObject()
	movem.l	(sp)+,a0/a6
	rts

; DupDiskObjectA(diskObject, tags)
	xdef	_DupDiskObjectA
_DupDiskObjectA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; diskObject
	move.l	16(sp),a1	; tags
	move.l	_IconBase,a6
	jsr	-150(a6)	; DupDiskObjectA()
	movem.l	(sp)+,a0-a1/a6
	rts

; IconControlA(icon, tags)
	xdef	_IconControlA
_IconControlA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; icon
	move.l	16(sp),a1	; tags
	move.l	_IconBase,a6
	jsr	-156(a6)	; IconControlA()
	movem.l	(sp)+,a0-a1/a6
	rts

; DrawIconStateA(rp, icon, label, leftOffset, topOffset, state, tags)
	xdef	_DrawIconStateA
_DrawIconStateA:
	movem.l	d0-d2/a0-a3/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),a1	; icon
	move.l	24(sp),a2	; label
	move.l	28(sp),d0	; leftOffset
	move.l	32(sp),d1	; topOffset
	move.l	36(sp),d2	; state
	move.l	40(sp),a3	; tags
	move.l	_IconBase,a6
	jsr	-162(a6)	; DrawIconStateA()
	movem.l	(sp)+,d0-d2/a0-a3/a6
	rts

; GetIconRectangleA(rp, icon, label, rect, tags)
	xdef	_GetIconRectangleA
_GetIconRectangleA:
	movem.l	a0-a4/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	16(sp),a1	; icon
	move.l	20(sp),a2	; label
	move.l	24(sp),a3	; rect
	move.l	28(sp),a4	; tags
	move.l	_IconBase,a6
	jsr	-168(a6)	; GetIconRectangleA()
	movem.l	(sp)+,a0-a4/a6
	rts

; NewDiskObject(type)
	xdef	_NewDiskObject
_NewDiskObject:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; type
	move.l	_IconBase,a6
	jsr	-174(a6)	; NewDiskObject()
	movem.l	(sp)+,d0/a6
	rts

; GetIconTagList(name, tags)
	xdef	_GetIconTagList
_GetIconTagList:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	16(sp),a1	; tags
	move.l	_IconBase,a6
	jsr	-180(a6)	; GetIconTagList()
	movem.l	(sp)+,a0-a1/a6
	rts

; PutIconTagList(name, icon, tags)
	xdef	_PutIconTagList
_PutIconTagList:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	16(sp),a1	; icon
	move.l	20(sp),a2	; tags
	move.l	_IconBase,a6
	jsr	-186(a6)	; PutIconTagList()
	movem.l	(sp)+,a0-a2/a6
	rts

; LayoutIconA(icon, screen, tags)
	xdef	_LayoutIconA
_LayoutIconA:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; icon
	move.l	16(sp),a1	; screen
	move.l	20(sp),a2	; tags
	move.l	_IconBase,a6
	jsr	-192(a6)	; LayoutIconA()
	movem.l	(sp)+,a0-a2/a6
	rts

; ChangeToSelectedIconColor(cr)
	xdef	_ChangeToSelectedIconColor
_ChangeToSelectedIconColor:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; cr
	move.l	_IconBase,a6
	jsr	-198(a6)	; ChangeToSelectedIconColor()
	movem.l	(sp)+,a0/a6
	rts

