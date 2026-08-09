; Generated from SFD file by Novus SFD Parser
; Library: layout.library
; Base: _LayoutBase
; Each function is in its own section for dead code elimination

	xref	_LayoutBase

	section	_LAYOUT_GetClass_stub,code

; Class * LAYOUT_GetClass()
	xdef	_LAYOUT_GetClass
_LAYOUT_GetClass:
	movem.l	a6,-(sp)
	movea.l	_LayoutBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_ActivateLayoutGadget_stub,code

; BOOL ActivateLayoutGadget(struct Gadget * gadget, struct Window * window, struct Requester * requester, ULONG object)
	xdef	_ActivateLayoutGadget
_ActivateLayoutGadget:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	move.l	24(sp),d0
	movea.l	_LayoutBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_FlushLayoutDomainCache_stub,code

; VOID FlushLayoutDomainCache(struct Gadget * gadget)
	xdef	_FlushLayoutDomainCache
_FlushLayoutDomainCache:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayoutBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_RethinkLayout_stub,code

; BOOL RethinkLayout(struct Gadget * gadget, struct Window * window, struct Requester * requester, BOOL refresh)
	xdef	_RethinkLayout
_RethinkLayout:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	move.l	24(sp),d0
	movea.l	_LayoutBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_LayoutLimits_stub,code

; VOID LayoutLimits(struct Gadget * gadget, struct LayoutLimits * limits, struct TextFont * font, struct Screen * screen)
	xdef	_LayoutLimits
_LayoutLimits:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_LayoutBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_PAGE_GetClass_stub,code

; Class * PAGE_GetClass()
	xdef	_PAGE_GetClass
_PAGE_GetClass:
	movem.l	a6,-(sp)
	movea.l	_LayoutBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetPageGadgetAttrsA_stub,code

; ULONG SetPageGadgetAttrsA(struct Gadget * gadget, Object * object, struct Window * window, struct Requester * requester, struct TagItem * tags)
	xdef	_SetPageGadgetAttrsA
_SetPageGadgetAttrsA:
	movem.l	a2/a3/a4/a6,-(sp)
	movea.l	20(sp),a0
	movea.l	24(sp),a1
	movea.l	28(sp),a2
	movea.l	32(sp),a3
	movea.l	36(sp),a4
	movea.l	_LayoutBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a2/a3/a4/a6
	rts

	section	_SetPageGadgetAttrs_stub,code

; ULONG SetPageGadgetAttrs(struct Gadget * gadget, Object * object, struct Window * window, struct Requester * requester, ... )
	xdef	_SetPageGadgetAttrs
_SetPageGadgetAttrs:
	movem.l	a2/a3/a4/a6,-(sp)
	movea.l	20(sp),a0
	movea.l	24(sp),a1
	movea.l	28(sp),a2
	movea.l	32(sp),a3
	lea	36(sp),a4
	movea.l	_LayoutBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a2/a3/a4/a6
	rts

	section	_RefreshPageGadget_stub,code

; VOID RefreshPageGadget(struct Gadget * gadget, Object * object, struct Window * window, struct Requester * requester)
	xdef	_RefreshPageGadget
_RefreshPageGadget:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_LayoutBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

