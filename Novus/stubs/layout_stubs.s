; Generated from SFD file by Novus SFD Parser
; Library: layout.library
; Base: _LayoutBase
; Each function is in its own section for dead code elimination

	xref	_LayoutBase

	section	_LAYOUT_GetClass_stub,code

; Class * LAYOUT_GetClass()
	xdef	_LAYOUT_GetClass
_LAYOUT_GetClass:
	movea.l	_LayoutBase,a6
	jsr	-30(a6)
	rts

	section	_ActivateLayoutGadget_stub,code

; BOOL ActivateLayoutGadget(struct Gadget * gadget, struct Window * window, struct Requester * requester, ULONG object)
	xdef	_ActivateLayoutGadget
_ActivateLayoutGadget:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	movea.l	_LayoutBase,a6
	jsr	-36(a6)
	rts

	section	_FlushLayoutDomainCache_stub,code

; VOID FlushLayoutDomainCache(struct Gadget * gadget)
	xdef	_FlushLayoutDomainCache
_FlushLayoutDomainCache:
	movea.l	4(sp),a0
	movea.l	_LayoutBase,a6
	jsr	-42(a6)
	rts

	section	_RethinkLayout_stub,code

; BOOL RethinkLayout(struct Gadget * gadget, struct Window * window, struct Requester * requester, BOOL refresh)
	xdef	_RethinkLayout
_RethinkLayout:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	movea.l	_LayoutBase,a6
	jsr	-48(a6)
	rts

	section	_LayoutLimits_stub,code

; VOID LayoutLimits(struct Gadget * gadget, struct LayoutLimits * limits, struct TextFont * font, struct Screen * screen)
	xdef	_LayoutLimits
_LayoutLimits:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_LayoutBase,a6
	jsr	-54(a6)
	rts

	section	_PAGE_GetClass_stub,code

; Class * PAGE_GetClass()
	xdef	_PAGE_GetClass
_PAGE_GetClass:
	movea.l	_LayoutBase,a6
	jsr	-60(a6)
	rts

	section	_SetPageGadgetAttrsA_stub,code

; ULONG SetPageGadgetAttrsA(struct Gadget * gadget, Object * object, struct Window * window, struct Requester * requester, struct TagItem * tags)
	xdef	_SetPageGadgetAttrsA
_SetPageGadgetAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	20(sp),a4
	movea.l	_LayoutBase,a6
	jsr	-66(a6)
	rts

	section	_RefreshPageGadget_stub,code

; VOID RefreshPageGadget(struct Gadget * gadget, Object * object, struct Window * window, struct Requester * requester)
	xdef	_RefreshPageGadget
_RefreshPageGadget:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_LayoutBase,a6
	jsr	-72(a6)
	rts

