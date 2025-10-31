; Generated from SFD file by Novus SFD Parser
; Library: aml.library
; Base: _AmlBase
; Each function is in its own section for dead code elimination

	xref	_AmlBase

	section	_RexxDispatcher_stub,code

; LONG RexxDispatcher(struct RexxMsg * rxm)
	xdef	_RexxDispatcher
_RexxDispatcher:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-30(a6)
	rts

	section	_CreateServerA_stub,code

; APTR CreateServerA(struct TagItem * tags)
	xdef	_CreateServerA
_CreateServerA:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-36(a6)
	rts

	section	_DisposeServer_stub,code

; VOID DisposeServer(APTR server)
	xdef	_DisposeServer
_DisposeServer:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-48(a6)
	rts

	section	_SetServerAttrsA_stub,code

; ULONG SetServerAttrsA(APTR server, struct TagItem * tags)
	xdef	_SetServerAttrsA
_SetServerAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-54(a6)
	rts

	section	_GetServerAttrsA_stub,code

; ULONG GetServerAttrsA(APTR server, struct TagItem * tags)
	xdef	_GetServerAttrsA
_GetServerAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-66(a6)
	rts

	section	_GetServerHeaders_stub,code

; ULONG GetServerHeaders(APTR server, ULONG flags)
	xdef	_GetServerHeaders
_GetServerHeaders:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmlBase,a6
	jsr	-78(a6)
	rts

	section	_GetServerArticles_stub,code

; LONG GetServerArticles(APTR server, APTR folder, struct Hook * hook, ULONG flags)
	xdef	_GetServerArticles
_GetServerArticles:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	movea.l	_AmlBase,a6
	jsr	-84(a6)
	rts

	section	_CreateFolderA_stub,code

; APTR CreateFolderA(APTR server, struct TagItem * tags)
	xdef	_CreateFolderA
_CreateFolderA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-90(a6)
	rts

	section	_DisposeFolder_stub,code

; BOOL DisposeFolder(APTR folder)
	xdef	_DisposeFolder
_DisposeFolder:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-102(a6)
	rts

	section	_OpenFolderA_stub,code

; APTR OpenFolderA(APTR server, struct TagItem * tags)
	xdef	_OpenFolderA
_OpenFolderA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-108(a6)
	rts

	section	_SaveFolder_stub,code

; BOOL SaveFolder(APTR folder)
	xdef	_SaveFolder
_SaveFolder:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-120(a6)
	rts

	section	_RemFolder_stub,code

; BOOL RemFolder(APTR folder)
	xdef	_RemFolder
_RemFolder:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-126(a6)
	rts

	section	_SetFolderAttrsA_stub,code

; ULONG SetFolderAttrsA(APTR folder, struct TagItem * tags)
	xdef	_SetFolderAttrsA
_SetFolderAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-132(a6)
	rts

	section	_GetFolderAttrsA_stub,code

; ULONG GetFolderAttrsA(APTR folder, struct TagItem * tags)
	xdef	_GetFolderAttrsA
_GetFolderAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-144(a6)
	rts

	section	_AddFolderArticle_stub,code

; BOOL AddFolderArticle(APTR folder, ULONG type, APTR data)
	xdef	_AddFolderArticle
_AddFolderArticle:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-156(a6)
	rts

	section	_RemFolderArticle_stub,code

; BOOL RemFolderArticle(APTR folder, APTR article)
	xdef	_RemFolderArticle
_RemFolderArticle:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-162(a6)
	rts

	section	_ReadFolderSpool_stub,code

; ULONG ReadFolderSpool(APTR folder, STRPTR importfile, ULONG flags)
	xdef	_ReadFolderSpool
_ReadFolderSpool:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-168(a6)
	rts

	section	_WriteFolderSpool_stub,code

; ULONG WriteFolderSpool(APTR folder, STRPTR exportfile, ULONG flags)
	xdef	_WriteFolderSpool
_WriteFolderSpool:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-174(a6)
	rts

	section	_ScanFolderIndex_stub,code

; ULONG ScanFolderIndex(APTR folder, struct Hook * hook, ULONG flags)
	xdef	_ScanFolderIndex
_ScanFolderIndex:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-180(a6)
	rts

	section	_ExpungeFolder_stub,code

; BOOL ExpungeFolder(APTR folder, APTR trash, struct Hook * hook)
	xdef	_ExpungeFolder
_ExpungeFolder:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_AmlBase,a6
	jsr	-186(a6)
	rts

	section	_CreateFolderIndex_stub,code

; ULONG CreateFolderIndex(APTR folder)
	xdef	_CreateFolderIndex
_CreateFolderIndex:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-192(a6)
	rts

	section	_SortFolderIndex_stub,code

; ULONG SortFolderIndex(APTR folder, ULONG field)
	xdef	_SortFolderIndex
_SortFolderIndex:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmlBase,a6
	jsr	-198(a6)
	rts

	section	_CreateArticleA_stub,code

; APTR CreateArticleA(APTR folder, struct TagItem * tags)
	xdef	_CreateArticleA
_CreateArticleA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-204(a6)
	rts

	section	_DisposeArticle_stub,code

; BOOL DisposeArticle(APTR article)
	xdef	_DisposeArticle
_DisposeArticle:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-216(a6)
	rts

	section	_OpenArticle_stub,code

; APTR OpenArticle(APTR server, APTR folder, ULONG MsgID, ULONG Flags)
	xdef	_OpenArticle
_OpenArticle:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_AmlBase,a6
	jsr	-222(a6)
	rts

	section	_CopyArticle_stub,code

; BOOL CopyArticle(APTR folder, APTR article)
	xdef	_CopyArticle
_CopyArticle:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-228(a6)
	rts

	section	_SetArticleAttrsA_stub,code

; ULONG SetArticleAttrsA(APTR article, struct TagItem * tags)
	xdef	_SetArticleAttrsA
_SetArticleAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-234(a6)
	rts

	section	_GetArticleAttrsA_stub,code

; ULONG GetArticleAttrsA(APTR article, struct TagItem * tags)
	xdef	_GetArticleAttrsA
_GetArticleAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-246(a6)
	rts

	section	_SendArticle_stub,code

; BOOL SendArticle(APTR server, APTR article, UBYTE * from_file)
	xdef	_SendArticle
_SendArticle:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_AmlBase,a6
	jsr	-258(a6)
	rts

	section	_AddArticlePartA_stub,code

; BOOL AddArticlePartA(APTR article, APTR part, struct TagItem * tags)
	xdef	_AddArticlePartA
_AddArticlePartA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_AmlBase,a6
	jsr	-264(a6)
	rts

	section	_RemArticlePart_stub,code

; VOID RemArticlePart(APTR article, APTR part)
	xdef	_RemArticlePart
_RemArticlePart:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmlBase,a6
	jsr	-276(a6)
	rts

	section	_GetArticlePart_stub,code

; APTR GetArticlePart(APTR article, ULONG partnum)
	xdef	_GetArticlePart
_GetArticlePart:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmlBase,a6
	jsr	-282(a6)
	rts

	section	_GetArticlePartAttrsA_stub,code

; ULONG GetArticlePartAttrsA(APTR part, struct TagItem * tags)
	xdef	_GetArticlePartAttrsA
_GetArticlePartAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-288(a6)
	rts

	section	_SetArticlePartAttrsA_stub,code

; ULONG SetArticlePartAttrsA(APTR part, struct TagItem * tags)
	xdef	_SetArticlePartAttrsA
_SetArticlePartAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-300(a6)
	rts

	section	_CreateArticlePartA_stub,code

; APTR CreateArticlePartA(APTR article, struct TagItem * tags)
	xdef	_CreateArticlePartA
_CreateArticlePartA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-312(a6)
	rts

	section	_DisposeArticlePart_stub,code

; VOID DisposeArticlePart(APTR part)
	xdef	_DisposeArticlePart
_DisposeArticlePart:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-324(a6)
	rts

	section	_GetArticlePartDataA_stub,code

; BOOL GetArticlePartDataA(APTR article, APTR part, struct TagItem * tags)
	xdef	_GetArticlePartDataA
_GetArticlePartDataA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_AmlBase,a6
	jsr	-330(a6)
	rts

	section	_SetArticlePartDataA_stub,code

; BOOL SetArticlePartDataA(APTR part, struct TagItem * tags)
	xdef	_SetArticlePartDataA
_SetArticlePartDataA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-342(a6)
	rts

	section	_CreateAddressEntryA_stub,code

; APTR CreateAddressEntryA(struct TagItem * tags)
	xdef	_CreateAddressEntryA
_CreateAddressEntryA:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-354(a6)
	rts

	section	_DisposeAddressEntry_stub,code

; BOOL DisposeAddressEntry(APTR addr)
	xdef	_DisposeAddressEntry
_DisposeAddressEntry:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-366(a6)
	rts

	section	_OpenAddressEntry_stub,code

; APTR OpenAddressEntry(APTR server, ULONG fileid)
	xdef	_OpenAddressEntry
_OpenAddressEntry:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmlBase,a6
	jsr	-372(a6)
	rts

	section	_SaveAddressEntry_stub,code

; LONG SaveAddressEntry(APTR server, APTR addr)
	xdef	_SaveAddressEntry
_SaveAddressEntry:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-378(a6)
	rts

	section	_RemAddressEntry_stub,code

; BOOL RemAddressEntry(APTR server, APTR addr)
	xdef	_RemAddressEntry
_RemAddressEntry:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-384(a6)
	rts

	section	_GetAddressEntryAttrsA_stub,code

; ULONG GetAddressEntryAttrsA(APTR addr, struct TagItem * tags)
	xdef	_GetAddressEntryAttrsA
_GetAddressEntryAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-390(a6)
	rts

	section	_SetAddressEntryAttrsA_stub,code

; ULONG SetAddressEntryAttrsA(APTR addr, struct TagItem * tags)
	xdef	_SetAddressEntryAttrsA
_SetAddressEntryAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-402(a6)
	rts

	section	_MatchAddressA_stub,code

; BOOL MatchAddressA(APTR addr, struct TagItem * tags)
	xdef	_MatchAddressA
_MatchAddressA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-414(a6)
	rts

	section	_FindAddressEntryA_stub,code

; APTR FindAddressEntryA(APTR server, struct TagItem * tags)
	xdef	_FindAddressEntryA
_FindAddressEntryA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-426(a6)
	rts

	section	_HuntAddressEntryA_stub,code

; APTR HuntAddressEntryA(APTR server, struct TagItem * tags)
	xdef	_HuntAddressEntryA
_HuntAddressEntryA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-438(a6)
	rts

	section	_ScanAddressIndex_stub,code

; ULONG ScanAddressIndex(APTR server, struct Hook * hook, ULONG type, ULONG flags)
	xdef	_ScanAddressIndex
_ScanAddressIndex:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_AmlBase,a6
	jsr	-450(a6)
	rts

	section	_AddCustomField_stub,code

; BOOL AddCustomField(APTR addr, STRPTR field, STRPTR data)
	xdef	_AddCustomField
_AddCustomField:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_AmlBase,a6
	jsr	-456(a6)
	rts

	section	_RemCustomField_stub,code

; BOOL RemCustomField(APTR addr, STRPTR field)
	xdef	_RemCustomField
_RemCustomField:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-462(a6)
	rts

	section	_GetCustomFieldData_stub,code

; STRPTR GetCustomFieldData(APTR addr, STRPTR field)
	xdef	_GetCustomFieldData
_GetCustomFieldData:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-468(a6)
	rts

	section	_CreateDecoderA_stub,code

; APTR CreateDecoderA(struct TagItem * tags)
	xdef	_CreateDecoderA
_CreateDecoderA:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-474(a6)
	rts

	section	_DisposeDecoder_stub,code

; VOID DisposeDecoder(APTR dec)
	xdef	_DisposeDecoder
_DisposeDecoder:
	movea.l	4(sp),a0
	movea.l	_AmlBase,a6
	jsr	-486(a6)
	rts

	section	_GetDecoderAttrsA_stub,code

; ULONG GetDecoderAttrsA(APTR dec, struct TagItem * tags)
	xdef	_GetDecoderAttrsA
_GetDecoderAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-492(a6)
	rts

	section	_SetDecoderAttrsA_stub,code

; ULONG SetDecoderAttrsA(APTR dec, struct TagItem * tags)
	xdef	_SetDecoderAttrsA
_SetDecoderAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmlBase,a6
	jsr	-504(a6)
	rts

	section	_Decode_stub,code

; LONG Decode(APTR dec, ULONG type)
	xdef	_Decode
_Decode:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmlBase,a6
	jsr	-516(a6)
	rts

	section	_Encode_stub,code

; LONG Encode(APTR dec, ULONG type)
	xdef	_Encode
_Encode:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmlBase,a6
	jsr	-522(a6)
	rts

