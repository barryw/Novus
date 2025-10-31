; Generated from SFD file by Novus SFD Parser
; Library: listbrowser.library
; Base: _ListBrowserBase
; Each function is in its own section for dead code elimination

	xref	_ListBrowserBase

	section	_LISTBROWSER_GetClass_stub,code

; struct IClass * LISTBROWSER_GetClass()
	xdef	_LISTBROWSER_GetClass
_LISTBROWSER_GetClass:
	movea.l	_ListBrowserBase,a6
	jsr	-30(a6)
	rts

	section	_AllocListBrowserNodeA_stub,code

; struct Node * AllocListBrowserNodeA(UWORD columns, struct TagItem * tags)
	xdef	_AllocListBrowserNodeA
_AllocListBrowserNodeA:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-36(a6)
	rts

	section	_FreeListBrowserNode_stub,code

; VOID FreeListBrowserNode(struct Node * node)
	xdef	_FreeListBrowserNode
_FreeListBrowserNode:
	movea.l	4(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-48(a6)
	rts

	section	_SetListBrowserNodeAttrsA_stub,code

; VOID SetListBrowserNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetListBrowserNodeAttrsA
_SetListBrowserNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ListBrowserBase,a6
	jsr	-54(a6)
	rts

	section	_GetListBrowserNodeAttrsA_stub,code

; VOID GetListBrowserNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetListBrowserNodeAttrsA
_GetListBrowserNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ListBrowserBase,a6
	jsr	-66(a6)
	rts

	section	_ListBrowserSelectAll_stub,code

; VOID ListBrowserSelectAll(struct List * list)
	xdef	_ListBrowserSelectAll
_ListBrowserSelectAll:
	movea.l	4(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-78(a6)
	rts

	section	_ShowListBrowserNodeChildren_stub,code

; VOID ShowListBrowserNodeChildren(struct Node * node, WORD depth)
	xdef	_ShowListBrowserNodeChildren
_ShowListBrowserNodeChildren:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_ListBrowserBase,a6
	jsr	-84(a6)
	rts

	section	_HideListBrowserNodeChildren_stub,code

; VOID HideListBrowserNodeChildren(struct Node * node)
	xdef	_HideListBrowserNodeChildren
_HideListBrowserNodeChildren:
	movea.l	4(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-90(a6)
	rts

	section	_ShowAllListBrowserChildren_stub,code

; VOID ShowAllListBrowserChildren(struct List * list)
	xdef	_ShowAllListBrowserChildren
_ShowAllListBrowserChildren:
	movea.l	4(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-96(a6)
	rts

	section	_HideAllListBrowserChildren_stub,code

; VOID HideAllListBrowserChildren(struct List * list)
	xdef	_HideAllListBrowserChildren
_HideAllListBrowserChildren:
	movea.l	4(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-102(a6)
	rts

	section	_FreeListBrowserList_stub,code

; VOID FreeListBrowserList(struct List * list)
	xdef	_FreeListBrowserList
_FreeListBrowserList:
	movea.l	4(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-108(a6)
	rts

	section	_AllocLBColumnInfoA_stub,code

; struct ColumnInfo * AllocLBColumnInfoA(UWORD columns, struct TagItem * tags)
	xdef	_AllocLBColumnInfoA
_AllocLBColumnInfoA:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-114(a6)
	rts

	section	_SetLBColumnInfoAttrsA_stub,code

; LONG SetLBColumnInfoAttrsA(struct ColumnInfo * columninfo, struct TagItem * tags)
	xdef	_SetLBColumnInfoAttrsA
_SetLBColumnInfoAttrsA:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-126(a6)
	rts

	section	_GetLBColumnInfoAttrsA_stub,code

; LONG GetLBColumnInfoAttrsA(struct ColumnInfo * columninfo, struct TagItem * tags)
	xdef	_GetLBColumnInfoAttrsA
_GetLBColumnInfoAttrsA:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-138(a6)
	rts

	section	_FreeLBColumnInfo_stub,code

; VOID FreeLBColumnInfo(struct ColumnInfo * columninfo)
	xdef	_FreeLBColumnInfo
_FreeLBColumnInfo:
	movea.l	4(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-150(a6)
	rts

	section	_ListBrowserClearAll_stub,code

; VOID ListBrowserClearAll(struct List * list)
	xdef	_ListBrowserClearAll
_ListBrowserClearAll:
	movea.l	4(sp),a0
	movea.l	_ListBrowserBase,a6
	jsr	-156(a6)
	rts

