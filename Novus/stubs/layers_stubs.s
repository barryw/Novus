; layers library stubs for Novus
; Auto-generated from layers_lib.fd

	xref	_LayersBase	; Provided by startup.o + -lamiga

	section	text,code

; InitLayers(li)
	xdef	_InitLayers
_InitLayers:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	_LayersBase,a6
	jsr	-30(a6)	; InitLayers()
	movem.l	(sp)+,a0/a6
	rts

; CreateUpfrontLayer(li, bm, x0, y0, x1, y1, flags, bm2)
	xdef	_CreateUpfrontLayer
_CreateUpfrontLayer:
	movem.l	d0-d4/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; li
	move.l	20(sp),a1	; bm
	move.l	24(sp),d0	; x0
	move.l	28(sp),d1	; y0
	move.l	32(sp),d2	; x1
	move.l	36(sp),d3	; y1
	move.l	40(sp),d4	; flags
	move.l	44(sp),a2	; bm2
	move.l	_LayersBase,a6
	jsr	-36(a6)	; CreateUpfrontLayer()
	movem.l	(sp)+,d0-d4/a0-a2/a6
	rts

; CreateBehindLayer(li, bm, x0, y0, x1, y1, flags, bm2)
	xdef	_CreateBehindLayer
_CreateBehindLayer:
	movem.l	d0-d4/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; li
	move.l	20(sp),a1	; bm
	move.l	24(sp),d0	; x0
	move.l	28(sp),d1	; y0
	move.l	32(sp),d2	; x1
	move.l	36(sp),d3	; y1
	move.l	40(sp),d4	; flags
	move.l	44(sp),a2	; bm2
	move.l	_LayersBase,a6
	jsr	-42(a6)	; CreateBehindLayer()
	movem.l	(sp)+,d0-d4/a0-a2/a6
	rts

; UpfrontLayer(dummy, layer)
	xdef	_UpfrontLayer
_UpfrontLayer:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; dummy
	move.l	16(sp),a1	; layer
	move.l	_LayersBase,a6
	jsr	-48(a6)	; UpfrontLayer()
	movem.l	(sp)+,a0-a1/a6
	rts

; BehindLayer(dummy, layer)
	xdef	_BehindLayer
_BehindLayer:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; dummy
	move.l	16(sp),a1	; layer
	move.l	_LayersBase,a6
	jsr	-54(a6)	; BehindLayer()
	movem.l	(sp)+,a0-a1/a6
	rts

; MoveLayer(dummy, layer, dx, dy)
	xdef	_MoveLayer
_MoveLayer:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; dummy
	move.l	20(sp),a1	; layer
	move.l	24(sp),d0	; dx
	move.l	28(sp),d1	; dy
	move.l	_LayersBase,a6
	jsr	-60(a6)	; MoveLayer()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; SizeLayer(dummy, layer, dx, dy)
	xdef	_SizeLayer
_SizeLayer:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; dummy
	move.l	20(sp),a1	; layer
	move.l	24(sp),d0	; dx
	move.l	28(sp),d1	; dy
	move.l	_LayersBase,a6
	jsr	-66(a6)	; SizeLayer()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; ScrollLayer(dummy, layer, dx, dy)
	xdef	_ScrollLayer
_ScrollLayer:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; dummy
	move.l	20(sp),a1	; layer
	move.l	24(sp),d0	; dx
	move.l	28(sp),d1	; dy
	move.l	_LayersBase,a6
	jsr	-72(a6)	; ScrollLayer()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; BeginUpdate(l)
	xdef	_BeginUpdate
_BeginUpdate:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; l
	move.l	_LayersBase,a6
	jsr	-78(a6)	; BeginUpdate()
	movem.l	(sp)+,a0/a6
	rts

; EndUpdate(layer, flag)
	xdef	_EndUpdate
_EndUpdate:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; layer
	move.l	20(sp),d0	; flag
	move.l	_LayersBase,a6
	jsr	-84(a6)	; EndUpdate()
	movem.l	(sp)+,d0/a0/a6
	rts

; DeleteLayer(dummy, layer)
	xdef	_DeleteLayer
_DeleteLayer:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; dummy
	move.l	16(sp),a1	; layer
	move.l	_LayersBase,a6
	jsr	-90(a6)	; DeleteLayer()
	movem.l	(sp)+,a0-a1/a6
	rts

; LockLayer(dummy, layer)
	xdef	_LockLayer
_LockLayer:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; dummy
	move.l	16(sp),a1	; layer
	move.l	_LayersBase,a6
	jsr	-96(a6)	; LockLayer()
	movem.l	(sp)+,a0-a1/a6
	rts

; UnlockLayer(layer)
	xdef	_UnlockLayer
_UnlockLayer:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; layer
	move.l	_LayersBase,a6
	jsr	-102(a6)	; UnlockLayer()
	movem.l	(sp)+,a0/a6
	rts

; LockLayers(li)
	xdef	_LockLayers
_LockLayers:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	_LayersBase,a6
	jsr	-108(a6)	; LockLayers()
	movem.l	(sp)+,a0/a6
	rts

; UnlockLayers(li)
	xdef	_UnlockLayers
_UnlockLayers:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	_LayersBase,a6
	jsr	-114(a6)	; UnlockLayers()
	movem.l	(sp)+,a0/a6
	rts

; LockLayerInfo(li)
	xdef	_LockLayerInfo
_LockLayerInfo:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	_LayersBase,a6
	jsr	-120(a6)	; LockLayerInfo()
	movem.l	(sp)+,a0/a6
	rts

; SwapBitsRastPortClipRect(rp, cr)
	xdef	_SwapBitsRastPortClipRect
_SwapBitsRastPortClipRect:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	16(sp),a1	; cr
	move.l	_LayersBase,a6
	jsr	-126(a6)	; SwapBitsRastPortClipRect()
	movem.l	(sp)+,a0-a1/a6
	rts

; WhichLayer(li, x, y)
	xdef	_WhichLayer
_WhichLayer:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; li
	move.l	20(sp),d0	; x
	move.l	24(sp),d1	; y
	move.l	_LayersBase,a6
	jsr	-132(a6)	; WhichLayer()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; UnlockLayerInfo(li)
	xdef	_UnlockLayerInfo
_UnlockLayerInfo:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	_LayersBase,a6
	jsr	-138(a6)	; UnlockLayerInfo()
	movem.l	(sp)+,a0/a6
	rts

; NewLayerInfo()
	xdef	_NewLayerInfo
_NewLayerInfo:
	movem.l	a6,-(sp)
	move.l	_LayersBase,a6
	jsr	-144(a6)	; NewLayerInfo()
	movem.l	(sp)+,a6
	rts

; DisposeLayerInfo(li)
	xdef	_DisposeLayerInfo
_DisposeLayerInfo:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	_LayersBase,a6
	jsr	-150(a6)	; DisposeLayerInfo()
	movem.l	(sp)+,a0/a6
	rts

; FattenLayerInfo(li)
	xdef	_FattenLayerInfo
_FattenLayerInfo:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	_LayersBase,a6
	jsr	-156(a6)	; FattenLayerInfo()
	movem.l	(sp)+,a0/a6
	rts

; ThinLayerInfo(li)
	xdef	_ThinLayerInfo
_ThinLayerInfo:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	_LayersBase,a6
	jsr	-162(a6)	; ThinLayerInfo()
	movem.l	(sp)+,a0/a6
	rts

; MoveLayerInFrontOf(layer_to_move, other_layer)
	xdef	_MoveLayerInFrontOf
_MoveLayerInFrontOf:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; layer_to_move
	move.l	16(sp),a1	; other_layer
	move.l	_LayersBase,a6
	jsr	-168(a6)	; MoveLayerInFrontOf()
	movem.l	(sp)+,a0-a1/a6
	rts

; InstallClipRegion(layer, region)
	xdef	_InstallClipRegion
_InstallClipRegion:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; layer
	move.l	16(sp),a1	; region
	move.l	_LayersBase,a6
	jsr	-174(a6)	; InstallClipRegion()
	movem.l	(sp)+,a0-a1/a6
	rts

; MoveSizeLayer(layer, dx, dy, dw, dh)
	xdef	_MoveSizeLayer
_MoveSizeLayer:
	movem.l	d0-d3/a0/a6,-(sp)
	move.l	16(sp),a0	; layer
	move.l	20(sp),d0	; dx
	move.l	24(sp),d1	; dy
	move.l	28(sp),d2	; dw
	move.l	32(sp),d3	; dh
	move.l	_LayersBase,a6
	jsr	-180(a6)	; MoveSizeLayer()
	movem.l	(sp)+,d0-d3/a0/a6
	rts

; CreateUpfrontHookLayer(li, bm, x0, y0, x1, y1, flags, hook, bm2)
	xdef	_CreateUpfrontHookLayer
_CreateUpfrontHookLayer:
	movem.l	d0-d4/a0-a3/a6,-(sp)
	move.l	16(sp),a0	; li
	move.l	20(sp),a1	; bm
	move.l	24(sp),d0	; x0
	move.l	28(sp),d1	; y0
	move.l	32(sp),d2	; x1
	move.l	36(sp),d3	; y1
	move.l	40(sp),d4	; flags
	move.l	44(sp),a3	; hook
	move.l	48(sp),a2	; bm2
	move.l	_LayersBase,a6
	jsr	-186(a6)	; CreateUpfrontHookLayer()
	movem.l	(sp)+,d0-d4/a0-a3/a6
	rts

; CreateBehindHookLayer(li, bm, x0, y0, x1, y1, flags, hook, bm2)
	xdef	_CreateBehindHookLayer
_CreateBehindHookLayer:
	movem.l	d0-d4/a0-a3/a6,-(sp)
	move.l	16(sp),a0	; li
	move.l	20(sp),a1	; bm
	move.l	24(sp),d0	; x0
	move.l	28(sp),d1	; y0
	move.l	32(sp),d2	; x1
	move.l	36(sp),d3	; y1
	move.l	40(sp),d4	; flags
	move.l	44(sp),a3	; hook
	move.l	48(sp),a2	; bm2
	move.l	_LayersBase,a6
	jsr	-192(a6)	; CreateBehindHookLayer()
	movem.l	(sp)+,d0-d4/a0-a3/a6
	rts

; InstallLayerHook(layer, hook)
	xdef	_InstallLayerHook
_InstallLayerHook:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; layer
	move.l	16(sp),a1	; hook
	move.l	_LayersBase,a6
	jsr	-198(a6)	; InstallLayerHook()
	movem.l	(sp)+,a0-a1/a6
	rts

; InstallLayerInfoHook(li, hook)
	xdef	_InstallLayerInfoHook
_InstallLayerInfoHook:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; li
	move.l	16(sp),a1	; hook
	move.l	_LayersBase,a6
	jsr	-204(a6)	; InstallLayerInfoHook()
	movem.l	(sp)+,a0-a1/a6
	rts

; SortLayerCR(layer, dx, dy)
	xdef	_SortLayerCR
_SortLayerCR:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; layer
	move.l	20(sp),d0	; dx
	move.l	24(sp),d1	; dy
	move.l	_LayersBase,a6
	jsr	-210(a6)	; SortLayerCR()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; DoHookClipRects(hook, rport, rect)
	xdef	_DoHookClipRects
_DoHookClipRects:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; hook
	move.l	16(sp),a1	; rport
	move.l	20(sp),a2	; rect
	move.l	_LayersBase,a6
	jsr	-216(a6)	; DoHookClipRects()
	movem.l	(sp)+,a0-a2/a6
	rts

