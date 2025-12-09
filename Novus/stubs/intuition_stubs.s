; Generated from SFD file by Novus SFD Parser
; Library: intuition.library
; Base: _IntuitionBase
; Each function is in its own section for dead code elimination

	xref	_IntuitionBase

	section	_OpenIntuition_stub,code

; VOID OpenIntuition()
	xdef	_OpenIntuition
_OpenIntuition:
	movea.l	_IntuitionBase,a6
	jsr	-30(a6)
	rts

	section	_Intuition_stub,code

; VOID Intuition(struct InputEvent * iEvent)
	xdef	_Intuition
_Intuition:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-36(a6)
	rts

	section	_AddGadget_stub,code

; UWORD AddGadget(struct Window * window, struct Gadget * gadget, UWORD position)
	xdef	_AddGadget
_AddGadget:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-42(a6)
	rts

	section	_ClearDMRequest_stub,code

; BOOL ClearDMRequest(struct Window * window)
	xdef	_ClearDMRequest
_ClearDMRequest:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-48(a6)
	rts

	section	_ClearMenuStrip_stub,code

; VOID ClearMenuStrip(struct Window * window)
	xdef	_ClearMenuStrip
_ClearMenuStrip:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-54(a6)
	rts

	section	_ClearPointer_stub,code

; VOID ClearPointer(struct Window * window)
	xdef	_ClearPointer
_ClearPointer:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-60(a6)
	rts

	section	_CloseScreen_stub,code

; BOOL CloseScreen(struct Screen * screen)
	xdef	_CloseScreen
_CloseScreen:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-66(a6)
	rts

	section	_CloseWindow_stub,code

; VOID CloseWindow(struct Window * window)
	xdef	_CloseWindow
_CloseWindow:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-72(a6)
	rts

	section	_CloseWorkBench_stub,code

; LONG CloseWorkBench()
	xdef	_CloseWorkBench
_CloseWorkBench:
	movea.l	_IntuitionBase,a6
	jsr	-78(a6)
	rts

	section	_CurrentTime_stub,code

; VOID CurrentTime(ULONG * seconds, ULONG * micros)
	xdef	_CurrentTime
_CurrentTime:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-84(a6)
	rts

	section	_DisplayAlert_stub,code

; BOOL DisplayAlert(ULONG alertNumber, CONST_STRPTR string, UWORD height)
	xdef	_DisplayAlert
_DisplayAlert:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	move.l	12(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-90(a6)
	rts

	section	_DisplayBeep_stub,code

; VOID DisplayBeep(struct Screen * screen)
	xdef	_DisplayBeep
_DisplayBeep:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-96(a6)
	rts

	section	_DoubleClick_stub,code

; BOOL DoubleClick(ULONG sSeconds, ULONG sMicros, ULONG cSeconds, ULONG cMicros)
	xdef	_DoubleClick
_DoubleClick:
	move.l	4(sp),d0
	move.l	8(sp),d1
	move.l	12(sp),d2
	move.l	16(sp),d3
	movea.l	_IntuitionBase,a6
	jsr	-102(a6)
	rts

	section	_DrawBorder_stub,code

; VOID DrawBorder(struct RastPort * rp, const struct Border * border, WORD leftOffset, WORD topOffset)
	xdef	_DrawBorder
_DrawBorder:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-108(a6)
	rts

	section	_DrawImage_stub,code

; VOID DrawImage(struct RastPort * rp, struct Image * image, WORD leftOffset, WORD topOffset)
	xdef	_DrawImage
_DrawImage:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-114(a6)
	rts

	section	_EndRequest_stub,code

; VOID EndRequest(struct Requester * requester, struct Window * window)
	xdef	_EndRequest
_EndRequest:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-120(a6)
	rts

	section	_GetDefPrefs_stub,code

; struct Preferences * GetDefPrefs(struct Preferences * preferences, WORD size)
	xdef	_GetDefPrefs
_GetDefPrefs:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-126(a6)
	rts

	section	_GetPrefs_stub,code

; struct Preferences * GetPrefs(struct Preferences * preferences, WORD size)
	xdef	_GetPrefs
_GetPrefs:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-132(a6)
	rts

	section	_InitRequester_stub,code

; VOID InitRequester(struct Requester * requester)
	xdef	_InitRequester
_InitRequester:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-138(a6)
	rts

	section	_ItemAddress_stub,code

; struct MenuItem * ItemAddress(const struct Menu * menuStrip, UWORD menuNumber)
	xdef	_ItemAddress
_ItemAddress:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-144(a6)
	rts

	section	_ModifyIDCMP_stub,code

; BOOL ModifyIDCMP(struct Window * window, ULONG flags)
	xdef	_ModifyIDCMP
_ModifyIDCMP:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-150(a6)
	rts

	section	_ModifyProp_stub,code

; VOID ModifyProp(struct Gadget * gadget, struct Window * window, struct Requester * requester, UWORD flags, UWORD horizPot, UWORD vertPot, UWORD horizBody, UWORD vertBody)
	xdef	_ModifyProp
_ModifyProp:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_IntuitionBase,a6
	jsr	-156(a6)
	rts

	section	_MoveScreen_stub,code

; VOID MoveScreen(struct Screen * screen, WORD dx, WORD dy)
	xdef	_MoveScreen
_MoveScreen:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-162(a6)
	rts

	section	_MoveWindow_stub,code

; VOID MoveWindow(struct Window * window, WORD dx, WORD dy)
	xdef	_MoveWindow
_MoveWindow:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-168(a6)
	rts

	section	_OffGadget_stub,code

; VOID OffGadget(struct Gadget * gadget, struct Window * window, struct Requester * requester)
	xdef	_OffGadget
_OffGadget:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-174(a6)
	rts

	section	_OffMenu_stub,code

; VOID OffMenu(struct Window * window, UWORD menuNumber)
	xdef	_OffMenu
_OffMenu:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-180(a6)
	rts

	section	_OnGadget_stub,code

; VOID OnGadget(struct Gadget * gadget, struct Window * window, struct Requester * requester)
	xdef	_OnGadget
_OnGadget:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-186(a6)
	rts

	section	_OnMenu_stub,code

; VOID OnMenu(struct Window * window, UWORD menuNumber)
	xdef	_OnMenu
_OnMenu:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-192(a6)
	rts

	section	_OpenScreen_stub,code

; struct Screen * OpenScreen(const struct NewScreen * newScreen)
	xdef	_OpenScreen
_OpenScreen:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-198(a6)
	rts

	section	_OpenWindow_stub,code

; struct Window * OpenWindow(const struct NewWindow * newWindow)
	xdef	_OpenWindow
_OpenWindow:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-204(a6)
	rts

	section	_OpenWorkBench_stub,code

; ULONG OpenWorkBench()
	xdef	_OpenWorkBench
_OpenWorkBench:
	movea.l	_IntuitionBase,a6
	jsr	-210(a6)
	rts

	section	_PrintIText_stub,code

; VOID PrintIText(struct RastPort * rp, const struct IntuiText * iText, WORD left, WORD top)
	xdef	_PrintIText
_PrintIText:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-216(a6)
	rts

	section	_RefreshGadgets_stub,code

; VOID RefreshGadgets(struct Gadget * gadgets, struct Window * window, struct Requester * requester)
	xdef	_RefreshGadgets
_RefreshGadgets:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-222(a6)
	rts

	section	_RemoveGadget_stub,code

; UWORD RemoveGadget(struct Window * window, struct Gadget * gadget)
	xdef	_RemoveGadget
_RemoveGadget:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-228(a6)
	rts

	section	_ReportMouse_stub,code

; VOID ReportMouse(BOOL flag, struct Window * window)
	xdef	_ReportMouse
_ReportMouse:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-234(a6)
	rts

	section	_Request_stub,code

; BOOL Request(struct Requester * requester, struct Window * window)
	xdef	_Request
_Request:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-246(a6)
	rts

	section	_ScreenToBack_stub,code

; VOID ScreenToBack(struct Screen * screen)
	xdef	_ScreenToBack
_ScreenToBack:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-252(a6)
	rts

	section	_ScreenToFront_stub,code

; VOID ScreenToFront(struct Screen * screen)
	xdef	_ScreenToFront
_ScreenToFront:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-258(a6)
	rts

	section	_SetDMRequest_stub,code

; BOOL SetDMRequest(struct Window * window, struct Requester * requester)
	xdef	_SetDMRequest
_SetDMRequest:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-264(a6)
	rts

	section	_SetMenuStrip_stub,code

; BOOL SetMenuStrip(struct Window * window, struct Menu * menu)
	xdef	_SetMenuStrip
_SetMenuStrip:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-270(a6)
	rts

	section	_SetPointer_stub,code

; VOID SetPointer(struct Window * window, UWORD * pointer, WORD height, WORD width, WORD xOffset, WORD yOffset)
	xdef	_SetPointer
_SetPointer:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_IntuitionBase,a6
	jsr	-276(a6)
	rts

	section	_SetWindowTitles_stub,code

; VOID SetWindowTitles(struct Window * window, CONST_STRPTR windowTitle, CONST_STRPTR screenTitle)
	xdef	_SetWindowTitles
_SetWindowTitles:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-282(a6)
	rts

	section	_ShowTitle_stub,code

; VOID ShowTitle(struct Screen * screen, BOOL showIt)
	xdef	_ShowTitle
_ShowTitle:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-288(a6)
	rts

	section	_SizeWindow_stub,code

; VOID SizeWindow(struct Window * window, WORD dx, WORD dy)
	xdef	_SizeWindow
_SizeWindow:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-294(a6)
	rts

	section	_ViewAddress_stub,code

; struct View * ViewAddress()
	xdef	_ViewAddress
_ViewAddress:
	movea.l	_IntuitionBase,a6
	jsr	-300(a6)
	rts

	section	_ViewPortAddress_stub,code

; struct ViewPort * ViewPortAddress(const struct Window * window)
	xdef	_ViewPortAddress
_ViewPortAddress:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-306(a6)
	rts

	section	_WindowToBack_stub,code

; VOID WindowToBack(struct Window * window)
	xdef	_WindowToBack
_WindowToBack:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-312(a6)
	rts

	section	_WindowToFront_stub,code

; VOID WindowToFront(struct Window * window)
	xdef	_WindowToFront
_WindowToFront:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-318(a6)
	rts

	section	_WindowLimits_stub,code

; BOOL WindowLimits(struct Window * window, LONG widthMin, LONG heightMin, ULONG widthMax, ULONG heightMax)
	xdef	_WindowLimits
_WindowLimits:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	movea.l	_IntuitionBase,a6
	jsr	-324(a6)
	rts

	section	_SetPrefs_stub,code

; struct Preferences * SetPrefs(const struct Preferences * preferences, LONG size, BOOL inform)
	xdef	_SetPrefs
_SetPrefs:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-330(a6)
	rts

	section	_IntuiTextLength_stub,code

; LONG IntuiTextLength(const struct IntuiText * iText)
	xdef	_IntuiTextLength
_IntuiTextLength:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-336(a6)
	rts

	section	_WBenchToBack_stub,code

; BOOL WBenchToBack()
	xdef	_WBenchToBack
_WBenchToBack:
	movea.l	_IntuitionBase,a6
	jsr	-342(a6)
	rts

	section	_WBenchToFront_stub,code

; BOOL WBenchToFront()
	xdef	_WBenchToFront
_WBenchToFront:
	movea.l	_IntuitionBase,a6
	jsr	-348(a6)
	rts

	section	_AutoRequest_stub,code

; BOOL AutoRequest(struct Window * window, const struct IntuiText * body, const struct IntuiText * posText, const struct IntuiText * negText, ULONG pFlag, ULONG nFlag, UWORD width, UWORD height)
	xdef	_AutoRequest
_AutoRequest:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	movea.l	_IntuitionBase,a6
	jsr	-354(a6)
	rts

	section	_BeginRefresh_stub,code

; VOID BeginRefresh(struct Window * window)
	xdef	_BeginRefresh
_BeginRefresh:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-360(a6)
	rts

	section	_BuildSysRequest_stub,code

; struct Window * BuildSysRequest(struct Window * window, const struct IntuiText * body, const struct IntuiText * posText, const struct IntuiText * negText, ULONG flags, UWORD width, UWORD height)
	xdef	_BuildSysRequest
_BuildSysRequest:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	move.l	20(sp),d0
	move.l	24(sp),d1
	move.l	28(sp),d2
	movea.l	_IntuitionBase,a6
	jsr	-366(a6)
	rts

	section	_EndRefresh_stub,code

; VOID EndRefresh(struct Window * window, LONG complete)
	xdef	_EndRefresh
_EndRefresh:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-372(a6)
	rts

	section	_FreeSysRequest_stub,code

; VOID FreeSysRequest(struct Window * window)
	xdef	_FreeSysRequest
_FreeSysRequest:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-378(a6)
	rts

	section	_MakeScreen_stub,code

; LONG MakeScreen(struct Screen * screen)
	xdef	_MakeScreen
_MakeScreen:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-384(a6)
	rts

	section	_RemakeDisplay_stub,code

; LONG RemakeDisplay()
	xdef	_RemakeDisplay
_RemakeDisplay:
	movea.l	_IntuitionBase,a6
	jsr	-390(a6)
	rts

	section	_RethinkDisplay_stub,code

; LONG RethinkDisplay()
	xdef	_RethinkDisplay
_RethinkDisplay:
	movea.l	_IntuitionBase,a6
	jsr	-396(a6)
	rts

	section	_AllocRemember_stub,code

; APTR AllocRemember(struct Remember ** rememberKey, ULONG size, ULONG flags)
	xdef	_AllocRemember
_AllocRemember:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-402(a6)
	rts

	section	_FreeRemember_stub,code

; VOID FreeRemember(struct Remember ** rememberKey, BOOL reallyForget)
	xdef	_FreeRemember
_FreeRemember:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-414(a6)
	rts

	section	_LockIBase_stub,code

; ULONG LockIBase(ULONG dontknow)
	xdef	_LockIBase
_LockIBase:
	move.l	4(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-420(a6)
	rts

	section	_UnlockIBase_stub,code

; VOID UnlockIBase(ULONG ibLock)
	xdef	_UnlockIBase
_UnlockIBase:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-426(a6)
	rts

	section	_GetScreenData_stub,code

; LONG GetScreenData(APTR buffer, UWORD size, UWORD type, const struct Screen * screen)
	xdef	_GetScreenData
_GetScreenData:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-432(a6)
	rts

	section	_RefreshGList_stub,code

; VOID RefreshGList(struct Gadget * gadgets, struct Window * window, struct Requester * requester, WORD numGad)
	xdef	_RefreshGList
_RefreshGList:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-438(a6)
	rts

	section	_AddGList_stub,code

; UWORD AddGList(struct Window * window, struct Gadget * gadget, UWORD position, WORD numGad, struct Requester * requester)
	xdef	_AddGList
_AddGList:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-444(a6)
	rts

	section	_RemoveGList_stub,code

; UWORD RemoveGList(struct Window * remPtr, struct Gadget * gadget, WORD numGad)
	xdef	_RemoveGList
_RemoveGList:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-450(a6)
	rts

	section	_ActivateWindow_stub,code

; VOID ActivateWindow(struct Window * window)
	xdef	_ActivateWindow
_ActivateWindow:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-456(a6)
	rts

	section	_RefreshWindowFrame_stub,code

; VOID RefreshWindowFrame(struct Window * window)
	xdef	_RefreshWindowFrame
_RefreshWindowFrame:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-462(a6)
	rts

	section	_ActivateGadget_stub,code

; BOOL ActivateGadget(struct Gadget * gadgets, struct Window * window, struct Requester * requester)
	xdef	_ActivateGadget
_ActivateGadget:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-468(a6)
	rts

	section	_NewModifyProp_stub,code

; VOID NewModifyProp(struct Gadget * gadget, struct Window * window, struct Requester * requester, UWORD flags, UWORD horizPot, UWORD vertPot, UWORD horizBody, UWORD vertBody, WORD numGad)
	xdef	_NewModifyProp
_NewModifyProp:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	move.l	36(sp),d5
	movea.l	_IntuitionBase,a6
	jsr	-474(a6)
	rts

	section	_QueryOverscan_stub,code

; LONG QueryOverscan(ULONG displayID, struct Rectangle * rect, WORD oScanType)
	xdef	_QueryOverscan
_QueryOverscan:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-480(a6)
	rts

	section	_MoveWindowInFrontOf_stub,code

; VOID MoveWindowInFrontOf(struct Window * window, struct Window * behindWindow)
	xdef	_MoveWindowInFrontOf
_MoveWindowInFrontOf:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-486(a6)
	rts

	section	_ChangeWindowBox_stub,code

; VOID ChangeWindowBox(struct Window * window, WORD left, WORD top, WORD width, WORD height)
	xdef	_ChangeWindowBox
_ChangeWindowBox:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	movea.l	_IntuitionBase,a6
	jsr	-492(a6)
	rts

	section	_SetEditHook_stub,code

; struct Hook * SetEditHook(struct Hook * hook)
	xdef	_SetEditHook
_SetEditHook:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-498(a6)
	rts

	section	_SetMouseQueue_stub,code

; LONG SetMouseQueue(struct Window * window, UWORD queueLength)
	xdef	_SetMouseQueue
_SetMouseQueue:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-504(a6)
	rts

	section	_ZipWindow_stub,code

; VOID ZipWindow(struct Window * window)
	xdef	_ZipWindow
_ZipWindow:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-510(a6)
	rts

	section	_LockPubScreen_stub,code

; struct Screen * LockPubScreen(CONST_STRPTR name)
	xdef	_LockPubScreen
_LockPubScreen:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-516(a6)
	rts

	section	_UnlockPubScreen_stub,code

; VOID UnlockPubScreen(CONST_STRPTR name, struct Screen * screen)
	xdef	_UnlockPubScreen
_UnlockPubScreen:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-522(a6)
	rts

	section	_LockPubScreenList_stub,code

; struct List * LockPubScreenList()
	xdef	_LockPubScreenList
_LockPubScreenList:
	movea.l	_IntuitionBase,a6
	jsr	-528(a6)
	rts

	section	_UnlockPubScreenList_stub,code

; VOID UnlockPubScreenList()
	xdef	_UnlockPubScreenList
_UnlockPubScreenList:
	movea.l	_IntuitionBase,a6
	jsr	-534(a6)
	rts

	section	_NextPubScreen_stub,code

; STRPTR NextPubScreen(const struct Screen * screen, STRPTR namebuf)
	xdef	_NextPubScreen
_NextPubScreen:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-540(a6)
	rts

	section	_SetDefaultPubScreen_stub,code

; VOID SetDefaultPubScreen(CONST_STRPTR name)
	xdef	_SetDefaultPubScreen
_SetDefaultPubScreen:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-546(a6)
	rts

	section	_SetPubScreenModes_stub,code

; UWORD SetPubScreenModes(UWORD modes)
	xdef	_SetPubScreenModes
_SetPubScreenModes:
	move.l	4(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-552(a6)
	rts

	section	_PubScreenStatus_stub,code

; UWORD PubScreenStatus(struct Screen * screen, UWORD statusFlags)
	xdef	_PubScreenStatus
_PubScreenStatus:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-558(a6)
	rts

	section	_ObtainGIRPort_stub,code

; struct RastPort * ObtainGIRPort(struct GadgetInfo * gInfo)
	xdef	_ObtainGIRPort
_ObtainGIRPort:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-564(a6)
	rts

	section	_ReleaseGIRPort_stub,code

; VOID ReleaseGIRPort(struct RastPort * rp)
	xdef	_ReleaseGIRPort
_ReleaseGIRPort:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-570(a6)
	rts

	section	_GadgetMouse_stub,code

; VOID GadgetMouse(struct Gadget * gadget, struct GadgetInfo * gInfo, WORD * mousePoint)
	xdef	_GadgetMouse
_GadgetMouse:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-576(a6)
	rts

	section	_GetDefaultPubScreen_stub,code

; VOID GetDefaultPubScreen(STRPTR nameBuffer)
	xdef	_GetDefaultPubScreen
_GetDefaultPubScreen:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-582(a6)
	rts

	section	_EasyRequestArgs_stub,code

; LONG EasyRequestArgs(struct Window * window, const struct EasyStruct * easyStruct, ULONG * idcmpPtr, const APTR args)
	xdef	_EasyRequestArgs
_EasyRequestArgs:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_IntuitionBase,a6
	jsr	-588(a6)
	rts

	section	_BuildEasyRequestArgs_stub,code

; struct Window * BuildEasyRequestArgs(struct Window * window, const struct EasyStruct * easyStruct, ULONG idcmp, const APTR args)
	xdef	_BuildEasyRequestArgs
_BuildEasyRequestArgs:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	16(sp),a3
	movea.l	_IntuitionBase,a6
	jsr	-594(a6)
	rts

	section	_SysReqHandler_stub,code

; LONG SysReqHandler(struct Window * window, ULONG * idcmpPtr, BOOL waitInput)
	xdef	_SysReqHandler
_SysReqHandler:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-600(a6)
	rts

	section	_OpenWindowTagList_stub,code

; struct Window * OpenWindowTagList(const struct NewWindow * newWindow, const struct TagItem * tagList)
	xdef	_OpenWindowTagList
_OpenWindowTagList:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-606(a6)
	rts

	section	_OpenScreenTagList_stub,code

; struct Screen * OpenScreenTagList(const struct NewScreen * newScreen, const struct TagItem * tagList)
	xdef	_OpenScreenTagList
_OpenScreenTagList:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-612(a6)
	rts

	section	_DrawImageState_stub,code

; VOID DrawImageState(struct RastPort * rp, struct Image * image, WORD leftOffset, WORD topOffset, ULONG state, const struct DrawInfo * drawInfo)
	xdef	_DrawImageState
_DrawImageState:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	move.l	20(sp),d2
	movea.l	24(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-618(a6)
	rts

	section	_PointInImage_stub,code

; BOOL PointInImage(ULONG point, struct Image * image)
	xdef	_PointInImage
_PointInImage:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-624(a6)
	rts

	section	_EraseImage_stub,code

; VOID EraseImage(struct RastPort * rp, struct Image * image, WORD leftOffset, WORD topOffset)
	xdef	_EraseImage
_EraseImage:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-630(a6)
	rts

	section	_NewObjectA_stub,code

; APTR NewObjectA(struct IClass * classPtr, CONST_STRPTR classID, const struct TagItem * tagList)
	xdef	_NewObjectA
_NewObjectA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_IntuitionBase,a6
	jsr	-636(a6)
	rts

	section	_DisposeObject_stub,code

; VOID DisposeObject(APTR object)
	xdef	_DisposeObject
_DisposeObject:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-642(a6)
	rts

	section	_SetAttrsA_stub,code

; ULONG SetAttrsA(APTR object, const struct TagItem * tagList)
	xdef	_SetAttrsA
_SetAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-648(a6)
	rts

	section	_GetAttr_stub,code

; ULONG GetAttr(ULONG attrID, APTR object, ULONG * storagePtr)
	xdef	_GetAttr
_GetAttr:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-654(a6)
	rts

	section	_SetGadgetAttrsA_stub,code

; ULONG SetGadgetAttrsA(struct Gadget * gadget, struct Window * window, struct Requester * requester, const struct TagItem * tagList)
	xdef	_SetGadgetAttrsA
_SetGadgetAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_IntuitionBase,a6
	jsr	-660(a6)
	rts

	section	_NextObject_stub,code

; APTR NextObject(APTR objectPtrPtr)
	xdef	_NextObject
_NextObject:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-666(a6)
	rts

	section	_MakeClass_stub,code

; struct IClass * MakeClass(CONST_STRPTR classID, CONST_STRPTR superClassID, const struct IClass * superClassPtr, UWORD instanceSize, ULONG flags)
	xdef	_MakeClass
_MakeClass:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_IntuitionBase,a6
	jsr	-678(a6)
	rts

	section	_AddClass_stub,code

; VOID AddClass(struct IClass * classPtr)
	xdef	_AddClass
_AddClass:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-684(a6)
	rts

	section	_GetScreenDrawInfo_stub,code

; struct DrawInfo * GetScreenDrawInfo(struct Screen * screen)
	xdef	_GetScreenDrawInfo
_GetScreenDrawInfo:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-690(a6)
	rts

	section	_FreeScreenDrawInfo_stub,code

; VOID FreeScreenDrawInfo(struct Screen * screen, struct DrawInfo * drawInfo)
	xdef	_FreeScreenDrawInfo
_FreeScreenDrawInfo:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-696(a6)
	rts

	section	_ResetMenuStrip_stub,code

; BOOL ResetMenuStrip(struct Window * window, struct Menu * menu)
	xdef	_ResetMenuStrip
_ResetMenuStrip:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-702(a6)
	rts

	section	_RemoveClass_stub,code

; VOID RemoveClass(struct IClass * classPtr)
	xdef	_RemoveClass
_RemoveClass:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-708(a6)
	rts

	section	_FreeClass_stub,code

; BOOL FreeClass(struct IClass * classPtr)
	xdef	_FreeClass
_FreeClass:
	movea.l	4(sp),a0
	movea.l	_IntuitionBase,a6
	jsr	-714(a6)
	rts

	section	_AllocScreenBuffer_stub,code

; struct ScreenBuffer * AllocScreenBuffer(struct Screen * sc, struct BitMap * bm, ULONG flags)
	xdef	_AllocScreenBuffer
_AllocScreenBuffer:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-816(a6)
	rts

	section	_FreeScreenBuffer_stub,code

; VOID FreeScreenBuffer(struct Screen * sc, struct ScreenBuffer * sb)
	xdef	_FreeScreenBuffer
_FreeScreenBuffer:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-822(a6)
	rts

	section	_ChangeScreenBuffer_stub,code

; ULONG ChangeScreenBuffer(struct Screen * sc, struct ScreenBuffer * sb)
	xdef	_ChangeScreenBuffer
_ChangeScreenBuffer:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-828(a6)
	rts

	section	_ScreenDepth_stub,code

; VOID ScreenDepth(struct Screen * screen, ULONG flags, APTR reserved)
	xdef	_ScreenDepth
_ScreenDepth:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-834(a6)
	rts

	section	_ScreenPosition_stub,code

; VOID ScreenPosition(struct Screen * screen, ULONG flags, LONG x1, LONG y1, LONG x2, LONG y2)
	xdef	_ScreenPosition
_ScreenPosition:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	move.l	24(sp),d4
	movea.l	_IntuitionBase,a6
	jsr	-840(a6)
	rts

	section	_ScrollWindowRaster_stub,code

; VOID ScrollWindowRaster(struct Window * win, WORD dx, WORD dy, WORD xMin, WORD yMin, WORD xMax, WORD yMax)
	xdef	_ScrollWindowRaster
_ScrollWindowRaster:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	move.l	12(sp),d1
	move.l	16(sp),d2
	move.l	20(sp),d3
	move.l	24(sp),d4
	move.l	28(sp),d5
	movea.l	_IntuitionBase,a6
	jsr	-846(a6)
	rts

	section	_LendMenus_stub,code

; VOID LendMenus(struct Window * fromwindow, struct Window * towindow)
	xdef	_LendMenus
_LendMenus:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-852(a6)
	rts

	section	_DoGadgetMethodA_stub,code

; ULONG DoGadgetMethodA(struct Gadget * gad, struct Window * win, struct Requester * req, Msg message)
	xdef	_DoGadgetMethodA
_DoGadgetMethodA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_IntuitionBase,a6
	jsr	-858(a6)
	rts

	section	_SetWindowPointerA_stub,code

; VOID SetWindowPointerA(struct Window * win, const struct TagItem * taglist)
	xdef	_SetWindowPointerA
_SetWindowPointerA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-870(a6)
	rts

	section	_TimedDisplayAlert_stub,code

; BOOL TimedDisplayAlert(ULONG alertNumber, CONST_STRPTR string, UWORD height, ULONG time)
	xdef	_TimedDisplayAlert
_TimedDisplayAlert:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	movea.l	_IntuitionBase,a6
	jsr	-882(a6)
	rts

	section	_HelpControl_stub,code

; VOID HelpControl(struct Window * win, ULONG flags)
	xdef	_HelpControl
_HelpControl:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_IntuitionBase,a6
	jsr	-888(a6)
	rts

