; iffparse library stubs for Novus
; Auto-generated from iffparse_lib.fd

	xref	_IFFParseBase	; Provided by startup.o + -lamiga

	section	text,code

; AllocIFF()
	xdef	_AllocIFF
_AllocIFF:
	movem.l	a6,-(sp)
	move.l	_IFFParseBase,a6
	jsr	-30(a6)	; AllocIFF()
	movem.l	(sp)+,a6
	rts

; OpenIFF(iff, rwMode)
	xdef	_OpenIFF
_OpenIFF:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; rwMode
	move.l	_IFFParseBase,a6
	jsr	-36(a6)	; OpenIFF()
	movem.l	(sp)+,d0/a0/a6
	rts

; ParseIFF(iff, control)
	xdef	_ParseIFF
_ParseIFF:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; control
	move.l	_IFFParseBase,a6
	jsr	-42(a6)	; ParseIFF()
	movem.l	(sp)+,d0/a0/a6
	rts

; CloseIFF(iff)
	xdef	_CloseIFF
_CloseIFF:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iff
	move.l	_IFFParseBase,a6
	jsr	-48(a6)	; CloseIFF()
	movem.l	(sp)+,a0/a6
	rts

; FreeIFF(iff)
	xdef	_FreeIFF
_FreeIFF:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iff
	move.l	_IFFParseBase,a6
	jsr	-54(a6)	; FreeIFF()
	movem.l	(sp)+,a0/a6
	rts

; ReadChunkBytes(iff, buf, numBytes)
	xdef	_ReadChunkBytes
_ReadChunkBytes:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),a1	; buf
	move.l	24(sp),d0	; numBytes
	move.l	_IFFParseBase,a6
	jsr	-60(a6)	; ReadChunkBytes()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; WriteChunkBytes(iff, buf, numBytes)
	xdef	_WriteChunkBytes
_WriteChunkBytes:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),a1	; buf
	move.l	24(sp),d0	; numBytes
	move.l	_IFFParseBase,a6
	jsr	-66(a6)	; WriteChunkBytes()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; ReadChunkRecords(iff, buf, bytesPerRecord, numRecords)
	xdef	_ReadChunkRecords
_ReadChunkRecords:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),a1	; buf
	move.l	24(sp),d0	; bytesPerRecord
	move.l	28(sp),d1	; numRecords
	move.l	_IFFParseBase,a6
	jsr	-72(a6)	; ReadChunkRecords()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; WriteChunkRecords(iff, buf, bytesPerRecord, numRecords)
	xdef	_WriteChunkRecords
_WriteChunkRecords:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),a1	; buf
	move.l	24(sp),d0	; bytesPerRecord
	move.l	28(sp),d1	; numRecords
	move.l	_IFFParseBase,a6
	jsr	-78(a6)	; WriteChunkRecords()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; PushChunk(iff, type, id, size)
	xdef	_PushChunk
_PushChunk:
	movem.l	d0-d2/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	28(sp),d2	; size
	move.l	_IFFParseBase,a6
	jsr	-84(a6)	; PushChunk()
	movem.l	(sp)+,d0-d2/a0/a6
	rts

; PopChunk(iff)
	xdef	_PopChunk
_PopChunk:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iff
	move.l	_IFFParseBase,a6
	jsr	-90(a6)	; PopChunk()
	movem.l	(sp)+,a0/a6
	rts

; EntryHandler(iff, type, id, position, handler, object)
	xdef	_EntryHandler
_EntryHandler:
	movem.l	d0-d2/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	28(sp),d2	; position
	move.l	32(sp),a1	; handler
	move.l	36(sp),a2	; object
	move.l	_IFFParseBase,a6
	jsr	-102(a6)	; EntryHandler()
	movem.l	(sp)+,d0-d2/a0-a2/a6
	rts

; ExitHandler(iff, type, id, position, handler, object)
	xdef	_ExitHandler
_ExitHandler:
	movem.l	d0-d2/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	28(sp),d2	; position
	move.l	32(sp),a1	; handler
	move.l	36(sp),a2	; object
	move.l	_IFFParseBase,a6
	jsr	-108(a6)	; ExitHandler()
	movem.l	(sp)+,d0-d2/a0-a2/a6
	rts

; PropChunk(iff, type, id)
	xdef	_PropChunk
_PropChunk:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	_IFFParseBase,a6
	jsr	-114(a6)	; PropChunk()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; PropChunks(iff, propArray, numPairs)
	xdef	_PropChunks
_PropChunks:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),a1	; propArray
	move.l	24(sp),d0	; numPairs
	move.l	_IFFParseBase,a6
	jsr	-120(a6)	; PropChunks()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; StopChunk(iff, type, id)
	xdef	_StopChunk
_StopChunk:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	_IFFParseBase,a6
	jsr	-126(a6)	; StopChunk()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; StopChunks(iff, propArray, numPairs)
	xdef	_StopChunks
_StopChunks:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),a1	; propArray
	move.l	24(sp),d0	; numPairs
	move.l	_IFFParseBase,a6
	jsr	-132(a6)	; StopChunks()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; CollectionChunk(iff, type, id)
	xdef	_CollectionChunk
_CollectionChunk:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	_IFFParseBase,a6
	jsr	-138(a6)	; CollectionChunk()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; CollectionChunks(iff, propArray, numPairs)
	xdef	_CollectionChunks
_CollectionChunks:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),a1	; propArray
	move.l	24(sp),d0	; numPairs
	move.l	_IFFParseBase,a6
	jsr	-144(a6)	; CollectionChunks()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; StopOnExit(iff, type, id)
	xdef	_StopOnExit
_StopOnExit:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	_IFFParseBase,a6
	jsr	-150(a6)	; StopOnExit()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; FindProp(iff, type, id)
	xdef	_FindProp
_FindProp:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	_IFFParseBase,a6
	jsr	-156(a6)	; FindProp()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; FindCollection(iff, type, id)
	xdef	_FindCollection
_FindCollection:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	_IFFParseBase,a6
	jsr	-162(a6)	; FindCollection()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; FindPropContext(iff)
	xdef	_FindPropContext
_FindPropContext:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iff
	move.l	_IFFParseBase,a6
	jsr	-168(a6)	; FindPropContext()
	movem.l	(sp)+,a0/a6
	rts

; CurrentChunk(iff)
	xdef	_CurrentChunk
_CurrentChunk:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iff
	move.l	_IFFParseBase,a6
	jsr	-174(a6)	; CurrentChunk()
	movem.l	(sp)+,a0/a6
	rts

; ParentChunk(contextNode)
	xdef	_ParentChunk
_ParentChunk:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; contextNode
	move.l	_IFFParseBase,a6
	jsr	-180(a6)	; ParentChunk()
	movem.l	(sp)+,a0/a6
	rts

; AllocLocalItem(type, id, ident, dataSize)
	xdef	_AllocLocalItem
_AllocLocalItem:
	movem.l	d0-d3/a6,-(sp)
	move.l	12(sp),d0	; type
	move.l	16(sp),d1	; id
	move.l	20(sp),d2	; ident
	move.l	24(sp),d3	; dataSize
	move.l	_IFFParseBase,a6
	jsr	-186(a6)	; AllocLocalItem()
	movem.l	(sp)+,d0-d3/a6
	rts

; LocalItemData(localItem)
	xdef	_LocalItemData
_LocalItemData:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; localItem
	move.l	_IFFParseBase,a6
	jsr	-192(a6)	; LocalItemData()
	movem.l	(sp)+,a0/a6
	rts

; SetLocalItemPurge(localItem, purgeHook)
	xdef	_SetLocalItemPurge
_SetLocalItemPurge:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; localItem
	move.l	16(sp),a1	; purgeHook
	move.l	_IFFParseBase,a6
	jsr	-198(a6)	; SetLocalItemPurge()
	movem.l	(sp)+,a0-a1/a6
	rts

; FreeLocalItem(localItem)
	xdef	_FreeLocalItem
_FreeLocalItem:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; localItem
	move.l	_IFFParseBase,a6
	jsr	-204(a6)	; FreeLocalItem()
	movem.l	(sp)+,a0/a6
	rts

; FindLocalItem(iff, type, id, ident)
	xdef	_FindLocalItem
_FindLocalItem:
	movem.l	d0-d2/a0/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; type
	move.l	24(sp),d1	; id
	move.l	28(sp),d2	; ident
	move.l	_IFFParseBase,a6
	jsr	-210(a6)	; FindLocalItem()
	movem.l	(sp)+,d0-d2/a0/a6
	rts

; StoreLocalItem(iff, localItem, position)
	xdef	_StoreLocalItem
_StoreLocalItem:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),a1	; localItem
	move.l	24(sp),d0	; position
	move.l	_IFFParseBase,a6
	jsr	-216(a6)	; StoreLocalItem()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; StoreItemInContext(iff, localItem, contextNode)
	xdef	_StoreItemInContext
_StoreItemInContext:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; iff
	move.l	16(sp),a1	; localItem
	move.l	20(sp),a2	; contextNode
	move.l	_IFFParseBase,a6
	jsr	-222(a6)	; StoreItemInContext()
	movem.l	(sp)+,a0-a2/a6
	rts

; InitIFF(iff, flags, streamHook)
	xdef	_InitIFF
_InitIFF:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; iff
	move.l	20(sp),d0	; flags
	move.l	24(sp),a1	; streamHook
	move.l	_IFFParseBase,a6
	jsr	-228(a6)	; InitIFF()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; InitIFFasDOS(iff)
	xdef	_InitIFFasDOS
_InitIFFasDOS:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iff
	move.l	_IFFParseBase,a6
	jsr	-234(a6)	; InitIFFasDOS()
	movem.l	(sp)+,a0/a6
	rts

; InitIFFasClip(iff)
	xdef	_InitIFFasClip
_InitIFFasClip:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iff
	move.l	_IFFParseBase,a6
	jsr	-240(a6)	; InitIFFasClip()
	movem.l	(sp)+,a0/a6
	rts

; OpenClipboard(unitNumber)
	xdef	_OpenClipboard
_OpenClipboard:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; unitNumber
	move.l	_IFFParseBase,a6
	jsr	-246(a6)	; OpenClipboard()
	movem.l	(sp)+,d0/a6
	rts

; CloseClipboard(clipHandle)
	xdef	_CloseClipboard
_CloseClipboard:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; clipHandle
	move.l	_IFFParseBase,a6
	jsr	-252(a6)	; CloseClipboard()
	movem.l	(sp)+,a0/a6
	rts

; GoodID(id)
	xdef	_GoodID
_GoodID:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; id
	move.l	_IFFParseBase,a6
	jsr	-258(a6)	; GoodID()
	movem.l	(sp)+,d0/a6
	rts

; GoodType(type)
	xdef	_GoodType
_GoodType:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; type
	move.l	_IFFParseBase,a6
	jsr	-264(a6)	; GoodType()
	movem.l	(sp)+,d0/a6
	rts

; IDtoStr(id, buf)
	xdef	_IDtoStr
_IDtoStr:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),d0	; id
	move.l	20(sp),a0	; buf
	move.l	_IFFParseBase,a6
	jsr	-270(a6)	; IDtoStr()
	movem.l	(sp)+,d0/a0/a6
	rts

