; Generated from SFD file by Novus SFD Parser
; Library: gadtools.library
; Base: _GadToolsBase
; Each function is in its own section for dead code elimination

	xref	_GadToolsBase

	section	_CreateGadgetA_stub,code

; struct Gadget * CreateGadgetA(ULONG kind, struct Gadget * gad, const struct NewGadget * ng, const struct TagItem * taglist)
	xdef	_CreateGadgetA
_CreateGadgetA:
	movem.l	a2/a6,-(sp)
	move.l	12(sp),d0
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	_GadToolsBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_CreateGadget_stub,code

; struct Gadget * CreateGadget(ULONG kind, struct Gadget * gad, const struct NewGadget * ng, Tag taglist, ... )
	xdef	_CreateGadget
_CreateGadget:
	movem.l	a2/a6,-(sp)
	move.l	12(sp),d0
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	lea	24(sp),a2
	movea.l	_GadToolsBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_FreeGadgets_stub,code

; VOID FreeGadgets(struct Gadget * gad)
	xdef	_FreeGadgets
_FreeGadgets:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GadToolsBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_GT_SetGadgetAttrsA_stub,code

; VOID GT_SetGadgetAttrsA(struct Gadget * gad, struct Window * win, struct Requester * req, const struct TagItem * taglist)
	xdef	_GT_SetGadgetAttrsA
_GT_SetGadgetAttrsA:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_GadToolsBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_GT_SetGadgetAttrs_stub,code

; VOID GT_SetGadgetAttrs(struct Gadget * gad, struct Window * win, struct Requester * req, Tag taglist, ... )
	xdef	_GT_SetGadgetAttrs
_GT_SetGadgetAttrs:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	lea	28(sp),a3
	movea.l	_GadToolsBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_CreateMenusA_stub,code

; struct Menu * CreateMenusA(const struct NewMenu * newmenu, const struct TagItem * taglist)
	xdef	_CreateMenusA
_CreateMenusA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateMenus_stub,code

; struct Menu * CreateMenus(const struct NewMenu * newmenu, Tag taglist, ... )
	xdef	_CreateMenus
_CreateMenus:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeMenus_stub,code

; VOID FreeMenus(struct Menu * menu)
	xdef	_FreeMenus
_FreeMenus:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GadToolsBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_LayoutMenuItemsA_stub,code

; BOOL LayoutMenuItemsA(struct MenuItem * firstitem, APTR vi, const struct TagItem * taglist)
	xdef	_LayoutMenuItemsA
_LayoutMenuItemsA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_GadToolsBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_LayoutMenuItems_stub,code

; BOOL LayoutMenuItems(struct MenuItem * firstitem, APTR vi, Tag taglist, ... )
	xdef	_LayoutMenuItems
_LayoutMenuItems:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	lea	20(sp),a2
	movea.l	_GadToolsBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_LayoutMenusA_stub,code

; BOOL LayoutMenusA(struct Menu * firstmenu, APTR vi, const struct TagItem * taglist)
	xdef	_LayoutMenusA
_LayoutMenusA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_GadToolsBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_LayoutMenus_stub,code

; BOOL LayoutMenus(struct Menu * firstmenu, APTR vi, Tag taglist, ... )
	xdef	_LayoutMenus
_LayoutMenus:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	lea	20(sp),a2
	movea.l	_GadToolsBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_GT_GetIMsg_stub,code

; struct IntuiMessage * GT_GetIMsg(struct MsgPort * iport)
	xdef	_GT_GetIMsg
_GT_GetIMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GadToolsBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_GT_ReplyIMsg_stub,code

; VOID GT_ReplyIMsg(struct IntuiMessage * imsg)
	xdef	_GT_ReplyIMsg
_GT_ReplyIMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_GT_RefreshWindow_stub,code

; VOID GT_RefreshWindow(struct Window * win, struct Requester * req)
	xdef	_GT_RefreshWindow
_GT_RefreshWindow:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_GT_BeginRefresh_stub,code

; VOID GT_BeginRefresh(struct Window * win)
	xdef	_GT_BeginRefresh
_GT_BeginRefresh:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GadToolsBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_GT_EndRefresh_stub,code

; VOID GT_EndRefresh(struct Window * win, BOOL complete)
	xdef	_GT_EndRefresh
_GT_EndRefresh:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_GadToolsBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_GT_FilterIMsg_stub,code

; struct IntuiMessage * GT_FilterIMsg(const struct IntuiMessage * imsg)
	xdef	_GT_FilterIMsg
_GT_FilterIMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_GT_PostFilterIMsg_stub,code

; struct IntuiMessage * GT_PostFilterIMsg(struct IntuiMessage * imsg)
	xdef	_GT_PostFilterIMsg
_GT_PostFilterIMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateContext_stub,code

; struct Gadget * CreateContext(struct Gadget ** glistptr)
	xdef	_CreateContext
_CreateContext:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GadToolsBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_DrawBevelBoxA_stub,code

; VOID DrawBevelBoxA(struct RastPort * rport, WORD left, WORD top, WORD width, WORD height, const struct TagItem * taglist)
	xdef	_DrawBevelBoxA
_DrawBevelBoxA:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	36(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_DrawBevelBox_stub,code

; VOID DrawBevelBox(struct RastPort * rport, WORD left, WORD top, WORD width, WORD height, Tag taglist, ... )
	xdef	_DrawBevelBox
_DrawBevelBox:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	lea	36(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_GetVisualInfoA_stub,code

; APTR GetVisualInfoA(struct Screen * screen, const struct TagItem * taglist)
	xdef	_GetVisualInfoA
_GetVisualInfoA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetVisualInfo_stub,code

; APTR GetVisualInfo(struct Screen * screen, Tag taglist, ... )
	xdef	_GetVisualInfo
_GetVisualInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_GadToolsBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeVisualInfo_stub,code

; VOID FreeVisualInfo(APTR vi)
	xdef	_FreeVisualInfo
_FreeVisualInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GadToolsBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_GT_GetGadgetAttrsA_stub,code

; LONG GT_GetGadgetAttrsA(struct Gadget * gad, struct Window * win, struct Requester * req, const struct TagItem * taglist)
	xdef	_GT_GetGadgetAttrsA
_GT_GetGadgetAttrsA:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_GadToolsBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_GT_GetGadgetAttrs_stub,code

; LONG GT_GetGadgetAttrs(struct Gadget * gad, struct Window * win, struct Requester * req, Tag taglist, ... )
	xdef	_GT_GetGadgetAttrs
_GT_GetGadgetAttrs:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	lea	28(sp),a3
	movea.l	_GadToolsBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

