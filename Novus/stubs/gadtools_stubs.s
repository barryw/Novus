; gadtools library stubs for Novus
; Auto-generated from gadtools_lib.fd

	xref	_GadToolsBase	; Provided by startup.o + -lamiga

	section	"CODE",code

; CreateGadgetA(kind, gad, ng, taglist)
	xdef	_CreateGadgetA
_CreateGadgetA:
	movem.l	d0/a0-a2/a6,-(sp)
	move.l	16(sp),d0	; kind
	move.l	20(sp),a0	; gad
	move.l	24(sp),a1	; ng
	move.l	28(sp),a2	; taglist
	move.l	_GadToolsBase,a6
	jsr	-30(a6)	; CreateGadgetA()
	movem.l	(sp)+,d0/a0-a2/a6
	rts

; FreeGadgets(gad)
	xdef	_FreeGadgets
_FreeGadgets:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; gad
	move.l	_GadToolsBase,a6
	jsr	-36(a6)	; FreeGadgets()
	movem.l	(sp)+,a0/a6
	rts

; GT_SetGadgetAttrsA(gad, win, req, taglist)
	xdef	_GT_SetGadgetAttrsA
_GT_SetGadgetAttrsA:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; gad
	move.l	16(sp),a1	; win
	move.l	20(sp),a2	; req
	move.l	24(sp),a3	; taglist
	move.l	_GadToolsBase,a6
	jsr	-42(a6)	; GT_SetGadgetAttrsA()
	movem.l	(sp)+,a0-a3/a6
	rts

; CreateMenusA(newmenu, taglist)
	xdef	_CreateMenusA
_CreateMenusA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; newmenu
	move.l	16(sp),a1	; taglist
	move.l	_GadToolsBase,a6
	jsr	-48(a6)	; CreateMenusA()
	movem.l	(sp)+,a0-a1/a6
	rts

; FreeMenus(menu)
	xdef	_FreeMenus
_FreeMenus:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; menu
	move.l	_GadToolsBase,a6
	jsr	-54(a6)	; FreeMenus()
	movem.l	(sp)+,a0/a6
	rts

; LayoutMenuItemsA(firstitem, vi, taglist)
	xdef	_LayoutMenuItemsA
_LayoutMenuItemsA:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; firstitem
	move.l	16(sp),a1	; vi
	move.l	20(sp),a2	; taglist
	move.l	_GadToolsBase,a6
	jsr	-60(a6)	; LayoutMenuItemsA()
	movem.l	(sp)+,a0-a2/a6
	rts

; LayoutMenusA(firstmenu, vi, taglist)
	xdef	_LayoutMenusA
_LayoutMenusA:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; firstmenu
	move.l	16(sp),a1	; vi
	move.l	20(sp),a2	; taglist
	move.l	_GadToolsBase,a6
	jsr	-66(a6)	; LayoutMenusA()
	movem.l	(sp)+,a0-a2/a6
	rts

; GT_GetIMsg(iport)
	xdef	_GT_GetIMsg
_GT_GetIMsg:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iport
	move.l	_GadToolsBase,a6
	jsr	-72(a6)	; GT_GetIMsg()
	movem.l	(sp)+,a0/a6
	rts

; GT_ReplyIMsg(imsg)
	xdef	_GT_ReplyIMsg
_GT_ReplyIMsg:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; imsg
	move.l	_GadToolsBase,a6
	jsr	-78(a6)	; GT_ReplyIMsg()
	movem.l	(sp)+,a1/a6
	rts

; GT_RefreshWindow(win, req)
	xdef	_GT_RefreshWindow
_GT_RefreshWindow:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; win
	move.l	16(sp),a1	; req
	move.l	_GadToolsBase,a6
	jsr	-84(a6)	; GT_RefreshWindow()
	movem.l	(sp)+,a0-a1/a6
	rts

; GT_BeginRefresh(win)
	xdef	_GT_BeginRefresh
_GT_BeginRefresh:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; win
	move.l	_GadToolsBase,a6
	jsr	-90(a6)	; GT_BeginRefresh()
	movem.l	(sp)+,a0/a6
	rts

; GT_EndRefresh(win, complete)
	xdef	_GT_EndRefresh
_GT_EndRefresh:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; win
	move.l	20(sp),d0	; complete
	move.l	_GadToolsBase,a6
	jsr	-96(a6)	; GT_EndRefresh()
	movem.l	(sp)+,d0/a0/a6
	rts

; GT_FilterIMsg(imsg)
	xdef	_GT_FilterIMsg
_GT_FilterIMsg:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; imsg
	move.l	_GadToolsBase,a6
	jsr	-102(a6)	; GT_FilterIMsg()
	movem.l	(sp)+,a1/a6
	rts

; GT_PostFilterIMsg(imsg)
	xdef	_GT_PostFilterIMsg
_GT_PostFilterIMsg:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; imsg
	move.l	_GadToolsBase,a6
	jsr	-108(a6)	; GT_PostFilterIMsg()
	movem.l	(sp)+,a1/a6
	rts

; CreateContext(glistptr)
	xdef	_CreateContext
_CreateContext:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; glistptr
	move.l	_GadToolsBase,a6
	jsr	-114(a6)	; CreateContext()
	movem.l	(sp)+,a0/a6
	rts

; DrawBevelBoxA(rport, left, top, width, height, taglist)
	xdef	_DrawBevelBoxA
_DrawBevelBoxA:
	movem.l	d0-d3/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; rport
	move.l	20(sp),d0	; left
	move.l	24(sp),d1	; top
	move.l	28(sp),d2	; width
	move.l	32(sp),d3	; height
	move.l	36(sp),a1	; taglist
	move.l	_GadToolsBase,a6
	jsr	-120(a6)	; DrawBevelBoxA()
	movem.l	(sp)+,d0-d3/a0-a1/a6
	rts

; GetVisualInfoA(screen, taglist)
	xdef	_GetVisualInfoA
_GetVisualInfoA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	16(sp),a1	; taglist
	move.l	_GadToolsBase,a6
	jsr	-126(a6)	; GetVisualInfoA()
	movem.l	(sp)+,a0-a1/a6
	rts

; FreeVisualInfo(vi)
	xdef	_FreeVisualInfo
_FreeVisualInfo:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vi
	move.l	_GadToolsBase,a6
	jsr	-132(a6)	; FreeVisualInfo()
	movem.l	(sp)+,a0/a6
	rts

; GT_GetGadgetAttrsA(gad, win, req, taglist)
	xdef	_GT_GetGadgetAttrsA
_GT_GetGadgetAttrsA:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; gad
	move.l	16(sp),a1	; win
	move.l	20(sp),a2	; req
	move.l	24(sp),a3	; taglist
	move.l	_GadToolsBase,a6
	jsr	-174(a6)	; GT_GetGadgetAttrsA()
	movem.l	(sp)+,a0-a3/a6
	rts

