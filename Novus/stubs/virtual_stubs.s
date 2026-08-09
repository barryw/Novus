; Generated from SFD file by Novus SFD Parser
; Library: virtual.library
; Base: _VirtualBase
; Each function is in its own section for dead code elimination

	xref	_VirtualBase

	section	_VIRTUAL_GetClass_stub,code

; Class * VIRTUAL_GetClass()
	xdef	_VIRTUAL_GetClass
_VIRTUAL_GetClass:
	movem.l	a6,-(sp)
	movea.l	_VirtualBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_RefreshVirtualGadget_stub,code

; VOID RefreshVirtualGadget(struct Gadget * gadget, Object * obj, struct Window * window, struct Requester * requester)
	xdef	_RefreshVirtualGadget
_RefreshVirtualGadget:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_VirtualBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_RethinkVirtualSize_stub,code

; BOOL RethinkVirtualSize(Object * virt_obj, Object * rootlayout, struct TextFont * font, struct Screen * screen, struct LayoutLimits * layoutlimits)
	xdef	_RethinkVirtualSize
_RethinkVirtualSize:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	move.l	32(sp),d0
	movea.l	_VirtualBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

