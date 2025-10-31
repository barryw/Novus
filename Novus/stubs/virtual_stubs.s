; Generated from SFD file by Novus SFD Parser
; Library: virtual.library
; Base: _VirtualBase
; Each function is in its own section for dead code elimination

	xref	_VirtualBase

	section	_VIRTUAL_GetClass_stub,code

; Class * VIRTUAL_GetClass()
	xdef	_VIRTUAL_GetClass
_VIRTUAL_GetClass:
	movea.l	_VirtualBase,a6
	jsr	-30(a6)
	rts

	section	_RefreshVirtualGadget_stub,code

; VOID RefreshVirtualGadget(struct Gadget * gadget, Object * obj, struct Window * window, struct Requester * requester)
	xdef	_RefreshVirtualGadget
_RefreshVirtualGadget:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_VirtualBase,a6
	jsr	-36(a6)
	rts

	section	_RethinkVirtualSize_stub,code

; BOOL RethinkVirtualSize(Object * virt_obj, Object * rootlayout, struct TextFont * font, struct Screen * screen, struct LayoutLimits * layoutlimits)
	xdef	_RethinkVirtualSize
_RethinkVirtualSize:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	move.l	20(sp),d0
	movea.l	_VirtualBase,a6
	jsr	-42(a6)
	rts

