; Generated from SFD file by Novus SFD Parser
; Library: graphics.library
; Base: _GfxBase
; Each function is in its own section for dead code elimination
; NOTE: Uses lazy initialization via ___graphics_ensure

	xref	_GfxBase
	xref	___graphics_ensure	; Lazy init - opens library if needed, returns base in A6

	section	_BltBitMap_stub,code

; LONG BltBitMap(const struct BitMap * srcBitMap, WORD xSrc, WORD ySrc, struct BitMap * destBitMap, WORD xDest, WORD yDest, WORD xSize, WORD ySize, UBYTE minterm, UBYTE mask, PLANEPTR tempA)
	xdef	_BltBitMap
_BltBitMap:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	move.l	20(sp),d2
	move.l	24(sp),d3
	move.l	28(sp),d4
	move.l	32(sp),d5
	move.l	36(sp),d6
	move.l	40(sp),d7
	movea.l	44(sp),a2
	jsr	___graphics_ensure
	jsr	-30(a6)
	rts

	section	_BltTemplate_stub,code

; VOID BltTemplate(const PLANEPTR source, WORD xSrc, WORD srcMod, struct RastPort * destRP, WORD xDest, WORD yDest, WORD xSize, WORD ySize)
	xdef	_BltTemplate
_BltTemplate:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	move.l	20(sp),d2
	move.l	24(sp),d3
	move.l	28(sp),d4
	move.l	32(sp),d5
	jsr	___graphics_ensure
	jsr	-36(a6)
	rts

	section	_ClearEOL_stub,code

; VOID ClearEOL(struct RastPort * rp)
	xdef	_ClearEOL
_ClearEOL:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-42(a6)
	rts

	section	_ClearScreen_stub,code

; VOID ClearScreen(struct RastPort * rp)
	xdef	_ClearScreen
_ClearScreen:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-48(a6)
	rts

	section	_TextLength_stub,code

; WORD TextLength(struct RastPort * rp, CONST_STRPTR string, UWORD count)
	xdef	_TextLength
_TextLength:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	move.l	12(sp),d0
	jsr	___graphics_ensure
	jsr	-54(a6)
	rts

	section	_Text_stub,code

; LONG Text(struct RastPort * rp, CONST_STRPTR string, UWORD count)
	xdef	_Text
_Text:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	move.l	12(sp),d0
	jsr	___graphics_ensure
	jsr	-60(a6)
	rts

	section	_SetFont_stub,code

; LONG SetFont(struct RastPort * rp, const struct TextFont * textFont)
	xdef	_SetFont
_SetFont:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	jsr	___graphics_ensure
	jsr	-66(a6)
	rts

	section	_OpenFont_stub,code

; struct TextFont * OpenFont(struct TextAttr * textAttr)
	xdef	_OpenFont
_OpenFont:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-72(a6)
	rts

	section	_CloseFont_stub,code

; VOID CloseFont(struct TextFont * textFont)
	xdef	_CloseFont
_CloseFont:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-78(a6)
	rts

	section	_AskSoftStyle_stub,code

; ULONG AskSoftStyle(struct RastPort * rp)
	xdef	_AskSoftStyle
_AskSoftStyle:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-84(a6)
	rts

	section	_SetSoftStyle_stub,code

; ULONG SetSoftStyle(struct RastPort * rp, ULONG style, ULONG enable)
	xdef	_SetSoftStyle
_SetSoftStyle:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-90(a6)
	rts

	section	_AddBob_stub,code

; VOID AddBob(struct Bob * bob, struct RastPort * rp)
	xdef	_AddBob
_AddBob:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-96(a6)
	rts

	section	_AddVSprite_stub,code

; VOID AddVSprite(struct VSprite * vSprite, struct RastPort * rp)
	xdef	_AddVSprite
_AddVSprite:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-102(a6)
	rts

	section	_DoCollision_stub,code

; VOID DoCollision(struct RastPort * rp)
	xdef	_DoCollision
_DoCollision:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-108(a6)
	rts

	section	_DrawGList_stub,code

; VOID DrawGList(struct RastPort * rp, struct ViewPort * vp)
	xdef	_DrawGList
_DrawGList:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	jsr	___graphics_ensure
	jsr	-114(a6)
	rts

	section	_InitGels_stub,code

; VOID InitGels(struct VSprite * head, struct VSprite * tail, struct GelsInfo * gelsInfo)
	xdef	_InitGels
_InitGels:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	jsr	___graphics_ensure
	jsr	-120(a6)
	rts

	section	_InitMasks_stub,code

; VOID InitMasks(struct VSprite * vSprite)
	xdef	_InitMasks
_InitMasks:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-126(a6)
	rts

	section	_RemIBob_stub,code

; VOID RemIBob(struct Bob * bob, struct RastPort * rp, struct ViewPort * vp)
	xdef	_RemIBob
_RemIBob:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	jsr	___graphics_ensure
	jsr	-132(a6)
	rts

	section	_RemVSprite_stub,code

; VOID RemVSprite(struct VSprite * vSprite)
	xdef	_RemVSprite
_RemVSprite:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-138(a6)
	rts

	section	_SetCollision_stub,code

; VOID SetCollision(ULONG num, VOID (*routine)(struct VSprite *gelA, struct VSprite *gelB) routine, struct GelsInfo * gelsInfo)
	xdef	_SetCollision
_SetCollision:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	jsr	___graphics_ensure
	jsr	-144(a6)
	rts

	section	_SortGList_stub,code

; VOID SortGList(struct RastPort * rp)
	xdef	_SortGList
_SortGList:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-150(a6)
	rts

	section	_AddAnimOb_stub,code

; VOID AddAnimOb(struct AnimOb * anOb, struct AnimOb ** anKey, struct RastPort * rp)
	xdef	_AddAnimOb
_AddAnimOb:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	jsr	___graphics_ensure
	jsr	-156(a6)
	rts

	section	_Animate_stub,code

; VOID Animate(struct AnimOb ** anKey, struct RastPort * rp)
	xdef	_Animate
_Animate:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-162(a6)
	rts

	section	_GetGBuffers_stub,code

; BOOL GetGBuffers(struct AnimOb * anOb, struct RastPort * rp, BOOL flag)
	xdef	_GetGBuffers
_GetGBuffers:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	jsr	___graphics_ensure
	jsr	-168(a6)
	rts

	section	_InitGMasks_stub,code

; VOID InitGMasks(struct AnimOb * anOb)
	xdef	_InitGMasks
_InitGMasks:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-174(a6)
	rts

	section	_DrawEllipse_stub,code

; VOID DrawEllipse(struct RastPort * rp, WORD xCenter, WORD yCenter, WORD a, WORD b)
	xdef	_DrawEllipse
_DrawEllipse:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	jsr	___graphics_ensure
	jsr	-180(a6)
	rts

	section	_AreaEllipse_stub,code

; LONG AreaEllipse(struct RastPort * rp, WORD xCenter, WORD yCenter, WORD a, WORD b)
	xdef	_AreaEllipse
_AreaEllipse:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	jsr	___graphics_ensure
	jsr	-186(a6)
	rts

	section	_LoadRGB4_stub,code

; VOID LoadRGB4(struct ViewPort * vp, const UWORD * colors, WORD count)
	xdef	_LoadRGB4
_LoadRGB4:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	jsr	___graphics_ensure
	jsr	-192(a6)
	rts

	section	_InitRastPort_stub,code

; VOID InitRastPort(struct RastPort * rp)
	xdef	_InitRastPort
_InitRastPort:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-198(a6)
	rts

	section	_InitVPort_stub,code

; VOID InitVPort(struct ViewPort * vp)
	xdef	_InitVPort
_InitVPort:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-204(a6)
	rts

	section	_MrgCop_stub,code

; ULONG MrgCop(struct View * view)
	xdef	_MrgCop
_MrgCop:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-210(a6)
	rts

	section	_MakeVPort_stub,code

; ULONG MakeVPort(struct View * view, struct ViewPort * vp)
	xdef	_MakeVPort
_MakeVPort:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-216(a6)
	rts

	section	_LoadView_stub,code

; VOID LoadView(struct View * view)
	xdef	_LoadView
_LoadView:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-222(a6)
	rts

	section	_WaitBlit_stub,code

; VOID WaitBlit()
	xdef	_WaitBlit
_WaitBlit:
	jsr	___graphics_ensure
	jsr	-228(a6)
	rts

	section	_SetRast_stub,code

; VOID SetRast(struct RastPort * rp, UBYTE pen)
	xdef	_SetRast
_SetRast:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-234(a6)
	rts

	section	_Move_stub,code

; VOID Move(struct RastPort * rp, WORD x, WORD y)
	xdef	_Move
_Move:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-240(a6)
	rts

	section	_Draw_stub,code

; VOID Draw(struct RastPort * rp, WORD x, WORD y)
	xdef	_Draw
_Draw:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-246(a6)
	rts

	section	_AreaMove_stub,code

; LONG AreaMove(struct RastPort * rp, WORD x, WORD y)
	xdef	_AreaMove
_AreaMove:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-252(a6)
	rts

	section	_AreaDraw_stub,code

; LONG AreaDraw(struct RastPort * rp, WORD x, WORD y)
	xdef	_AreaDraw
_AreaDraw:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-258(a6)
	rts

	section	_AreaEnd_stub,code

; LONG AreaEnd(struct RastPort * rp)
	xdef	_AreaEnd
_AreaEnd:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-264(a6)
	rts

	section	_WaitTOF_stub,code

; VOID WaitTOF()
	xdef	_WaitTOF
_WaitTOF:
	jsr	___graphics_ensure
	jsr	-270(a6)
	rts

	section	_QBlit_stub,code

; VOID QBlit(struct bltnode * blit)
	xdef	_QBlit
_QBlit:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-276(a6)
	rts

	section	_InitArea_stub,code

; VOID InitArea(struct AreaInfo * areaInfo, APTR vectorBuffer, WORD maxVectors)
	xdef	_InitArea
_InitArea:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	jsr	___graphics_ensure
	jsr	-282(a6)
	rts

	section	_SetRGB4_stub,code

; VOID SetRGB4(struct ViewPort * vp, WORD index, UBYTE red, UBYTE green, UBYTE blue)
	xdef	_SetRGB4
_SetRGB4:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	jsr	___graphics_ensure
	jsr	-288(a6)
	rts

	section	_QBSBlit_stub,code

; VOID QBSBlit(struct bltnode * blit)
	xdef	_QBSBlit
_QBSBlit:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-294(a6)
	rts

	section	_BltClear_stub,code

; VOID BltClear(PLANEPTR memBlock, ULONG byteCount, ULONG flags)
	xdef	_BltClear
_BltClear:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-300(a6)
	rts

	section	_RectFill_stub,code

; VOID RectFill(struct RastPort * rp, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_RectFill
_RectFill:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	jsr	___graphics_ensure
	jsr	-306(a6)
	rts

	section	_BltPattern_stub,code

; VOID BltPattern(struct RastPort * rp, const PLANEPTR mask, WORD xMin, WORD yMin, WORD xMax, WORD yMax, UWORD maskBPR)
	xdef	_BltPattern
_BltPattern:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	move.l	28(sp),d4
	jsr	___graphics_ensure
	jsr	-312(a6)
	rts

	section	_ReadPixel_stub,code

; ULONG ReadPixel(struct RastPort * rp, WORD x, WORD y)
	xdef	_ReadPixel
_ReadPixel:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-318(a6)
	rts

	section	_WritePixel_stub,code

; LONG WritePixel(struct RastPort * rp, WORD x, WORD y)
	xdef	_WritePixel
_WritePixel:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-324(a6)
	rts

	section	_Flood_stub,code

; BOOL Flood(struct RastPort * rp, ULONG mode, WORD x, WORD y)
	xdef	_Flood
_Flood:
	movea.l	4(sp),a1
	move.l	8(sp),d2
	move.l	12(sp),d0
	move.l	16(sp),d1
	jsr	___graphics_ensure
	jsr	-330(a6)
	rts

	section	_PolyDraw_stub,code

; VOID PolyDraw(struct RastPort * rp, WORD count, const WORD * polyTable)
	xdef	_PolyDraw
_PolyDraw:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	movea.l	12(sp),a0
	jsr	___graphics_ensure
	jsr	-336(a6)
	rts

	section	_SetAPen_stub,code

; VOID SetAPen(struct RastPort * rp, UBYTE pen)
	xdef	_SetAPen
_SetAPen:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-342(a6)
	rts

	section	_SetBPen_stub,code

; VOID SetBPen(struct RastPort * rp, UBYTE pen)
	xdef	_SetBPen
_SetBPen:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-348(a6)
	rts

	section	_SetDrMd_stub,code

; VOID SetDrMd(struct RastPort * rp, UBYTE drawMode)
	xdef	_SetDrMd
_SetDrMd:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-354(a6)
	rts

	section	_InitView_stub,code

; VOID InitView(struct View * view)
	xdef	_InitView
_InitView:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-360(a6)
	rts

	section	_CBump_stub,code

; VOID CBump(struct UCopList * copList)
	xdef	_CBump
_CBump:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-366(a6)
	rts

	section	_CMove_stub,code

; VOID CMove(struct UCopList * copList, APTR destination, WORD data)
	xdef	_CMove
_CMove:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-372(a6)
	rts

	section	_CWait_stub,code

; VOID CWait(struct UCopList * copList, WORD v, WORD h)
	xdef	_CWait
_CWait:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-378(a6)
	rts

	section	_VBeamPos_stub,code

; LONG VBeamPos()
	xdef	_VBeamPos
_VBeamPos:
	jsr	___graphics_ensure
	jsr	-384(a6)
	rts

	section	_InitBitMap_stub,code

; VOID InitBitMap(struct BitMap * bitMap, BYTE depth, WORD width, WORD height)
	xdef	_InitBitMap
_InitBitMap:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	jsr	___graphics_ensure
	jsr	-390(a6)
	rts

	section	_ScrollRaster_stub,code

; VOID ScrollRaster(struct RastPort * rp, WORD dx, WORD dy, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_ScrollRaster
_ScrollRaster:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	move.l	24(sp),d4
	move.l	28(sp),d5
	jsr	___graphics_ensure
	jsr	-396(a6)
	rts

	section	_WaitBOVP_stub,code

; VOID WaitBOVP(struct ViewPort * vp)
	xdef	_WaitBOVP
_WaitBOVP:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-402(a6)
	rts

	section	_GetSprite_stub,code

; WORD GetSprite(struct SimpleSprite * sprite, WORD num)
	xdef	_GetSprite
_GetSprite:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-408(a6)
	rts

	section	_FreeSprite_stub,code

; VOID FreeSprite(WORD num)
	xdef	_FreeSprite
_FreeSprite:
	move.l	4(sp),d0
	jsr	___graphics_ensure
	jsr	-414(a6)
	rts

	section	_ChangeSprite_stub,code

; VOID ChangeSprite(struct ViewPort * vp, struct SimpleSprite * sprite, UWORD * newData)
	xdef	_ChangeSprite
_ChangeSprite:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	jsr	___graphics_ensure
	jsr	-420(a6)
	rts

	section	_MoveSprite_stub,code

; VOID MoveSprite(struct ViewPort * vp, struct SimpleSprite * sprite, WORD x, WORD y)
	xdef	_MoveSprite
_MoveSprite:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	jsr	___graphics_ensure
	jsr	-426(a6)
	rts

	section	_LockLayerRom_stub,code

; VOID LockLayerRom(struct Layer * layer)
	xdef	_LockLayerRom
_LockLayerRom:
	movea.l	4(sp),a5
	jsr	___graphics_ensure
	jsr	-432(a6)
	rts

	section	_UnlockLayerRom_stub,code

; VOID UnlockLayerRom(struct Layer * layer)
	xdef	_UnlockLayerRom
_UnlockLayerRom:
	movea.l	4(sp),a5
	jsr	___graphics_ensure
	jsr	-438(a6)
	rts

	section	_SyncSBitMap_stub,code

; VOID SyncSBitMap(struct Layer * layer)
	xdef	_SyncSBitMap
_SyncSBitMap:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-444(a6)
	rts

	section	_CopySBitMap_stub,code

; VOID CopySBitMap(struct Layer * layer)
	xdef	_CopySBitMap
_CopySBitMap:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-450(a6)
	rts

	section	_OwnBlitter_stub,code

; VOID OwnBlitter()
	xdef	_OwnBlitter
_OwnBlitter:
	jsr	___graphics_ensure
	jsr	-456(a6)
	rts

	section	_DisownBlitter_stub,code

; VOID DisownBlitter()
	xdef	_DisownBlitter
_DisownBlitter:
	jsr	___graphics_ensure
	jsr	-462(a6)
	rts

	section	_InitTmpRas_stub,code

; struct TmpRas * InitTmpRas(struct TmpRas * tmpRas, PLANEPTR buffer, LONG size)
	xdef	_InitTmpRas
_InitTmpRas:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	jsr	___graphics_ensure
	jsr	-468(a6)
	rts

	section	_AskFont_stub,code

; VOID AskFont(struct RastPort * rp, struct TextAttr * textAttr)
	xdef	_AskFont
_AskFont:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	jsr	___graphics_ensure
	jsr	-474(a6)
	rts

	section	_AddFont_stub,code

; VOID AddFont(struct TextFont * textFont)
	xdef	_AddFont
_AddFont:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-480(a6)
	rts

	section	_RemFont_stub,code

; VOID RemFont(struct TextFont * textFont)
	xdef	_RemFont
_RemFont:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-486(a6)
	rts

	section	_AllocRaster_stub,code

; PLANEPTR AllocRaster(UWORD width, UWORD height)
	xdef	_AllocRaster
_AllocRaster:
	move.l	4(sp),d0
	move.l	8(sp),d1
	jsr	___graphics_ensure
	jsr	-492(a6)
	rts

	section	_FreeRaster_stub,code

; VOID FreeRaster(PLANEPTR p, UWORD width, UWORD height)
	xdef	_FreeRaster
_FreeRaster:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-498(a6)
	rts

	section	_AndRectRegion_stub,code

; VOID AndRectRegion(struct Region * region, const struct Rectangle * rectangle)
	xdef	_AndRectRegion
_AndRectRegion:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-504(a6)
	rts

	section	_OrRectRegion_stub,code

; BOOL OrRectRegion(struct Region * region, const struct Rectangle * rectangle)
	xdef	_OrRectRegion
_OrRectRegion:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-510(a6)
	rts

	section	_NewRegion_stub,code

; struct Region * NewRegion()
	xdef	_NewRegion
_NewRegion:
	jsr	___graphics_ensure
	jsr	-516(a6)
	rts

	section	_ClearRectRegion_stub,code

; BOOL ClearRectRegion(struct Region * region, const struct Rectangle * rectangle)
	xdef	_ClearRectRegion
_ClearRectRegion:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-522(a6)
	rts

	section	_ClearRegion_stub,code

; VOID ClearRegion(struct Region * region)
	xdef	_ClearRegion
_ClearRegion:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-528(a6)
	rts

	section	_DisposeRegion_stub,code

; VOID DisposeRegion(struct Region * region)
	xdef	_DisposeRegion
_DisposeRegion:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-534(a6)
	rts

	section	_FreeVPortCopLists_stub,code

; VOID FreeVPortCopLists(struct ViewPort * vp)
	xdef	_FreeVPortCopLists
_FreeVPortCopLists:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-540(a6)
	rts

	section	_FreeCopList_stub,code

; VOID FreeCopList(struct CopList * copList)
	xdef	_FreeCopList
_FreeCopList:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-546(a6)
	rts

	section	_ClipBlit_stub,code

; VOID ClipBlit(struct RastPort * srcRP, WORD xSrc, WORD ySrc, struct RastPort * destRP, WORD xDest, WORD yDest, WORD xSize, WORD ySize, UBYTE minterm)
	xdef	_ClipBlit
_ClipBlit:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	move.l	20(sp),d2
	move.l	24(sp),d3
	move.l	28(sp),d4
	move.l	32(sp),d5
	move.l	36(sp),d6
	jsr	___graphics_ensure
	jsr	-552(a6)
	rts

	section	_XorRectRegion_stub,code

; BOOL XorRectRegion(struct Region * region, const struct Rectangle * rectangle)
	xdef	_XorRectRegion
_XorRectRegion:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-558(a6)
	rts

	section	_FreeCprList_stub,code

; VOID FreeCprList(struct cprlist * cprList)
	xdef	_FreeCprList
_FreeCprList:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-564(a6)
	rts

	section	_GetColorMap_stub,code

; struct ColorMap * GetColorMap(LONG entries)
	xdef	_GetColorMap
_GetColorMap:
	move.l	4(sp),d0
	jsr	___graphics_ensure
	jsr	-570(a6)
	rts

	section	_FreeColorMap_stub,code

; VOID FreeColorMap(struct ColorMap * colorMap)
	xdef	_FreeColorMap
_FreeColorMap:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-576(a6)
	rts

	section	_GetRGB4_stub,code

; ULONG GetRGB4(struct ColorMap * colorMap, LONG entry)
	xdef	_GetRGB4
_GetRGB4:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-582(a6)
	rts

	section	_ScrollVPort_stub,code

; VOID ScrollVPort(struct ViewPort * vp)
	xdef	_ScrollVPort
_ScrollVPort:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-588(a6)
	rts

	section	_UCopperListInit_stub,code

; struct CopList * UCopperListInit(struct UCopList * uCopList, WORD n)
	xdef	_UCopperListInit
_UCopperListInit:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-594(a6)
	rts

	section	_FreeGBuffers_stub,code

; VOID FreeGBuffers(struct AnimOb * anOb, struct RastPort * rp, BOOL flag)
	xdef	_FreeGBuffers
_FreeGBuffers:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	jsr	___graphics_ensure
	jsr	-600(a6)
	rts

	section	_BltBitMapRastPort_stub,code

; VOID BltBitMapRastPort(const struct BitMap * srcBitMap, WORD xSrc, WORD ySrc, struct RastPort * destRP, WORD xDest, WORD yDest, WORD xSize, WORD ySize, UBYTE minterm)
	xdef	_BltBitMapRastPort
_BltBitMapRastPort:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	move.l	20(sp),d2
	move.l	24(sp),d3
	move.l	28(sp),d4
	move.l	32(sp),d5
	move.l	36(sp),d6
	jsr	___graphics_ensure
	jsr	-606(a6)
	rts

	section	_OrRegionRegion_stub,code

; BOOL OrRegionRegion(const struct Region * srcRegion, struct Region * destRegion)
	xdef	_OrRegionRegion
_OrRegionRegion:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-612(a6)
	rts

	section	_XorRegionRegion_stub,code

; BOOL XorRegionRegion(const struct Region * srcRegion, struct Region * destRegion)
	xdef	_XorRegionRegion
_XorRegionRegion:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-618(a6)
	rts

	section	_AndRegionRegion_stub,code

; BOOL AndRegionRegion(const struct Region * srcRegion, struct Region * destRegion)
	xdef	_AndRegionRegion
_AndRegionRegion:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-624(a6)
	rts

	section	_SetRGB4CM_stub,code

; VOID SetRGB4CM(struct ColorMap * colorMap, WORD index, UBYTE red, UBYTE green, UBYTE blue)
	xdef	_SetRGB4CM
_SetRGB4CM:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	jsr	___graphics_ensure
	jsr	-630(a6)
	rts

	section	_BltMaskBitMapRastPort_stub,code

; VOID BltMaskBitMapRastPort(const struct BitMap * srcBitMap, WORD xSrc, WORD ySrc, struct RastPort * destRP, WORD xDest, WORD yDest, WORD xSize, WORD ySize, UBYTE minterm, const PLANEPTR bltMask)
	xdef	_BltMaskBitMapRastPort
_BltMaskBitMapRastPort:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	move.l	20(sp),d2
	move.l	24(sp),d3
	move.l	28(sp),d4
	move.l	32(sp),d5
	move.l	36(sp),d6
	movea.l	40(sp),a2
	jsr	___graphics_ensure
	jsr	-636(a6)
	rts

	section	_AttemptLockLayerRom_stub,code

; BOOL AttemptLockLayerRom(struct Layer * layer)
	xdef	_AttemptLockLayerRom
_AttemptLockLayerRom:
	movea.l	4(sp),a5
	jsr	___graphics_ensure
	jsr	-654(a6)
	rts

	section	_GfxNew_stub,code

; APTR GfxNew(ULONG gfxNodeType)
	xdef	_GfxNew
_GfxNew:
	move.l	4(sp),d0
	jsr	___graphics_ensure
	jsr	-660(a6)
	rts

	section	_GfxFree_stub,code

; VOID GfxFree(APTR gfxNodePtr)
	xdef	_GfxFree
_GfxFree:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-666(a6)
	rts

	section	_GfxAssociate_stub,code

; VOID GfxAssociate(const APTR associateNode, APTR gfxNodePtr)
	xdef	_GfxAssociate
_GfxAssociate:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-672(a6)
	rts

	section	_BitMapScale_stub,code

; VOID BitMapScale(struct BitScaleArgs * bitScaleArgs)
	xdef	_BitMapScale
_BitMapScale:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-678(a6)
	rts

	section	_ScalerDiv_stub,code

; UWORD ScalerDiv(UWORD factor, UWORD numerator, UWORD denominator)
	xdef	_ScalerDiv
_ScalerDiv:
	move.l	4(sp),d0
	move.l	8(sp),d1
	move.l	12(sp),d2
	jsr	___graphics_ensure
	jsr	-684(a6)
	rts

	section	_TextExtent_stub,code

; WORD TextExtent(struct RastPort * rp, CONST_STRPTR string, WORD count, struct TextExtent * textExtent)
	xdef	_TextExtent
_TextExtent:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	16(sp),a2
	jsr	___graphics_ensure
	jsr	-690(a6)
	rts

	section	_TextFit_stub,code

; ULONG TextFit(struct RastPort * rp, CONST_STRPTR string, UWORD strLen, const struct TextExtent * textExtent, const struct TextExtent * constrainingExtent, WORD strDirection, UWORD constrainingBitWidth, UWORD constrainingBitHeight)
	xdef	_TextFit
_TextFit:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	16(sp),a2
	movea.l	20(sp),a3
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	jsr	___graphics_ensure
	jsr	-696(a6)
	rts

	section	_GfxLookUp_stub,code

; APTR GfxLookUp(const APTR associateNode)
	xdef	_GfxLookUp
_GfxLookUp:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-702(a6)
	rts

	section	_VideoControl_stub,code

; BOOL VideoControl(struct ColorMap * colorMap, struct TagItem * tagarray)
	xdef	_VideoControl
_VideoControl:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-708(a6)
	rts

	section	_OpenMonitor_stub,code

; struct MonitorSpec * OpenMonitor(CONST_STRPTR monitorName, ULONG displayID)
	xdef	_OpenMonitor
_OpenMonitor:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-720(a6)
	rts

	section	_CloseMonitor_stub,code

; BOOL CloseMonitor(struct MonitorSpec * monitorSpec)
	xdef	_CloseMonitor
_CloseMonitor:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-726(a6)
	rts

	section	_FindDisplayInfo_stub,code

; DisplayInfoHandle FindDisplayInfo(ULONG displayID)
	xdef	_FindDisplayInfo
_FindDisplayInfo:
	move.l	4(sp),d0
	jsr	___graphics_ensure
	jsr	-732(a6)
	rts

	section	_NextDisplayInfo_stub,code

; ULONG NextDisplayInfo(ULONG displayID)
	xdef	_NextDisplayInfo
_NextDisplayInfo:
	move.l	4(sp),d0
	jsr	___graphics_ensure
	jsr	-738(a6)
	rts

	section	_GetDisplayInfoData_stub,code

; ULONG GetDisplayInfoData(const DisplayInfoHandle handle, APTR buf, ULONG size, ULONG tagID, ULONG displayID)
	xdef	_GetDisplayInfoData
_GetDisplayInfoData:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	move.l	20(sp),d2
	jsr	___graphics_ensure
	jsr	-762(a6)
	rts

	section	_FontExtent_stub,code

; VOID FontExtent(const struct TextFont * font, struct TextExtent * fontExtent)
	xdef	_FontExtent
_FontExtent:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-768(a6)
	rts

	section	_ReadPixelLine8_stub,code

; LONG ReadPixelLine8(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD width, UBYTE * array, struct RastPort * tempRP)
	xdef	_ReadPixelLine8
_ReadPixelLine8:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	20(sp),a2
	movea.l	24(sp),a1
	jsr	___graphics_ensure
	jsr	-774(a6)
	rts

	section	_WritePixelLine8_stub,code

; LONG WritePixelLine8(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD width, UBYTE * array, struct RastPort * tempRP)
	xdef	_WritePixelLine8
_WritePixelLine8:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	20(sp),a2
	movea.l	24(sp),a1
	jsr	___graphics_ensure
	jsr	-780(a6)
	rts

	section	_ReadPixelArray8_stub,code

; LONG ReadPixelArray8(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD xstop, UWORD ystop, UBYTE * array, struct RastPort * temprp)
	xdef	_ReadPixelArray8
_ReadPixelArray8:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	movea.l	24(sp),a2
	movea.l	28(sp),a1
	jsr	___graphics_ensure
	jsr	-786(a6)
	rts

	section	_WritePixelArray8_stub,code

; LONG WritePixelArray8(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD xstop, UWORD ystop, UBYTE * array, struct RastPort * temprp)
	xdef	_WritePixelArray8
_WritePixelArray8:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	movea.l	24(sp),a2
	movea.l	28(sp),a1
	jsr	___graphics_ensure
	jsr	-792(a6)
	rts

	section	_GetVPModeID_stub,code

; LONG GetVPModeID(const struct ViewPort * vp)
	xdef	_GetVPModeID
_GetVPModeID:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-798(a6)
	rts

	section	_ModeNotAvailable_stub,code

; LONG ModeNotAvailable(ULONG modeID)
	xdef	_ModeNotAvailable
_ModeNotAvailable:
	move.l	4(sp),d0
	jsr	___graphics_ensure
	jsr	-804(a6)
	rts

	section	_EraseRect_stub,code

; VOID EraseRect(struct RastPort * rp, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_EraseRect
_EraseRect:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	jsr	___graphics_ensure
	jsr	-816(a6)
	rts

	section	_ExtendFont_stub,code

; ULONG ExtendFont(struct TextFont * font, const struct TagItem * fontTags)
	xdef	_ExtendFont
_ExtendFont:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-822(a6)
	rts

	section	_StripFont_stub,code

; VOID StripFont(struct TextFont * font)
	xdef	_StripFont
_StripFont:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-834(a6)
	rts

	section	_CalcIVG_stub,code

; UWORD CalcIVG(struct View * v, struct ViewPort * vp)
	xdef	_CalcIVG
_CalcIVG:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-840(a6)
	rts

	section	_AttachPalExtra_stub,code

; LONG AttachPalExtra(struct ColorMap * cm, struct ViewPort * vp)
	xdef	_AttachPalExtra
_AttachPalExtra:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-846(a6)
	rts

	section	_ObtainBestPenA_stub,code

; LONG ObtainBestPenA(struct ColorMap * cm, ULONG r, ULONG g, ULONG b, const struct TagItem * tags)
	xdef	_ObtainBestPenA
_ObtainBestPenA:
	movea.l	4(sp),a0
	move.l	8(sp),d1
	move.l	12(sp),d2
	move.l	16(sp),d3
	movea.l	20(sp),a1
	jsr	___graphics_ensure
	jsr	-852(a6)
	rts

	section	_SetRGB32_stub,code

; VOID SetRGB32(struct ViewPort * vp, ULONG n, ULONG r, ULONG g, ULONG b)
	xdef	_SetRGB32
_SetRGB32:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	jsr	___graphics_ensure
	jsr	-870(a6)
	rts

	section	_GetAPen_stub,code

; ULONG GetAPen(struct RastPort * rp)
	xdef	_GetAPen
_GetAPen:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-876(a6)
	rts

	section	_GetBPen_stub,code

; ULONG GetBPen(struct RastPort * rp)
	xdef	_GetBPen
_GetBPen:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-882(a6)
	rts

	section	_GetDrMd_stub,code

; ULONG GetDrMd(struct RastPort * rp)
	xdef	_GetDrMd
_GetDrMd:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-888(a6)
	rts

	section	_GetOutlinePen_stub,code

; ULONG GetOutlinePen(struct RastPort * rp)
	xdef	_GetOutlinePen
_GetOutlinePen:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-894(a6)
	rts

	section	_LoadRGB32_stub,code

; VOID LoadRGB32(struct ViewPort * vp, const ULONG * table)
	xdef	_LoadRGB32
_LoadRGB32:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-900(a6)
	rts

	section	_SetChipRev_stub,code

; ULONG SetChipRev(ULONG want)
	xdef	_SetChipRev
_SetChipRev:
	move.l	4(sp),d0
	jsr	___graphics_ensure
	jsr	-906(a6)
	rts

	section	_SetABPenDrMd_stub,code

; VOID SetABPenDrMd(struct RastPort * rp, ULONG apen, ULONG bpen, ULONG drawmode)
	xdef	_SetABPenDrMd
_SetABPenDrMd:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	jsr	___graphics_ensure
	jsr	-912(a6)
	rts

	section	_GetRGB32_stub,code

; VOID GetRGB32(const struct ColorMap * cm, ULONG firstcolor, ULONG ncolors, ULONG * table)
	xdef	_GetRGB32
_GetRGB32:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	jsr	___graphics_ensure
	jsr	-918(a6)
	rts

	section	_AllocBitMap_stub,code

; struct BitMap * AllocBitMap(ULONG sizex, ULONG sizey, ULONG depth, ULONG flags, const struct BitMap * friend_bitmap)
	xdef	_AllocBitMap
_AllocBitMap:
	move.l	4(sp),d0
	move.l	8(sp),d1
	move.l	12(sp),d2
	move.l	16(sp),d3
	movea.l	20(sp),a0
	jsr	___graphics_ensure
	jsr	-936(a6)
	rts

	section	_FreeBitMap_stub,code

; VOID FreeBitMap(struct BitMap * bm)
	xdef	_FreeBitMap
_FreeBitMap:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-942(a6)
	rts

	section	_GetExtSpriteA_stub,code

; LONG GetExtSpriteA(struct ExtSprite * ss, const struct TagItem * tags)
	xdef	_GetExtSpriteA
_GetExtSpriteA:
	movea.l	4(sp),a2
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-948(a6)
	rts

	section	_CoerceMode_stub,code

; ULONG CoerceMode(struct ViewPort * vp, ULONG monitorid, ULONG flags)
	xdef	_CoerceMode
_CoerceMode:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___graphics_ensure
	jsr	-960(a6)
	rts

	section	_ChangeVPBitMap_stub,code

; VOID ChangeVPBitMap(struct ViewPort * vp, struct BitMap * bm, struct DBufInfo * db)
	xdef	_ChangeVPBitMap
_ChangeVPBitMap:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	jsr	___graphics_ensure
	jsr	-966(a6)
	rts

	section	_ReleasePen_stub,code

; VOID ReleasePen(struct ColorMap * cm, ULONG n)
	xdef	_ReleasePen
_ReleasePen:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-972(a6)
	rts

	section	_ObtainPen_stub,code

; ULONG ObtainPen(struct ColorMap * cm, ULONG n, ULONG r, ULONG g, ULONG b, LONG f)
	xdef	_ObtainPen
_ObtainPen:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	move.l	24(sp),d4
	jsr	___graphics_ensure
	jsr	-978(a6)
	rts

	section	_GetBitMapAttr_stub,code

; ULONG GetBitMapAttr(const struct BitMap * bm, ULONG attrnum)
	xdef	_GetBitMapAttr
_GetBitMapAttr:
	movea.l	4(sp),a0
	move.l	8(sp),d1
	jsr	___graphics_ensure
	jsr	-984(a6)
	rts

	section	_AllocDBufInfo_stub,code

; struct DBufInfo * AllocDBufInfo(struct ViewPort * vp)
	xdef	_AllocDBufInfo
_AllocDBufInfo:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-990(a6)
	rts

	section	_FreeDBufInfo_stub,code

; VOID FreeDBufInfo(struct DBufInfo * dbi)
	xdef	_FreeDBufInfo
_FreeDBufInfo:
	movea.l	4(sp),a1
	jsr	___graphics_ensure
	jsr	-996(a6)
	rts

	section	_SetOutlinePen_stub,code

; ULONG SetOutlinePen(struct RastPort * rp, ULONG pen)
	xdef	_SetOutlinePen
_SetOutlinePen:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-1002(a6)
	rts

	section	_SetWriteMask_stub,code

; ULONG SetWriteMask(struct RastPort * rp, ULONG msk)
	xdef	_SetWriteMask
_SetWriteMask:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-1008(a6)
	rts

	section	_SetMaxPen_stub,code

; VOID SetMaxPen(struct RastPort * rp, ULONG maxpen)
	xdef	_SetMaxPen
_SetMaxPen:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___graphics_ensure
	jsr	-1014(a6)
	rts

	section	_SetRGB32CM_stub,code

; VOID SetRGB32CM(struct ColorMap * cm, ULONG n, ULONG r, ULONG g, ULONG b)
	xdef	_SetRGB32CM
_SetRGB32CM:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	jsr	___graphics_ensure
	jsr	-1020(a6)
	rts

	section	_ScrollRasterBF_stub,code

; VOID ScrollRasterBF(struct RastPort * rp, WORD dx, WORD dy, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_ScrollRasterBF
_ScrollRasterBF:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	move.l	24(sp),d4
	move.l	28(sp),d5
	jsr	___graphics_ensure
	jsr	-1026(a6)
	rts

	section	_FindColor_stub,code

; LONG FindColor(struct ColorMap * cm, ULONG r, ULONG g, ULONG b, LONG maxcolor)
	xdef	_FindColor
_FindColor:
	movea.l	4(sp),a3
	move.l	8(sp),d1
	move.l	12(sp),d2
	move.l	16(sp),d3
	move.l	20(sp),d4
	jsr	___graphics_ensure
	jsr	-1032(a6)
	rts

	section	_AllocSpriteDataA_stub,code

; struct ExtSprite * AllocSpriteDataA(const struct BitMap * bm, const struct TagItem * tags)
	xdef	_AllocSpriteDataA
_AllocSpriteDataA:
	movea.l	4(sp),a2
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-1044(a6)
	rts

	section	_ChangeExtSpriteA_stub,code

; LONG ChangeExtSpriteA(struct ViewPort * vp, struct ExtSprite * oldsprite, struct ExtSprite * newsprite, const struct TagItem * tags)
	xdef	_ChangeExtSpriteA
_ChangeExtSpriteA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	jsr	___graphics_ensure
	jsr	-1056(a6)
	rts

	section	_FreeSpriteData_stub,code

; VOID FreeSpriteData(struct ExtSprite * sp)
	xdef	_FreeSpriteData
_FreeSpriteData:
	movea.l	4(sp),a2
	jsr	___graphics_ensure
	jsr	-1068(a6)
	rts

	section	_SetRPAttrsA_stub,code

; VOID SetRPAttrsA(struct RastPort * rp, const struct TagItem * tags)
	xdef	_SetRPAttrsA
_SetRPAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-1074(a6)
	rts

	section	_GetRPAttrsA_stub,code

; VOID GetRPAttrsA(const struct RastPort * rp, const struct TagItem * tags)
	xdef	_GetRPAttrsA
_GetRPAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___graphics_ensure
	jsr	-1086(a6)
	rts

	section	_BestModeIDA_stub,code

; ULONG BestModeIDA(const struct TagItem * tags)
	xdef	_BestModeIDA
_BestModeIDA:
	movea.l	4(sp),a0
	jsr	___graphics_ensure
	jsr	-1098(a6)
	rts

	section	_WriteChunkyPixels_stub,code

; VOID WriteChunkyPixels(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD xstop, UWORD ystop, const UBYTE * array, LONG bytesperrow)
	xdef	_WriteChunkyPixels
_WriteChunkyPixels:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	movea.l	24(sp),a2
	move.l	28(sp),d4
	jsr	___graphics_ensure
	jsr	-1110(a6)
	rts

