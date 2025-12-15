; Generated from SFD file by Novus SFD Parser
; Library: workbench.library
; Base: _WorkbenchBase
; Each function is in its own section for dead code elimination

	xref	_WorkbenchBase

	section	_AddAppWindowA_stub,code

; struct AppWindow * AddAppWindowA(ULONG id, ULONG userdata, struct Window * window, struct MsgPort * msgport, struct TagItem * taglist)
	xdef	_AddAppWindowA
_AddAppWindowA:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-48(a6)
	rts

	section	_RemoveAppWindow_stub,code

; BOOL RemoveAppWindow(struct AppWindow * appWindow)
	xdef	_RemoveAppWindow
_RemoveAppWindow:
	movea.l	4(sp),a0
	movea.l	_WorkbenchBase,a6
	jsr	-54(a6)
	rts

	section	_AddAppIconA_stub,code

; struct AppIcon * AddAppIconA(ULONG id, ULONG userdata, UBYTE * text, struct MsgPort * msgport, BPTR lock, struct DiskObject * diskobj, struct TagItem * taglist)
	xdef	_AddAppIconA
_AddAppIconA:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	24(sp),a3
	movea.l	28(sp),a4
	movea.l	_WorkbenchBase,a6
	jsr	-60(a6)
	rts

	section	_RemoveAppIcon_stub,code

; BOOL RemoveAppIcon(struct AppIcon * appIcon)
	xdef	_RemoveAppIcon
_RemoveAppIcon:
	movea.l	4(sp),a0
	movea.l	_WorkbenchBase,a6
	jsr	-66(a6)
	rts

	section	_AddAppMenuItemA_stub,code

; struct AppMenuItem * AddAppMenuItemA(ULONG id, ULONG userdata, UBYTE * text, struct MsgPort * msgport, struct TagItem * taglist)
	xdef	_AddAppMenuItemA
_AddAppMenuItemA:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-72(a6)
	rts

	section	_RemoveAppMenuItem_stub,code

; BOOL RemoveAppMenuItem(struct AppMenuItem * appMenuItem)
	xdef	_RemoveAppMenuItem
_RemoveAppMenuItem:
	movea.l	4(sp),a0
	movea.l	_WorkbenchBase,a6
	jsr	-78(a6)
	rts

	section	_WBInfo_stub,code

; VOID WBInfo(BPTR lock, STRPTR name, struct Screen * screen)
	xdef	_WBInfo
_WBInfo:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-90(a6)
	rts

	section	_OpenWorkbenchObjectA_stub,code

; BOOL OpenWorkbenchObjectA(STRPTR name, struct TagItem * tags)
	xdef	_OpenWorkbenchObjectA
_OpenWorkbenchObjectA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-96(a6)
	rts

	section	_CloseWorkbenchObjectA_stub,code

; BOOL CloseWorkbenchObjectA(STRPTR name, struct TagItem * tags)
	xdef	_CloseWorkbenchObjectA
_CloseWorkbenchObjectA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-102(a6)
	rts

	section	_WorkbenchControlA_stub,code

; BOOL WorkbenchControlA(STRPTR name, struct TagItem * tags)
	xdef	_WorkbenchControlA
_WorkbenchControlA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-108(a6)
	rts

	section	_AddAppWindowDropZoneA_stub,code

; struct AppWindowDropZone * AddAppWindowDropZoneA(struct AppWindow * aw, ULONG id, ULONG userdata, struct TagItem * tags)
	xdef	_AddAppWindowDropZoneA
_AddAppWindowDropZoneA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-114(a6)
	rts

	section	_RemoveAppWindowDropZone_stub,code

; BOOL RemoveAppWindowDropZone(struct AppWindow * aw, struct AppWindowDropZone * dropZone)
	xdef	_RemoveAppWindowDropZone
_RemoveAppWindowDropZone:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-120(a6)
	rts

	section	_ChangeWorkbenchSelectionA_stub,code

; BOOL ChangeWorkbenchSelectionA(STRPTR name, struct Hook * hook, struct TagItem * tags)
	xdef	_ChangeWorkbenchSelectionA
_ChangeWorkbenchSelectionA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-126(a6)
	rts

	section	_MakeWorkbenchObjectVisibleA_stub,code

; BOOL MakeWorkbenchObjectVisibleA(STRPTR name, struct TagItem * tags)
	xdef	_MakeWorkbenchObjectVisibleA
_MakeWorkbenchObjectVisibleA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-132(a6)
	rts

