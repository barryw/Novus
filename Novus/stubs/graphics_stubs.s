; graphics library stubs for Novus
; Auto-generated from graphics_lib.fd

	xref	_GfxBase	; Provided by startup.o + -lamiga

	section	text,code

; BltBitMap(srcBitMap, xSrc, ySrc, destBitMap, xDest, yDest, xSize, ySize, minterm, mask, tempA)
	xdef	_BltBitMap
_BltBitMap:
	movem.l	d0-d7/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; srcBitMap
	move.l	20(sp),d0	; xSrc
	move.l	24(sp),d1	; ySrc
	move.l	28(sp),a1	; destBitMap
	move.l	32(sp),d2	; xDest
	move.l	36(sp),d3	; yDest
	move.l	40(sp),d4	; xSize
	move.l	44(sp),d5	; ySize
	move.l	48(sp),d6	; minterm
	move.l	52(sp),d7	; mask
	move.l	56(sp),a2	; tempA
	move.l	_GfxBase,a6
	jsr	-30(a6)	; BltBitMap()
	movem.l	(sp)+,d0-d7/a0-a2/a6
	rts

; BltTemplate(source, xSrc, srcMod, destRP, xDest, yDest, xSize, ySize)
	xdef	_BltTemplate
_BltTemplate:
	movem.l	d0-d5/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; source
	move.l	20(sp),d0	; xSrc
	move.l	24(sp),d1	; srcMod
	move.l	28(sp),a1	; destRP
	move.l	32(sp),d2	; xDest
	move.l	36(sp),d3	; yDest
	move.l	40(sp),d4	; xSize
	move.l	44(sp),d5	; ySize
	move.l	_GfxBase,a6
	jsr	-36(a6)	; BltTemplate()
	movem.l	(sp)+,d0-d5/a0-a1/a6
	rts

; ClearEOL(rp)
	xdef	_ClearEOL
_ClearEOL:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-42(a6)	; ClearEOL()
	movem.l	(sp)+,a1/a6
	rts

; ClearScreen(rp)
	xdef	_ClearScreen
_ClearScreen:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-48(a6)	; ClearScreen()
	movem.l	(sp)+,a1/a6
	rts

; TextLength(rp, string, count)
	xdef	_TextLength
_TextLength:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),a0	; string
	move.l	24(sp),d0	; count
	move.l	_GfxBase,a6
	jsr	-54(a6)	; TextLength()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; Text(rp, string, count)
	xdef	_Text
_Text:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),a0	; string
	move.l	24(sp),d0	; count
	move.l	_GfxBase,a6
	jsr	-60(a6)	; Text()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; SetFont(rp, textFont)
	xdef	_SetFont
_SetFont:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	16(sp),a0	; textFont
	move.l	_GfxBase,a6
	jsr	-66(a6)	; SetFont()
	movem.l	(sp)+,a0-a1/a6
	rts

; OpenFont(textAttr)
	xdef	_OpenFont
_OpenFont:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; textAttr
	move.l	_GfxBase,a6
	jsr	-72(a6)	; OpenFont()
	movem.l	(sp)+,a0/a6
	rts

; CloseFont(textFont)
	xdef	_CloseFont
_CloseFont:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; textFont
	move.l	_GfxBase,a6
	jsr	-78(a6)	; CloseFont()
	movem.l	(sp)+,a1/a6
	rts

; AskSoftStyle(rp)
	xdef	_AskSoftStyle
_AskSoftStyle:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-84(a6)	; AskSoftStyle()
	movem.l	(sp)+,a1/a6
	rts

; SetSoftStyle(rp, style, enable)
	xdef	_SetSoftStyle
_SetSoftStyle:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; style
	move.l	24(sp),d1	; enable
	move.l	_GfxBase,a6
	jsr	-90(a6)	; SetSoftStyle()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; AddBob(bob, rp)
	xdef	_AddBob
_AddBob:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; bob
	move.l	16(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-96(a6)	; AddBob()
	movem.l	(sp)+,a0-a1/a6
	rts

; AddVSprite(vSprite, rp)
	xdef	_AddVSprite
_AddVSprite:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; vSprite
	move.l	16(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-102(a6)	; AddVSprite()
	movem.l	(sp)+,a0-a1/a6
	rts

; DoCollision(rp)
	xdef	_DoCollision
_DoCollision:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-108(a6)	; DoCollision()
	movem.l	(sp)+,a1/a6
	rts

; DrawGList(rp, vp)
	xdef	_DrawGList
_DrawGList:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	16(sp),a0	; vp
	move.l	_GfxBase,a6
	jsr	-114(a6)	; DrawGList()
	movem.l	(sp)+,a0-a1/a6
	rts

; InitGels(head, tail, gelsInfo)
	xdef	_InitGels
_InitGels:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; head
	move.l	16(sp),a1	; tail
	move.l	20(sp),a2	; gelsInfo
	move.l	_GfxBase,a6
	jsr	-120(a6)	; InitGels()
	movem.l	(sp)+,a0-a2/a6
	rts

; InitMasks(vSprite)
	xdef	_InitMasks
_InitMasks:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vSprite
	move.l	_GfxBase,a6
	jsr	-126(a6)	; InitMasks()
	movem.l	(sp)+,a0/a6
	rts

; RemIBob(bob, rp, vp)
	xdef	_RemIBob
_RemIBob:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; bob
	move.l	16(sp),a1	; rp
	move.l	20(sp),a2	; vp
	move.l	_GfxBase,a6
	jsr	-132(a6)	; RemIBob()
	movem.l	(sp)+,a0-a2/a6
	rts

; RemVSprite(vSprite)
	xdef	_RemVSprite
_RemVSprite:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vSprite
	move.l	_GfxBase,a6
	jsr	-138(a6)	; RemVSprite()
	movem.l	(sp)+,a0/a6
	rts

; SetCollision(num, routine, gelsInfo)
	xdef	_SetCollision
_SetCollision:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),d0	; num
	move.l	20(sp),a0	; routine
	move.l	24(sp),a1	; gelsInfo
	move.l	_GfxBase,a6
	jsr	-144(a6)	; SetCollision()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; SortGList(rp)
	xdef	_SortGList
_SortGList:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-150(a6)	; SortGList()
	movem.l	(sp)+,a1/a6
	rts

; AddAnimOb(anOb, anKey, rp)
	xdef	_AddAnimOb
_AddAnimOb:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; anOb
	move.l	16(sp),a1	; anKey
	move.l	20(sp),a2	; rp
	move.l	_GfxBase,a6
	jsr	-156(a6)	; AddAnimOb()
	movem.l	(sp)+,a0-a2/a6
	rts

; Animate(anKey, rp)
	xdef	_Animate
_Animate:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; anKey
	move.l	16(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-162(a6)	; Animate()
	movem.l	(sp)+,a0-a1/a6
	rts

; GetGBuffers(anOb, rp, flag)
	xdef	_GetGBuffers
_GetGBuffers:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; anOb
	move.l	20(sp),a1	; rp
	move.l	24(sp),d0	; flag
	move.l	_GfxBase,a6
	jsr	-168(a6)	; GetGBuffers()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; InitGMasks(anOb)
	xdef	_InitGMasks
_InitGMasks:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; anOb
	move.l	_GfxBase,a6
	jsr	-174(a6)	; InitGMasks()
	movem.l	(sp)+,a0/a6
	rts

; DrawEllipse(rp, xCenter, yCenter, a, b)
	xdef	_DrawEllipse
_DrawEllipse:
	movem.l	d0-d3/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; xCenter
	move.l	24(sp),d1	; yCenter
	move.l	28(sp),d2	; a
	move.l	32(sp),d3	; b
	move.l	_GfxBase,a6
	jsr	-180(a6)	; DrawEllipse()
	movem.l	(sp)+,d0-d3/a1/a6
	rts

; AreaEllipse(rp, xCenter, yCenter, a, b)
	xdef	_AreaEllipse
_AreaEllipse:
	movem.l	d0-d3/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; xCenter
	move.l	24(sp),d1	; yCenter
	move.l	28(sp),d2	; a
	move.l	32(sp),d3	; b
	move.l	_GfxBase,a6
	jsr	-186(a6)	; AreaEllipse()
	movem.l	(sp)+,d0-d3/a1/a6
	rts

; LoadRGB4(vp, colors, count)
	xdef	_LoadRGB4
_LoadRGB4:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; vp
	move.l	20(sp),a1	; colors
	move.l	24(sp),d0	; count
	move.l	_GfxBase,a6
	jsr	-192(a6)	; LoadRGB4()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; InitRastPort(rp)
	xdef	_InitRastPort
_InitRastPort:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-198(a6)	; InitRastPort()
	movem.l	(sp)+,a1/a6
	rts

; InitVPort(vp)
	xdef	_InitVPort
_InitVPort:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	_GfxBase,a6
	jsr	-204(a6)	; InitVPort()
	movem.l	(sp)+,a0/a6
	rts

; MrgCop(view)
	xdef	_MrgCop
_MrgCop:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; view
	move.l	_GfxBase,a6
	jsr	-210(a6)	; MrgCop()
	movem.l	(sp)+,a1/a6
	rts

; MakeVPort(view, vp)
	xdef	_MakeVPort
_MakeVPort:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; view
	move.l	16(sp),a1	; vp
	move.l	_GfxBase,a6
	jsr	-216(a6)	; MakeVPort()
	movem.l	(sp)+,a0-a1/a6
	rts

; LoadView(view)
	xdef	_LoadView
_LoadView:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; view
	move.l	_GfxBase,a6
	jsr	-222(a6)	; LoadView()
	movem.l	(sp)+,a1/a6
	rts

; WaitBlit()
	xdef	_WaitBlit
_WaitBlit:
	movem.l	a6,-(sp)
	move.l	_GfxBase,a6
	jsr	-228(a6)	; WaitBlit()
	movem.l	(sp)+,a6
	rts

; SetRast(rp, pen)
	xdef	_SetRast
_SetRast:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; pen
	move.l	_GfxBase,a6
	jsr	-234(a6)	; SetRast()
	movem.l	(sp)+,d0/a1/a6
	rts

; Move(rp, x, y)
	xdef	_Move
_Move:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; x
	move.l	24(sp),d1	; y
	move.l	_GfxBase,a6
	jsr	-240(a6)	; Move()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; Draw(rp, x, y)
	xdef	_Draw
_Draw:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; x
	move.l	24(sp),d1	; y
	move.l	_GfxBase,a6
	jsr	-246(a6)	; Draw()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; AreaMove(rp, x, y)
	xdef	_AreaMove
_AreaMove:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; x
	move.l	24(sp),d1	; y
	move.l	_GfxBase,a6
	jsr	-252(a6)	; AreaMove()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; AreaDraw(rp, x, y)
	xdef	_AreaDraw
_AreaDraw:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; x
	move.l	24(sp),d1	; y
	move.l	_GfxBase,a6
	jsr	-258(a6)	; AreaDraw()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; AreaEnd(rp)
	xdef	_AreaEnd
_AreaEnd:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	_GfxBase,a6
	jsr	-264(a6)	; AreaEnd()
	movem.l	(sp)+,a1/a6
	rts

; WaitTOF()
	xdef	_WaitTOF
_WaitTOF:
	movem.l	a6,-(sp)
	move.l	_GfxBase,a6
	jsr	-270(a6)	; WaitTOF()
	movem.l	(sp)+,a6
	rts

; QBlit(blit)
	xdef	_QBlit
_QBlit:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; blit
	move.l	_GfxBase,a6
	jsr	-276(a6)	; QBlit()
	movem.l	(sp)+,a1/a6
	rts

; InitArea(areaInfo, vectorBuffer, maxVectors)
	xdef	_InitArea
_InitArea:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; areaInfo
	move.l	20(sp),a1	; vectorBuffer
	move.l	24(sp),d0	; maxVectors
	move.l	_GfxBase,a6
	jsr	-282(a6)	; InitArea()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; SetRGB4(vp, index, red, green, blue)
	xdef	_SetRGB4
_SetRGB4:
	movem.l	d0-d3/a0/a6,-(sp)
	move.l	16(sp),a0	; vp
	move.l	20(sp),d0	; index
	move.l	24(sp),d1	; red
	move.l	28(sp),d2	; green
	move.l	32(sp),d3	; blue
	move.l	_GfxBase,a6
	jsr	-288(a6)	; SetRGB4()
	movem.l	(sp)+,d0-d3/a0/a6
	rts

; QBSBlit(blit)
	xdef	_QBSBlit
_QBSBlit:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; blit
	move.l	_GfxBase,a6
	jsr	-294(a6)	; QBSBlit()
	movem.l	(sp)+,a1/a6
	rts

; BltClear(memBlock, byteCount, flags)
	xdef	_BltClear
_BltClear:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; memBlock
	move.l	20(sp),d0	; byteCount
	move.l	24(sp),d1	; flags
	move.l	_GfxBase,a6
	jsr	-300(a6)	; BltClear()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; RectFill(rp, xMin, yMin, xMax, yMax)
	xdef	_RectFill
_RectFill:
	movem.l	d0-d3/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; xMin
	move.l	24(sp),d1	; yMin
	move.l	28(sp),d2	; xMax
	move.l	32(sp),d3	; yMax
	move.l	_GfxBase,a6
	jsr	-306(a6)	; RectFill()
	movem.l	(sp)+,d0-d3/a1/a6
	rts

; BltPattern(rp, mask, xMin, yMin, xMax, yMax, maskBPR)
	xdef	_BltPattern
_BltPattern:
	movem.l	d0-d4/a0-a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),a0	; mask
	move.l	24(sp),d0	; xMin
	move.l	28(sp),d1	; yMin
	move.l	32(sp),d2	; xMax
	move.l	36(sp),d3	; yMax
	move.l	40(sp),d4	; maskBPR
	move.l	_GfxBase,a6
	jsr	-312(a6)	; BltPattern()
	movem.l	(sp)+,d0-d4/a0-a1/a6
	rts

; ReadPixel(rp, x, y)
	xdef	_ReadPixel
_ReadPixel:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; x
	move.l	24(sp),d1	; y
	move.l	_GfxBase,a6
	jsr	-318(a6)	; ReadPixel()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; WritePixel(rp, x, y)
	xdef	_WritePixel
_WritePixel:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; x
	move.l	24(sp),d1	; y
	move.l	_GfxBase,a6
	jsr	-324(a6)	; WritePixel()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; Flood(rp, mode, x, y)
	xdef	_Flood
_Flood:
	movem.l	d0-d2/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d2	; mode
	move.l	24(sp),d0	; x
	move.l	28(sp),d1	; y
	move.l	_GfxBase,a6
	jsr	-330(a6)	; Flood()
	movem.l	(sp)+,d0-d2/a1/a6
	rts

; PolyDraw(rp, count, polyTable)
	xdef	_PolyDraw
_PolyDraw:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; count
	move.l	24(sp),a0	; polyTable
	move.l	_GfxBase,a6
	jsr	-336(a6)	; PolyDraw()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; SetAPen(rp, pen)
	xdef	_SetAPen
_SetAPen:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; pen
	move.l	_GfxBase,a6
	jsr	-342(a6)	; SetAPen()
	movem.l	(sp)+,d0/a1/a6
	rts

; SetBPen(rp, pen)
	xdef	_SetBPen
_SetBPen:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; pen
	move.l	_GfxBase,a6
	jsr	-348(a6)	; SetBPen()
	movem.l	(sp)+,d0/a1/a6
	rts

; SetDrMd(rp, drawMode)
	xdef	_SetDrMd
_SetDrMd:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; drawMode
	move.l	_GfxBase,a6
	jsr	-354(a6)	; SetDrMd()
	movem.l	(sp)+,d0/a1/a6
	rts

; InitView(view)
	xdef	_InitView
_InitView:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; view
	move.l	_GfxBase,a6
	jsr	-360(a6)	; InitView()
	movem.l	(sp)+,a1/a6
	rts

; CBump(copList)
	xdef	_CBump
_CBump:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; copList
	move.l	_GfxBase,a6
	jsr	-366(a6)	; CBump()
	movem.l	(sp)+,a1/a6
	rts

; CMove(copList, destination, data)
	xdef	_CMove
_CMove:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; copList
	move.l	20(sp),d0	; destination
	move.l	24(sp),d1	; data
	move.l	_GfxBase,a6
	jsr	-372(a6)	; CMove()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; CWait(copList, v, h)
	xdef	_CWait
_CWait:
	movem.l	d0-d1/a1/a6,-(sp)
	move.l	16(sp),a1	; copList
	move.l	20(sp),d0	; v
	move.l	24(sp),d1	; h
	move.l	_GfxBase,a6
	jsr	-378(a6)	; CWait()
	movem.l	(sp)+,d0-d1/a1/a6
	rts

; VBeamPos()
	xdef	_VBeamPos
_VBeamPos:
	movem.l	a6,-(sp)
	move.l	_GfxBase,a6
	jsr	-384(a6)	; VBeamPos()
	movem.l	(sp)+,a6
	rts

; InitBitMap(bitMap, depth, width, height)
	xdef	_InitBitMap
_InitBitMap:
	movem.l	d0-d2/a0/a6,-(sp)
	move.l	16(sp),a0	; bitMap
	move.l	20(sp),d0	; depth
	move.l	24(sp),d1	; width
	move.l	28(sp),d2	; height
	move.l	_GfxBase,a6
	jsr	-390(a6)	; InitBitMap()
	movem.l	(sp)+,d0-d2/a0/a6
	rts

; ScrollRaster(rp, dx, dy, xMin, yMin, xMax, yMax)
	xdef	_ScrollRaster
_ScrollRaster:
	movem.l	d0-d5/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; dx
	move.l	24(sp),d1	; dy
	move.l	28(sp),d2	; xMin
	move.l	32(sp),d3	; yMin
	move.l	36(sp),d4	; xMax
	move.l	40(sp),d5	; yMax
	move.l	_GfxBase,a6
	jsr	-396(a6)	; ScrollRaster()
	movem.l	(sp)+,d0-d5/a1/a6
	rts

; WaitBOVP(vp)
	xdef	_WaitBOVP
_WaitBOVP:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	_GfxBase,a6
	jsr	-402(a6)	; WaitBOVP()
	movem.l	(sp)+,a0/a6
	rts

; GetSprite(sprite, num)
	xdef	_GetSprite
_GetSprite:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; sprite
	move.l	20(sp),d0	; num
	move.l	_GfxBase,a6
	jsr	-408(a6)	; GetSprite()
	movem.l	(sp)+,d0/a0/a6
	rts

; FreeSprite(num)
	xdef	_FreeSprite
_FreeSprite:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; num
	move.l	_GfxBase,a6
	jsr	-414(a6)	; FreeSprite()
	movem.l	(sp)+,d0/a6
	rts

; ChangeSprite(vp, sprite, newData)
	xdef	_ChangeSprite
_ChangeSprite:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	16(sp),a1	; sprite
	move.l	20(sp),a2	; newData
	move.l	_GfxBase,a6
	jsr	-420(a6)	; ChangeSprite()
	movem.l	(sp)+,a0-a2/a6
	rts

; MoveSprite(vp, sprite, x, y)
	xdef	_MoveSprite
_MoveSprite:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; vp
	move.l	20(sp),a1	; sprite
	move.l	24(sp),d0	; x
	move.l	28(sp),d1	; y
	move.l	_GfxBase,a6
	jsr	-426(a6)	; MoveSprite()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; LockLayerRom(layer)
	xdef	_LockLayerRom
_LockLayerRom:
	movem.l	a5/a6,-(sp)
	move.l	12(sp),a5	; layer
	move.l	_GfxBase,a6
	jsr	-432(a6)	; LockLayerRom()
	movem.l	(sp)+,a5/a6
	rts

; UnlockLayerRom(layer)
	xdef	_UnlockLayerRom
_UnlockLayerRom:
	movem.l	a5/a6,-(sp)
	move.l	12(sp),a5	; layer
	move.l	_GfxBase,a6
	jsr	-438(a6)	; UnlockLayerRom()
	movem.l	(sp)+,a5/a6
	rts

; SyncSBitMap(layer)
	xdef	_SyncSBitMap
_SyncSBitMap:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; layer
	move.l	_GfxBase,a6
	jsr	-444(a6)	; SyncSBitMap()
	movem.l	(sp)+,a0/a6
	rts

; CopySBitMap(layer)
	xdef	_CopySBitMap
_CopySBitMap:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; layer
	move.l	_GfxBase,a6
	jsr	-450(a6)	; CopySBitMap()
	movem.l	(sp)+,a0/a6
	rts

; OwnBlitter()
	xdef	_OwnBlitter
_OwnBlitter:
	movem.l	a6,-(sp)
	move.l	_GfxBase,a6
	jsr	-456(a6)	; OwnBlitter()
	movem.l	(sp)+,a6
	rts

; DisownBlitter()
	xdef	_DisownBlitter
_DisownBlitter:
	movem.l	a6,-(sp)
	move.l	_GfxBase,a6
	jsr	-462(a6)	; DisownBlitter()
	movem.l	(sp)+,a6
	rts

; InitTmpRas(tmpRas, buffer, size)
	xdef	_InitTmpRas
_InitTmpRas:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; tmpRas
	move.l	20(sp),a1	; buffer
	move.l	24(sp),d0	; size
	move.l	_GfxBase,a6
	jsr	-468(a6)	; InitTmpRas()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; AskFont(rp, textAttr)
	xdef	_AskFont
_AskFont:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a1	; rp
	move.l	16(sp),a0	; textAttr
	move.l	_GfxBase,a6
	jsr	-474(a6)	; AskFont()
	movem.l	(sp)+,a0-a1/a6
	rts

; AddFont(textFont)
	xdef	_AddFont
_AddFont:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; textFont
	move.l	_GfxBase,a6
	jsr	-480(a6)	; AddFont()
	movem.l	(sp)+,a1/a6
	rts

; RemFont(textFont)
	xdef	_RemFont
_RemFont:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; textFont
	move.l	_GfxBase,a6
	jsr	-486(a6)	; RemFont()
	movem.l	(sp)+,a1/a6
	rts

; AllocRaster(width, height)
	xdef	_AllocRaster
_AllocRaster:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; width
	move.l	16(sp),d1	; height
	move.l	_GfxBase,a6
	jsr	-492(a6)	; AllocRaster()
	movem.l	(sp)+,d0-d1/a6
	rts

; FreeRaster(p, width, height)
	xdef	_FreeRaster
_FreeRaster:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; p
	move.l	20(sp),d0	; width
	move.l	24(sp),d1	; height
	move.l	_GfxBase,a6
	jsr	-498(a6)	; FreeRaster()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; AndRectRegion(region, rectangle)
	xdef	_AndRectRegion
_AndRectRegion:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; region
	move.l	16(sp),a1	; rectangle
	move.l	_GfxBase,a6
	jsr	-504(a6)	; AndRectRegion()
	movem.l	(sp)+,a0-a1/a6
	rts

; OrRectRegion(region, rectangle)
	xdef	_OrRectRegion
_OrRectRegion:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; region
	move.l	16(sp),a1	; rectangle
	move.l	_GfxBase,a6
	jsr	-510(a6)	; OrRectRegion()
	movem.l	(sp)+,a0-a1/a6
	rts

; NewRegion()
	xdef	_NewRegion
_NewRegion:
	movem.l	a6,-(sp)
	move.l	_GfxBase,a6
	jsr	-516(a6)	; NewRegion()
	movem.l	(sp)+,a6
	rts

; ClearRectRegion(region, rectangle)
	xdef	_ClearRectRegion
_ClearRectRegion:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; region
	move.l	16(sp),a1	; rectangle
	move.l	_GfxBase,a6
	jsr	-522(a6)	; ClearRectRegion()
	movem.l	(sp)+,a0-a1/a6
	rts

; ClearRegion(region)
	xdef	_ClearRegion
_ClearRegion:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; region
	move.l	_GfxBase,a6
	jsr	-528(a6)	; ClearRegion()
	movem.l	(sp)+,a0/a6
	rts

; DisposeRegion(region)
	xdef	_DisposeRegion
_DisposeRegion:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; region
	move.l	_GfxBase,a6
	jsr	-534(a6)	; DisposeRegion()
	movem.l	(sp)+,a0/a6
	rts

; FreeVPortCopLists(vp)
	xdef	_FreeVPortCopLists
_FreeVPortCopLists:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	_GfxBase,a6
	jsr	-540(a6)	; FreeVPortCopLists()
	movem.l	(sp)+,a0/a6
	rts

; FreeCopList(copList)
	xdef	_FreeCopList
_FreeCopList:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; copList
	move.l	_GfxBase,a6
	jsr	-546(a6)	; FreeCopList()
	movem.l	(sp)+,a0/a6
	rts

; ClipBlit(srcRP, xSrc, ySrc, destRP, xDest, yDest, xSize, ySize, minterm)
	xdef	_ClipBlit
_ClipBlit:
	movem.l	d0-d6/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; srcRP
	move.l	20(sp),d0	; xSrc
	move.l	24(sp),d1	; ySrc
	move.l	28(sp),a1	; destRP
	move.l	32(sp),d2	; xDest
	move.l	36(sp),d3	; yDest
	move.l	40(sp),d4	; xSize
	move.l	44(sp),d5	; ySize
	move.l	48(sp),d6	; minterm
	move.l	_GfxBase,a6
	jsr	-552(a6)	; ClipBlit()
	movem.l	(sp)+,d0-d6/a0-a1/a6
	rts

; XorRectRegion(region, rectangle)
	xdef	_XorRectRegion
_XorRectRegion:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; region
	move.l	16(sp),a1	; rectangle
	move.l	_GfxBase,a6
	jsr	-558(a6)	; XorRectRegion()
	movem.l	(sp)+,a0-a1/a6
	rts

; FreeCprList(cprList)
	xdef	_FreeCprList
_FreeCprList:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; cprList
	move.l	_GfxBase,a6
	jsr	-564(a6)	; FreeCprList()
	movem.l	(sp)+,a0/a6
	rts

; GetColorMap(entries)
	xdef	_GetColorMap
_GetColorMap:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; entries
	move.l	_GfxBase,a6
	jsr	-570(a6)	; GetColorMap()
	movem.l	(sp)+,d0/a6
	rts

; FreeColorMap(colorMap)
	xdef	_FreeColorMap
_FreeColorMap:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; colorMap
	move.l	_GfxBase,a6
	jsr	-576(a6)	; FreeColorMap()
	movem.l	(sp)+,a0/a6
	rts

; GetRGB4(colorMap, entry)
	xdef	_GetRGB4
_GetRGB4:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; colorMap
	move.l	20(sp),d0	; entry
	move.l	_GfxBase,a6
	jsr	-582(a6)	; GetRGB4()
	movem.l	(sp)+,d0/a0/a6
	rts

; ScrollVPort(vp)
	xdef	_ScrollVPort
_ScrollVPort:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	_GfxBase,a6
	jsr	-588(a6)	; ScrollVPort()
	movem.l	(sp)+,a0/a6
	rts

; UCopperListInit(uCopList, n)
	xdef	_UCopperListInit
_UCopperListInit:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; uCopList
	move.l	20(sp),d0	; n
	move.l	_GfxBase,a6
	jsr	-594(a6)	; UCopperListInit()
	movem.l	(sp)+,d0/a0/a6
	rts

; FreeGBuffers(anOb, rp, flag)
	xdef	_FreeGBuffers
_FreeGBuffers:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; anOb
	move.l	20(sp),a1	; rp
	move.l	24(sp),d0	; flag
	move.l	_GfxBase,a6
	jsr	-600(a6)	; FreeGBuffers()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; BltBitMapRastPort(srcBitMap, xSrc, ySrc, destRP, xDest, yDest, xSize, ySize, minterm)
	xdef	_BltBitMapRastPort
_BltBitMapRastPort:
	movem.l	d0-d6/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; srcBitMap
	move.l	20(sp),d0	; xSrc
	move.l	24(sp),d1	; ySrc
	move.l	28(sp),a1	; destRP
	move.l	32(sp),d2	; xDest
	move.l	36(sp),d3	; yDest
	move.l	40(sp),d4	; xSize
	move.l	44(sp),d5	; ySize
	move.l	48(sp),d6	; minterm
	move.l	_GfxBase,a6
	jsr	-606(a6)	; BltBitMapRastPort()
	movem.l	(sp)+,d0-d6/a0-a1/a6
	rts

; OrRegionRegion(srcRegion, destRegion)
	xdef	_OrRegionRegion
_OrRegionRegion:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; srcRegion
	move.l	16(sp),a1	; destRegion
	move.l	_GfxBase,a6
	jsr	-612(a6)	; OrRegionRegion()
	movem.l	(sp)+,a0-a1/a6
	rts

; XorRegionRegion(srcRegion, destRegion)
	xdef	_XorRegionRegion
_XorRegionRegion:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; srcRegion
	move.l	16(sp),a1	; destRegion
	move.l	_GfxBase,a6
	jsr	-618(a6)	; XorRegionRegion()
	movem.l	(sp)+,a0-a1/a6
	rts

; AndRegionRegion(srcRegion, destRegion)
	xdef	_AndRegionRegion
_AndRegionRegion:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; srcRegion
	move.l	16(sp),a1	; destRegion
	move.l	_GfxBase,a6
	jsr	-624(a6)	; AndRegionRegion()
	movem.l	(sp)+,a0-a1/a6
	rts

; SetRGB4CM(colorMap, index, red, green, blue)
	xdef	_SetRGB4CM
_SetRGB4CM:
	movem.l	d0-d3/a0/a6,-(sp)
	move.l	16(sp),a0	; colorMap
	move.l	20(sp),d0	; index
	move.l	24(sp),d1	; red
	move.l	28(sp),d2	; green
	move.l	32(sp),d3	; blue
	move.l	_GfxBase,a6
	jsr	-630(a6)	; SetRGB4CM()
	movem.l	(sp)+,d0-d3/a0/a6
	rts

; BltMaskBitMapRastPort(srcBitMap, xSrc, ySrc, destRP, xDest, yDest, xSize, ySize, minterm, bltMask)
	xdef	_BltMaskBitMapRastPort
_BltMaskBitMapRastPort:
	movem.l	d0-d6/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; srcBitMap
	move.l	20(sp),d0	; xSrc
	move.l	24(sp),d1	; ySrc
	move.l	28(sp),a1	; destRP
	move.l	32(sp),d2	; xDest
	move.l	36(sp),d3	; yDest
	move.l	40(sp),d4	; xSize
	move.l	44(sp),d5	; ySize
	move.l	48(sp),d6	; minterm
	move.l	52(sp),a2	; bltMask
	move.l	_GfxBase,a6
	jsr	-636(a6)	; BltMaskBitMapRastPort()
	movem.l	(sp)+,d0-d6/a0-a2/a6
	rts

; AttemptLockLayerRom(layer)
	xdef	_AttemptLockLayerRom
_AttemptLockLayerRom:
	movem.l	a5/a6,-(sp)
	move.l	12(sp),a5	; layer
	move.l	_GfxBase,a6
	jsr	-654(a6)	; AttemptLockLayerRom()
	movem.l	(sp)+,a5/a6
	rts

; GfxNew(gfxNodeType)
	xdef	_GfxNew
_GfxNew:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; gfxNodeType
	move.l	_GfxBase,a6
	jsr	-660(a6)	; GfxNew()
	movem.l	(sp)+,d0/a6
	rts

; GfxFree(gfxNodePtr)
	xdef	_GfxFree
_GfxFree:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; gfxNodePtr
	move.l	_GfxBase,a6
	jsr	-666(a6)	; GfxFree()
	movem.l	(sp)+,a0/a6
	rts

; GfxAssociate(associateNode, gfxNodePtr)
	xdef	_GfxAssociate
_GfxAssociate:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; associateNode
	move.l	16(sp),a1	; gfxNodePtr
	move.l	_GfxBase,a6
	jsr	-672(a6)	; GfxAssociate()
	movem.l	(sp)+,a0-a1/a6
	rts

; BitMapScale(bitScaleArgs)
	xdef	_BitMapScale
_BitMapScale:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; bitScaleArgs
	move.l	_GfxBase,a6
	jsr	-678(a6)	; BitMapScale()
	movem.l	(sp)+,a0/a6
	rts

; ScalerDiv(factor, numerator, denominator)
	xdef	_ScalerDiv
_ScalerDiv:
	movem.l	d0-d2/a6,-(sp)
	move.l	12(sp),d0	; factor
	move.l	16(sp),d1	; numerator
	move.l	20(sp),d2	; denominator
	move.l	_GfxBase,a6
	jsr	-684(a6)	; ScalerDiv()
	movem.l	(sp)+,d0-d2/a6
	rts

; TextExtent(rp, string, count, textExtent)
	xdef	_TextExtent
_TextExtent:
	movem.l	d0/a0-a2/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),a0	; string
	move.l	24(sp),d0	; count
	move.l	28(sp),a2	; textExtent
	move.l	_GfxBase,a6
	jsr	-690(a6)	; TextExtent()
	movem.l	(sp)+,d0/a0-a2/a6
	rts

; TextFit(rp, string, strLen, textExtent, constrainingExtent, strDirection, constrainingBitWidth, constrainingBitHeight)
	xdef	_TextFit
_TextFit:
	movem.l	d0-d3/a0-a3/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),a0	; string
	move.l	24(sp),d0	; strLen
	move.l	28(sp),a2	; textExtent
	move.l	32(sp),a3	; constrainingExtent
	move.l	36(sp),d1	; strDirection
	move.l	40(sp),d2	; constrainingBitWidth
	move.l	44(sp),d3	; constrainingBitHeight
	move.l	_GfxBase,a6
	jsr	-696(a6)	; TextFit()
	movem.l	(sp)+,d0-d3/a0-a3/a6
	rts

; GfxLookUp(associateNode)
	xdef	_GfxLookUp
_GfxLookUp:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; associateNode
	move.l	_GfxBase,a6
	jsr	-702(a6)	; GfxLookUp()
	movem.l	(sp)+,a0/a6
	rts

; VideoControl(colorMap, tagarray)
	xdef	_VideoControl
_VideoControl:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; colorMap
	move.l	16(sp),a1	; tagarray
	move.l	_GfxBase,a6
	jsr	-708(a6)	; VideoControl()
	movem.l	(sp)+,a0-a1/a6
	rts

; OpenMonitor(monitorName, displayID)
	xdef	_OpenMonitor
_OpenMonitor:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),a1	; monitorName
	move.l	20(sp),d0	; displayID
	move.l	_GfxBase,a6
	jsr	-714(a6)	; OpenMonitor()
	movem.l	(sp)+,d0/a1/a6
	rts

; CloseMonitor(monitorSpec)
	xdef	_CloseMonitor
_CloseMonitor:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; monitorSpec
	move.l	_GfxBase,a6
	jsr	-720(a6)	; CloseMonitor()
	movem.l	(sp)+,a0/a6
	rts

; FindDisplayInfo(displayID)
	xdef	_FindDisplayInfo
_FindDisplayInfo:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; displayID
	move.l	_GfxBase,a6
	jsr	-726(a6)	; FindDisplayInfo()
	movem.l	(sp)+,d0/a6
	rts

; NextDisplayInfo(displayID)
	xdef	_NextDisplayInfo
_NextDisplayInfo:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; displayID
	move.l	_GfxBase,a6
	jsr	-732(a6)	; NextDisplayInfo()
	movem.l	(sp)+,d0/a6
	rts

; GetDisplayInfoData(handle, buf, size, tagID, displayID)
	xdef	_GetDisplayInfoData
_GetDisplayInfoData:
	movem.l	d0-d2/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; handle
	move.l	20(sp),a1	; buf
	move.l	24(sp),d0	; size
	move.l	28(sp),d1	; tagID
	move.l	32(sp),d2	; displayID
	move.l	_GfxBase,a6
	jsr	-756(a6)	; GetDisplayInfoData()
	movem.l	(sp)+,d0-d2/a0-a1/a6
	rts

; FontExtent(font, fontExtent)
	xdef	_FontExtent
_FontExtent:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; font
	move.l	16(sp),a1	; fontExtent
	move.l	_GfxBase,a6
	jsr	-762(a6)	; FontExtent()
	movem.l	(sp)+,a0-a1/a6
	rts

; ReadPixelLine8(rp, xstart, ystart, width, array, tempRP)
	xdef	_ReadPixelLine8
_ReadPixelLine8:
	movem.l	d0-d2/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),d0	; xstart
	move.l	24(sp),d1	; ystart
	move.l	28(sp),d2	; width
	move.l	32(sp),a2	; array
	move.l	36(sp),a1	; tempRP
	move.l	_GfxBase,a6
	jsr	-768(a6)	; ReadPixelLine8()
	movem.l	(sp)+,d0-d2/a0-a2/a6
	rts

; WritePixelLine8(rp, xstart, ystart, width, array, tempRP)
	xdef	_WritePixelLine8
_WritePixelLine8:
	movem.l	d0-d2/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),d0	; xstart
	move.l	24(sp),d1	; ystart
	move.l	28(sp),d2	; width
	move.l	32(sp),a2	; array
	move.l	36(sp),a1	; tempRP
	move.l	_GfxBase,a6
	jsr	-774(a6)	; WritePixelLine8()
	movem.l	(sp)+,d0-d2/a0-a2/a6
	rts

; ReadPixelArray8(rp, xstart, ystart, xstop, ystop, array, temprp)
	xdef	_ReadPixelArray8
_ReadPixelArray8:
	movem.l	d0-d3/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),d0	; xstart
	move.l	24(sp),d1	; ystart
	move.l	28(sp),d2	; xstop
	move.l	32(sp),d3	; ystop
	move.l	36(sp),a2	; array
	move.l	40(sp),a1	; temprp
	move.l	_GfxBase,a6
	jsr	-780(a6)	; ReadPixelArray8()
	movem.l	(sp)+,d0-d3/a0-a2/a6
	rts

; WritePixelArray8(rp, xstart, ystart, xstop, ystop, array, temprp)
	xdef	_WritePixelArray8
_WritePixelArray8:
	movem.l	d0-d3/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),d0	; xstart
	move.l	24(sp),d1	; ystart
	move.l	28(sp),d2	; xstop
	move.l	32(sp),d3	; ystop
	move.l	36(sp),a2	; array
	move.l	40(sp),a1	; temprp
	move.l	_GfxBase,a6
	jsr	-786(a6)	; WritePixelArray8()
	movem.l	(sp)+,d0-d3/a0-a2/a6
	rts

; GetVPModeID(vp)
	xdef	_GetVPModeID
_GetVPModeID:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	_GfxBase,a6
	jsr	-792(a6)	; GetVPModeID()
	movem.l	(sp)+,a0/a6
	rts

; ModeNotAvailable(modeID)
	xdef	_ModeNotAvailable
_ModeNotAvailable:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; modeID
	move.l	_GfxBase,a6
	jsr	-798(a6)	; ModeNotAvailable()
	movem.l	(sp)+,d0/a6
	rts

; EraseRect(rp, xMin, yMin, xMax, yMax)
	xdef	_EraseRect
_EraseRect:
	movem.l	d0-d3/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; xMin
	move.l	24(sp),d1	; yMin
	move.l	28(sp),d2	; xMax
	move.l	32(sp),d3	; yMax
	move.l	_GfxBase,a6
	jsr	-810(a6)	; EraseRect()
	movem.l	(sp)+,d0-d3/a1/a6
	rts

; ExtendFont(font, fontTags)
	xdef	_ExtendFont
_ExtendFont:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; font
	move.l	16(sp),a1	; fontTags
	move.l	_GfxBase,a6
	jsr	-816(a6)	; ExtendFont()
	movem.l	(sp)+,a0-a1/a6
	rts

; StripFont(font)
	xdef	_StripFont
_StripFont:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; font
	move.l	_GfxBase,a6
	jsr	-822(a6)	; StripFont()
	movem.l	(sp)+,a0/a6
	rts

; CalcIVG(v, vp)
	xdef	_CalcIVG
_CalcIVG:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; v
	move.l	16(sp),a1	; vp
	move.l	_GfxBase,a6
	jsr	-828(a6)	; CalcIVG()
	movem.l	(sp)+,a0-a1/a6
	rts

; AttachPalExtra(cm, vp)
	xdef	_AttachPalExtra
_AttachPalExtra:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; cm
	move.l	16(sp),a1	; vp
	move.l	_GfxBase,a6
	jsr	-834(a6)	; AttachPalExtra()
	movem.l	(sp)+,a0-a1/a6
	rts

; ObtainBestPenA(cm, r, g, b, tags)
	xdef	_ObtainBestPenA
_ObtainBestPenA:
	movem.l	d1-d3/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; cm
	move.l	20(sp),d1	; r
	move.l	24(sp),d2	; g
	move.l	28(sp),d3	; b
	move.l	32(sp),a1	; tags
	move.l	_GfxBase,a6
	jsr	-840(a6)	; ObtainBestPenA()
	movem.l	(sp)+,d1-d3/a0-a1/a6
	rts

; SetRGB32(vp, n, r, g, b)
	xdef	_SetRGB32
_SetRGB32:
	movem.l	d0-d3/a0/a6,-(sp)
	move.l	16(sp),a0	; vp
	move.l	20(sp),d0	; n
	move.l	24(sp),d1	; r
	move.l	28(sp),d2	; g
	move.l	32(sp),d3	; b
	move.l	_GfxBase,a6
	jsr	-852(a6)	; SetRGB32()
	movem.l	(sp)+,d0-d3/a0/a6
	rts

; GetAPen(rp)
	xdef	_GetAPen
_GetAPen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	_GfxBase,a6
	jsr	-858(a6)	; GetAPen()
	movem.l	(sp)+,a0/a6
	rts

; GetBPen(rp)
	xdef	_GetBPen
_GetBPen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	_GfxBase,a6
	jsr	-864(a6)	; GetBPen()
	movem.l	(sp)+,a0/a6
	rts

; GetDrMd(rp)
	xdef	_GetDrMd
_GetDrMd:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	_GfxBase,a6
	jsr	-870(a6)	; GetDrMd()
	movem.l	(sp)+,a0/a6
	rts

; GetOutlinePen(rp)
	xdef	_GetOutlinePen
_GetOutlinePen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	_GfxBase,a6
	jsr	-876(a6)	; GetOutlinePen()
	movem.l	(sp)+,a0/a6
	rts

; LoadRGB32(vp, table)
	xdef	_LoadRGB32
_LoadRGB32:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	16(sp),a1	; table
	move.l	_GfxBase,a6
	jsr	-882(a6)	; LoadRGB32()
	movem.l	(sp)+,a0-a1/a6
	rts

; SetChipRev(want)
	xdef	_SetChipRev
_SetChipRev:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; want
	move.l	_GfxBase,a6
	jsr	-888(a6)	; SetChipRev()
	movem.l	(sp)+,d0/a6
	rts

; SetABPenDrMd(rp, apen, bpen, drawmode)
	xdef	_SetABPenDrMd
_SetABPenDrMd:
	movem.l	d0-d2/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; apen
	move.l	24(sp),d1	; bpen
	move.l	28(sp),d2	; drawmode
	move.l	_GfxBase,a6
	jsr	-894(a6)	; SetABPenDrMd()
	movem.l	(sp)+,d0-d2/a1/a6
	rts

; GetRGB32(cm, firstcolor, ncolors, table)
	xdef	_GetRGB32
_GetRGB32:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; cm
	move.l	20(sp),d0	; firstcolor
	move.l	24(sp),d1	; ncolors
	move.l	28(sp),a1	; table
	move.l	_GfxBase,a6
	jsr	-900(a6)	; GetRGB32()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; AllocBitMap(sizex, sizey, depth, flags, friend_bitmap)
	xdef	_AllocBitMap
_AllocBitMap:
	movem.l	d0-d3/a0/a6,-(sp)
	move.l	16(sp),d0	; sizex
	move.l	20(sp),d1	; sizey
	move.l	24(sp),d2	; depth
	move.l	28(sp),d3	; flags
	move.l	32(sp),a0	; friend_bitmap
	move.l	_GfxBase,a6
	jsr	-918(a6)	; AllocBitMap()
	movem.l	(sp)+,d0-d3/a0/a6
	rts

; FreeBitMap(bm)
	xdef	_FreeBitMap
_FreeBitMap:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; bm
	move.l	_GfxBase,a6
	jsr	-924(a6)	; FreeBitMap()
	movem.l	(sp)+,a0/a6
	rts

; GetExtSpriteA(ss, tags)
	xdef	_GetExtSpriteA
_GetExtSpriteA:
	movem.l	a1-a2/a6,-(sp)
	move.l	12(sp),a2	; ss
	move.l	16(sp),a1	; tags
	move.l	_GfxBase,a6
	jsr	-930(a6)	; GetExtSpriteA()
	movem.l	(sp)+,a1-a2/a6
	rts

; CoerceMode(vp, monitorid, flags)
	xdef	_CoerceMode
_CoerceMode:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; vp
	move.l	20(sp),d0	; monitorid
	move.l	24(sp),d1	; flags
	move.l	_GfxBase,a6
	jsr	-936(a6)	; CoerceMode()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; ChangeVPBitMap(vp, bm, db)
	xdef	_ChangeVPBitMap
_ChangeVPBitMap:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	16(sp),a1	; bm
	move.l	20(sp),a2	; db
	move.l	_GfxBase,a6
	jsr	-942(a6)	; ChangeVPBitMap()
	movem.l	(sp)+,a0-a2/a6
	rts

; ReleasePen(cm, n)
	xdef	_ReleasePen
_ReleasePen:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; cm
	move.l	20(sp),d0	; n
	move.l	_GfxBase,a6
	jsr	-948(a6)	; ReleasePen()
	movem.l	(sp)+,d0/a0/a6
	rts

; ObtainPen(cm, n, r, g, b, f)
	xdef	_ObtainPen
_ObtainPen:
	movem.l	d0-d4/a0/a6,-(sp)
	move.l	16(sp),a0	; cm
	move.l	20(sp),d0	; n
	move.l	24(sp),d1	; r
	move.l	28(sp),d2	; g
	move.l	32(sp),d3	; b
	move.l	36(sp),d4	; f
	move.l	_GfxBase,a6
	jsr	-954(a6)	; ObtainPen()
	movem.l	(sp)+,d0-d4/a0/a6
	rts

; GetBitMapAttr(bm, attrnum)
	xdef	_GetBitMapAttr
_GetBitMapAttr:
	movem.l	d1/a0/a6,-(sp)
	move.l	16(sp),a0	; bm
	move.l	20(sp),d1	; attrnum
	move.l	_GfxBase,a6
	jsr	-960(a6)	; GetBitMapAttr()
	movem.l	(sp)+,d1/a0/a6
	rts

; AllocDBufInfo(vp)
	xdef	_AllocDBufInfo
_AllocDBufInfo:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	_GfxBase,a6
	jsr	-966(a6)	; AllocDBufInfo()
	movem.l	(sp)+,a0/a6
	rts

; FreeDBufInfo(dbi)
	xdef	_FreeDBufInfo
_FreeDBufInfo:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; dbi
	move.l	_GfxBase,a6
	jsr	-972(a6)	; FreeDBufInfo()
	movem.l	(sp)+,a1/a6
	rts

; SetOutlinePen(rp, pen)
	xdef	_SetOutlinePen
_SetOutlinePen:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),d0	; pen
	move.l	_GfxBase,a6
	jsr	-978(a6)	; SetOutlinePen()
	movem.l	(sp)+,d0/a0/a6
	rts

; SetWriteMask(rp, msk)
	xdef	_SetWriteMask
_SetWriteMask:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),d0	; msk
	move.l	_GfxBase,a6
	jsr	-984(a6)	; SetWriteMask()
	movem.l	(sp)+,d0/a0/a6
	rts

; SetMaxPen(rp, maxpen)
	xdef	_SetMaxPen
_SetMaxPen:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),d0	; maxpen
	move.l	_GfxBase,a6
	jsr	-990(a6)	; SetMaxPen()
	movem.l	(sp)+,d0/a0/a6
	rts

; SetRGB32CM(cm, n, r, g, b)
	xdef	_SetRGB32CM
_SetRGB32CM:
	movem.l	d0-d3/a0/a6,-(sp)
	move.l	16(sp),a0	; cm
	move.l	20(sp),d0	; n
	move.l	24(sp),d1	; r
	move.l	28(sp),d2	; g
	move.l	32(sp),d3	; b
	move.l	_GfxBase,a6
	jsr	-996(a6)	; SetRGB32CM()
	movem.l	(sp)+,d0-d3/a0/a6
	rts

; ScrollRasterBF(rp, dx, dy, xMin, yMin, xMax, yMax)
	xdef	_ScrollRasterBF
_ScrollRasterBF:
	movem.l	d0-d5/a1/a6,-(sp)
	move.l	16(sp),a1	; rp
	move.l	20(sp),d0	; dx
	move.l	24(sp),d1	; dy
	move.l	28(sp),d2	; xMin
	move.l	32(sp),d3	; yMin
	move.l	36(sp),d4	; xMax
	move.l	40(sp),d5	; yMax
	move.l	_GfxBase,a6
	jsr	-1002(a6)	; ScrollRasterBF()
	movem.l	(sp)+,d0-d5/a1/a6
	rts

; FindColor(cm, r, g, b, maxcolor)
	xdef	_FindColor
_FindColor:
	movem.l	d1-d4/a3/a6,-(sp)
	move.l	16(sp),a3	; cm
	move.l	20(sp),d1	; r
	move.l	24(sp),d2	; g
	move.l	28(sp),d3	; b
	move.l	32(sp),d4	; maxcolor
	move.l	_GfxBase,a6
	jsr	-1008(a6)	; FindColor()
	movem.l	(sp)+,d1-d4/a3/a6
	rts

; AllocSpriteDataA(bm, tags)
	xdef	_AllocSpriteDataA
_AllocSpriteDataA:
	movem.l	a1-a2/a6,-(sp)
	move.l	12(sp),a2	; bm
	move.l	16(sp),a1	; tags
	move.l	_GfxBase,a6
	jsr	-1020(a6)	; AllocSpriteDataA()
	movem.l	(sp)+,a1-a2/a6
	rts

; ChangeExtSpriteA(vp, oldsprite, newsprite, tags)
	xdef	_ChangeExtSpriteA
_ChangeExtSpriteA:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; vp
	move.l	16(sp),a1	; oldsprite
	move.l	20(sp),a2	; newsprite
	move.l	24(sp),a3	; tags
	move.l	_GfxBase,a6
	jsr	-1026(a6)	; ChangeExtSpriteA()
	movem.l	(sp)+,a0-a3/a6
	rts

; FreeSpriteData(sp)
	xdef	_FreeSpriteData
_FreeSpriteData:
	movem.l	a2/a6,-(sp)
	move.l	12(sp),a2	; sp
	move.l	_GfxBase,a6
	jsr	-1032(a6)	; FreeSpriteData()
	movem.l	(sp)+,a2/a6
	rts

; SetRPAttrsA(rp, tags)
	xdef	_SetRPAttrsA
_SetRPAttrsA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	16(sp),a1	; tags
	move.l	_GfxBase,a6
	jsr	-1038(a6)	; SetRPAttrsA()
	movem.l	(sp)+,a0-a1/a6
	rts

; GetRPAttrsA(rp, tags)
	xdef	_GetRPAttrsA
_GetRPAttrsA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	16(sp),a1	; tags
	move.l	_GfxBase,a6
	jsr	-1044(a6)	; GetRPAttrsA()
	movem.l	(sp)+,a0-a1/a6
	rts

; BestModeIDA(tags)
	xdef	_BestModeIDA
_BestModeIDA:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; tags
	move.l	_GfxBase,a6
	jsr	-1050(a6)	; BestModeIDA()
	movem.l	(sp)+,a0/a6
	rts

; WriteChunkyPixels(rp, xstart, ystart, xstop, ystop, array, bytesperrow)
	xdef	_WriteChunkyPixels
_WriteChunkyPixels:
	movem.l	d0-d4/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),d0	; xstart
	move.l	24(sp),d1	; ystart
	move.l	28(sp),d2	; xstop
	move.l	32(sp),d3	; ystop
	move.l	36(sp),a2	; array
	move.l	40(sp),d4	; bytesperrow
	move.l	_GfxBase,a6
	jsr	-1056(a6)	; WriteChunkyPixels()
	movem.l	(sp)+,d0-d4/a0-a2/a6
	rts

