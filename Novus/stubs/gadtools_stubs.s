; Generated from SFD file by Novus SFD Parser
; Library: gadtools.library
; Base: _GadToolsBase
; Each function is in its own section for dead code elimination
; NOTE: Uses lazy initialization via ___gadtools_ensure

	xref	_GadToolsBase
	xref	___gadtools_ensure	; Lazy init - opens library if needed, returns base in A6

	section	_CreateGadgetA_stub,code

; struct Gadget * CreateGadgetA(ULONG kind, struct Gadget * gad, const struct NewGadget * ng, const struct TagItem * taglist)
	xdef	_CreateGadgetA
_CreateGadgetA:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	16(sp),a2
	jsr	___gadtools_ensure
	jsr	-30(a6)
	rts

	section	_FreeGadgets_stub,code

; VOID FreeGadgets(struct Gadget * gad)
	xdef	_FreeGadgets
_FreeGadgets:
	movea.l	4(sp),a0
	jsr	___gadtools_ensure
	jsr	-42(a6)
	rts

	section	_GT_SetGadgetAttrsA_stub,code

; VOID GT_SetGadgetAttrsA(struct Gadget * gad, struct Window * win, struct Requester * req, const struct TagItem * taglist)
	xdef	_GT_SetGadgetAttrsA
_GT_SetGadgetAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	jsr	___gadtools_ensure
	jsr	-48(a6)
	rts

	section	_CreateMenusA_stub,code

; struct Menu * CreateMenusA(const struct NewMenu * newmenu, const struct TagItem * taglist)
	xdef	_CreateMenusA
_CreateMenusA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___gadtools_ensure
	jsr	-60(a6)
	rts

	section	_FreeMenus_stub,code

; VOID FreeMenus(struct Menu * menu)
	xdef	_FreeMenus
_FreeMenus:
	movea.l	4(sp),a0
	jsr	___gadtools_ensure
	jsr	-72(a6)
	rts

	section	_LayoutMenuItemsA_stub,code

; BOOL LayoutMenuItemsA(struct MenuItem * firstitem, APTR vi, const struct TagItem * taglist)
	xdef	_LayoutMenuItemsA
_LayoutMenuItemsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	jsr	___gadtools_ensure
	jsr	-78(a6)
	rts

	section	_LayoutMenusA_stub,code

; BOOL LayoutMenusA(struct Menu * firstmenu, APTR vi, const struct TagItem * taglist)
	xdef	_LayoutMenusA
_LayoutMenusA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	jsr	___gadtools_ensure
	jsr	-90(a6)
	rts

	section	_GT_GetIMsg_stub,code

; struct IntuiMessage * GT_GetIMsg(struct MsgPort * iport)
	xdef	_GT_GetIMsg
_GT_GetIMsg:
	movea.l	4(sp),a0
	jsr	___gadtools_ensure
	jsr	-102(a6)
	rts

	section	_GT_ReplyIMsg_stub,code

; VOID GT_ReplyIMsg(struct IntuiMessage * imsg)
	xdef	_GT_ReplyIMsg
_GT_ReplyIMsg:
	movea.l	4(sp),a1
	jsr	___gadtools_ensure
	jsr	-108(a6)
	rts

	section	_GT_RefreshWindow_stub,code

; VOID GT_RefreshWindow(struct Window * win, struct Requester * req)
	xdef	_GT_RefreshWindow
_GT_RefreshWindow:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___gadtools_ensure
	jsr	-114(a6)
	rts

	section	_GT_BeginRefresh_stub,code

; VOID GT_BeginRefresh(struct Window * win)
	xdef	_GT_BeginRefresh
_GT_BeginRefresh:
	movea.l	4(sp),a0
	jsr	___gadtools_ensure
	jsr	-120(a6)
	rts

	section	_GT_EndRefresh_stub,code

; VOID GT_EndRefresh(struct Window * win, BOOL complete)
	xdef	_GT_EndRefresh
_GT_EndRefresh:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___gadtools_ensure
	jsr	-126(a6)
	rts

	section	_GT_FilterIMsg_stub,code

; struct IntuiMessage * GT_FilterIMsg(const struct IntuiMessage * imsg)
	xdef	_GT_FilterIMsg
_GT_FilterIMsg:
	movea.l	4(sp),a1
	jsr	___gadtools_ensure
	jsr	-132(a6)
	rts

	section	_GT_PostFilterIMsg_stub,code

; struct IntuiMessage * GT_PostFilterIMsg(struct IntuiMessage * imsg)
	xdef	_GT_PostFilterIMsg
_GT_PostFilterIMsg:
	movea.l	4(sp),a1
	jsr	___gadtools_ensure
	jsr	-138(a6)
	rts

	section	_CreateContext_stub,code

; struct Gadget * CreateContext(struct Gadget ** glistptr)
	xdef	_CreateContext
_CreateContext:
	movea.l	4(sp),a0
	jsr	___gadtools_ensure
	jsr	-144(a6)
	rts

	section	_DrawBevelBoxA_stub,code

; VOID DrawBevelBoxA(struct RastPort * rport, WORD left, WORD top, WORD width, WORD height, const struct TagItem * taglist)
	xdef	_DrawBevelBoxA
_DrawBevelBoxA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	movea.l	24(sp),a1
	jsr	___gadtools_ensure
	jsr	-150(a6)
	rts

	section	_GetVisualInfoA_stub,code

; APTR GetVisualInfoA(struct Screen * screen, const struct TagItem * taglist)
	xdef	_GetVisualInfoA
_GetVisualInfoA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___gadtools_ensure
	jsr	-162(a6)
	rts

	section	_FreeVisualInfo_stub,code

; VOID FreeVisualInfo(APTR vi)
	xdef	_FreeVisualInfo
_FreeVisualInfo:
	movea.l	4(sp),a0
	jsr	___gadtools_ensure
	jsr	-174(a6)
	rts

	section	_GT_GetGadgetAttrsA_stub,code

; LONG GT_GetGadgetAttrsA(struct Gadget * gad, struct Window * win, struct Requester * req, const struct TagItem * taglist)
	xdef	_GT_GetGadgetAttrsA
_GT_GetGadgetAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	jsr	___gadtools_ensure
	jsr	-216(a6)
	rts

