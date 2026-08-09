; Generated from SFD file by Novus SFD Parser
; Library: aml.library
; Base: _AmlBase
; Each function is in its own section for dead code elimination

	xref	_AmlBase

	section	_RexxDispatcher_stub,code

; LONG RexxDispatcher(struct RexxMsg * rxm)
	xdef	_RexxDispatcher
_RexxDispatcher:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateServerA_stub,code

; APTR CreateServerA(struct TagItem * tags)
	xdef	_CreateServerA
_CreateServerA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateServer_stub,code

; APTR CreateServer(Tag tags, ... )
	xdef	_CreateServer
_CreateServer:
	movem.l	a6,-(sp)
	lea	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeServer_stub,code

; VOID DisposeServer(APTR server)
	xdef	_DisposeServer
_DisposeServer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetServerAttrsA_stub,code

; ULONG SetServerAttrsA(APTR server, struct TagItem * tags)
	xdef	_SetServerAttrsA
_SetServerAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetServerAttrs_stub,code

; ULONG SetServerAttrs(APTR server, ... )
	xdef	_SetServerAttrs
_SetServerAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetServerAttrsA_stub,code

; ULONG GetServerAttrsA(APTR server, struct TagItem * tags)
	xdef	_GetServerAttrsA
_GetServerAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetServerAttrs_stub,code

; ULONG GetServerAttrs(APTR server, ... )
	xdef	_GetServerAttrs
_GetServerAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetServerHeaders_stub,code

; ULONG GetServerHeaders(APTR server, ULONG flags)
	xdef	_GetServerHeaders
_GetServerHeaders:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetServerArticles_stub,code

; LONG GetServerArticles(APTR server, APTR folder, struct Hook * hook, ULONG flags)
	xdef	_GetServerArticles
_GetServerArticles:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	move.l	24(sp),d0
	movea.l	_AmlBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_CreateFolderA_stub,code

; APTR CreateFolderA(APTR server, struct TagItem * tags)
	xdef	_CreateFolderA
_CreateFolderA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateFolder_stub,code

; APTR CreateFolder(APTR server, ... )
	xdef	_CreateFolder
_CreateFolder:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeFolder_stub,code

; BOOL DisposeFolder(APTR folder)
	xdef	_DisposeFolder
_DisposeFolder:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenFolderA_stub,code

; APTR OpenFolderA(APTR server, struct TagItem * tags)
	xdef	_OpenFolderA
_OpenFolderA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenFolder_stub,code

; APTR OpenFolder(APTR server, ... )
	xdef	_OpenFolder
_OpenFolder:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_SaveFolder_stub,code

; BOOL SaveFolder(APTR folder)
	xdef	_SaveFolder
_SaveFolder:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemFolder_stub,code

; BOOL RemFolder(APTR folder)
	xdef	_RemFolder
_RemFolder:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetFolderAttrsA_stub,code

; ULONG SetFolderAttrsA(APTR folder, struct TagItem * tags)
	xdef	_SetFolderAttrsA
_SetFolderAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetFolderAttrs_stub,code

; ULONG SetFolderAttrs(APTR folder, ... )
	xdef	_SetFolderAttrs
_SetFolderAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetFolderAttrsA_stub,code

; ULONG GetFolderAttrsA(APTR folder, struct TagItem * tags)
	xdef	_GetFolderAttrsA
_GetFolderAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetFolderAttrs_stub,code

; ULONG GetFolderAttrs(APTR folder, ... )
	xdef	_GetFolderAttrs
_GetFolderAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddFolderArticle_stub,code

; BOOL AddFolderArticle(APTR folder, ULONG type, APTR data)
	xdef	_AddFolderArticle
_AddFolderArticle:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	16(sp),a1
	movea.l	_AmlBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemFolderArticle_stub,code

; BOOL RemFolderArticle(APTR folder, APTR article)
	xdef	_RemFolderArticle
_RemFolderArticle:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadFolderSpool_stub,code

; ULONG ReadFolderSpool(APTR folder, STRPTR importfile, ULONG flags)
	xdef	_ReadFolderSpool
_ReadFolderSpool:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmlBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteFolderSpool_stub,code

; ULONG WriteFolderSpool(APTR folder, STRPTR exportfile, ULONG flags)
	xdef	_WriteFolderSpool
_WriteFolderSpool:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmlBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_ScanFolderIndex_stub,code

; ULONG ScanFolderIndex(APTR folder, struct Hook * hook, ULONG flags)
	xdef	_ScanFolderIndex
_ScanFolderIndex:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_AmlBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_ExpungeFolder_stub,code

; BOOL ExpungeFolder(APTR folder, APTR trash, struct Hook * hook)
	xdef	_ExpungeFolder
_ExpungeFolder:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_AmlBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_CreateFolderIndex_stub,code

; ULONG CreateFolderIndex(APTR folder)
	xdef	_CreateFolderIndex
_CreateFolderIndex:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

	section	_SortFolderIndex_stub,code

; ULONG SortFolderIndex(APTR folder, ULONG field)
	xdef	_SortFolderIndex
_SortFolderIndex:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateArticleA_stub,code

; APTR CreateArticleA(APTR folder, struct TagItem * tags)
	xdef	_CreateArticleA
_CreateArticleA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateArticle_stub,code

; APTR CreateArticle(APTR folder, ... )
	xdef	_CreateArticle
_CreateArticle:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeArticle_stub,code

; BOOL DisposeArticle(APTR article)
	xdef	_DisposeArticle
_DisposeArticle:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-168(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenArticle_stub,code

; APTR OpenArticle(APTR server, APTR folder, ULONG MsgID, ULONG Flags)
	xdef	_OpenArticle
_OpenArticle:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_AmlBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a6
	rts

	section	_CopyArticle_stub,code

; BOOL CopyArticle(APTR folder, APTR article)
	xdef	_CopyArticle
_CopyArticle:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-180(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetArticleAttrsA_stub,code

; ULONG SetArticleAttrsA(APTR article, struct TagItem * tags)
	xdef	_SetArticleAttrsA
_SetArticleAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-186(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetArticleAttrs_stub,code

; ULONG SetArticleAttrs(APTR article, ... )
	xdef	_SetArticleAttrs
_SetArticleAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-186(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetArticleAttrsA_stub,code

; ULONG GetArticleAttrsA(APTR article, struct TagItem * tags)
	xdef	_GetArticleAttrsA
_GetArticleAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-192(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetArticleAttrs_stub,code

; ULONG GetArticleAttrs(APTR article, ... )
	xdef	_GetArticleAttrs
_GetArticleAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-192(a6)
	movem.l	(sp)+,a6
	rts

	section	_SendArticle_stub,code

; BOOL SendArticle(APTR server, APTR article, UBYTE * from_file)
	xdef	_SendArticle
_SendArticle:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_AmlBase,a6
	jsr	-198(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_AddArticlePartA_stub,code

; BOOL AddArticlePartA(APTR article, APTR part, struct TagItem * tags)
	xdef	_AddArticlePartA
_AddArticlePartA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_AmlBase,a6
	jsr	-204(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_AddArticlePart_stub,code

; BOOL AddArticlePart(APTR article, APTR part, ... )
	xdef	_AddArticlePart
_AddArticlePart:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	lea	20(sp),a2
	movea.l	_AmlBase,a6
	jsr	-204(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_RemArticlePart_stub,code

; VOID RemArticlePart(APTR article, APTR part)
	xdef	_RemArticlePart
_RemArticlePart:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-210(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetArticlePart_stub,code

; APTR GetArticlePart(APTR article, ULONG partnum)
	xdef	_GetArticlePart
_GetArticlePart:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-216(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetArticlePartAttrsA_stub,code

; ULONG GetArticlePartAttrsA(APTR part, struct TagItem * tags)
	xdef	_GetArticlePartAttrsA
_GetArticlePartAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-222(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetArticlePartAttrs_stub,code

; ULONG GetArticlePartAttrs(APTR part, ... )
	xdef	_GetArticlePartAttrs
_GetArticlePartAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-222(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetArticlePartAttrsA_stub,code

; ULONG SetArticlePartAttrsA(APTR part, struct TagItem * tags)
	xdef	_SetArticlePartAttrsA
_SetArticlePartAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-228(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetArticlePartAttrs_stub,code

; ULONG SetArticlePartAttrs(APTR part, ... )
	xdef	_SetArticlePartAttrs
_SetArticlePartAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-228(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateArticlePartA_stub,code

; APTR CreateArticlePartA(APTR article, struct TagItem * tags)
	xdef	_CreateArticlePartA
_CreateArticlePartA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-234(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateArticlePart_stub,code

; APTR CreateArticlePart(APTR article, ... )
	xdef	_CreateArticlePart
_CreateArticlePart:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-234(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeArticlePart_stub,code

; VOID DisposeArticlePart(APTR part)
	xdef	_DisposeArticlePart
_DisposeArticlePart:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetArticlePartDataA_stub,code

; BOOL GetArticlePartDataA(APTR article, APTR part, struct TagItem * tags)
	xdef	_GetArticlePartDataA
_GetArticlePartDataA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_AmlBase,a6
	jsr	-246(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_GetArticlePartData_stub,code

; BOOL GetArticlePartData(APTR article, APTR part, ... )
	xdef	_GetArticlePartData
_GetArticlePartData:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	lea	20(sp),a2
	movea.l	_AmlBase,a6
	jsr	-246(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_SetArticlePartDataA_stub,code

; BOOL SetArticlePartDataA(APTR part, struct TagItem * tags)
	xdef	_SetArticlePartDataA
_SetArticlePartDataA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-252(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetArticlePartData_stub,code

; BOOL SetArticlePartData(APTR part, ... )
	xdef	_SetArticlePartData
_SetArticlePartData:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-252(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateAddressEntryA_stub,code

; APTR CreateAddressEntryA(struct TagItem * tags)
	xdef	_CreateAddressEntryA
_CreateAddressEntryA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-258(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateAddressEntry_stub,code

; APTR CreateAddressEntry(Tag tags, ... )
	xdef	_CreateAddressEntry
_CreateAddressEntry:
	movem.l	a6,-(sp)
	lea	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-258(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeAddressEntry_stub,code

; BOOL DisposeAddressEntry(APTR addr)
	xdef	_DisposeAddressEntry
_DisposeAddressEntry:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-264(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenAddressEntry_stub,code

; APTR OpenAddressEntry(APTR server, ULONG fileid)
	xdef	_OpenAddressEntry
_OpenAddressEntry:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-270(a6)
	movem.l	(sp)+,a6
	rts

	section	_SaveAddressEntry_stub,code

; LONG SaveAddressEntry(APTR server, APTR addr)
	xdef	_SaveAddressEntry
_SaveAddressEntry:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-276(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemAddressEntry_stub,code

; BOOL RemAddressEntry(APTR server, APTR addr)
	xdef	_RemAddressEntry
_RemAddressEntry:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-282(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetAddressEntryAttrsA_stub,code

; ULONG GetAddressEntryAttrsA(APTR addr, struct TagItem * tags)
	xdef	_GetAddressEntryAttrsA
_GetAddressEntryAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-288(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetAddressEntryAttrs_stub,code

; ULONG GetAddressEntryAttrs(APTR addr, ... )
	xdef	_GetAddressEntryAttrs
_GetAddressEntryAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-288(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetAddressEntryAttrsA_stub,code

; ULONG SetAddressEntryAttrsA(APTR addr, struct TagItem * tags)
	xdef	_SetAddressEntryAttrsA
_SetAddressEntryAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-294(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetAddressEntryAttrs_stub,code

; ULONG SetAddressEntryAttrs(APTR addr, ... )
	xdef	_SetAddressEntryAttrs
_SetAddressEntryAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-294(a6)
	movem.l	(sp)+,a6
	rts

	section	_MatchAddressA_stub,code

; BOOL MatchAddressA(APTR addr, struct TagItem * tags)
	xdef	_MatchAddressA
_MatchAddressA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-300(a6)
	movem.l	(sp)+,a6
	rts

	section	_MatchAddress_stub,code

; BOOL MatchAddress(APTR addr, ... )
	xdef	_MatchAddress
_MatchAddress:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-300(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindAddressEntryA_stub,code

; APTR FindAddressEntryA(APTR server, struct TagItem * tags)
	xdef	_FindAddressEntryA
_FindAddressEntryA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-306(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindAddressEntry_stub,code

; APTR FindAddressEntry(APTR server, ... )
	xdef	_FindAddressEntry
_FindAddressEntry:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-306(a6)
	movem.l	(sp)+,a6
	rts

	section	_HuntAddressEntryA_stub,code

; APTR HuntAddressEntryA(APTR server, struct TagItem * tags)
	xdef	_HuntAddressEntryA
_HuntAddressEntryA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-312(a6)
	movem.l	(sp)+,a6
	rts

	section	_HuntAddressEntry_stub,code

; APTR HuntAddressEntry(APTR server, ... )
	xdef	_HuntAddressEntry
_HuntAddressEntry:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-312(a6)
	movem.l	(sp)+,a6
	rts

	section	_ScanAddressIndex_stub,code

; ULONG ScanAddressIndex(APTR server, struct Hook * hook, ULONG type, ULONG flags)
	xdef	_ScanAddressIndex
_ScanAddressIndex:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_AmlBase,a6
	jsr	-318(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddCustomField_stub,code

; BOOL AddCustomField(APTR addr, STRPTR field, STRPTR data)
	xdef	_AddCustomField
_AddCustomField:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_AmlBase,a6
	jsr	-324(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_RemCustomField_stub,code

; BOOL RemCustomField(APTR addr, STRPTR field)
	xdef	_RemCustomField
_RemCustomField:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-330(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetCustomFieldData_stub,code

; STRPTR GetCustomFieldData(APTR addr, STRPTR field)
	xdef	_GetCustomFieldData
_GetCustomFieldData:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-336(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateDecoderA_stub,code

; APTR CreateDecoderA(struct TagItem * tags)
	xdef	_CreateDecoderA
_CreateDecoderA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-342(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateDecoder_stub,code

; APTR CreateDecoder(Tag tags, ... )
	xdef	_CreateDecoder
_CreateDecoder:
	movem.l	a6,-(sp)
	lea	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-342(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeDecoder_stub,code

; VOID DisposeDecoder(APTR dec)
	xdef	_DisposeDecoder
_DisposeDecoder:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmlBase,a6
	jsr	-348(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetDecoderAttrsA_stub,code

; ULONG GetDecoderAttrsA(APTR dec, struct TagItem * tags)
	xdef	_GetDecoderAttrsA
_GetDecoderAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-354(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetDecoderAttrs_stub,code

; ULONG GetDecoderAttrs(APTR dec, ... )
	xdef	_GetDecoderAttrs
_GetDecoderAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-354(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetDecoderAttrsA_stub,code

; ULONG SetDecoderAttrsA(APTR dec, struct TagItem * tags)
	xdef	_SetDecoderAttrsA
_SetDecoderAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-360(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetDecoderAttrs_stub,code

; ULONG SetDecoderAttrs(APTR dec, ... )
	xdef	_SetDecoderAttrs
_SetDecoderAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmlBase,a6
	jsr	-360(a6)
	movem.l	(sp)+,a6
	rts

	section	_Decode_stub,code

; LONG Decode(APTR dec, ULONG type)
	xdef	_Decode
_Decode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-366(a6)
	movem.l	(sp)+,a6
	rts

	section	_Encode_stub,code

; LONG Encode(APTR dec, ULONG type)
	xdef	_Encode
_Encode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmlBase,a6
	jsr	-372(a6)
	movem.l	(sp)+,a6
	rts

