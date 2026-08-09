; Generated from SFD file by Novus SFD Parser
; Library: layers.library
; Base: _LayersBase
; Each function is in its own section for dead code elimination

	xref	_LayersBase

	section	_InitLayers_stub,code

; VOID InitLayers(struct Layer_Info * li)
	xdef	_InitLayers
_InitLayers:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateUpfrontLayer_stub,code

; struct Layer * CreateUpfrontLayer(struct Layer_Info * li, struct BitMap * bm, LONG x0, LONG y0, LONG x1, LONG y1, LONG flags, struct BitMap * bm2)
	xdef	_CreateUpfrontLayer
_CreateUpfrontLayer:
	movem.l	d2/d3/d4/a2/a6,-(sp)
	movea.l	24(sp),a0
	movea.l	28(sp),a1
	move.l	32(sp),d0
	move.l	36(sp),d1
	move.l	40(sp),d2
	move.l	44(sp),d3
	move.l	48(sp),d4
	movea.l	52(sp),a2
	movea.l	_LayersBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,d2/d3/d4/a2/a6
	rts

	section	_CreateBehindLayer_stub,code

; struct Layer * CreateBehindLayer(struct Layer_Info * li, struct BitMap * bm, LONG x0, LONG y0, LONG x1, LONG y1, LONG flags, struct BitMap * bm2)
	xdef	_CreateBehindLayer
_CreateBehindLayer:
	movem.l	d2/d3/d4/a2/a6,-(sp)
	movea.l	24(sp),a0
	movea.l	28(sp),a1
	move.l	32(sp),d0
	move.l	36(sp),d1
	move.l	40(sp),d2
	move.l	44(sp),d3
	move.l	48(sp),d4
	movea.l	52(sp),a2
	movea.l	_LayersBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,d2/d3/d4/a2/a6
	rts

	section	_UpfrontLayer_stub,code

; LONG UpfrontLayer(LONG dummy, struct Layer * layer)
	xdef	_UpfrontLayer
_UpfrontLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_BehindLayer_stub,code

; LONG BehindLayer(LONG dummy, struct Layer * layer)
	xdef	_BehindLayer
_BehindLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_MoveLayer_stub,code

; LONG MoveLayer(LONG dummy, struct Layer * layer, LONG dx, LONG dy)
	xdef	_MoveLayer
_MoveLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_LayersBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_SizeLayer_stub,code

; LONG SizeLayer(LONG dummy, struct Layer * layer, LONG dx, LONG dy)
	xdef	_SizeLayer
_SizeLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_LayersBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_ScrollLayer_stub,code

; VOID ScrollLayer(LONG dummy, struct Layer * layer, LONG dx, LONG dy)
	xdef	_ScrollLayer
_ScrollLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_LayersBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_BeginUpdate_stub,code

; LONG BeginUpdate(struct Layer * l)
	xdef	_BeginUpdate
_BeginUpdate:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_EndUpdate_stub,code

; VOID EndUpdate(struct Layer * layer, UWORD flag)
	xdef	_EndUpdate
_EndUpdate:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LayersBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_DeleteLayer_stub,code

; LONG DeleteLayer(LONG dummy, struct Layer * layer)
	xdef	_DeleteLayer
_DeleteLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_LockLayer_stub,code

; VOID LockLayer(LONG dummy, struct Layer * layer)
	xdef	_LockLayer
_LockLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_UnlockLayer_stub,code

; VOID UnlockLayer(struct Layer * layer)
	xdef	_UnlockLayer
_UnlockLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_LockLayers_stub,code

; VOID LockLayers(struct Layer_Info * li)
	xdef	_LockLayers
_LockLayers:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_UnlockLayers_stub,code

; VOID UnlockLayers(struct Layer_Info * li)
	xdef	_UnlockLayers
_UnlockLayers:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_LockLayerInfo_stub,code

; VOID LockLayerInfo(struct Layer_Info * li)
	xdef	_LockLayerInfo
_LockLayerInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_SwapBitsRastPortClipRect_stub,code

; VOID SwapBitsRastPortClipRect(struct RastPort * rp, struct ClipRect * cr)
	xdef	_SwapBitsRastPortClipRect
_SwapBitsRastPortClipRect:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_WhichLayer_stub,code

; struct Layer * WhichLayer(struct Layer_Info * li, WORD x, WORD y)
	xdef	_WhichLayer
_WhichLayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_LayersBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_UnlockLayerInfo_stub,code

; VOID UnlockLayerInfo(struct Layer_Info * li)
	xdef	_UnlockLayerInfo
_UnlockLayerInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_NewLayerInfo_stub,code

; struct Layer_Info * NewLayerInfo()
	xdef	_NewLayerInfo
_NewLayerInfo:
	movem.l	a6,-(sp)
	movea.l	_LayersBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeLayerInfo_stub,code

; VOID DisposeLayerInfo(struct Layer_Info * li)
	xdef	_DisposeLayerInfo
_DisposeLayerInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

	section	_FattenLayerInfo_stub,code

; LONG FattenLayerInfo(struct Layer_Info * li)
	xdef	_FattenLayerInfo
_FattenLayerInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a6
	rts

	section	_ThinLayerInfo_stub,code

; VOID ThinLayerInfo(struct Layer_Info * li)
	xdef	_ThinLayerInfo
_ThinLayerInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LayersBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a6
	rts

	section	_MoveLayerInFrontOf_stub,code

; LONG MoveLayerInFrontOf(struct Layer * layer_to_move, struct Layer * other_layer)
	xdef	_MoveLayerInFrontOf
_MoveLayerInFrontOf:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-168(a6)
	movem.l	(sp)+,a6
	rts

	section	_InstallClipRegion_stub,code

; struct Region * InstallClipRegion(struct Layer * layer, const struct Region * region)
	xdef	_InstallClipRegion
_InstallClipRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a6
	rts

	section	_MoveSizeLayer_stub,code

; LONG MoveSizeLayer(struct Layer * layer, LONG dx, LONG dy, LONG dw, LONG dh)
	xdef	_MoveSizeLayer
_MoveSizeLayer:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_LayersBase,a6
	jsr	-180(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_CreateUpfrontHookLayer_stub,code

; struct Layer * CreateUpfrontHookLayer(struct Layer_Info * li, struct BitMap * bm, LONG x0, LONG y0, LONG x1, LONG y1, LONG flags, struct Hook * hook, struct BitMap * bm2)
	xdef	_CreateUpfrontHookLayer
_CreateUpfrontHookLayer:
	movem.l	d2/d3/d4/a2/a3/a6,-(sp)
	movea.l	28(sp),a0
	movea.l	32(sp),a1
	move.l	36(sp),d0
	move.l	40(sp),d1
	move.l	44(sp),d2
	move.l	48(sp),d3
	move.l	52(sp),d4
	movea.l	56(sp),a3
	movea.l	60(sp),a2
	movea.l	_LayersBase,a6
	jsr	-186(a6)
	movem.l	(sp)+,d2/d3/d4/a2/a3/a6
	rts

	section	_CreateBehindHookLayer_stub,code

; struct Layer * CreateBehindHookLayer(struct Layer_Info * li, struct BitMap * bm, LONG x0, LONG y0, LONG x1, LONG y1, LONG flags, struct Hook * hook, struct BitMap * bm2)
	xdef	_CreateBehindHookLayer
_CreateBehindHookLayer:
	movem.l	d2/d3/d4/a2/a3/a6,-(sp)
	movea.l	28(sp),a0
	movea.l	32(sp),a1
	move.l	36(sp),d0
	move.l	40(sp),d1
	move.l	44(sp),d2
	move.l	48(sp),d3
	move.l	52(sp),d4
	movea.l	56(sp),a3
	movea.l	60(sp),a2
	movea.l	_LayersBase,a6
	jsr	-192(a6)
	movem.l	(sp)+,d2/d3/d4/a2/a3/a6
	rts

	section	_InstallLayerHook_stub,code

; struct Hook * InstallLayerHook(struct Layer * layer, struct Hook * hook)
	xdef	_InstallLayerHook
_InstallLayerHook:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-198(a6)
	movem.l	(sp)+,a6
	rts

	section	_InstallLayerInfoHook_stub,code

; struct Hook * InstallLayerInfoHook(struct Layer_Info * li, const struct Hook * hook)
	xdef	_InstallLayerInfoHook
_InstallLayerInfoHook:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LayersBase,a6
	jsr	-204(a6)
	movem.l	(sp)+,a6
	rts

	section	_SortLayerCR_stub,code

; VOID SortLayerCR(struct Layer * layer, WORD dx, WORD dy)
	xdef	_SortLayerCR
_SortLayerCR:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_LayersBase,a6
	jsr	-210(a6)
	movem.l	(sp)+,a6
	rts

	section	_DoHookClipRects_stub,code

; VOID DoHookClipRects(struct Hook * hook, struct RastPort * rport, const struct Rectangle * rect)
	xdef	_DoHookClipRects
_DoHookClipRects:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_LayersBase,a6
	jsr	-216(a6)
	movem.l	(sp)+,a2/a6
	rts

