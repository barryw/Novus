; Generated from SFD file by Novus SFD Parser
; Library: iffparse.library
; Base: _IFFParseBase
; Each function is in its own section for dead code elimination

	xref	_IFFParseBase

	section	_AllocIFF_stub,code

; struct IFFHandle * AllocIFF()
	xdef	_AllocIFF
_AllocIFF:
	movea.l	_IFFParseBase,a6
	jsr	-30(a6)
	rts

	section	_OpenIFF_stub,code

; LONG OpenIFF(struct IFFHandle * iff, LONG rwMode)
	xdef	_OpenIFF
_OpenIFF:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-36(a6)
	rts

	section	_ParseIFF_stub,code

; LONG ParseIFF(struct IFFHandle * iff, LONG control)
	xdef	_ParseIFF
_ParseIFF:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-42(a6)
	rts

	section	_CloseIFF_stub,code

; VOID CloseIFF(struct IFFHandle * iff)
	xdef	_CloseIFF
_CloseIFF:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-48(a6)
	rts

	section	_FreeIFF_stub,code

; VOID FreeIFF(struct IFFHandle * iff)
	xdef	_FreeIFF
_FreeIFF:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-54(a6)
	rts

	section	_ReadChunkBytes_stub,code

; LONG ReadChunkBytes(struct IFFHandle * iff, APTR buf, LONG numBytes)
	xdef	_ReadChunkBytes
_ReadChunkBytes:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-60(a6)
	rts

	section	_WriteChunkBytes_stub,code

; LONG WriteChunkBytes(struct IFFHandle * iff, const APTR buf, LONG numBytes)
	xdef	_WriteChunkBytes
_WriteChunkBytes:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-66(a6)
	rts

	section	_ReadChunkRecords_stub,code

; LONG ReadChunkRecords(struct IFFHandle * iff, APTR buf, LONG bytesPerRecord, LONG numRecords)
	xdef	_ReadChunkRecords
_ReadChunkRecords:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_IFFParseBase,a6
	jsr	-72(a6)
	rts

	section	_WriteChunkRecords_stub,code

; LONG WriteChunkRecords(struct IFFHandle * iff, const APTR buf, LONG bytesPerRecord, LONG numRecords)
	xdef	_WriteChunkRecords
_WriteChunkRecords:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_IFFParseBase,a6
	jsr	-78(a6)
	rts

	section	_PushChunk_stub,code

; LONG PushChunk(struct IFFHandle * iff, LONG type, LONG id, LONG size)
	xdef	_PushChunk
_PushChunk:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_IFFParseBase,a6
	jsr	-84(a6)
	rts

	section	_PopChunk_stub,code

; LONG PopChunk(struct IFFHandle * iff)
	xdef	_PopChunk
_PopChunk:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-90(a6)
	rts

	section	_EntryHandler_stub,code

; LONG EntryHandler(struct IFFHandle * iff, LONG type, LONG id, LONG position, struct Hook * handler, APTR object)
	xdef	_EntryHandler
_EntryHandler:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	_IFFParseBase,a6
	jsr	-102(a6)
	rts

	section	_ExitHandler_stub,code

; LONG ExitHandler(struct IFFHandle * iff, LONG type, LONG id, LONG position, struct Hook * handler, APTR object)
	xdef	_ExitHandler
_ExitHandler:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	_IFFParseBase,a6
	jsr	-108(a6)
	rts

	section	_PropChunk_stub,code

; LONG PropChunk(struct IFFHandle * iff, LONG type, LONG id)
	xdef	_PropChunk
_PropChunk:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IFFParseBase,a6
	jsr	-114(a6)
	rts

	section	_PropChunks_stub,code

; LONG PropChunks(struct IFFHandle * iff, const LONG * propArray, LONG numPairs)
	xdef	_PropChunks
_PropChunks:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-120(a6)
	rts

	section	_StopChunk_stub,code

; LONG StopChunk(struct IFFHandle * iff, LONG type, LONG id)
	xdef	_StopChunk
_StopChunk:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IFFParseBase,a6
	jsr	-126(a6)
	rts

	section	_StopChunks_stub,code

; LONG StopChunks(struct IFFHandle * iff, const LONG * propArray, LONG numPairs)
	xdef	_StopChunks
_StopChunks:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-132(a6)
	rts

	section	_CollectionChunk_stub,code

; LONG CollectionChunk(struct IFFHandle * iff, LONG type, LONG id)
	xdef	_CollectionChunk
_CollectionChunk:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IFFParseBase,a6
	jsr	-138(a6)
	rts

	section	_CollectionChunks_stub,code

; LONG CollectionChunks(struct IFFHandle * iff, const LONG * propArray, LONG numPairs)
	xdef	_CollectionChunks
_CollectionChunks:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-144(a6)
	rts

	section	_StopOnExit_stub,code

; LONG StopOnExit(struct IFFHandle * iff, LONG type, LONG id)
	xdef	_StopOnExit
_StopOnExit:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IFFParseBase,a6
	jsr	-150(a6)
	rts

	section	_FindProp_stub,code

; struct StoredProperty * FindProp(const struct IFFHandle * iff, LONG type, LONG id)
	xdef	_FindProp
_FindProp:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IFFParseBase,a6
	jsr	-156(a6)
	rts

	section	_FindCollection_stub,code

; struct CollectionItem * FindCollection(const struct IFFHandle * iff, LONG type, LONG id)
	xdef	_FindCollection
_FindCollection:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IFFParseBase,a6
	jsr	-162(a6)
	rts

	section	_FindPropContext_stub,code

; struct ContextNode * FindPropContext(const struct IFFHandle * iff)
	xdef	_FindPropContext
_FindPropContext:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-168(a6)
	rts

	section	_CurrentChunk_stub,code

; struct ContextNode * CurrentChunk(const struct IFFHandle * iff)
	xdef	_CurrentChunk
_CurrentChunk:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-174(a6)
	rts

	section	_ParentChunk_stub,code

; struct ContextNode * ParentChunk(const struct ContextNode * contextNode)
	xdef	_ParentChunk
_ParentChunk:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-180(a6)
	rts

	section	_AllocLocalItem_stub,code

; struct LocalContextItem * AllocLocalItem(LONG type, LONG id, LONG ident, LONG dataSize)
	xdef	_AllocLocalItem
_AllocLocalItem:
	move.l	4(sp),d0
	move.l	8(sp),d1
	move.l	12(sp),d2
	move.l	16(sp),d3
	movea.l	_IFFParseBase,a6
	jsr	-186(a6)
	rts

	section	_LocalItemData_stub,code

; APTR LocalItemData(const struct LocalContextItem * localItem)
	xdef	_LocalItemData
_LocalItemData:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-192(a6)
	rts

	section	_SetLocalItemPurge_stub,code

; VOID SetLocalItemPurge(struct LocalContextItem * localItem, const struct Hook * purgeHook)
	xdef	_SetLocalItemPurge
_SetLocalItemPurge:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IFFParseBase,a6
	jsr	-198(a6)
	rts

	section	_FreeLocalItem_stub,code

; VOID FreeLocalItem(struct LocalContextItem * localItem)
	xdef	_FreeLocalItem
_FreeLocalItem:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-204(a6)
	rts

	section	_FindLocalItem_stub,code

; struct LocalContextItem * FindLocalItem(const struct IFFHandle * iff, LONG type, LONG id, LONG ident)
	xdef	_FindLocalItem
_FindLocalItem:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_IFFParseBase,a6
	jsr	-210(a6)
	rts

	section	_StoreLocalItem_stub,code

; LONG StoreLocalItem(struct IFFHandle * iff, struct LocalContextItem * localItem, LONG position)
	xdef	_StoreLocalItem
_StoreLocalItem:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-216(a6)
	rts

	section	_StoreItemInContext_stub,code

; VOID StoreItemInContext(struct IFFHandle * iff, struct LocalContextItem * localItem, struct ContextNode * contextNode)
	xdef	_StoreItemInContext
_StoreItemInContext:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IFFParseBase,a6
	jsr	-222(a6)
	rts

	section	_InitIFF_stub,code

; VOID InitIFF(struct IFFHandle * iff, LONG flags, const struct Hook * streamHook)
	xdef	_InitIFF
_InitIFF:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_IFFParseBase,a6
	jsr	-228(a6)
	rts

	section	_InitIFFasDOS_stub,code

; VOID InitIFFasDOS(struct IFFHandle * iff)
	xdef	_InitIFFasDOS
_InitIFFasDOS:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-234(a6)
	rts

	section	_InitIFFasClip_stub,code

; VOID InitIFFasClip(struct IFFHandle * iff)
	xdef	_InitIFFasClip
_InitIFFasClip:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-240(a6)
	rts

	section	_OpenClipboard_stub,code

; struct ClipboardHandle * OpenClipboard(LONG unitNumber)
	xdef	_OpenClipboard
_OpenClipboard:
	move.l	4(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-246(a6)
	rts

	section	_CloseClipboard_stub,code

; VOID CloseClipboard(struct ClipboardHandle * clipHandle)
	xdef	_CloseClipboard
_CloseClipboard:
	movea.l	4(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-252(a6)
	rts

	section	_GoodID_stub,code

; LONG GoodID(LONG id)
	xdef	_GoodID
_GoodID:
	move.l	4(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-258(a6)
	rts

	section	_GoodType_stub,code

; LONG GoodType(LONG type)
	xdef	_GoodType
_GoodType:
	move.l	4(sp),d0
	movea.l	_IFFParseBase,a6
	jsr	-264(a6)
	rts

	section	_IDtoStr_stub,code

; STRPTR IDtoStr(LONG id, STRPTR buf)
	xdef	_IDtoStr
_IDtoStr:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_IFFParseBase,a6
	jsr	-270(a6)
	rts

