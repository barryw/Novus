; intuition library stubs for Novus
; Auto-generated from intuition_lib.fd

	xref	_IntuitionBase	; Provided by startup.o + -lamiga

	section	text,code

; OpenIntuition()
	xdef	_OpenIntuition
_OpenIntuition:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-30(a6)	; OpenIntuition()
	movem.l	(sp)+,a6
	rts

; Intuition(iEvent)
	xdef	_Intuition
_Intuition:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iEvent
	move.l	_IntuitionBase,a6
	jsr	-36(a6)	; Intuition()
	movem.l	(sp)+,a0/a6
	rts

; AddGadget(window, gadget, position)
	xdef	_AddGadget
_AddGadget:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),a1	; gadget
	move.l	24(sp),d0	; position
	move.l	_IntuitionBase,a6
	jsr	-42(a6)	; AddGadget()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; ClearDMRequest(window)
	xdef	_ClearDMRequest
_ClearDMRequest:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-48(a6)	; ClearDMRequest()
	movem.l	(sp)+,a0/a6
	rts

; ClearMenuStrip(window)
	xdef	_ClearMenuStrip
_ClearMenuStrip:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-54(a6)	; ClearMenuStrip()
	movem.l	(sp)+,a0/a6
	rts

; ClearPointer(window)
	xdef	_ClearPointer
_ClearPointer:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-60(a6)	; ClearPointer()
	movem.l	(sp)+,a0/a6
	rts

; CloseScreen(screen)
	xdef	_CloseScreen
_CloseScreen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	_IntuitionBase,a6
	jsr	-66(a6)	; CloseScreen()
	movem.l	(sp)+,a0/a6
	rts

; CloseWindow(window)
	xdef	_CloseWindow
_CloseWindow:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-72(a6)	; CloseWindow()
	movem.l	(sp)+,a0/a6
	rts

; CloseWorkBench()
	xdef	_CloseWorkBench
_CloseWorkBench:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-78(a6)	; CloseWorkBench()
	movem.l	(sp)+,a6
	rts

; CurrentTime(seconds, micros)
	xdef	_CurrentTime
_CurrentTime:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; seconds
	move.l	16(sp),a1	; micros
	move.l	_IntuitionBase,a6
	jsr	-84(a6)	; CurrentTime()
	movem.l	(sp)+,a0-a1/a6
	rts

; DisplayAlert(alertNumber, string, height)
	xdef	_DisplayAlert
_DisplayAlert:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),d0	; alertNumber
	move.l	20(sp),a0	; string
	move.l	24(sp),d1	; height
	move.l	_IntuitionBase,a6
	jsr	-90(a6)	; DisplayAlert()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; DisplayBeep(screen)
	xdef	_DisplayBeep
_DisplayBeep:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	_IntuitionBase,a6
	jsr	-96(a6)	; DisplayBeep()
	movem.l	(sp)+,a0/a6
	rts

; DoubleClick(sSeconds, sMicros, cSeconds, cMicros)
	xdef	_DoubleClick
_DoubleClick:
	movem.l	d0-d3/a6,-(sp)
	move.l	12(sp),d0	; sSeconds
	move.l	16(sp),d1	; sMicros
	move.l	20(sp),d2	; cSeconds
	move.l	24(sp),d3	; cMicros
	move.l	_IntuitionBase,a6
	jsr	-102(a6)	; DoubleClick()
	movem.l	(sp)+,d0-d3/a6
	rts

; DrawBorder(rp, border, leftOffset, topOffset)
	xdef	_DrawBorder
_DrawBorder:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),a1	; border
	move.l	24(sp),d0	; leftOffset
	move.l	28(sp),d1	; topOffset
	move.l	_IntuitionBase,a6
	jsr	-108(a6)	; DrawBorder()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; DrawImage(rp, image, leftOffset, topOffset)
	xdef	_DrawImage
_DrawImage:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),a1	; image
	move.l	24(sp),d0	; leftOffset
	move.l	28(sp),d1	; topOffset
	move.l	_IntuitionBase,a6
	jsr	-114(a6)	; DrawImage()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; EndRequest(requester, window)
	xdef	_EndRequest
_EndRequest:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; requester
	move.l	16(sp),a1	; window
	move.l	_IntuitionBase,a6
	jsr	-120(a6)	; EndRequest()
	movem.l	(sp)+,a0-a1/a6
	rts

; GetDefPrefs(preferences, size)
	xdef	_GetDefPrefs
_GetDefPrefs:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; preferences
	move.l	20(sp),d0	; size
	move.l	_IntuitionBase,a6
	jsr	-126(a6)	; GetDefPrefs()
	movem.l	(sp)+,d0/a0/a6
	rts

; GetPrefs(preferences, size)
	xdef	_GetPrefs
_GetPrefs:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; preferences
	move.l	20(sp),d0	; size
	move.l	_IntuitionBase,a6
	jsr	-132(a6)	; GetPrefs()
	movem.l	(sp)+,d0/a0/a6
	rts

; InitRequester(requester)
	xdef	_InitRequester
_InitRequester:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; requester
	move.l	_IntuitionBase,a6
	jsr	-138(a6)	; InitRequester()
	movem.l	(sp)+,a0/a6
	rts

; ItemAddress(menuStrip, menuNumber)
	xdef	_ItemAddress
_ItemAddress:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; menuStrip
	move.l	20(sp),d0	; menuNumber
	move.l	_IntuitionBase,a6
	jsr	-144(a6)	; ItemAddress()
	movem.l	(sp)+,d0/a0/a6
	rts

; ModifyIDCMP(window, flags)
	xdef	_ModifyIDCMP
_ModifyIDCMP:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; flags
	move.l	_IntuitionBase,a6
	jsr	-150(a6)	; ModifyIDCMP()
	movem.l	(sp)+,d0/a0/a6
	rts

; ModifyProp(gadget, window, requester, flags, horizPot, vertPot, horizBody, vertBody)
	xdef	_ModifyProp
_ModifyProp:
	movem.l	d0-d4/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; gadget
	move.l	20(sp),a1	; window
	move.l	24(sp),a2	; requester
	move.l	28(sp),d0	; flags
	move.l	32(sp),d1	; horizPot
	move.l	36(sp),d2	; vertPot
	move.l	40(sp),d3	; horizBody
	move.l	44(sp),d4	; vertBody
	move.l	_IntuitionBase,a6
	jsr	-156(a6)	; ModifyProp()
	movem.l	(sp)+,d0-d4/a0-a2/a6
	rts

; MoveScreen(screen, dx, dy)
	xdef	_MoveScreen
_MoveScreen:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; screen
	move.l	20(sp),d0	; dx
	move.l	24(sp),d1	; dy
	move.l	_IntuitionBase,a6
	jsr	-162(a6)	; MoveScreen()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; MoveWindow(window, dx, dy)
	xdef	_MoveWindow
_MoveWindow:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; dx
	move.l	24(sp),d1	; dy
	move.l	_IntuitionBase,a6
	jsr	-168(a6)	; MoveWindow()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; OffGadget(gadget, window, requester)
	xdef	_OffGadget
_OffGadget:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; gadget
	move.l	16(sp),a1	; window
	move.l	20(sp),a2	; requester
	move.l	_IntuitionBase,a6
	jsr	-174(a6)	; OffGadget()
	movem.l	(sp)+,a0-a2/a6
	rts

; OffMenu(window, menuNumber)
	xdef	_OffMenu
_OffMenu:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; menuNumber
	move.l	_IntuitionBase,a6
	jsr	-180(a6)	; OffMenu()
	movem.l	(sp)+,d0/a0/a6
	rts

; OnGadget(gadget, window, requester)
	xdef	_OnGadget
_OnGadget:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; gadget
	move.l	16(sp),a1	; window
	move.l	20(sp),a2	; requester
	move.l	_IntuitionBase,a6
	jsr	-186(a6)	; OnGadget()
	movem.l	(sp)+,a0-a2/a6
	rts

; OnMenu(window, menuNumber)
	xdef	_OnMenu
_OnMenu:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; menuNumber
	move.l	_IntuitionBase,a6
	jsr	-192(a6)	; OnMenu()
	movem.l	(sp)+,d0/a0/a6
	rts

; OpenScreen(newScreen)
	xdef	_OpenScreen
_OpenScreen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; newScreen
	move.l	_IntuitionBase,a6
	jsr	-198(a6)	; OpenScreen()
	movem.l	(sp)+,a0/a6
	rts

; OpenWindow(newWindow)
	xdef	_OpenWindow
_OpenWindow:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; newWindow
	move.l	_IntuitionBase,a6
	jsr	-204(a6)	; OpenWindow()
	movem.l	(sp)+,a0/a6
	rts

; OpenWorkBench()
	xdef	_OpenWorkBench
_OpenWorkBench:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-210(a6)	; OpenWorkBench()
	movem.l	(sp)+,a6
	rts

; PrintIText(rp, iText, left, top)
	xdef	_PrintIText
_PrintIText:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),a1	; iText
	move.l	24(sp),d0	; left
	move.l	28(sp),d1	; top
	move.l	_IntuitionBase,a6
	jsr	-216(a6)	; PrintIText()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; RefreshGadgets(gadgets, window, requester)
	xdef	_RefreshGadgets
_RefreshGadgets:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; gadgets
	move.l	16(sp),a1	; window
	move.l	20(sp),a2	; requester
	move.l	_IntuitionBase,a6
	jsr	-222(a6)	; RefreshGadgets()
	movem.l	(sp)+,a0-a2/a6
	rts

; RemoveGadget(window, gadget)
	xdef	_RemoveGadget
_RemoveGadget:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	16(sp),a1	; gadget
	move.l	_IntuitionBase,a6
	jsr	-228(a6)	; RemoveGadget()
	movem.l	(sp)+,a0-a1/a6
	rts

; ReportMouse(flag, window)
	xdef	_ReportMouse
_ReportMouse:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),d0	; flag
	move.l	20(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-234(a6)	; ReportMouse()
	movem.l	(sp)+,d0/a0/a6
	rts

; Request(requester, window)
	xdef	_Request
_Request:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; requester
	move.l	16(sp),a1	; window
	move.l	_IntuitionBase,a6
	jsr	-240(a6)	; Request()
	movem.l	(sp)+,a0-a1/a6
	rts

; ScreenToBack(screen)
	xdef	_ScreenToBack
_ScreenToBack:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	_IntuitionBase,a6
	jsr	-246(a6)	; ScreenToBack()
	movem.l	(sp)+,a0/a6
	rts

; ScreenToFront(screen)
	xdef	_ScreenToFront
_ScreenToFront:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	_IntuitionBase,a6
	jsr	-252(a6)	; ScreenToFront()
	movem.l	(sp)+,a0/a6
	rts

; SetDMRequest(window, requester)
	xdef	_SetDMRequest
_SetDMRequest:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	16(sp),a1	; requester
	move.l	_IntuitionBase,a6
	jsr	-258(a6)	; SetDMRequest()
	movem.l	(sp)+,a0-a1/a6
	rts

; SetMenuStrip(window, menu)
	xdef	_SetMenuStrip
_SetMenuStrip:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	16(sp),a1	; menu
	move.l	_IntuitionBase,a6
	jsr	-264(a6)	; SetMenuStrip()
	movem.l	(sp)+,a0-a1/a6
	rts

; SetPointer(window, pointer, height, width, xOffset, yOffset)
	xdef	_SetPointer
_SetPointer:
	movem.l	d0-d3/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),a1	; pointer
	move.l	24(sp),d0	; height
	move.l	28(sp),d1	; width
	move.l	32(sp),d2	; xOffset
	move.l	36(sp),d3	; yOffset
	move.l	_IntuitionBase,a6
	jsr	-270(a6)	; SetPointer()
	movem.l	(sp)+,d0-d3/a0-a1/a6
	rts

; SetWindowTitles(window, windowTitle, screenTitle)
	xdef	_SetWindowTitles
_SetWindowTitles:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	16(sp),a1	; windowTitle
	move.l	20(sp),a2	; screenTitle
	move.l	_IntuitionBase,a6
	jsr	-276(a6)	; SetWindowTitles()
	movem.l	(sp)+,a0-a2/a6
	rts

; ShowTitle(screen, showIt)
	xdef	_ShowTitle
_ShowTitle:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; screen
	move.l	20(sp),d0	; showIt
	move.l	_IntuitionBase,a6
	jsr	-282(a6)	; ShowTitle()
	movem.l	(sp)+,d0/a0/a6
	rts

; SizeWindow(window, dx, dy)
	xdef	_SizeWindow
_SizeWindow:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; dx
	move.l	24(sp),d1	; dy
	move.l	_IntuitionBase,a6
	jsr	-288(a6)	; SizeWindow()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; ViewAddress()
	xdef	_ViewAddress
_ViewAddress:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-294(a6)	; ViewAddress()
	movem.l	(sp)+,a6
	rts

; ViewPortAddress(window)
	xdef	_ViewPortAddress
_ViewPortAddress:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-300(a6)	; ViewPortAddress()
	movem.l	(sp)+,a0/a6
	rts

; WindowToBack(window)
	xdef	_WindowToBack
_WindowToBack:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-306(a6)	; WindowToBack()
	movem.l	(sp)+,a0/a6
	rts

; WindowToFront(window)
	xdef	_WindowToFront
_WindowToFront:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-312(a6)	; WindowToFront()
	movem.l	(sp)+,a0/a6
	rts

; WindowLimits(window, widthMin, heightMin, widthMax, heightMax)
	xdef	_WindowLimits
_WindowLimits:
	movem.l	d0-d3/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; widthMin
	move.l	24(sp),d1	; heightMin
	move.l	28(sp),d2	; widthMax
	move.l	32(sp),d3	; heightMax
	move.l	_IntuitionBase,a6
	jsr	-318(a6)	; WindowLimits()
	movem.l	(sp)+,d0-d3/a0/a6
	rts

; SetPrefs(preferences, size, inform)
	xdef	_SetPrefs
_SetPrefs:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; preferences
	move.l	20(sp),d0	; size
	move.l	24(sp),d1	; inform
	move.l	_IntuitionBase,a6
	jsr	-324(a6)	; SetPrefs()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; IntuiTextLength(iText)
	xdef	_IntuiTextLength
_IntuiTextLength:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iText
	move.l	_IntuitionBase,a6
	jsr	-330(a6)	; IntuiTextLength()
	movem.l	(sp)+,a0/a6
	rts

; WBenchToBack()
	xdef	_WBenchToBack
_WBenchToBack:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-336(a6)	; WBenchToBack()
	movem.l	(sp)+,a6
	rts

; WBenchToFront()
	xdef	_WBenchToFront
_WBenchToFront:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-342(a6)	; WBenchToFront()
	movem.l	(sp)+,a6
	rts

; AutoRequest(window, body, posText, negText, pFlag, nFlag, width, height)
	xdef	_AutoRequest
_AutoRequest:
	movem.l	d0-d3/a0-a3/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),a1	; body
	move.l	24(sp),a2	; posText
	move.l	28(sp),a3	; negText
	move.l	32(sp),d0	; pFlag
	move.l	36(sp),d1	; nFlag
	move.l	40(sp),d2	; width
	move.l	44(sp),d3	; height
	move.l	_IntuitionBase,a6
	jsr	-348(a6)	; AutoRequest()
	movem.l	(sp)+,d0-d3/a0-a3/a6
	rts

; BeginRefresh(window)
	xdef	_BeginRefresh
_BeginRefresh:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-354(a6)	; BeginRefresh()
	movem.l	(sp)+,a0/a6
	rts

; BuildSysRequest(window, body, posText, negText, flags, width, height)
	xdef	_BuildSysRequest
_BuildSysRequest:
	movem.l	d0-d2/a0-a3/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),a1	; body
	move.l	24(sp),a2	; posText
	move.l	28(sp),a3	; negText
	move.l	32(sp),d0	; flags
	move.l	36(sp),d1	; width
	move.l	40(sp),d2	; height
	move.l	_IntuitionBase,a6
	jsr	-360(a6)	; BuildSysRequest()
	movem.l	(sp)+,d0-d2/a0-a3/a6
	rts

; EndRefresh(window, complete)
	xdef	_EndRefresh
_EndRefresh:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; complete
	move.l	_IntuitionBase,a6
	jsr	-366(a6)	; EndRefresh()
	movem.l	(sp)+,d0/a0/a6
	rts

; FreeSysRequest(window)
	xdef	_FreeSysRequest
_FreeSysRequest:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-372(a6)	; FreeSysRequest()
	movem.l	(sp)+,a0/a6
	rts

; MakeScreen(screen)
	xdef	_MakeScreen
_MakeScreen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	_IntuitionBase,a6
	jsr	-378(a6)	; MakeScreen()
	movem.l	(sp)+,a0/a6
	rts

; RemakeDisplay()
	xdef	_RemakeDisplay
_RemakeDisplay:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-384(a6)	; RemakeDisplay()
	movem.l	(sp)+,a6
	rts

; RethinkDisplay()
	xdef	_RethinkDisplay
_RethinkDisplay:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-390(a6)	; RethinkDisplay()
	movem.l	(sp)+,a6
	rts

; AllocRemember(rememberKey, size, flags)
	xdef	_AllocRemember
_AllocRemember:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; rememberKey
	move.l	20(sp),d0	; size
	move.l	24(sp),d1	; flags
	move.l	_IntuitionBase,a6
	jsr	-396(a6)	; AllocRemember()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; FreeRemember(rememberKey, reallyForget)
	xdef	_FreeRemember
_FreeRemember:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; rememberKey
	move.l	20(sp),d0	; reallyForget
	move.l	_IntuitionBase,a6
	jsr	-408(a6)	; FreeRemember()
	movem.l	(sp)+,d0/a0/a6
	rts

; LockIBase(dontknow)
	xdef	_LockIBase
_LockIBase:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; dontknow
	move.l	_IntuitionBase,a6
	jsr	-414(a6)	; LockIBase()
	movem.l	(sp)+,d0/a6
	rts

; UnlockIBase(ibLock)
	xdef	_UnlockIBase
_UnlockIBase:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; ibLock
	move.l	_IntuitionBase,a6
	jsr	-420(a6)	; UnlockIBase()
	movem.l	(sp)+,a0/a6
	rts

; GetScreenData(buffer, size, type, screen)
	xdef	_GetScreenData
_GetScreenData:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; buffer
	move.l	20(sp),d0	; size
	move.l	24(sp),d1	; type
	move.l	28(sp),a1	; screen
	move.l	_IntuitionBase,a6
	jsr	-426(a6)	; GetScreenData()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; RefreshGList(gadgets, window, requester, numGad)
	xdef	_RefreshGList
_RefreshGList:
	movem.l	d0/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; gadgets
	move.l	20(sp),a1	; window
	move.l	24(sp),a2	; requester
	move.l	28(sp),d0	; numGad
	move.l	_IntuitionBase,a6
	jsr	-432(a6)	; RefreshGList()
	movem.l	(sp)+,d0/a0-a2/a6
	rts

; AddGList(window, gadget, position, numGad, requester)
	xdef	_AddGList
_AddGList:
	movem.l	d0-d1/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),a1	; gadget
	move.l	24(sp),d0	; position
	move.l	28(sp),d1	; numGad
	move.l	32(sp),a2	; requester
	move.l	_IntuitionBase,a6
	jsr	-438(a6)	; AddGList()
	movem.l	(sp)+,d0-d1/a0-a2/a6
	rts

; RemoveGList(remPtr, gadget, numGad)
	xdef	_RemoveGList
_RemoveGList:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; remPtr
	move.l	20(sp),a1	; gadget
	move.l	24(sp),d0	; numGad
	move.l	_IntuitionBase,a6
	jsr	-444(a6)	; RemoveGList()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; ActivateWindow(window)
	xdef	_ActivateWindow
_ActivateWindow:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-450(a6)	; ActivateWindow()
	movem.l	(sp)+,a0/a6
	rts

; RefreshWindowFrame(window)
	xdef	_RefreshWindowFrame
_RefreshWindowFrame:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-456(a6)	; RefreshWindowFrame()
	movem.l	(sp)+,a0/a6
	rts

; ActivateGadget(gadgets, window, requester)
	xdef	_ActivateGadget
_ActivateGadget:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; gadgets
	move.l	16(sp),a1	; window
	move.l	20(sp),a2	; requester
	move.l	_IntuitionBase,a6
	jsr	-462(a6)	; ActivateGadget()
	movem.l	(sp)+,a0-a2/a6
	rts

; NewModifyProp(gadget, window, requester, flags, horizPot, vertPot, horizBody, vertBody, numGad)
	xdef	_NewModifyProp
_NewModifyProp:
	movem.l	d0-d5/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; gadget
	move.l	20(sp),a1	; window
	move.l	24(sp),a2	; requester
	move.l	28(sp),d0	; flags
	move.l	32(sp),d1	; horizPot
	move.l	36(sp),d2	; vertPot
	move.l	40(sp),d3	; horizBody
	move.l	44(sp),d4	; vertBody
	move.l	48(sp),d5	; numGad
	move.l	_IntuitionBase,a6
	jsr	-468(a6)	; NewModifyProp()
	movem.l	(sp)+,d0-d5/a0-a2/a6
	rts

; QueryOverscan(displayID, rect, oScanType)
	xdef	_QueryOverscan
_QueryOverscan:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; displayID
	move.l	20(sp),a1	; rect
	move.l	24(sp),d0	; oScanType
	move.l	_IntuitionBase,a6
	jsr	-474(a6)	; QueryOverscan()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; MoveWindowInFrontOf(window, behindWindow)
	xdef	_MoveWindowInFrontOf
_MoveWindowInFrontOf:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	16(sp),a1	; behindWindow
	move.l	_IntuitionBase,a6
	jsr	-480(a6)	; MoveWindowInFrontOf()
	movem.l	(sp)+,a0-a1/a6
	rts

; ChangeWindowBox(window, left, top, width, height)
	xdef	_ChangeWindowBox
_ChangeWindowBox:
	movem.l	d0-d3/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; left
	move.l	24(sp),d1	; top
	move.l	28(sp),d2	; width
	move.l	32(sp),d3	; height
	move.l	_IntuitionBase,a6
	jsr	-486(a6)	; ChangeWindowBox()
	movem.l	(sp)+,d0-d3/a0/a6
	rts

; SetEditHook(hook)
	xdef	_SetEditHook
_SetEditHook:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; hook
	move.l	_IntuitionBase,a6
	jsr	-492(a6)	; SetEditHook()
	movem.l	(sp)+,a0/a6
	rts

; SetMouseQueue(window, queueLength)
	xdef	_SetMouseQueue
_SetMouseQueue:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),d0	; queueLength
	move.l	_IntuitionBase,a6
	jsr	-498(a6)	; SetMouseQueue()
	movem.l	(sp)+,d0/a0/a6
	rts

; ZipWindow(window)
	xdef	_ZipWindow
_ZipWindow:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	_IntuitionBase,a6
	jsr	-504(a6)	; ZipWindow()
	movem.l	(sp)+,a0/a6
	rts

; LockPubScreen(name)
	xdef	_LockPubScreen
_LockPubScreen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	_IntuitionBase,a6
	jsr	-510(a6)	; LockPubScreen()
	movem.l	(sp)+,a0/a6
	rts

; UnlockPubScreen(name, screen)
	xdef	_UnlockPubScreen
_UnlockPubScreen:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	16(sp),a1	; screen
	move.l	_IntuitionBase,a6
	jsr	-516(a6)	; UnlockPubScreen()
	movem.l	(sp)+,a0-a1/a6
	rts

; LockPubScreenList()
	xdef	_LockPubScreenList
_LockPubScreenList:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-522(a6)	; LockPubScreenList()
	movem.l	(sp)+,a6
	rts

; UnlockPubScreenList()
	xdef	_UnlockPubScreenList
_UnlockPubScreenList:
	movem.l	a6,-(sp)
	move.l	_IntuitionBase,a6
	jsr	-528(a6)	; UnlockPubScreenList()
	movem.l	(sp)+,a6
	rts

; NextPubScreen(screen, namebuf)
	xdef	_NextPubScreen
_NextPubScreen:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	16(sp),a1	; namebuf
	move.l	_IntuitionBase,a6
	jsr	-534(a6)	; NextPubScreen()
	movem.l	(sp)+,a0-a1/a6
	rts

; SetDefaultPubScreen(name)
	xdef	_SetDefaultPubScreen
_SetDefaultPubScreen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	_IntuitionBase,a6
	jsr	-540(a6)	; SetDefaultPubScreen()
	movem.l	(sp)+,a0/a6
	rts

; SetPubScreenModes(modes)
	xdef	_SetPubScreenModes
_SetPubScreenModes:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; modes
	move.l	_IntuitionBase,a6
	jsr	-546(a6)	; SetPubScreenModes()
	movem.l	(sp)+,d0/a6
	rts

; PubScreenStatus(screen, statusFlags)
	xdef	_PubScreenStatus
_PubScreenStatus:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; screen
	move.l	20(sp),d0	; statusFlags
	move.l	_IntuitionBase,a6
	jsr	-552(a6)	; PubScreenStatus()
	movem.l	(sp)+,d0/a0/a6
	rts

; ObtainGIRPort(gInfo)
	xdef	_ObtainGIRPort
_ObtainGIRPort:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; gInfo
	move.l	_IntuitionBase,a6
	jsr	-558(a6)	; ObtainGIRPort()
	movem.l	(sp)+,a0/a6
	rts

; ReleaseGIRPort(rp)
	xdef	_ReleaseGIRPort
_ReleaseGIRPort:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; rp
	move.l	_IntuitionBase,a6
	jsr	-564(a6)	; ReleaseGIRPort()
	movem.l	(sp)+,a0/a6
	rts

; GadgetMouse(gadget, gInfo, mousePoint)
	xdef	_GadgetMouse
_GadgetMouse:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; gadget
	move.l	16(sp),a1	; gInfo
	move.l	20(sp),a2	; mousePoint
	move.l	_IntuitionBase,a6
	jsr	-570(a6)	; GadgetMouse()
	movem.l	(sp)+,a0-a2/a6
	rts

; GetDefaultPubScreen(nameBuffer)
	xdef	_GetDefaultPubScreen
_GetDefaultPubScreen:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; nameBuffer
	move.l	_IntuitionBase,a6
	jsr	-582(a6)	; GetDefaultPubScreen()
	movem.l	(sp)+,a0/a6
	rts

; EasyRequestArgs(window, easyStruct, idcmpPtr, args)
	xdef	_EasyRequestArgs
_EasyRequestArgs:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	16(sp),a1	; easyStruct
	move.l	20(sp),a2	; idcmpPtr
	move.l	24(sp),a3	; args
	move.l	_IntuitionBase,a6
	jsr	-588(a6)	; EasyRequestArgs()
	movem.l	(sp)+,a0-a3/a6
	rts

; BuildEasyRequestArgs(window, easyStruct, idcmp, args)
	xdef	_BuildEasyRequestArgs
_BuildEasyRequestArgs:
	movem.l	d0/a0-a3/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),a1	; easyStruct
	move.l	24(sp),d0	; idcmp
	move.l	28(sp),a3	; args
	move.l	_IntuitionBase,a6
	jsr	-594(a6)	; BuildEasyRequestArgs()
	movem.l	(sp)+,d0/a0-a3/a6
	rts

; SysReqHandler(window, idcmpPtr, waitInput)
	xdef	_SysReqHandler
_SysReqHandler:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; window
	move.l	20(sp),a1	; idcmpPtr
	move.l	24(sp),d0	; waitInput
	move.l	_IntuitionBase,a6
	jsr	-600(a6)	; SysReqHandler()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; OpenWindowTagList(newWindow, tagList)
	xdef	_OpenWindowTagList
_OpenWindowTagList:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; newWindow
	move.l	16(sp),a1	; tagList
	move.l	_IntuitionBase,a6
	jsr	-606(a6)	; OpenWindowTagList()
	movem.l	(sp)+,a0-a1/a6
	rts

; OpenScreenTagList(newScreen, tagList)
	xdef	_OpenScreenTagList
_OpenScreenTagList:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; newScreen
	move.l	16(sp),a1	; tagList
	move.l	_IntuitionBase,a6
	jsr	-612(a6)	; OpenScreenTagList()
	movem.l	(sp)+,a0-a1/a6
	rts

; DrawImageState(rp, image, leftOffset, topOffset, state, drawInfo)
	xdef	_DrawImageState
_DrawImageState:
	movem.l	d0-d2/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),a1	; image
	move.l	24(sp),d0	; leftOffset
	move.l	28(sp),d1	; topOffset
	move.l	32(sp),d2	; state
	move.l	36(sp),a2	; drawInfo
	move.l	_IntuitionBase,a6
	jsr	-618(a6)	; DrawImageState()
	movem.l	(sp)+,d0-d2/a0-a2/a6
	rts

; PointInImage(point, image)
	xdef	_PointInImage
_PointInImage:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),d0	; point
	move.l	20(sp),a0	; image
	move.l	_IntuitionBase,a6
	jsr	-624(a6)	; PointInImage()
	movem.l	(sp)+,d0/a0/a6
	rts

; EraseImage(rp, image, leftOffset, topOffset)
	xdef	_EraseImage
_EraseImage:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; rp
	move.l	20(sp),a1	; image
	move.l	24(sp),d0	; leftOffset
	move.l	28(sp),d1	; topOffset
	move.l	_IntuitionBase,a6
	jsr	-630(a6)	; EraseImage()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; NewObjectA(classPtr, classID, tagList)
	xdef	_NewObjectA
_NewObjectA:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; classPtr
	move.l	16(sp),a1	; classID
	move.l	20(sp),a2	; tagList
	move.l	_IntuitionBase,a6
	jsr	-636(a6)	; NewObjectA()
	movem.l	(sp)+,a0-a2/a6
	rts

; DisposeObject(object)
	xdef	_DisposeObject
_DisposeObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; object
	move.l	_IntuitionBase,a6
	jsr	-642(a6)	; DisposeObject()
	movem.l	(sp)+,a0/a6
	rts

; SetAttrsA(object, tagList)
	xdef	_SetAttrsA
_SetAttrsA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; object
	move.l	16(sp),a1	; tagList
	move.l	_IntuitionBase,a6
	jsr	-648(a6)	; SetAttrsA()
	movem.l	(sp)+,a0-a1/a6
	rts

; GetAttr(attrID, object, storagePtr)
	xdef	_GetAttr
_GetAttr:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),d0	; attrID
	move.l	20(sp),a0	; object
	move.l	24(sp),a1	; storagePtr
	move.l	_IntuitionBase,a6
	jsr	-654(a6)	; GetAttr()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; SetGadgetAttrsA(gadget, window, requester, tagList)
	xdef	_SetGadgetAttrsA
_SetGadgetAttrsA:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; gadget
	move.l	16(sp),a1	; window
	move.l	20(sp),a2	; requester
	move.l	24(sp),a3	; tagList
	move.l	_IntuitionBase,a6
	jsr	-660(a6)	; SetGadgetAttrsA()
	movem.l	(sp)+,a0-a3/a6
	rts

; NextObject(objectPtrPtr)
	xdef	_NextObject
_NextObject:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; objectPtrPtr
	move.l	_IntuitionBase,a6
	jsr	-666(a6)	; NextObject()
	movem.l	(sp)+,a0/a6
	rts

; MakeClass(classID, superClassID, superClassPtr, instanceSize, flags)
	xdef	_MakeClass
_MakeClass:
	movem.l	d0-d1/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; classID
	move.l	20(sp),a1	; superClassID
	move.l	24(sp),a2	; superClassPtr
	move.l	28(sp),d0	; instanceSize
	move.l	32(sp),d1	; flags
	move.l	_IntuitionBase,a6
	jsr	-678(a6)	; MakeClass()
	movem.l	(sp)+,d0-d1/a0-a2/a6
	rts

; AddClass(classPtr)
	xdef	_AddClass
_AddClass:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; classPtr
	move.l	_IntuitionBase,a6
	jsr	-684(a6)	; AddClass()
	movem.l	(sp)+,a0/a6
	rts

; GetScreenDrawInfo(screen)
	xdef	_GetScreenDrawInfo
_GetScreenDrawInfo:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	_IntuitionBase,a6
	jsr	-690(a6)	; GetScreenDrawInfo()
	movem.l	(sp)+,a0/a6
	rts

; FreeScreenDrawInfo(screen, drawInfo)
	xdef	_FreeScreenDrawInfo
_FreeScreenDrawInfo:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; screen
	move.l	16(sp),a1	; drawInfo
	move.l	_IntuitionBase,a6
	jsr	-696(a6)	; FreeScreenDrawInfo()
	movem.l	(sp)+,a0-a1/a6
	rts

; ResetMenuStrip(window, menu)
	xdef	_ResetMenuStrip
_ResetMenuStrip:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; window
	move.l	16(sp),a1	; menu
	move.l	_IntuitionBase,a6
	jsr	-702(a6)	; ResetMenuStrip()
	movem.l	(sp)+,a0-a1/a6
	rts

; RemoveClass(classPtr)
	xdef	_RemoveClass
_RemoveClass:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; classPtr
	move.l	_IntuitionBase,a6
	jsr	-708(a6)	; RemoveClass()
	movem.l	(sp)+,a0/a6
	rts

; FreeClass(classPtr)
	xdef	_FreeClass
_FreeClass:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; classPtr
	move.l	_IntuitionBase,a6
	jsr	-714(a6)	; FreeClass()
	movem.l	(sp)+,a0/a6
	rts

; AllocScreenBuffer(sc, bm, flags)
	xdef	_AllocScreenBuffer
_AllocScreenBuffer:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; sc
	move.l	20(sp),a1	; bm
	move.l	24(sp),d0	; flags
	move.l	_IntuitionBase,a6
	jsr	-768(a6)	; AllocScreenBuffer()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; FreeScreenBuffer(sc, sb)
	xdef	_FreeScreenBuffer
_FreeScreenBuffer:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; sc
	move.l	16(sp),a1	; sb
	move.l	_IntuitionBase,a6
	jsr	-774(a6)	; FreeScreenBuffer()
	movem.l	(sp)+,a0-a1/a6
	rts

; ChangeScreenBuffer(sc, sb)
	xdef	_ChangeScreenBuffer
_ChangeScreenBuffer:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; sc
	move.l	16(sp),a1	; sb
	move.l	_IntuitionBase,a6
	jsr	-780(a6)	; ChangeScreenBuffer()
	movem.l	(sp)+,a0-a1/a6
	rts

; ScreenDepth(screen, flags, reserved)
	xdef	_ScreenDepth
_ScreenDepth:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; screen
	move.l	20(sp),d0	; flags
	move.l	24(sp),a1	; reserved
	move.l	_IntuitionBase,a6
	jsr	-786(a6)	; ScreenDepth()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; ScreenPosition(screen, flags, x1, y1, x2, y2)
	xdef	_ScreenPosition
_ScreenPosition:
	movem.l	d0-d4/a0/a6,-(sp)
	move.l	16(sp),a0	; screen
	move.l	20(sp),d0	; flags
	move.l	24(sp),d1	; x1
	move.l	28(sp),d2	; y1
	move.l	32(sp),d3	; x2
	move.l	36(sp),d4	; y2
	move.l	_IntuitionBase,a6
	jsr	-792(a6)	; ScreenPosition()
	movem.l	(sp)+,d0-d4/a0/a6
	rts

; ScrollWindowRaster(win, dx, dy, xMin, yMin, xMax, yMax)
	xdef	_ScrollWindowRaster
_ScrollWindowRaster:
	movem.l	d0-d5/a1/a6,-(sp)
	move.l	16(sp),a1	; win
	move.l	20(sp),d0	; dx
	move.l	24(sp),d1	; dy
	move.l	28(sp),d2	; xMin
	move.l	32(sp),d3	; yMin
	move.l	36(sp),d4	; xMax
	move.l	40(sp),d5	; yMax
	move.l	_IntuitionBase,a6
	jsr	-798(a6)	; ScrollWindowRaster()
	movem.l	(sp)+,d0-d5/a1/a6
	rts

; LendMenus(fromwindow, towindow)
	xdef	_LendMenus
_LendMenus:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; fromwindow
	move.l	16(sp),a1	; towindow
	move.l	_IntuitionBase,a6
	jsr	-804(a6)	; LendMenus()
	movem.l	(sp)+,a0-a1/a6
	rts

; DoGadgetMethodA(gad, win, req, message)
	xdef	_DoGadgetMethodA
_DoGadgetMethodA:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; gad
	move.l	16(sp),a1	; win
	move.l	20(sp),a2	; req
	move.l	24(sp),a3	; message
	move.l	_IntuitionBase,a6
	jsr	-810(a6)	; DoGadgetMethodA()
	movem.l	(sp)+,a0-a3/a6
	rts

; SetWindowPointerA(win, taglist)
	xdef	_SetWindowPointerA
_SetWindowPointerA:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; win
	move.l	16(sp),a1	; taglist
	move.l	_IntuitionBase,a6
	jsr	-816(a6)	; SetWindowPointerA()
	movem.l	(sp)+,a0-a1/a6
	rts

; TimedDisplayAlert(alertNumber, string, height, time)
	xdef	_TimedDisplayAlert
_TimedDisplayAlert:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),d0	; alertNumber
	move.l	20(sp),a0	; string
	move.l	24(sp),d1	; height
	move.l	28(sp),a1	; time
	move.l	_IntuitionBase,a6
	jsr	-822(a6)	; TimedDisplayAlert()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; HelpControl(win, flags)
	xdef	_HelpControl
_HelpControl:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; win
	move.l	20(sp),d0	; flags
	move.l	_IntuitionBase,a6
	jsr	-828(a6)	; HelpControl()
	movem.l	(sp)+,d0/a0/a6
	rts

