; Generated from SFD file by Novus SFD Parser
; Library: graphics.library
; Base: _GfxBase
; Each function is in its own section for dead code elimination

	xref	_GfxBase

	section	_BltBitMap_stub,code

; LONG BltBitMap(const struct BitMap * srcBitMap, WORD xSrc, WORD ySrc, struct BitMap * destBitMap, WORD xDest, WORD yDest, WORD xSize, WORD ySize, UBYTE minterm, UBYTE mask, PLANEPTR tempA)
	xdef	_BltBitMap
_BltBitMap:
	movem.l	d2/d3/d4/d5/d6/d7/a2/a6,-(sp)
	movea.l	36(sp),a0
	move.l	40(sp),d0
	move.l	44(sp),d1
	movea.l	48(sp),a1
	move.l	52(sp),d2
	move.l	56(sp),d3
	move.l	60(sp),d4
	move.l	64(sp),d5
	move.l	68(sp),d6
	move.l	72(sp),d7
	movea.l	76(sp),a2
	movea.l	_GfxBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,d2/d3/d4/d5/d6/d7/a2/a6
	rts

	section	_BltTemplate_stub,code

; VOID BltTemplate(const PLANEPTR source, WORD xSrc, WORD srcMod, struct RastPort * destRP, WORD xDest, WORD yDest, WORD xSize, WORD ySize)
	xdef	_BltTemplate
_BltTemplate:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	movea.l	24(sp),a0
	move.l	28(sp),d0
	move.l	32(sp),d1
	movea.l	36(sp),a1
	move.l	40(sp),d2
	move.l	44(sp),d3
	move.l	48(sp),d4
	move.l	52(sp),d5
	movea.l	_GfxBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_ClearEOL_stub,code

; VOID ClearEOL(struct RastPort * rp)
	xdef	_ClearEOL
_ClearEOL:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_ClearScreen_stub,code

; VOID ClearScreen(struct RastPort * rp)
	xdef	_ClearScreen
_ClearScreen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_TextLength_stub,code

; WORD TextLength(struct RastPort * rp, CONST_STRPTR string, UWORD count)
	xdef	_TextLength
_TextLength:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	12(sp),a0
	move.l	16(sp),d0
	movea.l	_GfxBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_Text_stub,code

; LONG Text(struct RastPort * rp, CONST_STRPTR string, UWORD count)
	xdef	_Text
_Text:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	12(sp),a0
	move.l	16(sp),d0
	movea.l	_GfxBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetFont_stub,code

; LONG SetFont(struct RastPort * rp, const struct TextFont * textFont)
	xdef	_SetFont
_SetFont:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	12(sp),a0
	movea.l	_GfxBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenFont_stub,code

; struct TextFont * OpenFont(struct TextAttr * textAttr)
	xdef	_OpenFont
_OpenFont:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloseFont_stub,code

; VOID CloseFont(struct TextFont * textFont)
	xdef	_CloseFont
_CloseFont:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_AskSoftStyle_stub,code

; ULONG AskSoftStyle(struct RastPort * rp)
	xdef	_AskSoftStyle
_AskSoftStyle:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetSoftStyle_stub,code

; ULONG SetSoftStyle(struct RastPort * rp, ULONG style, ULONG enable)
	xdef	_SetSoftStyle
_SetSoftStyle:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddBob_stub,code

; VOID AddBob(struct Bob * bob, struct RastPort * rp)
	xdef	_AddBob
_AddBob:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddVSprite_stub,code

; VOID AddVSprite(struct VSprite * vSprite, struct RastPort * rp)
	xdef	_AddVSprite
_AddVSprite:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_DoCollision_stub,code

; VOID DoCollision(struct RastPort * rp)
	xdef	_DoCollision
_DoCollision:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_DrawGList_stub,code

; VOID DrawGList(struct RastPort * rp, struct ViewPort * vp)
	xdef	_DrawGList
_DrawGList:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	12(sp),a0
	movea.l	_GfxBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitGels_stub,code

; VOID InitGels(struct VSprite * head, struct VSprite * tail, struct GelsInfo * gelsInfo)
	xdef	_InitGels
_InitGels:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_GfxBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_InitMasks_stub,code

; VOID InitMasks(struct VSprite * vSprite)
	xdef	_InitMasks
_InitMasks:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemIBob_stub,code

; VOID RemIBob(struct Bob * bob, struct RastPort * rp, struct ViewPort * vp)
	xdef	_RemIBob
_RemIBob:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_GfxBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_RemVSprite_stub,code

; VOID RemVSprite(struct VSprite * vSprite)
	xdef	_RemVSprite
_RemVSprite:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetCollision_stub,code

; VOID SetCollision(ULONG num, VOID (*routine)(struct VSprite *gelA, struct VSprite *gelB) routine, struct GelsInfo * gelsInfo)
	xdef	_SetCollision
_SetCollision:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	_GfxBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_SortGList_stub,code

; VOID SortGList(struct RastPort * rp)
	xdef	_SortGList
_SortGList:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddAnimOb_stub,code

; VOID AddAnimOb(struct AnimOb * anOb, struct AnimOb ** anKey, struct RastPort * rp)
	xdef	_AddAnimOb
_AddAnimOb:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_GfxBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_Animate_stub,code

; VOID Animate(struct AnimOb ** anKey, struct RastPort * rp)
	xdef	_Animate
_Animate:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetGBuffers_stub,code

; BOOL GetGBuffers(struct AnimOb * anOb, struct RastPort * rp, BOOL flag)
	xdef	_GetGBuffers
_GetGBuffers:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_GfxBase,a6
	jsr	-168(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitGMasks_stub,code

; VOID InitGMasks(struct AnimOb * anOb)
	xdef	_InitGMasks
_InitGMasks:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a6
	rts

	section	_DrawEllipse_stub,code

; VOID DrawEllipse(struct RastPort * rp, WORD xCenter, WORD yCenter, WORD a, WORD b)
	xdef	_DrawEllipse
_DrawEllipse:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a1
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_GfxBase,a6
	jsr	-180(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_AreaEllipse_stub,code

; LONG AreaEllipse(struct RastPort * rp, WORD xCenter, WORD yCenter, WORD a, WORD b)
	xdef	_AreaEllipse
_AreaEllipse:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a1
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_GfxBase,a6
	jsr	-186(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_LoadRGB4_stub,code

; VOID LoadRGB4(struct ViewPort * vp, const UWORD * colors, WORD count)
	xdef	_LoadRGB4
_LoadRGB4:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_GfxBase,a6
	jsr	-192(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitRastPort_stub,code

; VOID InitRastPort(struct RastPort * rp)
	xdef	_InitRastPort
_InitRastPort:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-198(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitVPort_stub,code

; VOID InitVPort(struct ViewPort * vp)
	xdef	_InitVPort
_InitVPort:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-204(a6)
	movem.l	(sp)+,a6
	rts

	section	_MrgCop_stub,code

; ULONG MrgCop(struct View * view)
	xdef	_MrgCop
_MrgCop:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-210(a6)
	movem.l	(sp)+,a6
	rts

	section	_MakeVPort_stub,code

; ULONG MakeVPort(struct View * view, struct ViewPort * vp)
	xdef	_MakeVPort
_MakeVPort:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-216(a6)
	movem.l	(sp)+,a6
	rts

	section	_LoadView_stub,code

; VOID LoadView(struct View * view)
	xdef	_LoadView
_LoadView:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-222(a6)
	movem.l	(sp)+,a6
	rts

	section	_WaitBlit_stub,code

; VOID WaitBlit()
	xdef	_WaitBlit
_WaitBlit:
	movem.l	a6,-(sp)
	movea.l	_GfxBase,a6
	jsr	-228(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetRast_stub,code

; VOID SetRast(struct RastPort * rp, UBYTE pen)
	xdef	_SetRast
_SetRast:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-234(a6)
	movem.l	(sp)+,a6
	rts

	section	_Move_stub,code

; VOID Move(struct RastPort * rp, WORD x, WORD y)
	xdef	_Move
_Move:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,a6
	rts

	section	_Draw_stub,code

; VOID Draw(struct RastPort * rp, WORD x, WORD y)
	xdef	_Draw
_Draw:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-246(a6)
	movem.l	(sp)+,a6
	rts

	section	_AreaMove_stub,code

; LONG AreaMove(struct RastPort * rp, WORD x, WORD y)
	xdef	_AreaMove
_AreaMove:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-252(a6)
	movem.l	(sp)+,a6
	rts

	section	_AreaDraw_stub,code

; LONG AreaDraw(struct RastPort * rp, WORD x, WORD y)
	xdef	_AreaDraw
_AreaDraw:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-258(a6)
	movem.l	(sp)+,a6
	rts

	section	_AreaEnd_stub,code

; LONG AreaEnd(struct RastPort * rp)
	xdef	_AreaEnd
_AreaEnd:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-264(a6)
	movem.l	(sp)+,a6
	rts

	section	_WaitTOF_stub,code

; VOID WaitTOF()
	xdef	_WaitTOF
_WaitTOF:
	movem.l	a6,-(sp)
	movea.l	_GfxBase,a6
	jsr	-270(a6)
	movem.l	(sp)+,a6
	rts

	section	_QBlit_stub,code

; VOID QBlit(struct bltnode * blit)
	xdef	_QBlit
_QBlit:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-276(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitArea_stub,code

; VOID InitArea(struct AreaInfo * areaInfo, APTR vectorBuffer, WORD maxVectors)
	xdef	_InitArea
_InitArea:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_GfxBase,a6
	jsr	-282(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetRGB4_stub,code

; VOID SetRGB4(struct ViewPort * vp, WORD index, UBYTE red, UBYTE green, UBYTE blue)
	xdef	_SetRGB4
_SetRGB4:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_GfxBase,a6
	jsr	-288(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_QBSBlit_stub,code

; VOID QBSBlit(struct bltnode * blit)
	xdef	_QBSBlit
_QBSBlit:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-294(a6)
	movem.l	(sp)+,a6
	rts

	section	_BltClear_stub,code

; VOID BltClear(PLANEPTR memBlock, ULONG byteCount, ULONG flags)
	xdef	_BltClear
_BltClear:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-300(a6)
	movem.l	(sp)+,a6
	rts

	section	_RectFill_stub,code

; VOID RectFill(struct RastPort * rp, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_RectFill
_RectFill:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a1
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_GfxBase,a6
	jsr	-306(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_BltPattern_stub,code

; VOID BltPattern(struct RastPort * rp, const PLANEPTR mask, WORD xMin, WORD yMin, WORD xMax, WORD yMax, UWORD maskBPR)
	xdef	_BltPattern
_BltPattern:
	movem.l	d2/d3/d4/a6,-(sp)
	movea.l	20(sp),a1
	movea.l	24(sp),a0
	move.l	28(sp),d0
	move.l	32(sp),d1
	move.l	36(sp),d2
	move.l	40(sp),d3
	move.l	44(sp),d4
	movea.l	_GfxBase,a6
	jsr	-312(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_ReadPixel_stub,code

; ULONG ReadPixel(struct RastPort * rp, WORD x, WORD y)
	xdef	_ReadPixel
_ReadPixel:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-318(a6)
	movem.l	(sp)+,a6
	rts

	section	_WritePixel_stub,code

; LONG WritePixel(struct RastPort * rp, WORD x, WORD y)
	xdef	_WritePixel
_WritePixel:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-324(a6)
	movem.l	(sp)+,a6
	rts

	section	_Flood_stub,code

; BOOL Flood(struct RastPort * rp, ULONG mode, WORD x, WORD y)
	xdef	_Flood
_Flood:
	movem.l	d2/a6,-(sp)
	movea.l	12(sp),a1
	move.l	16(sp),d2
	move.l	20(sp),d0
	move.l	24(sp),d1
	movea.l	_GfxBase,a6
	jsr	-330(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_PolyDraw_stub,code

; VOID PolyDraw(struct RastPort * rp, WORD count, const WORD * polyTable)
	xdef	_PolyDraw
_PolyDraw:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	16(sp),a0
	movea.l	_GfxBase,a6
	jsr	-336(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetAPen_stub,code

; VOID SetAPen(struct RastPort * rp, UBYTE pen)
	xdef	_SetAPen
_SetAPen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-342(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetBPen_stub,code

; VOID SetBPen(struct RastPort * rp, UBYTE pen)
	xdef	_SetBPen
_SetBPen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-348(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetDrMd_stub,code

; VOID SetDrMd(struct RastPort * rp, UBYTE drawMode)
	xdef	_SetDrMd
_SetDrMd:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-354(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitView_stub,code

; VOID InitView(struct View * view)
	xdef	_InitView
_InitView:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-360(a6)
	movem.l	(sp)+,a6
	rts

	section	_CBump_stub,code

; VOID CBump(struct UCopList * copList)
	xdef	_CBump
_CBump:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-366(a6)
	movem.l	(sp)+,a6
	rts

	section	_CMove_stub,code

; VOID CMove(struct UCopList * copList, APTR destination, WORD data)
	xdef	_CMove
_CMove:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-372(a6)
	movem.l	(sp)+,a6
	rts

	section	_CWait_stub,code

; VOID CWait(struct UCopList * copList, WORD v, WORD h)
	xdef	_CWait
_CWait:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-378(a6)
	movem.l	(sp)+,a6
	rts

	section	_VBeamPos_stub,code

; LONG VBeamPos()
	xdef	_VBeamPos
_VBeamPos:
	movem.l	a6,-(sp)
	movea.l	_GfxBase,a6
	jsr	-384(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitBitMap_stub,code

; VOID InitBitMap(struct BitMap * bitMap, BYTE depth, WORD width, WORD height)
	xdef	_InitBitMap
_InitBitMap:
	movem.l	d2/a6,-(sp)
	movea.l	12(sp),a0
	move.l	16(sp),d0
	move.l	20(sp),d1
	move.l	24(sp),d2
	movea.l	_GfxBase,a6
	jsr	-390(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_ScrollRaster_stub,code

; VOID ScrollRaster(struct RastPort * rp, WORD dx, WORD dy, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_ScrollRaster
_ScrollRaster:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	movea.l	24(sp),a1
	move.l	28(sp),d0
	move.l	32(sp),d1
	move.l	36(sp),d2
	move.l	40(sp),d3
	move.l	44(sp),d4
	move.l	48(sp),d5
	movea.l	_GfxBase,a6
	jsr	-396(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_WaitBOVP_stub,code

; VOID WaitBOVP(struct ViewPort * vp)
	xdef	_WaitBOVP
_WaitBOVP:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-402(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetSprite_stub,code

; WORD GetSprite(struct SimpleSprite * sprite, WORD num)
	xdef	_GetSprite
_GetSprite:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-408(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeSprite_stub,code

; VOID FreeSprite(WORD num)
	xdef	_FreeSprite
_FreeSprite:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_GfxBase,a6
	jsr	-414(a6)
	movem.l	(sp)+,a6
	rts

	section	_ChangeSprite_stub,code

; VOID ChangeSprite(struct ViewPort * vp, struct SimpleSprite * sprite, UWORD * newData)
	xdef	_ChangeSprite
_ChangeSprite:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_GfxBase,a6
	jsr	-420(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_MoveSprite_stub,code

; VOID MoveSprite(struct ViewPort * vp, struct SimpleSprite * sprite, WORD x, WORD y)
	xdef	_MoveSprite
_MoveSprite:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_GfxBase,a6
	jsr	-426(a6)
	movem.l	(sp)+,a6
	rts

	section	_LockLayerRom_stub,code

; VOID LockLayerRom(struct Layer * layer)
	xdef	_LockLayerRom
_LockLayerRom:
	movem.l	a5/a6,-(sp)
	movea.l	12(sp),a5
	movea.l	_GfxBase,a6
	jsr	-432(a6)
	movem.l	(sp)+,a5/a6
	rts

	section	_UnlockLayerRom_stub,code

; VOID UnlockLayerRom(struct Layer * layer)
	xdef	_UnlockLayerRom
_UnlockLayerRom:
	movem.l	a5/a6,-(sp)
	movea.l	12(sp),a5
	movea.l	_GfxBase,a6
	jsr	-438(a6)
	movem.l	(sp)+,a5/a6
	rts

	section	_SyncSBitMap_stub,code

; VOID SyncSBitMap(struct Layer * layer)
	xdef	_SyncSBitMap
_SyncSBitMap:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-444(a6)
	movem.l	(sp)+,a6
	rts

	section	_CopySBitMap_stub,code

; VOID CopySBitMap(struct Layer * layer)
	xdef	_CopySBitMap
_CopySBitMap:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-450(a6)
	movem.l	(sp)+,a6
	rts

	section	_OwnBlitter_stub,code

; VOID OwnBlitter()
	xdef	_OwnBlitter
_OwnBlitter:
	movem.l	a6,-(sp)
	movea.l	_GfxBase,a6
	jsr	-456(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisownBlitter_stub,code

; VOID DisownBlitter()
	xdef	_DisownBlitter
_DisownBlitter:
	movem.l	a6,-(sp)
	movea.l	_GfxBase,a6
	jsr	-462(a6)
	movem.l	(sp)+,a6
	rts

	section	_InitTmpRas_stub,code

; struct TmpRas * InitTmpRas(struct TmpRas * tmpRas, PLANEPTR buffer, LONG size)
	xdef	_InitTmpRas
_InitTmpRas:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_GfxBase,a6
	jsr	-468(a6)
	movem.l	(sp)+,a6
	rts

	section	_AskFont_stub,code

; VOID AskFont(struct RastPort * rp, struct TextAttr * textAttr)
	xdef	_AskFont
_AskFont:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	12(sp),a0
	movea.l	_GfxBase,a6
	jsr	-474(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddFont_stub,code

; VOID AddFont(struct TextFont * textFont)
	xdef	_AddFont
_AddFont:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-480(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemFont_stub,code

; VOID RemFont(struct TextFont * textFont)
	xdef	_RemFont
_RemFont:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-486(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocRaster_stub,code

; PLANEPTR AllocRaster(UWORD width, UWORD height)
	xdef	_AllocRaster
_AllocRaster:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_GfxBase,a6
	jsr	-492(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeRaster_stub,code

; VOID FreeRaster(PLANEPTR p, UWORD width, UWORD height)
	xdef	_FreeRaster
_FreeRaster:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-498(a6)
	movem.l	(sp)+,a6
	rts

	section	_AndRectRegion_stub,code

; VOID AndRectRegion(struct Region * region, const struct Rectangle * rectangle)
	xdef	_AndRectRegion
_AndRectRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-504(a6)
	movem.l	(sp)+,a6
	rts

	section	_OrRectRegion_stub,code

; BOOL OrRectRegion(struct Region * region, const struct Rectangle * rectangle)
	xdef	_OrRectRegion
_OrRectRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-510(a6)
	movem.l	(sp)+,a6
	rts

	section	_NewRegion_stub,code

; struct Region * NewRegion()
	xdef	_NewRegion
_NewRegion:
	movem.l	a6,-(sp)
	movea.l	_GfxBase,a6
	jsr	-516(a6)
	movem.l	(sp)+,a6
	rts

	section	_ClearRectRegion_stub,code

; BOOL ClearRectRegion(struct Region * region, const struct Rectangle * rectangle)
	xdef	_ClearRectRegion
_ClearRectRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-522(a6)
	movem.l	(sp)+,a6
	rts

	section	_ClearRegion_stub,code

; VOID ClearRegion(struct Region * region)
	xdef	_ClearRegion
_ClearRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-528(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeRegion_stub,code

; VOID DisposeRegion(struct Region * region)
	xdef	_DisposeRegion
_DisposeRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-534(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeVPortCopLists_stub,code

; VOID FreeVPortCopLists(struct ViewPort * vp)
	xdef	_FreeVPortCopLists
_FreeVPortCopLists:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-540(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeCopList_stub,code

; VOID FreeCopList(struct CopList * copList)
	xdef	_FreeCopList
_FreeCopList:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-546(a6)
	movem.l	(sp)+,a6
	rts

	section	_ClipBlit_stub,code

; VOID ClipBlit(struct RastPort * srcRP, WORD xSrc, WORD ySrc, struct RastPort * destRP, WORD xDest, WORD yDest, WORD xSize, WORD ySize, UBYTE minterm)
	xdef	_ClipBlit
_ClipBlit:
	movem.l	d2/d3/d4/d5/d6/a6,-(sp)
	movea.l	28(sp),a0
	move.l	32(sp),d0
	move.l	36(sp),d1
	movea.l	40(sp),a1
	move.l	44(sp),d2
	move.l	48(sp),d3
	move.l	52(sp),d4
	move.l	56(sp),d5
	move.l	60(sp),d6
	movea.l	_GfxBase,a6
	jsr	-552(a6)
	movem.l	(sp)+,d2/d3/d4/d5/d6/a6
	rts

	section	_XorRectRegion_stub,code

; BOOL XorRectRegion(struct Region * region, const struct Rectangle * rectangle)
	xdef	_XorRectRegion
_XorRectRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-558(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeCprList_stub,code

; VOID FreeCprList(struct cprlist * cprList)
	xdef	_FreeCprList
_FreeCprList:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-564(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetColorMap_stub,code

; struct ColorMap * GetColorMap(LONG entries)
	xdef	_GetColorMap
_GetColorMap:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_GfxBase,a6
	jsr	-570(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeColorMap_stub,code

; VOID FreeColorMap(struct ColorMap * colorMap)
	xdef	_FreeColorMap
_FreeColorMap:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-576(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetRGB4_stub,code

; ULONG GetRGB4(struct ColorMap * colorMap, LONG entry)
	xdef	_GetRGB4
_GetRGB4:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-582(a6)
	movem.l	(sp)+,a6
	rts

	section	_ScrollVPort_stub,code

; VOID ScrollVPort(struct ViewPort * vp)
	xdef	_ScrollVPort
_ScrollVPort:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-588(a6)
	movem.l	(sp)+,a6
	rts

	section	_UCopperListInit_stub,code

; struct CopList * UCopperListInit(struct UCopList * uCopList, WORD n)
	xdef	_UCopperListInit
_UCopperListInit:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-594(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeGBuffers_stub,code

; VOID FreeGBuffers(struct AnimOb * anOb, struct RastPort * rp, BOOL flag)
	xdef	_FreeGBuffers
_FreeGBuffers:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_GfxBase,a6
	jsr	-600(a6)
	movem.l	(sp)+,a6
	rts

	section	_BltBitMapRastPort_stub,code

; VOID BltBitMapRastPort(const struct BitMap * srcBitMap, WORD xSrc, WORD ySrc, struct RastPort * destRP, WORD xDest, WORD yDest, WORD xSize, WORD ySize, UBYTE minterm)
	xdef	_BltBitMapRastPort
_BltBitMapRastPort:
	movem.l	d2/d3/d4/d5/d6/a6,-(sp)
	movea.l	28(sp),a0
	move.l	32(sp),d0
	move.l	36(sp),d1
	movea.l	40(sp),a1
	move.l	44(sp),d2
	move.l	48(sp),d3
	move.l	52(sp),d4
	move.l	56(sp),d5
	move.l	60(sp),d6
	movea.l	_GfxBase,a6
	jsr	-606(a6)
	movem.l	(sp)+,d2/d3/d4/d5/d6/a6
	rts

	section	_OrRegionRegion_stub,code

; BOOL OrRegionRegion(const struct Region * srcRegion, struct Region * destRegion)
	xdef	_OrRegionRegion
_OrRegionRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-612(a6)
	movem.l	(sp)+,a6
	rts

	section	_XorRegionRegion_stub,code

; BOOL XorRegionRegion(const struct Region * srcRegion, struct Region * destRegion)
	xdef	_XorRegionRegion
_XorRegionRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-618(a6)
	movem.l	(sp)+,a6
	rts

	section	_AndRegionRegion_stub,code

; BOOL AndRegionRegion(const struct Region * srcRegion, struct Region * destRegion)
	xdef	_AndRegionRegion
_AndRegionRegion:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-624(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetRGB4CM_stub,code

; VOID SetRGB4CM(struct ColorMap * colorMap, WORD index, UBYTE red, UBYTE green, UBYTE blue)
	xdef	_SetRGB4CM
_SetRGB4CM:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_GfxBase,a6
	jsr	-630(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_BltMaskBitMapRastPort_stub,code

; VOID BltMaskBitMapRastPort(const struct BitMap * srcBitMap, WORD xSrc, WORD ySrc, struct RastPort * destRP, WORD xDest, WORD yDest, WORD xSize, WORD ySize, UBYTE minterm, const PLANEPTR bltMask)
	xdef	_BltMaskBitMapRastPort
_BltMaskBitMapRastPort:
	movem.l	d2/d3/d4/d5/d6/a2/a6,-(sp)
	movea.l	32(sp),a0
	move.l	36(sp),d0
	move.l	40(sp),d1
	movea.l	44(sp),a1
	move.l	48(sp),d2
	move.l	52(sp),d3
	move.l	56(sp),d4
	move.l	60(sp),d5
	move.l	64(sp),d6
	movea.l	68(sp),a2
	movea.l	_GfxBase,a6
	jsr	-636(a6)
	movem.l	(sp)+,d2/d3/d4/d5/d6/a2/a6
	rts

	section	_AttemptLockLayerRom_stub,code

; BOOL AttemptLockLayerRom(struct Layer * layer)
	xdef	_AttemptLockLayerRom
_AttemptLockLayerRom:
	movem.l	a5/a6,-(sp)
	movea.l	12(sp),a5
	movea.l	_GfxBase,a6
	jsr	-654(a6)
	movem.l	(sp)+,a5/a6
	rts

	section	_GfxNew_stub,code

; APTR GfxNew(ULONG gfxNodeType)
	xdef	_GfxNew
_GfxNew:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_GfxBase,a6
	jsr	-660(a6)
	movem.l	(sp)+,a6
	rts

	section	_GfxFree_stub,code

; VOID GfxFree(APTR gfxNodePtr)
	xdef	_GfxFree
_GfxFree:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-666(a6)
	movem.l	(sp)+,a6
	rts

	section	_GfxAssociate_stub,code

; VOID GfxAssociate(const APTR associateNode, APTR gfxNodePtr)
	xdef	_GfxAssociate
_GfxAssociate:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-672(a6)
	movem.l	(sp)+,a6
	rts

	section	_BitMapScale_stub,code

; VOID BitMapScale(struct BitScaleArgs * bitScaleArgs)
	xdef	_BitMapScale
_BitMapScale:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-678(a6)
	movem.l	(sp)+,a6
	rts

	section	_ScalerDiv_stub,code

; UWORD ScalerDiv(UWORD factor, UWORD numerator, UWORD denominator)
	xdef	_ScalerDiv
_ScalerDiv:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d0
	move.l	16(sp),d1
	move.l	20(sp),d2
	movea.l	_GfxBase,a6
	jsr	-684(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_TextExtent_stub,code

; WORD TextExtent(struct RastPort * rp, CONST_STRPTR string, WORD count, struct TextExtent * textExtent)
	xdef	_TextExtent
_TextExtent:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a1
	movea.l	16(sp),a0
	move.l	20(sp),d0
	movea.l	24(sp),a2
	movea.l	_GfxBase,a6
	jsr	-690(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_TextFit_stub,code

; ULONG TextFit(struct RastPort * rp, CONST_STRPTR string, UWORD strLen, const struct TextExtent * textExtent, const struct TextExtent * constrainingExtent, WORD strDirection, UWORD constrainingBitWidth, UWORD constrainingBitHeight)
	xdef	_TextFit
_TextFit:
	movem.l	d2/d3/a2/a3/a6,-(sp)
	movea.l	24(sp),a1
	movea.l	28(sp),a0
	move.l	32(sp),d0
	movea.l	36(sp),a2
	movea.l	40(sp),a3
	move.l	44(sp),d1
	move.l	48(sp),d2
	move.l	52(sp),d3
	movea.l	_GfxBase,a6
	jsr	-696(a6)
	movem.l	(sp)+,d2/d3/a2/a3/a6
	rts

	section	_GfxLookUp_stub,code

; APTR GfxLookUp(const APTR associateNode)
	xdef	_GfxLookUp
_GfxLookUp:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-702(a6)
	movem.l	(sp)+,a6
	rts

	section	_VideoControl_stub,code

; BOOL VideoControl(struct ColorMap * colorMap, struct TagItem * tagarray)
	xdef	_VideoControl
_VideoControl:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-708(a6)
	movem.l	(sp)+,a6
	rts

	section	_VideoControlTags_stub,code

; BOOL VideoControlTags(struct ColorMap * colorMap, ULONG tagarray, ... )
	xdef	_VideoControlTags
_VideoControlTags:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-708(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenMonitor_stub,code

; struct MonitorSpec * OpenMonitor(CONST_STRPTR monitorName, ULONG displayID)
	xdef	_OpenMonitor
_OpenMonitor:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-714(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloseMonitor_stub,code

; BOOL CloseMonitor(struct MonitorSpec * monitorSpec)
	xdef	_CloseMonitor
_CloseMonitor:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-720(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindDisplayInfo_stub,code

; DisplayInfoHandle FindDisplayInfo(ULONG displayID)
	xdef	_FindDisplayInfo
_FindDisplayInfo:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_GfxBase,a6
	jsr	-726(a6)
	movem.l	(sp)+,a6
	rts

	section	_NextDisplayInfo_stub,code

; ULONG NextDisplayInfo(ULONG displayID)
	xdef	_NextDisplayInfo
_NextDisplayInfo:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_GfxBase,a6
	jsr	-732(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetDisplayInfoData_stub,code

; ULONG GetDisplayInfoData(const DisplayInfoHandle handle, APTR buf, ULONG size, ULONG tagID, ULONG displayID)
	xdef	_GetDisplayInfoData
_GetDisplayInfoData:
	movem.l	d2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	movea.l	_GfxBase,a6
	jsr	-756(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_FontExtent_stub,code

; VOID FontExtent(const struct TextFont * font, struct TextExtent * fontExtent)
	xdef	_FontExtent
_FontExtent:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-762(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadPixelLine8_stub,code

; LONG ReadPixelLine8(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD width, UBYTE * array, struct RastPort * tempRP)
	xdef	_ReadPixelLine8
_ReadPixelLine8:
	movem.l	d2/a2/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	movea.l	32(sp),a2
	movea.l	36(sp),a1
	movea.l	_GfxBase,a6
	jsr	-768(a6)
	movem.l	(sp)+,d2/a2/a6
	rts

	section	_WritePixelLine8_stub,code

; LONG WritePixelLine8(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD width, UBYTE * array, struct RastPort * tempRP)
	xdef	_WritePixelLine8
_WritePixelLine8:
	movem.l	d2/a2/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	movea.l	32(sp),a2
	movea.l	36(sp),a1
	movea.l	_GfxBase,a6
	jsr	-774(a6)
	movem.l	(sp)+,d2/a2/a6
	rts

	section	_ReadPixelArray8_stub,code

; LONG ReadPixelArray8(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD xstop, UWORD ystop, UBYTE * array, struct RastPort * temprp)
	xdef	_ReadPixelArray8
_ReadPixelArray8:
	movem.l	d2/d3/a2/a6,-(sp)
	movea.l	20(sp),a0
	move.l	24(sp),d0
	move.l	28(sp),d1
	move.l	32(sp),d2
	move.l	36(sp),d3
	movea.l	40(sp),a2
	movea.l	44(sp),a1
	movea.l	_GfxBase,a6
	jsr	-780(a6)
	movem.l	(sp)+,d2/d3/a2/a6
	rts

	section	_WritePixelArray8_stub,code

; LONG WritePixelArray8(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD xstop, UWORD ystop, UBYTE * array, struct RastPort * temprp)
	xdef	_WritePixelArray8
_WritePixelArray8:
	movem.l	d2/d3/a2/a6,-(sp)
	movea.l	20(sp),a0
	move.l	24(sp),d0
	move.l	28(sp),d1
	move.l	32(sp),d2
	move.l	36(sp),d3
	movea.l	40(sp),a2
	movea.l	44(sp),a1
	movea.l	_GfxBase,a6
	jsr	-786(a6)
	movem.l	(sp)+,d2/d3/a2/a6
	rts

	section	_GetVPModeID_stub,code

; LONG GetVPModeID(const struct ViewPort * vp)
	xdef	_GetVPModeID
_GetVPModeID:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-792(a6)
	movem.l	(sp)+,a6
	rts

	section	_ModeNotAvailable_stub,code

; LONG ModeNotAvailable(ULONG modeID)
	xdef	_ModeNotAvailable
_ModeNotAvailable:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_GfxBase,a6
	jsr	-798(a6)
	movem.l	(sp)+,a6
	rts

	section	_EraseRect_stub,code

; VOID EraseRect(struct RastPort * rp, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_EraseRect
_EraseRect:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a1
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_GfxBase,a6
	jsr	-810(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_ExtendFont_stub,code

; ULONG ExtendFont(struct TextFont * font, const struct TagItem * fontTags)
	xdef	_ExtendFont
_ExtendFont:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-816(a6)
	movem.l	(sp)+,a6
	rts

	section	_ExtendFontTags_stub,code

; ULONG ExtendFontTags(struct TextFont * font, ULONG fontTags, ... )
	xdef	_ExtendFontTags
_ExtendFontTags:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-816(a6)
	movem.l	(sp)+,a6
	rts

	section	_StripFont_stub,code

; VOID StripFont(struct TextFont * font)
	xdef	_StripFont
_StripFont:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-822(a6)
	movem.l	(sp)+,a6
	rts

	section	_CalcIVG_stub,code

; UWORD CalcIVG(struct View * v, struct ViewPort * vp)
	xdef	_CalcIVG
_CalcIVG:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-828(a6)
	movem.l	(sp)+,a6
	rts

	section	_AttachPalExtra_stub,code

; LONG AttachPalExtra(struct ColorMap * cm, struct ViewPort * vp)
	xdef	_AttachPalExtra
_AttachPalExtra:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-834(a6)
	movem.l	(sp)+,a6
	rts

	section	_ObtainBestPenA_stub,code

; LONG ObtainBestPenA(struct ColorMap * cm, ULONG r, ULONG g, ULONG b, const struct TagItem * tags)
	xdef	_ObtainBestPenA
_ObtainBestPenA:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	movea.l	32(sp),a1
	movea.l	_GfxBase,a6
	jsr	-840(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_ObtainBestPen_stub,code

; LONG ObtainBestPen(struct ColorMap * cm, ULONG r, ULONG g, ULONG b, ULONG tags, ... )
	xdef	_ObtainBestPen
_ObtainBestPen:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	lea	32(sp),a1
	movea.l	_GfxBase,a6
	jsr	-840(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_SetRGB32_stub,code

; VOID SetRGB32(struct ViewPort * vp, ULONG n, ULONG r, ULONG g, ULONG b)
	xdef	_SetRGB32
_SetRGB32:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_GfxBase,a6
	jsr	-852(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_GetAPen_stub,code

; ULONG GetAPen(struct RastPort * rp)
	xdef	_GetAPen
_GetAPen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-858(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetBPen_stub,code

; ULONG GetBPen(struct RastPort * rp)
	xdef	_GetBPen
_GetBPen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-864(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetDrMd_stub,code

; ULONG GetDrMd(struct RastPort * rp)
	xdef	_GetDrMd
_GetDrMd:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-870(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetOutlinePen_stub,code

; ULONG GetOutlinePen(struct RastPort * rp)
	xdef	_GetOutlinePen
_GetOutlinePen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-876(a6)
	movem.l	(sp)+,a6
	rts

	section	_LoadRGB32_stub,code

; VOID LoadRGB32(struct ViewPort * vp, const ULONG * table)
	xdef	_LoadRGB32
_LoadRGB32:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-882(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetChipRev_stub,code

; ULONG SetChipRev(ULONG want)
	xdef	_SetChipRev
_SetChipRev:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_GfxBase,a6
	jsr	-888(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetABPenDrMd_stub,code

; VOID SetABPenDrMd(struct RastPort * rp, ULONG apen, ULONG bpen, ULONG drawmode)
	xdef	_SetABPenDrMd
_SetABPenDrMd:
	movem.l	d2/a6,-(sp)
	movea.l	12(sp),a1
	move.l	16(sp),d0
	move.l	20(sp),d1
	move.l	24(sp),d2
	movea.l	_GfxBase,a6
	jsr	-894(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_GetRGB32_stub,code

; VOID GetRGB32(const struct ColorMap * cm, ULONG firstcolor, ULONG ncolors, ULONG * table)
	xdef	_GetRGB32
_GetRGB32:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a1
	movea.l	_GfxBase,a6
	jsr	-900(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocBitMap_stub,code

; struct BitMap * AllocBitMap(ULONG sizex, ULONG sizey, ULONG depth, ULONG flags, const struct BitMap * friend_bitmap)
	xdef	_AllocBitMap
_AllocBitMap:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d0
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	movea.l	32(sp),a0
	movea.l	_GfxBase,a6
	jsr	-918(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_FreeBitMap_stub,code

; VOID FreeBitMap(struct BitMap * bm)
	xdef	_FreeBitMap
_FreeBitMap:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-924(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetExtSpriteA_stub,code

; LONG GetExtSpriteA(struct ExtSprite * ss, const struct TagItem * tags)
	xdef	_GetExtSpriteA
_GetExtSpriteA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a2
	movea.l	16(sp),a1
	movea.l	_GfxBase,a6
	jsr	-930(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_GetExtSprite_stub,code

; LONG GetExtSprite(struct ExtSprite * ss, ULONG tags, ... )
	xdef	_GetExtSprite
_GetExtSprite:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a2
	lea	16(sp),a1
	movea.l	_GfxBase,a6
	jsr	-930(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_CoerceMode_stub,code

; ULONG CoerceMode(struct ViewPort * vp, ULONG monitorid, ULONG flags)
	xdef	_CoerceMode
_CoerceMode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_GfxBase,a6
	jsr	-936(a6)
	movem.l	(sp)+,a6
	rts

	section	_ChangeVPBitMap_stub,code

; VOID ChangeVPBitMap(struct ViewPort * vp, struct BitMap * bm, struct DBufInfo * db)
	xdef	_ChangeVPBitMap
_ChangeVPBitMap:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_GfxBase,a6
	jsr	-942(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_ReleasePen_stub,code

; VOID ReleasePen(struct ColorMap * cm, ULONG n)
	xdef	_ReleasePen
_ReleasePen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-948(a6)
	movem.l	(sp)+,a6
	rts

	section	_ObtainPen_stub,code

; ULONG ObtainPen(struct ColorMap * cm, ULONG n, ULONG r, ULONG g, ULONG b, LONG f)
	xdef	_ObtainPen
_ObtainPen:
	movem.l	d2/d3/d4/a6,-(sp)
	movea.l	20(sp),a0
	move.l	24(sp),d0
	move.l	28(sp),d1
	move.l	32(sp),d2
	move.l	36(sp),d3
	move.l	40(sp),d4
	movea.l	_GfxBase,a6
	jsr	-954(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_GetBitMapAttr_stub,code

; ULONG GetBitMapAttr(const struct BitMap * bm, ULONG attrnum)
	xdef	_GetBitMapAttr
_GetBitMapAttr:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d1
	movea.l	_GfxBase,a6
	jsr	-960(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocDBufInfo_stub,code

; struct DBufInfo * AllocDBufInfo(struct ViewPort * vp)
	xdef	_AllocDBufInfo
_AllocDBufInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-966(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeDBufInfo_stub,code

; VOID FreeDBufInfo(struct DBufInfo * dbi)
	xdef	_FreeDBufInfo
_FreeDBufInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_GfxBase,a6
	jsr	-972(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetOutlinePen_stub,code

; ULONG SetOutlinePen(struct RastPort * rp, ULONG pen)
	xdef	_SetOutlinePen
_SetOutlinePen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-978(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetWriteMask_stub,code

; ULONG SetWriteMask(struct RastPort * rp, ULONG msk)
	xdef	_SetWriteMask
_SetWriteMask:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-984(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetMaxPen_stub,code

; VOID SetMaxPen(struct RastPort * rp, ULONG maxpen)
	xdef	_SetMaxPen
_SetMaxPen:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_GfxBase,a6
	jsr	-990(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetRGB32CM_stub,code

; VOID SetRGB32CM(struct ColorMap * cm, ULONG n, ULONG r, ULONG g, ULONG b)
	xdef	_SetRGB32CM
_SetRGB32CM:
	movem.l	d2/d3/a6,-(sp)
	movea.l	16(sp),a0
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_GfxBase,a6
	jsr	-996(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_ScrollRasterBF_stub,code

; VOID ScrollRasterBF(struct RastPort * rp, WORD dx, WORD dy, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_ScrollRasterBF
_ScrollRasterBF:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	movea.l	24(sp),a1
	move.l	28(sp),d0
	move.l	32(sp),d1
	move.l	36(sp),d2
	move.l	40(sp),d3
	move.l	44(sp),d4
	move.l	48(sp),d5
	movea.l	_GfxBase,a6
	jsr	-1002(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_FindColor_stub,code

; LONG FindColor(struct ColorMap * cm, ULONG r, ULONG g, ULONG b, LONG maxcolor)
	xdef	_FindColor
_FindColor:
	movem.l	d2/d3/d4/a3/a6,-(sp)
	movea.l	24(sp),a3
	move.l	28(sp),d1
	move.l	32(sp),d2
	move.l	36(sp),d3
	move.l	40(sp),d4
	movea.l	_GfxBase,a6
	jsr	-1008(a6)
	movem.l	(sp)+,d2/d3/d4/a3/a6
	rts

	section	_AllocSpriteDataA_stub,code

; struct ExtSprite * AllocSpriteDataA(const struct BitMap * bm, const struct TagItem * tags)
	xdef	_AllocSpriteDataA
_AllocSpriteDataA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a2
	movea.l	16(sp),a1
	movea.l	_GfxBase,a6
	jsr	-1020(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_AllocSpriteData_stub,code

; struct ExtSprite * AllocSpriteData(const struct BitMap * bm, ULONG tags, ... )
	xdef	_AllocSpriteData
_AllocSpriteData:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a2
	lea	16(sp),a1
	movea.l	_GfxBase,a6
	jsr	-1020(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_ChangeExtSpriteA_stub,code

; LONG ChangeExtSpriteA(struct ViewPort * vp, struct ExtSprite * oldsprite, struct ExtSprite * newsprite, const struct TagItem * tags)
	xdef	_ChangeExtSpriteA
_ChangeExtSpriteA:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_GfxBase,a6
	jsr	-1026(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_ChangeExtSprite_stub,code

; LONG ChangeExtSprite(struct ViewPort * vp, struct ExtSprite * oldsprite, struct ExtSprite * newsprite, ULONG tags, ... )
	xdef	_ChangeExtSprite
_ChangeExtSprite:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	lea	28(sp),a3
	movea.l	_GfxBase,a6
	jsr	-1026(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_FreeSpriteData_stub,code

; VOID FreeSpriteData(struct ExtSprite * sp)
	xdef	_FreeSpriteData
_FreeSpriteData:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a2
	movea.l	_GfxBase,a6
	jsr	-1032(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_SetRPAttrsA_stub,code

; VOID SetRPAttrsA(struct RastPort * rp, const struct TagItem * tags)
	xdef	_SetRPAttrsA
_SetRPAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-1038(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetRPAttrs_stub,code

; VOID SetRPAttrs(struct RastPort * rp, ULONG tags, ... )
	xdef	_SetRPAttrs
_SetRPAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-1038(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetRPAttrsA_stub,code

; VOID GetRPAttrsA(const struct RastPort * rp, const struct TagItem * tags)
	xdef	_GetRPAttrsA
_GetRPAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-1044(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetRPAttrs_stub,code

; VOID GetRPAttrs(const struct RastPort * rp, ULONG tags, ... )
	xdef	_GetRPAttrs
_GetRPAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_GfxBase,a6
	jsr	-1044(a6)
	movem.l	(sp)+,a6
	rts

	section	_BestModeIDA_stub,code

; ULONG BestModeIDA(const struct TagItem * tags)
	xdef	_BestModeIDA
_BestModeIDA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-1050(a6)
	movem.l	(sp)+,a6
	rts

	section	_BestModeID_stub,code

; ULONG BestModeID(ULONG tags, ... )
	xdef	_BestModeID
_BestModeID:
	movem.l	a6,-(sp)
	lea	8(sp),a0
	movea.l	_GfxBase,a6
	jsr	-1050(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteChunkyPixels_stub,code

; VOID WriteChunkyPixels(struct RastPort * rp, UWORD xstart, UWORD ystart, UWORD xstop, UWORD ystop, const UBYTE * array, LONG bytesperrow)
	xdef	_WriteChunkyPixels
_WriteChunkyPixels:
	movem.l	d2/d3/d4/a2/a6,-(sp)
	movea.l	24(sp),a0
	move.l	28(sp),d0
	move.l	32(sp),d1
	move.l	36(sp),d2
	move.l	40(sp),d3
	movea.l	44(sp),a2
	move.l	48(sp),d4
	movea.l	_GfxBase,a6
	jsr	-1056(a6)
	movem.l	(sp)+,d2/d3/d4/a2/a6
	rts

